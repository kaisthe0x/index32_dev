using Godot;

namespace MyGame;

/// <summary>
/// Being dragged toward a point — Khalid's Come Closer (<see cref="MagnetField"/>). While a pull is on, the enemy
/// slides toward the anchor, slowing as it nears; on arrival the pull ends and the enemy is stunned.
/// </summary>
public sealed class MagnetPull
{
    private const float MinSpeedShare = 0.25f;   // it never slows below this share of the pull speed

    private Node2D? _anchor;
    private float _arriveDist, _speed;

    /// <summary>How long the enemy is stunned once it arrives.</summary>
    public float StunTime { get; private set; }

    /// <summary>Start (or retarget) a pull toward <paramref name="anchor"/>.</summary>
    public void Start(Node2D anchor, float arriveDist, float speed, float stunTime)
    {
        _anchor = anchor;
        _arriveDist = arriveDist;
        _speed = speed;
        StunTime = stunTime;
    }

    /// <summary>One tick for a body at world X <paramref name="x"/>: null if no pull is on (or its anchor is gone);
    /// 0 if it has ARRIVED (the pull is over — stun it); otherwise the horizontal velocity to move at.</summary>
    public float? Step(float x)
    {
        if (_anchor == null)
            return null;
        if (!GodotObject.IsInstanceValid(_anchor))
        {
            _anchor = null;
            return null;
        }
        float dx = _anchor.GlobalPosition.X - x;
        if (Mathf.Abs(dx) <= _arriveDist)
        {
            _anchor = null;
            return 0.0f;
        }
        return Mathf.Sign(dx) * _speed * Mathf.Clamp(Mathf.Abs(dx) / (_arriveDist * 2.0f), MinSpeedShare, 1.0f);
    }
}
