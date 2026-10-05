namespace MyGame;

/// <summary>
/// How an enemy MOVES / approaches — the second axis alongside <see cref="StrikeType"/> (how it HITS).
/// Advisory kit metadata (like <see cref="EnemyTier"/>): RunManager skips it when applying a kit, it's not an
/// Enemy property. Behaviour still comes from the enemy's script/subclass; this is the design-level tag.
/// </summary>
public enum EnemyMovement
{
    Ground,      // walks toward the player to chase (the default cohort: Kebus, Baghel, Mazab, Matat, Tarri, Breski, Ventilator)
    Flying,      // airborne, ignores gravity/platforms (Ein — the DiverEnemy)
    Stationary,  // holds position, never moves (Nasen — the SleeperEnemy: dormant until triggered, then hits in place)
}
// NOTE: no "Charger" — a lunging enemy isn't a movement type; the lunge lives in the ATTACK (a StrikeType.Lunge /
// Kamikaze attack carries a `Lunge` impulse, like Khalid's Zahluq), so no run/charge animation is needed.
