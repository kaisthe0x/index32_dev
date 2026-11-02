namespace MyGame;

/// <summary>Bounty: while it lasts, every kill's Lira is multiplied by <see cref="LotteryItemDef.Value"/>. A second
/// Bounty makes it last longer, not pay ×4 — doubled Lira buying more pulls would run away.</summary>
public partial class BountyEffect : LotteryEffect
{
    public BountyEffect(LotteryItemDef def) : base(def) { }

    public override int LiraMultiplier => (int)Def.Value;
}
