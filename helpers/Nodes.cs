using Godot;

namespace MyGame;

/// <summary>Small node utilities shared by unrelated scripts.</summary>
public static class Nodes
{
    /// <summary>Put <paramref name="node"/> at a world position NOW: sets the position and resets physics interpolation,
    /// so it does not smear in from where it was (or from the origin, for a node just added).</summary>
    public static void PlaceAt(Node2D node, Vector2 pos)
    {
        node.GlobalPosition = pos;
        node.ResetPhysicsInterpolation();
    }
}
