namespace MyGame;

/// <summary>
/// One lottery item: how it is spent (<paramref name="Measure"/> — <paramref name="Count"/> kills / uses / charges),
/// its <paramref name="Weight"/> on the wheel, whether it <paramref name="Heals"/> (heal items share a capped slice of
/// the wheel — <see cref="Lottery.HealShare"/>), and its one tuning number <paramref name="Value"/> (what it
/// means is per item — see <see cref="Lottery.Items"/>). <paramref name="RequiresSpecial"/> keeps it off the wheel
/// unless that special is equipped; null = always eligible.
/// </summary>
public sealed record LotteryItemDef(string Id, string Name, string Description, LotteryMeasure Measure, int Count,
    int Weight, bool Heals, float Value, string? RequiresSpecial = null)
{
    /// <summary>The description with its <c>{0}</c> filled from <see cref="Value"/>, so the number is written once.</summary>
    public string Text => string.Format(Description, Value);
}
