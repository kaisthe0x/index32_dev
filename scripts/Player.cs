using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// The player's body. A character-agnostic one: every character shares the same animation set + normalised sprite
/// canvas, so switching is just swapping the SpriteFrames resource.
///
/// <para>This file is what every state shares and what the rest of the game talks to: health and Ruh, the loadout
/// (the equipped <see cref="Action"/>s and the movement numbers they carry), passives and the tuning seam, taking
/// damage and dying, and the per-tick pipeline. WHAT HE IS DOING is a state — one class each, nested here and kept in
/// <c>scripts/player/states/</c> (<see cref="PlayerState"/>): free movement, dash, attack, special, surge, slam, land,
/// launch, death, spawn. Self-contained pieces are in <c>scripts/player/parts/</c> (<see cref="Wallet"/>,
/// <see cref="BodyTint"/>). What a strike asks of its wielder goes through <see cref="IStrikeWielder"/>.</para>
/// </summary>
[Tool]
[GlobalClass]
public partial class Player : Combatant, IStrikeWielder
{
    // --- signals (the HUD listens to these) ---
    [Signal] public delegate void HealthChangedEventHandler(double current, double maximum);
    [Signal] public delegate void RuhChangedEventHandler(double current, double maximum);

    private const string FramesPathTmpl = "res://resources/characters/{0}.tres";

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

    [Export] public float AttackRecovery = 0.12f;
    [Export] public float ComboResetTime = 0.45f;

    // --- the state machine: one instance of each state, and which one he is in (scripts/player/states/) ---
    private readonly FreeState _free;
    private readonly DashState _dash;
    private readonly AttackState _attack;
    private readonly SpecialState _special;
    private readonly SurgeState _surge;
    private readonly SlamState _slam;
    private readonly LandState _land;
    private readonly LaunchState _launch;
    private readonly DeathState _death;
    private readonly SpawnState _spawn;
    private PlayerState _current;

    public Player()
    {
        _free = new FreeState(this);
        _dash = new DashState(this);
        _attack = new AttackState(this);
        _special = new SpecialState(this);
        _surge = new SurgeState(this);
        _slam = new SlamState(this);
        _land = new LandState(this);
        _launch = new LaunchState(this);
        _death = new DeathState(this);
        _spawn = new SpawnState(this);
        _current = _free;
        _tint = new BodyTint(this);
    }

    private int _facing = 1;
    // The equipped actions. Null until a character with that slot is applied -- in the editor (this is a [Tool]
    // script) and for a character whose sprite frames are missing, ApplyCharacter stops before the loadout.
    private Action? _currentAttack;
    private Action? _currentSpecial;
    private Action? _currentSurge;
    private readonly System.Collections.Generic.Dictionary<LoadoutCategory, string> _loadout = new();
    private SegmentData _activeHit = new();
    private float _dashCd = 0.0f;         // time until the next dash charge refills (runs while below max)
    private int _dashCharges = 1;
    private bool _blinkDash = false;
    private bool _wasOnFloor = true;
    private float _fallPeak = 0.0f;
    private float _apexY = 0.0f;
    private bool _dead = false;
    private bool _fellOut = false;   // died by falling out of the arena — free-falls off-screen, no death animation
    private bool _justLanded = false;
    private float _stunLeft = 0.0f;   // a flinch / stagger in progress: no control until it runs out (any state but death / spawn)
    private float _gustLeft = 0.0f; // a gust is carrying him (Combat.GustCarryTime): weak steering, no air brake
    private float _armorLeft = 0.0f;
    private float _holdLeft = 0.0f;
    private BlastStrike? _channel;
    private readonly List<Passive> _passives = new();

    private float _iframesLeft = 0.0f;  // generic invulnerability window (GrantInvuln) — the immunity buffs

