using Godot;

namespace MyGame;

/// <summary>
/// A spawned effect that can have the resolved <see cref="SegmentData"/> tuning injected into its hitbox at spawn
/// (implemented by <c>Strike</c> + <c>Projectile</c>). Lets the generic spawners (<c>ParticleDirector</c>, <c>Enemy</c>)
/// feed combat numbers typed instead of via a dynamic <c>Call("apply_tuning", …)</c>.
/// </summary>
public interface ITunable
{
    void ApplyTuning(SegmentData t, Node source);
}
