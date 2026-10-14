namespace MyGame;

/// <summary>What a <see cref="Wine"/> does when it is drunk (decided by <see cref="FigmenterLedger"/> as the brew starts).</summary>
public enum WineKind
{
    Buff,     // a build-defining buff, permanent for the run, as strong as the wine's rarity
    Special,  // (rare) a special-swap: drinking it replaces the equipped special; rarity changes nothing
}
