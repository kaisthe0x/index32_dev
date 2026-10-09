using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// A flying attack — player OR enemy (team-agnostic via <see cref="Hostile"/>). It travels (straight or homing
/// toward a target), carries a <see cref="Hitbox"/> that damages the opposing team, and frees itself on a hit
/// or when it runs out of range/life. C# port of <c>scripts/combat/projectile.gd</c>.
///
/// Spawned as a SCENE (player, fired by the ParticleDirector) or in CODE (<see cref="Enemy"/> sets velocity/params).
/// The scenes author the <c>[Export]</c>s by name — rename one and its scenes must follow.
/// </summary>
[GlobalClass]
public partial class Projectile : Node2D, ITunable, ISidedAttack
{
    [Export] public bool Hostile { get; set; }
    [Export] public bool FriendlyFire { get; set; }

    [ExportGroup("Motion")]
    [Export] public float Speed { get; set; } = 420.0f;
    [Export] public float Homing { get; set; } = 6.0f;
    [Export] public float MaxRange { get; set; }
    [Export] public float MaxLife { get; set; }
    [Export] public float AcquireRange { get; set; } = 420.0f;
    [Export] public bool CanFlyUp { get; set; }
    [Export] public float VerticalReach { get; set; } = 40.0f;
    [Export] public bool RotateToHeading { get; set; } = true;

    [ExportGroup("Bounce")]
    [Export] public int Bounces { get; set; }
    [Export] public float BounceHoming { get; set; } = 8.0f;
    [Export] public float BounceRange { get; set; }

    [ExportGroup("Look / lifecycle")]
    /// <summary>Positional Sfx cue played at the contact point on hit ("" = silent). Same pattern as LobProjectile.ExplosionSfx.</summary>
    [Export] public string ImpactSfx { get; set; } = "";
    /// <summary>Optional drawn END animation played in place on expiry (dissolve instead of a blink-out).</summary>
    [Export] public SpriteFrames? EndFrames { get; set; }
    /// <summary>Lay red embers along the floor as it rolls past (a ground surge scorch trail).</summary>
    [Export] public bool GroundTrail { get; set; }

    /// <summary>Ride the terrain surface as it travels — snap onto the ground each frame + tilt to the slope, so a
    /// forward "ground wave" hugs curves instead of flying flat. It dissipates when it runs off the ground (a ledge).</summary>
    [Export] public bool GroundFollow { get; set; }
    /// <summary>How high above the sampled surface the wave rides (its particles/hitbox are authored around this).</summary>
    [Export] public float GroundFollowOffset { get; set; }

    private const float GroundFollowReach = 40.0f;

    /// <summary>Who fired it (knockback credit); set by the spawner.</summary>
    public Node? Source { get; set; }
    /// <summary>A straight (homing == 0) shot moves by this; set by the spawner. A homing shot derives its own dir.</summary>
    public Vector2 Velocity = Vector2.Zero;

    private Vector2 _dir = Vector2.Right;
    private float _traveled;
    private float _life;
    private Node2D? _target;
    private bool _acquired;
    private bool _dying;
    private Vector2 _launchDir = Vector2.Right;
    private int _bouncesLeft;
    private readonly List<Node> _hitTargets = new();

    public override void _Ready()
    {
        AddToGroup("projectiles"); // so a respawn can clear in-flight shots
        _bouncesLeft = Bounces;
        if (Velocity.Length() > 0.01f)
        {
            // The spawner (enemy) gave an explicit velocity: derive heading + speed from it.
            _dir = Velocity.Normalized();
            Speed = Velocity.Length();
        }
        else
        {
            // Director-fired (player): facing came in as scale.x. Read forward, normalise the sign.
            _dir = new Vector2(Scale.X < 0.0f ? -1.0f : 1.0f, 0.0f);
        }
        _launchDir = _dir;
        var sc = Scale;
        sc.X = Mathf.IsZeroApprox(sc.X) ? 1.0f : Mathf.Abs(sc.X);
        Scale = sc;
        Orient();

        var hb = FindHitbox();
        if (hb != null)
        {
            hb.CollisionLayer = Combat.HitLayer(Hostile);
            hb.CollisionMask = Combat.HurtMask(Hostile, FriendlyFire);
            hb.Ranged = true; // flag every projectile hit as ranged (nasen etc. react by type)
            hb.Source = Source;
            hb.Struck += OnStruck;
            hb.Activate(); // a projectile leaves its box live for its whole flight
        }

        if (GroundTrail)
            AddChild(MakeGroundTrail(SampleVisualColor()));
        // Target acquired on the first physics tick, NOT here (the spawner snaps us to the muzzle after add_child).
    }

