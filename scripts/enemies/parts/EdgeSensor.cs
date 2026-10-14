using Godot;

namespace MyGame;

/// <summary>
/// Tells a walking enemy whether there is floor ahead of it, so it stops at a ledge instead of walking off: one
/// downward ray a little ahead of each foot.
///
/// <para>The rays span tall on purpose, so a SLOPE isn't mistaken for a cliff. They reach UP 14 px (an upslope's rising
/// floor; hitting from inside also catches steep climbs where the origin embeds in terrain) and DOWN about a tile
/// (28 px), so a DESCENDING floor is still "ahead" and the enemy keeps chasing down instead of stopping at the lip. A
/// true drop deeper than about a tile still reads as a cliff and halts it.</para>
///
/// <para>A HUNTING enemy may step off such a cliff when there is ground to land on (<see cref="LandingBelow"/>): the
/// same ray, cast once more all the way down. No hit means the void, and it stays put.</para>
/// </summary>
public sealed class EdgeSensor
{
    private const float ReachUp = 14.0f;
    private const float Span = 42.0f;
    /// <summary>How far down <see cref="LandingBelow"/> looks: deeper than the arena's highest platform is above its
    /// floor, so any ground under a ledge is found and only the void comes back empty.</summary>
    private const float DropSpan = 640.0f;

    private readonly RayCast2D _left, _right;

    /// <summary>Adds the two rays to <paramref name="body"/>, <paramref name="ahead"/> px to each side of its feet.</summary>
    public EdgeSensor(Node2D body, float ahead)
    {
        _left = MakeRay(body, -ahead);
        _right = MakeRay(body, ahead);
    }

    /// <summary>Whether there is footing ahead in direction <paramref name="dir"/> (&lt; 0 = left, else right).</summary>
    public bool FloorAhead(int dir)
    {
        var ray = dir < 0 ? _left : _right;
        ray.ForceRaycastUpdate();
        return ray.IsColliding();
    }

    /// <summary>Whether a step off the ledge in direction <paramref name="dir"/> ends on ground rather than in the
    /// void. Asked only when <see cref="FloorAhead"/> said no.</summary>
    public bool LandingBelow(int dir)
    {
        var ray = dir < 0 ? _left : _right;
        ray.TargetPosition = new Vector2(0, DropSpan);
        ray.ForceRaycastUpdate();
        bool lands = ray.IsColliding();
        ray.TargetPosition = new Vector2(0, Span);
        return lands;
    }

    private static RayCast2D MakeRay(Node2D body, float x)
    {
        var ray = new RayCast2D
        {
            Position = new Vector2(x, -ReachUp),
            TargetPosition = new Vector2(0, Span),
            HitFromInside = true,
            CollisionMask = Combat.GroundMask, // platforms count as footing too
        };
        body.AddChild(ray);
        return ray;
    }
}
