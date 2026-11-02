namespace MyGame;

/// <summary>Fig Frenzy: while it lasts, every kill is <see cref="LotteryItemDef.Value"/> likelier to drop a fig, and loose
/// figs within <see cref="Lottery.FigFrenzyMagnetRange"/> fly to him.</summary>
public partial class FigFrenzyEffect : LotteryEffect
{
    public FigFrenzyEffect(LotteryItemDef def) : base(def) { }

    public override void Setup(Player player)
    {
        player.FigChanceBonus += Def.Value;
        player.FigMagnetRange += Lottery.FigFrenzyMagnetRange;
    }

    public override void Teardown(Player player)
    {
        player.FigChanceBonus -= Def.Value;
        player.FigMagnetRange -= Lottery.FigFrenzyMagnetRange;
    }
}
