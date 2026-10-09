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
    public float run_speed = 160.0f;
    public float acceleration = 1200.0f;
    public float friction = 1400.0f;
    public float run_anim_speed = 1.5f;
    // jump / vertical arc / landing
    public float jump_velocity = -330.0f;
    public int air_jumps = 2;
    public float gravity = 900.0f;
    public float fall_gravity_scale = 1.35f;
    public float land_min_fall_speed = 140.0f;
    public float land_predict_distance = 22.0f;
    // dash
    public float dash_speed = 420.0f;
    public float dash_time = 0.18f;
    public float dash_cooldown = 0.45f;
    public float dash_anim_time = 0.30f;
    public float dash_gravity_scale = 0.35f;
    public bool blink = false;
    // slam
    public float slam_speed = 1200.0f;
    public float slam_min_clearance = 50.0f;
    public int slam_hold_frame = 2;
    public float slam_impact_distance = 30.0f;
    public float slam_min_drop = 120.0f;
    public float slam_max_drop = 700.0f;
    public float slam_max_damage_mult = 2.5f;
}
