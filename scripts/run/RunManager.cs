using Godot;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// The run driver + the <c>arena.tscn</c> root. Builds ONE continuous arena (levels/exits are retired — the pivot's
/// single Fissure arena), then trickles enemies in at a STEADY rate from a mixed roster, proximity-placed around the
/// player. Banks Ruh on hits, drops Fada Figs on each kill, pops a free pick-1-of-3 MILD buff menu at escalating
/// fada-fig milestones (25/55/105…), spawns a mystery box (spend figs for a stingy powerful-buff gamble), and restarts
/// the run on death. Owns the player spawn, camera follow, and death/spawn flair. C# port of <c>run_manager.gd</c>.
///
/// <para>Talks to the C# body tree (Player/Enemy) + collectibles (FadaFig) + MysteryBox directly; BRIDGES the still-GDScript
/// config/autoload layer (Terrain/Levels via the constant map, Music/Sfx via <c>/root/*</c>, AttackSelect/LaunchOrb via
/// <c>.New()</c> + signals). Levels data survives only as the arena's palette source; the reward-door system is parked.</para>
/// </summary>
[GlobalClass]
public partial class RunManager : Node2D
{
    private static readonly Vector2 SpawnFxOffset = new(0, -22);
    private static readonly Vector2 DamageNumberOffset = new(0, -42);
    private const float DeathY = 320.0f;
    private const string StartCharacter = "khalid";

    // --- continuous spawn (no levels/exits): enemies trickle in at a STEADY rate. Tunable when seals arrive. ---
    private const float SpawnInterval = 2.0f;   // seconds between spawn ticks ("waves")
    private const int EnemiesPerWave = 1;        // enemies dropped in per tick
    private const int MaxAlive = 8;             // pause spawning past this many living non-optional enemies
    // Per-TYPE concurrent cap: a kit's `spawn_cap` (if any) limits how many of that enemy can be alive at once;
    // the cap grows by +1 every SpawnCapGrowthWaves waves as the run progresses. Kits without a cap are unlimited.
    private const int SpawnCapGrowthWaves = 20;
    // Anti-camp: an enemy that stays OFF-SCREEN this long (e.g. can't path to a camping player) is silently freed,
    // freeing its cap slot so a fresh one can spawn near the player. Margin grows the on-screen rect a little.
    private const float OffscreenDespawnTime = 8.0f;
    private const float OffscreenMargin = 96.0f;
    // Buff menu: every time LIFETIME fada_figs collected crosses the next milestone, a free pick-1-of-3 MILD buff
    // menu pops (game pauses). The gap to the next grows, so milestones land at 25, 55, 105, 175, … (tune here).
    private const int FirstMilestone = 5;
    private const int MilestoneGapBase = 10;
    private const int MilestoneGapGrowth = 10;
    private const int BuffMenuChoices = 3;
    private const float LevelUpDelay = 1.1f;   // "LEVEL UP" banner hold before the buff menu opens

    /// <summary>The roster the continuous spawner draws from (uniform random) — a mixed assortment of grunts plus the
    /// flyer (Ein) and the stationary sleeper (Nasen). Wardens (Kroj) are elite/pivot-only, not part of the trickle.</summary>
    private static readonly GDict[] SpawnPool =
    {
        EnemyKits.KEBUS, EnemyKits.BAGHEL, EnemyKits.MAZAB, EnemyKits.MATAT,
        EnemyKits.TARRI, EnemyKits.BRESKI, EnemyKits.EIN, EnemyKits.NASEN,
    };

    // Camera follow (speed-adaptive).
    private const float CamFollowBase = 0.002f;
    private const float CamTightenStart = 600.0f;
    private const float CamTightenFull = 1200.0f;
    private const float CamTightK = 0.9f;
    private static readonly Vector2 CamZoomNormal = new(1.5f, 1.5f);
    private static readonly Vector2 CamZoomDeath = new(3.0f, 3.0f);
    private static readonly Vector2 CamZoomSpawn = new(2, 2);
    private const float DeathHold = 0.7f;
    private const int DeathPlayerZ = 500;
    private const int DeathOverlayZ = 400;
    private const float DeathFadeIn = 0.55f;
    private const float DeathFadeOut = 0.6f;
    private const float DeathFreeze = 0.5f;