    /// <summary>Face the heading: a drawn shot ROTATES; a shot authored blasting +x MIRRORS via scale.x (no 180 flip).</summary>
    private void Orient()
    {
        if (RotateToHeading)
        {
            Rotation = _dir.Angle();
        }
        else
        {
            var sc = Scale;
            sc.X = _dir.X < 0.0f ? -Mathf.Abs(sc.X) : Mathf.Abs(sc.X);
            Scale = sc;
        }
    }

    /// <summary>Ground-follow: drop onto the terrain surface beneath the wave and tilt to its normal (facing stays on
    /// Scale.X from <see cref="Orient"/>). False when the ground ran out here (a ledge / pit) so the caller ends it.</summary>
    private bool SnapToGround()
    {
        var space = GetWorld2D()?.DirectSpaceState;
        if (space == null)
            return true; // can't sample -> keep travelling rather than vanishing
        if (!GroundProbe.TryAt(space, GlobalPosition.X, GlobalPosition.Y, GroundFollowReach, out Vector2 point, out Vector2 normal))
            return false;
        GlobalPosition = new Vector2(GlobalPosition.X, point.Y - GroundFollowOffset);
        Rotation = normal.Angle() + Mathf.Pi / 2.0f; // stand perpendicular to the surface
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;
        if (_dying)
            return;
        if (Homing > 0.0f)
        {
            if (!_acquired)
            {
                _target = NearestTargetAhead();
                _acquired = true;
            }
            if (TargetAlive())
            {
                Node2D? aim = AimPoint(_target!);
                Vector2 want = (aim ?? _target!).GlobalPosition - GlobalPosition;
                if (!CanFlyUp && want.Y < 0.0f)
                    want.Y = 0.0f; // track a level/lower target, never steer upward
                if (want.Length() > 0.01f)
                    _dir = _dir.Slerp(want.Normalized(), Mathf.Clamp(Homing * d, 0.0f, 1.0f));
            }
            else
            {
                // No target ahead -> stop homing and fly straight along the launch heading.
                Homing = 0.0f;
                _dir = _launchDir;
            }
        }
        if (!CanFlyUp && _dir.Y < 0.0f)
        {
            _dir = new Vector2(_dir.X, 0.0f); // hard floor: never travel upward
            _dir = _dir.Length() > 0.01f ? _dir.Normalized() : Vector2.Right;
        }
        GlobalPosition += _dir * Speed * d;
        Orient();
        if (GroundFollow && !SnapToGround())
        {
            Expire(); // ran off the ground (a ledge / pit) -> the wave dissipates
            return;
        }
        _traveled += Speed * d;

        _life += d;
        if ((MaxRange > 0.0f && _traveled >= MaxRange) || (MaxLife > 0.0f && _life >= MaxLife))
            Expire();
    }

    /// <summary>
    /// Configure this shot's hitbox from a resolved tuning dict. Called by the spawner after add_child. Absent
    /// fields keep the hitbox's authored values (the cherry_shots case, where two shots carry their own damage).
    /// </summary>
    public void ApplyTuning(SegmentData t, Node? striker)
    {
        if (striker != null)
            Source = striker;
        var hb = FindHitbox();
        if (hb == null)
            return;
        if (t.Damage.HasValue) hb.Damage = t.Damage.Value;
        if (t.Knockback.HasValue) hb.Knockback = t.Knockback.Value;
        if (t.Gust.HasValue) hb.Gust = t.Gust.Value;
        if (t.Stun.HasValue) hb.Stun = t.Stun.Value;
        if (t.Color.HasValue)
        {
            hb.StatusColor = t.Color.Value;
            hb.StatusTime = t.ColorTime ?? t.Stun ?? 0.0f;
        }
        if (t.FromSpecial.HasValue) hb.FromSpecial = t.FromSpecial.Value;
        if (t.Reap.HasValue)
        {
            hb.DotPercent = t.Reap.Value;
            hb.DotTime = t.ReapTime ?? 0.0f;
        }
    }

    /// <summary>Nearest target AHEAD in the facing x-direction, within AcquireRange. Opposing-team group.</summary>
    private Node2D? NearestTargetAhead()
    {
        float facing = _dir.X >= 0.0f ? 1.0f : -1.0f;
        string group = Hostile ? "player" : "enemies";
        Node2D? best = null;
        float bestD = AcquireRange;
        foreach (var e in GetTree().GetNodesInGroup(group))
        {
            if (e is not Node2D n)
                continue;
            Node2D? aim = AimPoint(n);
            if (aim == null)
                continue;
            Vector2 to = aim.GlobalPosition - GlobalPosition;
            if (to.X * facing <= 0.0f)
                continue; // behind us in x
            if (!CanFlyUp && Mathf.Abs(to.Y) > VerticalReach)
                continue; // off our level
            float dist = Mathf.Abs(to.X);
            if (dist < bestD)
            {
                bestD = dist;
                best = n;
            }
        }
        return best;
    }

