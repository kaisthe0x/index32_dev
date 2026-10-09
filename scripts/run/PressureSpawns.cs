using Godot;

namespace MyGame;

/// <summary>
/// The two anti-camping spawns, outside the round's quota (tuning in <see cref="Rounds"/>): a player who STANDS STILL
/// draws kamikazes (Ein), and a player who HUGS AN END of the arena draws a Ventilator that blows him off it. Each has
/// its own clock. <see cref="RunManager"/> ticks this during normal play only (not paused, dead or spawning) and builds
/// a new one for each arena, which resets the clocks.
/// </summary>
public sealed class PressureSpawns
{
    private readonly Player _player;
    private readonly EnemySpawner _spawner;
    private readonly ArenaGround _ground;
    private readonly float _arenaLeft, _arenaRight; // the arena's horizontal ends (LevelLayout.HorizontalSpan)
    private Vector2 _stillAnchor;      // where the player has been standing still since
    private float _stillTime = 0.0f;   // how long he's stayed within Rounds.StillRadius of it
    private float _kamikazeCd = 0.0f;  // until the next kamikaze may spawn while he stays put
    private float _edgeTime = 0.0f;    // how long the player has been within Rounds.EdgeZone of an end
    private float _ventilatorCd = 0.0f; // until the next Ventilator may come (starts when one dies)

    public PressureSpawns(Player player, EnemySpawner spawner, ArenaGround ground, float arenaLeft, float arenaRight)
    {
        _player = player;
        _spawner = spawner;
        _ground = ground;
        _arenaLeft = arenaLeft;
        _arenaRight = arenaRight;
        _spawner.Died += OnEnemyDied;
    }

    /// <summary>One tick of both clocks, in round <paramref name="round"/>.</summary>
    public void Tick(float delta, int round)
    {
        TickStandStill(delta, round);
        TickEdge(delta, round);
    }

    private void OnEnemyDied(Enemy enemy)
    {
        if (enemy.EnemyId == EnemyIds.Ventilator)
            _ventilatorCd = Rounds.VentilatorCooldown; // the next one waits
    }

    /// <summary>STAND-STILL PRESSURE: from <see cref="Rounds.KamikazeFromRound"/>, a player who stays within
    /// <see cref="Rounds.StillRadius"/> for <see cref="Rounds.StillTime"/> gets a kamikaze (Ein) near him, then another
    /// every <see cref="Rounds.KamikazeInterval"/> while he stays put (at most <see cref="Rounds.KamikazeMax"/> alive).
    /// Moving away resets it. Only runs during normal play (not paused, not dead, not spawning).</summary>
    private void TickStandStill(float delta, int round)
    {
        Vector2 at = _player.GlobalPosition;
        if (round < Rounds.KamikazeFromRound || at.DistanceTo(_stillAnchor) > Rounds.StillRadius)
        {
            _stillAnchor = at;
            _stillTime = 0.0f;
            _kamikazeCd = 0.0f;
            return;
        }
        if (_player.IsChannelingSurge())
            return; // Nem's sleep pauses the clock (ones already diving still come)
        _stillTime += delta;
        _kamikazeCd -= delta;
        if (_stillTime < Rounds.StillTime || _kamikazeCd > 0.0f || _spawner.LivingOfType(EnemyIds.Ein) >= KamikazeMax(round))
            return;
        _kamikazeCd = KamikazeInterval(round);
        _spawner.SpawnAt(EnemyKits.Ein, KamikazeSpot(at));
    }

    /// <summary>THE EDGE ENEMY: from <see cref="Rounds.VentilatorFromRound"/>, a player who stays within
    /// <see cref="Rounds.EdgeZone"/> of either end of the arena for <see cref="Rounds.EdgeDwell"/> gets a Ventilator on his
    /// floor, on the INLAND side (<see cref="EdgeInland"/>), so its wind blows him outward — off the edge unless he air-jumps
    /// or dashes back. At most <see cref="Rounds.VentilatorMax"/> alive; the next waits <see cref="Rounds.VentilatorCooldown"/>
    /// after one dies. Leaving the edge resets the dwell.</summary>
    private void TickEdge(float delta, int round)
    {
        _ventilatorCd = Mathf.Max(_ventilatorCd - delta, 0.0f);
        int inland = round >= Rounds.VentilatorFromRound ? EdgeInland(_player.GlobalPosition.X) : 0;
        if (inland == 0)
        {
            _edgeTime = 0.0f;
            return;
        }
        _edgeTime += delta;
        if (_edgeTime < Rounds.EdgeDwell || _ventilatorCd > 0.0f || _spawner.LivingOfType(EnemyIds.Ventilator) >= Rounds.VentilatorMax)
            return;
        Vector2 player = _player.GlobalPosition;
        // Only a tile actually inland of him — PickSurface falls back to either side when one side has none, and a
        // Ventilator on the OUTER side would blow him back into the arena. None yet = try again next tick.
        if (_ground.PickSurface(player, Rounds.VentilatorSpawnMin, Rounds.VentilatorSpawnMax, inland) is Vector2 at
            && Mathf.Sign(at.X - player.X) == inland)
            _spawner.SpawnAt(EnemyKits.Ventilator, at);
    }

    /// <summary>+1 if <paramref name="x"/> is within <see cref="Rounds.EdgeZone"/> of the arena's LEFT end (inland is to
    /// the right), -1 if of its RIGHT end, 0 if it's at neither.</summary>
    private int EdgeInland(float x)
    {
        if (_arenaRight <= _arenaLeft)
            return 0;
        if (x - _arenaLeft <= Rounds.EdgeZone)
            return 1;
        if (_arenaRight - x <= Rounds.EdgeZone)
            return -1;
        return 0;
    }

    private static float KamikazeInterval(int r) => Mathf.Max(Rounds.KamikazeIntervalMin,
        Rounds.KamikazeIntervalBase * Mathf.Pow(Rounds.KamikazeIntervalDecay, r - Rounds.KamikazeFromRound));

    private static int KamikazeMax(int r) => Mathf.Min(Rounds.KamikazeMaxCap,
        Rounds.KamikazeMaxBase + (r - Rounds.KamikazeFromRound) / Rounds.KamikazeMaxGrowthRounds);

    /// <summary>Where a kamikaze appears: <see cref="Rounds.KamikazeDistance"/> to a random side of the player (the other
    /// side if that one is inside a wall) and up to <see cref="Rounds.KamikazeHeight"/> above, under any ceiling — far
    /// enough that he has time to react.</summary>
    private Vector2 KamikazeSpot(Vector2 player)
    {
        int side = GD.Randf() < 0.5f ? -1 : 1;
        float up = Mathf.Min(Rounds.KamikazeHeight, _ground.HeadroomAbove(player, Rounds.KamikazeHeight));
        Vector2 spot = player + new Vector2(side * Rounds.KamikazeDistance, -up);
        return _ground.InsideWall(spot) ? player + new Vector2(-side * Rounds.KamikazeDistance, -up) : spot;
    }
}
