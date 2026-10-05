namespace MyGame;

/// <summary>What a mystery-box spin came up as (<see cref="BoxRoll"/>, rolled by <see cref="BoxLedger"/>).</summary>
public enum BoxOutcome
{
    Buff,     // a permanent build-defining buff from the box pool
    Special,  // (extreme rarity) a special-swap: taking it replaces the equipped special
    Teddy,    // the teddy bear: the figs come back and the box relocates
}
