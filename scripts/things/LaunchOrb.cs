using Godot;

namespace MyGame;

/// <summary>
/// A levitating LAUNCH orb: dash into (or near) it and it magnets Khalid through and flings him up + forward
/// (see Player._process_launch). Placed by RunManager from a level's `orbs` list. Detection is PLAYER-side (the
/// orb joins the "orbs" group and is dumb). Wears a tint+glow shader; hums on loop, one-shot on use. C# port of
/// <c>scripts/things/launch_orb.gd</c>. The Player finds it via the group + drives set_near/play_use.
/// </summary>
public partial class LaunchOrb : Node2D
{
    private const string Frames = "res://resources/things/launch_orb.tres";
    private const string RecolorShader = "res://vfx/shaders/thing_recolor.gdshader";
    private static readonly Color Sample = new(1.0f, 0.22f, 0.26f); // bright orb red -> palette family + tint
    private const float BobAmplitude = 4.0f;
    private const float BobSpeed = 2.2f;
    private const float ShineNear = 1.4f;
    private const float ShineTween = 0.14f;

    /// <summary>The SET launch this orb gives (px/s): a strong UP + a good FORWARD. The Player reads these on capture.</summary>
    [Export] public float launch_up { get; set; } = 950.0f;
    [Export] public float launch_forward { get; set; } = 650.0f;

    private AnimatedSprite2D _sprite = null!;
    private ShaderMaterial? _mat;
    private AudioStreamPlayer2D? _hum;
    private float _baseY;
    private float _phase;
    private bool _near;
    private float _shine;

    public override void _Ready()
    {
        ZIndex = WorldZ.Stalls; // a thing you use: behind the tiles, in front of the scenery (WorldZ)
        AddToGroup("orbs");
        _baseY = Position.Y;
        _phase = GlobalPosition.X * 0.05f; // desync neighbouring orbs so a row doesn't bob in lockstep
        _sprite = new AnimatedSprite2D { Centered = true };
        if (ResourceLoader.Exists(Frames))
        {
            _sprite.SpriteFrames = GD.Load<SpriteFrames>(Frames);
            _sprite.Play("bob");
        }
        ApplyRecolor();
        AddChild(_sprite);
        _hum = GetNode<Sfx>("/root/Sfx").make_loop_2d("launch_orb");
        if (_hum != null)
        {
            AddChild(_hum);
            _hum.Play();
        }
    }

    /// <summary>Wear the tint+glow material and paint it the chosen power-family colour (VfxPalette.recolor).</summary>
    private void ApplyRecolor()
    {
        if (!ResourceLoader.Exists(RecolorShader))
            return;
        _mat = new ShaderMaterial { Shader = GD.Load<Shader>(RecolorShader) };
        _mat.SetShaderParameter("tint", VfxPalette.Recolor(Sample));
        _mat.SetShaderParameter("shine", 0.0f);
        _sprite.Material = _mat;
    }

    public override void _Process(double delta)
    {
        _phase += (float)delta * BobSpeed;
        Position = new Vector2(Position.X, _baseY + Mathf.Sin(_phase) * BobAmplitude);
        float target = _near ? ShineNear : 0.0f;
        if (!Mathf.IsEqualApprox(_shine, target))
        {
            _shine = Mathf.MoveToward(_shine, target, (float)delta / Mathf.Max(ShineTween, 0.001f) * ShineNear);
            _mat?.SetShaderParameter("shine", _shine);
        }
    }

    /// <summary>Called by the Player each frame: is Khalid close enough to launch off this orb? Drives the SHINE.</summary>
    public void set_near(bool value) => _near = value;

    /// <summary>Play the one-shot "used it" cue at the orb (called by the Player when it captures him).</summary>
    public void play_use() => GetNode<Sfx>("/root/Sfx").play_at("launch_orb_use", GlobalPosition, 0.0f, 1.0f);
}
