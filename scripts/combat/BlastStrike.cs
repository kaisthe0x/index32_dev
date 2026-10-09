using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// A CHANNELLED strike: its emitters emit CONTINUOUSLY for <see cref="emit_duration"/>, and while they do the
/// caster's animation is HELD on the cast frame (the caster can cancel via interrupt). Tarri's blast, Khalid's
/// crimson vortex. The channel knobs live HERE — Enemy/Player detect a channel with <c>is BlastStrike</c> and
/// read <see cref="emit_duration"/>/<see cref="interrupt_on_hurt"/> off it.
/// </summary>
[GlobalClass]
public partial class BlastStrike : Strike
{
    /// <summary>Continuous emission window (s). While it plays, the striker's anim is held on its current frame.</summary>
    [Export] public float emit_duration { get; set; }
    /// <summary>Whether the caster being HIT cancels the channel (the norm). false = an uninterruptible channel.</summary>
    [Export] public bool interrupt_on_hurt { get; set; } = true;

    /// <summary>Free-delay extended by the continuous-emission window, so the stream isn't cut mid-emit.</summary>
    protected override float FreeDelay()
    {
        float emitWindow = Mathf.Max(emit_duration, 0.0f);
        float freeDelay = Mathf.Max(lifetime, emitWindow);
        foreach (var em in Emitters())
            freeDelay = Mathf.Max(freeDelay, emitWindow + EmitterLife(em));
        return freeDelay;
    }

    /// <summary>Hold the striker's pose for the emission window (option A: the strike drives its wielder).</summary>
    protected override void OnTuningApplied(SegmentData t)
    {
        // Pass `this` so the caster can cancel us.
        if (emit_duration > 0.0f && source is IStrikeWielder wielder)
            wielder.HoldAnimation(emit_duration, this);
    }
}
