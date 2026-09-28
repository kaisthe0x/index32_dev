using Godot;

namespace MyGame;

/// <summary>
/// An ACTIVE Needle Point shot on Khalid: one <see cref="ShotDef"/> at one level, held as a <see cref="Passive"/> so it
/// grants and tears down through the same machinery as buffs. <see cref="Setup"/> applies its number to the player,
/// <see cref="Teardown"/> undoes it exactly (additive stats subtract, multipliers divide). How long it stays is the
/// <see cref="ShotLedger"/>'s business — it keeps <see cref="RoundsLeft"/> current for the HUD.
/// </summary>
public partial class Shot : Passive
{
    public readonly ShotDef Def;
    public readonly int Level;

    /// <summary>Rounds this shot still covers — counted down at each round clear; the HUD's number.</summary>
    public int RoundsLeft;

    public Shot(ShotDef def, int level, int roundsLeft)
    {
        Id = def.Id;
        Def = def;
        Level = level;
        RoundsLeft = roundsLeft;
    }

    public float Value => Def.Levels[Level];

    public override void Setup(Player p) => Apply(p, Value, true);
    public override void Teardown(Player p) => Apply(p, Value, false);

    private void Apply(Player p, float v, bool on)
    {
        float f = on ? v : 1.0f / v;  // multiplier stats
        int n = on ? (int)v : -(int)v; // additive stats
        switch (Def.Stat)
        {
            case ShotStat.Dashes: p.add_dash_charges(n); break;
            case ShotStat.AirJumps: p.add_air_jumps(n); break;
            case ShotStat.JumpHeight: p.jump_velocity_bonus *= f; break;
            case ShotStat.RunSpeed: p.scale_run_speed(f); break;
            case ShotStat.Reach: p.attack_reach_mult *= f; break;
            case ShotStat.AttackDamage: p.damage_mult *= f; break;
            case ShotStat.SlamDamage: p.slam_damage_mult *= f; break;
        }
    }

    /// <summary>A level's value as the player reads it: "2" for an additive stat, "15%" for a multiplier.</summary>
    public static string FormatValue(ShotDef def, int level)
    {
        float v = def.Levels[level];
        return def.Stat is ShotStat.Dashes or ShotStat.AirJumps ? $"{(int)v}" : $"{Mathf.RoundToInt((v - 1.0f) * 100.0f)}%";
    }

    /// <summary>The player-facing effect line at a level ("+15% jump height").</summary>
    public static string EffectText(ShotDef def, int level) => string.Format(def.Effect, FormatValue(def, level));
}
