using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>What free movement currently looks like — it picks the animation, nothing else.</summary>
    private enum FreeMode { Idle, Run, Jump, Fall, Hurt }

    /// <summary>
    /// Free movement: he is under the player's control with nothing in progress — standing, running, jumping, falling
    /// (and the tail of a flinch). Every action starts from here: attack, special, slam, dash, launch-orb, jump,
    /// drop-through. <see cref="Mode"/> only labels which of those he looks like.
    /// </summary>
    private sealed class FreeState : PlayerState
    {
        private const float DropThroughTime = 0.3f;
        private const float DoubleJumpLean = 0.6f;

        public FreeState(Player player) : base(player) { }

        public FreeMode Mode = FreeMode.Idle;
        /// <summary>He just left the ground by JUMPING (not by walking off a ledge): the jump animation plays from its
        /// first frame instead of its last. Cleared once the jump animation is showing.</summary>
        public bool JumpLaunch;
        public int AirJumpsUsed;
        /// <summary>Slam Spring: a one-shot height multiplier for the NEXT ground jump (consumed by it).</summary>
        public float SpringBonus = 1.0f;

        public override StringName Animation => Mode switch
        {
            FreeMode.Run => "run",
            FreeMode.Jump => "jump",
            FreeMode.Fall => "fall",
            FreeMode.Hurt => "hurt",
            _ => "idle",
        };

        /// <summary>What he looks like in the air with no jump behind it.</summary>
        public FreeMode AirborneDefault() => P.HasFall() ? FreeMode.Fall : FreeMode.Jump;

        public override void Tick(float delta)
        {
            float input = Input.GetAxis("move_left", "move_right");

            if (!OnFloor)
            {
                float gScale = Velocity.Y > 0.0f ? P._fallGravityScale : 1.0f;
                AddVelY(P._gravity * gScale * delta);
            }

            bool gusted = P._gustLeft > 0.0f && !OnFloor;
            if (gusted)
            {
                // Riding a gust: no air brake, and steering only pushes back AGAINST the fling, weakly — holding the way
                // he's blown can't slow him to run speed either.
                if (input != 0.0f)
                    P._facing = input > 0.0f ? 1 : -1;
                if (input != 0.0f && Mathf.Sign(input) != Mathf.Sign(Velocity.X))
                    SetVelX(Mathf.MoveToward(Velocity.X, input * P.RunSpeed(), P._acceleration * Combat.GustControl * delta));
            }
            else if (input != 0.0f)
            {
                P._facing = input > 0.0f ? 1 : -1;
                SetVelX(Mathf.MoveToward(Velocity.X, input * P.RunSpeed(), P._acceleration * delta));
            }
            else
            {
                Brake(delta);
            }

            if (Input.IsActionJustPressed("special"))
            {
                if (OnFloor && P._currentSpecial != null)
                {
                    P._special.Start();
                    return;
                }
                if (!OnFloor && P._slam.CanStart())
                {
                    P.Enter(P._slam);
                    return;
                }
            }
            if (AttackHeld() && !gusted && (OnFloor || P.AirAttackOk())) // no swinging while blown (it would halt the fling)
            {
                P._attack.Advance();
                return;
            }
            if (Input.IsActionJustPressed("dash"))
            {
                if (P._launch.TryBegin())
                    return;
                if (P._dashCharges > 0)
                {
                    P.Enter(P._dash);
                    return;
                }
            }
            if (Input.IsActionJustPressed("drop") && OnFloor)
                DropThroughPlatform();
            if (Input.IsActionJustPressed("jump"))
            {
                if (OnFloor)
                {
                    SetVelY(JumpVelocity(ground: true));
                    JumpLaunch = true;
                    P._sfx.Play("jump");
                    foreach (var p in P._passives)
                        p.OnGroundJump(P);
                }
                else if (AirJumpsUsed < P._maxAirJumps)
                {
                    AirJump();
                }
            }

            if (!OnFloor)
                SetAirborneMode();
            else if (P._justLanded && P.HasLand())
                P.Enter(P._land);
            else if (input != 0.0f && Mathf.Abs(Velocity.X) > 5.0f)
                P.SetFree(FreeMode.Run);
            else
                P.SetFree(FreeMode.Idle);
        }

        private void SetAirborneMode()
        {
            if (Velocity.Y >= P._landMinFallSpeed && P.HasLand() && P.NearGround())
            {
                P.Enter(P._land);
                return;
            }
            if (Mode == FreeMode.Jump || Mode == FreeMode.Fall)
                return;
            P.SetFree(JumpLaunch ? FreeMode.Jump : AirborneDefault());
        }

        /// <summary>The jump velocity to apply, folding in the Jump Height shot (all jumps) and, for a GROUND jump, a
        /// one-shot Slam Spring (consumed here).</summary>
        public float JumpVelocity(bool ground)
        {
            float v = P._jumpVelocity * P.JumpVelocityBonus;
            if (ground && !Mathf.IsEqualApprox(SpringBonus, 1.0f))
            {
                v *= SpringBonus;
                SpringBonus = 1.0f;
            }
            return v;
        }

        /// <summary>A jump in mid-air (also how he catches himself out of a gust).</summary>
        public void AirJump()
        {
            P._gustLeft = 0.0f; // an air jump catches him out of a gust — full air control back (a recovery move)
            SetVelY(JumpVelocity(ground: false));
            AirJumpsUsed += 1;
            P._sfx.Play("jump");
            P._apexY = P.GlobalPosition.Y;
            P._fallPeak = 0.0f;
            JumpLaunch = true;
            P.EnterFree(FreeMode.Jump);
            Sprite.Play("jump");
            Sprite.SetFrameAndProgress(0, 0.0f);
            if (P._particles != null)
            {
                float lean = Mathf.Clamp(Velocity.X / Mathf.Max(P._runSpeedV, 1.0f), -1.0f, 1.0f);
                P._particles.FireEffect("double_jump", lean * DoubleJumpLean);
            }
            foreach (var p in P._passives)
                p.OnAirJump(P);
        }

        /// <summary>Drop down through the one-way platform he's standing on: stop colliding with the Platform layer for
        /// <see cref="DropThroughTime"/> (solid ground stays solid). Only when the floor under him IS a platform — the
        /// tile bodies of a TileMapLayer carry their physics layer's collision layer, so the floor contact tells us.</summary>
        private void DropThroughPlatform()
        {
            const uint platform = (uint)Combat.Layer.Platform;
            var player = P;
            for (int i = 0; i < player.GetSlideCollisionCount(); i++)
            {
                var c = player.GetSlideCollision(i);
                if (c.GetNormal().Dot(player.UpDirection) < 0.5f || (PhysicsServer2D.BodyGetCollisionLayer(c.GetColliderRid()) & platform) == 0)
                    continue; // not a floor contact with a platform
                player.CollisionMask &= ~platform;
                SetVelY(Mathf.Max(Velocity.Y, 60.0f));
                player.GetTree().CreateTimer(DropThroughTime).Timeout += () =>
                {
                    if (IsInstanceValid(player))
                        player.CollisionMask |= platform;
                };
                return;
            }
        }
    }
}
