using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// The run driver + the <c>arena.tscn</c> root. Builds ONE arena and runs the endless ROUND
/// loop (<c>docs/game-loop.md</c>, tuning in <see cref="Rounds"/>): each round trickles a hidden QUOTA of enemies in from
/// a mixed roster at the layout's spawn spots (where they patrol until they notice him), up to a concurrent cap;
/// spawning stops once the quota has spawned, and the last few STRAGGLERS hunt him down; the round clears when they're
/// all dead, and the next starts at once (a ROUND banner, no break). From a set round, standing still draws KAMIKAZES
/// (Ein — no drops, not in the quota) so he can't camp. Banks Ruh on hits, drops
/// Lira on every kill (+ a per-kit chance of a Fada Fig), spawns a mystery box (spend figs for a stingy powerful-buff
/// gamble), and restarts the run on death. Owns the player spawn, camera follow, and death/spawn flair. C# port of <c>run_manager.gd</c>.
///
/// <para>Talks to the C# body tree (Player/Enemy), the collectibles (Lira, FadaFig), the MysteryBox, and the autoloads
/// (Music/Sfx via <c>/root/*</c>) directly.</para>
/// </summary>
[GlobalClass]
public partial class RunManager : Node2D
{
    private static readonly Vector2 SpawnFxOffset = new(0, -22);
    private static readonly Vector2 DamageNumberOffset = new(0, -42);
    private const float DeathY = 320.0f;   // falling below this world Y kills the player
    private const string StartCharacter = "khalid";


    /// <summary>The roster the continuous spawner draws from (uniform random) — a mixed assortment of grunts plus the
    /// stationary sleeper (Nasen). Wardens (Kroj) are elite/pivot-only, not part of the trickle.</summary>
    private static readonly GDict[] SpawnPool =
    {
        EnemyKits.KEBUS, EnemyKits.BAGHEL, EnemyKits.MAZAB, EnemyKits.MATAT,
        EnemyKits.TARRI, EnemyKits.BRESKI, EnemyKits.NASEN, // Ein isn't here: it's the stand-still kamikaze (TickPressure)
    };

    // Camera follow: a CRITICALLY DAMPED SPRING (SmoothDamp) toward Khalid — it has velocity, so after a sudden jump
    // (a blink dash) it accelerates smoothly, glides and decelerates with no overshoot, and a stop eases out instead of
    // "lagging into place". CamSmoothTime ≈ time to catch up; lower = tighter (trails a running Khalid by ~13 px at
    // 0.055 s). Fast VERTICAL motion (launch orbs) tightens toward CamSmoothTimeFast so he never leaves the frame.
    private const float CamSmoothTime = 0.055f;
    private const float CamSmoothTimeFast = 0.015f;
    private const float CamTightenStart = 600.0f;
    private const float CamTightenFull = 1200.0f;
    private Vector2 _camVel = Vector2.Zero; // the follow spring's velocity (zeroed whenever the camera is placed directly)
    private static readonly Vector2 CamZoomNormal = new(.5f, .5f);
    private static readonly Vector2 CamZoomDeath = new(3.0f, 3.0f);
    private static readonly Vector2 CamZoomSpawn = new(2, 2);
    private const float DeathHold = 0.7f;
    private const float DeathFadeIn = 0.55f;
    private const float DeathFadeOut = 0.6f;
    private const float DeathFreeze = 0.5f;
    private const float FallDeathHold = 1.2f;  // min seconds the run lingers after a fall-death (the fall sound may run longer)

    [Export] public NodePath player_path = "Player";

    private Player _player;
    private Camera2D _camera;

    // --- round state (see Rounds) — only NON-optional enemies are "quota" enemies ---
    private int _round = 0;            // the current round (0 = before round 1 — it starts on the first tick of play)
    private int _quota = 0;            // this round's hidden enemy count
    private int _spawned = 0;          // quota enemies spawned so far this round
    private int _killed = 0;           // quota enemies killed this round
    private int _alive = 0;            // living quota enemies (the concurrent cap looks at this)
    private float _spawnAccum = 0.0f;  // seconds accrued toward the next spawn
    private readonly System.Collections.Generic.HashSet<Enemy> _enemies = new(); // every living spawned enemy (quota + optional)
    private readonly System.Collections.Generic.Dictionary<Enemy, Vector2> _spotOf = new(); // spot-spawned enemy → the EnemySpawns spot it holds
    private Vector2 _stillAnchor;      // where the player has been standing still since (stand-still pressure)
    private float _stillTime = 0.0f;   // how long he's stayed within Rounds.StillRadius of it
    private float _kamikazeCd = 0.0f;  // until the next kamikaze may spawn while he stays put
    private float _arenaLeft, _arenaRight; // the arena's horizontal ends (LevelLayout.HorizontalSpan) — the Ventilator's edges
    private float _edgeTime = 0.0f;        // how long the player has been within Rounds.EdgeZone of an end
    private float _ventilatorCd = 0.0f;    // until the next Ventilator may come (starts when one dies)
    private Node2D _content;
    private ColorRect _bg;
    private Sprite2D _bgSky;
    private Vector2 _bgImgSize;
    private LevelLayout _layout;
    private PerkLedger _perks;                   // this run's Dekken perks (stock, active, owned)
    private MysteryBox _box;                     // Fast Travel's destination

    private const string StageDir = "res://scenes/levels/stage1/";
    private Vector2 _playerSpawn = Vector2.Zero;
    private bool _deadPrev = false;
    private float _deathHold = 0.0f;
    private bool _spawning = false;
    private Tween _camTween;
    private Polygon2D _deathOverlay;
    private float _deathTuneLeft = 0.0f;

