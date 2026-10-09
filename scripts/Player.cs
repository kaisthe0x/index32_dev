using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// A character-agnostic player. Every character shares the same animation set + normalised sprite canvas, so
/// switching is just swapping the SpriteFrames resource. C# port of <c>scripts/player.gd</c> (Phase 4b of the
/// migration) — the state machine, combat seam, surges, launch orbs, and the passive/buff dispatch.
///
/// <para>What a strike asks of its wielder goes through <see cref="IStrikeWielder"/>.
/// Config is fully typed C#: the equipped move is an <see cref="Action"/> record, its tuning a <see cref="SegmentData"/>.</para>
/// </summary>
[Tool]
[GlobalClass]
public partial class Player : Combatant, IStrikeWielder
{
    // --- signals (the HUD connects by these exact names) ---
    [Signal] public delegate void HealthChangedEventHandler(double current, double maximum);
    [Signal] public delegate void RuhChangedEventHandler(double current, double maximum);

    // --- path templates (mirror CharacterConfig; hardcoded so C# needn't read GDScript consts) ---
    private const string FramesPathTmpl = "res://resources/characters/{0}.tres";

    // --- bridged GDScript statics/singletons (cached in _Ready) ---
    private Sfx _sfx = null!;

    // =====================================================================================================
    // Character
    // =====================================================================================================
    private string _character = "khalid";

    [Export(PropertyHint.Enum, "khalid")]
    public string Character
    {
        get => _character;
        set { _character = value; ApplyCharacter(); }
    }

    // =====================================================================================================
    // Health (SLOT-based — measured in half-blocks; see BaseMaxHealth / TakeDamage)
    // =====================================================================================================
    private float _maxHealth = 6.0f;

    [Export]
    public float MaxHealth
    {
        get => _maxHealth;
        set { _maxHealth = Mathf.Max(value, 1.0f); Health = Mathf.Min(Health, _maxHealth); }
    }

    private float _health = 6.0f;

    public float Health
    {
        get => _health;
        set
        {
            float clamped = Mathf.Clamp(value, 0.0f, _maxHealth);
            if (Mathf.IsEqualApprox(clamped, _health))
                return;
            _health = clamped;
            EmitSignal(SignalName.HealthChanged, _health, _maxHealth);
        }
    }

    // =====================================================================================================
    // Ruh (surge meter)
    // =====================================================================================================
    public const float RuhPerBlock = 100.0f;  // one HUD "block" = one charge (the default surge cost)
    private const float RuhPerHit = 20.0f;     // Ruh gained per HIT landed (5 hits = 1 charge)
    private const float MaxRuhCap = 500.0f;    // hard ceiling: 5 charges

    private float _ruhCap = 300.0f;

    [Export]
    public float RuhCap
    {
        get => _ruhCap;
        set { _ruhCap = Mathf.Clamp(value, 0.0f, MaxRuhCap); Ruh = Mathf.Min(Ruh, _ruhCap); }
    }

    private float _ruh = 0.0f;

    public float Ruh
    {
        get => _ruh;
        set
        {
            float clamped = Mathf.Clamp(value, 0.0f, _ruhCap);
            if (Mathf.IsEqualApprox(clamped, _ruh))
                return;
            _ruh = clamped;
            EmitSignal(SignalName.RuhChanged, _ruh, _ruhCap);
        }
    }

    // --- run-reward buffs (per-run, reset by BeginRun); shots, perks and box buffs change these. ---
    private const float BaseRuhCap = 300.0f;
    // Slot health: HP is measured in HALF-BLOCKS. 3 blocks = 6 half-blocks, and EVERY hit costs one half-block
    // regardless of damage (so 6 hits kill). BeginRun fills to BaseMaxHealth.
    private const int HealthBlocks = 3;
    private const float BaseMaxHealth = HealthBlocks * 2.0f; // 3 blocks × 2 half-blocks = 6
    private const float HitCost = 1.0f;                      // one hit = half a block
    private const float SurgeHealHalfBlocks = 2.0f;          // the Nem surge restores one block
    private const float ShieldGraceTime = 0.4f;              // i-frames after the Shield perk eats a hit
    private const float HealthWarnHalf = 0.5f;               // "health_half" cue at 1.5 blocks left
    private const float HealthWarnLow = 0.34f;               // "health_low" cue at ~1 block left

    public float DamageMult = 1.0f;
    public float RunMult = 1.0f;
    public int AirJumpBonus = 0;
    public int DashBonus = 0;                // extra dash CHARGES on top of the one you always have
    public float SlamDamageMult = 1.0f;    // Slam Damage shot
    public float AttackReachMult = 1.0f;   // Long Arm
    public float SpecialInvulnBonus = 0.0f; // Fortitude: extends any surge window
    public float JumpVelocityBonus = 1.0f;  // Jump Height shot: multiplies applied jump velocity (all jumps)
    public int MagnetTargetBonus = 0;        // Wider Pull perk: extra Come Closer magnet targets
    public float FigChanceBonus = 0.0f;      // Fig Chance perk: added to every kill's fada_fig drop chance
    public float FigMagnetRange = 0.0f;      // Magnet perk: loose FadaFigs within this (px) fly to you (0 = off)
    public int HitShields = 0;                // Shield perk: hits blocked outright before any damage

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

    // Read-only views of the movement runtime values.
    public float BaseRunSpeed => _runSpeedV;
    public float JumpVelocity => _jumpVelocity;
    public float DashSpeed => _dashSpeed;
    public int MaxAirJumps => _maxAirJumps;
    public float Gravity => _gravity;
    public float SlamSpeed => _slamSpeed;

    [Export] public float AttackRecovery = 0.12f;
    [Export] public float ComboResetTime = 0.45f;

    private const float DoubleJumpLean = 0.6f;

    public enum State { Idle, Run, Jump, Dash, Attack, Special, Land, Slam, Fall, Death, Spawn, Hurt, Surge, Launch }

    // --- launch orbs (magnet traversal) ---
    private const float LaunchPullRange = 96.0f;
    private static readonly Vector2 LaunchBody = new(0.0f, -20.0f);
    private const float LaunchMagnetTime = 0.08f;
    private const float LaunchCd = 0.45f;

    private LaunchOrb? _launchOrb;
    private Vector2 _launchFrom = Vector2.Zero;
    private float _launchT = 0.0f;
    private Vector2 _launchVel = Vector2.Zero;
    private float _launchCdLeft = 0.0f;
    private LaunchOrb? _nearOrb;

    private State _state = State.Idle;
    private int _facing = 1;
    // The equipped actions. Null until a character with that slot is applied -- in the editor (this is a [Tool]
    // script) and for a character whose sprite frames are missing, ApplyCharacter stops before the loadout.
    private Action? _currentAttack;
    private Action? _currentSpecial;
    private Action? _currentSurge;
    private readonly System.Collections.Generic.Dictionary<LoadoutCategory, string> _loadout = new();
    private SegmentData _activeHit = new();
    private float _dashLeft = 0.0f;
    private float _dashAnimLeft = 0.0f;
    private float _dashCd = 0.0f;         // time until the next dash charge refills (runs while below max)
    private int _dashCharges = 1;
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
    private float _gustLeft = 0.0f; // a gust is carrying him (Combat.GustCarryTime): weak steering, no air brake
    private float _armorLeft = 0.0f;
    private float _holdLeft = 0.0f;
    private BlastStrike? _channel;
    private readonly List<Passive> _passives = new();

