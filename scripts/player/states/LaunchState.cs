using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>
    /// Launch-orb traversal. Dashing within <see cref="PullRange"/> of a <see cref="LaunchOrb"/> hands the dash to the
    /// orb: he is pulled onto it in a blink, then flung up and forward with air control, untouchable meanwhile. This
    /// state is the pull; it also keeps track of which orb is in range (the orb lights up) and the short lock-out
    /// after a launch.
    /// </summary>
    private sealed class LaunchState : PlayerState
    {
        private const float PullRange = 96.0f;
        private static readonly Vector2 BodyOffset = new(0.0f, -20.0f);   // his body's centre, from his feet
        private const float MagnetTime = 0.08f;
        private const float Lockout = 0.45f;
        private const float MagnetEase = 0.35f;

        public LaunchState(Player player) : base(player) { }

        private LaunchOrb? _orb;        // the orb pulling him in
        private Vector2 _from;
        private float _t;
        private Vector2 _exitVelocity;
        private float _lockoutLeft;
        private LaunchOrb? _nearOrb;    // the orb currently lit as "in range"

        public override StringName Animation => "dash";

        public override void Enter() => Sprite.Play("dash");

        /// <summary>Every tick, whatever the state: run the lock-out down and light up the orb in range.</summary>
        public void Update(float delta)
        {
            _lockoutLeft = Mathf.Max(_lockoutLeft - delta, 0.0f);
            LaunchOrb? near = null;
            if (!P._dead && P._current != P._spawn && P._current != this)
                near = OrbInRange();
            if (near == _nearOrb)
                return;
            if (_nearOrb != null && IsInstanceValid(_nearOrb))
                _nearOrb.SetNear(false);
            near?.SetNear(true);
            _nearOrb = near;
        }

        /// <summary>A dash was pressed (or is under way): if an orb is in range and the lock-out is over, the orb takes
        /// it. True if a launch began.</summary>
        public bool TryBegin()
        {
            if (_lockoutLeft > 0.0f || OrbInRange() is not { } orb)
                return false;
            _orb = orb;
            _from = P.GlobalPosition;
            _t = 0.0f;
            _exitVelocity = new Vector2(P._facing * orb.LaunchForward, -orb.LaunchUp);
            Velocity = Vector2.Zero;
            P.Enter(this);
            orb.PlayUse();
            return true;
        }

        /// <summary>Let go of the orb (he was hit, or died). <paramref name="lockOut"/> also starts the lock-out.</summary>
        public void Release(bool lockOut)
        {
            _orb = null;
            if (lockOut)
                _lockoutLeft = Lockout;
        }

        public override void Tick(float delta)
        {
            if (!IsInstanceValid(_orb))
            {
                _orb = null;
                P.EnterFree(P._free.AirborneDefault());
                return;
            }
            _t += delta;
            float t = Mathf.Clamp(_t / MagnetTime, 0.0f, 1.0f);
            Vector2 target = _orb!.GlobalPosition - BodyOffset;
            P.GlobalPosition = _from.Lerp(target, Ease(t, MagnetEase));
            P.UpdateAnimation(delta);
            if (t >= 1.0f)
            {
                _orb = null;
                _lockoutLeft = Lockout;
                Velocity = _exitVelocity;
                if (Mathf.Abs(Velocity.X) > 5.0f)
                    P._facing = Velocity.X > 0.0f ? 1 : -1;
                P._dash.Left = Mathf.Max(P._dash.Left, 0.12f);
                P._free.JumpLaunch = true;
                P.EnterFree(P._free.AirborneDefault());
            }
        }

        private LaunchOrb? OrbInRange()
        {
            var body = P.GlobalPosition + BodyOffset;
            LaunchOrb? best = null;
            float bestD = PullRange * PullRange;
            foreach (Node o in P.GetTree().GetNodesInGroup("orbs"))
            {
                if (o is not LaunchOrb orb)
                    continue;
                float d = body.DistanceSquaredTo(orb.GlobalPosition);
                if (d < bestD)
                {
                    bestD = d;
                    best = orb;
                }
            }
            return best;
        }

        /// <summary>GDScript's <c>ease(x, curve)</c> for 0 &lt; curve &lt; 1 (ease-out).</summary>
        private static float Ease(float x, float curve)
        {
            x = Mathf.Clamp(x, 0.0f, 1.0f);
            if (curve > 0.0f)
                return curve < 1.0f ? 1.0f - Mathf.Pow(1.0f - x, 1.0f / curve) : Mathf.Pow(x, curve);
            return x;
        }
    }
}