    /// <summary>Still a live target? Valid AND still in its group (an enemy leaves "enemies" the instant it dies).</summary>
    private bool TargetAlive()
    {
        if (!IsInstanceValid(_target))
            return false;
        return _target!.IsInGroup(Hostile ? "player" : "enemies");
    }

    /// <summary>What the shot homes to for `target`: its hurtbox's collision-shape (the torso). Falls back to the target.</summary>
    private Node2D? AimPoint(Node2D? target)
    {
        if (target == null)
            return null;
        foreach (var a in target.FindChildren("*", "Area2D", true, false))
            if (a is Hurtbox hurt)
            {
                var shapes = hurt.FindChildren("*", "CollisionShape2D", true, false);
                if (shapes.Count > 0)
                    return (Node2D)shapes[0];
            }
        return target;
    }

    private Hitbox? FindHitbox()
    {
        foreach (var a in FindChildren("*", "Area2D", true, false))
            if (a is Hitbox hb)
                return hb;
        return null;
    }

    /// <summary>Hit something: drop the impact + clang at the contact point, then RICOCHET to the next un-hit target or die.</summary>
    private void OnStruck(Hurtbox victim)
    {
        Vector2 at = HitPoint(victim);
        SpawnImpact(at);
        if (ImpactSfx != "")
            GetNodeOrNull<Sfx>("/root/Sfx")?.PlayAt(ImpactSfx, at);

        Node struckEnemy = victim.GetParent();
        if (struckEnemy != null && !_hitTargets.Contains(struckEnemy))
            _hitTargets.Add(struckEnemy);

        if (_bouncesLeft > 0)
        {
            Node2D? next = NearestBounceTarget();
            if (next != null)
            {
                _bouncesLeft -= 1;
                _target = next;
                _acquired = true;
                Homing = Mathf.Max(Homing, BounceHoming);
                Node2D? aim = AimPoint(next);
                Vector2 to = (aim ?? next).GlobalPosition - GlobalPosition;
                if (to.Length() > 0.01f)
                    _dir = to.Normalized();
                _launchDir = _dir;
                _traveled = 0.0f; // each ricochet leg gets a fresh MaxRange budget
                Orient();
                return; // keep flying
            }
        }
        QueueFree();
    }

    /// <summary>Nearest UN-hit opposing-team member for a ricochet, in ANY direction (a bounce can reverse).</summary>
    private Node2D? NearestBounceTarget()
    {
        string group = Hostile ? "player" : "enemies";
        float reach = BounceRange > 0.0f ? BounceRange : AcquireRange;
        Node2D? best = null;
        float bestD = reach;
        foreach (var e in GetTree().GetNodesInGroup(group))
        {
            if (e is not Node2D n || _hitTargets.Contains(n))
                continue;
            Node2D? aim = AimPoint(n);
            if (aim == null)
                continue;
            Vector2 to = aim.GlobalPosition - GlobalPosition;
            if (!CanFlyUp && Mathf.Abs(to.Y) > VerticalReach)
                continue;
            float dist = to.Length();
            if (dist < bestD)
            {
                bestD = dist;
                best = n;
            }
        }
        return best;
    }

    /// <summary>Reached max range/life without hitting: dissolve via EndFrames, else fade any trail, else vanish.</summary>
    private void Expire()
    {
        if (_dying)
            return;
        _dying = true;
        var hb = FindHitbox();
        if (hb != null)
        {
            hb.SetDeferred(Area2D.PropertyName.Monitoring, false);
            hb.SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 0);
        }
        Velocity = Vector2.Zero;

        if (EndFrames != null)
        {
            AnimatedSprite2D? spr = FindSprite();
            if (spr == null) // a particle-only shot -- make a sprite to play the dissolve on
            {
                spr = new AnimatedSprite2D();
                AddChild(spr);
            }
            foreach (var em in Emitters())
                em.Emitting = false;
            spr.SpriteFrames = EndFrames;
            spr.Play("default");
            spr.AnimationFinished += QueueFree;
            GetTree().CreateTimer(3.0).Timeout += QueueFree; // safety net; QueueFree on a freed self is a no-op
            return;
        }

