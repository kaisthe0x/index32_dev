namespace MyGame;

/// <summary>Cooldown Flush: the next <see cref="LotteryItemDef.Count"/> specials skip their cooldown. The special's
/// cooldown is set just before <see cref="Passive.OnSpecialCast"/> fires, so clearing it there is what sticks.</summary>
public partial class CooldownFlushEffect : LotteryEffect
{
    private const float WholeCooldown = float.PositiveInfinity; // ReduceSpecialCooldown clamps the result to zero

    public CooldownFlushEffect(LotteryItemDef def) : base(def) { }

    public override void OnSpecialCast(Player player, Action action)
    {
        player.ReduceSpecialCooldown(WholeCooldown);
        SpendUse();
    }
}
