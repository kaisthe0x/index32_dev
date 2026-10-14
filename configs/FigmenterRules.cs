using Godot;

namespace MyGame;

/// <summary>
/// The FIGMENTER's rules (docs/game-loop.md § Economy) — PURE DATA; the run state lives in
/// <see cref="FigmenterLedger"/>, the machine itself in <see cref="Figmenter"/>, the carried bottle's key and HUD slot
/// in <see cref="WineControls"/>, and what a wine can hold in <see cref="BuffCatalog"/>. Every number here is a
/// PLACEHOLDER to tune in play.
///
/// <para>The Figmenter turns Fada Figs into FIG WINE. <see cref="Cost"/> figs start a brew; it takes
/// <see cref="BrewRounds"/> cleared rounds, the tank filling a step each round. The finished wine holds one permanent
/// buff the player doesn't own yet (or, at <see cref="SpecialChance"/>, a special-swap) and is labelled with it. He
/// collects it into his one wine slot and can drink it at any moment — or carry it: every <see cref="AgeRounds"/>
/// rounds carried it climbs one <see cref="WineRarity"/>, and the buff comes out that much stronger.</para>
/// </summary>
public static class FigmenterRules
{
    public const int Cost = 4;                  // figs per brew (figs are rare — ~15 % of kills → first brew ~round 3; 8 until 2026-10-11)
    public const int BrewRounds = 2;            // rounds cleared before the wine is ready (paid in round 3 → ready as round 5 starts; 5 until 2026-10-11)
    public const int AgeRounds = 2;             // rounds carried per rarity step: Common → Epic takes 8 rounds in the pocket
    public const float SpecialChance = 0.06f;   // a brew makes a special-swap instead of a buff

    /// <summary>The specials only the Figmenter gives (never one already equipped).</summary>
    public static readonly string[] Specials = { SpecialIds.Zahluq, SpecialIds.Bakshen };

    /// <summary>A rarity's colour (label, HUD slot, buff list), indexed by <see cref="WineRarity"/>.</summary>
    private static readonly Color[] RarityColors =
    {
        new(0.80f, 0.80f, 0.84f), // Common — plain
        new(0.30f, 0.60f, 1.00f), // Rare — blue
        new(1.00f, 0.60f, 0.20f), // Hot — orange
        new(0.70f, 0.35f, 1.00f), // Sensational — purple
        new(1.00f, 0.25f, 0.25f), // Epic — red
    };

    public static Color RarityColor(WineRarity rarity) => RarityColors[(int)rarity];
}
