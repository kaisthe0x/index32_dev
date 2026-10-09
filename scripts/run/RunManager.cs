using Godot;

namespace MyGame;

/// <summary>
/// The run driver + the <c>arena.tscn</c> root. Builds ONE arena and runs the endless ROUND loop
/// (<c>docs/game-loop.md</c>, tuning in <see cref="Rounds"/>): each round trickles a hidden QUOTA of enemies in, up to
/// a concurrent cap; spawning stops once the quota has spawned, and the last few STRAGGLERS hunt him down; the round
/// clears when they're all dead, and the next starts at once (a ROUND banner, no break). Banks Ruh on hits, drops Lira
/// on every kill (+ a per-kit chance of a Fada Fig), wires the stalls to this run's ledgers, and restarts the run on
/// death.
///
/// <para>It owns the ORDER of things and the round's counters; the parts are their own classes:
/// <see cref="EnemySpawner"/> (who spawns and where, and the living list), <see cref="PressureSpawns"/> (the
/// stand-still kamikazes and the edge Ventilator), <see cref="ArenaGround"/> (where things can stand),
/// <see cref="RunCamera"/> (follow and zoom), <see cref="DeathSequence"/> (what plays between dying and the restart),
/// <see cref="ArenaBackdrop"/> (what is drawn behind the arena) and <see cref="VialControls"/> (drinking carried
/// vials). The spawner, the ground and the pressure spawns are rebuilt with each arena.</para>
/// </summary>
[GlobalClass]
public partial class RunManager : Node2D
{
    private static readonly Vector2 DamageNumberOffset = new(0, -42);
    private static readonly Vector2 FastTravelOffset = new(-28, -4); // where Fast Travel drops you, beside the box
    private const float DeathY = 320.0f;   // falling below this world Y kills the player
    private const string StartCharacter = "khalid";
    private const string StageDir = "res://scenes/levels/stage1/";

    [Export] public NodePath PlayerPath = "Player";

    private Player _player = null!;
    private RunCamera _camera = null!;
    private DeathSequence _death = null!;
    private VialControls _vials = null!;
    private Music _music = null!;
    private Sfx _sfx = null!;
    private PackedScene _ruhOrb = null!, _liraScene = null!, _fadaFigScene = null!;

    // --- round state (see Rounds) — only NON-optional enemies are "quota" enemies ---
    private int _round = 0;            // the current round (0 = before round 1 — it starts on the first tick of play)
    private int _quota = 0;            // this round's hidden enemy count
    private int _spawned = 0;          // quota enemies spawned so far this round
    private int _killed = 0;           // quota enemies killed this round
    private int _alive = 0;            // living quota enemies (the concurrent cap looks at this)
    private float _spawnAccum = 0.0f;  // seconds accrued toward the next spawn

    // --- the current arena (rebuilt by BuildArena) ---
    private Node2D _content = null!;                // everything of this arena: the layout, enemies, drops
    private EnemySpawner _spawner = null!;
    private PressureSpawns _pressure = null!;
    private PerkLedger _perks = null!;              // this run's Dekken perks (stock, active, owned)
    private MysteryBox? _box;                       // Fast Travel's destination
    private Vector2 _playerSpawn = Vector2.Zero;
    private bool _spawning = false;                 // the spawn animation is playing (the camera is zoomed in on it)

    public override void _Ready()
    {
        _player = GetNode<Player>(PlayerPath);
        _music = GetNode<Music>("/root/Music");
        _sfx = GetNode<Sfx>("/root/Sfx");
        _ruhOrb = GD.Load<PackedScene>("res://vfx/character/khalid/ruh_orb/ruh_orb.tscn");
        _liraScene = GD.Load<PackedScene>("res://scenes/lira.tscn");
        _fadaFigScene = GD.Load<PackedScene>("res://scenes/fada_fig.tscn");
        _camera = new RunCamera(GetNodeOrNull<Camera2D>("Camera2D"));
        _death = new DeathSequence(_player, _camera, _music);
        AddChild(_death);
        _vials = new VialControls(_player, _sfx);
        AddChild(_vials);

        Engine.TimeScale = 1.0;
        Input.MouseMode = Input.MouseModeEnum.Hidden; // hide the cursor during play; menus re-show it while open
        AddGlow();
        AddChild(new ArenaBackdrop());
        _player.Character = StartCharacter;
        BuildArena();
        _player.Spawn();
        _camera.SnapTo(_playerSpawn);
        ChooseAttack();
    }

