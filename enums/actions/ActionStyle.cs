namespace MyGame;

/// <summary>How an attack plays out — its cadence. Either way, HOLDING the attack button keeps attacking.</summary>
public enum ActionStyle
{
    /// <summary>A combo — each hit plays in turn (a press, or holding, chains the next; the last one loops back).</summary>
    Standard,
    /// <summary>A held multi-hit flurry — one looping animation for as long as the button is held.</summary>
    Flurry,
}
