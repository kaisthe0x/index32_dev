using Godot;

namespace MyGame;

/// <summary>
/// The carried bottle of fig wine in the HUD, under the vials: one framed slot showing its name and rarity in the
/// rarity's colour, or a dim dash when he carries none. PLACEHOLDER look (text) until the bottle icon exists.
/// </summary>
public partial class WineSlot : PanelContainer
{
    private readonly Label _label = UiStyle.HudLabel(UiStyle.HudMuted);

    public WineSlot()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        AddChild(_label);
        SetWine(null, WineRarity.Common);
    }

    /// <summary>Show <paramref name="wine"/> at <paramref name="rarity"/> (null = the empty slot).</summary>
    public void SetWine(Wine? wine, WineRarity rarity)
    {
        Color color = wine != null ? FigmenterRules.RarityColor(rarity) : UiStyle.FrameDim;
        AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.RaisedBg, color, wine != null ? 2 : 1));
        _label.Text = wine != null ? $" {wine.Name} · {rarity.ToString().ToUpper()} " : " — ";
        if (wine != null)
            _label.AddThemeColorOverride("font_color", color);
        else
            _label.RemoveThemeColorOverride("font_color");
    }
}
