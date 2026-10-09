using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>
    /// The slam: from the air he drops straight down at <see cref="Locomotion.SlamSpeed"/>, hidden once the wind-up
    /// frames have played, and on reaching the ground plays the impact — whose damage scales with how far he fell.
    /// Started from the air with the special button, if there is room to fall.
    /// </summary>
    private sealed class SlamState : PlayerState
    {
        public SlamState(Player player) : base(player) { }

        private bool _impacting;
        private float _startY;

        public override StringName Animation => "slam";

        /// <summary>Whether a slam can start here: the character has one, and there is clear air below.</summary>
        public bool CanStart()
        {
            if (Sprite.SpriteFrames == null || !Sprite.SpriteFrames.HasAnimation("slam"))
                return false;
            if (P._slamMinClearance <= 0.0f)
                return true;
            var space = P.GetWorld2D().DirectSpaceState;
            if (space == null)
                return true;
            var q = PhysicsRayQueryParameters2D.Create(
                P.GlobalPosition, P.GlobalPosition + new Vector2(0.0f, P._slamMinClearance), P.CollisionMask);
            q.Exclude = new Godot.Collections.Array<Rid> { P.GetRid() };
            return space.IntersectRay(q).Count == 0;
        }

        public override void Enter()
        {
            Velocity = new Vector2(0.0f, P._slamSpeed);
            _impacting = false;
            _startY = P.GlobalPosition.Y;
            P._slamDownSfx?.Play();
            foreach (var p in P._passives)
                p.OnSlamTrigger(P);
        }

        public override void Tick(float delta)
        {
            Brake(delta);
            if (_impacting)
            {
                SetVelY(OnFloor ? 0.0f : Mathf.Max(Velocity.Y, P._slamSpeed));
                return;
            }
            if (OnFloor || P.NearGround(P._slamImpactDistance))
            {
                Impact();
                return;
            }
            SetVelY(Mathf.Max(Velocity.Y, P._slamSpeed));
            int hold = Mathf.Max(0, P._slamHoldFrame - AnimMeta.SheetStart(Sprite.SpriteFrames, "slam"));
            if (Sprite.Frame >= hold)
            {
                Sprite.SetFrameAndProgress(hold, 0.0f);
                Sprite.SpeedScale = 0.0f;
                Sprite.Visible = false;
            }
        }

        private void Impact()
        {
            _impacting = true;
            Sprite.Visible = true;
            Sprite.SpeedScale = 1.0f;
            P._slamDownSfx?.Stop();
            P._sfx.Play("slam");
            float drop = P.GlobalPosition.Y - _startY;
            float t = Mathf.Clamp((drop - P._slamMinDrop) / Mathf.Max(P._slamMaxDrop - P._slamMinDrop, 1.0f), 0.0f, 1.0f);
            P._activeHit = new SegmentData { DamageScale = Mathf.Lerp(1.0f, P._slamMaxDamageMult, t) * P.SlamDamageMult };
            foreach (var p in P._passives)
                p.OnSlamLand(P, drop, Mathf.Max(Velocity.Y, P._slamSpeed));
        }
    }
}
