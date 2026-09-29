using Godot;

namespace MyGame;

/// <summary>
/// One currency on the HUD (Lira, Fada Figs): its icon + the current balance. The icon pops on a gain
/// (<see cref="SetCount"/>). The HUD stacks one per currency, top-left.
/// </summary>
public partial class CurrencyCounter : HBoxContainer
{
    private const float IconSize = 24.0f;

    private readonly string _iconPath;
    private TextureRect _icon;
    private Label _label;
    private int _count = 0;

    public CurrencyCounter() { } // Godot needs a parameterless constructor; the HUD always builds it with an icon

    public CurrencyCounter(string iconPath) => _iconPath = iconPath;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 6);
        _icon = new TextureRect
        {
            Texture = GD.Load<Texture2D>(_iconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(IconSize, IconSize),
            // The currency icons are hi-res drawings shown small — filter smoothly when downscaled.
            TextureFilter = TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_icon);
        _label = new Label
        {
            Text = "0",
            ThemeTypeVariation = UiStyle.HudValue,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_label);
    }

    /// <summary>Show <paramref name="count"/>; the icon pops if it went up.</summary>
    public void SetCount(int count)
    {
        if (_label == null)
            return; // not built yet
        if (count > _count)
            HudFx.Pop(_icon);
        _count = count;
        _label.Text = count.ToString();
    }
}
