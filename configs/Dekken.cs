using System.Collections.Generic;
using System.Linq;

namespace MyGame;

/// <summary>
/// DEKKEN — the perk shop's catalog (docs/game-loop.md § Economy). Pure data; the rules live in <see cref="PerkLedger"/>,
/// the timed/whole-run effects in <see cref="Perk"/>. Every number here is a PLACEHOLDER to tune in play.
///
/// <para>Perks are UTILITY / TACTICS (not stats — those are Needle Point shots), sold as VIALS: each round, Dekken
/// stocks <see cref="StockSize"/> of these at random. A vial is either DRUNK at the machine (its perk happens / starts
/// at once) or KEPT — carried, up to <see cref="CarrySlots"/> at a time and never two of the same — and drunk later
/// with one key press, instantly. A whole-run perk can only be drunk at the machine (there's nothing to save it for).
/// A timed perk's rounds start when it's DRUNK and count that round — so 2 = "this round and the next" (drunk
/// mid-round, 1 could end seconds later, and Prepared — which fires at a round's start — would never fire).</para>
/// </summary>
public static class Dekken
{
    /// <summary>How many perks the shop offers each round.</summary>
    public const int StockSize = 5;

    /// <summary>How many vials the player can carry (one of each kind at most).</summary>
    public const int CarrySlots = 2;

    /// <summary>The perk pool. <c>Value</c> per perk: Heal = half-blocks restored; Fig Chance = added drop chance;
    /// Magnet = pull radius (px); Shield = hits blocked per round; Wider Pull = extra Come Closer targets.</summary>
    public static readonly PerkDef[] PERKS =
    {
        new(PerkIds.Heal, "Heal", "Restore a health block.", PerkDuration.OneUse, 0, 20, 2f),
        new(PerkIds.FastTravel, "Fast Travel", "Teleport to the mystery box.", PerkDuration.OneUse, 0, 20, 0f),
        new(PerkIds.FigChance, "Fig Chance", "Enemies 5% likelier to drop figs.", PerkDuration.Run, 0, 40, 0.05f),
        new(PerkIds.Magnet, "Magnet", "Nearby figs fly to you.", PerkDuration.Rounds, 2, 15, 400f),
        new(PerkIds.Shield, "Shield", "Blocks your first hit each round.", PerkDuration.Rounds, 2, 25, 1f),
        new(PerkIds.Prepared, "Prepared", "Free surge as each round starts.", PerkDuration.Rounds, 2, 25, 0f),
        new(PerkIds.WiderPull, "Wider Pull", "Come Closer pulls 2 more enemies.", PerkDuration.Rounds, 2, 15, 2f,
            SpecialIds.ComeCloser),
    };

    private static readonly Dictionary<string, PerkDef> ById = PERKS.ToDictionary(d => d.Id); // after PERKS: init order

    /// <summary>The perk with this <see cref="PerkIds"/> id.</summary>
    public static PerkDef Get(string id) => ById[id];
}
