using Godot;

namespace MyGame;

/// <summary>
/// A region that RECEIVES hits. It doesn't scan for anything; opposing Hitboxes scan for it (their mask
/// includes this box's layer). On a hit it just relays the <see cref="Hit"/> via a signal — the owning body
/// decides what to do. C# port of <c>scripts/combat/hurtbox.gd</c>.
///
/// Set <c>CollisionLayer</c> to the team's hurt layer (Combat.*Hurt) and leave <c>CollisionMask</c> at 0;
/// <c>Monitorable</c> must stay true (default) so hitboxes see it.
/// </summary>
[GlobalClass]
public partial class Hurtbox : Area2D
{
    [Signal]
    public delegate void HurtEventHandler(Hit hit);

    public void TakeHit(Hit hit) => EmitSignal(SignalName.Hurt, hit);
}
