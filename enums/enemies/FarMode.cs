namespace MyGame;

/// <summary>How an enemy's FAR attack (its projectile) is launched — set per kit (<see cref="Enemy.far_mode"/>).</summary>
public enum FarMode
{
    Aimed,      // tracks the player and aims at his body (tilt capped by far_aim_cap) — Kebus
    Forward,    // flies straight ahead, the way the enemy faces
    GroundWave, // rolls forward along the terrain surface — Baghel
    Lob,        // an arcing bomb that lands, dwells, then bursts — Mazab
}
