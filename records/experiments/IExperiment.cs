namespace MyGame;

/// <summary>
/// A dev-only gameplay idea under <c>experiments/</c> (docs/standards.md, rule C8). It is compiled only when named in
/// <c>EnabledExperiments</c> (mygamedev.csproj) and never in a release export, so game code can't name it: the run
/// finds whichever are compiled in (<see cref="Experiments.AttachAll"/>) and hands each the
/// <see cref="ExperimentHost"/>. Needs a public parameterless constructor.
/// </summary>
public interface IExperiment
{
    /// <summary>Join the run: everything the experiment may touch comes through <paramref name="host"/>.</summary>
    void Attach(ExperimentHost host);
}
