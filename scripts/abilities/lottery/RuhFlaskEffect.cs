namespace MyGame;

/// <summary>Ruh Flask: <see cref="LotteryItemDef.Value"/> Ruh charges at once (up to the cap). Off the wheel while his
/// Ruh is full — it would do nothing.</summary>
public partial class RuhFlaskEffect : LotteryEffect
{
    public RuhFlaskEffect(LotteryItemDef def) : base(def) { }

    public override bool IsUseful(Player player) => player.Ruh < player.RuhCap;

    public override void Setup(Player player) => player.Ruh += Def.Value * Player.RuhPerBlock;
}
