using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// A region that DEALS damage. While active it scans for Hurtboxes (its <c>CollisionMask</c> = the opposing
/// team's hurt layer) and damages each once per activation. Re-activating (a new swing) clears the memory so
/// it can hit again. Melee-style boxes toggle on for their active frames via <c>activate()</c>/<c>deactivate()</c>;
/// a projectile just leaves it active for its whole life. C# port of <c>scripts/combat/hitbox.gd</c>.
///
/// The <c>[Export]</c> names are also the property keys in the <c>.tscn</c> files that set them: rename one here
/// and every scene that sets it must be renamed with it.
/// </summary>
[GlobalClass]
public partial class Hitbox : Area2D
{
    [Export] public float Damage { get; set; } = 10.0f;
    [Export] public float Knockback { get; set; }
    [Export] public float Gust { get; set; }
    [Export] public float Stun { get; set; }
    [Export] public Color StatusColor { get; set; } = new(0, 0, 0, 0);
    [Export] public float StatusTime { get; set; }
    [Export] public PackedScene? VictimVfx { get; set; }
    [Export] public float VictimVfxTime { get; set; }
    [Export] public bool Ranged { get; set; }
    [Export] public bool FromSpecial { get; set; }
    [Export] public float FrenemyTime { get; set; }
    [Export] public float DotPercent { get; set; }
    [Export] public float DotTime { get; set; }

    /// <summary>Who fired this, passed along so the victim knocks back away from them.</summary>
    public Node? Source;

    /// <summary>Emitted when this box connects with a Hurtbox — lets a projectile free on impact.</summary>
    [Signal]
    public delegate void StruckEventHandler(Hurtbox victim);

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
    /// Turn the box on until <see cref="Deactivate"/> (an attack's active frames, or a projectile's whole
    /// life). Parameterless because GDScript does NOT honour C# default parameters — the timed variant is
    /// <see cref="ActivateTimed"/>.
    /// </summary>
    public void Activate()
    {
        _alreadyHit.Clear();
        _connectedSinceActivate = false;
        Monitoring = true;
    }

    /// <summary>Activate, then auto-deactivate after <paramref name="duration"/> seconds (a discrete strike).</summary>
    public void ActivateTimed(float duration)
    {
        Activate();
        if (duration > 0.0f)
            GetTree().CreateTimer(duration).Timeout += Deactivate;
    }

    /// <summary>Turn the box off — the end of a swing's active frames, or a projectile expiring. A PLAYER attack box
    /// (source is the Player) that struck nobody this activation is a WHIFF → notify the player (OnMiss buffs).
    /// Gated to non-special boxes so surges/specials don't feed attack-miss procs.</summary>
    public void Deactivate()
    {
        bool whiffed = Monitoring && !_connectedSinceActivate && !FromSpecial;
        Monitoring = false;
        if (whiffed && GodotObject.IsInstanceValid(Source) && Source is Player p)
            p.NotifyMiss();
    }

    /// <summary>
    /// Re-deal to every Hurtbox CURRENTLY inside the box — one pulse of a ticking/DoT field. <c>AreaEntered</c>
    /// only fires on ENTER, so a target standing still would never be hit again; this clears the per-hit memory
    /// and re-delivers to whoever's overlapping now. Walk out and you stop taking it. No-op while off.
    /// </summary>
    public void Pulse()
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
        if (GodotObject.IsInstanceValid(Source) && box.GetParent() == Source)
            return;
        _alreadyHit.Add(box);
        _connectedSinceActivate = true;
        var hit = new Hit
        {
            Amount = Damage,
            Knockback = Knockback,
            Gust = Gust,
            Stun = Stun,
            StatusColor = StatusColor,
            // Default the status window to the stun duration.
            StatusTime = StatusTime > 0.0f ? StatusTime : Stun,
            VictimVfx = VictimVfx,
            Ranged = Ranged,
            FromSpecial = FromSpecial,
            FrenemyTime = FrenemyTime,
            DotPercent = DotPercent,
            DotTime = DotTime,
        };
        // Default the VFX lifetime to the status/stun window so e.g. a stun effect lasts the whole stun.
        hit.VictimVfxTime = VictimVfxTime > 0.0f ? VictimVfxTime : hit.StatusTime;
        Node? credit = GodotObject.IsInstanceValid(Source) ? Source : Owner;
        hit.Source = GodotObject.IsInstanceValid(credit) ? credit : null;
        box.TakeHit(hit);
        EmitSignal(SignalName.Struck, box);
    }
}
