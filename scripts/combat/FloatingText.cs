using Godot;

namespace MyGame;

/// <summary>
/// A global floating-text emitter (Risk of Rain style): pops a label above a host and floats it up, then frees
/// itself. ONE call does it: <see cref="Emit"/>. The LOOK + the in/out transition come entirely from the TYPE
/// preset (<c>configs/floating_text_types.gd</c>), so damage numbers and a "perfect!" callout animate differently
/// with zero code change. Parented to the host and animated in its LOCAL space (rides a moving enemy, immune to
/// the camera). C# port of <c>scripts/combat/floating_text.gd</c>; only C# emits it (plain class).
/// </summary>
public partial class FloatingText : Node2D
{
    private static readonly Vector2 Box = new(220, 60);

    private float _elapsed = 0.0f;
    private Vector2 _start = Vector2.Zero;
    private Vector2 _targetPos = Vector2.Zero;
    private float _life = 0.8f;
    private float _hold = 0.4f;
    private float _popScale = 0.7f;
    private float _popTime = 0.14f;
    private bool _fadeIn = false;

    /// <summary>Emit a `type` label reading `text` at `localPos` on `host` (parented to it). `magnitude` drives the
    /// size/colour ramp; `colorOverride` replaces the preset colour for THIS call (the player's hair pick).</summary>
    public static void Emit(FloatingTextType type, Node2D host, Vector2 localPos, string text, float magnitude = 0.0f, Color? colorOverride = null)
    {
        if (!FloatingTextTypes.Types.TryGetValue(type, out var style))
        {
            GD.PushWarning($"FloatingText: unknown type '{type}'");
            return;
        }
        if (colorOverride is Color c)
            style = style with { Color = c };
        var n = new FloatingText();
        host.AddChild(n);
        n.SetupText(style, localPos, text, magnitude);
    }

    private void SetupText(FloatingTextStyle s, Vector2 localPos, string text, float magnitude)
    {
        float f = s.MagHi > s.MagLo ? Mathf.Clamp((magnitude - s.MagLo) / (s.MagHi - s.MagLo), 0.0f, 1.0f) : 0.0f;
        int size = s.Size ?? Mathf.RoundToInt(Mathf.Lerp(s.SizeLo, s.SizeHi, f));
        Color col = s.Color ?? s.ColorLo.Lerp(s.ColorHi, f);

        var label = new Label
        {
            Text = text,
            Size = Box,
            Position = -Box * 0.5f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", col);
        label.AddThemeColorOverride("font_outline_color", s.OutlineColor);
        label.AddThemeConstantOverride("outline_size", s.OutlineSize);
        if (s.Font != "" && ResourceLoader.Exists(s.Font))
            label.AddThemeFontOverride("font", GD.Load<Font>(s.Font));
        AddChild(label);

        // Fake ITALIC: a Label has no italic flag, so slant the whole node (shear).
        if (s.Italic)
            Skew = Mathf.DegToRad(s.ItalicDeg);

        _life = s.Life;
        _hold = s.Hold;
        _popScale = s.PopScale;
        _popTime = Mathf.Max(s.PopTime, 0.001f);
        _fadeIn = s.FadeIn;
        Position = localPos + new Vector2((float)GD.RandRange(-s.Jitter, s.Jitter), (float)GD.RandRange(-s.Jitter, s.Jitter) * 0.5f);
        _start = Position;
        _targetPos = _start + new Vector2((float)GD.RandRange(-s.Drift, s.Drift), -s.Rise);
        Scale = new Vector2(_popScale, _popScale);
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        float u = _elapsed / _life;
        if (u >= 1.0f)
        {
            QueueFree();
            return;
        }
        float easeOut = 1.0f - (1.0f - u) * (1.0f - u);
        Position = _start.Lerp(_targetPos, easeOut);
        Scale = Vector2.One * Mathf.Min(_popScale + (1.0f - _popScale) * (_elapsed / _popTime), 1.0f);
        var m = Modulate;
        if (_fadeIn && _elapsed < _popTime)
            m.A = _elapsed / _popTime;
        else if (u > _hold)
            m.A = Mathf.Clamp(1.0f - (u - _hold) / (1.0f - _hold), 0.0f, 1.0f);
        else
            m.A = 1.0f;
        Modulate = m;
    }
}