    // --- bridges (cached in _Ready) ---
    private Music _music;
    private Sfx _sfx;
    private PackedScene _enemyScene, _spawnFx, _ruhOrb, _liraScene, _fadaFigScene;

    public override void _Ready()
    {
        EnsureVialActions();
        _player = GetNodeOrNull<Player>(player_path);
        _camera = GetNodeOrNull<Camera2D>("Camera2D");
        _music = GetNode<Music>("/root/Music");
        _sfx = GetNode<Sfx>("/root/Sfx");
        _enemyScene = GD.Load<PackedScene>("res://scenes/enemy.tscn");
        _spawnFx = GD.Load<PackedScene>("res://vfx/spawn/enemy_spawn.tscn");
        _ruhOrb = GD.Load<PackedScene>("res://vfx/character/khalid/ruh_orb/ruh_orb.tscn");
        _liraScene = GD.Load<PackedScene>("res://scenes/lira.tscn");
        _fadaFigScene = GD.Load<PackedScene>("res://scenes/fada_fig.tscn");

        Engine.TimeScale = 1.0;
        Input.MouseMode = Input.MouseModeEnum.Hidden; // hide the cursor during play; menus re-show it while open
        AddGlow();
        BuildBg();
        BuildFloor();
        if (_player != null)
            _player.character = StartCharacter;
        BuildArena();
        if (_player != null)
            _player.spawn();
        if (_camera != null)
            PlaceAt(_camera, _playerSpawn + new Vector2(0, -30));
        ChooseAttack();
    }

    public override void _PhysicsProcess(double deltaD)
    {
        if (_player == null)
            return;
        float delta = (float)deltaD;

        if (_player.is_dead())
        {
            HandleDeath(delta);
            return;
        }
        if (_player.is_spawning())
        {
            HandleSpawn(delta);
            return;
        }
        if (_player.GlobalPosition.Y > DeathY)
        {
            _player.fall_to_death(); // fell off the arena — that's a death (next tick runs the death flow)
            return;
        }
        if (_spawning)
        {
            _spawning = false;
            ZoomTo(CamZoomNormal, 0.4f);
        }
        TickRound(delta);
        TickPressure(delta);
        TickEdge(delta);
        FollowCamera(delta);
    }

    // --- arena building -------------------------------------------------------

    /// <summary>Every hand-painted layout variant for the stage (<c>stage1_v*.tscn</c>). Auto-uses whatever exists —
    /// add a variant to the folder and it joins the random pool with no code change.</summary>
    private static string[] StageLayoutPaths()
    {
        var list = new System.Collections.Generic.List<string>();
        using var da = DirAccess.Open(StageDir);
        if (da != null)
        {
            da.ListDirBegin();
            for (string f = da.GetNext(); f != ""; f = da.GetNext())
            {
                if (da.CurrentIsDir())
                    continue;
                string name = f.TrimSuffix(".remap"); // exported builds serve .tscn.remap
                if (name.StartsWith("stage1_v") && name.EndsWith(".tscn"))
                    list.Add(StageDir + name);
            }
            da.ListDirEnd();
        }
        return list.ToArray();
    }

    private void BuildArena()
    {
        _music.play_stage("stage1"); // the stage music starts as the arena loads (the colour-scheme screen stays silent)
        _round = 0; // round 1 starts on the first tick of play (after the attack pick + spawn)
        _quota = 0;
        _spawned = 0;
        _killed = 0;
        _alive = 0;
        _spawnAccum = 0.0f;
        _enemies.Clear(); // old enemies free with _content
        _spotOf.Clear();
        _stillTime = 0.0f;
        _kamikazeCd = 0.0f;
        _edgeTime = 0.0f;
        _ventilatorCd = 0.0f;
        PushRoundHud();
        if (_content != null && IsInstanceValid(_content))
            _content.QueueFree();
        _content = new Node2D();
        AddChild(_content);

        Color tint = Terrain.BackgroundTint;
        if (Terrain.BackgroundTexture() != null)
            tint.A = Terrain.BackgroundTintAlpha;
        _bg.Color = tint;

        // Load one of the stage's hand-painted layouts at RANDOM (terrain + collision + ground tiles for spawning).
        _layout = null;
        var layoutPaths = StageLayoutPaths();
        if (layoutPaths.Length > 0)
        {
            var scene = GD.Load<PackedScene>(layoutPaths[GD.Randi() % (uint)layoutPaths.Length]);
            _layout = scene?.Instantiate() as LevelLayout;
            if (_layout != null)
            {
                _layout.Position = Vector2.Zero; // ignore any authored root offset — sit the layout at origin
                _content.AddChild(_layout);
            }
        }
        else
        {
            GD.PushWarning("RunManager: no stage1_v*.tscn layouts under scenes/levels/stage1/ — arena will be empty.");
        }
        _playerSpawn = _layout != null ? _layout.PlayerSpawn() : Vector2.Zero;
        (_arenaLeft, _arenaRight) = _layout?.HorizontalSpan() ?? (0.0f, 0.0f);

        foreach (var op in _layout?.Orbs() ?? new System.Collections.Generic.List<Vector2>())
            _content.AddChild(new LaunchOrb { Position = op });

        // The stalls are scenes the layout places (LevelLayout.Placed) — all three are required.
        _box = _layout?.Placed<MysteryBox>();
        var needlePoint = _layout?.Placed<NeedlePointStall>();
        var dekken = _layout?.Placed<DekkenStall>();
        if (_box == null || needlePoint == null || dekken == null)
            GD.PushError("RunManager: the layout must place all three stall scenes (scenes/things/: mystery_box, needle_point, dekken).");
        if (_box != null)
        {
            if (_layout.BoxSpots(hard: false).Count == 0)
                GD.PushError("RunManager: the layout has no easy box spot (BoxSpots/Easy markers) — the box stays where it was placed and can't relocate.");
            _box.Setup(new BoxLedger(_player), _layout.BoxSpots(hard: false), _layout.BoxSpots(hard: true));
        }
        if (needlePoint != null)
            needlePoint.Ledger = new ShotLedger(_player); // this run's Needle Point ranks
        _perks = new PerkLedger(_player, FastTravelToBox, PushVialHud);
        if (dekken != null)
            dekken.Ledger = _perks;
        if (_layout != null && _layout.EnemySpawns().Count == 0)
            GD.PushError("RunManager: the layout has no EnemySpawns markers — no enemies can spawn.");

        if (_player != null)
            PlaceAt(_player, _playerSpawn);
    }

