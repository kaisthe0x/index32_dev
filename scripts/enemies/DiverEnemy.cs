using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// Diver archetype: a floating kamikaze. Drifts + bobs on patrol, and the moment the player enters
/// <see cref="detect_range"/> it LOCKS the player's current position and flies straight at it (attack anim
/// looping) — committing fully, no re-tracking, so dodging makes it miss. On arrival (or on contact) it ERUPTS
/// a one-shot AoE + its death burst and is gone. Killed before it arrives, the death burst still plays; it just
/// doesn't explode. Floats freely (overrides the grounded loop; no gravity/floor/edge patrol). Ein is one
/// instance (an EnemyKits entry). C# port of <c>scripts/enemies/ein.gd</c>, reframed as a type.
/// </summary>
[GlobalClass]
public partial class DiverEnemy : Enemy
{
    [ExportGroup("Diver")]
    [Export] public float detect_range { get; set; } = 220.0f;
    [Export] public float charge_speed { get; set; } = 230.0f;
    [Export] public float arrival_radius { get; set; } = 12.0f;
    [Export] public float bob_amplitude { get; set; } = 6.0f;
    [Export] public float bob_speed { get; set; } = 3.0f;

    [ExportSubgroup("Explosion")]
    [Export] public Vector2 explosion_extents { get; set; } = new(38, 32);
    [Export] public Vector2 explosion_offset { get; set; } = new(0, -16);
    [Export] public float explosion_damage { get; set; } = 18.0f;
    [Export] public float explosion_knockback { get; set; } = 170.0f;
    [Export] public float explosion_stun { get; set; } = 0.2f;

    private float _homeY;
    private float _bobT;
    private Vector2 _chargeTarget;
    private Node? _trail;
    private Area2D _contact = null!;

    public override void _Ready()
    {
        base._Ready();
        CollisionMask = 0; // float freely -- ignore terrain (we move by global_position, not slide)
        _homeY = GlobalPosition.Y;
        BuildContactDetector();
        SetTrail("walk_trail");
        SetState(EState.Patrol);
    }

    /// <summary>A body-sized detector that erupts him the instant the player TOUCHES him (dash i-frames = safe).</summary>
    private void BuildContactDetector()
    {
        _contact = new Area2D { CollisionLayer = 0, CollisionMask = (uint)Combat.Layer.PlayerHurt };
        _contact.AddChild(MakeBox(hurtbox_size, new Vector2(0, -hurtbox_size.Y / 2.0f)));
        AddChild(_contact);
        _contact.AreaEntered += OnContact;
    }

    private void OnContact(Area2D area)
    {
        if (State == EState.Dead)
            return;
        if (area is Hurtbox)
            // Deferred: we're inside the physics area-flush, where arming the blast hitbox is illegal.
            Callable.From(Arrive).CallDeferred();
    }

    /// <summary>Floating AI — replaces the grounded loop entirely (no gravity, floor, or edge patrol).</summary>
    public override void _PhysicsProcess(double delta)
    {
        if (State == EState.Dead)
            return;
        if (State == EState.Charge)
            Charge((float)delta);
        else
            FloatPatrol((float)delta);
    }

