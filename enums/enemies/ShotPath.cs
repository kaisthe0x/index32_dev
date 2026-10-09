namespace MyGame;

/// <summary>How a <see cref="ShotAttack"/>'s projectile flies.</summary>
public enum ShotPath
{
    Aimed,      // at the target's body, tracking his elevation (tilt capped by ShotAttack.AimCap) — Kebus
    Forward,    // straight ahead, the way the enemy faces
    GroundWave, // rolls forward along the terrain surface — Baghel
}
