namespace MyGame;

/// <summary>Which number on Khalid's body a Needle Point shot changes (see <see cref="Shot"/>). Additive stats add their
/// level value; multiplier stats multiply by it.</summary>
public enum ShotStat
{
    Dashes,        // + dash charges
    AirJumps,      // + air jumps
    JumpHeight,    // × jump velocity
    RunSpeed,      // × run speed
    Reach,         // × attack reach
    AttackDamage,  // × attack damage
    SlamDamage,    // × slam damage
}
