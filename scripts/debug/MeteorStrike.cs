using Godot;

namespace MyGame;

/// <summary>
/// DEBUG: the CAMPING METEOR, for a devlog — with it on (M), a player who stays within <see cref="Rounds.StillRadius"/>
/// for <see cref="CampTime"/> gets a meteor dropped where he stands, and another every <see cref="CampTime"/> he
/// stays. Not part of the game (a council ruled a camping meteor out, 2026-10-13): it lives only in this file and is
/// switched on by <see cref="DebugTools"/>. Delete this file and the M line in <c>DebugTools</c> to remove it.
///
/// <para>The strike, all built in code: the ground glows and a shadow grows under him (<see cref="WarnTime"/>, time to
/// move), then a burning rock comes down at an angle trailing fire, smoke and sparks, and the impact is a white flash,
/// a shockwave ring, a fireball, debris thrown up under gravity, a dust cloud rolling along the ground, embers, a
/// scorch mark that fades, a camera shake and a blast. It hits like an enemy attack: a hostile <see cref="Hitbox"/>
/// for one instant at the impact (the usual half-block, knocked back away from the crater).</para>
/// </summary>
public partial class MeteorStrike : Node
{
    private const float CampTime = 5.0f;

    private readonly Player _player;
    private Vector2 _anchor;
    private float _still;

    public MeteorStrike(Player player) => _player = player;

    public bool On { get; private set; }

    public void Toggle()
    {
        On = !On;
        _still = 0.0f;
        GD.Print($"[DEBUG] camping meteor: {(On ? "ON" : "off")}");
    }

    public override void _Process(double delta)
    {
        if (!On || !IsInstanceValid(_player) || _player.IsDead())
            return;
        Vector2 at = _player.GlobalPosition;
        if (at.DistanceTo(_anchor) > Rounds.StillRadius)
        {
            _anchor = at;
            _still = 0.0f;
            return;
        }
        _still += (float)delta;
        if (_still < CampTime || !_player.IsOnFloor())
            return;
        _still = 0.0f;
        var meteor = new Meteor();
        _player.GetParent().AddChild(meteor);
        Nodes.PlaceAt(meteor, at);
    }

    /// <summary>One meteor, from warning to fading scorch mark. Placed at the impact point (his feet), it frees itself
    /// when done.</summary>
    private sealed partial class Meteor : Node2D
    {
        private const float WarnTime = 1.1f;
        private const float FallTime = 0.6f;
        private const float FlashTime = 0.18f;
        private const float RingTime = 0.45f;
        private const float GlowTime = 1.6f;   // the crater rim's molten glow
        private const float ScorchTime = 5.0f;  // the scorch mark, from impact until it has faded
        private const float EmberTime = 3.0f;
        private const float FallDistance = 760.0f;
        private const float FallSlant = 0.42f;   // sideways per unit down: it comes in at an angle, from either side
        private const float HitRadius = 52.0f;
        private const float HitDamage = 12.0f;
        private const float HitKnockback = 320.0f;
        private const float HitActive = 0.12f;
        private const float ShakeAmp = 9.0f;
        private const float ShakeTime = 0.55f;
        private const string BlastCue = "ein.kamikaze";
        private const string FallCue = "slam_down";
        private static readonly Texture2D Dot = GD.Load<Texture2D>("res://vfx/shared/textures/soft_dot.png");

        // HDR colours (> 1 blooms): the molten core is white-hot, the fire orange, the smoke and rock dull.
        private static readonly Color Core = new(4.0f, 3.2f, 2.0f);
        private static readonly Color Molten = new(3.0f, 1.4f, 0.4f, 0.85f);
        private static readonly Color Halo = new(2.0f, 0.7f, 0.15f, 0.35f);
        private static readonly Color Rock = new(0.23f, 0.17f, 0.14f);
        private static readonly Color WarnGlow = new(2.2f, 0.6f, 0.12f);
        private static readonly Color Shadow = new(0.0f, 0.0f, 0.0f, 0.55f);
        private static readonly Color Scorch = new(0.05f, 0.03f, 0.03f, 0.75f);

