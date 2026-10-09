using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// The single entry point for particle-emitter config, characters AND enemies — aggregates
/// <see cref="EmittersCharacters"/> + <see cref="EmittersEnemies"/> so callers have one place to look. Characters
/// are keyed id → animation → rows and driven by <see cref="ParticleDirector"/> on animation frames; enemies are
/// keyed id → effect → row and attached in code by state/event (no frame scheduling). Both use <see cref="EmitterDef"/>.
/// </summary>
public static class Emitters
{
    private static readonly Dictionary<string, EmitterDef[]> NoRows = new();

    /// <summary>Every animation's rows for a character id (empty if the character has none).</summary>
    public static IReadOnlyDictionary<string, EmitterDef[]> Character(string id) =>
        EmittersCharacters.TABLE.TryGetValue(id, out var byAnim) ? byAnim : NoRows;

    /// <summary>One enemy effect's row, or null if unlisted (an absent row = no such emitter).</summary>
    public static EmitterDef? EnemyEffect(string id, string effect) =>
        EmittersEnemies.TABLE.TryGetValue(id, out var effects) && effects.TryGetValue(effect, out var row) ? row : null;
}
