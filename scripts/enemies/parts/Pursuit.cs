using Godot;

namespace MyGame;

/// <summary>
/// How a HUNTING enemy closes on a target it cannot simply walk up to. A hunter chases the player wherever he is
/// (<see cref="Enemy.Hunt"/>), and — unlike a patrolling enemy — steps off a ledge when there is ground below to land
/// on. Its body cannot climb, so against a target on another level it does the one thing each case allows:
///
/// <para><b>Target above:</b> walk to the spot under him and wait there, each hunter a little to one side
/// (its own offset, rolled once) so a crowd waiting under a platform spreads out instead of standing on one point.</para>
///
/// <para><b>Target below:</b> walk toward him and off the first ledge on the way. Once it stands right over him the
/// way down is at an end of its floor, not under its feet, so it keeps going the way it faces until it finds one — and
/// turns round at an end that only drops into the void.</para>
/// </summary>
public sealed class Pursuit
{
    /// <summary>Within this many px of where it is heading counts as there (no jitter back and forth over the point).</summary>
    private const float ArriveSlack = 4.0f;

    private readonly EdgeSensor _edges;
    private float _waitOffset;  // where this hunter waits under a target above it, relative to him (px)
    private int _detour;        // target below and overshot: the way it keeps walking to find a ledge (0 = none)

    public Pursuit(EdgeSensor edges) => _edges = edges;

    /// <summary>Whether the enemy is hunting.</summary>
    public bool Active { get; private set; }
    /// <summary>A hunter's chase speed, as a multiple of its move speed (its walk animation speeds up to match).</summary>
    public float SpeedMult { get; private set; } = 1.0f;

    /// <summary>Start hunting, or change a hunter's pace. <paramref name="waitSpread"/> is how far to either side of a
    /// target above it this hunter may wait; its place in that band is rolled the first time only.</summary>
    public void Begin(float speedMult, float waitSpread)
    {
        if (!Active)
            _waitOffset = (float)GD.RandRange(-waitSpread, waitSpread);
        Active = true;
        SpeedMult = speedMult;
    }

    /// <summary>Whether a hunter may walk on in direction <paramref name="dir"/>: there is footing ahead, or a ledge with
    /// ground under it. False at an edge over the void.</summary>
    public bool CanStep(int dir) => _edges.FloorAhead(dir) || _edges.LandingBelow(dir);

    /// <summary>The target is back within the enemy's own level: forget the way it was walking to get down to him.</summary>
    public void OnLevel() => _detour = 0;

    /// <summary>Which way to walk (-1, 0 = stay, +1) from <paramref name="self"/> toward a <paramref name="target"/>
    /// that is on another level; <paramref name="facing"/> is the way the enemy faces now.</summary>
    public int StepToward(Vector2 self, Vector2 target, int facing)
    {
        float toTarget = target.X - self.X;
        if (target.Y < self.Y)
        {
            _detour = 0;
            float toWait = toTarget + _waitOffset;
            if (Mathf.Abs(toWait) <= ArriveSlack)
                return 0;
            int toward = Mathf.Sign(toWait);
            return CanStep(toward) ? toward : 0;
        }

        if (_detour == 0 && Mathf.Abs(toTarget) <= ArriveSlack)
            _detour = facing;
        int dir = _detour != 0 ? _detour : Mathf.Sign(toTarget);
        if (CanStep(dir))
            return dir;
        _detour = -dir;
        return 0;
    }
}