        private float _t;
        private Vector2 _from;           // where the rock starts, relative to the impact
        private bool _landed;
        private CpuParticles2D _fire = null!, _smoke = null!, _sparks = null!, _embers = null!;
        private Camera2D? _camera;
        private Vector2 _cameraRest;

        public override void _Ready()
        {
            float side = GD.Randf() < 0.5f ? -1.0f : 1.0f;
            _from = new Vector2(side * FallSlant, -1.0f).Normalized() * FallDistance;
            _fire = AddChildParticles(Trail(160, 0.35f, 1.6f, FireRamp(), 20.0f, 60.0f));
            _smoke = AddChildParticles(Trail(90, 1.6f, 1.5f, SmokeRamp(), 10.0f, 30.0f));
            _smoke.Gravity = new Vector2(0, -20);
            GrowBy(_smoke, 3.0f);
            _sparks = AddChildParticles(Trail(40, 0.5f, 0.35f, SparkRamp(), 60.0f, 160.0f));
            _sparks.Spread = 40.0f;
            _sparks.Gravity = new Vector2(0, 300);
            foreach (var trail in new[] { _fire, _smoke, _sparks })
            {
                trail.Emitting = false;
                trail.Position = _from;
            }
        }

        public override void _Process(double delta)
        {
            _t += (float)delta;
            float fallT = (_t - WarnTime) / FallTime;
            if (fallT >= 0.0f && fallT < 1.0f)
                Fly(fallT);
            else if (fallT >= 1.0f && !_landed)
                Land();
            if (_landed)
                Shake();
            if (_t > WarnTime + FallTime + ScorchTime)
                QueueFree();
            QueueRedraw();
        }

        private void Fly(float fallT)
        {
            if (!_fire.Emitting)
            {
                _fire.Emitting = _smoke.Emitting = _sparks.Emitting = true;
                GetNode<Sfx>("/root/Sfx").PlayAt(FallCue, GlobalPosition);
            }
            Vector2 head = _from * (1.0f - fallT * fallT); // eased in: it speeds up as it falls
            Vector2 back = (_from - head).Normalized();
            foreach (var trail in new[] { _fire, _smoke, _sparks })
            {
                trail.Position = head;
                trail.Direction = back;
            }
        }

        private void Land()
        {
            _landed = true;
            _fire.Emitting = _smoke.Emitting = _sparks.Emitting = false;
            GetNode<Sfx>("/root/Sfx").PlayAt(BlastCue, GlobalPosition);
            var fireball = Burst(60, 0.6f, 2.5f, FireRamp(), new Vector2(0, -1), 75.0f, 60.0f, 200.0f, Vector2.Zero, 0.0f);
            fireball.Position = new Vector2(0, -8); // up and out of the crater, never into the ground
            var debris = Burst(40, 0.85f, 4.0f, DebrisRamp(), new Vector2(0, -1), 60.0f, 220.0f, 460.0f, new Vector2(0, 980), 0.0f);
            debris.Texture = null; // square chunks of rock, pixel-sized like the art
            debris.ScaleAmountMin = 2.0f;
            debris.ScaleAmountCurve = null;
            foreach (float side in new[] { -1.0f, 1.0f }) // dust rolls out along the ground both ways
            {
                var dust = Burst(28, 1.6f, 1.4f, DustRamp(), new Vector2(side, -0.12f), 10.0f, 80.0f, 230.0f, Vector2.Zero, 140.0f);
                dust.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
                dust.EmissionRectExtents = new Vector2(14, 2);
                dust.Position = new Vector2(0, -6);
                GrowBy(dust, 2.2f);
            }
            _embers = AddChildParticles(Trail(20, 1.2f, 0.3f, SparkRamp(), 10.0f, 40.0f));
            _embers.Direction = new Vector2(0, -1);
            _embers.Spread = 60.0f;
            _embers.Gravity = new Vector2(0, -30);
            _embers.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
            _embers.EmissionRectExtents = new Vector2(26, 2);
            GetTree().CreateTimer(EmberTime).Timeout += () => _embers.Emitting = false;
            StrikeHitbox();
            _camera = GetViewport().GetCamera2D();
            _cameraRest = _camera?.Offset ?? Vector2.Zero;
        }

