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

    /// <summary>A spawn tile must sit in a flat run of at least this many walkable tiles — a lone scattered tile (or a
    /// 2-tile ledge) would strand a grunt with nowhere to walk.</summary>
    private const int MinSpawnFloorTiles = 3;

    /// <summary>World positions on TOP of exposed ground tiles — a Terrain cell WITH COLLISION (solid or one-way
    /// platform; decoration-only tiles don't count) whose cell ABOVE is empty, i.e. walkable footing — that belong to
    /// a flat run of at least <see cref="MinSpawnFloorTiles"/> such tiles, so an enemy spawned there can move left and
    /// right. RunManager proximity-spawns ground/stationary enemies onto these (near the player, but never on him).
    /// Computed once from the Terrain tilemap; empty if the layout has no Terrain layer.</summary>
    public List<Vector2> GroundSurfaces()
    {
        if (_groundSurfaces != null)
            return _groundSurfaces;
        _groundSurfaces = new List<Vector2>();
        var tm = GetNodeOrNull<TileMapLayer>("Terrain");
        if (tm?.TileSet == null)
            return _groundSurfaces;
        var tops = new HashSet<Vector2I>();
        foreach (Vector2I cell in tm.GetUsedCells())
        {
            if (tm.GetCellSourceId(cell + new Vector2I(0, -1)) != -1)
                continue; // something sits directly above -> not an exposed top
            if (!HasCollision(tm.GetCellTileData(cell), tm.TileSet.GetPhysicsLayersCount()))
                continue; // decoration — nothing to stand on
            tops.Add(cell);
        }
        float halfH = tm.TileSet.TileSize.Y * 0.5f;
        foreach (Vector2I cell in tops)
        {
            if (FloorRun(tops, cell) < MinSpawnFloorTiles)
                continue; // too short to walk on
            _groundSurfaces.Add(tm.ToGlobal(tm.MapToLocal(cell) - new Vector2(0.0f, halfH))); // tile-top, world space
        }
        return _groundSurfaces;
    }

    /// <summary>Length in tiles of the flat run of exposed tops through <paramref name="cell"/> (same row, contiguous).</summary>
    private static int FloorRun(HashSet<Vector2I> tops, Vector2I cell)
    {
        int run = 1;
        for (var c = cell + Vector2I.Left; tops.Contains(c); c += Vector2I.Left)
            run++;
        for (var c = cell + Vector2I.Right; tops.Contains(c); c += Vector2I.Right)
            run++;
        return run;
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