    public override void _PhysicsProcess(double deltaD)
    {
        float delta = (float)deltaD;

        if (_player.IsDead())
        {
            if (_death.Tick(delta))
                RestartRun();
            return;
        }
        if (_player.IsSpawning())
        {
            if (!_spawning)
            {
                _spawning = true;
                _camera.ZoomToSpawn();
            }
            _camera.EaseOnto(_player);
            return;
        }
        if (_player.GlobalPosition.Y > DeathY)
        {
            _player.FallToDeath(); // fell off the arena — that's a death (next tick runs the death flow)
            return;
        }
        if (_spawning)
        {
            _spawning = false;
            _camera.ZoomToPlay();
        }
        TickRound(delta);
        _pressure.Tick(delta, _round);
        _camera.Follow(_player, delta);
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
        _music.PlayStage("stage1"); // the stage music starts as the arena loads (the colour-scheme screen stays silent)
        _round = 0; // round 1 starts on the first tick of play (after the attack pick + spawn)
        _quota = 0;
        _spawned = 0;
        _killed = 0;
        _alive = 0;
        _spawnAccum = 0.0f;
        PushRoundHud();
        if (_content != null && IsInstanceValid(_content))
            _content.QueueFree(); // the old arena's layout, enemies and drops go with it
        _content = new Node2D();
        AddChild(_content);

        // Load one of the stage's hand-painted layouts at RANDOM (terrain + collision + ground tiles for spawning).
        LevelLayout? layout = null;
        var layoutPaths = StageLayoutPaths();
        if (layoutPaths.Length > 0)
        {
            var scene = GD.Load<PackedScene>(layoutPaths[GD.Randi() % (uint)layoutPaths.Length]);
            layout = scene?.Instantiate() as LevelLayout;
            if (layout != null)
            {
                layout.Position = Vector2.Zero; // ignore any authored root offset — sit the layout at origin
                _content.AddChild(layout);
            }
        }
        else
        {
            GD.PushWarning("RunManager: no stage1_v*.tscn layouts under scenes/levels/stage1/ — arena will be empty.");
        }
        _playerSpawn = layout != null ? layout.PlayerSpawn() : Vector2.Zero;
        var (arenaLeft, arenaRight) = layout?.HorizontalSpan() ?? (0.0f, 0.0f);

        var ground = new ArenaGround(this, layout);
        _spawner = new EnemySpawner(_content, layout, _player, ground, _sfx);
        _spawner.Spawned += OnEnemySpawned;
        _spawner.Died += OnEnemyDied;
        _spawner.Damaged += OnEnemyDamaged;
        _pressure = new PressureSpawns(_player, _spawner, ground, arenaLeft, arenaRight);

        foreach (var op in layout?.Orbs() ?? new System.Collections.Generic.List<Vector2>())
            _content.AddChild(new LaunchOrb { Position = op });

        // The stalls are scenes the layout places (LevelLayout.Placed) — all three are required.
        _box = layout?.Placed<MysteryBox>();
        var needlePoint = layout?.Placed<NeedlePointStall>();
        var dekken = layout?.Placed<DekkenStall>();
        if (_box == null || needlePoint == null || dekken == null)
            GD.PushError("RunManager: the layout must place all three stall scenes (scenes/things/: mystery_box, needle_point, dekken).");
        if (_box != null && layout != null)
        {
            if (layout.BoxSpots(hard: false).Count == 0)
                GD.PushError("RunManager: the layout has no easy box spot (BoxSpots/Easy markers) — the box stays where it was placed and can't relocate.");
            _box.Setup(new BoxLedger(_player), layout.BoxSpots(hard: false), layout.BoxSpots(hard: true));
        }
        if (needlePoint != null)
            needlePoint.Ledger = new ShotLedger(_player); // this run's Needle Point ranks
        _perks = new PerkLedger(_player, FastTravelToBox, _vials.ShowVials);
        _vials.Ledger = _perks;
        if (dekken != null)
            dekken.Ledger = _perks;
        if (layout != null && layout.EnemySpawns().Count == 0)
            GD.PushError("RunManager: the layout has no EnemySpawns markers — no enemies can spawn.");

        Nodes.PlaceAt(_player, _playerSpawn);
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
        var kit = _spawner.PickKit(_round); // a random kit under its per-type cap (or null if every kit is at cap)
        if (kit == null)
            return; // every kit is at its per-type cap — try again next tick
        if (_spawner.SpawnFromPool(kit, _round))
            _spawnAccum = 0.0f; // else nowhere to put it yet (every spot held) — try again next tick
    }

