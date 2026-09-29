using System.Collections.Generic;
using System.Linq;

namespace MyGame;

/// <summary>
/// DEKKEN — the perk shop's catalog (docs/game-loop.md § Economy). Pure data; the rules live in <see cref="PerkLedger"/>,
/// the timed/whole-run effects in <see cref="Perk"/>. Every number here is a PLACEHOLDER to tune in play.
///
/// <para>Perks are UTILITY / TACTICS (not stats — those are Needle Point shots): each break, Dekken stocks
/// <see cref="StockSize"/> of these at random; bought in the break, a perk is active at once.</para>
/// </summary>
public static class Dekken
{
    /// <summary>How many perks the shop offers each break.</summary>
    public const int StockSize = 5;

    /// <summary>The perk pool. <c>Value</c> per perk: Heal = half-blocks restored; Fig Chance = added drop chance;
    /// Magnet = pull radius (px); Shield = hits blocked per round; Wider Pull = extra Come Closer targets.</summary>
    public static readonly PerkDef[] PERKS =
    {
        new(PerkIds.Heal, "Heal", "Restore a health block.", PerkDuration.OneUse, 0, 20, 2f),
        new(PerkIds.FastTravel, "Fast Travel", "Teleport to the mystery box.", PerkDuration.OneUse, 0, 20, 0f),
        new(PerkIds.FigChance, "Fig Chance", "Enemies 5% likelier to drop figs.", PerkDuration.Run, 0, 40, 0.05f),
        new(PerkIds.Magnet, "Magnet", "Nearby figs fly to you.", PerkDuration.Rounds, 1, 15, 400f),
        new(PerkIds.Shield, "Shield", "Blocks your first hit each round.", PerkDuration.Rounds, 1, 25, 1f),
        new(PerkIds.Prepared, "Prepared", "Free surge as each round starts.", PerkDuration.Rounds, 1, 25, 0f),
        new(PerkIds.WiderPull, "Wider Pull", "Come Closer pulls 2 more enemies.", PerkDuration.Rounds, 1, 15, 2f,
            SpecialIds.ComeCloser),
    };

    private static readonly Dictionary<string, PerkDef> ById = PERKS.ToDictionary(d => d.Id); // after PERKS: init order

    /// <summary>The perk with this <see cref="PerkIds"/> id.</summary>
    public static PerkDef Get(string id) => ById[id];
}
