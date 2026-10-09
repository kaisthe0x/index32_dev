using Godot;

namespace MyGame;

/// <summary>
/// A Needle Point shot on Khalid: one <see cref="ShotDef"/> at the rank bought so far, held as a <see cref="Passive"/>
/// so it grants and tears down through the same machinery as buffs. It lasts the run; buying the next rank replaces it
/// with a stronger copy (<see cref="ShotLedger"/>). <see cref="Setup"/> applies its number to the player,
/// <see cref="Teardown"/> undoes it exactly (additive stats subtract, multipliers divide).
/// </summary>
public partial class Shot : Passive
{
    public readonly ShotDef Def;
    public readonly int Rank; // 1 = rank I

    public Shot(ShotDef def, int rank)
    {
        Id = def.Id;
        Def = def;
        Rank = rank;
    }

    public float Value => Def.Levels[Rank - 1];

    public override void Setup(Player p) => Apply(p, Value, true);
    public override void Teardown(Player p) => Apply(p, Value, false);

    private void Apply(Player p, float v, bool on)
    {
        float f = on ? v : 1.0f / v;  // multiplier stats
        int n = on ? (int)v : -(int)v; // additive stats
        switch (Def.Stat)
        {
            case ShotStat.Dashes: p.AddDashCharges(n); break;
            case ShotStat.AirJumps: p.AddAirJumps(n); break;
            case ShotStat.JumpHeight: p.JumpVelocityBonus *= f; break;
            case ShotStat.RunSpeed: p.ScaleRunSpeed(f); break;
            case ShotStat.Reach: p.AttackReachMult *= f; break;
            case ShotStat.AttackDamage: p.DamageMult *= f; break;
            case ShotStat.SlamDamage: p.SlamDamageMult *= f; break;
        }
    }

    /// <summary>A rank's value as the player reads it: "2" for an additive stat, "15%" for a multiplier.</summary>
    public static string FormatValue(ShotDef def, int rank)
    {
        float v = def.Levels[rank - 1];
        return def.Stat is ShotStat.Dashes or ShotStat.AirJumps ? $"{(int)v}" : $"{Mathf.RoundToInt((v - 1.0f) * 100.0f)}%";
    }

    /// <summary>The player-facing effect line at a rank ("+15% jump height").</summary>
    public static string EffectText(ShotDef def, int rank) => string.Format(def.Effect, FormatValue(def, rank));

    /// <summary>A rank as a roman numeral (the menu + HUD label).</summary>
    public static string Roman(int rank) => rank switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => rank.ToString() };
}
