using Godot;

namespace MyGame;

/// <summary>
/// How much TRIGGERED healing Khalid may still get this round — the "when X, heal" sources (the lottery's heal items,
/// the Figmenter's lifesteal buffs) share <see cref="Lottery.HealCapPerRound"/> half-blocks a round, so stacking them
/// can't erase the scarcity of health. Chosen heals (drinking the wine, the Nem surge) and the one-shot Second Wind
/// are not counted. Owned by <see cref="Player"/>; refilled when a round starts and when a run begins.
/// </summary>
public sealed class HealBudget
{
    private float _left = Lottery.HealCapPerRound;

    /// <summary>A round began (or a run): the full budget is back.</summary>
    public void Refill() => _left = Lottery.HealCapPerRound;

    /// <summary>Take up to <paramref name="want"/> half-blocks from the budget; returns how much was granted.</summary>
    public float Take(float want)
    {
        float granted = Mathf.Min(want, _left);
        _left -= granted;
        return granted;
    }
}
