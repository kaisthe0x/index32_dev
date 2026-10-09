using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Per-enemy MARKER colour for the off-screen enemy arrows (<see cref="OffscreenMarkers"/>). Keyed by EnemyId;
/// an unknown id falls back to a threat red.
/// </summary>
public static class EnemyMarkers
{
    private static readonly Dictionary<string, Color> COLORS = new()
    {
        { EnemyIds.Kebus, new Color(0.90f, 0.68f, 0.24f) },   // tan / gold
        { EnemyIds.Baghel, new Color(0.72f, 0.48f, 0.98f) },  // purple
        { EnemyIds.Nasen, new Color(0.36f, 0.66f, 1.00f) },   // steel blue
        { EnemyIds.Mazab, new Color(1.00f, 0.32f, 0.30f) },   // crimson
        { EnemyIds.Ein, new Color(1.00f, 0.56f, 0.20f) },     // orange
        { EnemyIds.Matat, new Color(1.00f, 0.42f, 0.12f) },   // deep orange-red
        { EnemyIds.Tarri, new Color(1.00f, 0.87f, 0.10f) },   // yellow-gold
        { EnemyIds.Breski, new Color(0.92f, 0.32f, 0.16f) },  // blood-red
        { EnemyIds.Ventilator, new Color(0.72f, 0.94f, 1.00f) },  // pale wind-cyan
    };
    private static readonly Color Fallback = new(1.0f, 0.30f, 0.30f);

    public static Color ColorFor(string enemyId) => COLORS.GetValueOrDefault(enemyId, Fallback);
}
