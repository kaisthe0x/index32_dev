using Godot;

namespace MyGame;

/// <summary>
/// Something that pops off an enemy and flies to the player on a CURVED (quadratic Bezier) path bowed by
/// <see cref="ArcHeight"/>, always arriving at the end of <see cref="FlightTime"/> (unless the target is gone — then
/// it just frees). On arrival it shrinks into the target and calls <see cref="OnArrived"/>. The shared motion behind the
/// Ruh soul (<see cref="RuhOrb"/>) and the Lira coin (<see cref="Lira"/>) — a subclass supplies only the arrival.
/// </summary>
public abstract partial class ArcFlight : Node2D
{
    private enum Phase { Fly, Absorb }

    private Node2D? _target;
    private Vector2 _p0 = Vector2.Zero;
    private float _t = 0.0f;
    private Phase _phase = Phase.Fly;

    [Export] public float FlightTime { get; set; } = 1.1f;
    [Export] public float ArcHeight { get; set; } = 90.0f;
    [Export] public Vector2 TargetOffset { get; set; } = new(0, -18);
    [Export] public float AbsorbTime { get; set; } = 0.12f;

    public override void _Ready() => ZIndex = WorldZ.FlyingPickups;

    /// <summary>Start the flight from where this node is now toward <paramref name="target"/> (the player).</summary>
    protected void Fly(Node2D target)
    {
        _target = target;
        _p0 = GlobalPosition;
        _t = 0.0f;
        _phase = Phase.Fly;
    }

    /// <summary>The flight reached <paramref name="target"/> — the subclass's payoff (credit a pickup, fire a reaction).</summary>
    protected abstract void OnArrived(Node2D target);

    public override void _Process(double delta)
    {
        if (_phase == Phase.Absorb)
            return; // the absorb tween owns motion + its own free
        if (_target == null || !IsInstanceValid(_target))
        {
            QueueFree();
            return;
        }

        Vector2 dest = _target.GlobalPosition + TargetOffset;
        _t += (float)delta / Mathf.Max(FlightTime, 0.01f);
        if (_t >= 1.0f)
        {
            GlobalPosition = dest;
            Absorb(_target, dest);
            return;
        }

        // Quadratic Bezier p0 -> control -> dest, the control bowed perpendicular (upward-biased) by ArcHeight.
        Vector2 mid = _p0.Lerp(dest, 0.5f);
        Vector2 line = dest - _p0;
        Vector2 perp = new Vector2(-line.Y, line.X).Normalized(); // 90deg; zero-safe if line ~ 0
        if (perp.Y > 0.0f)
            perp = -perp; // bow upward
        Vector2 control = mid + perp * ArcHeight;
        GlobalPosition = Bezier(_p0, control, dest, _t);
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1.0f - t;
        return u * u * a + 2.0f * u * t * b + t * t * c;
    }

    /// <summary>The pickup beat: pay off, then shrink into the target + free.</summary>
    private void Absorb(Node2D target, Vector2 dest)
    {
        _phase = Phase.Absorb;
        OnArrived(target);
        var tw = CreateTween();
        tw.SetParallel(true);
        tw.TweenProperty(this, "global_position", dest, AbsorbTime);
        tw.TweenProperty(this, "scale", Vector2.Zero, AbsorbTime).SetEase(Tween.EaseType.In);
        tw.Chain().TweenCallback(Callable.From(QueueFree));
    }
}
