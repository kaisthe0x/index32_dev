using Godot;

namespace MyGame;

/// <summary>
/// Shared combat constants + team-layer helpers — the C# port of <c>configs/combat.gd</c>.
///
/// During the migration the GDScript <c>Combat</c> (a global <c>class_name</c>) STAYS: GDScript cannot
/// read C# statics, so its consumers keep using it until they are themselves ported. This C# copy is the
/// bedrock the ported C# combat classes build on. The two are kept in lock-step (stable collision bits),
/// and the GDScript one is deleted once no <c>.gd</c> references it. This is a plain static class (not a
/// Godot type / not <c>[GlobalClass]</c>) — it is used by C# only.
/// </summary>
public static class Combat
{
    /// <summary>
    /// Collision-layer bits, mirroring project.godot's 2d_physics layer names. Bodies stand on
    /// <see cref="Layer.World"/>; damage is dealt by Hitboxes (masking the opposing team's Hurtbox layer)
    /// landing on Hurtboxes — teams never touch, so no friendly fire and no group checks.
    /// </summary>
    [System.Flags]
    public enum Layer : uint
    {
        World = 1u << 0,      // floor / terrain
        PlayerBody = 1u << 1,
        EnemyBody = 1u << 2,
        PlayerHurt = 1u << 3, // player receives hits here
        EnemyHurt = 1u << 4,  // enemies receive hits here
        PlayerHit = 1u << 5,  // player attack boxes / friendly projectiles
        EnemyHit = 1u << 6,   // enemy attack boxes / hostile projectiles
    }

    /// <summary>
    /// Layer an attack box / projectile lives on. Friendly (player) boxes hit enemies; hostile boxes hit
    /// the player. Returns the raw <c>uint</c> Godot's <c>CollisionObject2D.CollisionLayer</c> expects.
    /// </summary>
    public static uint HitLayer(bool hostile) =>
        (uint)(hostile ? Layer.EnemyHit : Layer.PlayerHit);

    /// <summary>
    /// Which hurt layer(s) an attack box scans. Normally just the OPPOSING team's; with
    /// <paramref name="friendlyFire"/> it also scans its OWN team's hurt layer (the Hitbox still skips its
    /// own <c>source</c>, so the attacker never hits itself). Per-attacker, not a global toggle.
    /// </summary>
    public static uint HurtMask(bool hostile, bool friendlyFire = false)
    {
        Layer mask = hostile ? Layer.PlayerHurt : Layer.EnemyHurt;
        if (friendlyFire)
            mask |= hostile ? Layer.EnemyHurt : Layer.PlayerHurt;
        return (uint)mask;
    }

    // --- combat feel (shared by Player and Enemy hit reactions) ---
    /// <summary>Upward pop on a knockback, as a fraction of the horizontal shove, so a hit lifts the victim a little and reads.</summary>
    public const float KnockbackPop = 0.25f;
    /// <summary>A knockback always freezes the victim at least this long, or the AI/input overwrites the shove next frame.</summary>
    public const float MinStagger = 0.18f;
    /// <summary>How long a discrete melee strike's hitbox stays live for one swing.</summary>
    /// <summary>Red tint a hit flashes, fading back over <see cref="HitFlashTime"/>.</summary>
    public static readonly Color HitFlash = new(1.0f, 0.4f, 0.4f);
    public const float HitFlashTime = 0.16f;
}
