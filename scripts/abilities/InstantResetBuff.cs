using Godot;

namespace MyGame;

/// <summary>
/// Zahluq Instant Reset: whiffing (a zero-victim swing) fully clears the cooldown, so a missed lunge can be re-thrown
/// at once. Fires off <see cref="Trigger.OnMiss"/> (emitted by <see cref="Hitbox.deactivate"/> when a player box
/// connects with nobody). Offer-gated to Zahluq (<see cref="Buff.AppliesTo"/>), which fires a single hitbox per swing
/// so one whiff = one reset. Built by <see cref="BuffCatalog"/>.
///
/// PARKED (like the other per-move buffs): the run flow only offers <see cref="BuffCatalog.MildIds"/>/<see
/// cref="BuffCatalog.PowerfulIds"/>, which exclude move-gated buffs, so this is never offered yet. NOTE for a future
/// per-move-offer pass — Zahluq is now a *special*, so it resets the SPECIAL cooldown; before it can fire, special-box
/// whiffs still need to emit OnMiss (<see cref="Hitbox.deactivate"/> currently gates OnMiss to <c>!from_special</c>).
/// </summary>
public partial class InstantResetBuff : Buff
{
    private const float FullReset = 9999.0f; // huge subtraction → cooldown clamps to zero (reduce_special_cooldown)

    public InstantResetBuff(string id)
    {
        Id = id;
        Trigger = Trigger.OnMiss;
    }

    public override void OnMiss(Player p) => p.reduce_special_cooldown(FullReset);
}
