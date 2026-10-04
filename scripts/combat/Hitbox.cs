using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// A region that DEALS damage. While active it scans for Hurtboxes (its <c>CollisionMask</c> = the opposing
/// team's hurt layer) and damages each once per activation. Re-activating (a new swing) clears the memory so
/// it can hit again. Melee-style boxes toggle on for their active frames via <c>activate()</c>/<c>deactivate()</c>;
/// a projectile just leaves it active for its whole life. C# port of <c>scripts/combat/hitbox.gd</c>.
///
/// NAMING: the public surface keeps the original <c>snake_case</c> (<c>damage</c>, <c>source</c>,
/// <c>activate</c>, <c>struck</c>, …) so the still-GDScript callers AND the <c>.tscn</c>-authored
/// <c>[Export]</c> values keep working UNCHANGED through the migration. It becomes idiomatic PascalCase in the
/// final cleanup pass, once every caller is C#. Internals are idiomatic now.
/// </summary>
[GlobalClass]
public partial class Hitbox : Area2D
{
    [Export] public float damage { get; set; } = 10.0f;
    [Export] public float knockback { get; set; }
    [Export] public float gust { get; set; }
    [Export] public float stun { get; set; }
    [Export] public Color status_color { get; set; } = new(0, 0, 0, 0);
    [Export] public float status_time { get; set; }
    [Export] public PackedScene? victim_vfx { get; set; }
    [Export] public float victim_vfx_time { get; set; }
    [Export] public bool ranged { get; set; }
    [Export] public bool from_special { get; set; }
    [Export] public float frenemy_time { get; set; }
    [Export] public float dot_percent { get; set; }
    [Export] public float dot_time { get; set; }

    /// <summary>Who fired this, passed along so the victim knocks back away from them.</summary>
    public Node? source;

    /// <summary>Emitted when this box connects with a Hurtbox — lets a projectile free on impact.</summary>
    [Signal]
    public delegate void struckEventHandler(Hurtbox victim);

    private readonly List<Hurtbox> _alreadyHit = new();

    // Cumulative "did this activation connect with anyone" — reset on activate(), set on a hit, and (unlike
    // _alreadyHit) NOT cleared by pulse(), so a multi-pulse DoT still reads true. Read on deactivate() for a WHIFF.
    private bool _connectedSinceActivate;

    public override void _Ready()
    {
        AreaEntered += OnAreaEntered;
        // Off until explicitly activated; scanning a stale overlap on spawn is a common phantom-hit source.
        Monitoring = false;
    }

    /// <summary>
    /// Turn the box on until <see cref="deactivate"/> (an attack's active frames, or a projectile's whole
    /// life). Parameterless because GDScript does NOT honour C# default parameters — the timed variant is
    /// <see cref="ActivateTimed"/>.
    /// </summary>
    public void activate()
    {
        _alreadyHit.Clear();
        _connectedSinceActivate = false;
        Monitoring = true;
    }

    /// <summary>Activate, then auto-deactivate after <paramref name="duration"/> seconds (a discrete strike).</summary>
    public void ActivateTimed(float duration)
    {
        activate();
        if (duration > 0.0f)
            GetTree().CreateTimer(duration).Timeout += deactivate;
    }

    /// <summary>Turn the box off — the end of a swing's active frames, or a projectile expiring. A PLAYER attack box
    /// (source is the Player) that struck nobody this activation is a WHIFF → notify the player (OnMiss buffs).
    /// Gated to non-special boxes so surges/specials don't feed attack-miss procs.</summary>
    public void deactivate()
    {
        bool whiffed = Monitoring && !_connectedSinceActivate && !from_special;
        Monitoring = false;
        if (whiffed && GodotObject.IsInstanceValid(source) && source is Player p)
            p.notify_miss();
    }

    /// <summary>
    /// Re-deal to every Hurtbox CURRENTLY inside the box — one pulse of a ticking/DoT field. <c>AreaEntered</c>
    /// only fires on ENTER, so a target standing still would never be hit again; this clears the per-hit memory
    /// and re-delivers to whoever's overlapping now. Walk out and you stop taking it. No-op while off.
    /// </summary>
    public void pulse()
    {
        if (!Monitoring)
            return;
        _alreadyHit.Clear();
        foreach (var area in GetOverlappingAreas())
            OnAreaEntered(area);
    }

    /// <summary>
    /// On first overlap with a Hurtbox this activation, build a <see cref="Hit"/> from this box's fields
    /// (damage/knockback/stun/status + source) and deliver it.
    /// </summary>
    private void OnAreaEntered(Area2D area)
    {
        if (area is not Hurtbox box || _alreadyHit.Contains(box))
            return;
        // Never hit our own source's hurtbox (harmless normally — teams don't overlap — but a friendly-fire box
        // would otherwise damage the attacker). IsInstanceValid, not != null: a shot outlives its firer, so
        // `source` may be a FREED ref (which isn't null).
        if (GodotObject.IsInstanceValid(source) && box.GetParent() == source)
            return;
        _alreadyHit.Add(box);
        _connectedSinceActivate = true;
        var hit = new Hit
        {
            Amount = damage,
            Knockback = knockback,
            Gust = gust,
            Stun = stun,
            StatusColor = status_color,
            // Default the status window to the stun duration.
            StatusTime = status_time > 0.0f ? status_time : stun,
            VictimVfx = victim_vfx,
            Ranged = ranged,
            FromSpecial = from_special,
            FrenemyTime = frenemy_time,
            DotPercent = dot_percent,
            DotTime = dot_time,
        };
        // Default the VFX lifetime to the status/stun window so e.g. a stun effect lasts the whole stun.
        hit.VictimVfxTime = victim_vfx_time > 0.0f ? victim_vfx_time : hit.StatusTime;
        Node? credit = GodotObject.IsInstanceValid(source) ? source : Owner;
        hit.Source = GodotObject.IsInstanceValid(credit) ? credit : null;
        box.take_hit(hit);
        EmitSignal(SignalName.struck, box);
    }
}
