namespace MyGame;

/// <summary>
/// The MYSTERY BOX's rules (docs/game-loop.md § Economy) — PURE DATA; the run state + rolls live in
/// <see cref="BoxLedger"/>, the box itself (the spin, the offer, relocating) in <see cref="MysteryBox"/>, and what it can
/// give in <see cref="BuffCatalog"/>. Every number here is a PLACEHOLDER to tune in play.
///
/// <para>A spin costs <see cref="Cost"/> figs and always gives a result, in REAL TIME (the game doesn't pause): the box
/// spins for <see cref="SpinTime"/>, then holds the result up for <see cref="OfferTime"/> — press E to take it, or leave
/// it and it's gone (the figs are spent either way). The result is one permanent buff the player doesn't own yet, or at
/// <see cref="SpecialChance"/> a special-swap, or at <see cref="TeddyChance"/> the TEDDY BEAR: the figs come back and
/// the box moves to another of the layout's box spots, a hard-to-reach one <see cref="HardSpotChance"/> of the time.</para>
/// </summary>
public static class BoxRules
{
    public const int Cost = 8;                  // figs per spin (figs are rare — ~10 % of kills → first spin ~round 4-5)
    public const float SpinTime = 2.5f;         // the spin, before the result shows
    public const float OfferTime = 8.0f;        // how long the result waits to be taken
    public const float TeddyChance = 1.0f / 8.0f;
    public const float HardSpotChance = 0.4f;   // a relocation lands on a HARD spot this often (when the layout has one)
    public const float SpecialChance = 0.06f;   // a spin offers a special-swap instead of a buff

    /// <summary>The specials only the box gives (never one already equipped).</summary>
    public static readonly string[] Specials = { SpecialIds.Zahluq, SpecialIds.Bakshen };
}
