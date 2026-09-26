namespace MyGame;

/// <summary>
/// Stable string IDs for every buff in the catalog (docs/buff-catalog.md). `const string` (see
/// <see cref="AttackIds"/>): the id IS the key into <see cref="BuffCatalog"/> and the reward/offer tables —
/// nothing to convert. Reference <c>BuffIds.AtkDamage</c>, never a raw literal. Grouped by the catalog's
/// categories; a buff's category is implied by its trigger + move scope, so there is no separate category enum.
/// </summary>
public static class BuffIds
{
    // --- Dash (OnDash / OnPerfectDodge) ---
    public const string DashImmunity = "dash_immunity";
    public const string ChainDash = "chain_dash";
    public const string DashDamage = "dash_damage";     // NEW mechanic (dash hitbox)
    public const string DashLeech = "dash_leech";        // NEW (needs DashDamage)
    public const string DashStun = "dash_stun";          // NEW
    public const string DashTrap = "dash_trap";          // NEW (trap entity)
    public const string PdHaste = "pd_haste";            // reserved trigger OnPerfectDodge
    public const string PdFury = "pd_fury";              // reserved
    public const string PdAegis = "pd_aegis";            // reserved

    // --- Jump (OnGroundJump / OnAirJump) ---
    public const string JumpImmunity = "jump_immunity";
    public const string HighJump = "high_jump";
    public const string JumpTrap = "jump_trap";          // NEW (trap entity)
    public const string ExtraAirJump = "extra_air_jump";
    public const string PeakSlam = "peak_slam";          // NEW (peak window + cue)

    // --- Slam (OnSlamTrigger / OnSlamLand) ---
    public const string SlamVolley = "slam_volley";      // NEW (downward projectiles)
    public const string SlamForce = "slam_force";
    public const string SlamImmunity = "slam_immunity";
    public const string SlamQuake = "slam_quake";
    public const string SlamFeast = "slam_feast";
    public const string SlamSpring = "slam_spring";
    public const string SlamWrath = "slam_wrath";

    // --- Attack (general) ---
    public const string LongReach = "long_reach";
    public const string OpeningFury = "opening_fury";    // reserved (stage timer)
    public const string Momentum = "momentum";
    public const string HitGuard = "hit_guard";
    public const string FollowThrough = "follow_through"; // reserved (OnAnimEnd)

    // --- Attack (per-attack — offered only when that attack is equipped) ---
    public const string LedgeSave = "ledge_save";        // Zahluq; NEW; reserved OnAnimEnd
    public const string Bloodrush = "bloodrush";         // Zahluq
    public const string InstantReset = "instant_reset";  // Zahluq; reserved OnMiss
    public const string Barrage = "barrage";             // Ora Ora; NEW
    public const string Skim = "skim";                   // Ora Ora
    public const string AirWall = "air_wall";            // Ora Ora; NEW; reserved OnMiss
    public const string SpearFinisher = "spear_finisher"; // Spear; NEW
    public const string Backstab = "backstab";           // Spear
    public const string Missfire = "missfire";           // Spear; NEW; reserved OnMiss
    public const string Overcharge = "overcharge";       // Bakshen

    // --- Surge / Special ---
    public const string Prepared = "prepared";           // all surges
    public const string WiderPull = "wider_pull";        // Come Closer special

    // --- Traps (shared sub-system; a Dash/Jump trap picks a flavour) — NEW (trap entity) ---
    public const string TrapSnare = "trap_snare";
    public const string TrapSap = "trap_sap";
    public const string TrapPyre = "trap_pyre";
    public const string TrapMine = "trap_mine";
}
