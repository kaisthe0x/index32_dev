using Godot;

namespace MyGame;

/// <summary>
/// Zahluq Instant Reset: whiffing (a zero-victim swing) fully clears the cooldown, so a missed lunge can be re-thrown
/// at once. Fires off <see cref="Trigger.OnMiss"/> (emitted by <see cref="Hitbox.Deactivate"/> when a player box
/// connects with nobody). Offer-gated to Zahluq (<see cref="Buff.AppliesTo"/>), which fires a single hitbox per swing
/// so one whiff = one reset. Built by <see cref="BuffCatalog"/>.
///
/// PARKED — kept out of the box pool (<see cref="BuffCatalog.Parked"/>): Zahluq is now a *special*, so it resets the
/// SPECIAL cooldown, but special-box whiffs don't emit OnMiss yet (<see cref="Hitbox.Deactivate"/> gates OnMiss to
/// <c>!FromSpecial</c>), so it would never fire. Unpark it once they do.
/// </summary>
public partial class InstantResetBuff : Buff
{
    private const float FullReset = 9999.0f; // huge subtraction → cooldown clamps to zero (ReduceSpecialCooldown)

    public InstantResetBuff(string id)
    {
        Id = id;
        Trigger = Trigger.OnMiss;
    }

    public override void OnMiss(Player p) => p.ReduceSpecialCooldown(FullReset);
}
