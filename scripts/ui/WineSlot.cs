using Godot;

namespace MyGame;

/// <summary>
/// Khalid's one wine bottle in the HUD, under the currency counters: the EMPTY bottle while he carries no wine, the FULL one while
/// he does — then with the wine's name and rarity beside it, in the rarity's colour. The bottle pops when it is
/// filled and each time the wine climbs a rarity.
/// </summary>
public partial class WineSlot : HBoxContainer
{
    private const string SheetPath = "res://assets/things/wine_bottle.png"; // two 64×64 frames: empty, full
    private const int FrameSize = 64;
    private const float IconSize = 32.0f;

    private readonly AtlasTexture _empty = Frame(0), _full = Frame(1);
    private readonly TextureRect _icon;
    private readonly Label _label = UiStyle.HudLabel(UiStyle.HudHeading);
    private Wine? _shown;
    private WineRarity _shownRarity;

    public WineSlot()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 6);
        _icon = new TextureRect
        {
            Texture = _empty,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(IconSize, IconSize),
            TextureFilter = TextureFilterEnum.LinearWithMipmaps, // shown at half size, like the currency icons
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_icon);
        _label.VerticalAlignment = VerticalAlignment.Center;
        AddChild(_label);
    }

    private static AtlasTexture Frame(int index) => new()
    {
        Atlas = GD.Load<Texture2D>(SheetPath),
        Region = new Rect2(index * FrameSize, 0, FrameSize, FrameSize),
    };

    /// <summary>Show <paramref name="wine"/> in the bottle at <paramref name="rarity"/> (null = the empty bottle).</summary>
    public void SetWine(Wine? wine, WineRarity rarity)
    {
        bool changed = wine != null && (wine != _shown || rarity != _shownRarity);
        _shown = wine;
        _shownRarity = rarity;
        _icon.Texture = wine != null ? _full : _empty;
        _label.Text = wine != null ? $"{wine.Name} · {rarity.ToString().ToUpper()}" : "";
        if (wine != null)
            _label.AddThemeColorOverride("font_color", FigmenterRules.RarityColor(rarity));
        if (changed && IsInsideTree())
            HudFx.Pop(_icon);
    }
}
