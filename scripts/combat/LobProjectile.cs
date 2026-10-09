using Godot;

namespace MyGame;

/// <summary>
/// A LOBBED / mortar projectile: THROWN in a ballistic arc so it rises, falls, lands next to the target, sits
/// as a telegraphed bomb for <see cref="DwellTime"/>, then ERUPTS into an AoE. Unlike <see cref="Projectile"/>
/// (a linear tracer that hits on contact), a lob deals NO damage in the air — only the explosion hurts, so it
/// is DODGEABLE. C# port of <c>scripts/combat/lob_projectile.gd</c>. Code-built (no scene) by <see cref="Enemy"/>.
/// </summary>
[GlobalClass]
public partial class LobProjectile : Node2D
{
    [Export] public bool Hostile { get; set; }
    [Export] public bool FriendlyFire { get; set; }

    [ExportGroup("Arc")]
    [Export] public float ArcTime { get; set; } = 0.9f;
    [Export] public float Gravity { get; set; } = 900.0f;
    [Export] public float Spin { get; set; } = 480.0f;
    [Export] public float MaxLife { get; set; } = 3.0f;

    [ExportGroup("Dwell + explosion")]
    [Export] public float DwellTime { get; set; } = 1.0f;
    [Export] public Vector2 ExplosionExtents { get; set; } = new(48, 26);
    [Export] public float ExplosionDamage { get; set; } = 16.0f;
    [Export] public float ExplosionKnockback { get; set; } = 160.0f;
    [Export] public float ExplosionStun { get; set; } = 0.25f;
    /// <summary>Particle-only scene for the blast look, instanced inside the explosion Strike. null = the Strike's own flash.</summary>
    [Export] public PackedScene? ExplosionEffect { get; set; }
    [Export] public Vector2 ExplosionEffectPos { get; set; } = Vector2.Zero;
    /// <summary>Sfx cue key played positionally at the detonation point when the bomb POPS. "" = none.</summary>
    [Export] public string ExplosionSfx { get; set; } = "";

    /// <summary>Where to AIM the arc (world space); set by the spawner. Vector2.Inf = a short fallback toss.</summary>
    public Vector2 Target = Vector2.Inf;
    /// <summary>Who threw it (knockback credit + friendly-fire exemption); set by the spawner.</summary>
    public Node? Source;

    private enum Phase { Arc, Dwell, Spent }
    private Phase _phase = Phase.Arc;
    private Vector2 _vel = Vector2.Zero;
    private float _t;
    private float _life;
    private bool _launched;
    private Node2D? _visual;

    public override void _Ready()
    {
        AddToGroup("projectiles"); // so a respawn can clear bombs in mid-air
        _visual = FindVisual();
    }

    public override void _PhysicsProcess(double delta)
    {
        float d = (float)delta;
        // Solve the launch velocity on the FIRST tick: the spawner snaps us to the muzzle AFTER add_child.
        if (!_launched)
        {
            Launch();
            _launched = true;
        }

        switch (_phase)
        {
            case Phase.Arc:
                _life += d;
                Vector2 from = GlobalPosition;
                _vel.Y += Gravity * d;
                Vector2 to = from + _vel * d;
                if (_visual != null && Spin != 0.0f)
                    _visual.Rotation += Mathf.DegToRad(Spin) * d;
                // Land only when DESCENDING onto a surface (rising, we pass up through one-way platforms).
                Vector2 surface = _vel.Y > 0.0f ? SurfaceBetween(from, to) : Vector2.Inf;
                if (surface != Vector2.Inf)
                {
                    GlobalPosition = surface;
                    Land();
                }
                else
                {
                    GlobalPosition = to;
                    if (_life >= MaxLife)
                        Explode(); // never found ground -> blow mid-air
                }
                break;
            case Phase.Dwell:
                _t += d;
                if (_t >= DwellTime)
                    Explode();
                break;
            case Phase.Spent:
                break;
        }
    }

    /// <summary>Solve the launch velocity so the arc is AIMED at `target` (reaching it at ~ArcTime under gravity).</summary>
    private void Launch()
    {
        if (Target == Vector2.Inf)
            Target = GlobalPosition + new Vector2(60.0f, 40.0f);
        Vector2 to = Target - GlobalPosition;
        _vel = new Vector2(to.X / ArcTime, to.Y / ArcTime - 0.5f * Gravity * ArcTime);
    }

