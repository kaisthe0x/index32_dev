namespace MyGame;

/// <summary>
/// An item the lottery stall landed on, held on Khalid as a <see cref="Passive"/> while it lasts (an
/// <see cref="LotteryMeasure.Instant"/> one happens in <see cref="Passive.Setup"/> and is never held). The run's
/// <see cref="LotteryLedger"/> owns the held ones: it counts their kills down, adds a duplicate's <see cref="Def"/>
/// count to <see cref="Remaining"/> instead of stacking the effect, and removes them when they run out.
/// </summary>
public abstract partial class LotteryEffect : Passive
{
    public readonly LotteryItemDef Def;

    /// <summary>Kills / uses left (a primed item: 1 until it fires). Shown in the buff list.</summary>
    public int Remaining { get; private set; }

    /// <summary>Set by the ledger: a USE-spent item ran out inside one of the player's hooks. The ledger removes it after
    /// that hook — removing a passive while the player is looping over them is not allowed.</summary>
    public System.Action<LotteryEffect>? SpentOut;

    protected LotteryEffect(LotteryItemDef def)
    {
        Id = def.Id;
        Def = def;
        Remaining = def.Count;
    }

    /// <summary>Would this item do something if it landed now? The wheel leaves out the ones that wouldn't.</summary>
    public virtual bool IsUseful(Player player) => true;

    /// <summary>What every kill's Lira is multiplied by while this item lasts.</summary>
    public virtual int LiraMultiplier => 1;

    /// <summary>A kill happened (any enemy) while this item is held — before the ledger counts it down.</summary>
    public virtual void OnKill(Player player) { }

    /// <summary>A duplicate landed: this one lasts its count again — longer, never stronger.</summary>
    public void Extend() => Remaining += Def.Count;

    /// <summary>One kill passed (a <see cref="LotteryMeasure.Kills"/> item). True when it has run out.</summary>
    public bool CountKill() => --Remaining <= 0;

    /// <summary>Spend one use (a <see cref="LotteryMeasure.Uses"/> or primed item) from inside a hook.</summary>
    protected void SpendUse()
    {
        if (--Remaining <= 0)
            SpentOut?.Invoke(this);
    }
}
