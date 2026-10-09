using Godot;

namespace MyGame;

/// <summary>
/// Small world-space health bar with an optional name, hovering above an enemy (or a plain gauge like the
/// player's cooldown bar). Built in code so any body can add one without a scene; diegetic (scales with the
/// camera). C# port of <c>scripts/combat/health_bar.gd</c>. C#-only consumers now (Player/Enemy) — plain class;
/// the HUD inlines its own copy of the colour bands.
/// </summary>
public partial class FloatingHealthBar : Node2D
{
    public float BarWidth = 26.0f;
    public float BarHeight = 3.0f;
    public Color BgColor = new(0.09f, 0.09f, 0.11f, 0.85f);
    public Color BorderColor = new(0, 0, 0, 0.85f);
    public Color FillColor = new(0.82f, 0.24f, 0.24f);
    /// <summary>When true the fill is coloured by fullness (green/orange/red) instead of the fixed <see cref="FillColor"/>.</summary>
    public bool RatioColors = false;
    public int NameSize = 6;

    // Shared health-fill thresholds + colours (the HUD keeps its own matching copy).
    private const float LowRatio = 0.25f;
    private const float MidRatio = 0.5f;
    private static readonly Color ColorHigh = new(0.30f, 0.80f, 0.32f);
    private static readonly Color ColorMid = new(0.95f, 0.62f, 0.16f);
    private static readonly Color ColorLow = new(0.86f, 0.22f, 0.22f);

    private float _ratio = 1.0f;
    private Label _label = null!;

    /// <summary>Green (high) / orange (mid) / red (low) for a 0..1 fill fraction.</summary>
    public static Color ColorForRatio(float r)
    {
        if (r <= LowRatio)
            return ColorLow;
        if (r <= MidRatio)
            return ColorMid;
        return ColorHigh;
    }

    /// <summary>Add the floating name label above the bar. No name → bar only.</summary>
    public void Setup(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
            return;
        _label = new Label
        {
            Text = displayName,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Size = new Vector2(80, NameSize + 4),
            Position = new Vector2(-40, -BarHeight - NameSize - 5),
        };
        _label.AddThemeFontSizeOverride("font_size", NameSize);
        _label.AddThemeColorOverride("font_color", new Color(0.92f, 0.92f, 0.96f));
        _label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
        _label.AddThemeConstantOverride("outline_size", 3);
        AddChild(_label);
    }

    /// <summary>Set the fill fraction (0..1, clamped) and redraw.</summary>
    public void SetRatio(float value)
    {
        _ratio = Mathf.Clamp(value, 0.0f, 1.0f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float w = BarWidth;
        float h = BarHeight;
        var origin = new Vector2(-w / 2.0f, -h);
        DrawRect(new Rect2(origin - Vector2.One, new Vector2(w + 2, h + 2)), BorderColor);
        DrawRect(new Rect2(origin, new Vector2(w, h)), BgColor);
        if (_ratio > 0.0f)
        {
            Color fc = RatioColors ? ColorForRatio(_ratio) : FillColor;
            DrawRect(new Rect2(origin, new Vector2(w * _ratio, h)), fc);
        }
    }
}
