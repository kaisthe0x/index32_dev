namespace MyGame;

/// <summary>Wider Pull: while it lasts, Come Closer pulls <see cref="LotteryItemDef.Value"/> more enemies (only on the
/// wheel while Come Closer is equipped — <see cref="LotteryItemDef.RequiresSpecial"/>). It lasts KILLS, not casts: the
/// pull reads the bonus when its field spawns, after the cast, so a cast-counted one would end before its last pull.</summary>
public partial class WiderPullEffect : LotteryEffect
{
    public WiderPullEffect(LotteryItemDef def) : base(def) { }

    public override void Setup(Player player) => player.MagnetTargetBonus += (int)Def.Value;

    public override void Teardown(Player player) => player.MagnetTargetBonus -= (int)Def.Value;
}
