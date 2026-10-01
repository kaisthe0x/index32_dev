using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Root of a hand-painted arena LAYOUT scene (<c>scenes/levels/stageN/stageN_vK.tscn</c>). Holds a painted
/// <c>TileMapLayer</c> ("Terrain" — its tiles carry collision), a "Decor" <c>TileMapLayer</c> (rocks and plants, no
/// collision — this class ignores it), plus a <b>PlayerSpawn</b> <c>Marker2D</c>. RunManager
/// instantiates ONE random variant per run and reads these.
///
/// <para>AUTHORING (in the editor): paint the <b>Terrain</b> layer with the terrain TileSet and drag the
/// <b>PlayerSpawn</b> marker where you want it. Enemy spawn positions aren't authored — RunManager proximity-spawns
/// around the player on the floor he's standing on (<see cref="SpawnSurfacesNear"/>). Optional launch-orb spots
/// go in the <b>orb</b> group. WHICH enemies appear is RunManager's spawn pool (kits in <see cref="EnemyKits"/>).</para>
/// </summary>
[GlobalClass, Tool]
public partial class LevelLayout : Node2D
{
    /// <summary>Put the layout's layers — and the stalls placed in it — at their <see cref="WorldZ"/>, at runtime AND in
    /// the editor (this is a [Tool] for that only), so what you paint sits in the same order you'll see in the game,
    /// whatever the scene file says. Stalls are found by their scenes' <c>stalls</c> group, not their type — in the editor
    /// their (non-tool) scripts don't run as C# classes. (A stall sets its own z too; that covers one dropped in when a
    /// layout has none.)</summary>
    /// <summary>The group every stall scene's root is in (<c>scenes/things/</c>).</summary>
    private const string StallGroup = "stalls";

    public override void _Ready()
    {
        SetZ("Aesthetic", WorldZ.Scenery);
        SetZ("Decor", WorldZ.Decor);
        SetZ("Terrain", WorldZ.Terrain);
        foreach (Node n in FindChildren("*", "", true, false))
            if (n.IsInGroup(StallGroup) && n is Node2D stall)
                stall.ZIndex = WorldZ.Stalls;
    }

    private void SetZ(string child, int z)
    {
        if (GetNodeOrNull<Node2D>(child) is Node2D n)
            n.ZIndex = z;
    }

    /// <summary>World position of the player start.</summary>
    public Vector2 PlayerSpawn() => MarkerPos("PlayerSpawn");

    /// <summary>The stall of type <typeparamref name="T"/> placed in this layout (its scene dropped in the editor), or null
    /// if the layout has none.</summary>
    public T Placed<T>() where T : Stall
    {
        foreach (Node n in FindChildren("*", "", true, false))
            if (n is T stall)
                return stall;
        return null;
    }

    /// <summary>Optional launch-orb positions.</summary>
    public List<Vector2> Orbs() => GroupPositions("orb");

    /// <summary>A spawn tile must sit in a flat run of at least this many walkable tiles — a lone scattered tile (or a
    /// 2-tile ledge) would strand a grunt with nowhere to walk.</summary>
    private const int MinSpawnFloorTiles = 3;

    // The walkable floors, computed once from the Terrain tilemap. A "top" is a Terrain cell WITH COLLISION (solid or
    // one-way; decoration doesn't count) whose cell ABOVE is empty. Two neighbouring tops join the same FLOOR region
    // only where their surfaces actually MEET (see Linked) — flat tiles side by side, a ramp and the floors at its two
    // ends — so a block step, or slopes laid as a sawtooth, splits regions. A region is somewhere a grunt can walk.
    private readonly List<(Vector2 Pos, int Region)> _topPositions = new();         // every top: tile-top world pos + region
    private readonly Dictionary<int, List<Vector2>> _spawnable = new();             // region → its spawn-worthy tops
    private bool _groundBuilt;

    /// <summary>Where a ground/stationary enemy may spawn so it can actually reach the player: the tops of the FLOOR
    /// region nearest <paramref name="groundPoint"/> (the ground under the player), limited to flat runs of at least
    /// <see cref="MinSpawnFloorTiles"/> tiles. If that region has no such run (the player is perched on a lone tile or
    /// short ledge), the region of the nearest spawn-worthy top instead. World positions on the tile tops; empty only if
    /// the layout has no walkable run at all.</summary>
    public List<Vector2> SpawnSurfacesNear(Vector2 groundPoint)
    {
        BuildGround();
        int region = NearestRegion(groundPoint, requireSpawnable: false);
        if (region >= 0 && _spawnable.TryGetValue(region, out var here))
            return here;
        region = NearestRegion(groundPoint, requireSpawnable: true);
        return region >= 0 ? _spawnable[region] : new List<Vector2>();
    }

    /// <summary>The region of the top nearest <paramref name="point"/> (optionally only regions with spawn-worthy tops);
    /// -1 if there's none.</summary>
    private int NearestRegion(Vector2 point, bool requireSpawnable)
    {
        int best = -1;
        float bestD = float.MaxValue;
        foreach (var (pos, region) in _topPositions)
        {
            if (requireSpawnable && !_spawnable.ContainsKey(region))
                continue;
            float d = pos.DistanceSquaredTo(point);
            if (d < bestD)
            {
                bestD = d;
                best = region;
            }
        }
        return best;
    }

