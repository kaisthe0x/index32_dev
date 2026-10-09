using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Moons orbiting the player like a little planet system — a REAL orbit with DEPTH: each moon passes BEHIND the
/// sprite at the far side of the (tilted) ellipse and IN FRONT at the near side (a single emitter can't, all its
/// particles share one z_index). Reusable — any aura scene uses it.
/// The aura scenes set the <c>[Export]</c>s by name.
/// </summary>
public partial class OrbitAura : Node2D
{
    [Export] public int Count { get; set; } = 6;
    [Export] public Texture2D? MoonTexture { get; set; }
    [Export] public Color MoonColor { get; set; } = new(1.7f, 1.35f, 0.4f); // HDR gold -> blooms; alpha set per-frame
    [Export] public float RadiusX { get; set; } = 28.0f;
    [Export] public float RadiusY { get; set; } = 11.0f;   // < RadiusX -> a tilted (perspective) ring
    [Export] public Vector2 Center { get; set; } = new(0, -26);
    [Export] public float Speed { get; set; } = 2.6f;       // radians / second
    [Export] public float NearScale { get; set; } = 1.6f;  // moon scale at the FRONT
    [Export] public float FarScale { get; set; } = 0.55f;  // moon scale at the BACK
    [Export] public float FarAlpha { get; set; } = 0.4f;   // moon alpha at the back
    [Export] public int BehindZ { get; set; } = -1;        // z while behind the sprite
    [Export] public int FrontZ { get; set; } = 1;          // z while in front
    [Export] public float SpawnTime { get; set; } = 0.22f; // seconds to GROW from nothing into the orbit

    private readonly List<Sprite2D> _moons = new();
    private float _t = 0.0f;

    public override void _Ready()
    {
        for (int i = 0; i < Count; i++)
        {
            // Start at ZERO scale (never the texture's native size — that'd be a huge one-frame flash).
            var m = new Sprite2D { Texture = MoonTexture, Scale = Vector2.Zero };
            AddChild(m);
            _moons.Add(m);
        }
        Layout(); // seed positions so there's no one-frame pop from the origin
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        Layout();
    }

    /// <summary>Place every moon on the tilted ellipse for the current time, flipping z + scaling/dimming by depth.</summary>
    private void Layout()
    {
        int n = _moons.Count;
        float grow = SpawnTime > 0.0f ? Mathf.Clamp(_t / SpawnTime, 0.0f, 1.0f) : 1.0f;
        for (int i = 0; i < n; i++)
        {
            var m = _moons[i];
            float ang = _t * Speed + Mathf.Tau * i / n;
            float s = Mathf.Sin(ang);         // -1 at the far back .. +1 at the near front
            float depth = (s + 1.0f) * 0.5f;  // 0 = back, 1 = front
            m.Position = Center + new Vector2(Mathf.Cos(ang) * RadiusX, s * RadiusY);
            m.ZIndex = s < 0.0f ? BehindZ : FrontZ;
            float sc = Mathf.Lerp(FarScale, NearScale, depth) * grow;
            m.Scale = new Vector2(sc, sc);
            m.Modulate = new Color(MoonColor.R, MoonColor.G, MoonColor.B, Mathf.Lerp(FarAlpha, 1.0f, depth));
        }
    }
}
