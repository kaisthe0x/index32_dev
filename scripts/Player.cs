using Godot;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// A character-agnostic player. Every character shares the same animation set + normalised sprite canvas, so
/// switching is just swapping the SpriteFrames resource. C# port of <c>scripts/player.gd</c> (Phase 4b of the
/// migration) — the state machine, combat seam, surges, launch orbs, and the passive/buff dispatch.
///
/// <para>PUBLIC SURFACE stays snake_case: the scene and the C# combat components (Strike calling
/// <c>hold_animation</c>/<c>apply_lunge</c>) address these by exact name. Internals are idiomatic PascalCase.
/// Config is fully typed C#: the equipped move is an <see cref="Action"/> record, its tuning a <see cref="SegmentData"/>.</para>
/// </summary>
[Tool]
[GlobalClass]
public partial class Player : Combatant
{
    // --- signals (the HUD connects by these exact names) ---
    [Signal] public delegate void health_changedEventHandler(double current, double maximum);
    [Signal] public delegate void ruh_changedEventHandler(double current, double maximum);

    // --- path templates (mirror CharacterConfig; hardcoded so C# needn't read GDScript consts) ---
    private const string FramesPathTmpl = "res://resources/characters/{0}.tres";

    // --- bridged GDScript statics/singletons (cached in _Ready) ---
    private Sfx _sfx = null!;

    // =====================================================================================================
    // Character
    // =====================================================================================================
    private string _character = "khalid";

    [Export(PropertyHint.Enum, "khalid")]
    public string character
    {
        get => _character;
        set { _character = value; ApplyCharacter(); }
    }

    // =====================================================================================================
    // Health (SLOT-based — measured in half-blocks; see BaseMaxHealth / take_damage)
    // =====================================================================================================
    private float _maxHealth = 6.0f;

    [Export]
    public float max_health
    {
        get => _maxHealth;
        set { _maxHealth = Mathf.Max(value, 1.0f); health = Mathf.Min(health, _maxHealth); }
    }

    private float _health = 6.0f;

    public float health
    {
        get => _health;
        set
        {
            float clamped = Mathf.Clamp(value, 0.0f, _maxHealth);
            if (Mathf.IsEqualApprox(clamped, _health))
                return;
            _health = clamped;
            EmitSignal(SignalName.health_changed, _health, _maxHealth);
        }
    }

    // =====================================================================================================
    // Ruh (surge meter)
    // =====================================================================================================
    private const float RuhPerBlock = 100.0f; // one HUD "block" = one charge (the default surge cost)
    private const float RuhPerHit = 20.0f;     // Ruh gained per HIT landed (5 hits = 1 charge)
    private const float MaxRuhCap = 500.0f;    // hard ceiling: 5 charges
    private const float SpecialCooldown = 0.6f; // tiny anti-spam between specials (they cost no Ruh)

    /// <summary>Instance accessor so GDScript (HUD block sizing / run debug) can read the block size — it can't read a C# const.</summary>
    public float RUH_PER_BLOCK => RuhPerBlock;

    private float _ruhCap = 300.0f;

    [Export]
    public float ruh_cap
    {
        get => _ruhCap;
        set { _ruhCap = Mathf.Clamp(value, 0.0f, MaxRuhCap); ruh = Mathf.Min(ruh, _ruhCap); }
    }

    private float _ruh = 0.0f;

    public float ruh
    {
        get => _ruh;
        set
        {
            float clamped = Mathf.Clamp(value, 0.0f, _ruhCap);
            if (Mathf.IsEqualApprox(clamped, _ruh))
                return;
            _ruh = clamped;
            EmitSignal(SignalName.ruh_changed, _ruh, _ruhCap);
        }
    }

    // --- run-reward buffs (per-run, reset by begin_run). Public snake_case: Rewards mutates these. ---
    private const float BaseRuhCap = 300.0f;
    // Slot health: HP is measured in HALF-BLOCKS. 3 blocks = 6 half-blocks, and EVERY hit costs one half-block
    // regardless of damage (so 6 hits kill). begin_run fills to BaseMaxHealth.
    private const int HealthBlocks = 3;
    private const float BaseMaxHealth = HealthBlocks * 2.0f; // 3 blocks × 2 half-blocks = 6
    private const float HitCost = 1.0f;                      // one hit = half a block
    private const float SurgeHealHalfBlocks = 2.0f;          // the Nem surge restores one block
    private const float HealthWarnHalf = 0.5f;               // "health_half" cue at 1.5 blocks left
    private const float HealthWarnLow = 0.34f;               // "health_low" cue at ~1 block left

    public float damage_mult = 1.0f;
    public float run_mult = 1.0f;
    public int air_jump_bonus = 0;
    public float damage_taken_mult = 1.0f;   // Thick Hide
    public float slam_damage_mult = 1.0f;    // Meteor
    public float attack_reach_mult = 1.0f;   // Long Arm
    public int attack_projectile_bonus = 0;  // Split Shot (WIP)
    public bool impervious_until_hit = false; // Last Stand (WIP)
    public float special_radius_mult = 1.0f;  // Wide Impact (WIP)
    public float special_invuln_bonus = 0.0f; // Fortitude: extends any surge window
    public float jump_velocity_bonus = 1.0f;  // High Jump: multiplies applied jump velocity (all jumps)
    public int magnet_target_bonus = 0;        // Wider Pull: extra Come Closer magnet targets

    private readonly List<string> _rewardsTaken = new();
    private const string StartingDashEffect = "dash_default";
    private string _dashEffect = StartingDashEffect;

    // =====================================================================================================
    // Movement runtime state — SEEDED FROM CONFIG (Locomotion) in ApplyMovement; do not set here.
    // =====================================================================================================
    private float _runSpeedV, _acceleration, _friction, _runAnimSpeed;
    private float _jumpVelocity; private int _maxAirJumps; private float _gravity, _fallGravityScale;
    private float _dashSpeed, _dashTime, _dashCooldown, _dashAnimTime, _dashGravityScale;
    private float _slamSpeed, _slamMinClearance; private int _slamHoldFrame;
    private float _slamImpactDistance, _slamMinDrop, _slamMaxDrop, _slamMaxDamageMult;
    private float _landMinFallSpeed, _landPredictDistance;

    // Read-only snake_case views of the movement runtime vars, for the HUD debug stats panel.
    public float run_speed => _runSpeedV;
    public float jump_velocity => _jumpVelocity;
    public float dash_speed => _dashSpeed;
    public int max_air_jumps => _maxAirJumps;
    public float gravity => _gravity;
    public float slam_speed => _slamSpeed;

    [Export] public float attack_recovery = 0.12f;
    [Export] public float combo_reset_time = 0.45f;

    private const float DoubleJumpLean = 0.6f;

    public enum State { IDLE, RUN, JUMP, DASH, ATTACK, SPECIAL, LAND, SLAM, FALL, DEATH, SPAWN, HURT, SURGE, LAUNCH }

    // --- launch orbs (magnet traversal) ---
    private const float LaunchPullRange = 96.0f;
    private static readonly Vector2 LaunchBody = new(0.0f, -20.0f);
    private const float LaunchMagnetTime = 0.08f;
    private const float LaunchCd = 0.45f;

    private Node2D _launchOrb;
    private Vector2 _launchFrom = Vector2.Zero;
    private float _launchT = 0.0f;
    private Vector2 _launchVel = Vector2.Zero;
    private float _launchCdLeft = 0.0f;
    private Node2D _nearOrb;

    private State _state = State.IDLE;
    private int _facing = 1;
    private Action _currentAttack;   // the equipped attack (or null)
    private Action _currentSpecial;
    private Action _currentSurge;
    private readonly System.Collections.Generic.Dictionary<LoadoutCategory, string> _loadout = new();
    private SegmentData _activeHit = new();
    private float _dashLeft = 0.0f;
    private float _dashAnimLeft = 0.0f;
    private float _dashCd = 0.0f;
    private bool _dashCustom = false;
    private bool _blinkDash = false;
    private bool _blinkPhaseWalls = false;
    private bool _wasOnFloor = true;
    private float _fallPeak = 0.0f;
    private float _apexY = 0.0f;
    private int _airJumpsUsed = 0;
    private float _slamSpringBonus = 1.0f;   // Slam Spring: one-shot next-ground-jump height mult (consumed on use)
    private bool _jumpLaunch = false;
    private bool _dead = false;
    private bool _deathFinished = false;
    private bool _deathFrozen = false;
    private bool _fellOut = false;   // died by falling out of the arena — free-falls off-screen, no death animation
    private bool _slamImpacting = false;
    private float _slamStartY = 0.0f;
    private bool _justLanded = false;
    private int _comboStep = 0;
    private int _segEnd = 0;
    private bool _comboPlaying = false;
    private float _comboWindow = 0.0f;
    private float _recoveryLeft = 0.0f;
    private bool _bufferedSpecial = false;
    private bool _bufferedAttack = false;
    private bool _flurry = false;
    private float _stunLeft = 0.0f;
    private float _armorLeft = 0.0f;
    private float _holdLeft = 0.0f;
    private BlastStrike _channel = null;
    private readonly List<Passive> _passives = new();

    // --- surge window ---
    private float _surgeLeft = 0.0f;
    private float _iframesLeft = 0.0f;  // generic invulnerability window (grant_invuln) — the immunity buffs
    private bool _surgeInvuln = false;
    private float _surgeDmgMult = 1.0f;
    private float _surgeSpeedMult = 1.0f;
    private bool _surgeChannel = false;
    private bool _surgeAsleep = false;
    private float _surgeHealTarget = 0.0f;
    private float _surgeHealRate = 0.0f;
    private int _surgeSleepFrame = 0;
    private bool _surgeArmed = false;
    private SurgeSpec _armedSurge = null;
    private Node2D _specialAura = null;

