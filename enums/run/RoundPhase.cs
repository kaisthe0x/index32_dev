namespace MyGame;

/// <summary>Where the endless round loop is (see <see cref="Rounds"/> / <c>docs/game-loop.md</c>).</summary>
public enum RoundPhase
{
    /// <summary>No spawns: the gap before round 1, or after a clear before the next round.</summary>
    Breather,
    /// <summary>A round is live: quota enemies trickle in until all have spawned; ends when all are dead.</summary>
    Fighting,
}
