using Godot;

namespace MyGame;

/// <summary>
/// A spawned attack that fights for one side and remembers who made it — <see cref="Strike"/> and
/// <see cref="Projectile"/>, the two kinds of scene <see cref="Enemy.SpawnAttack"/> spawns. Lets the spawner set the
/// side and the owner typed, instead of by property name.
///
/// <para>The members keep the snake_case names those classes already expose; they are renamed with the rest of the
/// GDScript-era surface (docs/standards.md, Known debt).</para>
/// </summary>
public interface ISidedAttack
{
    /// <summary>false = a player attack (hits enemies); true = an enemy attack (hits the player).</summary>
    bool hostile { get; set; }

    /// <summary>When true the attack also hits its own side (never its own <see cref="source"/>).</summary>
    bool friendly_fire { get; set; }

    /// <summary>The body that made the attack (credited for the hit, and the origin of knockback).</summary>
    Node? source { get; set; }
}