    /// <summary>First L_WORLD surface crossed by the segment, or Vector2.Inf. Ray ignores one-way; caller gates on descending.</summary>
    private Vector2 SurfaceBetween(Vector2 from, Vector2 to)
    {
        if (to == from)
            to = from + new Vector2(0.0f, 0.5f);
        var space = GetWorld2D().DirectSpaceState;
        var q = PhysicsRayQueryParameters2D.Create(from, to, (uint)Combat.Layer.World);
        q.HitFromInside = true; // catch a ledge we start the step already inside
        var r = space.IntersectRay(q);
        return r.Count > 0 ? r["position"].AsVector2() : Vector2.Inf;
    }

    private void Land()
    {
        _phase = Phase.Dwell;
        _t = 0.0f;
        if (_visual != null)
            _visual.Rotation = 0.0f;
        // Telegraph: pulse alpha so the player reads "move!" before it blows. Explode() frees us, ending it.
        var tw = CreateTween().SetLoops();
        tw.TweenProperty(this, "modulate:a", 0.35, 0.11);
        tw.TweenProperty(this, "modulate:a", 1.0, 0.11);
    }

    /// <summary>
    /// Erupt: a hostile AoE Strike (from <see cref="ExplosionEffect"/>, a self-contained AoeStrike scene) built
    /// from this bomb's tuning, plus a code fallback for a visual-only/missing effect. Same activation pattern
    /// as the enemy melee strike.
    /// </summary>
    private void Explode()
    {
        _phase = Phase.Spent;
        if (ExplosionSfx != "")
            GetNodeOrNull<Sfx>("/root/Sfx")?.PlayAt(ExplosionSfx, GlobalPosition); // the delayed POP
        Node parent = GetParent();
        if (parent == null)
        {
            QueueFree();
            return;
        }
        // The thrower may have DIED while the bomb flew (a lob outlives its owner) -> drop a freed `source` to null.
        Node? src = GodotObject.IsInstanceValid(Source) ? Source : null;
        Node? effect = ExplosionEffect != null ? ExplosionEffect.Instantiate() : null;

        var tuning = new SegmentData
        {
            Damage = ExplosionDamage,
            Knockback = ExplosionKnockback,
            Stun = ExplosionStun,
        };
        if (effect is Strike strike)
        {
            // The ExplosionEffect scene IS a self-contained AoeStrike (own Hitbox + visual) — call it TYPED.
            strike.Hostile = Hostile;
            strike.FriendlyFire = FriendlyFire;
            strike.Source = src;
            parent.AddChild(strike);
            Nodes.PlaceAt(strike, GlobalPosition);
            strike.ApplyTuning(tuning, src);
            foreach (var a in strike.FindChildren("*", "Area2D", true, false))
                if (a is Hitbox hb)
                {
                    hb.Source = src; // credit the blast (knockback + `hit.source is Enemy` checks)
                    hb.Activate();
                }
        }
        else
        {
            // Fallback for a visual-only (or missing) effect: build the AoeStrike + Hitbox in code.
            var codeStrike = new AoeStrike { Hostile = Hostile, FriendlyFire = FriendlyFire, Lifetime = 0.4f, Source = src };
            var hb = new Hitbox
            {
                Damage = ExplosionDamage,
                Knockback = ExplosionKnockback,
                Stun = ExplosionStun,
                Ranged = true, // a thrown-bomb blast reads as ranged (nasen etc. react by type)
                Source = src,
            };
            hb.AddChild(MakeBox(ExplosionExtents * 2.0f, new Vector2(0, -ExplosionExtents.Y)));
            codeStrike.AddChild(hb);
            if (effect is Node2D vis)
            {
                vis.Position = ExplosionEffectPos;
                codeStrike.AddChild(vis);
            }
            parent.AddChild(codeStrike); // _Ready: team layers + self-free timer
            Nodes.PlaceAt(codeStrike, GlobalPosition);
            hb.Activate();
        }
        QueueFree();
    }

    /// <summary>The thrown-object body (the first Node2D child, e.g. a particle scene). null = no visual.</summary>
    private Node2D? FindVisual()
    {
        foreach (var c in GetChildren())
            if (c is Node2D n)
                return n;
        return null;
    }

    // Inlined Nodes.place_at / Shapes.make_box (GDScript static helpers C# can't call).

    private static CollisionShape2D MakeBox(Vector2 size, Vector2 offset) =>
        new() { Position = offset, Shape = new RectangleShape2D { Size = size } };
}