    // --- surge window ---
    private float _surgeLeft = 0.0f;
    private float _iframesLeft = 0.0f;  // generic invulnerability window (GrantInvuln) — the immunity buffs
    private bool _surgeInvuln = false;
    private float _surgeDmgMult = 1.0f;
    private float _surgeSpeedMult = 1.0f;
    private bool _surgeChannel = false;
    private bool _surgeAsleep = false;
    private float _surgeHealTarget = 0.0f;
    private float _surgeHealRate = 0.0f;
    private int _surgeSleepFrame = 0;
    private float _surgeSleepTime = 0.0f;   // how long a channelled surge sleeps once it reaches the sleep frame
    private bool _surgeArmed = false;
    private SurgeSpec? _armedSurge;
    private Node2D? _specialAura;

    // --- shield / parry ---
    private float _parryLeft = 0.0f;
    [Export] public float ParryWindow = 0.25f;
    [Export] public float ShieldReflectMult = 1.0f;
    private float _shakeLeft = 0.0f, _shakeDur = 0.0f, _shakeAmp = 0.0f;
    [Export] public float ShieldShakeAmp = 4.0f;
    [Export] public float ShieldShakeTime = 0.18f;
    [Export] public bool FlinchOnAllDamage = true;

    private float _specialCd = 0.0f;
    private AudioStreamPlayer? _runSfx;
    private AudioStreamPlayer? _slamDownSfx;
    private const float RuhFlashRefractory = 0.2f;
    private float _ruhFlashCd = 0.0f;
    private Tween? _hairTween;
    private ShaderMaterial? _tintMat;
    private bool _bodyIsLut = false;
    private (Color Base, Color AccentA, Color AccentB)? _hairBase;   // the tint shader's own colours (non-LUT body only)
    private static readonly Color HairAbsorbBase = new(2.6f, 1.7f, 0.5f);
    private static readonly Color HairAbsorbA = new(2.3f, 1.0f, 0.35f);
    private static readonly Color HairAbsorbB = new(1.9f, 0.6f, 0.25f);

    private ParticleDirector _particles = null!;
    private Hurtbox _hurtbox = null!;
    private StatusOverlay _status = null!;
    private AnimatedSprite2D _sprite = null!;

    // =====================================================================================================
    // Lifecycle
    // =====================================================================================================
    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        Combat.ApplyFloorHandling(this); // shared slope handling — walk up painted ramps, stay glued going down

        Health = MaxHealth;
        ApplyCharacter();
        if (Engine.IsEditorHint())
            return;
        _sfx = GetNode<Sfx>("/root/Sfx");
        _sprite.AnimationFinished += OnAnimationFinished;
        _sprite.AnimationLooped += OnAnimationLooped;

        _particles = new ParticleDirector();
        AddChild(_particles);
        _particles.Setup(_sprite);
        _particles.SetCharacter(Character);

        BuildCombat();
        _sprite.FrameChanged += OnFrameChanged;