    // --- rounds ---------------------------------------------------------------

    /// <summary>Advance the round loop: start round 1 on the first tick of play, then trickle quota enemies in (one per
    /// interval, under the concurrent cap) until the quota has spawned. The CLEAR is detected on the last kill
    /// (<see cref="OnEnemyDied"/>), and the next round starts right then — there's no break.</summary>
    private void TickRound(float delta)
    {
        if (_round == 0)
        {
            StartRound(1);
            return;
        }
        if (_spawned >= _quota)
            return; // quota fully spawned — wait for the clear
        _spawnAccum += delta;
        if (_spawnAccum < SpawnInterval(_round) || _alive >= ConcurrentCap(_round))
            return;
        var kit = PickSpawnKit(); // a random kit under its per-type cap (or null if every kit is at cap)
        if (kit == null)
            return; // every kit is at its per-type cap — try again next tick
        if (SpawnOne(kit))
            _spawnAccum = 0.0f; // else nowhere to put it yet (every spot held) — try again next tick
    }

    private void StartRound(int round)
    {
        _round = round;
        _quota = Quota(round);
        _spawned = 0;
        _killed = 0;
        _player?.notify_round_start(); // round-scoped perks re-arm (Shield) / fire (Prepared)
        _spawnAccum = SpawnInterval(round); // first enemy arrives immediately
        PushRoundHud(); // the HUD plays the ROUND n intro for a new round
        _sfx.play("round_start");
    }

    /// <summary>The last quota enemy of the round died: timed perks spend a round, Dekken restocks, and the next round
    /// starts at once — the player never gets a break.</summary>
    private void ClearRound()
    {
        _perks.OnRoundClear();
        StartRound(_round + 1);
    }

    private static int Quota(int r) =>
        Mathf.RoundToInt(Rounds.QuotaBase + Rounds.QuotaLinear * r + Rounds.QuotaQuad * r * r);

    private static int ConcurrentCap(int r) =>
        Mathf.Min(Rounds.CapBase + (r - 1) / Rounds.CapGrowthRounds, Rounds.CapMax);

    private static float SpawnInterval(int r) =>
        Mathf.Max(Rounds.IntervalMin, Rounds.IntervalBase * Mathf.Pow(Rounds.IntervalDecay, r - 1));

    /// <summary>Push the round state to the HUD: the round number and how many quota enemies remain (revealed only
    /// once few remain).</summary>
    private void PushRoundHud()
    {
        int left = _quota - _killed;
        GetNodeOrNull<HUD>("/root/HUD")?.SetRound(_round, left <= Rounds.ShowLeftAt ? left : 0, SaveData.RoundsRecord());
    }

    /// <summary>A random kit from the pool that is UNDER its per-type concurrent cap (uncapped kits always qualify);
    /// null if every kit is currently at cap.</summary>
    private GDict PickSpawnKit()
    {
        var eligible = new System.Collections.Generic.List<GDict>();
        foreach (GDict kit in SpawnPool)
            if (LivingOfType(kit["id"].AsString()) < EffectiveCap(kit))
                eligible.Add(kit);
        return eligible.Count == 0 ? null : eligible[(int)(GD.Randi() % (uint)eligible.Count)];
    }

    /// <summary>A kit's current per-type concurrent cap: its <c>spawn_cap</c> base + 1 per
    /// <see cref="Rounds.KitCapGrowthRounds"/> rounds. Kits with no <c>spawn_cap</c> are uncapped.</summary>
    private int EffectiveCap(GDict kit) =>
        kit.ContainsKey("spawn_cap") ? kit["spawn_cap"].AsInt32() + _round / Rounds.KitCapGrowthRounds : int.MaxValue;

    /// <summary>How many living enemies of type <paramref name="id"/> are currently tracked.</summary>
    private int LivingOfType(string id)
    {
        int n = 0;
        foreach (Enemy e in _enemies)
            if (IsInstanceValid(e) && e.enemy_id == id)
                n += 1;
        return n;
    }

    /// <summary>Spawn ONE enemy from a kit: at a free spawn spot (<see cref="PickSpawnSpot"/>), where it patrols until it
    /// notices the player — or, from <see cref="Rounds.NearSpawnFromRound"/>, for <see cref="NearShare"/> of the grunts
    /// (and whenever every spot is held), near the player (<see cref="NearPlayerSpot"/>). False if there's nowhere to put
    /// it yet.</summary>
    private bool SpawnOne(GDict kit)
    {
        bool stationary = kit.ContainsKey("movement") && kit["movement"].AsInt32() == (int)EnemyMovement.Stationary;
        bool nearAllowed = !stationary && _round >= Rounds.NearSpawnFromRound; // a stationary kit always uses a spot
        bool near = nearAllowed && GD.Randf() < NearShare(_round);
        Vector2? at = near ? NearPlayerSpot() : null;
        Vector2? spot = at == null ? PickSpawnSpot() : null;
        at ??= spot ?? (nearAllowed && !near ? NearPlayerSpot() : null);
        if (at is not Vector2 pos)
            return false;
        var enemy = SpawnAt(kit, pos);
        if (enemy != null && spot != null)
            _spotOf[enemy] = pos;
        return enemy != null;
    }