    // --- shield / parry ---
    private float _parryLeft = 0.0f;
    [Export] public float parry_window = 0.25f;
    [Export] public float shield_reflect_mult = 1.0f;
    private float _shakeLeft = 0.0f, _shakeDur = 0.0f, _shakeAmp = 0.0f;
    [Export] public float shield_shake_amp = 4.0f;
    [Export] public float shield_shake_time = 0.18f;
    [Export] public bool flinch_on_all_damage = true;

    private float _specialCd = 0.0f;
    private float _attackCd = 0.0f;
    private FloatingHealthBar _cooldownBar = null;
    private AudioStreamPlayer _runSfx = null;
    private AudioStreamPlayer _slamDownSfx = null;
    private const float RuhFlashRefractory = 0.2f;
    private float _ruhFlashCd = 0.0f;
    private Tween _hairTween = null;
    private ShaderMaterial _tintMat = null;
    private bool _bodyIsLut = false;
    private GDict _hairBase = new();
    private static readonly Color HairAbsorbBase = new(2.6f, 1.7f, 0.5f);
    private static readonly Color HairAbsorbA = new(2.3f, 1.0f, 0.35f);
    private static readonly Color HairAbsorbB = new(1.9f, 0.6f, 0.25f);

    private ParticleDirector _particles = null;
    private Hurtbox _hurtbox = null;
    private StatusOverlay _status = null;
    private AnimatedSprite2D _sprite = null;

    // =====================================================================================================
    // Lifecycle
    // =====================================================================================================
    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        health = max_health;
        ApplyCharacter();
        if (Engine.IsEditorHint())
            return;
        _sfx = GetNode<Sfx>("/root/Sfx");
        _sprite.AnimationFinished += OnAnimationFinished;
        _sprite.AnimationLooped += OnAnimationLooped;

        _particles = new ParticleDirector();
        AddChild(_particles);
        _particles.setup(_sprite);
        _particles.set_character(character);

        BuildCombat();
        _sprite.FrameChanged += OnFrameChanged;