        // Seed listeners that connected before _ready (the setters stay silent on no-change).
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        EmitSignal(SignalName.RuhChanged, Ruh, RuhCap);
    }

    private void ApplyCharacter()
    {
        var sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite == null)
            return;
        string path = string.Format(FramesPathTmpl, Character);
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"No SpriteFrames for character '{Character}' at {path}");
            return;
        }
        sprite.SpriteFrames = GD.Load<SpriteFrames>(path);
        string matPath = $"res://resources/{Character}_tint.tres";
        if (Character == "khalid")
        {
            // Khalid wears the material-aware palette LUT (recolour + glow / hair-flow effects).
            _tintMat = PaletteConfig.MakeMaterial();
            _bodyIsLut = true;
            sprite.Material = _tintMat;
            _hairBase = null;
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
                    ? (br.AsColor(), aa.AsColor(), ab.AsColor())
                    : null;
            }
            else
            {
                _tintMat = null;
                _hairBase = null;
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
        _dashCd = 0.0f;
        _dashCharges = MaxDashCharges;
        if (!Engine.IsEditorHint())
            _state = State.Idle;
        sprite.SpeedScale = 1.0f;
        sprite.Play(AnimationFor(_state));
        SeedPassives();
        _particles?.SetCharacter(Character);
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

    private Action? GetAction(string kind, string id) => Actions.GetAction(Character, kind, id);

    private void ApplyMovement(LoadoutCategory category, string optionId)
    {
        var a = GetAction(category.Kind(), optionId);
        if (a?.Move is not Locomotion m)
            return;
        switch (category)
        {
            case LoadoutCategory.Run:
                _runSpeedV = m.RunSpeed * RunMult;
                _acceleration = m.Acceleration;
                _friction = m.Friction;
                _runAnimSpeed = m.RunAnimSpeed;
                break;
            case LoadoutCategory.Jump:
                _jumpVelocity = m.JumpVelocity;
                _maxAirJumps = m.AirJumps + AirJumpBonus;
                _gravity = m.Gravity;
                _fallGravityScale = m.FallGravityScale;
                _landMinFallSpeed = m.LandMinFallSpeed;
                _landPredictDistance = m.LandPredictDistance;
                break;
            case LoadoutCategory.Dash:
                _dashSpeed = m.DashSpeed;
                _dashTime = m.DashTime;
                _dashCooldown = m.DashCooldown;
                _dashAnimTime = m.DashAnimTime;
                _dashGravityScale = m.DashGravityScale;
                _blinkDash = m.Blink;
                break;
            case LoadoutCategory.Slam:
                _slamSpeed = m.SlamSpeed;
                _slamMinClearance = m.SlamMinClearance;
                _slamHoldFrame = m.SlamHoldFrame;
                _slamImpactDistance = m.SlamImpactDistance;
                _slamMinDrop = m.SlamMinDrop;
                _slamMaxDrop = m.SlamMaxDrop;
                _slamMaxDamageMult = m.SlamMaxDamageMult;
                break;
        }
    }

    public void Equip(LoadoutCategory category, string optionId)
    {
        _loadout[category] = optionId;
        ApplyLoadout();
    }

    public string LoadoutId(LoadoutCategory category)
    {
        return _loadout.TryGetValue(category, out var id) ? id : Loadout.DefaultId(Character, category);
    }

    private void SeedPassives()
    {
        ClearPassives();
        if (Engine.IsEditorHint())
            return;
        var ability = CharacterAbilityFor(Character);
        if (ability != null)
            AddPassive(ability);
    }

    /// <summary>Remove every passive, undoing each one's stat changes (Teardown) while those changes are still applied.</summary>
    private void ClearPassives()
    {
        foreach (var p in _passives)
            p.Teardown(this);
        _passives.Clear();
        RefreshBuffHud();
    }

    /// <summary>A character's intrinsic ability, or null. Khalid ships without one. (Add a case when a character gets a C# CharacterAbility.)</summary>
    private static Passive? CharacterAbilityFor(string character) => null;

    /// <summary>Whether a passive with this id is on him (a box buff he already holds).</summary>
    public bool HasPassive(string id)
    {
        foreach (var p in _passives)
            if (p.Id == id)
                return true;
        return false;
    }

    public void AddPassive(Passive p)
    {
        _passives.Add(p);
        p.Setup(this);
        RefreshBuffHud();
    }

    /// <summary>Remove one held passive, undoing its changes (a Needle Point shot running out). No-op if not held.</summary>
    public void RemovePassive(Passive p)
    {
        if (!_passives.Remove(p))
            return;
        p.Teardown(this);
        RefreshBuffHud();
    }

    /// <summary>The HUD autoload — null while this player is outside the scene tree. The editor applies a character
    /// to this [Tool] script's instance before it is in the tree (a background scene tab, a script reload), and an
    /// absolute-path lookup from there is an engine error.</summary>
    private HUD? Hud => IsInsideTree() ? GetNodeOrNull<HUD>("/root/HUD") : null;

    /// <summary>Push the current buff loadout to the HUD's active-buff list (autoload). Also called when a shot's
    /// rounds-left changed without the list changing.</summary>
    public void RefreshBuffHud() => Hud?.RefreshBuffs(_passives);

    /// <summary>Lira banked this run — the common currency (docs/game-loop.md § Economy). Reset by <see cref="BeginRun"/>.</summary>
    public int Lira { get; private set; } = 0;

    /// <summary>Collect <paramref name="n"/> Lira (a <see cref="MyGame.Lira"/> coin reached the player) — bank it + update the HUD.</summary>
    public void CollectLira(int n)
    {
        Lira += n;
        Hud?.SetLira(Lira);
    }

    /// <summary>FadaFigs banked this run — the rare currency (the mystery box spends it). Reset by <see cref="BeginRun"/>.</summary>
    public int FadaFigs { get; private set; } = 0;

    /// <summary>Collect <paramref name="n"/> fada_fig(s) (a FadaFig touched the player) — bank them + update the HUD.</summary>
    public void CollectFadaFig(int n = 1)
    {
        FadaFigs += n;
        Hud?.SetFadaFigs(FadaFigs);
    }

    /// <summary>Try to spend <paramref name="cost"/> FadaFigs (the mystery box). True + deducts if affordable; else false.</summary>
    public bool SpendFadaFigs(int cost)
    {
        if (cost <= 0 || FadaFigs < cost)
            return false;
        FadaFigs -= cost;
        Hud?.SetFadaFigs(FadaFigs);
        return true;
    }

    public void NotifyHitDealt(float amount, Node target)
    {
        foreach (var p in _passives)
            p.OnHitDealt(this, amount, target);
    }

    /// <summary>A player attack hitbox deactivated having hit nobody (a whiff). Called by <see cref="Hitbox"/> on a
    /// zero-victim deactivation of a player-sourced attack box — dispatches OnMiss (Instant Reset, etc.).</summary>
    public void NotifyMiss()
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

    public int GetState() => (int)_state;
    public bool IsSpawning() => _state == State.Spawn;

    /// <summary>Which way Khalid faces: +1 right, -1 left (RunManager spawns grunts on the other side).</summary>
    public int Facing => _facing;
    public Action? CurrentAttack() => _currentAttack;
    public Action? CurrentSpecial() => _currentSpecial;

    // =====================================================================================================
    // Action helpers (thin typed accessors over the equipped Action)
    // =====================================================================================================
    private static StringName Anim(Action a) => a.Animation;
    private static bool HasTag(Action a, string t) => a.HasTag(t);
    private static float CooldownOf(Action a) => a.Cooldown;
    private static bool IsFlurry(Action a) => a.IsFlurry;

    private bool HasAnim(StringName anim) =>
        _sprite != null && _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim);

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

    private void FireEffect(string anim, float tilt = 0.0f) => _particles?.FireEffect(anim, tilt);

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
    private static readonly string[] HurtCues = { "hurt.1", "hurt.2", "hurt.3" }; // one is picked at random per hit

    public void TakeDamage(float amount)
    {
        // Slot health: every hit costs a flat HALF-BLOCK, regardless of `amount` (so damage-reduction is inert now).
        // `amount` is kept for callers but no longer scales the HP loss, and there's no damage number to show.
        float before = Health;
        Health -= HitCost;
        WarnLowHealth(before, Health);
        _sfx.PlayRandom(HurtCues); // pitch variation comes from SfxCharacters.Pitch
        // Colour flash over the hurt anim, via the palette shader's `flash` uniform (a plain modulate is swallowed).
        FlashSprite(_sprite, Combat.DamageFlash, Combat.DamageFlashTime);
        if (Health <= 0.0f && !_dead)
            Die();
    }

    private void WarnLowHealth(float oldHp, float newHp)
    {
        if (MaxHealth <= 0.0f || newHp >= oldHp)
            return;
        float oldR = oldHp / MaxHealth;
        float newR = newHp / MaxHealth;
        if (oldR > HealthWarnLow && newR <= HealthWarnLow)
            _sfx.Play("health_low");
        else if (oldR > HealthWarnHalf && newR <= HealthWarnHalf)
            _sfx.Play("health_half");
    }

    private void Shake(float amp, float time)
    {
        if (time <= 0.0f)
            return;
        _shakeAmp = amp;
        _shakeDur = time;
        _shakeLeft = time;
    }

    public void Heal(float amount) => Health = Mathf.Min(Health + amount, MaxHealth);

    /// <summary>Kill Khalid outright: he fell out of the arena. Ignores i-frames / Aegis (nothing survives the void).
    /// Unlike a normal death there's no death animation — he keeps his fall animation and keeps dropping, out of
    /// control (<see cref="ProcessFreefall"/>), with its own sound; RunManager ends the run.</summary>
    public void FallToDeath()
    {
        if (_dead)
            return;
        Health = 0.0f;
        Die(fell: true);
    }

    /// <summary>True once the player has died by falling out of the arena (RunManager runs the fall-death flow).</summary>
    public bool FellOut() => _fellOut;

    /// <summary>Grant a generic invulnerability window (the immunity buffs: dash/jump/slam/on-hit). Refreshes to the longer.</summary>
    public void GrantInvuln(float seconds) => _iframesLeft = Mathf.Max(_iframesLeft, seconds);

    /// <summary>Add air jumps (the Extra Jump shot) — bumps the bonus AND the live max. Undo with n &lt; 0.</summary>
    public void AddAirJumps(int n) { AirJumpBonus += n; _maxAirJumps += n; }

    /// <summary>Prime the NEXT ground jump with a height multiplier (Slam Spring) — one-shot, consumed on that jump.</summary>
    public void SetJumpSpring(float mult) => _slamSpringBonus = mult;

    /// <summary>Shave <paramref name="seconds"/> off the special's cooldown (clamped at ready).</summary>
    public void ReduceSpecialCooldown(float seconds) => _specialCd = Mathf.Max(_specialCd - seconds, 0.0f);

    /// <summary>How recharged the special is, 0..1 (1 = ready to cast) — drives the HUD's special bar.</summary>
    public float SpecialReady()
    {
        float cd = _currentSpecial != null ? CooldownOf(_currentSpecial) : 0.0f;
        return cd > 0.0f ? 1.0f - _specialCd / cd : 1.0f;
    }

    /// <summary>How many dashes are banked now vs. the most you can hold (1 + <see cref="DashBonus"/>).</summary>
    private int MaxDashCharges => 1 + DashBonus;

    /// <summary>Add dash charges (the +Dash shot) — raises the max AND hands the new charges over now. Undo with n &lt; 0.</summary>
    public void AddDashCharges(int n)
    {
        DashBonus += n;
        _dashCharges = Mathf.Clamp(_dashCharges + n, 0, MaxDashCharges);
    }

    /// <summary>Scale run speed (the Run Speed shot) — the live speed as well as the multiplier the next equip reads.</summary>
    public void ScaleRunSpeed(float f)
    {
        RunMult *= f;
        _runSpeedV *= f;
    }

    /// <summary>Try to spend <paramref name="cost"/> Lira (the stalls). True + deducts if affordable; else false.</summary>
    public bool SpendLira(int cost)
    {
        if (cost < 0 || Lira < cost)
            return false;
        Lira -= cost;
        Hud?.SetLira(Lira);
        return true;
    }

    /// <summary>Global cooldown fairness: an action whose windup is interrupted by a stagger BEFORE its hit came out
    /// never actually fired, so it shouldn't burn its cooldown. An ATTACK still short of its hit frame
    /// (<see cref="_segEnd"/>) or a SPECIAL short of its strike frame is "uncommitted" → zero the matching cooldown.
    /// Called from <see cref="OnHurt"/> the instant a hurt is about to knock the player into HURT (windup cancelled).</summary>
    private void RefundUncommittedCooldown()
    {
        if (_sprite == null)
            return;
        if (_state == State.Special && _sprite.Frame < SpecialStrikeFrame())
            _specialCd = 0.0f;
    }

    // --- DEBUG: playtest the buff catalog (triggered from RunManager's DEBUG keys; REMOVE before release) -------
    private int _debugBuffIdx = 0;

    /// <summary>DEBUG: grant the next wired catalog buff, cycling through the whole set.</summary>
    public void DebugGrantNextBuff()
    {
        var ids = new List<string>(BuffCatalog.Factories.Keys);
        if (ids.Count == 0)
            return;
        string id = ids[_debugBuffIdx++ % ids.Count];
        var buff = BuffCatalog.Make(id);
        if (buff != null)
        {
            AddPassive(buff);
            GD.Print($"[DEBUG] granted buff: {id}");
        }
    }

    /// <summary>DEBUG: clear all granted buffs (keeps the character ability).</summary>
    public void DebugClearBuffs()
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

    private static readonly Color StunSweepColor = new(1.0f, 0.85f, 0.2f, 0.6f); // the gold tint on a stun-swept enemy

    /// <summary>Stun every enemy within `radius` for `seconds` — the stun sweep shared by the Wara surge and Slam Quake.</summary>
    public void StunNearby(float radius, float seconds)
    {
        foreach (Node e in GetTree().GetNodesInGroup("enemies"))
        {
            if (e is not Enemy enemy || GlobalPosition.DistanceTo(enemy.GlobalPosition) > radius)
                continue;
            enemy.ApplyHit(new Hit
            {
                Stun = seconds,
                Source = this,
                StatusColor = StunSweepColor,
                StatusTime = seconds,
            });
        }
    }

    /// <summary>The jump velocity to apply, folding in the Jump Height shot (all jumps) and, for a GROUND jump, a one-shot Slam Spring (consumed here).</summary>
    private float AppliedJumpVelocity(bool ground)
    {
        float v = _jumpVelocity * JumpVelocityBonus;
        if (ground && !Mathf.IsEqualApprox(_slamSpringBonus, 1.0f))
        {
            v *= _slamSpringBonus;
            _slamSpringBonus = 1.0f;
        }
        return v;
    }

    public bool GainRuhOnHit()
    {
        float before = Ruh;
        Ruh += RuhPerHit;
        return Mathf.FloorToInt(Ruh / RuhPerBlock) > Mathf.FloorToInt(before / RuhPerBlock);
    }

    public void OnRuhAbsorbed(bool completedCharge)
    {
        if (!completedCharge && _ruhFlashCd > 0.0f)
            return;
        _ruhFlashCd = RuhFlashRefractory;
        HairSurge(completedCharge ? 1.0f : 0.6f, completedCharge ? 0.6f : 0.35f);
        _sfx.Play("ruh_absorb", 0.0f, completedCharge ? 1.12f : 1.0f);
    }

    private void HairSurge(float strength, float dur)
    {
        if (_tintMat == null || (!_bodyIsLut && _hairBase == null))
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
        if (_hairBase is not var (baseCol, accentA, accentB))
            return;
        _tintMat.SetShaderParameter("base_red", baseCol.Lerp(HairAbsorbBase, f));
        _tintMat.SetShaderParameter("accent_a", accentA.Lerp(HairAbsorbA, f));
        _tintMat.SetShaderParameter("accent_b", accentB.Lerp(HairAbsorbB, f));
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
        CollisionMask = Combat.GroundMask;

        _hurtbox = new Hurtbox { CollisionLayer = (uint)Combat.Layer.PlayerHurt, CollisionMask = 0 };
        _hurtbox.AddChild(MakeBox(new Vector2(16, 30), new Vector2(0, -15)));
        AddChild(_hurtbox);
        _hurtbox.Hurt += OnHurt;

        _status = new StatusOverlay();
        AddChild(_status);
        _status.Setup(_sprite);

        if (!Engine.IsEditorHint())
        {
            _runSfx = _sfx.MakeLoop("run");
            if (_runSfx != null)
                AddChild(_runSfx);
            _slamDownSfx = _sfx.MakeOneshot("slam_down");
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
        float dmgMult = DamageMult * _surgeDmgMult;
        if (!Mathf.IsEqualApprox(dmgMult, 1.0f) && baseT.Damage.HasValue)
            baseT.Damage *= dmgMult;
        if (!Mathf.IsEqualApprox(AttackReachMult, 1.0f))
        {
            if (baseT.Extents.HasValue)
                baseT.Extents *= AttackReachMult;
            if (baseT.X.HasValue)
                baseT.X *= AttackReachMult;
        }
        foreach (var p in _passives)
            baseT = p.ModifyTuning(this, action, seg, baseT);
        return baseT;
    }

    public SegmentData ActiveHit() => _activeHit;

    private bool IsShielding() =>
        _state == State.Special && _currentSpecial != null && HasTag(_currentSpecial, "shield");

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
                    if (ShieldReflectMult > 0.0f && hit.Source is Enemy reflEnemy && hit.Amount > 0.0f)
                    {
                        var back = new Hit { Amount = hit.Amount * ShieldReflectMult, Knockback = 120.0f, Source = this };
                        reflEnemy.ApplyHit(back);
                    }
                    _sfx.Play("redere_shield_parry");
                    foreach (var p in _passives)
                        p.OnParry(this, hit);
                }
                else
                {
                    _sfx.Play("redere_shield_block");
                }
                Flash(_sprite);
                Shake(ShieldShakeAmp, ShieldShakeTime);
                return;
            }
        }
        if (hit.Gust > 0.0f)
        {
            BlownAway(hit);
            return;
        }
        if (_surgeArmed && hit.Source is Enemy)
        {
            TriggerWara();
            return;
        }
        if (HitShields > 0)
        {
            HitShields -= 1;
            _sfx.Play("redere_shield_block");
            Flash(_sprite);
            GrantInvuln(ShieldGraceTime); // a hit rarely comes alone — don't let the next one land the same instant
            return;
        }
        TakeDamage(hit.Amount);
        if (_dead)
            return;
        foreach (var p in _passives)
            p.OnHurt(this, hit);
        BreakOffForHit();
        if (_armorLeft > 0.0f)
            return;
        float stagger = ApplyKnockback(hit, _facing);
        if (FlinchOnAllDamage || stagger > 0.0f)
        {
            RefundUncommittedCooldown(); // staggered mid-windup: read state BEFORE we leave ATTACK/SPECIAL for HURT
            float flinch = Mathf.Max(stagger, AnimDuration("hurt"));
            if (_state == State.Hurt)
                _stunLeft = Mathf.Max(_stunLeft, flinch);
            else
            {
                _stunLeft = flinch;
                Enter(State.Hurt);
            }
            _bufferedSpecial = false;
        }
        if (hit.StatusColor.A > 0.0f)
            _status.ShowFor(hit.StatusColor, hit.StatusTime);
        if (hit.VictimVfx != null)
            SpawnVictimVfx(hit.VictimVfx, hit.VictimVfxTime);
    }

    /// <summary>What any landed hit interrupts: an orb launch, a channelled strike he's holding (if it allows), and a
    /// channelled surge (Nem's sleep wakes).</summary>
    private void BreakOffForHit()
    {
        if (_state == State.Launch)
        {
            _launchOrb = null;
            _launchCdLeft = LaunchCd;
        }
        if (_channel != null && IsInstanceValid(_channel) && _channel.InterruptOnHurt)
        {
            _channel.Cancel();
            _holdLeft = 0.0f;
            _sprite.Play();
        }
        _channel = null;
        if (_surgeChannel)
        {
            EndSurge();
            if (_state == State.Surge)
                Enter(State.Idle);
        }
    }

    /// <summary>A GUST hit (<see cref="Hit.Gust"/>, Ventilator's wind): no damage, no stagger — it breaks off whatever
    /// he's doing and flings him away from the source. For <see cref="Combat.GustCarryTime"/> his air steering is weak
    /// and only works against the fling (ProcessNormal); an air jump or a dash BREAKS the carry (full control back) —
    /// those are how he recovers.</summary>
    private void BlownAway(Hit hit)
    {
        BreakOffForHit();
        RefundUncommittedCooldown(); // blown out of a wind-up: read state BEFORE we leave ATTACK/SPECIAL
        _stunLeft = 0.0f;
        _holdLeft = 0.0f;
        _bufferedSpecial = false;
        Velocity = GustVelocity(hit, _facing);
        _gustLeft = Combat.GustCarryTime;
        _jumpLaunch = false;
        Enter(AirborneDefault());
    }

    public void ApplyLunge(float impulse) => SetVelX(impulse * _facing);

    public void SetArmor(float duration) => _armorLeft = Mathf.Max(_armorLeft, duration);

    public void SetDashEffect(string effect) => _dashEffect = effect;

    private float RunSpeed() => _runSpeedV * _surgeSpeedMult;

    // =====================================================================================================
    // Surges
    // =====================================================================================================
    private void BeginSurge(Action surge, SurgeSpec s)
    {
        EndSurge();
        _surgeInvuln = s.Invuln;
        _surgeDmgMult = s.DamageMult;
        _surgeSpeedMult = s.SpeedMult;
        _surgeChannel = s.Channel;
        _surgeArmed = s.Trigger == "hit";
        if (_surgeArmed)
        {
            _armedSurge = s;
            _surgeLeft = 0.0f;
        }
        else if (_surgeChannel)
        {
            _surgeAsleep = false;
            _surgeLeft = 0.0f;
            // Slot health: a healing surge (HealFrac > 0, i.e. Nem) restores ONE block over its channel.
            _surgeHealTarget = Mathf.Min(Health + SurgeHealHalfBlocks, MaxHealth);
            _surgeHealRate = (_surgeHealTarget - Health) / Mathf.Max(s.Duration, 0.01f);
            var anim = Anim(surge);
            int fcount = (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim))
                ? _sprite.SpriteFrames.GetFrameCount(anim) : 0;
            _surgeSleepFrame = Mathf.Max(fcount - 2, 0);
            _surgeSleepTime = s.Duration;
        }
        else
        {
            _surgeLeft = s.Duration + SpecialInvulnBonus;
        }
        string aura = s.Aura;
        if (aura != "" && ResourceLoader.Exists(aura))
        {
            var scene = GD.Load<PackedScene>(aura);
            _specialAura = scene?.Instantiate() as Node2D;
            if (_specialAura != null)
            {
                if (_specialAura is OrbitAura orbit)
                    orbit.MoonColor = VfxPalette.Recolor(orbit.MoonColor);
                VfxPalette.RecolorTree(_specialAura);
                AddChild(_specialAura);
            }
        }
    }

    private void TrySurge()
    {
        if (!Input.IsActionJustPressed("surge") || ReadySurge() is not var (surge, spec) || Ruh < spec.Cost)
            return;
        Ruh -= spec.Cost;
        FireSurge(surge, spec);
    }

    /// <summary>The equipped surge and its spec, if one can fire now (alive, none channelling or armed); else null.</summary>
    private (Action Surge, SurgeSpec Spec)? ReadySurge() =>
        !_dead && !_surgeChannel && !_surgeArmed && _currentSurge is { Surge: { } spec } surge ? (surge, spec) : null;

    /// <summary>Fire the equipped surge WITHOUT spending Ruh (the Prepared perk, at round start). No-op if one is
    /// already going.</summary>
    public void SurgeFree()
    {
        if (ReadySurge() is var (surge, spec))
            FireSurge(surge, spec);
    }

    /// <summary>Tell every passive a round began (RunManager.StartRound) — see <see cref="Passive.OnRoundStart"/>.</summary>
    public void NotifyRoundStart()
    {
        foreach (var p in new List<Passive>(_passives))
            p.OnRoundStart(this);
    }

    private void FireSurge(Action surge, SurgeSpec s)
    {
        BeginSurge(surge, s);
        Flash(_sprite);
        _sfx.Play(Anim(surge).ToString());
        if (_state != State.Spawn && HasAnim(Anim(surge)))
            Enter(State.Surge);
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
        StunNearby(s.StunRadius, s.StunTime);
        string burst = s.Burst;
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
        _sfx.Play("surge_wara_trigger");
        Flash(_sprite);
        EndSurge();
    }

    public void HoldAnimation(double duration, BlastStrike effect)
    {
        if (duration <= 0.0 || _sprite == null)
            return;
        _holdLeft = Mathf.Max(_holdLeft, (float)duration);
        _channel = effect;
        _sprite.Pause();
    }

    private void OnFrameChanged()
    {
        if (_state == State.Special)
        {
            if (_sprite.Frame == SpecialStrikeFrame())
                foreach (var p in _passives)
                    p.OnSpecialStrike(this);
            return;
        }
        int loopTo = AnimMeta.LoopTo(_sprite.SpriteFrames, _sprite.Animation);
        if (loopTo >= 0 && _sprite.Frame > loopTo)
            _sprite.SetFrameAndProgress(Mathf.Max(AnimMeta.LoopFrom(_sprite.SpriteFrames, _sprite.Animation), 0), 0.0f);
    }

    private int SpecialStrikeFrame()
    {
        if (_currentSpecial is not { } special)
            return 0;
        var hits = AnimMeta.HitFrames(_sprite.SpriteFrames, Anim(special));
        if (hits.Count > 0)
            return hits[0];
        return _sprite.SpriteFrames.GetFrameCount(Anim(special)) / 2;
    }

    public bool IsDead() => _dead;
    /// <summary>In a channelled surge (Nem's sleep) — the stand-still kamikaze clock pauses for it.</summary>
    public bool IsChannelingSurge() => _surgeChannel;
    public bool DeathComplete() => _dead && _deathFinished;

    public void ReleaseDeath()
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
        _sfx.Play(fell ? "player_fall_death" : "player_death");
        _stunLeft = 0.0f;
        _comboPlaying = false;
        _flurry = false;
        _holdLeft = 0.0f;
        EndSurge();
        if (_channel != null && IsInstanceValid(_channel))
            _channel.Cancel();
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
            Enter(State.Death);
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

    public void Spawn()
    {
        Velocity = Vector2.Zero;
        if (HasAnim("spawn"))
            Enter(State.Spawn);
        else
        {
            if (_hurtbox != null)
                _hurtbox.Monitorable = true;
            Enter(State.Idle);
        }
    }

    public void BeginRun()
    {
        // Buffs FIRST: each Teardown undoes its own change (e.g. -1 air jump, ÷ jump height), so it must run while
        // those changes are still in place — resetting the stats below first made every undo apply twice.
        ClearPassives();
        _dead = false;
        _deathFinished = false;
        _fellOut = false;
        Lira = 0;
        FadaFigs = 0;
        Hud?.SetLira(0);
        Hud?.SetFadaFigs(0);
        EndSurge();
        _shakeLeft = 0.0f;
        if (_sprite != null)
            _sprite.Position = Vector2.Zero;
        _parryLeft = 0.0f;
        DamageMult = 1.0f;
        RunMult = 1.0f;
        DashBonus = 0;
        SlamDamageMult = 1.0f;
        AttackReachMult = 1.0f;
        _dashEffect = StartingDashEffect;
        SpecialInvulnBonus = 0.0f;
        _iframesLeft = 0.0f;
        JumpVelocityBonus = 1.0f;
        _slamSpringBonus = 1.0f;
        MagnetTargetBonus = 0;
        FigChanceBonus = 0.0f;
        FigMagnetRange = 0.0f;
        HitShields = 0;
        RuhCap = BaseRuhCap;
        AirJumpBonus = 0;
        MaxHealth = BaseMaxHealth;
        _loadout.Clear();
        ApplyCharacter();
        Health = MaxHealth;
        Ruh = RuhCap;
        Velocity = Vector2.Zero;
        Spawn();
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

        if (_dashCharges < MaxDashCharges && (_dashCd -= delta) <= 0.0f)
        {
            _dashCharges += 1; // one charge back per cooldown; keep timing if more are still missing
            _dashCd = _dashCharges < MaxDashCharges ? _dashCooldown : 0.0f;
        }
        if (!HoldingSpecial())
            _specialCd = Mathf.Max(_specialCd - delta, 0.0f); // a held special's cooldown starts on release
        _launchCdLeft = Mathf.Max(_launchCdLeft - delta, 0.0f);
        UpdateOrbProximity();
        TrySurge();
        _ruhFlashCd = Mathf.Max(_ruhFlashCd - delta, 0.0f);
        _armorLeft = Mathf.Max(_armorLeft - delta, 0.0f);
        _iframesLeft = Mathf.Max(_iframesLeft - delta, 0.0f);
        if (_holdLeft > 0.0f)
        {
            _holdLeft = Mathf.Max(_holdLeft - delta, 0.0f);
            if (_holdLeft <= 0.0f)
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
        _gustLeft = onFloor && !_wasOnFloor ? 0.0f : Mathf.Max(_gustLeft - delta, 0.0f); // touching down ends a gust
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

        if (_state == State.Death)
            ProcessDeath(delta);
        else if (_state == State.Spawn)
            ProcessSpawn(delta);
        else if (_stunLeft > 0.0f)
            ProcessStun(delta);
        else if (_state == State.Dash)
            ProcessDash(delta);
        else if (_state == State.Attack)
            ProcessAttack(delta);
        else if (_state == State.Special)
            ProcessSpecial(delta);
        else if (_state == State.Surge)
            ProcessSurge(delta);
        else if (_state == State.Slam)
            ProcessSlam(delta);
        else if (_state == State.Land)
            ProcessLand(delta);
        else if (_state == State.Launch)
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
            _hurtbox.Monitorable = !_dead && _state != State.Spawn && _state != State.Launch
                && !(_state == State.Dash && _dashLeft > 0.0f)
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
        if (_state != State.Hurt)
            _state = State.Idle;
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
            if (_state != State.Dash)
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
            Enter(holdingDashDir && IsOnFloor() ? State.Run : State.Idle);
    }

    private void ProcessNormal(float delta)
    {
        float input = Input.GetAxis("move_left", "move_right");

        if (!IsOnFloor())
        {
            float gScale = Velocity.Y > 0.0f ? _fallGravityScale : 1.0f;
            AddVelY(_gravity * gScale * delta);
        }

        bool gusted = _gustLeft > 0.0f && !IsOnFloor();
        if (gusted)
        {
            // Riding a gust: no air brake, and steering only pushes back AGAINST the fling, weakly — holding the way
            // he's blown can't slow him to run speed either.
            if (input != 0.0f)
                _facing = input > 0.0f ? 1 : -1;
            if (input != 0.0f && Mathf.Sign(input) != Mathf.Sign(Velocity.X))
                SetVelX(Mathf.MoveToward(Velocity.X, input * RunSpeed(), _acceleration * Combat.GustControl * delta));
        }
        else if (input != 0.0f)
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
                Enter(State.Slam);
                return;
            }
        }
        if (AttackHeld() && !gusted && (IsOnFloor() || AirAttackOk())) // no swinging while blown (it would halt the fling)
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
            if (_dashCharges > 0)
            {
                Enter(State.Dash);
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
                _sfx.Play("jump");
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
            Enter(State.Land);
        else if (input != 0.0f && Mathf.Abs(Velocity.X) > 5.0f)
            _state = State.Run;
        else
            _state = State.Idle;
    }

    private void SetAirborneState()
    {
        if (Velocity.Y >= _landMinFallSpeed && HasLand() && NearGround())
        {
            Enter(State.Land);
            return;
        }
        if (_state == State.Jump || _state == State.Fall)
            return;
        _state = _jumpLaunch ? State.Jump : AirborneDefault();
    }

    private State AirborneDefault() => HasFall() ? State.Fall : State.Jump;

    // --- launch orbs ---
    private LaunchOrb? OrbInPullRange()
    {
        var body = GlobalPosition + LaunchBody;
        LaunchOrb? best = null;
        float bestD = LaunchPullRange * LaunchPullRange;
        foreach (Node o in GetTree().GetNodesInGroup("orbs"))
        {
            if (o is not LaunchOrb orb)
                continue;
            float d = body.DistanceSquaredTo(orb.GlobalPosition);
            if (d < bestD)
            {
                bestD = d;
                best = orb;
            }
        }
        return best;
    }

    private void UpdateOrbProximity()
    {
        LaunchOrb? near = null;
        if (!_dead && _state != State.Spawn && _state != State.Launch)
            near = OrbInPullRange();
        if (near == _nearOrb)
            return;
        if (_nearOrb != null && IsInstanceValid(_nearOrb))
            _nearOrb.SetNear(false);
        near?.SetNear(true);
        _nearOrb = near;
    }

    private void BeginLaunch(LaunchOrb orb)
    {
        _launchOrb = orb;
        _launchFrom = GlobalPosition;
        _launchT = 0.0f;
        _launchVel = new Vector2(_facing * orb.LaunchForward, -orb.LaunchUp);
        Velocity = Vector2.Zero;
        Enter(State.Launch);
        orb.PlayUse();
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

    /// <summary>Drop down through the one-way platform he's standing on: stop colliding with the Platform layer for
    /// <see cref="DropThroughTime"/> (solid ground stays solid). Only when the floor under him IS a platform — the
    /// tile bodies of a TileMapLayer carry their physics layer's collision layer, so the floor contact tells us.</summary>
    private void DropThroughPlatform()
    {
        const uint platform = (uint)Combat.Layer.Platform;
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var c = GetSlideCollision(i);
            if (c.GetNormal().Dot(UpDirection) < 0.5f || (PhysicsServer2D.BodyGetCollisionLayer(c.GetColliderRid()) & platform) == 0)
                continue; // not a floor contact with a platform
            CollisionMask &= ~platform;
            SetVelY(Mathf.Max(Velocity.Y, 60.0f));
            GetTree().CreateTimer(DropThroughTime).Timeout += () =>
            {
                if (IsInstanceValid(this))
                    CollisionMask |= platform;
            };
            return;
        }
    }

    private void AirJump()
    {
        _gustLeft = 0.0f; // an air jump catches him out of a gust — full air control back (a recovery move)
        SetVelY(AppliedJumpVelocity(false));
        _airJumpsUsed += 1;
        _sfx.Play("jump");
        _apexY = GlobalPosition.Y;
        _fallPeak = 0.0f;
        _jumpLaunch = true;
        Enter(State.Jump);
        _sprite.Play("jump");
        _sprite.SetFrameAndProgress(0, 0.0f);
        if (_particles != null)
        {
            float lean = Mathf.Clamp(Velocity.X / Mathf.Max(_runSpeedV, 1.0f), -1.0f, 1.0f);
            _particles.FireEffect("double_jump", lean * DoubleJumpLean);
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
                Enter(State.Slam);
                return;
            }
            if (AttackHeld() && AirAttackOk())
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
                if (_dashCharges > 0)
                {
                    Enter(State.Dash);
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
        if (AttackHeld())
        {
            AdvanceCombo();
            return;
        }
        if (Input.IsActionJustPressed("dash") && _dashCharges > 0)
        {
            Enter(State.Dash);
            return;
        }
        if (Input.IsActionJustPressed("jump"))
        {
            SetVelY(AppliedJumpVelocity(true));
            _jumpLaunch = true;
            _state = State.Jump;
            return;
        }

        float input = Input.GetAxis("move_left", "move_right");
        if (input != 0.0f)
        {
            _facing = input > 0.0f ? 1 : -1;
            SetVelX(Mathf.MoveToward(Velocity.X, input * RunSpeed(), _acceleration * delta));
            _state = State.Run;
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

        if (Input.IsActionJustPressed("special") && _specialCd <= 0.0f)
            _bufferedSpecial = true; // only a READY special — one on cooldown would stall the attack until it recharged

        if (_flurry)
        {
            if (_bufferedSpecial)
                StartSpecial();
            else if (!Input.IsActionPressed("attack"))
            {
                NotifyAttackAnimEnd();
                Enter(State.Idle);
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
                _recoveryLeft = Mathf.Max(AttackRecovery, _activeHit.Hold ?? 0.0f);
                _comboWindow = ComboResetTime;
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
        // A press chains the next hit; HOLDING chains it too — except after the combo's last hit, which keeps its
        // recovery beat before holding loops the combo back to its first hit (via IDLE).
        if (Input.IsActionJustPressed("attack") || (AttackHeld() && _currentAttack is { } held && _comboStep < AttackHits(held).Count))
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
            Enter(State.Idle);
        }
    }

    /// <summary>The attack button is down — every attack keeps going while it's held (flurries loop, combos chain).</summary>
    private static bool AttackHeld() => Input.IsActionPressed("attack");

    /// <summary>A "held" special (Redere Shield) is up right now — its cooldown waits until it's released.</summary>
    private bool HoldingSpecial() => _state == State.Special && _currentSpecial != null && HasTag(_currentSpecial, "held");

    private void StartSpecial()
    {
        if (_specialCd > 0.0f || _currentSpecial is not { } special)
            return;
        _specialCd = CooldownOf(special); // every special has its own cooldown
        bool isShield = HasTag(special, "shield");
        foreach (var p in _passives)
            p.OnSpecialCast(this, special);
        if (isShield)
            _parryLeft = ParryWindow;
        _comboStep = 0;
        _comboWindow = 0.0f;
        _comboPlaying = false;
        _bufferedSpecial = false;
        _activeHit = ResolveTuning(special, 0);
        _activeHit.FromSpecial = true;
        Enter(State.Special);
        if (HasAnim(Anim(special)))
        {
            _sprite.Play(Anim(special));
            _sprite.SetFrameAndProgress(0, 0.0f);
        }
    }

    private void ProcessSpecial(float delta)
    {
        // A special that carries Lunge (e.g. Zahluq) dashes through: hold vertical and let the lunge impulse
        // ride instead of friction-damping it (mirrors the dash branch in ProcessAttack). SetArmor is already
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
                    Enter(State.Idle);
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
                    _surgeLeft = _surgeSleepTime;
                }
            }
            else
            {
                Health = Mathf.Min(Health + _surgeHealRate * delta, _surgeHealTarget);
                _surgeLeft -= delta;
                if (_surgeLeft <= 0.0f)
                {
                    EndSurge();
                    Enter(State.Idle);
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
        int hold = Mathf.Max(0, _slamHoldFrame - AnimMeta.SheetStart(_sprite.SpriteFrames, "slam"));
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
        _sfx.Play("slam");
        float drop = GlobalPosition.Y - _slamStartY;
        float t = Mathf.Clamp((drop - _slamMinDrop) / Mathf.Max(_slamMaxDrop - _slamMinDrop, 1.0f), 0.0f, 1.0f);
        _activeHit = new SegmentData { DamageScale = Mathf.Lerp(1.0f, _slamMaxDamageMult, t) * SlamDamageMult };
        foreach (var p in _passives)
            p.OnSlamLand(this, drop, Mathf.Max(Velocity.Y, _slamSpeed));
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
        if (_currentAttack is not { } attack)
            return;
        if (IsFlurry(attack))
        {
            if (!_flurry)
                StartFlurry(attack);
            return;
        }
        var hits = AttackHits(attack);
        if (hits.Count == 0)
            return;
        _bufferedSpecial = false;

        if (_comboWindow <= 0.0f || _comboStep >= hits.Count)
            _comboStep = 0;
        int segStart = _comboStep == 0 ? 0 : hits[_comboStep - 1] + 1;
        _segEnd = hits[_comboStep];
        _comboStep += 1;
        _activeHit = ResolveTuning(attack, _comboStep - 1);

        _comboWindow = ComboResetTime;
        _comboPlaying = true;
        Enter(State.Attack);
        _sprite.SpeedScale = 1.0f;
        _sprite.Play(Anim(attack));
        _sprite.SetFrameAndProgress(segStart, 0.0f);
    }

    private void StartFlurry(Action attack)
    {
        _bufferedSpecial = false;
        _flurry = true;
        _activeHit = ResolveTuning(attack, 0);
        Enter(State.Attack);
        _sprite.SpeedScale = 1.0f;
        _sprite.Play(Anim(attack));
    }

    /// <summary>The frames that end each combo segment: the authored hit frames, or every frame when none are authored.</summary>
    private IReadOnlyList<int> AttackHits(Action attack) => AnimMeta.HitFramesOrAll(_sprite.SpriteFrames, Anim(attack));

    private void Enter(State state)
    {
        _state = state;
        if (state != State.Attack)
        {
            // Leaving an attack by ANY route (release, special, surge, hurt, …) ends its flurry / combo segment. Done
            // here, centrally, because a stale _flurry makes AdvanceCombo swallow every later attack press.
            _flurry = false;
            _comboPlaying = false;
        }
        _sprite.SpeedScale = 1.0f;
        _sprite.Visible = true;
        switch (state)
        {
            case State.Dash:
                _gustLeft = 0.0f; // dashing breaks out of a gust (a recovery move)
                _dashLeft = _dashTime;
                _dashAnimLeft = Mathf.Max(_dashAnimTime, _dashTime);
                if (_dashCharges == MaxDashCharges)
                    _dashCd = _dashCooldown; // the refill clock starts with the first charge spent
                _dashCharges -= 1;
                _bufferedAttack = false;
                _sfx.Play("dash");
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
            case State.Attack:
                SetVelX(0.0f);
                break;
            case State.Hurt:
                if (HasAnim("hurt"))
                {
                    _sprite.Play("hurt");
                    _sprite.SetFrameAndProgress(0, 0.0f);
                }
                else
                {
                    _state = State.Idle;
                }
                break;
            case State.Surge:
                SetVelX(0.0f);
                if (_currentSurge != null && HasAnim(Anim(_currentSurge)))
                {
                    _sprite.Play(Anim(_currentSurge));
                    _sprite.SetFrameAndProgress(0, 0.0f);
                }
                else
                {
                    _state = State.Idle;
                }
                break;
            case State.Death:
                SetVelX(0.0f);
                _deathFrozen = true;
                _sprite.Play("death");
                _sprite.SetFrameAndProgress(0, 0.0f);
                _sprite.Pause();
                break;
            case State.Spawn:
                SetVelX(0.0f);
                break;
            case State.Slam:
                Velocity = new Vector2(0.0f, _slamSpeed);
                _slamImpacting = false;
                _slamStartY = GlobalPosition.Y;
                _slamDownSfx?.Play();
                foreach (var p in _passives)
                    p.OnSlamTrigger(this);
                break;
            case State.Launch:
                _sprite.Play("dash");
                break;
        }
    }

    private StringName AnimationFor(State state) => state switch
    {
        State.Run => "run",
        State.Jump => "jump",
        State.Fall => "fall",
        State.Dash => "dash",
        State.Attack => _currentAttack != null ? Anim(_currentAttack) : "idle",
        State.Special => _currentSpecial != null ? Anim(_currentSpecial) : "idle",
        State.Land => "land",
        State.Slam => "slam",
        State.Death => "death",
        State.Spawn => "spawn",
        State.Hurt => "hurt",
        State.Surge => _currentSurge != null ? Anim(_currentSurge) : "idle",
        State.Launch => "dash",
        _ => "idle",
    };

    private void UpdateAnimation(float delta)
    {
        _sprite.FlipH = _facing < 0;
        if (_runSfx != null)
        {
            bool running = _state == State.Run;
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
            case State.Run:
                float speedRatio = Mathf.Abs(Velocity.X) / Mathf.Max(_runSpeedV, 1.0f);
                _sprite.SpeedScale = Mathf.Clamp(speedRatio * _runAnimSpeed, 0.4f, 3.0f);
                if (_runSfx != null)
                    _runSfx.PitchScale = Mathf.Clamp(speedRatio, 0.6f, 3.0f);
                break;
            case State.Idle:
            case State.Jump:
            case State.Fall:
            case State.Land:
                _sprite.SpeedScale = 1.0f;
                break;
        }
    }

    private void OnAnimationLooped()
    {
        int start = AnimMeta.LoopFrom(_sprite.SpriteFrames, _sprite.Animation);
        if (start > 0)
            _sprite.SetFrameAndProgress(start, 0.0f);
    }

    private void OnAnimationFinished()
    {
        if (_state == State.Death)
        {
            _sprite.Visible = false;
            _deathFinished = true;
            return;
        }
        if (_state == State.Spawn)
        {
            Enter(State.Idle);
            return;
        }
        if (_state == State.Jump && !IsOnFloor() && HasFall())
        {
            Enter(State.Fall);
            return;
        }
        if (_state == State.Land && !IsOnFloor())
        {
            Enter(AirborneDefault());
            return;
        }
        if (_state == State.Dash || _state == State.Special || _state == State.Land || _state == State.Slam)
        {
            _activeHit = new SegmentData();
            Enter(State.Idle);
        }
        if (_state == State.Surge)
            Enter(!IsOnFloor() ? AirborneDefault() : State.Idle);
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