    private void FloatPatrol(float delta)
    {
        var player = Player();
        if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= detect_range)
        {
            BeginCharge(player.GlobalPosition);
            return;
        }
        float dir = Mathf.Sign(PatrolTarget - GlobalPosition.X);
        GlobalPosition += new Vector2(dir * move_speed * delta, 0);
        if (Mathf.Abs(PatrolTarget - GlobalPosition.X) <= 2.0f)
            PatrolTarget = Mathf.IsEqualApprox(PatrolTarget, PointB) ? PointA : PointB;
        _bobT += delta;
        GlobalPosition = new Vector2(GlobalPosition.X, _homeY + Mathf.Sin(_bobT * bob_speed) * bob_amplitude);
        if (dir != 0.0f)
            Face((int)dir);
    }

    private void BeginCharge(Vector2 target)
    {
        _chargeTarget = target;
        SetTrail("kamikaze_trail");
        SetState(EState.Charge);
        Play(CloseAnim);
        Face(Mathf.Sign(target.X - GlobalPosition.X));
    }

    /// <summary>Fly straight at the locked point; erupt on arrival. No re-tracking -- he commits.</summary>
    private void Charge(float delta)
    {
        Vector2 to = _chargeTarget - GlobalPosition;
        if (to.Length() <= arrival_radius)
        {
            Arrive();
            return;
        }
        Vector2 step = to.Normalized() * charge_speed;
        GlobalPosition += step * delta;
        if (!Mathf.IsZeroApprox(step.X))
            Face(Mathf.Sign(step.X));
    }

    private void Arrive()
    {
        if (State == EState.Dead)
            return; // contact + arrival could both land the same frame
        SpawnExplosion();
        Die();
    }

    /// <summary>A hit chips + flashes him; lethal -> death burst. No stun/knockback: he commits, never staggered.</summary>
    protected override void OnHurt(Hit hit)
    {
        if (State == EState.Dead)
            return;
        Health = Mathf.Max(Health - hit.Amount, 0.0f);
        Bar.SetRatio(Health / max_health);
        Flash(Sprite);
        if (Health <= 0.0f)
            Die();
    }

    protected override void Die()
    {
        SetTrail(""); // stop trailing before the death burst
        _contact?.SetDeferred(Area2D.PropertyName.Monitoring, false);
        base.Die();
    }

    /// <summary>Build the arrival blast: the `kamikaze` Strike scene into the LEVEL (outlives our death).</summary>
    private void SpawnExplosion()
    {
        SfxPlayAt("ein.kamikaze", GlobalPosition);
        var strike = SpawnAttack(VfxScene("kamikaze"),
            new SegmentData { Damage = explosion_damage, Knockback = explosion_knockback, Stun = explosion_stun },
            true);
        if (strike != null)
            PlaceAt(strike, GlobalPosition);
    }

    /// <summary>Wear the trail for `effect` (config key), or "" to clear it. The old trail dissipates in the level.</summary>
    private void SetTrail(string effect)
    {
        if (_trail is Node2D old && IsInstanceValid(old))
            RetireParticles(old, GetParent());
        _trail = null;
        if (effect == "")
            return;
        _trail = MakeVfx(effect);
        if (_trail != null)
            AddChild(_trail);
    }

    // Inlined Nodes.retire_particles: re-parent into `into` (keep world pos), stop emitters, free once they fade.
    private static void RetireParticles(Node2D node, Node into)
    {
        if (node == null || !IsInstanceValid(node))
            return;
        var tree = node.GetTree();
        if (into != null && IsInstanceValid(into) && node.GetParent() != into)
        {
            Vector2 gpos = node.GlobalPosition;
            node.GetParent().RemoveChild(node);
            into.AddChild(node);
            node.GlobalPosition = gpos;
        }
        float linger = 0.0f;
        if (node is CpuParticles2D rc) { rc.Emitting = false; linger = Mathf.Max(linger, (float)(rc.Lifetime * (1.0 + rc.LifetimeRandomness))); }
        if (node is GpuParticles2D rg) { rg.Emitting = false; linger = Mathf.Max(linger, (float)rg.Lifetime); }
        foreach (var e in node.FindChildren("*", "CpuParticles2D", true, false))
        {
            var em = (CpuParticles2D)e;
            em.Emitting = false;
            linger = Mathf.Max(linger, (float)(em.Lifetime * (1.0 + em.LifetimeRandomness)));
        }
        foreach (var e in node.FindChildren("*", "GpuParticles2D", true, false))
        {
            var em = (GpuParticles2D)e;
            em.Emitting = false;
            linger = Mathf.Max(linger, (float)em.Lifetime);
        }
        if (linger <= 0.0f || tree == null)
            node.QueueFree();
        else
            tree.CreateTimer(linger).Timeout += node.QueueFree;
    }
}
