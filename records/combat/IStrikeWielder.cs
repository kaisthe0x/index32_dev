using Godot;

namespace MyGame;

/// <summary>
/// What a <see cref="Strike"/> can ask of the body that threw it (its <c>source</c>): the wielder-effects carried in
/// the strike's tuning. <see cref="Player"/> implements it; a source that doesn't (an enemy) simply gets none of them.
/// Replaces the old by-name <c>HasMethod</c> / <c>Call</c> on the source.
/// </summary>
public interface IStrikeWielder
{
    /// <summary>Shove the wielder forward (the way it faces) by <paramref name="impulse"/> px/s — a dash-attack's lunge.</summary>
    void ApplyLunge(float impulse);

    /// <summary>Make the wielder shrug off stagger for <paramref name="duration"/> seconds.</summary>
    void SetArmor(float duration);

    /// <summary>Freeze the wielder's animation on its current frame for <paramref name="duration"/> seconds while
    /// <paramref name="effect"/> channels; the wielder may cancel the effect if it is interrupted.</summary>
    void HoldAnimation(double duration, BlastStrike effect);
}
