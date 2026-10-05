using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MyGame;

/// <summary>
/// NEEDLE POINT — the stat stall's catalog + prices (docs/game-loop.md § Economy). Pure data; the rules live in
/// <see cref="ShotLedger"/>, the effects in <see cref="Shot"/>. Every number here is a PLACEHOLDER to tune in play.
///
/// <para>A shot is a PERMANENT number on Khalid's body for the rest of the run. Each purchase raises it one rank (each
/// rank shows in its colour — grey → green → blue → purple → gold) and costs more than the last, until it's MAXED. The
/// whole catalog is always on sale.</para>
/// </summary>
public static class NeedlePoint
{
    /// <summary>Every shot, in the order the stall lists them. Values per rank (index 0 = rank I), then rank I's Lira price.
    /// Priced by worth: mobility is cheap, damage is an investment (Attack Damage multiplies everything you do). For
    /// scale — the round curve pays roughly 140 Lira by the end of round 5 and 560 by round 10.</summary>
    public static readonly ShotDef[] SHOTS =
    {
        new(ShotIds.Dash, "Extra Dash", "+{0} dash", ShotStat.Dashes, new[] { 1f, 2f, 3f }, 30),
        new(ShotIds.AirJump, "Extra Jump", "+{0} air jump", ShotStat.AirJumps, new[] { 1f, 2f, 3f }, 25),
        new(ShotIds.JumpHeight, "Jump Height", "+{0} jump height", ShotStat.JumpHeight,
            new[] { 1.15f, 1.30f, 1.45f, 1.60f, 1.80f }, 15),
        new(ShotIds.RunSpeed, "Run Speed", "+{0} run speed", ShotStat.RunSpeed,
            new[] { 1.20f, 1.30f, 1.40f, 1.50f, 1.60f }, 15),
        new(ShotIds.Reach, "Reach", "+{0} attack reach", ShotStat.Reach,
            new[] { 1.25f, 1.50f, 1.75f, 2.00f, 2.50f }, 30),
        new(ShotIds.AttackDamage, "Attack Damage", "+{0} attack damage", ShotStat.AttackDamage,
            new[] { 1.15f, 1.25f, 1.35f, 1.50f, 1.65f }, 60),
        new(ShotIds.SlamDamage, "Slam Damage", "+{0} slam damage", ShotStat.SlamDamage,
            new[] { 1.20f, 1.35f, 1.50f, 1.70f, 2.00f }, 30),
    };

    private static readonly Dictionary<string, ShotDef> ById = SHOTS.ToDictionary(d => d.Id); // after SHOTS: init order

    /// <summary>The shot with this <see cref="ShotIds"/> id.</summary>
    public static ShotDef Get(string id) => ById[id];

    /// <summary>Each rank bought multiplies the price of the next by this.</summary>
    public const float PriceGrowth = 1.6f;

    /// <summary>The rank colours (index 0 = rank I). Beyond the table, the last colour repeats.</summary>
    public static readonly Color[] RANK_COLORS =
    {
        new(0.62f, 0.62f, 0.66f), // grey
        new(0.35f, 0.85f, 0.45f), // green
        new(0.30f, 0.60f, 1.00f), // blue
        new(0.70f, 0.35f, 1.00f), // purple
        new(1.00f, 0.78f, 0.25f), // gold
    };

    /// <summary>The Lira price of buying <paramref name="def"/>'s next rank when <paramref name="owned"/> ranks are held.</summary>
    public static int PriceOfNext(ShotDef def, int owned) => Mathf.RoundToInt(def.Price * Mathf.Pow(PriceGrowth, owned));

    /// <summary>The colour of rank <paramref name="rank"/> (1 = rank I).</summary>
    public static Color RankColor(int rank) => RANK_COLORS[Mathf.Clamp(rank - 1, 0, RANK_COLORS.Length - 1)];
}