    private static float NearShare(int r) =>
        Mathf.Min(Rounds.NearShareMax, Rounds.NearShareBase + Rounds.NearShareStep * (r - Rounds.NearSpawnFromRound));

    /// <summary>Put an enemy from <paramref name="kit"/> at <paramref name="at"/>: puff, wire its died/damaged signals, track
    /// it, and (unless optional) count it toward the round quota + the concurrent cap.</summary>
    private Enemy SpawnAt(GDict kit, Vector2 at)
    {
        SpawnFx(at);
        var enemy = SpawnEnemy(kit, at);
        if (enemy == null)
            return null;
        var e = enemy; // stable capture for the bound handlers
        enemy.Connect(Enemy.SignalName.died, Callable.From(() => OnEnemyDied(e)));
        enemy.Connect(Enemy.SignalName.damaged, Callable.From((float amount, Node source) => OnEnemyDamaged(amount, source, e)));
        _enemies.Add(enemy);
        if (!enemy.optional)
        {
            _spawned += 1;
            _alive += 1;
            UpdateStragglers();
        }
        return enemy;
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
        var held = new System.Collections.Generic.List<Vector2>(_spotOf.Values);
        var free = spots.FindAll(s => !held.Contains(s));
        if (free.Count == 0)
            return null;
        Vector2 player = _player?.GlobalPosition ?? _playerSpawn;
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

    private static Vector2 MaxBy(System.Collections.Generic.List<Vector2> points, System.Func<Vector2, float> score)
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
    private Vector2? NearPlayerSpot()
    {
        if (_player == null)
            return null;
        return PickGroundSurface(_player.GlobalPosition, Rounds.NearSpawnMin, Rounds.NearSpawnMax, -_player.facing);
    }

    /// <summary>STRAGGLERS: once the round has fully spawned and only <see cref="Rounds.StragglerCount"/> or fewer quota
    /// enemies are left, they all hunt the player — so a round never stalls on one he can't find.</summary>
    private void UpdateStragglers()
    {
        if (_spawned < _quota || _quota - _killed > Rounds.StragglerCount)
            return;
        foreach (Enemy e in _enemies)
            if (IsInstanceValid(e) && !e.optional)
                e.hunt(Rounds.StragglerSpeedMult);
    }

    /// <summary>STAND-STILL PRESSURE: from <see cref="Rounds.KamikazeFromRound"/>, a player who stays within
    /// <see cref="Rounds.StillRadius"/> for <see cref="Rounds.StillTime"/> gets a kamikaze (Ein) near him, then another
    /// every <see cref="Rounds.KamikazeInterval"/> while he stays put (at most <see cref="Rounds.KamikazeMax"/> alive).
    /// Moving away resets it. Only runs during normal play (not paused, not dead, not spawning).</summary>
    private void TickPressure(float delta)
    {
        if (_player == null)
            return;
        Vector2 at = _player.GlobalPosition;
        if (_round < Rounds.KamikazeFromRound || at.DistanceTo(_stillAnchor) > Rounds.StillRadius)
        {
            _stillAnchor = at;
            _stillTime = 0.0f;
            _kamikazeCd = 0.0f;
            return;
        }
        if (_player.is_channeling_surge())
            return; // Nem's sleep pauses the clock (ones already diving still come)
        _stillTime += delta;
        _kamikazeCd -= delta;
        if (_stillTime < Rounds.StillTime || _kamikazeCd > 0.0f || LivingOfType(EnemyIds.Ein) >= KamikazeMax(_round))
            return;
        _kamikazeCd = KamikazeInterval(_round);
        SpawnAt(EnemyKits.EIN, KamikazeSpot(at));
    }

    /// <summary>THE EDGE ENEMY: from <see cref="Rounds.VentilatorFromRound"/>, a player who stays within
    /// <see cref="Rounds.EdgeZone"/> of either end of the arena for <see cref="Rounds.EdgeDwell"/> gets a Ventilator on his
    /// floor, on the INLAND side (<see cref="EdgeInland"/>), so its wind blows him outward — off the edge unless he air-jumps
    /// or dashes back. At most <see cref="Rounds.VentilatorMax"/> alive; the next waits <see cref="Rounds.VentilatorCooldown"/>
    /// after one dies. Leaving the edge resets the dwell.</summary>
    private void TickEdge(float delta)
    {
        _ventilatorCd = Mathf.Max(_ventilatorCd - delta, 0.0f);
        int inland = _player != null && _round >= Rounds.VentilatorFromRound ? EdgeInland(_player.GlobalPosition.X) : 0;
        if (inland == 0)
        {
            _edgeTime = 0.0f;
            return;
        }
        _edgeTime += delta;
        if (_edgeTime < Rounds.EdgeDwell || _ventilatorCd > 0.0f || LivingOfType(EnemyIds.Ventilator) >= Rounds.VentilatorMax)
            return;
        Vector2 player = _player!.GlobalPosition;
        // Only a tile actually inland of him — PickGroundSurface falls back to either side when one side has none, and a
        // Ventilator on the OUTER side would blow him back into the arena. None yet = try again next tick.
        if (PickGroundSurface(player, Rounds.VentilatorSpawnMin, Rounds.VentilatorSpawnMax, inland) is Vector2 at
            && Mathf.Sign(at.X - player.X) == inland)
            SpawnAt(EnemyKits.VENTILATOR, at);
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
        float up = Mathf.Min(Rounds.KamikazeHeight, HeadroomAbove(player));
        Vector2 spot = player + new Vector2(side * Rounds.KamikazeDistance, -up);
        return InsideWall(spot) ? player + new Vector2(-side * Rounds.KamikazeDistance, -up) : spot;
    }

    private bool InsideWall(Vector2 point)
    {
        var space = GetWorld2D()?.DirectSpaceState;
        if (space == null)
            return false;
        var q = new PhysicsPointQueryParameters2D { Position = point, CollisionMask = (uint)Combat.Layer.World };
        return space.IntersectPoint(q, 1).Count > 0;
    }

    private static readonly Vector2 FastTravelOffset = new(-28, -4); // where Fast Travel drops you, beside the box
    private const float GroundProbeDepth = 600.0f; // how far below the player to look for the floor he's over
    private static readonly Vector2 SpawnClearance = new(24, 40);    // room a spawning ground enemy needs (a bit over a grunt's body)

    /// <summary>Clear vertical space above <paramref name="from"/> up to <see cref="Rounds.KamikazeHeight"/> — so a
    /// kamikaze isn't spawned inside a ceiling. Returns how high it can safely sit.</summary>
    private float HeadroomAbove(Vector2 from)
    {
        var space = GetWorld2D()?.DirectSpaceState;
        if (space == null)
            return Rounds.KamikazeHeight;
        var q = PhysicsRayQueryParameters2D.Create(from, from + new Vector2(0.0f, -(Rounds.KamikazeHeight + 16.0f)), (uint)Combat.Layer.World);
        var hit = space.IntersectRay(q);
        if (hit.Count == 0)
            return Rounds.KamikazeHeight;
        return Mathf.Max(from.Y - hit["position"].As<Vector2>().Y - 14.0f, 0.0f);
    }

    /// <summary>Dekken's Fast Travel: put the player right beside the mystery box.</summary>
    private void FastTravelToBox()
    {
        if (_player == null || _box == null)
            return;
        PlaceAt(_player, _box.GlobalPosition + FastTravelOffset);
        _player.Velocity = Vector2.Zero;
    }

    /// <summary>A random spawn tile ON THE FLOOR <paramref name="from"/> stands on (<see cref="LevelLayout.SpawnSurfacesNear"/>,
    /// found from the ground straight below it — so a grunt never spawns on a platform the player can't reach, or that
    /// can't reach the player) whose horizontal distance from <paramref name="from"/> is in [min,max]; if none fall in
    /// that band, the nearest tile that is still ≥ min away (so it's never adjacent to the player); null only if the
    /// layout has no walkable run at all. A nonzero <paramref name="side"/> (+1 right / -1 left) restricts the band to
    /// that side; if that side has no tile in the band (backed against the arena edge or a pit), it falls back to
    /// either side.</summary>
    private Vector2? PickGroundSurface(Vector2 from, float min, float max, int side = 0)
    {
        float fromX = from.X;
        var near = _layout?.SpawnSurfacesNear(GroundBelow(from));
        if (near == null)
            return null;
        var surfaces = near.FindAll(SpotIsClear); // not inside something solid standing on the tiles (a stall's dais)
        if (surfaces.Count == 0)
            return null;
        if (side != 0)
        {
            var sideBand = new System.Collections.Generic.List<Vector2>();
            foreach (Vector2 s in surfaces)
            {
                float dx = (s.X - fromX) * side; // distance toward the requested side
                if (dx >= min && dx <= max)
                    sideBand.Add(s);
            }
            if (sideBand.Count > 0)
                return sideBand[(int)(GD.Randi() % (uint)sideBand.Count)];
        }
        var band = new System.Collections.Generic.List<Vector2>();
        Vector2? nearestFair = null;
        float nearestFairScore = float.MaxValue;
        Vector2 farthest = surfaces[0];
        float farthestD = -1.0f;
        foreach (Vector2 s in surfaces)
        {
            float d = Mathf.Abs(s.X - fromX);
            if (d >= min && d <= max)
                band.Add(s);
            if (d >= min && d < nearestFairScore) { nearestFairScore = d; nearestFair = s; }
            if (d > farthestD) { farthestD = d; farthest = s; }
        }
        if (band.Count > 0)
            return band[(int)(GD.Randi() % (uint)band.Count)];
        return nearestFair ?? farthest; // band empty → closest tile still ≥min; if even that fails, the farthest we have
    }

    /// <summary>Whether a body standing on <paramref name="surface"/> would be clear of solid collision — the tiles know
    /// nothing about a stall's dais (or any solid prop) built over them, so a floor tile under one would otherwise
    /// spawn an enemy stuck inside it. Checks a <see cref="SpawnClearance"/> box just above the surface against
    /// <see cref="Combat.Layer.World"/> (one-way platforms don't block).</summary>
    private bool SpotIsClear(Vector2 surface)
    {
        var space = GetWorld2D()?.DirectSpaceState;
        if (space == null)
            return true;
        var q = new PhysicsShapeQueryParameters2D
        {
            Shape = new RectangleShape2D { Size = SpawnClearance },
            Transform = new Transform2D(0.0f, surface - new Vector2(0.0f, SpawnClearance.Y / 2.0f + 2.0f)), // 2 px off the floor
            CollisionMask = (uint)Combat.Layer.World,
        };
        return space.IntersectShape(q, 1).Count == 0;
    }

    /// <summary>The ground straight below <paramref name="from"/> (within <see cref="GroundProbeDepth"/>) — so a player
    /// mid-jump still counts as on the floor under him. Over a pit (nothing below), <paramref name="from"/> itself.</summary>
    private Vector2 GroundBelow(Vector2 from)
    {
        var space = GetWorld2D()?.DirectSpaceState;
        if (space == null)
            return from;
        var q = PhysicsRayQueryParameters2D.Create(from + new Vector2(0.0f, -4.0f), from + new Vector2(0.0f, GroundProbeDepth), Combat.GroundMask);
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? hit["position"].As<Vector2>() : from;
    }

    private Enemy SpawnEnemy(GDict kit, Vector2 pos)
    {
        var scene = kit.ContainsKey("scene") ? GD.Load<PackedScene>(kit["scene"].AsString()) : _enemyScene;
        var enemy = (Enemy)scene.Instantiate();
        foreach (var key in kit.Keys)
        {
            string k = key.AsString();
            if (k is "scene" or "tier" or "pos" or "air" or "movement" or "spawn_cap")  // advisory kit metadata, not Enemy properties
                continue;
            if (k == "id")
                enemy.Set("enemy_id", kit[key]);
            else
                enemy.Set(k, kit[key]);
        }
        // Lira drop count defaults from the advisory tier unless the kit set lira_drop explicitly (Wardens do).
        if (!kit.ContainsKey("lira_drop") && kit.ContainsKey("tier"))
            enemy.lira_drop = LiraForTier((EnemyTier)kit["tier"].AsInt32());
        enemy.Position = pos;
        _content.AddChild(enemy);
        return enemy;
    }

    /// <summary>Default Lira dropped by an enemy of a given advisory tier (Wardens override via their kit).</summary>
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
        PlaceAt(fx, pos + SpawnFxOffset);
        _sfx.play_at("enemy_spawn", pos);
        GetTree().CreateTimer(1.2).Timeout += () =>
        {
            if (IsInstanceValid(fx))
                fx.QueueFree();
        };
    }

