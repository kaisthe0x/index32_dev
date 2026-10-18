namespace MyGame;

/// <summary>Tithe: while it lasts, every <see cref="LotteryItemDef.Value"/>th kill heals <see cref="Lottery.HealPerTrigger"/>
/// — a counter the player can plan around, drawing on the round's <see cref="HealBudget"/>.</summary>
public partial class TitheEffect : LotteryEffect
{
    private int _kills; // kills since it landed (a duplicate keeps the count going)

    public TitheEffect(LotteryItemDef def) : base(def) { }

    public override void OnKill(Player player)
    {
        if (++_kills % (int)Def.Value == 0)
            player.HealTriggered(Lottery.HealPerTrigger);
    }
}
