namespace MyGame;

/// <summary>Second Wind: primed until a hit leaves him on his last half-block, then heals
/// <see cref="LotteryItemDef.Value"/> half-blocks and is spent. The one heal that helps a cornered player who lands no
/// hits — so it is not counted against the <see cref="HealBudget"/> (an emergency heal that could be capped would fail
/// exactly when it matters). One at a time: a primed one keeps another off the wheel.</summary>
public partial class SecondWindEffect : LotteryEffect
{
    private const float LastHalfBlock = 1.0f;

    public SecondWindEffect(LotteryItemDef def) : base(def) { }

    public override void OnHurt(Player player, Hit hit)
    {
        if (player.Health > LastHalfBlock)
            return;
        player.Heal(Def.Value);
        SpendUse();
    }
}
