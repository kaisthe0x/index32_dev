using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Puts enemies into the arena and keeps track of the living ones. It knows WHO can spawn (<see cref="SpawnPool"/>,
/// under each kit's per-type cap) and WHERE (a free spawn spot of the layout, spread over the map — or near the player
/// in later rounds), builds the enemy from its <see cref="EnemyKit"/>, and reports what happens to it through
/// <see cref="Spawned"/>, <see cref="Died"/> and <see cref="Damaged"/>. WHEN to spawn — the round's quota, interval
/// and concurrent cap — is <see cref="RunManager"/>'s business; the stand-still and edge spawns are
/// <see cref="PressureSpawns"/>'. Built by RunManager for each arena.
/// </summary>
public sealed class EnemySpawner
{
    private static readonly Vector2 SpawnFxOffset = new(0, -22);
    private const string SpawnFxPath = "res://vfx/spawn/enemy_spawn.tscn";
    private const float SpawnFxLife = 1.2f;

    /// <summary>The roster the continuous spawner draws from (uniform random) — a mixed assortment of grunts plus the
    /// stationary sleeper (Nasen). Wardens (Kroj) are elite/pivot-only, not part of the trickle.</summary>
    public static readonly EnemyKit[] SpawnPool =
    {
        EnemyKits.Kebus, EnemyKits.Baghel, EnemyKits.Mazab, EnemyKits.Matat,
        EnemyKits.Tarri, EnemyKits.Breski, EnemyKits.Nasen, // Ein isn't here: it's the stand-still kamikaze (PressureSpawns)
    };

    private readonly Node2D _content;       // the arena's content node: enemies and their spawn puffs are added here
    private readonly LevelLayout? _layout;
    private readonly Player _player;
    private readonly ArenaGround _ground;
    private readonly Sfx _sfx;
    private readonly PackedScene _spawnFx = GD.Load<PackedScene>(SpawnFxPath);
    private readonly HashSet<Enemy> _enemies = new();                  // every living spawned enemy (quota + optional)
    private readonly Dictionary<Enemy, Vector2> _spotOf = new();       // spot-spawned enemy → the EnemySpawns spot it holds
    private readonly Dictionary<string, PackedScene> _enemyScenes = new(); // kit scene path → loaded scene

    /// <summary>An enemy was put into the arena.</summary>
    public event System.Action<Enemy>? Spawned;
    /// <summary>A tracked enemy died (it is already off the living list and its spot is free).</summary>
    public event System.Action<Enemy>? Died;
    /// <summary>A tracked enemy took damage: the enemy, the amount, and who dealt it (null if they are gone).</summary>
    public event System.Action<Enemy, float, Node?>? Damaged;

    public EnemySpawner(Node2D content, LevelLayout? layout, Player player, ArenaGround ground, Sfx sfx)
    {
        _content = content;
        _layout = layout;
        _player = player;
        _ground = ground;
        _sfx = sfx;
    }

    /// <summary>Every living enemy this spawner put in.</summary>
    public IReadOnlyCollection<Enemy> Living => _enemies;

    /// <summary>A random kit from the pool that is UNDER its per-type concurrent cap (uncapped kits always qualify);
    /// null if every kit is currently at cap.</summary>
    public EnemyKit? PickKit(int round)
    {
        var eligible = new List<EnemyKit>();
        foreach (EnemyKit kit in SpawnPool)
            if (LivingOfType(kit.Id) < EffectiveCap(kit, round))
                eligible.Add(kit);
        return eligible.Count == 0 ? null : eligible[(int)(GD.Randi() % (uint)eligible.Count)];
    }

    /// <summary>A kit's current per-type concurrent cap: its <see cref="EnemyKit.SpawnCap"/> base + 1 per
    /// <see cref="Rounds.KitCapGrowthRounds"/> rounds. Kits with no cap are uncapped.</summary>
    private static int EffectiveCap(EnemyKit kit, int round) =>
        kit.SpawnCap is int cap ? cap + round / Rounds.KitCapGrowthRounds : int.MaxValue;

    /// <summary>How many living enemies of type <paramref name="id"/> are currently tracked.</summary>
    public int LivingOfType(string id)
    {
        int n = 0;
        foreach (Enemy e in _enemies)
            if (GodotObject.IsInstanceValid(e) && e.EnemyId == id)
                n += 1;
        return n;
    }

    /// <summary>Spawn ONE enemy from a kit: at a free spawn spot (<see cref="PickSpawnSpot"/>), where it patrols until it
    /// notices the player — or, from <see cref="Rounds.NearSpawnFromRound"/>, for <see cref="NearShare"/> of the grunts
    /// (and whenever every spot is held), near the player (<see cref="NearPlayerSpot"/>). False if there's nowhere to put
    /// it yet.</summary>
    public bool SpawnFromPool(EnemyKit kit, int round)
    {
        bool stationary = kit.Movement == EnemyMovement.Stationary;
        bool nearAllowed = !stationary && round >= Rounds.NearSpawnFromRound; // a stationary kit always uses a spot
        bool near = nearAllowed && GD.Randf() < NearShare(round);
        Vector2? at = near ? NearPlayerSpot() : null;
        Vector2? spot = at == null ? PickSpawnSpot() : null;
        at ??= spot ?? (nearAllowed && !near ? NearPlayerSpot() : null);
        if (at is not Vector2 pos)
            return false;
        var enemy = SpawnAt(kit, pos);
        if (spot != null)
            _spotOf[enemy] = pos;
        return true;
    }

    private static float NearShare(int r) =>
        Mathf.Min(Rounds.NearShareMax, Rounds.NearShareBase + Rounds.NearShareStep * (r - Rounds.NearSpawnFromRound));

    /// <summary>Put an enemy from <paramref name="kit"/> at <paramref name="at"/>: puff, wire its died/damaged signals, track
    /// it, and announce it (<see cref="Spawned"/>).</summary>
    public Enemy SpawnAt(EnemyKit kit, Vector2 at)
    {
        SpawnFx(at);
        var enemy = SpawnEnemy(kit, at);
        var e = enemy; // stable capture for the bound handlers
        enemy.Connect(Enemy.SignalName.Died, Callable.From(() => OnDied(e)));
        enemy.Connect(Enemy.SignalName.Damaged, Callable.From((float amount, Node? source) => Damaged?.Invoke(e, amount, source)));
        _enemies.Add(enemy);
        Spawned?.Invoke(enemy);
        return enemy;
    }

    private void OnDied(Enemy enemy)
    {
        _enemies.Remove(enemy);
        _spotOf.Remove(enemy); // its spot is free again
        Died?.Invoke(enemy);
    }

    /// <summary>A FREE EnemySpawns spot (no living enemy holds it), spreading the enemies over the map: the one farthest
    /// from the spots already held (a random one if none are), among those at least <see cref="Rounds.SpawnMinDistance"/>
    /// from the player — or, if every free spot is closer than that, the free one farthest from him. Null if every spot
    /// is held (or the layout has none).</summary>
    private Vector2? PickSpawnSpot()
    {
        var spots = _layout?.EnemySpawns();
        if (spots == null)
            return null;
        var held = new List<Vector2>(_spotOf.Values);
        var free = spots.FindAll(s => !held.Contains(s));
        if (free.Count == 0)
            return null;
        Vector2 player = _player.GlobalPosition;
        var fair = free.FindAll(s => s.DistanceTo(player) >= Rounds.SpawnMinDistance);
        if (fair.Count == 0)
            return MaxBy(free, s => s.DistanceTo(player));
        if (held.Count == 0)
            return fair[(int)(GD.Randi() % (uint)fair.Count)];
        return MaxBy(fair, s =>
        {
            float nearest = float.MaxValue;
            foreach (Vector2 h in held)
                nearest = Mathf.Min(nearest, s.DistanceTo(h));
            return nearest;
        });
    }

    private static Vector2 MaxBy(List<Vector2> points, System.Func<Vector2, float> score)
    {
        Vector2 best = points[0];
        foreach (Vector2 p in points)
            if (score(p) > score(best))
                best = p;
        return best;
    }

    /// <summary>A NEAR-PLAYER spawn: a tile on the floor he's standing on, <see cref="Rounds.NearSpawnMin"/>..
    /// <see cref="Rounds.NearSpawnMax"/> px away and BEHIND him (opposite his facing) so it doesn't land in the swing he's
    /// already making — he has to turn. Null only if the layout has no walkable run.</summary>
    private Vector2? NearPlayerSpot() =>
        _ground.PickSurface(_player.GlobalPosition, Rounds.NearSpawnMin, Rounds.NearSpawnMax, -_player.Facing);

    /// <summary>Build an enemy from <paramref name="kit"/> — its scene, id, name and tuning — at <paramref name="pos"/>.
    /// The Lira it drops comes from its tier unless the kit names an amount (Wardens do).</summary>
    private Enemy SpawnEnemy(EnemyKit kit, Vector2 pos)
    {
        var enemy = EnemyScene(kit.Scene).Instantiate<Enemy>();
        enemy.EnemyId = kit.Id;
        enemy.DisplayName = kit.DisplayName;
        kit.Tune(enemy);
        enemy.LiraDrop = kit.LiraDrop ?? LiraForTier(kit.Tier);
        enemy.Position = pos;
        _content.AddChild(enemy);
        return enemy;
    }

    /// <summary>The packed scene at <paramref name="path"/>, loaded once and kept (a kit's scene is spawned many times).</summary>
    private PackedScene EnemyScene(string path)
    {
        if (!_enemyScenes.TryGetValue(path, out var scene))
            _enemyScenes[path] = scene = GD.Load<PackedScene>(path);
        return scene;
    }

    /// <summary>Default Lira dropped by an enemy of a given tier (a kit can override it).</summary>
    private static int LiraForTier(EnemyTier tier) => tier switch
    {
        EnemyTier.Chip => 1,
        EnemyTier.Mid => 2,
        EnemyTier.Strong => 3,
        _ => 1,
    };

    private void SpawnFx(Vector2 pos)
    {
        var fx = _spawnFx.Instantiate<Node2D>();
        fx.ZIndex = WorldZ.SpawnFx;
        _content.AddChild(fx);
        Nodes.PlaceAt(fx, pos + SpawnFxOffset);
        _sfx.PlayAt("enemy_spawn", pos);
        _content.GetTree().CreateTimer(1.2).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(fx))
                fx.QueueFree();
        };
    }
}