        /// <summary>The hit: a hostile box over the crater for one instant, the meteor as its source so the knockback
        /// throws him away from the impact.</summary>
        private void StrikeHitbox()
        {
            var box = new Hitbox
            {
                CollisionLayer = Combat.HitLayer(hostile: true),
                CollisionMask = Combat.HurtMask(hostile: true),
                Damage = HitDamage,
                Knockback = HitKnockback,
                Source = this,
            };
            box.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = HitRadius }, Position = new Vector2(0, -HitRadius * 0.5f) });
            AddChild(box);
            Callable.From(() => box.ActivateTimed(HitActive)).CallDeferred(); // once the shape is in the physics space
        }

        private void Shake()
        {
            if (_camera == null || !IsInstanceValid(_camera))
                return;
            float left = Mathf.Max(ShakeTime - (_t - WarnTime - FallTime), 0.0f);
            float amp = ShakeAmp * left / ShakeTime;
            _camera.Offset = _cameraRest + (left > 0.0f
                ? new Vector2((float)GD.RandRange(-amp, amp), (float)GD.RandRange(-amp, amp))
                : Vector2.Zero);
            if (left <= 0.0f)
                _camera = null; // at rest again: stop touching it
        }

        public override void _Draw()
        {
            float fallT = (_t - WarnTime) / FallTime;
            if (!_landed)
            {
                // The warning: a glow on the ground under him and a shadow that tightens and darkens as it nears.
                float near = Mathf.Clamp(_t / (WarnTime + FallTime), 0.0f, 1.0f);
                float pulse = 0.55f + 0.45f * Mathf.Sin(_t * 18.0f);
                DrawEllipse(Vector2.Zero, Mathf.Lerp(60, 30, near), 6, Shadow with { A = Shadow.A * near });
                DrawEllipse(Vector2.Zero, Mathf.Lerp(14, 42, near), 5, WarnGlow with { A = 0.5f * pulse * near });
                if (fallT >= 0.0f)
                {
                    Vector2 head = _from * (1.0f - fallT * fallT);
                    DrawSoft(head, 22, Halo);
                    DrawSoft(head, 12, Molten);
                    DrawCircle(head, 6, Rock);
                    DrawCircle(head + (_from - head).Normalized() * -2.0f, 3.5f, Core); // the hot face, toward the ground
                }
                return;
            }
            float since = _t - WarnTime - FallTime;
            float scorch = 1.0f - Mathf.Clamp((since - (ScorchTime - 3.0f)) / 3.0f, 0.0f, 1.0f);
            DrawEllipse(Vector2.Zero, 50, 9, Scorch with { A = Scorch.A * scorch });
            float glow = 1.0f - Mathf.Clamp(since / GlowTime, 0.0f, 1.0f);
            if (glow > 0.0f)
                DrawEllipse(Vector2.Zero, 30, 5, Molten with { A = Molten.A * glow * glow });
            if (since < RingTime)
            {
                float r = since / RingTime;
                // The shockwave runs out along the ground: a flattened ring.
                DrawSetTransform(new Vector2(0, -3), 0.0f, new Vector2(1.0f, 0.3f));
                DrawArc(Vector2.Zero, Mathf.Lerp(10, 120, r), 0, Mathf.Tau, 48, Halo with { A = 1.0f - r }, Mathf.Lerp(10, 2, r));
                DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
            }
            if (since < FlashTime)
                DrawSoft(new Vector2(0, -14), 90, new Color(6, 5, 4, 1.0f - since / FlashTime));
        }

        /// <summary>A soft radial glow (the soft-dot texture), brightest at its centre.</summary>
        private void DrawSoft(Vector2 at, float radius, Color color) =>
            DrawTextureRect(Dot, new Rect2(at - Vector2.One * radius, Vector2.One * radius * 2.0f), false, color);

        private void DrawEllipse(Vector2 at, float rx, float ry, Color color)
        {
            DrawSetTransform(at, 0.0f, new Vector2(1.0f, ry / rx));
            DrawCircle(Vector2.Zero, rx, color);
            DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
        }

        private CpuParticles2D AddChildParticles(CpuParticles2D p)
        {
            AddChild(p);
            return p;
        }

        /// <summary>A continuous emitter that leaves its particles in the world (a trail behind whatever moves it).</summary>
        private static CpuParticles2D Trail(int amount, float life, float scale, Gradient ramp, float vMin, float vMax) => new()
        {
            Amount = amount,
            Lifetime = life,
            LocalCoords = false,
            Texture = Dot,
            ScaleAmountMin = scale * 0.7f,
            ScaleAmountMax = scale,
            ScaleAmountCurve = Shrink(),
            ColorRamp = ramp,
            Spread = 12.0f,
            Gravity = Vector2.Zero,
            InitialVelocityMin = vMin,
            InitialVelocityMax = vMax,
        };

        /// <summary>A one-shot burst at the impact.</summary>
        private CpuParticles2D Burst(int amount, float life, float scale, Gradient ramp, Vector2 dir, float spread,
            float vMin, float vMax, Vector2 gravity, float damping)
        {
            var p = Trail(amount, life, scale, ramp, vMin, vMax);
            p.OneShot = true;
            p.Explosiveness = 1.0f;
            p.Direction = dir;
            p.Spread = spread;
            p.Gravity = gravity;
            p.DampingMin = damping * 0.6f;
            p.DampingMax = damping;
            p.Emitting = true;
            return AddChildParticles(p);
        }

        private static Curve Shrink() => CurveOf(1.0f, 0.0f);

        /// <summary>Particles that swell to <paramref name="factor"/> times their size over their life (a curve can't
        /// exceed 1, so the base scale is raised and the curve starts below 1).</summary>
        private static void GrowBy(CpuParticles2D p, float factor)
        {
            p.ScaleAmountMin *= factor;
            p.ScaleAmountMax *= factor;
            p.ScaleAmountCurve = CurveOf(1.0f / factor, 1.0f);
        }

        private static Curve CurveOf(float start, float end)
        {
            var c = new Curve();
            c.AddPoint(new Vector2(0, start));
            c.AddPoint(new Vector2(1, end));
            return c;
        }

        private static Gradient Ramp(params (float At, Color Colour)[] stops)
        {
            var g = new Gradient { Offsets = [], Colors = [] };
            foreach (var (at, colour) in stops)
                g.AddPoint(at, colour);
            return g;
        }

        private static Gradient FireRamp() => Ramp(
            (0.0f, new Color(4.0f, 2.6f, 1.2f)),
            (0.25f, new Color(2.5f, 0.8f, 0.15f, 0.9f)),
            (0.6f, new Color(0.6f, 0.1f, 0.05f, 0.5f)),
            (1.0f, new Color(0.1f, 0.08f, 0.08f, 0.0f)));

        private static Gradient SmokeRamp() => Ramp(
            (0.0f, new Color(0.35f, 0.32f, 0.3f, 0.55f)),
            (1.0f, new Color(0.2f, 0.2f, 0.2f, 0.0f)));

        private static Gradient SparkRamp() => Ramp(
            (0.0f, new Color(4.0f, 3.0f, 1.5f)),
            (0.5f, new Color(3.0f, 1.0f, 0.2f)),
            (1.0f, new Color(1.0f, 0.2f, 0.05f, 0.0f)));

        private static Gradient DebrisRamp() => Ramp(
            (0.0f, new Color(3.0f, 1.2f, 0.3f)),
            (0.35f, Rock),
            (1.0f, Rock with { A = 0.0f }));

        private static Gradient DustRamp() => Ramp(
            (0.0f, new Color(0.45f, 0.4f, 0.36f, 0.5f)),
            (1.0f, new Color(0.3f, 0.28f, 0.26f, 0.0f)));
    }
}
