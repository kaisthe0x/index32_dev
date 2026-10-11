namespace MyGame;

/// <summary>What the Figmenter is doing (<see cref="FigmenterLedger.State"/>).</summary>
public enum BrewState
{
    Empty,    // nothing in it — figs start a brew
    Brewing,  // filling up, a step per round cleared
    Ready,    // a finished wine is waiting to be collected
}
