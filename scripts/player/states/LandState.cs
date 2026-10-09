using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>
    /// Landing after a real fall: the land animation, which starts a moment BEFORE touchdown (when the ground is
    /// close and he is falling fast enough) and can be cut short by any action. In the air he can still slam, attack
    /// (if the attack works in the air), dash or air-jump out of it; on the ground everything is available.
    /// </summary>
    private sealed class LandState : PlayerState
    {
        public LandState(Player player) : base(player) { }

        public override StringName Animation => "land";

        public override void Tick(float delta)
        {
            if (!OnFloor)
            {
                if (Velocity.Y <= 0.0f || !P.NearGround())
                {
                    P.EnterFree(P._free.AirborneDefault());
                    return;
                }
                AddVelY(P._gravity * P._fallGravityScale * delta);
                Brake(delta);
                if (Input.IsActionJustPressed("special") && P._slam.CanStart())
                {
                    P.Enter(P._slam);
                    return;
                }
                if (AttackHeld() && P.AirAttackOk())
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
                if (Input.IsActionJustPressed("jump") && P._free.AirJumpsUsed < P._maxAirJumps)
                    P._free.AirJump();
                return;
            }

            if (Input.IsActionJustPressed("special") && P._currentSpecial != null)
            {
                P._special.Start();
                return;
            }
            if (AttackHeld())
            {
                P._attack.Advance();
                return;
            }
            if (Input.IsActionJustPressed("dash") && P._dashCharges > 0)
            {
                P.Enter(P._dash);
                return;
            }
            if (Input.IsActionJustPressed("jump"))
            {
                SetVelY(P._free.JumpVelocity(ground: true));
                P._free.JumpLaunch = true;
                P.SetFree(FreeMode.Jump);
                return;
            }

            float input = Input.GetAxis("move_left", "move_right");
            if (input != 0.0f)
            {
                P._facing = input > 0.0f ? 1 : -1;
                SetVelX(Mathf.MoveToward(Velocity.X, input * P.RunSpeed(), P._acceleration * delta));
                P.SetFree(FreeMode.Run);
                return;
            }
            Brake(delta);
        }
    }
}
