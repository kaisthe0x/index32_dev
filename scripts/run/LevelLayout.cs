using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Root of a hand-painted level LAYOUT scene (<c>scenes/levels/stageN/lM/vK.tscn</c>). Holds a painted
/// <c>TileMapLayer</c> ("Terrain" — its solid tiles carry collision) plus spawn <c>Marker2D</c>s. RunManager
/// instantiates ONE random variant per level and reads these, so each entry into a level is a hand-made look.
///
/// <para>AUTHORING (in the editor): paint the <b>Terrain</b> layer with the terrain TileSet; drag the
/// <b>PlayerSpawn</b> + <b>Exit</b> markers where you want them. Enemy spawn positions are NO LONGER authored —
/// RunManager proximity-spawns around the player using the Terrain's exposed ground tiles (<see cref="GroundSurfaces"/>),
/// so old <c>spawn_ground</c>/<c>spawn_air</c> markers are unused and can be deleted. Optional launch-orb spots still
/// go in the <b>orb</b> group. WHICH enemies appear is the shared per-level roster in <see cref="Levels"/>.</para>
/// </summary>
[GlobalClass]
public partial class LevelLayout : Node2D
{
    /// <summary>World position of the player start.</summary>
    public Vector2 PlayerSpawn() => MarkerPos("PlayerSpawn");

    /// <summary>World position of the exit door.</summary>
    public Vector2 ExitPoint() => MarkerPos("Exit");

    /// <summary>Optional launch-orb positions.</summary>
    public List<Vector2> Orbs() => GroupPositions("orb");

    private List<Vector2> _groundSurfaces;

    /// <summary>World positions on TOP of exposed ground tiles — a Terrain cell WITH COLLISION (solid or one-way
    /// platform; decoration-only tiles like support poles don't count) whose cell ABOVE is empty, i.e.
    /// walkable footing. RunManager proximity-spawns ground/stationary enemies onto these (near the player, but never
    /// on him). Computed once from the Terrain tilemap; empty if the layout has no Terrain layer.</summary>
    public List<Vector2> GroundSurfaces()
    {
        if (_groundSurfaces != null)
            return _groundSurfaces;
        _groundSurfaces = new List<Vector2>();
        var tm = GetNodeOrNull<TileMapLayer>("Terrain");
        if (tm?.TileSet == null)
            return _groundSurfaces;
        float halfH = tm.TileSet.TileSize.Y * 0.5f;
        foreach (Vector2I cell in tm.GetUsedCells())
        {
            if (tm.GetCellSourceId(cell + new Vector2I(0, -1)) != -1)
                continue; // something sits directly above -> not an exposed top
            if (!HasCollision(tm.GetCellTileData(cell), tm.TileSet.GetPhysicsLayersCount()))
                continue; // decoration — nothing to stand on
            _groundSurfaces.Add(tm.ToGlobal(tm.MapToLocal(cell) - new Vector2(0.0f, halfH))); // tile-top, world space
        }
        return _groundSurfaces;
    }

    /// <summary>Whether a tile collides on any physics layer (solid ground or one-way platform).</summary>
    private static bool HasCollision(TileData td, int physicsLayers)
    {
        if (td == null)
            return false;
        for (int layer = 0; layer < physicsLayers; layer++)
            if (td.GetCollisionPolygonsCount(layer) > 0)
                return true;
        return false;
    }

    private Vector2 MarkerPos(string childName)
    {
        var m = GetNodeOrNull<Node2D>(childName);
        return m?.GlobalPosition ?? GlobalPosition;
    }

    private List<Vector2> GroupPositions(string group)
    {
        var outL = new List<Vector2>();
        foreach (var n in GetTree().GetNodesInGroup(group))
            if (n is Node2D m && IsAncestorOf(m))
                outL.Add(m.GlobalPosition);
        return outL;
    }
}
