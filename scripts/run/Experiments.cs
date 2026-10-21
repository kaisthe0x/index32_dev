using System;

namespace MyGame;

/// <summary>
/// Attaches every experiment compiled into this build (rule C8: <c>experiments/</c>, switched on in mygamedev.csproj's
/// <c>EnabledExperiments</c>, never in a release export). Found by reflection on purpose: game code can't list them,
/// because a list would stop compiling the moment one is switched off.
/// </summary>
public static class Experiments
{
    /// <summary>Create each compiled-in <see cref="IExperiment"/> and attach it to the run. Called once by
    /// RunManager.</summary>
    public static void AttachAll(ExperimentHost host)
    {
        foreach (var type in typeof(Experiments).Assembly.GetTypes())
            if (type.IsClass && !type.IsAbstract && typeof(IExperiment).IsAssignableFrom(type))
                ((IExperiment)Activator.CreateInstance(type)!).Attach(host);
    }
}
