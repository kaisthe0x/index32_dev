using Godot;

namespace MyGame;

/// <summary>Leech Draught: while it lasts, each hit he LANDS has a <see cref="LotteryItemDef.Value"/> chance to heal
/// <see cref="Lottery.HealPerTrigger"/> — a triggered heal, so it draws on the round's <see cref="HealBudget"/>.</summary>
public partial class LeechDraughtEffect : LotteryEffect
{
    public LeechDraughtEffect(LotteryItemDef def) : base(def) { }

    public override void OnHitDealt(Player player, float amount, Node target)
    {
        if (amount > 0.0f && GD.Randf() < Def.Value)
            player.HealTriggered(Lottery.HealPerTrigger);
    }
}