    // --- shield / parry ---
    [Export] public float ParryWindow = 0.25f;
    [Export] public float ShieldReflectMult = 1.0f;
    private float _shakeLeft = 0.0f, _shakeDur = 0.0f, _shakeAmp = 0.0f;
    [Export] public float ShieldShakeAmp = 4.0f;
    [Export] public float ShieldShakeTime = 0.18f;
    [Export] public bool FlinchOnAllDamage = true;

    private AudioStreamPlayer? _runSfx;
    private AudioStreamPlayer? _slamDownSfx;
    private readonly BodyTint _tint;

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
        _tint.Apply(sprite, Character);
        ApplyLoadout();
        AnchorToFeet(sprite);
        _attack.Reset();
        _dash.ClearBuffer();
        _dashCd = 0.0f;
        _dashCharges = MaxDashCharges;
        if (!Engine.IsEditorHint())
            SetFree(FreeMode.Idle);
        sprite.SpeedScale = 1.0f;
        sprite.Play(_current.Animation);
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

    /// <summary>The Lira and Fada Figs he has banked this run.</summary>
    public Wallet Wallet { get; } = new();

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

    public bool IsSpawning() => _current == _spawn;

    /// <summary>Which way Khalid faces: +1 right, -1 left (RunManager spawns grunts on the other side).</summary>
    public int Facing => _facing;