        // Seed listeners that connected before _ready (the setters stay silent on no-change).
        EmitSignal(SignalName.health_changed, health, max_health);
        EmitSignal(SignalName.ruh_changed, ruh, ruh_cap);
    }

    private void ApplyCharacter()
    {
        var sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite == null)
            return;
        string path = string.Format(FramesPathTmpl, character);
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"No SpriteFrames for character '{character}' at {path}");
            return;
        }
        sprite.SpriteFrames = GD.Load<SpriteFrames>(path);
        string matPath = $"res://resources/{character}_tint.tres";
        if (character == "khalid")
        {
            // Khalid wears the material-aware palette LUT (recolour + glow / hair-flow effects).
            _tintMat = PaletteConfig.MakeMaterial();
            _bodyIsLut = true;
            sprite.Material = _tintMat;
            _hairBase = new GDict();
        }
        else
        {
            var mat = ResourceLoader.Exists(matPath) ? GD.Load<Material>(matPath) : null;
            _bodyIsLut = false;
            if (mat is ShaderMaterial sm)
            {
                _tintMat = (ShaderMaterial)sm.Duplicate();
                sprite.Material = _tintMat;
                Variant br = _tintMat.GetShaderParameter("base_red");
                Variant aa = _tintMat.GetShaderParameter("accent_a");
                Variant ab = _tintMat.GetShaderParameter("accent_b");
                _hairBase = (br.VariantType == Variant.Type.Color && aa.VariantType == Variant.Type.Color && ab.VariantType == Variant.Type.Color)
                    ? new GDict { { "base_red", br }, { "accent_a", aa }, { "accent_b", ab } }
                    : new GDict();
            }
            else
            {
                _tintMat = null;
                _hairBase = new GDict();
                sprite.Material = mat;
            }
        }
        ApplyLoadout();
        AnchorToFeet(sprite);
        _comboStep = 0;
        _comboWindow = 0.0f;
        _comboPlaying = false;
        _bufferedSpecial = false;
        _bufferedAttack = false;
        _flurry = false;
        _attackCd = 0.0f;
        if (!Engine.IsEditorHint())
            _state = State.IDLE;
        sprite.SpeedScale = 1.0f;
        sprite.Play(AnimationFor(_state));
        SeedPassives();
        _particles?.set_character(character);
    }

    // =====================================================================================================
    // Loadout
    // =====================================================================================================
    private string LoadoutGet(LoadoutCategory cat, string def) => _loadout.GetValueOrDefault(cat, def);

    private void ApplyLoadout()
    {
        _currentAttack = GetAction(LoadoutCategory.Attack.Kind(), LoadoutGet(LoadoutCategory.Attack, ""));
        _currentSpecial = GetAction(LoadoutCategory.Special.Kind(), LoadoutGet(LoadoutCategory.Special, ""));
        _currentSurge = GetAction(LoadoutCategory.Surge.Kind(), LoadoutGet(LoadoutCategory.Surge, ""));
        foreach (var cat in LoadoutCategories.Movement)
            ApplyMovement(cat, LoadoutGet(cat, "default"));
    }

    private Action GetAction(string kind, string id)
    {
        return Actions.GetAction(character, kind, id);
    }

    private void ApplyMovement(LoadoutCategory category, string optionId)
    {
        var a = GetAction(category.Kind(), optionId);
        if (a?.Move is not Locomotion m)
            return;
        switch (category)
        {
            case LoadoutCategory.Run:
                _runSpeedV = m.run_speed * run_mult;
                _acceleration = m.acceleration;
                _friction = m.friction;
                _runAnimSpeed = m.run_anim_speed;
                break;
            case LoadoutCategory.Jump:
                _jumpVelocity = m.jump_velocity;
                _maxAirJumps = m.air_jumps + air_jump_bonus;
                _gravity = m.gravity;
                _fallGravityScale = m.fall_gravity_scale;
                _landMinFallSpeed = m.land_min_fall_speed;
                _landPredictDistance = m.land_predict_distance;
                break;
            case LoadoutCategory.Dash:
                _dashSpeed = m.dash_speed;
                _dashTime = m.dash_time;
                _dashCooldown = m.dash_cooldown;
                _dashAnimTime = m.dash_anim_time;
                _dashGravityScale = m.dash_gravity_scale;
                _blinkDash = m.blink;
                break;
            case LoadoutCategory.Slam:
                _slamSpeed = m.slam_speed;
                _slamMinClearance = m.slam_min_clearance;
                _slamHoldFrame = m.slam_hold_frame;
                _slamImpactDistance = m.slam_impact_distance;
                _slamMinDrop = m.slam_min_drop;
                _slamMaxDrop = m.slam_max_drop;
                _slamMaxDamageMult = m.slam_max_damage_mult;
                break;
        }
    }

    public void equip(LoadoutCategory category, string optionId)
    {
        _loadout[category] = optionId;
        ApplyLoadout();
    }

    public string loadout_id(LoadoutCategory category)
    {
        return _loadout.TryGetValue(category, out var id) ? id : Loadout.DefaultId(character, category);
    }

    public GArr loadout_choices() => Loadout.SwapChoices(character, _loadout);

    private void SeedPassives()
    {
        foreach (var p in _passives)
            p.Teardown(this);
        _passives.Clear();
        RefreshBuffHud();
        if (Engine.IsEditorHint())
            return;
        var ability = CharacterAbilityFor(character);
        if (ability != null)
            add_passive(ability);
    }

    /// <summary>A character's intrinsic ability, or null. Khalid ships without one. (Add a case when a character gets a C# CharacterAbility.)</summary>
    private static Passive CharacterAbilityFor(string character) => null;

    public void add_passive(Passive p)
    {
        // REPLACE-IN-PLACE: a Buff with a non-empty family supersedes any held buff of the same family.
        if (p is Buff b && b.Family != "")
        {
            foreach (var existing in new List<Passive>(_passives))
                if (existing is Buff eb && eb.Family == b.Family)
                {
                    existing.Teardown(this);
                    _passives.Remove(existing);
                }
        }
        _passives.Add(p);
        p.Setup(this);
        RefreshBuffHud();
    }

    /// <summary>Push the current buff loadout to the HUD's active-buff list (autoload).</summary>
    private void RefreshBuffHud() => GetNodeOrNull<HUD>("/root/HUD")?.RefreshBuffs(_passives);

    /// <summary>Emitted whenever fada_figs are collected — carries the current spendable balance + the run's LIFETIME
    /// total collected. RunManager listens to fire the milestone buff-menu off the lifetime total.</summary>
    [Signal] public delegate void fada_collectedEventHandler(int balance, int lifetime);

    /// <summary>FadaFigs banked this run — the SPENDABLE balance (mystery box spends it). Reset by <see cref="begin_run"/>.</summary>
    public int fada_figs { get; private set; } = 0;

    /// <summary>Total fada_figs collected this run (monotonic — the box's spending never lowers it). Drives the free
    /// milestone buff-menu, so spending at the box doesn't cost menu progress. Reset by <see cref="begin_run"/>.</summary>
    public int fada_lifetime { get; private set; } = 0;

    /// <summary>Collect <paramref name="n"/> fada_fig(s) (a FadaFig touched the player) — bank them + update the HUD.</summary>
    public void collect_fada_fig(int n = 1)
    {
        fada_figs += n;
        fada_lifetime += n;
        GetNodeOrNull<HUD>("/root/HUD")?.SetFadaFigs(fada_figs);
        EmitSignal(SignalName.fada_collected, fada_figs, fada_lifetime);
    }

    /// <summary>Try to spend <paramref name="cost"/> fada_figs (the mystery box). True + deducts if affordable; else false.
    /// Only the spendable balance moves — <see cref="fada_lifetime"/> (menu progress) is untouched.</summary>
    public bool spend_fada_figs(int cost)
    {
        if (cost <= 0 || fada_figs < cost)
            return false;
        fada_figs -= cost;
        GetNodeOrNull<HUD>("/root/HUD")?.SetFadaFigs(fada_figs);
        return true;
    }

    public void notify_hit_dealt(float amount, Node target)
    {
        foreach (var p in _passives)
            p.OnHitDealt(this, amount, target);
    }

    /// <summary>A player attack hitbox deactivated having hit nobody (a whiff). Called by <see cref="Hitbox"/> on a
    /// zero-victim deactivation of a player-sourced attack box — dispatches OnMiss (Instant Reset, etc.).</summary>
    public void notify_miss()
    {
        foreach (var p in _passives)
            p.OnMiss(this);
    }

    /// <summary>An attack swing's animation finished + recovered to neutral (no chain/cancel) — dispatches OnAnimEnd (Follow-through).</summary>
    private void NotifyAttackAnimEnd()
    {
        foreach (var p in _passives)
            p.OnAnimEnd(this);
    }

    public void record_reward(string id) => _rewardsTaken.Add(id);

    public GArr rewards_taken()
    {
        var arr = new GArr();
        foreach (var id in _rewardsTaken)
            arr.Add(id);
        return arr;
    }

    /// <summary>Advance the run one LEVEL: tick down every level-scoped buff's lifetime and tear out any that expired (doc: temporary buffs). Call from RunManager on level advance.</summary>
    public void advance_level()
    {
        foreach (var existing in new List<Passive>(_passives))
            if (existing is Buff b && b.TickLevelAndExpired())
            {
                existing.Teardown(this);
                _passives.Remove(existing);
            }
    }

    public int get_state() => (int)_state;
    public bool is_spawning() => _state == State.SPAWN;

    /// <summary>Which way Khalid faces: +1 right, -1 left (RunManager spawns grunts on the other side).</summary>
    public int facing => _facing;
    public Action current_attack() => _currentAttack;
    public Action current_special() => _currentSpecial;

    // =====================================================================================================
    // Action helpers (thin typed accessors over the equipped Action)
    // =====================================================================================================
    private static StringName Anim(Action a) => a.Animation;
    private static bool HasTag(Action a, string t) => a != null && a.HasTag(t);
    private static float CooldownOf(Action a) => a.Cooldown;
    private static bool IsFlurry(Action a) => a.IsFlurry;

    private bool HasAnim(StringName anim) =>
        _sprite != null && _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim);
    public bool has_anim(StringName anim) => HasAnim(anim);

    private bool AirAttackOk() => _currentAttack != null && HasTag(_currentAttack, "air");

    private float AnimDuration(StringName anim)
    {
        if (!HasAnim(anim))
            return 0.0f;
        var sf = _sprite.SpriteFrames;
        float fps = (float)sf.GetAnimationSpeed(anim);
        if (fps <= 0.0f)
            return 0.0f;
        float total = 0.0f;
        for (int i = 0; i < sf.GetFrameCount(anim); i++)
            total += (float)sf.GetFrameDuration(anim, i) / fps;
        return total;
    }

    private void FireEffect(string anim, float tilt = 0.0f) => _particles?.fire_effect(anim, tilt);
    public void fire_effect(string anim, float tilt = 0.0f) => FireEffect(anim, tilt);

    private void DoBlink()
    {
        var motion = new Vector2(_dashSpeed * _dashTime * _facing, 0.0f);
        FireEffect("blink_out");
        if (_blinkPhaseWalls)
            GlobalPosition += motion;
        else
            MoveAndCollide(motion);
        SetVelX(0.0f);
        FireEffect("blink_in");
        Modulate = new Color(2.2f, 2.2f, 2.2f);
        CreateTween().TweenProperty(this, "modulate", new Color(1, 1, 1), 0.18);
    }

    // =====================================================================================================
    // Damage / health
    // =====================================================================================================
    public void take_damage(float amount)
    {
        // Slot health: every hit costs a flat HALF-BLOCK, regardless of `amount` (so damage-reduction is inert now).
        // `amount` is kept for callers but no longer scales the HP loss, and there's no damage number to show.
        float before = health;
        health -= HitCost;
        WarnLowHealth(before, health);
        _sfx.play_random(new GArr { "hurt.1", "hurt.2", "hurt.3" }, 0.0f, (float)GD.RandRange(0.95, 1.06));
        // Colour flash over the hurt anim, via the palette shader's `flash` uniform (a plain modulate is swallowed).
        FlashSprite(_sprite, Combat.DamageFlash, Combat.DamageFlashTime);
        if (health <= 0.0f && !_dead)
            Die();
    }

    private void WarnLowHealth(float oldHp, float newHp)
    {
        if (max_health <= 0.0f || newHp >= oldHp)
            return;
        float oldR = oldHp / max_health;
        float newR = newHp / max_health;
        if (oldR > HealthWarnLow && newR <= HealthWarnLow)
            _sfx.play("health_low");
        else if (oldR > HealthWarnHalf && newR <= HealthWarnHalf)
            _sfx.play("health_half");
    }

    private void Shake(float amp, float time)
    {
        if (time <= 0.0f)
            return;
        _shakeAmp = amp;
        _shakeDur = time;
        _shakeLeft = time;
    }

    public void heal(float amount) => health = Mathf.Min(health + amount, max_health);

    /// <summary>Kill Khalid outright: he fell out of the arena. Ignores i-frames / Aegis (nothing survives the void).
    /// Unlike a normal death there's no death animation — he keeps his fall animation and keeps dropping, out of
    /// control (<see cref="ProcessFreefall"/>), with its own sound; RunManager ends the run.</summary>
    public void fall_to_death()
    {
        if (_dead)
            return;
        health = 0.0f;
        Die(fell: true);
    }

    /// <summary>True once the player has died by falling out of the arena (RunManager runs the fall-death flow).</summary>
    public bool fell_out() => _fellOut;

    /// <summary>Grant a generic invulnerability window (the immunity buffs: dash/jump/slam/on-hit). Refreshes to the longer.</summary>
    public void grant_invuln(float seconds) => _iframesLeft = Mathf.Max(_iframesLeft, seconds);

    /// <summary>Add air jumps for the run (ExtraAirJump buff) — bumps the bonus AND the live max. Undo with n &lt; 0.</summary>
    public void add_air_jumps(int n) { air_jump_bonus += n; _maxAirJumps += n; }

    /// <summary>Prime the NEXT ground jump with a height multiplier (Slam Spring) — one-shot, consumed on that jump.</summary>
    public void set_jump_spring(float mult) => _slamSpringBonus = mult;

    /// <summary>Lower the current attack cooldown (Bakshen Overcharge on-hit). Clamped to zero (a huge value = full reset).</summary>
    public void reduce_attack_cooldown(float seconds) => _attackCd = Mathf.Max(_attackCd - seconds, 0.0f);

    /// <summary>Zero the dash cooldown so the follow-up dash is free (Chain Dash on-dash).</summary>
    public void reset_dash_cooldown() => _dashCd = 0.0f;

    /// <summary>Global cooldown fairness: an action whose windup is interrupted by a stagger BEFORE its hit came out
    /// never actually fired, so it shouldn't burn its cooldown. An ATTACK still short of its hit frame
    /// (<see cref="_segEnd"/>) or a SPECIAL short of its strike frame is "uncommitted" → zero the matching cooldown.
    /// Called from <see cref="OnHurt"/> the instant a hurt is about to knock the player into HURT (windup cancelled).</summary>
    private void RefundUncommittedCooldown()
    {
        if (_sprite == null)
            return;
        if (_state == State.ATTACK && _sprite.Frame < _segEnd)
            _attackCd = 0.0f;
        else if (_state == State.SPECIAL && _sprite.Frame < SpecialStrikeFrame())
            _specialCd = 0.0f;
    }

    // --- DEBUG: playtest the buff catalog (triggered from RunManager's input; REMOVE before release) -------
    private int _debugBuffIdx = 0;

    /// <summary>DEBUG: grant the next wired catalog buff (at Hot), cycling through the whole set.</summary>
    public void debug_grant_next_buff()
    {
        var ids = new List<string>(BuffCatalog.FACTORIES.Keys);
        if (ids.Count == 0)
            return;
        string id = ids[_debugBuffIdx++ % ids.Count];
        var buff = BuffCatalog.Make(id, Tier.Hot);
        if (buff != null)
        {
            add_passive(buff);
            GD.Print($"[DEBUG] granted buff: {id} (Hot)");
        }
    }

    /// <summary>DEBUG: clear all granted buffs (keeps the character ability).</summary>
    public void debug_clear_buffs()
    {
        foreach (var p in new List<Passive>(_passives))
            if (p is Buff)
            {
                p.Teardown(this);
                _passives.Remove(p);
            }
        RefreshBuffHud();
        GD.Print("[DEBUG] cleared granted buffs");
    }

    /// <summary>Stun every enemy within `radius` for `seconds` (Slam Quake) — the surge's stun-sweep pattern.</summary>
    public void stun_nearby(float radius, float seconds)
    {
        foreach (Node e in GetTree().GetNodesInGroup("enemies"))
        {
            if (e is not Node2D en || !en.HasMethod("apply_hit"))
                continue;
            if (GlobalPosition.DistanceTo(en.GlobalPosition) <= radius)
                en.Call("apply_hit", new Hit
                {
                    Stun = seconds,
                    Source = this,
                    StatusColor = new Color(1.0f, 0.85f, 0.2f, 0.6f),
                    StatusTime = seconds,
                });
        }
    }

    /// <summary>The jump velocity to apply, folding in High Jump (all jumps) and, for a GROUND jump, a one-shot Slam Spring (consumed here).</summary>
    private float AppliedJumpVelocity(bool ground)
    {
        float v = _jumpVelocity * jump_velocity_bonus;
        if (ground && !Mathf.IsEqualApprox(_slamSpringBonus, 1.0f))
        {
            v *= _slamSpringBonus;
            _slamSpringBonus = 1.0f;
        }
        return v;
    }

    public bool gain_ruh_on_hit()
    {
        float before = ruh;
        ruh += RuhPerHit;
        return Mathf.FloorToInt(ruh / RuhPerBlock) > Mathf.FloorToInt(before / RuhPerBlock);
    }

    public void on_ruh_absorbed(bool completedCharge)
    {
        if (!completedCharge && _ruhFlashCd > 0.0f)
            return;
        _ruhFlashCd = RuhFlashRefractory;
        HairSurge(completedCharge ? 1.0f : 0.6f, completedCharge ? 0.6f : 0.35f);
        _sfx.play("ruh_absorb", 0.0f, completedCharge ? 1.12f : 1.0f);
    }

    private void HairSurge(float strength, float dur)
    {
        if (_tintMat == null || (!_bodyIsLut && _hairBase.Count == 0))
            return;
        if (_hairTween != null && _hairTween.IsValid())
            _hairTween.Kill();
        _hairTween = CreateTween();
        _hairTween.TweenMethod(Callable.From<float>(SetHairMix), 0.0f, strength, dur * 0.35f).SetEase(Tween.EaseType.Out);
        _hairTween.TweenMethod(Callable.From<float>(SetHairMix), strength, 0.0f, dur * 0.65f).SetEase(Tween.EaseType.In);
    }

    private void SetHairMix(float f)
    {
        if (_tintMat == null)
            return;
        if (_bodyIsLut)
        {
            _tintMat.SetShaderParameter("hair_surge", f);
            return;
        }
        _tintMat.SetShaderParameter("base_red", ((Color)_hairBase["base_red"]).Lerp(HairAbsorbBase, f));
        _tintMat.SetShaderParameter("accent_a", ((Color)_hairBase["accent_a"]).Lerp(HairAbsorbA, f));
        _tintMat.SetShaderParameter("accent_b", ((Color)_hairBase["accent_b"]).Lerp(HairAbsorbB, f));
    }

    // =====================================================================================================
    // Combat build + tuning seam
    // =====================================================================================================
    private static CollisionShape2D MakeBox(Vector2 size, Vector2 offset) =>
        new() { Shape = new RectangleShape2D { Size = size }, Position = offset };

    private void BuildCombat()
    {
        AddToGroup("player");
        CollisionLayer = (uint)Combat.Layer.PlayerBody;
        CollisionMask = (uint)Combat.Layer.World;

        _hurtbox = new Hurtbox { CollisionLayer = (uint)Combat.Layer.PlayerHurt, CollisionMask = 0 };
        _hurtbox.AddChild(MakeBox(new Vector2(16, 30), new Vector2(0, -15)));
        AddChild(_hurtbox);
        _hurtbox.hurt += OnHurt;

        _status = new StatusOverlay();
        AddChild(_status);
        _status.Setup(_sprite);

        _cooldownBar = new FloatingHealthBar
        {
            FillColor = new Color(1.0f, 0.08f, 0.08f),
            Position = new Vector2(0, -52),
            Visible = false,
        };
        AddChild(_cooldownBar);

        if (!Engine.IsEditorHint())
        {
            _runSfx = _sfx.make_loop("run");
            if (_runSfx != null)
                AddChild(_runSfx);
            _slamDownSfx = _sfx.make_oneshot("slam_down");
            if (_slamDownSfx != null)
                AddChild(_slamDownSfx);
        }
    }

    /// <summary>THE BUFF SEAM. Resolve the effective per-hit tuning of `action`'s combo segment `seg`.</summary>
    private SegmentData ResolveTuning(Action action, int seg = 0)
    {
        if (action == null)
            return new SegmentData();
        SegmentData baseT = action.Segment(seg).Clone();
        float dmgMult = damage_mult * _surgeDmgMult;
        if (!Mathf.IsEqualApprox(dmgMult, 1.0f) && baseT.Damage.HasValue)
            baseT.Damage *= dmgMult;
        if (!Mathf.IsEqualApprox(attack_reach_mult, 1.0f))
        {
            if (baseT.Extents.HasValue)
                baseT.Extents *= attack_reach_mult;
            if (baseT.X.HasValue)
                baseT.X *= attack_reach_mult;
        }
        foreach (var p in _passives)
            baseT = p.ModifyTuning(this, action, seg, baseT);
        return baseT;
    }

    public SegmentData active_hit() => _activeHit;

    private bool IsShielding() =>
        _state == State.SPECIAL && _currentSpecial != null && HasTag(_currentSpecial, "shield");

    private void OnHurt(Hit hit)
    {
        if (_iframesLeft > 0.0f)
            return;  // generic i-frames (immunity buffs) — ignore the hit entirely
        if (IsShielding())
        {
            bool fromBehind = hit.Source is Node2D src2
                && Mathf.Sign(src2.GlobalPosition.X - GlobalPosition.X) == -_facing;
            if (!fromBehind)
            {
                if (_parryLeft > 0.0f)
                {
                    if (shield_reflect_mult > 0.0f && hit.Source is Enemy reflEnemy && hit.Amount > 0.0f)
                    {
                        var back = new Hit { Amount = hit.Amount * shield_reflect_mult, Knockback = 120.0f, Source = this };
                        reflEnemy.apply_hit(back);
                    }
                    _sfx.play("redere_shield_parry");
                    foreach (var p in _passives)
                        p.OnParry(this, hit);
                }
                else
                {
                    _sfx.play("redere_shield_block");
                }
                Flash(_sprite);
                Shake(shield_shake_amp, shield_shake_time);
                return;
            }
        }
        if (_surgeArmed && hit.Source is Enemy)
        {
            TriggerWara();
            return;
        }
        take_damage(hit.Amount);
        if (_dead)
            return;
        foreach (var p in _passives)
            p.OnHurt(this, hit);
        if (_state == State.LAUNCH)
        {
            _launchOrb = null;
            _launchCdLeft = LaunchCd;
        }
        if (_channel != null && IsInstanceValid(_channel) && _channel.interrupt_on_hurt)
        {
            _channel.cancel();
            _holdLeft = 0.0f;
            _sprite.Play();
        }
        _channel = null;
        if (_surgeChannel)
        {
            EndSurge();
            if (_state == State.SURGE)
                Enter(State.IDLE);
        }
        if (_armorLeft > 0.0f)
            return;
        float stagger = ApplyKnockback(hit, _facing);
        if (flinch_on_all_damage || stagger > 0.0f)
        {
            RefundUncommittedCooldown(); // staggered mid-windup: read state BEFORE we leave ATTACK/SPECIAL for HURT
            float flinch = Mathf.Max(stagger, AnimDuration("hurt"));
            if (_state == State.HURT)
                _stunLeft = Mathf.Max(_stunLeft, flinch);
            else
            {
                _stunLeft = flinch;
                Enter(State.HURT);
            }
            _comboPlaying = false;
            _flurry = false;
            _bufferedSpecial = false;
        }
        if (hit.StatusColor.A > 0.0f)
            _status.ShowFor(hit.StatusColor, hit.StatusTime);
        if (hit.VictimVfx != null)
            SpawnVictimVfx(hit.VictimVfx, hit.VictimVfxTime);
    }

    public void apply_lunge(float impulse) => SetVelX(impulse * _facing);

    public void set_armor(float duration) => _armorLeft = Mathf.Max(_armorLeft, duration);

    public void set_dash_effect(string effect) => _dashEffect = effect;

    private float RunSpeed() => _runSpeedV * _surgeSpeedMult;

    // =====================================================================================================
    // Surges
    // =====================================================================================================
    private void BeginSurge(SurgeSpec s)
    {
        EndSurge();
        _surgeInvuln = s.invuln;
        _surgeDmgMult = s.damage_mult;
        _surgeSpeedMult = s.speed_mult;
        _surgeChannel = s.channel;
        _surgeArmed = s.trigger == "hit";
        if (_surgeArmed)
        {
            _armedSurge = s;
            _surgeLeft = 0.0f;
        }
        else if (_surgeChannel)
        {
            _surgeAsleep = false;
            _surgeLeft = 0.0f;
            // Slot health: a healing surge (heal_frac > 0, i.e. Nem) restores ONE block over its channel.
            _surgeHealTarget = Mathf.Min(health + SurgeHealHalfBlocks, max_health);
            _surgeHealRate = (_surgeHealTarget - health) / Mathf.Max(s.duration, 0.01f);
            var anim = Anim(_currentSurge);
            int fcount = (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim))
                ? _sprite.SpriteFrames.GetFrameCount(anim) : 0;
            _surgeSleepFrame = Mathf.Max(fcount - 2, 0);
        }
        else
        {
            _surgeLeft = s.duration + special_invuln_bonus;
        }
        string aura = s.aura;
        if (aura != "" && ResourceLoader.Exists(aura))
        {
            var scene = GD.Load<PackedScene>(aura);
            _specialAura = scene?.Instantiate() as Node2D;
            if (_specialAura != null)
            {
                Variant mc = _specialAura.Get("moon_color");
                if (mc.VariantType == Variant.Type.Color)
                    _specialAura.Set("moon_color", VfxPalette.Recolor(mc.As<Color>()));
                VfxPalette.RecolorTree(_specialAura);
                AddChild(_specialAura);
            }
        }
    }

    private void TrySurge()
    {
        if (_dead || _currentSurge?.Surge == null)
            return;
        if (_surgeChannel || _surgeArmed)
            return;
        if (!Input.IsActionJustPressed("surge"))
            return;
        var s = _currentSurge.Surge;
        if (ruh < s.cost)
            return;
        ruh -= s.cost;
        BeginSurge(s);
        Flash(_sprite);
        _sfx.play(Anim(_currentSurge).ToString());
        if (_state != State.SPAWN && HasAnim(Anim(_currentSurge)))
            Enter(State.SURGE);
    }

    private void TickSurge(float delta)
    {
        if (_surgeLeft <= 0.0f)
            return;
        _surgeLeft -= delta;
        if (_surgeLeft <= 0.0f)
            EndSurge();
    }

    private void EndSurge()
    {
        _surgeLeft = 0.0f;
        _surgeInvuln = false;
        _surgeDmgMult = 1.0f;
        _surgeSpeedMult = 1.0f;
        _surgeArmed = false;
        _armedSurge = null;
        if (_surgeChannel)
        {
            _surgeChannel = false;
            _surgeAsleep = false;
            _sprite?.Play();
        }
        if (IsInstanceValid(_specialAura))
        {
            var aura = _specialAura;
            var tw = aura.CreateTween();
            tw.TweenProperty(aura, "modulate:a", 0.0, 0.3);
            tw.TweenCallback(Callable.From(aura.QueueFree));
        }
        _specialAura = null;
    }

    private void TriggerWara()
    {
        var s = _armedSurge;
        if (s == null)
        {
            EndSurge();
            return;
        }
        float stunRadius = s.stun_radius;
        float stunTime = s.stun_time;
        foreach (Node e in GetTree().GetNodesInGroup("enemies"))
        {
            if (e is not Node2D en || !en.HasMethod("apply_hit"))
                continue;
            if (GlobalPosition.DistanceTo(en.GlobalPosition) <= stunRadius)
            {
                var h = new Hit
                {
                    Stun = stunTime,
                    Source = this,
                    StatusColor = new Color(1.0f, 0.85f, 0.2f, 0.6f),
                    StatusTime = stunTime,
                };
                en.Call("apply_hit", h);
            }
        }
        string burst = s.burst;
        if (burst != "" && ResourceLoader.Exists(burst))
        {
            var scene = GD.Load<PackedScene>(burst);
            var b = scene?.Instantiate() as Node2D;
            if (b != null)
            {
                VfxPalette.RecolorTree(b);
                AddChild(b);
                GetTree().CreateTimer(1.5).Timeout += b.QueueFree;
            }
        }
        _sfx.play("surge_wara_trigger");
        Flash(_sprite);
        EndSurge();
    }

    public void hold_animation(double duration, BlastStrike effect)
    {
        if (duration <= 0.0 || _sprite == null)
            return;
        _holdLeft = Mathf.Max(_holdLeft, (float)duration);
        _channel = effect;
        _sprite.Pause();
    }

    private void OnFrameChanged()
    {
        if (_state == State.SPECIAL)
        {
            if (_sprite.Frame == SpecialStrikeFrame())
                foreach (var p in _passives)
                    p.OnSpecialStrike(this);
            return;
        }
        int loopTo = LoopMeta("loop_to");
        if (loopTo >= 0 && _sprite.Frame > loopTo)
            _sprite.SetFrameAndProgress(Mathf.Max(LoopMeta("loop_from"), 0), 0.0f);
    }

    private int SpecialStrikeFrame()
    {
        var hits = AnimMeta.HitFrames(_sprite.SpriteFrames, Anim(_currentSpecial));
        if (hits.Count > 0)
            return hits[0].As<int>();
        return _sprite.SpriteFrames.GetFrameCount(Anim(_currentSpecial)) / 2;
    }

    public bool is_dead() => _dead;
    public bool death_complete() => _dead && _deathFinished;

    public void release_death()
    {
        if (!_deathFrozen)
            return;
        _deathFrozen = false;
        _sprite?.Play();
    }

    /// <summary>Common death: stop everything in progress and disable the hurtbox. A normal death then plays the death
    /// animation; a <paramref name="fell"/> death (out of the arena) skips it and free-falls in the fall animation.</summary>
    private void Die(bool fell = false)
    {
        if (_dead)
            return;
        _dead = true;
        _deathFinished = false;
        _sfx.play(fell ? "player_fall_death" : "player_death");
        _stunLeft = 0.0f;
        _comboPlaying = false;
        _flurry = false;
        _holdLeft = 0.0f;
        EndSurge();
        if (_channel != null && IsInstanceValid(_channel))
            _channel.cancel();
        _channel = null;
        _launchOrb = null;
        if (_hurtbox != null)
            // Die() runs inside the hurtbox's hit-signal flush; a direct set is blocked while physics
            // queries flush ("Function blocked during in/out signal"), so defer it to after the flush.
            _hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
        if (fell)
        {
            _fellOut = true;
            _deathFinished = true; // no death animation to wait for
            if (HasFall())
                _sprite.Play("fall");
            return;
        }
        if (HasAnim("death"))
            Enter(State.DEATH);
        else
            _deathFinished = true;
    }

    /// <summary>Fell out of the arena: no input, no state machine — gravity just carries him down in the fall animation.</summary>
    private void ProcessFreefall(float delta)
    {
        AddVelY(_gravity * _fallGravityScale * delta);
        MoveAndSlide();
    }

    private void ProcessDeath(float delta)
    {
        SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * delta));
        if (IsOnFloor())
            SetVelY(0.0f);
        else
            AddVelY(_gravity * delta);
    }

    private void ProcessSpawn(float delta)
    {
        SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * delta));
        if (IsOnFloor())
            SetVelY(0.0f);
        else
            AddVelY(_gravity * delta);
    }

    public void spawn()
    {
        Velocity = Vector2.Zero;
        if (HasAnim("spawn"))
            Enter(State.SPAWN);
        else
        {
            if (_hurtbox != null)
                _hurtbox.Monitorable = true;
            Enter(State.IDLE);
        }
    }

    public void begin_run()
    {
        _dead = false;
        _deathFinished = false;
        _fellOut = false;
        fada_figs = 0;
        fada_lifetime = 0;
        GetNodeOrNull<HUD>("/root/HUD")?.SetFadaFigs(0);
        EndSurge();
        _shakeLeft = 0.0f;
        if (_sprite != null)
            _sprite.Position = Vector2.Zero;
        _parryLeft = 0.0f;
        damage_mult = 1.0f;
        run_mult = 1.0f;
        damage_taken_mult = 1.0f;
        slam_damage_mult = 1.0f;
        attack_reach_mult = 1.0f;
        attack_projectile_bonus = 0;
        impervious_until_hit = false;
        special_radius_mult = 1.0f;
        _dashEffect = StartingDashEffect;
        special_invuln_bonus = 0.0f;
        _iframesLeft = 0.0f;
        jump_velocity_bonus = 1.0f;
        _slamSpringBonus = 1.0f;
        magnet_target_bonus = 0;
        ruh_cap = BaseRuhCap;
        air_jump_bonus = 0;
        _rewardsTaken.Clear();
        max_health = BaseMaxHealth;
        _loadout.Clear();
        ApplyCharacter();
        health = max_health;
        ruh = ruh_cap;
        Velocity = Vector2.Zero;
        spawn();
    }

    // =====================================================================================================
    // Physics
    // =====================================================================================================
    public override void _PhysicsProcess(double deltaD)
    {
        if (Engine.IsEditorHint())
            return;
        float delta = (float)deltaD;
        if (_fellOut)
        {
            ProcessFreefall(delta);
            return;
        }

        _dashCd = Mathf.Max(_dashCd - delta, 0.0f);
        _specialCd = Mathf.Max(_specialCd - delta, 0.0f);
        _launchCdLeft = Mathf.Max(_launchCdLeft - delta, 0.0f);
        UpdateOrbProximity();
        TrySurge();
        _attackCd = Mathf.Max(_attackCd - delta, 0.0f);
        UpdateCooldownBar();
        _ruhFlashCd = Mathf.Max(_ruhFlashCd - delta, 0.0f);
        _armorLeft = Mathf.Max(_armorLeft - delta, 0.0f);
        _iframesLeft = Mathf.Max(_iframesLeft - delta, 0.0f);
        if (_holdLeft > 0.0f)
        {
            _holdLeft = Mathf.Max(_holdLeft - delta, 0.0f);
            if (_holdLeft <= 0.0f && _sprite != null)
            {
                _sprite.Play();
                _channel = null;
            }
        }

        bool onFloor = IsOnFloor();
        if (!onFloor)
        {
            if (_wasOnFloor)
                _apexY = GlobalPosition.Y;
            _fallPeak = Mathf.Max(_fallPeak, Velocity.Y);
            _apexY = Mathf.Min(_apexY, GlobalPosition.Y);
        }
        _justLanded = onFloor && !_wasOnFloor && _fallPeak >= _landMinFallSpeed;
        if (onFloor && !_wasOnFloor && _passives.Count > 0)
        {
            float drop = Mathf.Max(GlobalPosition.Y - _apexY, 0.0f);
            foreach (var p in _passives)
                p.OnLand(this, drop, _fallPeak);
        }
        if (onFloor)
        {
            _fallPeak = 0.0f;
            _airJumpsUsed = 0;
        }
        _wasOnFloor = onFloor;

        if (_state == State.DEATH)
            ProcessDeath(delta);
        else if (_state == State.SPAWN)
            ProcessSpawn(delta);
        else if (_stunLeft > 0.0f)
            ProcessStun(delta);
        else if (_state == State.DASH)
            ProcessDash(delta);
        else if (_state == State.ATTACK)
            ProcessAttack(delta);
        else if (_state == State.SPECIAL)
            ProcessSpecial(delta);
        else if (_state == State.SURGE)
            ProcessSurge(delta);
        else if (_state == State.SLAM)
            ProcessSlam(delta);
        else if (_state == State.LAND)
            ProcessLand(delta);
        else if (_state == State.LAUNCH)
            ProcessLaunch(delta);
        else
        {
            _comboWindow = Mathf.Max(_comboWindow - delta, 0.0f);
            ProcessNormal(delta);
        }

        foreach (var p in _passives)
            p.Physics(this, delta);

        TickSurge(delta);
        _parryLeft = Mathf.Max(_parryLeft - delta, 0.0f);
        if (_shakeLeft > 0.0f)
        {
            _shakeLeft = Mathf.Max(_shakeLeft - delta, 0.0f);
            float amp = _shakeAmp * (_shakeLeft / _shakeDur);
            _sprite.Position = _shakeLeft > 0.0f
                ? new Vector2((float)GD.RandRange(-amp, amp), (float)GD.RandRange(-amp, amp))
                : Vector2.Zero;
        }

        if (_hurtbox != null)
            _hurtbox.Monitorable = !_dead && _state != State.SPAWN && _state != State.LAUNCH
                && !(_state == State.DASH && _dashLeft > 0.0f)
                && !(_surgeInvuln && _surgeLeft > 0.0f);

        MoveAndSlide();
        UpdateAnimation(delta);
    }

    private void ProcessStun(float delta)
    {
        _stunLeft -= delta;
        if (!IsOnFloor())
            AddVelY(_gravity * delta);
        SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * 0.5f * delta));
        if (_state != State.HURT)
            _state = State.IDLE;
    }

    private void ProcessDash(float delta)
    {
        if (_launchCdLeft <= 0.0f)
        {
            var orb = OrbInPullRange();
            if (orb != null)
            {
                BeginLaunch(orb);
                return;
            }
        }
        _dashAnimLeft -= delta;
        float input = Input.GetAxis("move_left", "move_right");
        bool holdingDashDir = input != 0.0f && Mathf.Sign(input) == _facing;

        if (Input.IsActionJustPressed("attack"))
            _bufferedAttack = true;
        if (_bufferedAttack && _dashLeft <= 0.0f && (IsOnFloor() || AirAttackOk()))
        {
            _bufferedAttack = false;
            AdvanceCombo();
            if (_state != State.DASH)
                return;
        }

        if (_dashCustom)
        {
            _dashLeft = Mathf.Max(_dashLeft - delta, 0.0f);
            float target = holdingDashDir ? RunSpeed() * _facing : 0.0f;
            SetVelX(Mathf.MoveToward(Velocity.X, target, (_dashSpeed / _dashTime) * delta));
        }
        else if (_dashLeft > 0.0f)
        {
            _dashLeft -= delta;
            SetVelX(_dashSpeed * _facing);
        }
        else
        {
            float target = holdingDashDir ? RunSpeed() * _facing : 0.0f;
            float recovery = Mathf.Max(_dashAnimTime - _dashTime, 0.001f);
            SetVelX(Mathf.MoveToward(Velocity.X, target, (_dashSpeed / recovery) * delta));
        }
        if (IsOnFloor())
            SetVelY(0.0f);
        else
            AddVelY(_gravity * _dashGravityScale * delta);
        if (_dashAnimLeft <= 0.0f)
            Enter(holdingDashDir && IsOnFloor() ? State.RUN : State.IDLE);
    }

    private void ProcessNormal(float delta)
    {
        float input = Input.GetAxis("move_left", "move_right");

        if (!IsOnFloor())
        {
            float gScale = Velocity.Y > 0.0f ? _fallGravityScale : 1.0f;
            AddVelY(_gravity * gScale * delta);
        }

        if (input != 0.0f)
        {
            _facing = input > 0.0f ? 1 : -1;
            SetVelX(Mathf.MoveToward(Velocity.X, input * RunSpeed(), _acceleration * delta));
        }
        else
        {
            SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * delta));
        }

        if (Input.IsActionJustPressed("special"))
        {
            if (IsOnFloor() && _currentSpecial != null)
            {
                StartSpecial();
                return;
            }
            if (!IsOnFloor() && HasSlam() && SlamHasClearance())
            {
                Enter(State.SLAM);
                return;
            }
        }
        if (Input.IsActionJustPressed("attack") && (IsOnFloor() || AirAttackOk()))
        {
            AdvanceCombo();
            return;
        }
        if (Input.IsActionJustPressed("dash"))
        {
            if (_launchCdLeft <= 0.0f)
            {
                var orb = OrbInPullRange();
                if (orb != null)
                {
                    BeginLaunch(orb);
                    return;
                }
            }
            if (_dashCd <= 0.0f)
            {
                Enter(State.DASH);
                return;
            }
        }
        if (Input.IsActionJustPressed("drop") && IsOnFloor())
            DropThroughPlatform();
        if (Input.IsActionJustPressed("jump"))
        {
            if (IsOnFloor())
            {
                SetVelY(AppliedJumpVelocity(true));
                _jumpLaunch = true;
                _sfx.play("jump");
                foreach (var p in _passives)
                    p.OnGroundJump(this);
            }
            else if (_airJumpsUsed < _maxAirJumps)
            {
                AirJump();
            }
        }

        if (!IsOnFloor())
            SetAirborneState();
        else if (_justLanded && HasLand())
            Enter(State.LAND);
        else if (input != 0.0f && Mathf.Abs(Velocity.X) > 5.0f)
            _state = State.RUN;
        else
            _state = State.IDLE;
    }

    private void SetAirborneState()
    {
        if (Velocity.Y >= _landMinFallSpeed && HasLand() && NearGround())
        {
            Enter(State.LAND);
            return;
        }
        if (_state == State.JUMP || _state == State.FALL)
            return;
        _state = _jumpLaunch ? State.JUMP : AirborneDefault();
    }

    private State AirborneDefault() => HasFall() ? State.FALL : State.JUMP;

    // --- launch orbs ---
    private Node2D OrbInPullRange()
    {
        var body = GlobalPosition + LaunchBody;
        Node2D best = null;
        float bestD = LaunchPullRange * LaunchPullRange;
        foreach (Node o in GetTree().GetNodesInGroup("orbs"))
        {
            if (o is not Node2D o2)
                continue;
            float d = body.DistanceSquaredTo(o2.GlobalPosition);
            if (d < bestD)
            {
                bestD = d;
                best = o2;
            }
        }
        return best;
    }

    private void UpdateOrbProximity()
    {
        Node2D near = null;
        if (!_dead && _state != State.SPAWN && _state != State.LAUNCH)
            near = OrbInPullRange();
        if (near == _nearOrb)
            return;
        if (_nearOrb != null && IsInstanceValid(_nearOrb) && _nearOrb.HasMethod("set_near"))
            _nearOrb.Call("set_near", false);
        if (near != null && near.HasMethod("set_near"))
            near.Call("set_near", true);
        _nearOrb = near;
    }

    private void BeginLaunch(Node2D orb)
    {
        _launchOrb = orb;
        _launchFrom = GlobalPosition;
        _launchT = 0.0f;
        Variant upV = orb.Get("launch_up");
        Variant fwdV = orb.Get("launch_forward");
        float up = upV.VariantType != Variant.Type.Nil ? upV.As<float>() : 950.0f;
        float fwd = fwdV.VariantType != Variant.Type.Nil ? fwdV.As<float>() : 650.0f;
        _launchVel = new Vector2(_facing * fwd, -up);
        Velocity = Vector2.Zero;
        Enter(State.LAUNCH);
        if (orb.HasMethod("play_use"))
            orb.Call("play_use");
    }

    private void ProcessLaunch(float delta)
    {
        if (!IsInstanceValid(_launchOrb))
        {
            _launchOrb = null;
            Enter(AirborneDefault());
            return;
        }
        _launchT += delta;
        float t = Mathf.Clamp(_launchT / LaunchMagnetTime, 0.0f, 1.0f);
        Vector2 target = _launchOrb.GlobalPosition - LaunchBody;
        GlobalPosition = _launchFrom.Lerp(target, Ease(t, 0.35f));
        UpdateAnimation(delta);
        if (t >= 1.0f)
        {
            _launchOrb = null;
            _launchCdLeft = LaunchCd;
            Velocity = _launchVel;
            if (Mathf.Abs(Velocity.X) > 5.0f)
                _facing = Velocity.X > 0.0f ? 1 : -1;
            _dashLeft = Mathf.Max(_dashLeft, 0.12f);
            _jumpLaunch = true;
            Enter(AirborneDefault());
        }
    }

    private const float DropThroughTime = 0.3f;

    private bool DropThroughPlatform()
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collider = GetSlideCollision(i).GetCollider();
            if (collider is Node n && n.IsInGroup("oneway_platform"))
            {
                AddCollisionExceptionWith(n);
                SetVelY(Mathf.Max(Velocity.Y, 60.0f));
                var body = n;
                GetTree().CreateTimer(DropThroughTime).Timeout += () =>
                {
                    if (IsInstanceValid(body))
                        RemoveCollisionExceptionWith(body);
                };
                return true;
            }
        }
        return false;
    }

    private void AirJump()
    {
        SetVelY(AppliedJumpVelocity(false));
        _airJumpsUsed += 1;
        _sfx.play("jump");
        _apexY = GlobalPosition.Y;
        _fallPeak = 0.0f;
        _jumpLaunch = true;
        Enter(State.JUMP);
        _sprite.Play("jump");
        _sprite.SetFrameAndProgress(0, 0.0f);
        if (_particles != null)
        {
            float lean = Mathf.Clamp(Velocity.X / Mathf.Max(_runSpeedV, 1.0f), -1.0f, 1.0f);
            _particles.fire_effect("double_jump", lean * DoubleJumpLean);
        }
        foreach (var p in _passives)
            p.OnAirJump(this);
    }

    private void ProcessLand(float delta)
    {
        if (!IsOnFloor())
        {
            if (Velocity.Y <= 0.0f || !NearGround())
            {
                Enter(AirborneDefault());
                return;
            }
            AddVelY(_gravity * _fallGravityScale * delta);
            SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * delta));
            if (Input.IsActionJustPressed("special") && HasSlam() && SlamHasClearance())
            {
                Enter(State.SLAM);
                return;
            }
            if (Input.IsActionJustPressed("attack") && AirAttackOk())
            {
                AdvanceCombo();
                return;
            }
            if (Input.IsActionJustPressed("dash"))
            {
                if (_launchCdLeft <= 0.0f)
                {
                    var orb = OrbInPullRange();
                    if (orb != null)
                    {
                        BeginLaunch(orb);
                        return;
                    }
                }
                if (_dashCd <= 0.0f)
                {
                    Enter(State.DASH);
                    return;
                }
            }
            if (Input.IsActionJustPressed("jump") && _airJumpsUsed < _maxAirJumps)
                AirJump();
            return;
        }

        if (Input.IsActionJustPressed("special") && _currentSpecial != null)
        {
            StartSpecial();
            return;
        }
        if (Input.IsActionJustPressed("attack"))
        {
            AdvanceCombo();
            return;
        }
        if (Input.IsActionJustPressed("dash") && _dashCd <= 0.0f)
        {
            Enter(State.DASH);
            return;
        }
        if (Input.IsActionJustPressed("jump"))
        {
            SetVelY(AppliedJumpVelocity(true));
            _jumpLaunch = true;
            _state = State.JUMP;
            return;
        }

        float input = Input.GetAxis("move_left", "move_right");
        if (input != 0.0f)
        {
            _facing = input > 0.0f ? 1 : -1;
            SetVelX(Mathf.MoveToward(Velocity.X, input * RunSpeed(), _acceleration * delta));
            _state = State.RUN;
            return;
        }
        SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * delta));
    }

    private bool HasLand() => _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation("land");
    private bool HasFall() => _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation("fall");

    private bool NearGround() => NearGround(_landPredictDistance);

    private bool NearGround(float dist)
    {
        if (dist <= 0.0f)
            return false;
        var space = GetWorld2D().DirectSpaceState;
        if (space == null)
            return false;
        var q = PhysicsRayQueryParameters2D.Create(
            GlobalPosition, GlobalPosition + new Vector2(0.0f, dist), CollisionMask);
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        return space.IntersectRay(q).Count > 0;
    }

    private void ProcessAttack(float delta)
    {
        bool dashing = _activeHit.Lunge.HasValue && _recoveryLeft > 0.0f;
        if (dashing)
        {
            SetVelY(0.0f);
        }
        else
        {
            SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * delta));
            if (!IsOnFloor())
                AddVelY(_gravity * delta);
        }

        if (Input.IsActionJustPressed("special"))
            _bufferedSpecial = true;

        if (_flurry)
        {
            if (_bufferedSpecial)
            {
                _flurry = false;
                StartSpecial();
            }
            else if (!Input.IsActionPressed("attack"))
            {
                _flurry = false;
                NotifyAttackAnimEnd();
                Enter(State.IDLE);
            }
            return;
        }

        if (_comboPlaying)
        {
            if (_sprite.Frame >= _segEnd)
            {
                _sprite.SetFrameAndProgress(_segEnd, 0.0f);
                _sprite.Pause();
                _comboPlaying = false;
                _recoveryLeft = Mathf.Max(attack_recovery, _activeHit.Hold ?? 0.0f);
                _comboWindow = combo_reset_time;
                if (_bufferedSpecial)
                    StartSpecial();
            }
            return;
        }

        if (_bufferedSpecial)
        {
            StartSpecial();
            return;
        }
        if (Input.IsActionJustPressed("attack"))
        {
            AdvanceCombo();
            return;
        }
        _comboWindow = Mathf.Max(_comboWindow - delta, 0.0f);
        _recoveryLeft -= delta;
        if (_recoveryLeft <= 0.0f)
        {
            if (_activeHit.Lunge.HasValue)
                SetVelX(0.0f);
            NotifyAttackAnimEnd();
            Enter(State.IDLE);
        }
    }

    private void StartSpecial()
    {
        if (_specialCd > 0.0f)
            return;
        _specialCd = Mathf.Max(SpecialCooldown, _currentSpecial != null ? CooldownOf(_currentSpecial) : 0.0f);
        bool isShield = _currentSpecial != null && HasTag(_currentSpecial, "shield");
        foreach (var p in _passives)
            p.OnSpecialCast(this, _currentSpecial);
        if (isShield)
            _parryLeft = parry_window;
        _comboStep = 0;
        _comboWindow = 0.0f;
        _comboPlaying = false;
        _bufferedSpecial = false;
        _activeHit = ResolveTuning(_currentSpecial, 0);
        _activeHit.FromSpecial = true;
        Enter(State.SPECIAL);
        if (_sprite != null && _currentSpecial != null && HasAnim(Anim(_currentSpecial)))
        {
            _sprite.Play(Anim(_currentSpecial));
            _sprite.SetFrameAndProgress(0, 0.0f);
        }
    }

    private void ProcessSpecial(float delta)
    {
        // A special that carries Lunge (e.g. Zahluq) dashes through: hold vertical and let the lunge impulse
        // ride instead of friction-damping it (mirrors the dash branch in ProcessAttack). set_armor is already
        // honoured globally, so super-armor works for specials without extra handling here.
        if (_activeHit.Lunge.HasValue)
        {
            SetVelY(0.0f);
        }
        else
        {
            SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * delta));
            if (!IsOnFloor())
                AddVelY(_gravity * delta);
        }
        if (_currentSpecial != null && HasTag(_currentSpecial, "held"))
        {
            int last = _sprite.SpriteFrames.GetFrameCount(Anim(_currentSpecial)) - 1;
            if (_sprite.Frame >= last)
            {
                if (Input.IsActionPressed("special"))
                {
                    if (_sprite.IsPlaying())
                        _sprite.Pause();
                }
                else
                {
                    _activeHit = new SegmentData();
                    Enter(State.IDLE);
                }
            }
        }
    }

    private void ProcessSurge(float delta)
    {
        SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * delta));
        if (!IsOnFloor())
            AddVelY(_gravity * delta);
        if (_surgeChannel)
        {
            if (!_surgeAsleep)
            {
                if (_sprite.Frame >= _surgeSleepFrame)
                {
                    _surgeAsleep = true;
                    _sprite.SetFrameAndProgress(_surgeSleepFrame, 0.0f);
                    _sprite.Pause();
                    _surgeLeft = _currentSurge.Surge.duration;
                }
            }
            else
            {
                health = Mathf.Min(health + _surgeHealRate * delta, _surgeHealTarget);
                _surgeLeft -= delta;
                if (_surgeLeft <= 0.0f)
                {
                    EndSurge();
                    Enter(State.IDLE);
                }
            }
        }
    }

    private void ProcessSlam(float delta)
    {
        SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * delta));
        if (_slamImpacting)
        {
            SetVelY(IsOnFloor() ? 0.0f : Mathf.Max(Velocity.Y, _slamSpeed));
            return;
        }
        if (IsOnFloor() || NearGround(_slamImpactDistance))
        {
            SlamRelease();
            return;
        }
        SetVelY(Mathf.Max(Velocity.Y, _slamSpeed));
        int hold = Mathf.Max(0, _slamHoldFrame - SheetStart("slam"));
        if (_sprite.Frame >= hold)
        {
            _sprite.SetFrameAndProgress(hold, 0.0f);
            _sprite.SpeedScale = 0.0f;
            _sprite.Visible = false;
        }
    }

    private void SlamRelease()
    {
        _slamImpacting = true;
        _sprite.Visible = true;
        _sprite.SpeedScale = 1.0f;
        _slamDownSfx?.Stop();
        _sfx.play("slam");
        float drop = GlobalPosition.Y - _slamStartY;
        float t = Mathf.Clamp((drop - _slamMinDrop) / Mathf.Max(_slamMaxDrop - _slamMinDrop, 1.0f), 0.0f, 1.0f);
        _activeHit = new SegmentData { DamageScale = Mathf.Lerp(1.0f, _slamMaxDamageMult, t) * slam_damage_mult };
        foreach (var p in _passives)
            p.OnSlamLand(this, drop, Mathf.Max(Velocity.Y, _slamSpeed));
    }

    private int SheetStart(StringName anim)
    {
        var sf = _sprite.SpriteFrames;
        if (sf != null && sf.HasMeta("sheet_start"))
            return sf.GetMeta("sheet_start").As<GDict>()[anim.ToString()].As<int>();
        return 0;
    }

    private bool HasSlam() => _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation("slam");

    private bool SlamHasClearance()
    {
        if (_slamMinClearance <= 0.0f)
            return true;
        var space = GetWorld2D().DirectSpaceState;
        if (space == null)
            return true;
        var q = PhysicsRayQueryParameters2D.Create(
            GlobalPosition, GlobalPosition + new Vector2(0.0f, _slamMinClearance), CollisionMask);
        q.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        return space.IntersectRay(q).Count == 0;
    }

    private void AdvanceCombo()
    {
        if (_currentAttack != null && IsFlurry(_currentAttack))
        {
            if (!_flurry)
                StartFlurry();
            return;
        }
        if (_currentAttack != null && CooldownOf(_currentAttack) > 0.0f && _attackCd > 0.0f)
            return;

        var hits = AttackHits();
        if (hits.Count == 0)
            return;
        _bufferedSpecial = false;

        if (_comboWindow <= 0.0f || _comboStep >= hits.Count)
            _comboStep = 0;
        int segStart = _comboStep == 0 ? 0 : hits[_comboStep - 1].As<int>() + 1;
        _segEnd = hits[_comboStep].As<int>();
        _comboStep += 1;
        _activeHit = ResolveTuning(_currentAttack, _comboStep - 1);

        _comboWindow = combo_reset_time;
        _comboPlaying = true;
        Enter(State.ATTACK);
        _sprite.SpeedScale = 1.0f;
        _sprite.Play(Anim(_currentAttack));
        _sprite.SetFrameAndProgress(segStart, 0.0f);
        if (CooldownOf(_currentAttack) > 0.0f)
            _attackCd = CooldownOf(_currentAttack);
    }

    private void StartFlurry()
    {
        _bufferedSpecial = false;
        _flurry = true;
        _activeHit = ResolveTuning(_currentAttack, 0);
        Enter(State.ATTACK);
        _sprite.SpeedScale = 1.0f;
        _sprite.Play(Anim(_currentAttack));
    }

    private void UpdateCooldownBar()
    {
        if (_cooldownBar == null)
            return;
        float cd = 0.0f, left = 0.0f;
        if (_currentSpecial != null && CooldownOf(_currentSpecial) > 0.0f && _specialCd > 0.0f)
        {
            cd = CooldownOf(_currentSpecial);
            left = _specialCd;
        }
        else if (_currentAttack != null && CooldownOf(_currentAttack) > 0.0f && _attackCd > 0.0f)
        {
            cd = CooldownOf(_currentAttack);
            left = _attackCd;
        }
        if (cd <= 0.0f || left <= 0.0f)
        {
            if (_cooldownBar.Visible)
                _cooldownBar.Visible = false;
            return;
        }
        _cooldownBar.Visible = true;
        _cooldownBar.SetRatio(1.0f - left / cd);
    }

    private GArr AttackHits()
    {
        var hits = AnimMeta.HitFrames(_sprite.SpriteFrames, Anim(_currentAttack));
        if (hits.Count > 0)
            return hits;
        var all = new GArr();
        for (int i = 0; i < _sprite.SpriteFrames.GetFrameCount(Anim(_currentAttack)); i++)
            all.Add(i);
        return all;
    }

    private void Enter(State state)
    {
        _state = state;
        _sprite.SpeedScale = 1.0f;
        _sprite.Visible = true;
        switch (state)
        {
            case State.DASH:
                _dashLeft = _dashTime;
                _dashAnimLeft = Mathf.Max(_dashAnimTime, _dashTime);
                _dashCd = _dashCooldown;
                _bufferedAttack = false;
                _sfx.play("dash");
                foreach (var p in _passives)
                    p.OnDash(this);
                if (_dashEffect != "")
                {
                    _activeHit = new SegmentData();
                    FireEffect(_dashEffect);
                }
                var frames = _sprite.SpriteFrames;
                float fps = (float)frames.GetAnimationSpeed("dash");
                if (fps > 0.0f)
                {
                    float animTime = frames.GetFrameCount("dash") / fps;
                    _sprite.SpeedScale = animTime / Mathf.Max(_dashAnimTime, _dashTime);
                }
                _dashCustom = _blinkDash;
                if (_dashCustom)
                    DoBlink();
                break;
            case State.ATTACK:
                SetVelX(0.0f);
                break;
            case State.HURT:
                if (HasAnim("hurt"))
                {
                    _sprite.Play("hurt");
                    _sprite.SetFrameAndProgress(0, 0.0f);
                }
                else
                {
                    _state = State.IDLE;
                }
                break;
            case State.SURGE:
                SetVelX(0.0f);
                if (_currentSurge != null && HasAnim(Anim(_currentSurge)))
                {
                    _sprite.Play(Anim(_currentSurge));
                    _sprite.SetFrameAndProgress(0, 0.0f);
                }
                else
                {
                    _state = State.IDLE;
                }
                break;
            case State.DEATH:
                SetVelX(0.0f);
                _deathFrozen = true;
                _sprite.Play("death");
                _sprite.SetFrameAndProgress(0, 0.0f);
                _sprite.Pause();
                break;
            case State.SPAWN:
                SetVelX(0.0f);
                break;
            case State.SLAM:
                Velocity = new Vector2(0.0f, _slamSpeed);
                _slamImpacting = false;
                _slamStartY = GlobalPosition.Y;
                _slamDownSfx?.Play();
                foreach (var p in _passives)
                    p.OnSlamTrigger(this);
                break;
            case State.LAUNCH:
                _sprite.Play("dash");
                break;
        }
    }

    private StringName AnimationFor(State state) => state switch
    {
        State.RUN => "run",
        State.JUMP => "jump",
        State.FALL => "fall",
        State.DASH => "dash",
        State.ATTACK => Anim(_currentAttack),
        State.SPECIAL => Anim(_currentSpecial),
        State.LAND => "land",
        State.SLAM => "slam",
        State.DEATH => "death",
        State.SPAWN => "spawn",
        State.HURT => "hurt",
        State.SURGE => _currentSurge != null ? Anim(_currentSurge) : "idle",
        State.LAUNCH => "dash",
        _ => "idle",
    };

    private void UpdateAnimation(float delta)
    {
        _sprite.FlipH = _facing < 0;
        if (_runSfx != null)
        {
            bool running = _state == State.RUN;
            if (running != _runSfx.Playing)
            {
                if (running)
                    _runSfx.Play();
                else
                    _runSfx.Stop();
            }
        }
        var next = AnimationFor(_state);
        if (_sprite.Animation != next)
        {
            _sprite.Play(next);
            if (next == "jump" && !_jumpLaunch)
            {
                int jn = _sprite.SpriteFrames.GetFrameCount("jump");
                if (jn > 0)
                    _sprite.SetFrameAndProgress(jn - 1, 0.0f);
            }
        }
        if (next == "jump")
            _jumpLaunch = false;

        switch (_state)
        {
            case State.RUN:
                float speedRatio = Mathf.Abs(Velocity.X) / Mathf.Max(_runSpeedV, 1.0f);
                _sprite.SpeedScale = Mathf.Clamp(speedRatio * _runAnimSpeed, 0.4f, 3.0f);
                if (_runSfx != null)
                    _runSfx.PitchScale = Mathf.Clamp(speedRatio, 0.6f, 3.0f);
                break;
            case State.IDLE:
            case State.JUMP:
            case State.FALL:
            case State.LAND:
                _sprite.SpeedScale = 1.0f;
                break;
        }
    }

    private void OnAnimationLooped()
    {
        int start = LoopMeta("loop_from");
        if (start > 0)
            _sprite.SetFrameAndProgress(start, 0.0f);
    }

    private int LoopMeta(StringName key) =>
        AnimMeta.LoopBound(_sprite.SpriteFrames, _sprite.Animation, key.ToString());

    private void OnAnimationFinished()
    {
        if (_state == State.DEATH)
        {
            _sprite.Visible = false;
            _deathFinished = true;
            return;
        }
        if (_state == State.SPAWN)
        {
            Enter(State.IDLE);
            return;
        }
        if (_state == State.JUMP && !IsOnFloor() && HasFall())
        {
            Enter(State.FALL);
            return;
        }
        if (_state == State.LAND && !IsOnFloor())
        {
            Enter(AirborneDefault());
            return;
        }
        if (_state == State.DASH || _state == State.SPECIAL || _state == State.LAND || _state == State.SLAM)
        {
            _activeHit = new SegmentData();
            Enter(State.IDLE);
        }
        if (_state == State.SURGE)
            Enter(!IsOnFloor() ? AirborneDefault() : State.IDLE);
    }

    // =====================================================================================================
    // Small helpers
    // =====================================================================================================
    private void SetVelX(float x) { var v = Velocity; v.X = x; Velocity = v; }
    private void SetVelY(float y) { var v = Velocity; v.Y = y; Velocity = v; }
    private void AddVelY(float dy) { var v = Velocity; v.Y += dy; Velocity = v; }

    /// <summary>Replicates GDScript's <c>ease(x, curve)</c> for 0 &lt; curve &lt; 1 (ease-out), used by the launch magnet.</summary>
    private static float Ease(float x, float curve)
    {
        x = Mathf.Clamp(x, 0.0f, 1.0f);
        if (curve > 0.0f)
            return curve < 1.0f ? 1.0f - Mathf.Pow(1.0f - x, 1.0f / curve) : Mathf.Pow(x, curve);
        return x;
    }
}
