using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>
    /// A dash: a burst forward with brief invulnerability (<see cref="Left"/> &gt; 0), then a recovery in which he keeps
    /// moving only if the direction is held. Khalid's is a BLINK — he is displaced at once and the rest is the recovery.
    /// Dashing into range of a launch orb is taken by the orb instead. An attack pressed during the dash is buffered
    /// and comes out as the burst ends. The dash CHARGES are the player's (<see cref="Player._dashCharges"/>).
    /// </summary>
    private sealed class DashState : PlayerState
    {
        public DashState(Player player) : base(player) { }

        /// <summary>Seconds of the burst (and of its invulnerability) still to go.</summary>
        public float Left;
        private float _animLeft;
        private bool _blink;            // this dash displaced him at once (Locomotion.Blink)
        private bool _bufferedAttack;

        public override StringName Animation => "dash";

        /// <summary>Forget a buffered attack (the character was re-applied).</summary>
        public void ClearBuffer() => _bufferedAttack = false;

        public override void Enter()
        {
            P._gustLeft = 0.0f; // dashing breaks out of a gust (a recovery move)
            Left = P._dashTime;
            _animLeft = Mathf.Max(P._dashAnimTime, P._dashTime);
            if (P._dashCharges == P.MaxDashCharges)
                P._dashCd = P._dashCooldown; // the refill clock starts with the first charge spent
            P._dashCharges -= 1;
            _bufferedAttack = false;
            P._sfx.Play("dash");
            foreach (var p in P._passives)
                p.OnDash(P);
            if (P._dashEffect != "")
            {
                P._activeHit = new SegmentData();
                P.FireEffect(P._dashEffect);
            }
            var frames = Sprite.SpriteFrames;
            float fps = (float)frames.GetAnimationSpeed("dash");
            if (fps > 0.0f)
            {
                float animTime = frames.GetFrameCount("dash") / fps;
                Sprite.SpeedScale = animTime / Mathf.Max(P._dashAnimTime, P._dashTime);
            }
            _blink = P._blinkDash;
            if (_blink)
                Blink();
        }

        public override void Tick(float delta)
        {
            if (P._launch.TryBegin())
                return;
            _animLeft -= delta;
            float input = Input.GetAxis("move_left", "move_right");
            bool holdingDashDir = input != 0.0f && Mathf.Sign(input) == P._facing;

            if (Input.IsActionJustPressed("attack"))
                _bufferedAttack = true;
            if (_bufferedAttack && Left <= 0.0f && (OnFloor || P.AirAttackOk()))
            {
                _bufferedAttack = false;
                P._attack.Advance();
                if (P._current != this)
                    return;
            }

            if (_blink)
            {
                Left = Mathf.Max(Left - delta, 0.0f);
                float target = holdingDashDir ? P.RunSpeed() * P._facing : 0.0f;
                SetVelX(Mathf.MoveToward(Velocity.X, target, (P._dashSpeed / P._dashTime) * delta));
            }
            else if (Left > 0.0f)
            {
                Left -= delta;
                SetVelX(P._dashSpeed * P._facing);
            }
            else
            {
                float target = holdingDashDir ? P.RunSpeed() * P._facing : 0.0f;
                float recovery = Mathf.Max(P._dashAnimTime - P._dashTime, 0.001f);
                SetVelX(Mathf.MoveToward(Velocity.X, target, (P._dashSpeed / recovery) * delta));
            }
            if (OnFloor)
                SetVelY(0.0f);
            else
                AddVelY(P._gravity * P._dashGravityScale * delta);
            if (_animLeft <= 0.0f)
                P.EnterFree(holdingDashDir && OnFloor ? FreeMode.Run : FreeMode.Idle);
        }

        /// <summary>The blink: jump the whole dash distance now, stopping at walls.</summary>
        private void Blink()
        {
            var motion = new Vector2(P._dashSpeed * P._dashTime * P._facing, 0.0f);
            P.FireEffect("blink_out");
            P.MoveAndCollide(motion);
            SetVelX(0.0f);
            P.FireEffect("blink_in");
            P.Modulate = new Color(2.2f, 2.2f, 2.2f);
            P.CreateTween().TweenProperty(P, "modulate", new Color(1, 1, 1), 0.18);
        }
    }
}
