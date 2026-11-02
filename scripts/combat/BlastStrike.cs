using Godot;

namespace MyGame;

/// <summary>
/// A CHANNELLED strike: its emitters emit CONTINUOUSLY for <see cref="EmitDuration"/>. Tarri's and the Ventilator's
/// blasts (the enemy channels it, and a hit cancels it — <see cref="InterruptOnHurt"/>, read by Enemy), and the
/// crimson vortex a Khalid dash leaves behind (he moves on; it spins out on its own).
/// </summary>
[GlobalClass]
public partial class BlastStrike : Strike
{
    /// <summary>Continuous emission window (s).</summary>
    [Export] public float EmitDuration { get; set; }
    /// <summary>Whether an enemy caster being HIT cancels the channel (the norm). false = an uninterruptible channel.</summary>
    [Export] public bool InterruptOnHurt { get; set; } = true;

    /// <summary>Free-delay extended by the continuous-emission window, so the stream isn't cut mid-emit.</summary>
    protected override float FreeDelay()
    {
        float emitWindow = Mathf.Max(EmitDuration, 0.0f);
        float freeDelay = Mathf.Max(Lifetime, emitWindow);
        foreach (var em in Emitters())
            freeDelay = Mathf.Max(freeDelay, emitWindow + EmitterLife(em));
        return freeDelay;
    }
}
