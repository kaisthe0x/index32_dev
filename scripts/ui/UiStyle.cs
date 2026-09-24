using Godot;

namespace MyGame;

/// <summary>
/// The shared look of the game's framed menus (the attack picker, the pause menu): the gold-on-near-black panel and its
/// palette, so every menu reads as one family.
/// </summary>
public static class UiStyle
{
    public static readonly Color Gold = new(0.85f, 0.72f, 0.18f);
    public static readonly Color PanelBg = new(0.06f, 0.06f, 0.08f, 0.98f);
    public static readonly Color TitleText = new(0.93f, 0.87f, 0.62f);
    public static readonly Color BodyText = new(0.72f, 0.72f, 0.80f);

    /// <summary>A flat panel box with a uniform border + rounded corners and no content margin.</summary>
    public static StyleBoxFlat Framed(Color bg, Color border, int borderW, int radius)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetBorderWidthAll(borderW);
        s.SetCornerRadiusAll(radius);
        s.SetContentMarginAll(0);
        return s;
    }
}
