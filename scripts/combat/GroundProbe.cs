using Godot;

namespace MyGame;

/// <summary>
/// The one primitive the ground-conforming (<see cref="GroundContour"/>) and ground-following (a Projectile riding
/// the terrain) features both build on: cast a short vertical ray and report the <see cref="Combat.Layer.World"/>
/// surface at an x — its point and normal. One source of truth for "where is the ground here?".
/// </summary>
public static class GroundProbe
{
    /// <summary>The terrain surface at <paramref name="x"/>, searched ±<paramref name="reach"/> around
    /// <paramref name="aroundY"/> (topmost hit). False if there's no ground in that window (a gap / ledge).</summary>
    public static bool TryAt(PhysicsDirectSpaceState2D space, float x, float aroundY, float reach,
        out Vector2 point, out Vector2 normal)
    {
        point = default;
        normal = Vector2.Up;
        if (space == null)
            return false;
        var q = PhysicsRayQueryParameters2D.Create(
            new Vector2(x, aroundY - reach), new Vector2(x, aroundY + reach), (uint)Combat.Layer.World);
        var hit = space.IntersectRay(q);
        if (hit.Count == 0)
            return false;
        point = hit["position"].As<Vector2>();
        normal = hit["normal"].As<Vector2>();
        return true;
    }
}
