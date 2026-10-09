using Godot;

namespace MyGame;

/// <summary>
/// A LOBBED / mortar projectile: THROWN in a ballistic arc so it rises, falls, lands next to the target, sits
/// as a telegraphed bomb for <see cref="dwell_time"/>, then ERUPTS into an AoE. Unlike <see cref="Projectile"/>
/// (a linear tracer that hits on contact), a lob deals NO damage in the air — only the explosion hurts, so it
/// is DODGEABLE. C# port of <c>scripts/combat/lob_projectile.gd</c>. Code-built (no scene) via enemy.gd.
/// Public surface stays snake_case for the still-GDScript spawner.
/// </summary>
[GlobalClass]
public partial class LobProjectile : Node2D
{
    [Export] public bool hostile { get; set; }
    [Export] public bool friendly_fire { get; set; }

    [ExportGroup("Arc")]
    [Export] public float arc_time { get; set; } = 0.9f;
    [Export] public float gravity { get; set; } = 900.0f;
    [Export] public float spin { get; set; } = 480.0f;
    [Export] public float max_life { get; set; } = 3.0f;

    [ExportGroup("Dwell + explosion")]
    [Export] public float dwell_time { get; set; } = 1.0f;
    [Export] public Vector2 explosion_extents { get; set; } = new(48, 26);
    [Export] public float explosion_damage { get; set; } = 16.0f;
    [Export] public float explosion_knockback { get; set; } = 160.0f;
    [Export] public float explosion_stun { get; set; } = 0.25f;
    /// <summary>Particle-only scene for the blast look, instanced inside the explosion Strike. null = the Strike's own flash.</summary>
    [Export] public PackedScene? explosion_effect { get; set; }
    [Export] public Vector2 explosion_effect_pos { get; set; } = Vector2.Zero;
    /// <summary>Sfx cue key played positionally at the detonation point when the bomb POPS. "" = none.</summary>
    [Export] public string explosion_sfx { get; set; } = "";

    /// <summary>Where to AIM the arc (world space); set by the spawner. Vector2.Inf = a short fallback toss.</summary>
    public Vector2 target = Vector2.Inf;
    /// <summary>Who threw it (knockback credit + friendly-fire exemption); set by the spawner.</summary>
    public Node? source;

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
                _vel.Y += gravity * d;
                Vector2 to = from + _vel * d;
                if (_visual != null && spin != 0.0f)
                    _visual.Rotation += Mathf.DegToRad(spin) * d;
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
                    if (_life >= max_life)
                        Explode(); // never found ground -> blow mid-air
                }
                break;
            case Phase.Dwell:
                _t += d;
                if (_t >= dwell_time)
                    Explode();
                break;
            case Phase.Spent:
                break;
        }
    }

    /// <summary>Solve the launch velocity so the arc is AIMED at `target` (reaching it at ~arc_time under gravity).</summary>
    private void Launch()
    {
        if (target == Vector2.Inf)
            target = GlobalPosition + new Vector2(60.0f, 40.0f);
        Vector2 to = target - GlobalPosition;
        _vel = new Vector2(to.X / arc_time, to.Y / arc_time - 0.5f * gravity * arc_time);
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
    /// Erupt: a hostile AoE Strike (from <see cref="explosion_effect"/>, a self-contained AoeStrike scene) built
    /// from this bomb's tuning, plus a code fallback for a visual-only/missing effect. Same activation pattern
    /// as the enemy melee strike.
    /// </summary>
    private void Explode()
    {
        _phase = Phase.Spent;
        if (explosion_sfx != "")
            GetNodeOrNull<Sfx>("/root/Sfx")?.play_at(explosion_sfx, GlobalPosition); // the delayed POP
        Node parent = GetParent();
        if (parent == null)
        {
            QueueFree();
            return;
        }
        // The thrower may have DIED while the bomb flew (a lob outlives its owner) -> drop a freed `source` to null.
        Node? src = GodotObject.IsInstanceValid(source) ? source : null;
        Node? effect = explosion_effect != null ? explosion_effect.Instantiate() : null;

        var tuning = new SegmentData
        {
            Damage = explosion_damage,
            Knockback = explosion_knockback,
            Stun = explosion_stun,
        };
        if (effect is Strike strike)
        {
            // The explosion_effect scene IS a self-contained AoeStrike (own Hitbox + visual) — call it TYPED.
            strike.hostile = hostile;
            strike.friendly_fire = friendly_fire;
            strike.source = src;
            parent.AddChild(strike);
            PlaceAt(strike, GlobalPosition);
            strike.apply_tuning(tuning, src);
            foreach (var a in strike.FindChildren("*", "Area2D", true, false))
                if (a is Hitbox hb)
                {
                    hb.source = src; // credit the blast (knockback + `hit.source is Enemy` checks)
                    hb.activate();
                }
        }
        else
        {
            // Fallback for a visual-only (or missing) effect: build the AoeStrike + Hitbox in code.
            var codeStrike = new AoeStrike { hostile = hostile, friendly_fire = friendly_fire, lifetime = 0.4f, source = src };
            var hb = new Hitbox
            {
                damage = explosion_damage,
                knockback = explosion_knockback,
                stun = explosion_stun,
                ranged = true, // a thrown-bomb blast reads as ranged (nasen etc. react by type)
                source = src,
            };
            hb.AddChild(MakeBox(explosion_extents * 2.0f, new Vector2(0, -explosion_extents.Y)));
            codeStrike.AddChild(hb);
            if (effect is Node2D vis)
            {
                vis.Position = explosion_effect_pos;
                codeStrike.AddChild(vis);
            }
            parent.AddChild(codeStrike); // _Ready: team layers + self-free timer
            PlaceAt(codeStrike, GlobalPosition);
            hb.activate();
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
    private static void PlaceAt(Node2D node, Vector2 pos)
    {
        node.GlobalPosition = pos;
        node.ResetPhysicsInterpolation();
    }

    private static CollisionShape2D MakeBox(Vector2 size, Vector2 offset) =>
        new() { Position = offset, Shape = new RectangleShape2D { Size = size } };
}