    private void StartRound(int round)
    {
        _round = round;
        _quota = Quota(round);
        _spawned = 0;
        _killed = 0;
        _player.NotifyRoundStart(); // round-scoped perks re-arm (Shield) / fire (Prepared)
        _spawnAccum = SpawnInterval(round); // first enemy arrives immediately
        PushRoundHud(); // the HUD plays the ROUND n intro for a new round
        _sfx.Play("round_start");
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

    /// <summary>STRAGGLERS: once the round has fully spawned and only <see cref="Rounds.StragglerCount"/> or fewer quota
    /// enemies are left, they all hunt the player — so a round never stalls on one he can't find.</summary>
    private void UpdateStragglers()
    {
        if (_spawned < _quota || _quota - _killed > Rounds.StragglerCount)
            return;
        foreach (Enemy e in _spawner.Living)
            if (IsInstanceValid(e) && !e.Optional)
                e.Hunt(Rounds.StragglerSpeedMult);
    }

    /// <summary>Dekken's Fast Travel: put the player right beside the mystery box.</summary>
    private void FastTravelToBox()
    {
        if (_box == null)
            return;
        Nodes.PlaceAt(_player, _box.GlobalPosition + FastTravelOffset);
        _player.Velocity = Vector2.Zero;
    }

    /// <summary>A quota enemy entered the arena: it counts toward the round and the concurrent cap.</summary>
    private void OnEnemySpawned(Enemy enemy)
    {
        if (enemy.Optional)
            return;
        _spawned += 1;
        _alive += 1;
        UpdateStragglers();
    }

    private void OnEnemyDied(Enemy enemy)
    {
        // Death fires INSIDE a physics query flush (Hitbox callback), where adding a RigidBody is illegal
        // ("Can't change this state while flushing queries"). Capture the values (the enemy frees) + defer the drop.
        // Every kill pays Lira; a per-kit chance also drops ONE fada_fig (the rare currency). None if it fell off-map.
        Vector2 at = enemy.GlobalPosition;
        int lira = enemy.LiraDrop;
        // + the Fig Chance perk — but an enemy that never drops figs (a kamikaze) doesn't start to with it.
        bool fig = enemy.FigChance > 0.0f && GD.Randf() < enemy.FigChance + _player.FigChanceBonus;
        if (!enemy.FellOff)
            Callable.From(() => SpawnDrops(at, lira, fig)).CallDeferred();
        if (enemy.Optional)
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
        for (int i = 0; i < lira; i++)
        {
            var coin = _liraScene.Instantiate<Lira>();
            _content.AddChild(coin);
            Nodes.PlaceAt(coin, at + new Vector2((float)GD.RandRange(-8, 8), -18));
            coin.Launch(_player);
        }
        if (fig)
        {
            var figDrop = _fadaFigScene.Instantiate<FadaFig>();
            figDrop.Collector = _player; // the Magnet perk pulls it in when he's close
            _content.AddChild(figDrop);
            Nodes.PlaceAt(figDrop, at + new Vector2((float)GD.RandRange(-10, 10), -12));
        }
    }

    private void SpawnRuhOrb(Vector2 at, bool completedCharge)
    {
        var orb = _ruhOrb.Instantiate<RuhOrb>();
        VfxPalette.RecolorTree(orb);
        AddChild(orb);
        Nodes.PlaceAt(orb, at + new Vector2(0, -18));
        orb.Launch(_player, completedCharge);
    }

    private void OnEnemyDamaged(Enemy enemy, float amount, Node? source)
    {
        if (source == _player)
        {
            _player.NotifyHitDealt(amount, enemy);
            if (amount > 0.0f && !enemy.LastHitFromSpecial && _player.GainRuhOnHit())
                SpawnRuhOrb(enemy.GlobalPosition, true);
            FloatingTextType kind = enemy.LastHitFromSpecial ? FloatingTextType.DamageSpecial : FloatingTextType.Damage;
            FloatingText.Emit(kind, enemy, DamageNumberOffset, Mathf.RoundToInt(amount).ToString(), amount);
        }
    }

    // --- run restart (on death) -----------------------------------------------

    private void RestartRun()
    {
        Engine.TimeScale = 1.0;
        SaveData.ReportRun(_round);   // persist a new best (highest round reached) before the arena resets
        BuildArena();
        _player.BeginRun();
        ChooseAttack();
        _death.End();
    }

    private void ChooseAttack()
    {
        var ui = new AttackSelect();
        AddChild(ui);
        ui.Chosen += OnAttackChosen;
        ui.Open(_player.Character);
    }

    private void OnAttackChosen(string id) => _player.Equip(LoadoutCategory.Attack, id);

    // --- scaffolding ----------------------------------------------------------

    /// <summary>The world's bloom: anything drawn brighter than 1.0 (the HDR neon) glows.</summary>
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

    /// <summary>DEBUG keys: rebuild the arena, hurt / refill the player, grant / clear catalog buffs.</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("debug_respawn"))
        {
            BuildArena();
            return;
        }
        if (@event.IsActionPressed("debug_damage"))
            _player.TakeDamage(12.0f);
        else if (@event.IsActionPressed("debug_heal"))
            _player.Ruh += Player.RuhPerBlock;
        else if (@event is InputEventKey k && k.Pressed && !k.Echo && k.Keycode == Key.B)
            _player.DebugGrantNextBuff();   // DEBUG: cycle-grant catalog buffs
        else if (@event is InputEventKey k2 && k2.Pressed && !k2.Echo && k2.Keycode == Key.N)
            _player.DebugClearBuffs();
    }
}
