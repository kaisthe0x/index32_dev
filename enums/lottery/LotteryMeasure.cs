namespace MyGame;

/// <summary>How a lottery item is spent (see <see cref="LotteryItemDef.Count"/>) — and so how a duplicate stacks and how
/// the buff list shows what is left.</summary>
public enum LotteryMeasure
{
    Instant, // happens once as the orb lands and is never held; a duplicate simply happens again
    Kills,   // lasts for Count kills; a duplicate adds Count more kills, never a stronger effect
    Uses,    // lasts for Count uses of what it boosts; a duplicate adds Count more uses
    Primed,  // waits for its trigger, then is spent; one at a time (a primed one keeps another off the wheel)
}