    private void OnEnemyDied(Enemy enemy)
    {
        // Death fires INSIDE a physics query flush (Hitbox callback), where adding a RigidBody is illegal
        // ("Can't change this state while flushing queries"). Capture the values (the enemy frees) + defer the drop.
        // Every kill pays Lira; a per-kit chance also drops ONE fada_fig (the rare currency). None if it fell off-map.
        Vector2 at = enemy.GlobalPosition;
        int lira = enemy.lira_drop;
        // + the Fig Chance perk — but an enemy that never drops figs (a kamikaze) doesn't start to with it.
        bool fig = enemy.fig_chance > 0.0f && GD.Randf() < enemy.fig_chance + (_player?.fig_chance_bonus ?? 0.0f);
        if (!enemy.fell_off)
            Callable.From(() => SpawnDrops(at, lira, fig)).CallDeferred();
        _enemies.Remove(enemy);
        _spotOf.Remove(enemy); // its spot is free again
        if (enemy.enemy_id == EnemyIds.Ventilator)
            _ventilatorCd = Rounds.VentilatorCooldown; // the next one waits
        if (enemy.optional)
            return; // optional enemies (the sleeper, kamikazes, the Ventilator) aren't part of the round
        _alive -= 1;   // free a slot in the concurrency cap
        _killed += 1;
        if (_round > 0 && _killed >= _quota)
            ClearRound();
        else
        {
            UpdateStragglers();
            PushRoundHud();
        }
    }

