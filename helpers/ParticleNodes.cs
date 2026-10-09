using Godot;

namespace MyGame;

/// <summary>
/// Typed access to the two engine particle node classes. <see cref="CpuParticles2D"/> and <see cref="GpuParticles2D"/>
/// share property names but no common typed base, so anything that drives "an emitter" without caring which kind
/// goes through here instead of setting properties by name.
/// </summary>
public static class ParticleNodes
{
    /// <summary>Start or stop emission on a particle node of either kind (nothing happens for any other node).</summary>
    public static void SetEmitting(Node emitter, bool on)
    {
        if (emitter is CpuParticles2D cpu)
            cpu.Emitting = on;
        else if (emitter is GpuParticles2D gpu)
            gpu.Emitting = on;
    }

    /// <summary>Set whether a particle node of either kind plays once or keeps emitting.</summary>
    public static void SetOneShot(Node emitter, bool on)
    {
        if (emitter is CpuParticles2D cpu)
            cpu.OneShot = on;
        else if (emitter is GpuParticles2D gpu)
            gpu.OneShot = on;
    }

    /// <summary>Run <paramref name="handler"/> when a particle node of either kind finishes a one-shot emission.</summary>
    public static void OnFinished(Node emitter, System.Action handler)
    {
        if (emitter is CpuParticles2D cpu)
            cpu.Finished += handler;
        else if (emitter is GpuParticles2D gpu)
            gpu.Finished += handler;
    }
}
