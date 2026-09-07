using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// The run driver + the <c>arena.tscn</c> root. Builds ONE continuous arena (levels/exits are retired — the pivot's
/// single Fissure arena), then trickles enemies in at a STEADY rate from a mixed roster, proximity-placed around the
/// player. Banks Ruh on hits, drops Fada Figs + (at a ramping chance) a random BUFF on each kill, and restarts the run
/// on death. Owns the player spawn, camera follow, and death/spawn flair. C# port of <c>scripts/run/run_manager.gd</c>.
///
/// <para>Talks to the C# body tree (Player/Enemy) + collectibles (FadaFig/BuffDrop) directly; BRIDGES the still-GDScript
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
    // Buff drops: a dying enemy drops a random buff at a chance that RAMPS per wave — 40% → 70% cap.
    private const float BuffDropBase = 0.40f;
    private const float BuffDropStep = 0.03f;    // +per wave (hits the cap after ~10 waves)
    private const float BuffDropCap = 0.70f;

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
    private int _waveCount = 0;        // spawn ticks so far this run — ramps the buff-drop chance
    private float _spawnAccum = 0.0f;  // seconds accrued toward the next spawn tick
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
        _music.play("level");
        _alive = 0;
        _waveCount = 0;
        _spawnAccum = 0.0f;
        SaveData.SetCurrentWaves(0);
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
            SpawnOne(SpawnPool[GD.Randi() % (uint)SpawnPool.Length]);
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
        if (!enemy.optional)
            _alive += 1;
    }

    // Proximity-spawn tuning (px). Ground grunts appear within a fair band — far enough that the player can react,
    // never on top of him; stationary enemies (Nasen) much farther; flyers (Ein) overhead with dodge room.
    private const float GroundSpawnMin = 170.0f;
    private const float GroundSpawnMax = 440.0f;
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
            if (k is "scene" or "tier" or "pos" or "air" or "movement")  // advisory kit metadata, not Enemy properties
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
        // ("Can't change this state while flushing queries"). Capture the values (the enemy frees) + defer the drops.
        Vector2 at = enemy.GlobalPosition;
        int figs = enemy.fada_fig_drop;
        bool buff = GD.Randf() < BuffDropChance();  // ramping chance, rolled at death (optional enemies drop too, if killed)
        Callable.From(() =>
        {
            SpawnFadaFigs(at, figs);
            if (buff)
                SpawnBuffDrop(at);
        }).CallDeferred();
        if (!enemy.optional)
            _alive -= 1;   // free a slot in the concurrency cap
    }

    /// <summary>Current buff-drop chance — ramps from <see cref="BuffDropBase"/> up to <see cref="BuffDropCap"/> as waves
    /// accrue (steady early → richer as the run wears on). A steady rate for now; retuned when seals arrive.</summary>
    private float BuffDropChance() => Mathf.Min(BuffDropCap, BuffDropBase + BuffDropStep * _waveCount);

    private void SpawnBuffDrop(Vector2 at)
    {
        var drop = new BuffDrop();
        _content.AddChild(drop);
        PlaceAt(drop, at + new Vector2(0, -12));
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