    // =====================================================================================================
    // Action helpers (thin typed accessors over the equipped Action)
    // =====================================================================================================
    private bool HasAnim(StringName anim) =>
        _sprite != null && _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim);

    private bool AirAttackOk() => _currentAttack != null && _currentAttack.HasTag("air");

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
    public void SetJumpSpring(float mult) => _free.SpringBonus = mult;

    /// <summary>Shave <paramref name="seconds"/> off the special's cooldown (clamped at ready).</summary>
    public void ReduceSpecialCooldown(float seconds) => _special.Cooldown = Mathf.Max(_special.Cooldown - seconds, 0.0f);

    /// <summary>How recharged the special is, 0..1 (1 = ready to cast) — drives the HUD's special bar.</summary>
    public float SpecialReady()
    {
        float cd = _currentSpecial != null ? _currentSpecial.Cooldown : 0.0f;
        return cd > 0.0f ? 1.0f - _special.Cooldown / cd : 1.0f;
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

    /// <summary>Global cooldown fairness: an action whose windup is interrupted by a stagger BEFORE its hit came out
    /// never actually fired, so it shouldn't burn its cooldown. An ATTACK still short of its hit frame
    /// (<see cref="_segEnd"/>) or a SPECIAL short of its strike frame is "uncommitted" → zero the matching cooldown.
    /// Called from <see cref="OnHurt"/> the instant a hurt is about to knock the player into HURT (windup cancelled).</summary>
    private void RefundUncommittedCooldown()
    {
        if (_sprite == null)
            return;
        if (_current == _special && _sprite.Frame < _special.StrikeFrame())
            _special.Cooldown = 0.0f;
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

    public bool GainRuhOnHit()
    {
        float before = Ruh;
        Ruh += RuhPerHit;
        return Mathf.FloorToInt(Ruh / RuhPerBlock) > Mathf.FloorToInt(before / RuhPerBlock);
    }

    public void OnRuhAbsorbed(bool completedCharge)
    {
        if (_tint.FlareOnRuh(completedCharge))
            _sfx.Play("ruh_absorb", 0.0f, completedCharge ? 1.12f : 1.0f);
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
        float dmgMult = DamageMult * _surge.DamageMult;
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

    private void OnHurt(Hit hit)
    {
        if (_iframesLeft > 0.0f)
            return;  // generic i-frames (immunity buffs) — ignore the hit entirely
        if (_special.Shielding)
        {
            bool fromBehind = hit.Source is Node2D src2
                && Mathf.Sign(src2.GlobalPosition.X - GlobalPosition.X) == -_facing;
            if (!fromBehind)
            {
                if (_special.ParryLeft > 0.0f)
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
        if (_surge.Armed && hit.Source is Enemy)
        {
            _surge.TriggerArmed();
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
            if (_current == _free && _free.Mode == FreeMode.Hurt)
                _stunLeft = Mathf.Max(_stunLeft, flinch);
            else
            {
                _stunLeft = flinch;
                EnterFree(FreeMode.Hurt);
            }
            _attack.DropBufferedSpecial();
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
        if (_current == _launch)
            _launch.Release(lockOut: true);
        if (_channel != null && IsInstanceValid(_channel) && _channel.InterruptOnHurt)
        {
            _channel.Cancel();
            _holdLeft = 0.0f;
            _sprite.Play();
        }
        _channel = null;
        if (_surge.Channelling)
        {
            _surge.End();
            if (_current == _surge)
                EnterFree(FreeMode.Idle);
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
        _attack.DropBufferedSpecial();
        Velocity = GustVelocity(hit, _facing);
        _gustLeft = Combat.GustCarryTime;
        _free.JumpLaunch = false;
        EnterFree(_free.AirborneDefault());
    }

    public void ApplyLunge(float impulse) => SetVelX(impulse * _facing);

    public void SetArmor(float duration) => _armorLeft = Mathf.Max(_armorLeft, duration);

    public void SetDashEffect(string effect) => _dashEffect = effect;

    private float RunSpeed() => _runSpeedV * _surge.SpeedMult;

    /// <summary>Fire the equipped surge WITHOUT spending Ruh (the Prepared perk, at round start). No-op if one is
    /// already going.</summary>
    public void SurgeFree() => _surge.FireFree();

    /// <summary>Tell every passive a round began (RunManager.StartRound) — see <see cref="Passive.OnRoundStart"/>.</summary>
    public void NotifyRoundStart()
    {
        foreach (var p in new List<Passive>(_passives))
            p.OnRoundStart(this);
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
        if (_current == _special)
        {
            if (_sprite.Frame == _special.StrikeFrame())
                foreach (var p in _passives)
                    p.OnSpecialStrike(this);
            return;
        }
        int loopTo = AnimMeta.LoopTo(_sprite.SpriteFrames, _sprite.Animation);
        if (loopTo >= 0 && _sprite.Frame > loopTo)
            _sprite.SetFrameAndProgress(Mathf.Max(AnimMeta.LoopFrom(_sprite.SpriteFrames, _sprite.Animation), 0), 0.0f);
    }

    public bool IsDead() => _dead;
    /// <summary>In a channelled surge (Nem's sleep) — the stand-still kamikaze clock pauses for it.</summary>
    public bool IsChannelingSurge() => _surge.Channelling;
    public bool DeathComplete() => _dead && _death.Finished;

    /// <summary>Let the death animation's held first frame go (the run's death sequence calls this after its beat).</summary>
    public void ReleaseDeath() => _death.Release();

    /// <summary>Common death: stop everything in progress and disable the hurtbox. A normal death then plays the death
    /// animation; a <paramref name="fell"/> death (out of the arena) skips it and free-falls in the fall animation.</summary>
    private void Die(bool fell = false)
    {
        if (_dead)
            return;
        _dead = true;
        _death.Finished = false;
        _sfx.Play(fell ? "player_fall_death" : "player_death");
        _stunLeft = 0.0f;
        _attack.StopSwing();
        _holdLeft = 0.0f;
        _surge.End();
        if (_channel != null && IsInstanceValid(_channel))
            _channel.Cancel();
        _channel = null;
        _launch.Release(lockOut: false);
        if (_hurtbox != null)
            // Die() runs inside the hurtbox's hit-signal flush; a direct set is blocked while physics
            // queries flush ("Function blocked during in/out signal"), so defer it to after the flush.
            _hurtbox.SetDeferred(Area2D.PropertyName.Monitorable, false);
        if (fell)
        {
            _fellOut = true;
            _death.Finished = true; // no death animation to wait for
            if (HasFall())
                _sprite.Play("fall");
            return;
        }
        if (HasAnim("death"))
            Enter(_death);
        else
            _death.Finished = true;
    }

    /// <summary>Fell out of the arena: no input, no state machine — gravity just carries him down in the fall animation.</summary>
    private void ProcessFreefall(float delta)
    {
        AddVelY(_gravity * _fallGravityScale * delta);
        MoveAndSlide();
    }

    public void Spawn()
    {
        Velocity = Vector2.Zero;
        if (HasAnim("spawn"))
            Enter(_spawn);
        else
        {
            if (_hurtbox != null)
                _hurtbox.Monitorable = true;
            EnterFree(FreeMode.Idle);
        }
    }

    public void BeginRun()
    {
        // Buffs FIRST: each Teardown undoes its own change (e.g. -1 air jump, ÷ jump height), so it must run while
        // those changes are still in place — resetting the stats below first made every undo apply twice.
        ClearPassives();
        _dead = false;
        _death.Finished = false;
        _fellOut = false;
        Wallet.Empty();
        _surge.End();
        _shakeLeft = 0.0f;
        if (_sprite != null)
            _sprite.Position = Vector2.Zero;
        _special.ParryLeft = 0.0f;
        DamageMult = 1.0f;
        RunMult = 1.0f;
        DashBonus = 0;
        SlamDamageMult = 1.0f;
        AttackReachMult = 1.0f;
        _dashEffect = StartingDashEffect;
        SpecialInvulnBonus = 0.0f;
        _iframesLeft = 0.0f;
        JumpVelocityBonus = 1.0f;
        _free.SpringBonus = 1.0f;
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
        if (!_special.Holding)
            _special.Cooldown = Mathf.Max(_special.Cooldown - delta, 0.0f); // a held special's cooldown starts on release
        _launch.Update(delta);
        _surge.TryFireFromInput();
        _tint.Tick(delta);
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
            _free.AirJumpsUsed = 0;
        }
        _wasOnFloor = onFloor;

        // Death and spawn run regardless; a stagger takes control away from any other state; otherwise the
        // current state has the tick.
        if (_current == _death || _current == _spawn)
            _current.Tick(delta);
        else if (_stunLeft > 0.0f)
            ProcessStun(delta);
        else
        {
            if (_current == _free)
                _attack.ComboWindow = Mathf.Max(_attack.ComboWindow - delta, 0.0f);
            _current.Tick(delta);
        }

        foreach (var p in _passives)
            p.Physics(this, delta);

        _surge.Update(delta);
        _special.ParryLeft = Mathf.Max(_special.ParryLeft - delta, 0.0f);
        if (_shakeLeft > 0.0f)
        {
            _shakeLeft = Mathf.Max(_shakeLeft - delta, 0.0f);
            float amp = _shakeAmp * (_shakeLeft / _shakeDur);
            _sprite.Position = _shakeLeft > 0.0f
                ? new Vector2((float)GD.RandRange(-amp, amp), (float)GD.RandRange(-amp, amp))
                : Vector2.Zero;
        }

        if (_hurtbox != null)
            _hurtbox.Monitorable = !_dead && _current != _spawn && _current != _launch
                && !(_current == _dash && _dash.Left > 0.0f)
                && !_surge.Invulnerable;

        MoveAndSlide();
        UpdateAnimation(delta);
    }

    private void ProcessStun(float delta)
    {
        _stunLeft -= delta;
        if (!IsOnFloor())
            AddVelY(_gravity * delta);
        SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, _friction * 0.5f * delta));
        if (!(_current == _free && _free.Mode == FreeMode.Hurt))
            SetFree(FreeMode.Idle);
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

    private void UpdateAnimation(float delta)
    {
        _sprite.FlipH = _facing < 0;
        if (_runSfx != null)
        {
            bool running = _current == _free && _free.Mode == FreeMode.Run;
            if (running != _runSfx.Playing)
            {
                if (running)
                    _runSfx.Play();
                else
                    _runSfx.Stop();
            }
        }
        var next = _current.Animation;
        if (_sprite.Animation != next)
        {
            _sprite.Play(next);
            if (next == "jump" && !_free.JumpLaunch)
            {
                int jn = _sprite.SpriteFrames.GetFrameCount("jump");
                if (jn > 0)
                    _sprite.SetFrameAndProgress(jn - 1, 0.0f);
            }
        }
        if (next == "jump")
            _free.JumpLaunch = false;

        // The run animation (and its footsteps) keep pace with his real speed; the other movement loops play at 1x.
        // Every other state sets its own speed when it starts.
        if (_current == _free && _free.Mode == FreeMode.Run)
        {
            float speedRatio = Mathf.Abs(Velocity.X) / Mathf.Max(_runSpeedV, 1.0f);
            _sprite.SpeedScale = Mathf.Clamp(speedRatio * _runAnimSpeed, 0.4f, 3.0f);
            if (_runSfx != null)
                _runSfx.PitchScale = Mathf.Clamp(speedRatio, 0.6f, 3.0f);
        }
        else if ((_current == _free && _free.Mode != FreeMode.Hurt) || _current == _land)
        {
            _sprite.SpeedScale = 1.0f;
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
        if (_current == _death)
        {
            _sprite.Visible = false;
            _death.Finished = true;
            return;
        }
        if (_current == _spawn)
        {
            EnterFree(FreeMode.Idle);
            return;
        }
        if (_current == _free && _free.Mode == FreeMode.Jump && !IsOnFloor() && HasFall())
        {
            EnterFree(FreeMode.Fall);
            return;
        }
        if (_current == _land && !IsOnFloor())
        {
            EnterFree(_free.AirborneDefault());
            return;
        }
        if (_current == _dash || _current == _special || _current == _land || _current == _slam)
        {
            _activeHit = new SegmentData();
            EnterFree(FreeMode.Idle);
        }
        if (_current == _surge)
            EnterFree(!IsOnFloor() ? _free.AirborneDefault() : FreeMode.Idle);
    }

    // =====================================================================================================
    // State changes
    // =====================================================================================================

    /// <summary>Change state: the sprite is reset to normal speed and shown, any swing in progress ends, and the new
    /// state's <see cref="PlayerState.Enter"/> runs.</summary>
    private void Enter(PlayerState next)
    {
        _current = next;
        if (next != _attack)
            _attack.StopSwing();
        _sprite.SpeedScale = 1.0f;
        _sprite.Visible = true;
        next.Enter();
    }

    /// <summary>Change to free movement looking like <paramref name="mode"/>, as a real state change (see
    /// <see cref="Enter"/>). Entering a flinch plays the hurt animation from its start — or, for a character with
    /// none, just stands.</summary>
    private void EnterFree(FreeMode mode)
    {
        _free.Mode = mode;
        Enter(_free);
        if (mode != FreeMode.Hurt)
            return;
        if (HasAnim("hurt"))
        {
            _sprite.Play("hurt");
            _sprite.SetFrameAndProgress(0, 0.0f);
        }
        else
        {
            _free.Mode = FreeMode.Idle;
        }
    }

    /// <summary>Relabel free movement as <paramref name="mode"/> with NO side effect — the per-tick idle / run / jump
    /// / fall bookkeeping, which must not reset the sprite or end a swing.</summary>
    private void SetFree(FreeMode mode)
    {
        _current = _free;
        _free.Mode = mode;
    }

    // =====================================================================================================
    // Small helpers
    // =====================================================================================================
    private void SetVelX(float x) { var v = Velocity; v.X = x; Velocity = v; }
    private void SetVelY(float y) { var v = Velocity; v.Y = y; Velocity = v; }
    private void AddVelY(float dy) { var v = Velocity; v.Y += dy; Velocity = v; }

}