    /// <summary>A corpse's drops: <paramref name="lira"/> coins that fly straight to the player (banked on arrival), and
    /// optionally one fada_fig that bounces out and settles until touched.</summary>
    private void SpawnDrops(Vector2 at, int lira, bool fig)
    {
        if (_player != null)
            for (int i = 0; i < lira; i++)
            {
                var coin = _liraScene.Instantiate<Lira>();
                _content.AddChild(coin);
                PlaceAt(coin, at + new Vector2((float)GD.RandRange(-8, 8), -18));
                coin.launch(_player);
            }
        if (fig)
        {
            var fada_fig = _fadaFigScene.Instantiate<FadaFig>();
            fada_fig.Collector = _player; // the Magnet perk pulls it in when he's close
            _content.AddChild(fada_fig);
            PlaceAt(fada_fig, at + new Vector2((float)GD.RandRange(-10, 10), -12));
        }
    }

    private void SpawnRuhOrb(Vector2 at, bool completedCharge)
    {
        if (_player == null)
            return;
        var orb = _ruhOrb.Instantiate<Node2D>();
        VfxPalette.RecolorTree(orb);
        AddChild(orb);
        PlaceAt(orb, at + new Vector2(0, -18));
        orb.Call("launch", _player, completedCharge);
    }

    private void OnEnemyDamaged(float amount, Node source, Enemy enemy)
    {
        if (_player != null && source == _player)
        {
            _player.notify_hit_dealt(amount, enemy);
            if (amount > 0.0f && !enemy.last_hit_from_special && _player.gain_ruh_on_hit())
                SpawnRuhOrb(enemy.GlobalPosition, true);
            FloatingTextType kind = enemy.last_hit_from_special ? FloatingTextType.DamageSpecial : FloatingTextType.Damage;
            FloatingText.Emit(kind, enemy, DamageNumberOffset, Mathf.RoundToInt(amount).ToString(), amount);
        }
    }

    // --- run restart (on death) -----------------------------------------------

    private void RestartRun()
    {
        Engine.TimeScale = 1.0;
        SaveData.ReportRun(_round);   // persist a new best (highest round reached) before the arena resets
        _deadPrev = false;
        BuildArena();
        if (_player != null)
        {
            _player.begin_run();
            ChooseAttack();
        }
        EndDeathCinematic();
    }

    private void ChooseAttack()
    {
        if (_player == null)
            return;
        var ui = new AttackSelect();
        AddChild(ui);
        ui.chosen += OnAttackChosen;
        ui.Open(_player.character);
    }

    private void OnAttackChosen(string id) => _player.equip(LoadoutCategory.Attack, id);