        var emitters = Emitters();
        if (emitters.Count == 0)
        {
            QueueFree();
            return;
        }
        float linger = 0.15f;
        foreach (var em in emitters)
        {
            em.Emitting = false;
            linger = Mathf.Max(linger, (float)(em.Lifetime * (1.0 + em.LifetimeRandomness)));
        }
        var tw = CreateTween();
        tw.TweenProperty(this, "modulate:a", 0.0, linger);
        tw.TweenCallback(Callable.From(QueueFree));
    }

    private AnimatedSprite2D? FindSprite()
    {
        foreach (var a in FindChildren("*", "AnimatedSprite2D", true, false))
            return (AnimatedSprite2D)a;
        return null;
    }

    private List<CpuParticles2D> Emitters()
    {
        var outList = new List<CpuParticles2D>();
        foreach (var n in FindChildren("*", "CpuParticles2D", true, false))
            outList.Add((CpuParticles2D)n);
        return outList;
    }

    /// <summary>
    /// Spawn this projectile's authored "Impact" child (if any) at `at` and let it self-finish. Duplicated so a
    /// bouncing shot can burst at every enemy; lifted above the target; fired once; freed after the longest life.
    /// </summary>
    private void SpawnImpact(Vector2 at)
    {
        Node tmpl = FindChild("Impact", true, false);
        if (tmpl == null)
            return;
        Node world = GetParent();
        if (world == null)
            return;
        Node fx = tmpl.Duplicate();
        world.AddChild(fx);
        if (fx is Node2D n)
        {
            n.GlobalPosition = at;
            n.ZIndex = WorldZ.Impacts; // render over the enemy sprite it hit
            n.Visible = true;
        }
        float life = 0.5f;
        var emitters = new List<CpuParticles2D>();
        if (fx is CpuParticles2D self)
            emitters.Add(self);
        foreach (var e in fx.FindChildren("*", "CpuParticles2D", true, false))
            emitters.Add((CpuParticles2D)e);
        foreach (var em in emitters)
        {
            em.OneShot = true;
            em.Emitting = true;
            life = Mathf.Max(life, (float)em.Lifetime);
        }
        // Free via a Tween bound to fx (its callback is fx.QueueFree — a method group, GC-safe), not a
        // capturing SceneTreeTimer lambda.
        var tw = fx.CreateTween();
        tw.TweenInterval(life + 0.4);
        tw.TweenCallback(Callable.From(fx.QueueFree));
    }

    /// <summary>Where the hit reads on `victim`: its hurtbox's collision-shape centre (the torso), not the feet.</summary>
    private Vector2 HitPoint(Hurtbox? victim)
    {
        if (victim == null)
            return GlobalPosition;
        var shapes = victim.FindChildren("*", "CollisionShape2D", true, false);
        if (shapes.Count > 0)
            return ((Node2D)shapes[0]).GlobalPosition;
        return victim.GlobalPosition;
    }

    /// <summary>Pull the visual's headline colour from its gradient so the ground trail matches its tint. White fallback.</summary>
    private Color SampleVisualColor()
    {
        foreach (var em in Emitters())
            if (em.ColorRamp != null)
                return em.ColorRamp.Sample(0.0f);
        return new Color(1, 1, 1);
    }

    /// <summary>Red embers laid along the floor (local_coords = false pins them in world space as a scorch trail).</summary>
    private CpuParticles2D MakeGroundTrail(Color tint)
    {
        var p = new CpuParticles2D
        {
            Texture = GD.Load<Texture2D>("res://vfx/shared/textures/pixel_ember.png"),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            LocalCoords = false,
            Amount = 40,
            Lifetime = 0.75,
            LifetimeRandomness = 0.3,
            EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle,
            EmissionRectExtents = new Vector2(4, 1),
            Direction = new Vector2(0, -1),
            Spread = 40.0f,
            Gravity = new Vector2(0, 90),
            InitialVelocityMin = 4.0f,
            InitialVelocityMax = 26.0f,
            ScaleAmountMin = 0.5f,
            ScaleAmountMax = 1.1f,
        };
        var ramp = new Gradient
        {
            Offsets = new[] { 0.0f, 0.6f, 1.0f },
            Colors = new[]
            {
                new Color(tint.R, tint.G, tint.B, 1.0f),
                new Color(tint.R * 0.7f, tint.G * 0.6f, tint.B * 0.6f, 0.6f),
                new Color(tint.R * 0.5f, tint.G * 0.4f, tint.B * 0.4f, 0.0f),
            },
        };
        p.ColorRamp = ramp;
        return p;
    }
}
