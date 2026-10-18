using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGame;

/// <summary>
/// The LOTTERY STALL — what it costs and what its wheel holds (docs/game-loop.md § The lottery stall). Pure data; the
/// rules live in <see cref="LotteryLedger"/>, the items' effects in the <see cref="LotteryEffect"/> classes. Every
/// number here is a PLACEHOLDER to tune in play.
///
/// <para>Pay first, then spin: E pays <see cref="PullPrice"/> at once, the orb spins <see cref="SpinTime"/> seconds and
/// lands on a random item, which takes effect immediately — nothing is carried. The wheel only ever holds items that
/// would do something right now (<see cref="LotteryEffect.IsUseful"/>). Items last a number of KILLS or USES, never
/// "this round" (rounds don't pause, so a timed item pulled late would be wasted), and a duplicate makes an item last
/// longer, never stronger.</para>
/// </summary>
public static class Lottery
{
    /// <summary>The first pull of a round costs <see cref="BasePrice"/> Lira; each further pull that round costs
    /// <see cref="PriceStep"/> more (20 → 30 → 40 → 50), and the price resets when a round starts.</summary>
    public const int BasePrice = 20;
    public const int PriceStep = 10;

    /// <summary>Pulls allowed per round — the hard brake that stops pulling until a heal lands.</summary>
    public const int PullsPerRound = 4;

    /// <summary>Seconds the orb spins before it lands (the item was decided and paid for at the press).</summary>
    public const float SpinTime = 1.0f;

    /// <summary>The heal items' total weight is at most this fraction of the other items' weight on any one spin — so
    /// a heal is at most 1 in 5, however many items the wheel had to leave out.</summary>
    public const float HealShare = 0.25f;

    /// <summary>Triggered healing per round (half-blocks) from every "when X, heal" source together — the lottery's
    /// heal items and the Figmenter's lifesteal buffs: 4 = 2 blocks. It refills when a round starts.</summary>
    public const float HealCapPerRound = 4.0f;

    /// <summary>What one heal trigger restores (half-blocks): Leech Draught's proc, Tithe's every-Nth kill.</summary>
    public const float HealPerTrigger = 1.0f;

    /// <summary>Fig Frenzy's pull radius for loose figs (px) while it lasts.</summary>
    public const float FigFrenzyMagnetRange = 400.0f;

    /// <summary>The wheel (a description's <c>{0}</c> is its <c>Value</c> — <see cref="LotteryItemDef.Text"/>).
    /// <c>Count</c> is how long an item lasts in its <c>Measure</c>; <c>Value</c> per item: Ruh Flask = Ruh charges
    /// added; Fig Frenzy = added fig drop chance; Bounty = Lira multiplier; Wider Pull = extra Come Closer targets; Crimson Vortex = unused (its damage is the vortex scene's);
    /// Leech Draught = heal chance per landed hit; Tithe = every Nth kill heals; Second Wind = half-blocks healed.
    /// Weights: 4 for an ordinary item, 1 for a heal (then capped by <see cref="HealShare"/>).</summary>
    public static readonly LotteryItemDef[] Items =
    {
        new(LotteryIds.RuhFlask, "Ruh Flask", "+{0} Ruh charges.", LotteryMeasure.Instant, 1, 4, false, 2.0f),
        new(LotteryIds.CooldownFlush, "Cooldown Flush", "Your next specials skip their cooldown.",
            LotteryMeasure.Uses, 3, 4, false, 0.0f),
        new(LotteryIds.FigFrenzy, "Fig Frenzy", "Figs drop more often and fly to you.",
            LotteryMeasure.Kills, 25, 4, false, 0.15f),
        new(LotteryIds.Bounty, "Bounty", "Kills pay ×{0} Lira.", LotteryMeasure.Kills, 25, 4, false, 2.0f),
        new(LotteryIds.WiderPull, "Wider Pull", "Come Closer pulls {0} more enemies.", LotteryMeasure.Kills, 25, 4,
            false, 2.0f, SpecialIds.ComeCloser),
        new(LotteryIds.CrimsonVortex, "Crimson Vortex", "Your dashes leave a vortex that hits for {0}.",
            LotteryMeasure.Kills, 25, 4, false, 18.0f), // Value = the vortex's damage per enemy
        new(LotteryIds.LeechDraught, "Leech Draught", "Landed hits may heal half a block.",
            LotteryMeasure.Kills, 30, 1, true, 0.10f),
        new(LotteryIds.Tithe, "Tithe", "Every {0}th kill heals half a block.", LotteryMeasure.Kills, 8, 1, true, 4.0f),
        new(LotteryIds.SecondWind, "Second Wind", "At your last half-block, heal {0} half-blocks. Once.",
            LotteryMeasure.Primed, 1, 1, true, 2.0f),
    };

    /// <summary>Builds the effect of each item (same keys as <see cref="Items"/>).</summary>
    public static readonly Dictionary<string, Func<LotteryItemDef, LotteryEffect>> Factories = new()
    {
        [LotteryIds.RuhFlask] = d => new RuhFlaskEffect(d),
        [LotteryIds.CooldownFlush] = d => new CooldownFlushEffect(d),
        [LotteryIds.FigFrenzy] = d => new FigFrenzyEffect(d),
        [LotteryIds.Bounty] = d => new BountyEffect(d),
        [LotteryIds.WiderPull] = d => new WiderPullEffect(d),
        [LotteryIds.CrimsonVortex] = d => new CrimsonVortexEffect(d),
        [LotteryIds.LeechDraught] = d => new LeechDraughtEffect(d),
        [LotteryIds.Tithe] = d => new TitheEffect(d),
        [LotteryIds.SecondWind] = d => new SecondWindEffect(d),
    };

    private static readonly Dictionary<string, LotteryItemDef> ById = Items.ToDictionary(d => d.Id); // after Items: init order

    /// <summary>The item with this <see cref="LotteryIds"/> id.</summary>
    public static LotteryItemDef Get(string id) => ById[id];
}