    [Export] public NodePath player_path = "Player";

    private Player _player;
    private Camera2D _camera;

    private int _alive = 0;            // living NON-optional enemies (the spawn-cap looks at this)
    private int _waveCount = 0;        // spawn ticks so far this run
    private float _spawnAccum = 0.0f;  // seconds accrued toward the next spawn tick
    private int _nextMilestone = FirstMilestone; // lifetime fada_figs that pops the next buff menu
    private int _prevMilestone = 0;              // the last milestone reached (the progress bar spans prev→next)
    private int _milestoneGap = MilestoneGapBase; // grows each milestone (25 → +30 → +50 → …)
    private int _milestoneIndex = 0;              // how many buff menus taken this run (scales offered tiers)
    private bool _menuOpen = false;               // a buff menu is up (game paused) — don't stack another
    private readonly System.Collections.Generic.Dictionary<string, Buff> _menuBuffs = new(); // id → the exact offered buff (tiered)
    private readonly System.Collections.Generic.Dictionary<Enemy, float> _offscreen = new(); // living enemy → seconds off-screen (anti-camp cull)
    private CanvasLayer _levelUpBanner;           // transient "LEVEL UP" flash shown before the buff menu
    private Node2D _content;
    private ColorRect _bg;
    private Sprite2D _bgSky;
    private Vector2 _bgImgSize;
    private AnimatedSprite2D _bgAnim;
    private LevelLayout _layout;

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
    private PackedScene _enemyScene, _spawnFx, _ruhOrb, _fadaFigScene;

    public override void _Ready()
    {
        _player = GetNodeOrNull<Player>(player_path);
        _camera = GetNodeOrNull<Camera2D>("Camera2D");
        _music = GetNode<Music>("/root/Music");
        _sfx = GetNode<Sfx>("/root/Sfx");
        _enemyScene = GD.Load<PackedScene>("res://scenes/enemy.tscn");
        _spawnFx = GD.Load<PackedScene>("res://vfx/spawn/enemy_spawn.tscn");
        _ruhOrb = GD.Load<PackedScene>("res://vfx/character/khalid/ruh_orb/ruh_orb.tscn");
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
        if (_player != null)
            _player.fada_collected += OnFadaCollected; // milestone buff menu (fires off the lifetime total)
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
            PlaceAt(_player, _playerSpawn);
            if (_camera != null)
                PlaceAt(_camera, _playerSpawn + new Vector2(0, -30));
            return;
        }
        if (_spawning)
        {
            _spawning = false;
            ZoomTo(CamZoomNormal, 0.4f);
        }
        _spawnAccum += delta;
        if (_spawnAccum >= SpawnInterval)
        {
            _spawnAccum = 0.0f;
            SpawnWave();
        }
        FollowCamera(delta);
        CullOffscreen(delta); // free enemies stuck off-screen (anti-camp), using the just-moved camera
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
        _music.play_stage("stage1");
        _alive = 0;
        _waveCount = 0;
        _spawnAccum = 0.0f;
        _nextMilestone = FirstMilestone;
        _prevMilestone = 0;
        _milestoneGap = MilestoneGapBase;
        _milestoneIndex = 0;
        _menuOpen = false;
        _offscreen.Clear(); // old enemies free with _content
        SaveData.SetCurrentWaves(0);
        PushBuffProgress();
        HideLevelUpBanner();
        if (_content != null && IsInstanceValid(_content))
            _content.QueueFree();
        _content = new Node2D();
        AddChild(_content);

        // Levels are retired, but index 0 still holds the arena's background palette (a single source of the look).
        Color tint = Levels.GetLevel(0)["bg"].As<Color>();
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

