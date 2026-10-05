using Godot;

namespace MyGame;

/// <summary>
/// Everything an attack delivers to a Hurtbox, in one object so new effects can be added without changing
/// every signature. A Hitbox / Projectile fills one in and hands it to <c>Hurtbox.TakeHit()</c>; the victim
/// reads what it needs. C# port of <c>scripts/combat/hit.gd</c>.
/// </summary>
[GlobalClass]
public partial class Hit : RefCounted
{
    /// <summary>Damage.</summary>
    public float Amount { get; set; }
    /// <summary>px/s shove away from the source (0 = none).</summary>
    public float Knockback { get; set; }
    /// <summary>A GUST (px/s, 0 = none): the hit does NO damage and no stagger — it flings the victim away from the
    /// source (and up, <see cref="Combat.GustLift"/>), and the player keeps control to recover (Ventilator's wind).</summary>
    public float Gust { get; set; }
    /// <summary>Seconds frozen / staggered (0 = none).</summary>
    public float Stun { get; set; }
    /// <summary>Who dealt it (for knockback direction).</summary>
    public Node? Source { get; set; }
    /// <summary>True if this came from a Projectile (ranged) vs a melee/Strike box — lets a victim react differently (e.g. Nasen is only stunned by melee).</summary>
    public bool Ranged { get; set; }
    /// <summary>True if from the player's SPECIAL — a special kill doesn't refill Ruh (RunManager checks this).</summary>
    public bool FromSpecial { get; set; }
    /// <summary>Seconds to CHARM the victim into a temporary ally (0 = none); see Enemy.become_frenemy.</summary>
    public float FrenemyTime { get; set; }
    /// <summary>Optional engulfing overlay tint on the victim (<c>a &gt; 0</c> enables), lasting <see cref="StatusTime"/> s.</summary>
    public Color StatusColor { get; set; } = new(0, 0, 0, 0);
    public float StatusTime { get; set; }
    /// <summary>Optional custom VFX scene spawned ON the victim when this lands (null = none); freed after <see cref="VictimVfxTime"/> s (0 = self-freeing one-shot).</summary>
    public PackedScene? VictimVfx { get; set; }
    public float VictimVfxTime { get; set; }
    /// <summary>Optional damage-over-time ("reap"): fraction of the victim's MAX health drained per 1s tick.</summary>
    public float DotPercent { get; set; }
    /// <summary>How long the reap lasts (re-applying refreshes, never shortens). 0 = no DoT.</summary>
    public float DotTime { get; set; }

    // ----------------------------------------------------------------------------------------------------
    // TRANSITION-ONLY snake_case aliases. GDScript addresses C# members by their exact name, so these let
    // the still-GDScript consumers (player/enemy/abilities/hitbox/projectile) keep using `hit.amount` etc.
    // UNCHANGED until they are ported. DELETE this whole block once no .gd touches a Hit. (See docs/csharp-migration.md.)
    // ----------------------------------------------------------------------------------------------------
    public float amount { get => Amount; set => Amount = value; }
    public float knockback { get => Knockback; set => Knockback = value; }
    public float stun { get => Stun; set => Stun = value; }
    public Node? source { get => Source; set => Source = value; }
    public bool ranged { get => Ranged; set => Ranged = value; }
    public bool from_special { get => FromSpecial; set => FromSpecial = value; }
    public float frenemy_time { get => FrenemyTime; set => FrenemyTime = value; }
    public Color status_color { get => StatusColor; set => StatusColor = value; }
    public float status_time { get => StatusTime; set => StatusTime = value; }
    public PackedScene? victim_vfx { get => VictimVfx; set => VictimVfx = value; }
    public float victim_vfx_time { get => VictimVfxTime; set => VictimVfxTime = value; }
    public float dot_percent { get => DotPercent; set => DotPercent = value; }
    public float dot_time { get => DotTime; set => DotTime = value; }
}
