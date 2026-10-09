using Godot;

namespace MyGame;

/// <summary>
/// The carried Dekken vials in the HUD, under the currency counters: one framed slot per carry slot
/// (<see cref="Dekken.CarrySlots"/>).
/// </summary>
public partial class VialRow : HBoxContainer
{
    public VialRow()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 4);
    }

    /// <summary>Show the carried vials: one framed slot per carry slot — a held vial's
    /// name, the <paramref name="selected"/> one (what the drink key drinks) framed in the accent colour, an empty slot
    /// dim. PLACEHOLDER look (text) until the vial icons exist.</summary>
    public void SetVials(IReadOnlyList<string> names, int selected)
    {
        foreach (Node child in GetChildren())
            child.QueueFree();
        for (int i = 0; i < Dekken.CarrySlots; i++)
        {
            bool held = i < names.Count;
            bool isSelected = held && i == selected;
            var slot = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            slot.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.RaisedBg, isSelected ? UiStyle.Accent : UiStyle.FrameDim, isSelected ? 2 : 1));
            var label = UiStyle.HudLabel(held ? UiStyle.HudHeading : UiStyle.HudMuted);
            label.Text = held ? $" {names[i]} " : " — ";
            slot.AddChild(label);
            AddChild(slot);
        }
    }
}