    private void BuildGround()
    {
        if (_groundBuilt)
            return;
        _groundBuilt = true;
        var tm = GetNodeOrNull<TileMapLayer>("Terrain");
        if (tm?.TileSet == null)
            return;
        int layers = tm.TileSet.GetPhysicsLayersCount();
        var tops = new HashSet<Vector2I>();
        var slopes = new Dictionary<Vector2I, int>(); // slope cell → the side it rises toward (+1 right, -1 left)
        foreach (Vector2I cell in tm.GetUsedCells())
        {
            if (HasCollision(tm.GetCellTileData(cell + Vector2I.Up), layers))
                continue; // something SOLID sits directly above -> not an exposed top (a plant or rock doesn't count)
            TileData td = tm.GetCellTileData(cell);
            if (!HasCollision(td, layers))
                continue; // decoration — nothing to stand on
            tops.Add(cell);
            int rise = SlopeRise(td, layers);
            if (rise != 0)
                slopes[cell] = rise;
        }
        var regionOf = LabelRegions(tops, slopes);
        float halfH = tm.TileSet.TileSize.Y * 0.5f;
        foreach (Vector2I cell in tops)
        {
            Vector2 pos = tm.ToGlobal(tm.MapToLocal(cell) - new Vector2(0.0f, halfH)); // tile-top, world space
            int region = regionOf[cell];
            _topPositions.Add((pos, region));
            if (FloorRun(tops, slopes, cell) < MinSpawnFloorTiles)
                continue; // too short to walk on
            if (!_spawnable.TryGetValue(region, out var list))
                _spawnable[region] = list = new List<Vector2>();
            list.Add(pos);
        }
    }

    /// <summary>Flood-fill the tops into connected floor regions over the cells left/right and diagonally up/down,
    /// joining only where the surfaces meet (<see cref="Linked"/>).</summary>
    private static Dictionary<Vector2I, int> LabelRegions(HashSet<Vector2I> tops, Dictionary<Vector2I, int> slopes)
    {
        var regionOf = new Dictionary<Vector2I, int>();
        int next = 0;
        var queue = new Queue<Vector2I>();
        foreach (Vector2I start in tops)
        {
            if (regionOf.ContainsKey(start))
                continue;
            regionOf[start] = next;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2I c = queue.Dequeue();
                foreach (int dx in new[] { -1, 1 })
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var n = c + new Vector2I(dx, dy);
                        if (tops.Contains(n) && !regionOf.ContainsKey(n) && Linked(c, n, slopes))
                        {
                            regionOf[n] = next;
                            queue.Enqueue(n);
                        }
                    }
            }
            next++;
        }
        return regionOf;
    }

    /// <summary>Whether neighbouring tops <paramref name="a"/> and <paramref name="b"/> (b = a + (±1, -1..1)) have surfaces
    /// that meet, so a body can walk from one onto the other. Side by side: both must reach the TOP of their row on the
    /// facing sides (a flat tile always does; a slope only on the side it rises toward). Diagonal: the upper one must be
    /// a slope whose LOW side faces the lower one (a ramp tile sits a row above the floor it rises from), and the lower
    /// one must reach its row top on the facing side.</summary>
    private static bool Linked(Vector2I a, Vector2I b, Dictionary<Vector2I, int> slopes)
    {
        bool TopOpen(Vector2I cell, int side) => !slopes.TryGetValue(cell, out int rise) || rise == side;
        if (a.Y == b.Y)
        {
            int dx = b.X - a.X;
            return TopOpen(a, dx) && TopOpen(b, -dx);
        }
        Vector2I upper = a.Y < b.Y ? a : b, lower = a.Y < b.Y ? b : a;
        int toward = lower.X - upper.X; // the side of the upper cell that faces the lower one
        return slopes.TryGetValue(upper, out int upRise) && upRise == -toward && TopOpen(lower, -toward);
    }

    /// <summary>Length in tiles of the walkable run of tops through <paramref name="cell"/> along its row (contiguous,
    /// each step <see cref="Linked"/>).</summary>
    private static int FloorRun(HashSet<Vector2I> tops, Dictionary<Vector2I, int> slopes, Vector2I cell)
    {
        int run = 1;
        foreach (Vector2I step in new[] { Vector2I.Left, Vector2I.Right })
            for (Vector2I c = cell; tops.Contains(c + step) && Linked(c, c + step, slopes); c += step)
                run++;
        return run;
    }

    /// <summary>If a tile's collision is a RAMP — any polygon edge running diagonally over at least
    /// <see cref="SlopeEdgeMin"/> px both ways (so a cut or rounded corner doesn't count) — the side it rises toward
    /// (+1 right / -1 left, from where its highest point sits); 0 if it isn't a slope.</summary>
    private static int SlopeRise(TileData td, int physicsLayers)
    {
        bool sloped = false;
        Vector2 highest = new(0.0f, float.MaxValue);
        for (int layer = 0; layer < physicsLayers; layer++)
            for (int i = 0; i < td.GetCollisionPolygonsCount(layer); i++)
            {
                Vector2[] pts = td.GetCollisionPolygonPoints(layer, i);
                for (int j = 0; j < pts.Length; j++)
                {
                    Vector2 d = pts[(j + 1) % pts.Length] - pts[j];
                    if (Mathf.Abs(d.X) >= SlopeEdgeMin && Mathf.Abs(d.Y) >= SlopeEdgeMin)
                        sloped = true;
                    if (pts[j].Y < highest.Y)
                        highest = pts[j];
                }
            }
        return sloped ? (highest.X > 0.0f ? 1 : -1) : 0;
    }

    private const float SlopeEdgeMin = 16.0f; // half a tile — a real ramp; a cut or rounded corner (≤ ~9 px) isn't one

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
