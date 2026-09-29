using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MyGame;

/// <summary>
/// NEEDLE POINT — the stat stall's catalog + economy (docs/game-loop.md § Economy). Pure data; the rules live in
/// <see cref="ShotLedger"/>, the effects in <see cref="Shot"/>. Every number here is a PLACEHOLDER to tune in play.
///
/// <para>A shot is a number on Khalid's body for a few rounds. It's bought with Lira; fig UPGRADES raise its level for
/// the rest of the run (each level shows in its colour — grey → green → blue → purple → gold) and its Lira price with
/// it. The whole catalog is always on sale (no rotation), so an upgrade is never wasted.</para>
/// </summary>
public static class NeedlePoint
{
    /// <summary>Every shot, in the order the stall lists them. Values per level: index 0 = the base (grey).</summary>
    public static readonly ShotDef[] SHOTS =
    {
        new(ShotIds.Dash, "Extra Dash", "+{0} dash", ShotStat.Dashes, new[] { 1f, 2f, 3f }, 1, 10),
        new(ShotIds.AirJump, "Extra Jump", "+{0} air jump", ShotStat.AirJumps, new[] { 1f, 2f, 3f }, 1, 10),
        new(ShotIds.JumpHeight, "Jump Height", "+{0} jump height", ShotStat.JumpHeight,
            new[] { 1.15f, 1.30f, 1.45f, 1.60f, 1.80f }, 1, 10),
        new(ShotIds.RunSpeed, "Run Speed", "+{0} run speed", ShotStat.RunSpeed,
            new[] { 1.20f, 1.30f, 1.40f, 1.50f, 1.60f }, 1, 10),
        new(ShotIds.Reach, "Reach", "+{0} attack reach", ShotStat.Reach,
            new[] { 1.25f, 1.50f, 1.75f, 2.00f, 2.50f }, 1, 10),
        new(ShotIds.AttackDamage, "Attack Damage", "+{0} attack damage", ShotStat.AttackDamage,
            new[] { 1.15f, 1.25f, 1.35f, 1.50f, 1.65f }, 1, 10),
        new(ShotIds.SlamDamage, "Slam Damage", "+{0} slam damage", ShotStat.SlamDamage,
            new[] { 1.20f, 1.35f, 1.50f, 1.70f, 2.00f }, 1, 10),
    };

    private static readonly Dictionary<string, ShotDef> ById = SHOTS.ToDictionary(d => d.Id); // after SHOTS: init order

    /// <summary>The shot with this <see cref="ShotIds"/> id.</summary>
    public static ShotDef Get(string id) => ById[id];

    /// <summary>Each upgrade level multiplies a shot's Lira price by this.</summary>
    public const float LevelPriceGrowth = 1.5f;

    /// <summary>Figs to upgrade INTO level i+1 (index 0 = the cost of level 1). A shot's max level is capped by both
    /// its own <see cref="ShotDef.Levels"/> and this table.</summary>
    public static readonly int[] UPGRADE_FIGS = { 3, 5, 8, 12 };

    /// <summary>The level colours (index = level). Beyond the table, the last colour repeats.</summary>
    public static readonly Color[] LEVEL_COLORS =
    {
        new(0.62f, 0.62f, 0.66f), // grey
        new(0.35f, 0.85f, 0.45f), // green
        new(0.30f, 0.60f, 1.00f), // blue
        new(0.70f, 0.35f, 1.00f), // purple
        new(1.00f, 0.78f, 0.25f), // gold
    };

    /// <summary>The Lira price of a shot at <paramref name="level"/>.</summary>
    public static int PriceAt(ShotDef def, int level) => Mathf.RoundToInt(def.Price * Mathf.Pow(LevelPriceGrowth, level));

    /// <summary>The highest level <paramref name="def"/> can reach.</summary>
    public static int MaxLevel(ShotDef def) => Mathf.Min(def.MaxLevel, UPGRADE_FIGS.Length);

    /// <summary>Figs to upgrade from <paramref name="level"/> to the next (only valid below <see cref="MaxLevel"/>).</summary>
    public static int UpgradeFigs(int level) => UPGRADE_FIGS[level];

    public static Color LevelColor(int level) => LEVEL_COLORS[Mathf.Clamp(level, 0, LEVEL_COLORS.Length - 1)];
}
