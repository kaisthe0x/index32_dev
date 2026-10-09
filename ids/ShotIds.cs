namespace MyGame;

/// <summary>Stable string IDs for the Needle Point shots (see <see cref="AttackIds"/> for why const string, not enum):
/// the id is the key into <see cref="NeedlePoint.Shots"/>.</summary>
public static class ShotIds
{
    public const string Dash = "dash";
    public const string AirJump = "air_jump";
    public const string JumpHeight = "jump_height";
    public const string RunSpeed = "run_speed";
    public const string Reach = "reach";
    public const string AttackDamage = "attack_damage";
    public const string SlamDamage = "slam_damage";
}