    // --- death / spawn / camera flair -----------------------------------------

    private void HandleDeath(float delta)
    {
        if (_player.fell_out())
        {
            HandleFallDeath(delta);
            return;
        }
        if (!_deadPrev)
        {
            _deadPrev = true;
            _deathHold = DeathHold;
            ZoomTo(CamZoomDeath, 0.45f);
            BeginDeathCinematic();
        }
        _deathTuneLeft = Mathf.Max(_deathTuneLeft - delta, 0.0f);
        _camVel = Vector2.Zero;
        if (_camera != null)
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(_player.GlobalPosition + new Vector2(0, -18), 0.12f);
        if (_player.death_complete() && _deathTuneLeft <= 0.0f)
        {
            _deathHold -= delta;
            if (_deathHold <= 0.0f)
                RestartRun();
        }
    }

    /// <summary>Fell out of the arena: NOT the death cinematic — the camera stops where it is (no follow, no zoom, no
    /// overlay) and Khalid drops out of frame in his fall animation; the music stops, and once the fall sound has played
    /// (at least <see cref="FallDeathHold"/>) the run ends.</summary>
    private void HandleFallDeath(float delta)
    {
        if (!_deadPrev)
        {
            _deadPrev = true;
            _music.stop();
            _deathHold = Mathf.Max(FallDeathHold, CueLength("player_fall_death"));
        }
        if ((_deathHold -= delta) <= 0.0f)
            RestartRun();
    }

    private void BeginDeathCinematic()
    {
        _deathTuneLeft = CueLength("player_death");
        _music.stop();
        if (_player != null)
        {
            _player.ZIndex = WorldZ.DeathPlayer;
            _player.ZAsRelative = false;
        }
        if (_deathOverlay != null && IsInstanceValid(_deathOverlay))
            _deathOverlay.QueueFree();
        const float s = 20000.0f;
        _deathOverlay = new Polygon2D
        {
            Polygon = new Vector2[] { new(-s, -s), new(s, -s), new(s, s), new(-s, s) },
            Color = Colors.Black,
            Modulate = new Color(1, 1, 1, 0.0f),
            ZIndex = WorldZ.DeathOverlay,
            ZAsRelative = false,
        };
        Node host = _camera != null ? _camera : this;
        host.AddChild(_deathOverlay);
        CreateTween().TweenProperty(_deathOverlay, "modulate:a", 1.0, DeathFadeIn);
        GetTree().CreateTimer(DeathFreeze).Timeout += () =>
        {
            if (_player != null && _player.is_dead())
                _player.release_death();
        };
    }

