namespace MyGame;

/// <summary>How an enemy's FAR attack (its projectile) is launched — set per kit (<see cref="Enemy.FarMode"/>).</summary>
public enum FarMode
{
    Aimed,      // tracks the player and aims at his body (tilt capped by FarAimCap) — Kebus
    Forward,    // flies straight ahead, the way the enemy faces
    GroundWave, // rolls forward along the terrain surface — Baghel
    Lob,        // an arcing bomb that lands, dwells, then bursts — Mazab
}
