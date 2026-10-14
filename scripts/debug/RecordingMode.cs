using Godot;

namespace MyGame;

/// <summary>
/// DEBUG: RECORDING mode — just Khalid and the arena, for capturing footage of the stage. While it is on the HUD is
/// hidden, the round does not run (no enemy spawns, no pressure spawns) and the stalls show no prompt and don't open.
/// Driven by <see cref="DebugTools"/> (H); <see cref="RunManager"/> reads <see cref="On"/> to hold the round.
/// </summary>
public sealed class RecordingMode
{
    private readonly System.Action _rebuildArena;

    /// <param name="rebuildArena">Rebuilds the arena fresh (<c>RunManager.BuildArena</c>).</param>
    public RecordingMode(System.Action rebuildArena)
    {
        _rebuildArena = rebuildArena;
    }

    public bool On { get; private set; }

    /// <summary>Switch recording mode on or off. Either way the arena is rebuilt, so going in clears the enemies and
    /// coming out starts a fresh round 1.</summary>
    /// <param name="inTree">Any node in the scene tree — for the HUD autoload and the new arena's stalls.</param>
    public void Toggle(Node inTree)
    {
        On = !On;
        _rebuildArena();
        inTree.GetNode<HUD>("/root/HUD").Visible = !On;
        if (On)
            foreach (Node stall in inTree.GetTree().GetNodesInGroup(LevelLayout.StallGroup))
                if (!stall.IsQueuedForDeletion()) // the old arena's stalls are still in the group until freed
                    stall.GetNode<Area2D>("Interact").SetDeferred(Area2D.PropertyName.Monitoring, false);
        GD.Print($"[DEBUG] recording mode: {(On ? "ON" : "off")}");
    }
}
