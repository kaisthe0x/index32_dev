using Godot;

namespace MyGame;

/// <summary>
/// The "Locomotion" component of a movement Action (run/jump/dash/slam) — every movement/physics knob. The
/// values below are the shared BASELINE; a character's movement Action sets only the fields it deviates on
/// (see <see cref="ActionsKhalid"/>).
/// </summary>
public partial class Locomotion : RefCounted
{
    // run
    public float RunSpeed = 160.0f;
    public float Acceleration = 1200.0f;
    public float Friction = 1400.0f;
    public float RunAnimSpeed = 1.5f;
    // jump / vertical arc / landing
    public float JumpVelocity = -330.0f;
    public int AirJumps = 2;
    public float Gravity = 900.0f;
    public float FallGravityScale = 1.35f;
    public float LandMinFallSpeed = 140.0f;
    public float LandPredictDistance = 22.0f;
    // dash
    public float DashSpeed = 420.0f;
    public float DashTime = 0.18f;
    public float DashCooldown = 0.45f;
    public float DashAnimTime = 0.30f;
    public float DashGravityScale = 0.35f;
    public bool Blink = false;
    // slam
    public float SlamSpeed = 1200.0f;
    public float SlamMinClearance = 50.0f;
    public int SlamHoldFrame = 2;
    public float SlamImpactDistance = 30.0f;
    public float SlamMinDrop = 120.0f;
    public float SlamMaxDrop = 700.0f;
    public float SlamMaxDamageMult = 2.5f;
}
