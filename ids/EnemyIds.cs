namespace MyGame;

/// <summary>
/// Stable string IDs for the enemy roster. `const string` (see <see cref="AttackIds"/>): the id IS the runtime
/// <c>enemy_id</c> AND the key into the EmittersEnemies / SfxEnemies / EnemyMarkers tables — nothing to convert.
/// Reference them (<c>EnemyIds.Kebus</c>) instead of raw string literals.
/// </summary>
public static class EnemyIds
{
    public const string Kebus = "kebus";
    public const string Baghel = "baghel";
    public const string Mazab = "mazab";
    public const string Nasen = "nasen";
    public const string Ein = "ein";
    public const string Matat = "matat";
    public const string Tarri = "tarri";
    public const string Breski = "breski";
    public const string Ventilator = "ventilator";

    // Wardens (elite tier — WardenEnemy, resources/wardens/, docs/game-loop.md)
    public const string Kroj = "kroj";
}
