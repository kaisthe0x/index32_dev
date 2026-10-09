using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Where things can stand in the current arena: physics probes against the world plus the layout's walkable tiles.
/// The spawners ask it for a tile on the player's floor, whether a spot is inside a wall, how much headroom there is.
/// Built by <see cref="RunManager"/> for each arena (the layout changes with it). The queries build their shapes per
/// call — they run at spawn time, not per frame (docs/standards.md, Known debt).
/// </summary>
public sealed class ArenaGround
{
    private const float GroundProbeDepth = 600.0f; // how far below a point to look for the floor it is over
    private static readonly Vector2 SpawnClearance = new(24, 40);    // room a spawning ground enemy needs (a bit over a grunt's body)

    private readonly Node2D _world;         // any node in the arena's physics space
    private readonly LevelLayout? _layout;

    public ArenaGround(Node2D world, LevelLayout? layout)
    {
        _world = world;
        _layout = layout;
    }

    /// <summary>Whether <paramref name="point"/> is inside solid world collision.</summary>
    public bool InsideWall(Vector2 point)
    {
        var space = _world.GetWorld2D()?.DirectSpaceState;
        if (space == null)
            return false;
        var q = new PhysicsPointQueryParameters2D { Position = point, CollisionMask = (uint)Combat.Layer.World };
        return space.IntersectPoint(q, 1).Count > 0;
    }

    /// <summary>Clear vertical space above <paramref name="from"/>, up to <paramref name="max"/> — how high something
    /// can sit there without being inside a ceiling.</summary>
    public float HeadroomAbove(Vector2 from, float max)
    {
        var space = _world.GetWorld2D()?.DirectSpaceState;
        if (space == null)
            return max;
        var q = PhysicsRayQueryParameters2D.Create(from, from + new Vector2(0.0f, -(max + 16.0f)), (uint)Combat.Layer.World);
        var hit = space.IntersectRay(q);
        if (hit.Count == 0)
            return max;
        return Mathf.Max(from.Y - hit["position"].As<Vector2>().Y - 14.0f, 0.0f);
    }

    /// <summary>A random spawn tile ON THE FLOOR <paramref name="from"/> stands on (<see cref="LevelLayout.SpawnSurfacesNear"/>,
    /// found from the ground straight below it — so a grunt never spawns on a platform the player can't reach, or that
    /// can't reach the player) whose horizontal distance from <paramref name="from"/> is in [min,max]; if none fall in
    /// that band, the nearest tile that is still ≥ min away (so it's never adjacent to the player); null only if the
    /// layout has no walkable run at all. A nonzero <paramref name="side"/> (+1 right / -1 left) restricts the band to
    /// that side; if that side has no tile in the band (backed against the arena edge or a pit), it falls back to
    /// either side.</summary>
    public Vector2? PickSurface(Vector2 from, float min, float max, int side = 0)
    {
        float fromX = from.X;
        var near = _layout?.SpawnSurfacesNear(GroundBelow(from));
        if (near == null)
            return null;
        var surfaces = near.FindAll(SpotIsClear); // not inside something solid standing on the tiles (a stall's dais)
        if (surfaces.Count == 0)
            return null;
        if (side != 0)
        {
            var sideBand = new List<Vector2>();
            foreach (Vector2 s in surfaces)
            {
                float dx = (s.X - fromX) * side; // distance toward the requested side
                if (dx >= min && dx <= max)
                    sideBand.Add(s);
            }
            if (sideBand.Count > 0)
                return sideBand[(int)(GD.Randi() % (uint)sideBand.Count)];
        }
        var band = new List<Vector2>();
        Vector2? nearestFair = null;
        float nearestFairScore = float.MaxValue;
        Vector2 farthest = surfaces[0];
        float farthestD = -1.0f;
        foreach (Vector2 s in surfaces)
        {
            float d = Mathf.Abs(s.X - fromX);
            if (d >= min && d <= max)
                band.Add(s);
            if (d >= min && d < nearestFairScore) { nearestFairScore = d; nearestFair = s; }
            if (d > farthestD) { farthestD = d; farthest = s; }
        }
        if (band.Count > 0)
            return band[(int)(GD.Randi() % (uint)band.Count)];
        return nearestFair ?? farthest; // band empty → closest tile still ≥min; if even that fails, the farthest we have
    }

    /// <summary>Whether a body standing on <paramref name="surface"/> would be clear of solid collision — the tiles know
    /// nothing about a stall's dais (or any solid prop) built over them, so a floor tile under one would otherwise
    /// spawn an enemy stuck inside it. Checks a <see cref="SpawnClearance"/> box just above the surface against
    /// <see cref="Combat.Layer.World"/> (one-way platforms don't block).</summary>
    private bool SpotIsClear(Vector2 surface)
    {
        var space = _world.GetWorld2D()?.DirectSpaceState;
        if (space == null)
            return true;
        var q = new PhysicsShapeQueryParameters2D
        {
            Shape = new RectangleShape2D { Size = SpawnClearance },
            Transform = new Transform2D(0.0f, surface - new Vector2(0.0f, SpawnClearance.Y / 2.0f + 2.0f)), // 2 px off the floor
            CollisionMask = (uint)Combat.Layer.World,
        };
        return space.IntersectShape(q, 1).Count == 0;
    }

    /// <summary>The ground straight below <paramref name="from"/> (within <see cref="GroundProbeDepth"/>) — so a player
    /// mid-jump still counts as on the floor under him. Over a pit (nothing below), <paramref name="from"/> itself.</summary>
    private Vector2 GroundBelow(Vector2 from)
    {
        var space = _world.GetWorld2D()?.DirectSpaceState;
        if (space == null)
            return from;
        var q = PhysicsRayQueryParameters2D.Create(from + new Vector2(0.0f, -4.0f), from + new Vector2(0.0f, GroundProbeDepth), Combat.GroundMask);
        var hit = space.IntersectRay(q);
        return hit.Count > 0 ? hit["position"].As<Vector2>() : from;
    }
}
