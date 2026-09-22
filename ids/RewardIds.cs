namespace MyGame;

/// <summary>
/// Stable string IDs for the reward catalog (<see cref="RewardsCatalog"/>). `const string` (see <see cref="AttackIds"/>):
/// the id is the runtime reward key (build tracking / offer dedup / effect dispatch). Some coincide by value with a
/// <see cref="PassiveIds"/> entry (a reward that grants that passive) — that's fine, they're distinct namespaces.
/// </summary>
public static class RewardIds
{
    // health
    public const string Mend = "mend";
    public const string MaxHp = "max_hp";
    // athletic
    public const string AirJump = "air_jump";
    public const string Run = "run";
    public const string Tough = "tough";
    public const string SlamDmg = "slam_dmg";
    public const string CrimsonVortex = "crimson_vortex";
    // attack
    public const string Reach = "reach";
    public const string AtkDmg = "atk_dmg";
    public const string Lifesteal = "lifesteal";
    public const string Multishot = "multishot";
    public const string ReaperEdge = "reaper_edge";
    public const string RopeDart = "rope_dart";
    // special
    public const string RuhCap = "ruh_cap";
    public const string LongerImp = "longer_imp";
    public const string ImpUntilHit = "imp_until_hit";
    public const string BiggerBlast = "bigger_blast";
    public const string ParryMend = "parry_mend";
}