        foreach (var op in _layout?.Orbs() ?? new System.Collections.Generic.List<Vector2>())
            _content.AddChild(new LaunchOrb { Position = op });

        // One mystery box per arena, on a ground tile a short walk from spawn (the fig sink for powerful buffs).
        Vector2 boxPos = PickGroundSurface(_playerSpawn.X, 120.0f, 320.0f) ?? _playerSpawn + new Vector2(120, 0);
        var box = new MysteryBox { Position = boxPos };
        box.won += OpenPowerfulBuffMenu; // a winning pull opens the 3-choice powerful menu
        _content.AddChild(box);

        SpawnWave(); // seed the arena so the player isn't waiting on the first tick
        if (_player != null)
            PlaceAt(_player, _playerSpawn);
    }

    // --- continuous spawning --------------------------------------------------

    /// <summary>One spawn tick ("wave"): drop in <see cref="EnemiesPerWave"/> random enemies from the pool, unless
    /// we're already at the living-enemy cap. Always bumps the wave counter (which ramps the buff-drop chance).</summary>
    private void SpawnWave()
    {
        if (_player == null || _player.is_dead() || _player.is_spawning())
            return;
        _waveCount += 1;
        SaveData.SetCurrentWaves(_waveCount);   // live HUD counter (survival metric)
        for (int i = 0; i < EnemiesPerWave && _alive < MaxAlive; i++)
        {
            var kit = PickSpawnKit();
            if (kit != null)
                SpawnOne(kit);
        }
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

    /// <summary>A kit's current concurrent cap: its <c>spawn_cap</c> base + 1 per <see cref="SpawnCapGrowthWaves"/>
    /// waves survived. Kits with no <c>spawn_cap</c> are uncapped.</summary>
    private int EffectiveCap(GDict kit) =>
        kit.ContainsKey("spawn_cap") ? kit["spawn_cap"].AsInt32() + _waveCount / SpawnCapGrowthWaves : int.MaxValue;

    /// <summary>How many living enemies of type <paramref name="id"/> are currently tracked.</summary>
    private int LivingOfType(string id)
    {
        int n = 0;
        foreach (Enemy e in _offscreen.Keys)
            if (IsInstanceValid(e) && e.enemy_id == id)
                n += 1;
        return n;
    }

    /// <summary>Spawn ONE enemy from a kit: proximity-place it (near/overhead/far by type, never on the player), puff +
    /// wire its died/damaged signals, and count it toward the cap.</summary>
    private void SpawnOne(GDict kit)
    {
        Vector2 pos = SpawnPosition(kit, _playerSpawn);
        SpawnFx(pos);
        var enemy = SpawnEnemy(kit, pos);
        if (enemy == null)
            return;
        var e = enemy; // stable capture for the bound handlers
        enemy.Connect(Enemy.SignalName.died, Callable.From(() => OnEnemyDied(e)));
        enemy.Connect(Enemy.SignalName.damaged, Callable.From((float amount, Node source) => OnEnemyDamaged(amount, source, e)));
        _offscreen[enemy] = 0.0f; // start its anti-camp off-screen timer
        if (!enemy.optional)
            _alive += 1;
    }

    // Proximity-spawn tuning (px). Ground grunts appear within a fair band — far enough that the player can react,
    // never on top of him; stationary enemies (Nasen) much farther; flyers (Ein) overhead with dodge room.
    private const float GroundSpawnMin = 100.0f;
    private const float GroundSpawnMax = 240.0f;
    private const float StationarySpawnMin = 500.0f;
    private const float StationarySpawnMax = 920.0f;
    private const float FlyerHeightMin = 130.0f;
    private const float FlyerHeightMax = 210.0f;
    private const float FlyerXSpread = 90.0f;

    /// <summary>Where to drop this enemy relative to the player: flyers overhead (with headroom), stationary far on a
    /// ground tile, grunts near on a ground tile — always at least the min band away. <paramref name="fallback"/> is
    /// the authored spec position, used only if the layout has no usable ground tiles.</summary>
    private Vector2 SpawnPosition(GDict kit, Vector2 fallback)
    {
        Vector2 player = _player?.GlobalPosition ?? Vector2.Zero;
        if (kit.ContainsKey("air") && kit["air"].AsBool())
        {
            float x = player.X + (float)GD.RandRange(-FlyerXSpread, FlyerXSpread);
            float up = (float)GD.RandRange(FlyerHeightMin, Mathf.Max(FlyerHeightMin, HeadroomAbove(player)));
            return new Vector2(x, player.Y - up);
        }
        bool stationary = kit.ContainsKey("movement") && kit["movement"].AsInt32() == (int)EnemyMovement.Stationary;
        float min = stationary ? StationarySpawnMin : GroundSpawnMin;
        float max = stationary ? StationarySpawnMax : GroundSpawnMax;
        return PickGroundSurface(player.X, min, max) ?? fallback;
    }

    /// <summary>Clear vertical space above <paramref name="from"/> up to <see cref="FlyerHeightMax"/> — so a flyer isn't
    /// spawned inside a ceiling. Returns how high it can safely sit.</summary>
    private float HeadroomAbove(Vector2 from)
    {
        var space = GetWorld2D()?.DirectSpaceState;
        if (space == null)
            return FlyerHeightMax;
        var q = PhysicsRayQueryParameters2D.Create(from, from + new Vector2(0.0f, -(FlyerHeightMax + 16.0f)), (uint)Combat.Layer.World);
        var hit = space.IntersectRay(q);
        if (hit.Count == 0)
            return FlyerHeightMax;
        return Mathf.Clamp(from.Y - hit["position"].As<Vector2>().Y - 14.0f, FlyerHeightMin * 0.5f, FlyerHeightMax);
    }

    /// <summary>A random exposed ground-tile position whose horizontal distance from <paramref name="fromX"/> is in
    /// [min,max]; if none fall in that band, the nearest tile that is still ≥ min away (so it's never adjacent to the
    /// player); null only if the layout has no ground tiles at all.</summary>
    private Vector2? PickGroundSurface(float fromX, float min, float max)
    {
        var surfaces = _layout?.GroundSurfaces();
        if (surfaces == null || surfaces.Count == 0)
            return null;
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
        // FadaFig drop count defaults from the advisory tier unless the kit set fada_fig_drop explicitly (Wardens do).
        if (!kit.ContainsKey("fada_fig_drop") && kit.ContainsKey("tier"))
            enemy.fada_fig_drop = FadaFigsForTier((EnemyTier)kit["tier"].AsInt32());
        enemy.Position = pos;
        _content.AddChild(enemy);
        return enemy;
    }

    /// <summary>Default fada_figs dropped by an enemy of a given advisory tier (Wardens override via their kit).</summary>
    private static int FadaFigsForTier(EnemyTier tier) => tier switch
    {
        EnemyTier.Chip => 1,
        EnemyTier.Mid => 2,
        EnemyTier.Strong => 3,
        _ => 1,
    };

    private void SpawnFx(Vector2 pos)
    {
        var fx = _spawnFx.Instantiate<Node2D>();
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
        // Buffs no longer drop on kill — they come from the fada-fig milestone menu + the mystery box (below).
        Vector2 at = enemy.GlobalPosition;
        int figs = enemy.fada_fig_drop;
        if (!enemy.fell_off)
            Callable.From(() => SpawnFadaFigs(at, figs)).CallDeferred();
        _offscreen.Remove(enemy);
        if (!enemy.optional)
            _alive -= 1;   // free a slot in the concurrency cap
    }

    /// <summary>Anti-camp: free any tracked enemy that's stayed OFF-SCREEN for <see cref="OffscreenDespawnTime"/> (it
    /// likely can't path to a camping player). Silent — no death VFX/sfx/figs — but it frees its cap slot so a fresh
    /// enemy can spawn near the player. Runs only during normal play (paused/dead/spawning all early-return above).</summary>
    private void CullOffscreen(float delta)
    {
        if (_camera == null || _offscreen.Count == 0)
            return;
        Vector2 half = GetViewport().GetVisibleRect().Size / _camera.Zoom * 0.5f;
        Rect2 view = new Rect2(_camera.GlobalPosition - half, half * 2.0f).Grow(OffscreenMargin);
        System.Collections.Generic.List<Enemy> cull = null;
        foreach (Enemy e in new System.Collections.Generic.List<Enemy>(_offscreen.Keys))
        {
            if (!IsInstanceValid(e))
            {
                _offscreen.Remove(e);
                continue;
            }
            if (view.HasPoint(e.GlobalPosition))
                _offscreen[e] = 0.0f;
            else if ((_offscreen[e] += delta) >= OffscreenDespawnTime)
                (cull ??= new()).Add(e);
        }
        if (cull != null)
            foreach (Enemy e in cull)
                DespawnEnemy(e);
    }

    /// <summary>Silently remove <paramref name="e"/> (no death signal/VFX/figs) and free its concurrency-cap slot.</summary>
    private void DespawnEnemy(Enemy e)
    {
        _offscreen.Remove(e);
        if (!IsInstanceValid(e))
            return;
        if (!e.optional)
            _alive = Mathf.Max(0, _alive - 1);
        e.QueueFree();
    }

    /// <summary>Scatter <paramref name="count"/> collectible fada_figs out of a corpse (they bounce, roll, and settle).</summary>
    private void SpawnFadaFigs(Vector2 at, int count)
    {
        if (_fadaFigScene == null)
            return;
        for (int i = 0; i < count; i++)
        {
            var fada_fig = _fadaFigScene.Instantiate<Node2D>();
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
        SaveData.ReportRun(_waveCount);   // persist a new best (most waves survived) before the arena resets
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

    // --- fada-fig milestone buff menu -----------------------------------------

    /// <summary>Every time the run's LIFETIME fada_figs crosses the next milestone, pop a free pick-1-of-3 MILD buff
    /// menu (game pauses). The spendable balance is untouched — that's the mystery box's currency.</summary>
    private void OnFadaCollected(int balance, int lifetime)
    {
        if (!_menuOpen && lifetime >= _nextMilestone)
            BeginBuffMilestone();
        else
            PushBuffProgress();
    }

    /// <summary>Hit a fada-fig milestone: advance the counter, freeze the game, flash a "LEVEL UP" banner + cue, then
    /// (after a beat) open the mild buff menu. The menu is FREE — figs aren't spent (balance is the box's currency).</summary>
    private void BeginBuffMilestone()
    {
        _menuOpen = true; // lock out re-triggers + stacking through the whole banner→menu flow
        _prevMilestone = _nextMilestone;
        _milestoneIndex += 1;
        _nextMilestone += _milestoneGap;              // 5 → 15 → 35 → … (gap grows by MilestoneGapGrowth)
        _milestoneGap += MilestoneGapGrowth;
        PushBuffProgress();                           // bar resets toward the new milestone (visible behind the banner)
        GetTree().Paused = true;
        ShowLevelUpBanner();
        _sfx.play("buff_levelup");                    // PLACEHOLDER cue
        GetTree().CreateTimer(LevelUpDelay, true).Timeout += () =>
        {
            HideLevelUpBanner();
            ShowBuffMenu(BuffCatalog.MildIds(), false, "CHOOSE A BUFF");
        };
    }

    /// <summary>Push progress toward the next buff milestone (figs since the last one) to the HUD bar.</summary>
    private void PushBuffProgress()
    {
        if (_player == null)
            return;
        GetNodeOrNull<HUD>("/root/HUD")?.SetBuffProgress(_player.fada_lifetime - _prevMilestone, _nextMilestone - _prevMilestone);
    }

    /// <summary>A brief centred "LEVEL UP" flash (its own CanvasLayer, ProcessMode.Always so it animates while the
    /// game is paused). Freed by <see cref="HideLevelUpBanner"/> once the menu opens.</summary>
    private void ShowLevelUpBanner()
    {
        HideLevelUpBanner();
        _levelUpBanner = new CanvasLayer { Layer = 60, ProcessMode = ProcessModeEnum.Always };
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _levelUpBanner.AddChild(center);
        var label = new Label { Text = "LEVEL UP!", HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 44);
        label.AddThemeColorOverride("font_color", new Color(1.8f, 1.5f, 0.4f)); // HDR gold, blooms
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 8);
        center.AddChild(label);
        label.Scale = new Vector2(0.6f, 0.6f);
        label.PivotOffset = new Vector2(120, 30);
        _levelUpBanner.CreateTween().TweenProperty(label, "scale", Vector2.One, 0.28f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); // pops even while paused (banner is Always)
        AddChild(_levelUpBanner);
    }

    private void HideLevelUpBanner()
    {
        if (_levelUpBanner != null && IsInstanceValid(_levelUpBanner))
            _levelUpBanner.QueueFree();
        _levelUpBanner = null;
    }

    /// <summary>The mystery box's payoff — same pick-1-of-3 menu as the milestone, but from the POWERFUL pool
    /// (above-rare tiers). Called when a (non-dud) box pull wins.</summary>
    private void OpenPowerfulBuffMenu()
    {
        if (!_menuOpen)
            ShowBuffMenu(BuffCatalog.PowerfulIds(), true, "MYSTERY BOX");
    }

    /// <summary>Roll BuffMenuChoices distinct buffs from <paramref name="pool"/> (powerful vs mild tiers) → a 3-card
    /// `RewardUI`; picking grants the exact tiered buff shown. Pauses the game.</summary>
    private void ShowBuffMenu(string[] pool, bool powerful, string title)
    {
        if (_player == null)
            return;
        _menuOpen = true;
        _menuBuffs.Clear();
        var cards = new GArr();
        foreach (string id in PickDistinct(pool, BuffMenuChoices))
        {
            Tier tier = powerful ? RollPowerfulTier() : RollMildTier();
            Buff buff = BuffCatalog.Make(id, tier);
            if (buff == null)
                continue;
            _menuBuffs[id] = buff;
            cards.Add(new GDict { { "id", id }, { "name", buff.Name }, { "desc", buff.Description }, { "tier", (int)tier } });
        }
        var ui = new RewardUI();
        AddChild(ui);
        ui.chosen += OnBuffChosen;
        ui.Open(cards, title);
    }

    private void OnBuffChosen(string id)
    {
        _menuOpen = false;
        if (_menuBuffs.TryGetValue(id, out var buff) && _player != null)
            _player.add_passive(buff);
        _menuBuffs.Clear();
        _sfx.play("buff_select"); // PLACEHOLDER cue
    }

    /// <summary>Mild tiers skew Common, easing toward Rare as more milestones are taken this run.</summary>
    private Tier RollMildTier()
    {
        float rare = Mathf.Min(0.6f, 0.2f + 0.08f * _milestoneIndex);
        return GD.Randf() < rare ? Tier.Rare : Tier.Common;
    }

    /// <summary>Powerful (mystery-box) tiers — above rare: mostly Hot, some Sensational, rarely Epic.</summary>
    private static Tier RollPowerfulTier()
    {
        float r = GD.Randf();
        return r < 0.6f ? Tier.Hot : r < 0.9f ? Tier.Sensational : Tier.Epic;
    }

    /// <summary>Up to <paramref name="n"/> distinct ids from <paramref name="pool"/> (Fisher–Yates on a copy).</summary>
    private static System.Collections.Generic.List<string> PickDistinct(string[] pool, int n)
    {
        var copy = new System.Collections.Generic.List<string>(pool);
        var outL = new System.Collections.Generic.List<string>();
        for (int i = 0; i < n && copy.Count > 0; i++)
        {
            int j = (int)(GD.Randi() % (uint)copy.Count);
            outL.Add(copy[j]);
            copy.RemoveAt(j);
        }
        return outL;
    }

    // --- death / spawn / camera flair -----------------------------------------

    private void HandleDeath(float delta)
    {
        if (!_deadPrev)
        {
            _deadPrev = true;
            _deathHold = DeathHold;
            ZoomTo(CamZoomDeath, 0.45f);
            BeginDeathCinematic();
        }
        _deathTuneLeft = Mathf.Max(_deathTuneLeft - delta, 0.0f);
        if (_camera != null)
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(_player.GlobalPosition + new Vector2(0, -18), 0.12f);
        if (_player.death_complete() && _deathTuneLeft <= 0.0f)
        {
            _deathHold -= delta;
            if (_deathHold <= 0.0f)
                RestartRun();
        }
    }

    private void BeginDeathCinematic()
    {
        _deathTuneLeft = DeathTuneLength();
        _music.stop();
        if (_player != null)
        {
            _player.ZIndex = DeathPlayerZ;
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
            ZIndex = DeathOverlayZ,
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
            _player.ZIndex = 0;
            _player.ZAsRelative = true;
        }
    }

    private float DeathTuneLength()
    {
        var cues = SfxCharacters.CUES;
        string path = cues.ContainsKey("player_death") ? cues["player_death"].AsString() : "";
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
        if (_camera != null)
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(_player.GlobalPosition + new Vector2(0, -18), 0.12f);
    }

    private void FollowCamera(float delta)
    {
        if (_camera == null)
            return;
        Vector2 target = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Y - 30.0f) + _player.Velocity * delta;
        float vy = Mathf.Abs(_player.Velocity.Y);
        float t = Mathf.Clamp((vy - CamTightenStart) / (CamTightenFull - CamTightenStart), 0.0f, 1.0f);
        float k = Mathf.Lerp(1.0f - Mathf.Pow(CamFollowBase, delta), CamTightK, t);
        _camera.GlobalPosition = _camera.GlobalPosition.Lerp(target, k);
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
        var layer = new CanvasLayer { Layer = -100 };
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
        // Animated background element (orbiting planet) — over the sky, scaled with the same zoom.
        var animFrames = Terrain.BackgroundAnimFrames();
        if (animFrames != null)
        {
            _bgAnim = new AnimatedSprite2D
            {
                SpriteFrames = animFrames,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            layer.AddChild(_bgAnim);
            _bgAnim.Play("orbit");
        }
        LayoutBg();
        GetViewport().SizeChanged += LayoutBg;
        _bg = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
        _bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_bg);
    }

    /// <summary>Centre + scale the single bg image (BackgroundZoom of the viewport) and place the animated element
    /// inside its rect, for the current resolution. Re-run on viewport resize.</summary>
    private void LayoutBg()
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float zoom = Terrain.BackgroundZoom;
        Vector2 skySize = vp * zoom;        // the image's on-screen rect (zoom 1.0 = fills)
        Vector2 origin = (vp - skySize) / 2; // centred
        if (_bgSky != null && IsInstanceValid(_bgSky) && _bgImgSize.X > 0)
        {
            _bgSky.Position = vp / 2;
            _bgSky.Scale = skySize / _bgImgSize;
        }
        if (_bgAnim != null && IsInstanceValid(_bgAnim))
        {
            _bgAnim.Position = origin + Terrain.BackgroundAnimRatio * skySize;
            float px = _bgImgSize.X > 0 ? skySize.X / _bgImgSize.X : zoom;
            _bgAnim.Scale = new Vector2(px, px) * Terrain.BackgroundAnimScale;
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
        else if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.B)
            _player.debug_grant_next_buff();   // DEBUG: cycle-grant catalog buffs
        else if (@event is InputEventKey k2 && k2.Pressed && !k2.Echo && k2.Keycode == Key.N)
            _player.debug_clear_buffs();
    }

    // --- small helpers --------------------------------------------------------

    private static void PlaceAt(Node2D node, Vector2 pos)
    {
        node.GlobalPosition = pos;
        node.ResetPhysicsInterpolation();
    }
}