    private void EndDeathCinematic()
    {
        if (_deathOverlay == null || !IsInstanceValid(_deathOverlay))
        {
            ResetPlayerZ();
            return;
        }
        var ov = _deathOverlay;
        _deathOverlay = null;
        var tw = CreateTween();
        tw.TweenProperty(ov, "modulate:a", 0.0, DeathFadeOut);
        tw.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(ov))
                ov.QueueFree();
            ResetPlayerZ();
        }));
    }

    private void ResetPlayerZ()
    {
        if (_player != null)
        {
            _player.ZIndex = WorldZ.Actors;
            _player.ZAsRelative = true;
        }
    }

    /// <summary>Length in seconds of a character sound cue (0 if the cue or its file is missing).</summary>
    private static float CueLength(string cue)
    {
        var cues = SfxCharacters.CUES;
        string path = cues.ContainsKey(cue) ? cues[cue].AsString() : "";
        if (path == "" || !ResourceLoader.Exists(path))
            return 0.0f;
        var s = GD.Load<AudioStream>(path);
        return s != null ? (float)s.GetLength() : 0.0f;
    }

    private void HandleSpawn(float delta)
    {
        if (!_spawning)
        {
            _spawning = true;
            ZoomTo(CamZoomSpawn, 0.35f);
        }
        _camVel = Vector2.Zero;
        if (_camera != null)
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(_player.GlobalPosition + new Vector2(0, -18), 0.12f);
    }

    private void FollowCamera(float delta)
    {
        if (_camera == null)
            return;
        Vector2 target = new(_player.GlobalPosition.X, _player.GlobalPosition.Y - 30.0f);
        float vy = Mathf.Abs(_player.Velocity.Y);
        float t = Mathf.Clamp((vy - CamTightenStart) / (CamTightenFull - CamTightenStart), 0.0f, 1.0f);
        float smoothTime = Mathf.Lerp(CamSmoothTime, CamSmoothTimeFast, t);
        _camera.GlobalPosition = SmoothDamp(_camera.GlobalPosition, target, ref _camVel, smoothTime, delta);
    }

    /// <summary>Critically damped spring step (Game Programming Gems 4 §1.10 — the classic SmoothDamp): moves
    /// <paramref name="from"/> toward <paramref name="to"/> with continuous velocity, no overshoot.</summary>
    private static Vector2 SmoothDamp(Vector2 from, Vector2 to, ref Vector2 vel, float smoothTime, float delta)
    {
        float omega = 2.0f / Mathf.Max(smoothTime, 0.0001f);
        float x = omega * delta;
        float exp = 1.0f / (1.0f + x + 0.48f * x * x + 0.235f * x * x * x);
        Vector2 change = from - to;
        Vector2 temp = (vel + omega * change) * delta;
        vel = (vel - omega * temp) * exp;
        return to + (change + temp) * exp;
    }

    private void ZoomTo(Vector2 z, float dur)
    {
        if (_camera == null)
            return;
        if (_camTween != null && _camTween.IsValid())
            _camTween.Kill();
        _camTween = _camera.CreateTween();
        _camTween.TweenProperty(_camera, "zoom", z, dur).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    // --- scaffolding ----------------------------------------------------------

    private void BuildBg()
    {
        var layer = new CanvasLayer { Layer = UiLayers.Background };
        AddChild(layer);
        var bgTex = Terrain.BackgroundTexture();
        if (bgTex != null)
        {
            _bgImgSize = bgTex.GetSize();
            // Dark backing in the image's OWN edge tone, so zooming the single (non-tiled) image out never shows a
            // hard cut or the void — the starfield just sits in a bit more of its own space.
            Color fill = new(0.05f, 0.05f, 0.06f);
            Image im = bgTex.GetImage();
            if (im != null)
            {
                if (im.IsCompressed())
                    im.Decompress();
                fill = im.GetPixel(0, 0);
            }
            var back = new ColorRect { Color = fill, MouseFilter = Control.MouseFilterEnum.Ignore };
            back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(back);
            // The SINGLE starfield (no tiling), centred + scaled by BackgroundZoom in LayoutBg (1.0 = fills).
            _bgSky = new Sprite2D { Texture = bgTex, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
            layer.AddChild(_bgSky);
        }
        LayoutBg();
        GetViewport().SizeChanged += LayoutBg;
        _bg = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
        _bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_bg);
    }

    /// <summary>Centre + scale the single bg image (BackgroundZoom of the viewport) for the current resolution. Re-run
    /// on viewport resize.</summary>
    private void LayoutBg()
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float zoom = Terrain.BackgroundZoom;
        Vector2 skySize = vp * zoom;        // the image's on-screen rect (zoom 1.0 = fills)
        if (_bgSky != null && IsInstanceValid(_bgSky) && _bgImgSize.X > 0)
        {
            _bgSky.Position = vp / 2;
            _bgSky.Scale = skySize / _bgImgSize;
        }
    }

    private void BuildFloor()
    {
        // Legacy arena floor — the painted layout is the terrain now, so switch the old floor OFF entirely.
        var floorBody = GetNodeOrNull<StaticBody2D>("Floor");
        if (floorBody == null)
            return;
        floorBody.CollisionLayer = 0; // no collision: the layout's painted solid tiles are the ground
        floorBody.Visible = false;
        var old = floorBody.GetNodeOrNull<ColorRect>("ColorRect");
        if (old != null)
            old.Visible = false;
    }

    private void AddGlow()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Canvas,
            GlowEnabled = true,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive,
            GlowIntensity = 0.9f,
            GlowBloom = 0.15f,
            GlowHdrThreshold = 1.0f,
        };
        AddChild(new WorldEnvironment { Environment = env });
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("debug_respawn"))
        {
            BuildArena();
            return;
        }
        if (_player == null)
            return;
        if (@event.IsActionPressed("debug_damage"))
            _player.take_damage(12.0f);
        else if (@event.IsActionPressed("debug_heal"))
            _player.ruh += _player.RUH_PER_BLOCK;
        else if (@event.IsActionPressed(VialDrinkAction))
            DrinkVial();
        else if (@event.IsActionPressed(VialCycleAction))
            _perks?.CycleHeld();
        else if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.B)
            _player.debug_grant_next_buff();   // DEBUG: cycle-grant catalog buffs
        else if (@event is InputEventKey k2 && k2.Pressed && !k2.Echo && k2.Keycode == Key.N)
            _player.debug_clear_buffs();
    }

    // --- Dekken vials (carried perks) -----------------------------------------

    private const string VialDrinkAction = "vial_drink"; // Q / RB — drink the selected carried vial
    private const string VialCycleAction = "vial_cycle"; // Tab / LB — select the next carried vial
    private static readonly Vector2 VialTextOffset = new(0, -52);
    private static readonly Color VialDrunkColor = new(1.0f, 0.78f, 0.25f);
    private static readonly Color VialBlockedColor = new(0.72f, 0.72f, 0.78f);

    /// <summary>Register the vial actions (physical keys = layout-independent, + the pad bumpers) if the project doesn't
    /// define them — like the stalls' <c>interact</c>, so they work without a project.godot edit.</summary>
    private static void EnsureVialActions()
    {
        AddAction(VialDrinkAction, Key.Q, JoyButton.RightShoulder);
        AddAction(VialCycleAction, Key.Tab, JoyButton.LeftShoulder);
    }

    private static void AddAction(string action, Key key, JoyButton button)
    {
        if (InputMap.HasAction(action))
            return;
        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
    }

    /// <summary>Drink the selected carried vial (instant) and say so over Khalid — its name, or why it didn't go down
    /// (e.g. FULL HEALTH: the vial stays in the pocket).</summary>
    private void DrinkVial()
    {
        if (_perks == null || _player.is_dead())
            return;
        var (text, drunk) = _perks.DrinkHeld();
        if (text == "")
            return;
        FloatingText.Emit(FloatingTextType.Damage, _player, VialTextOffset, text, 0.0f, drunk ? VialDrunkColor : VialBlockedColor);
        if (drunk)
            _sfx.play("buff_select"); // PLACEHOLDER cue
    }

    /// <summary>Show <paramref name="ledger"/>'s carried vials in the HUD (it calls this whenever they change — and once
    /// when a run's ledger is created, which clears the last run's).</summary>
    private void PushVialHud(PerkLedger ledger)
    {
        var names = new System.Collections.Generic.List<string>();
        foreach (string id in ledger.Held)
            names.Add(Dekken.Get(id).Name);
        GetNodeOrNull<HUD>("/root/HUD")?.SetVials(names, ledger.Selected);
    }

    // --- small helpers --------------------------------------------------------

    private static void PlaceAt(Node2D node, Vector2 pos)
    {
        node.GlobalPosition = pos;
        node.ResetPhysicsInterpolation();
    }
}
