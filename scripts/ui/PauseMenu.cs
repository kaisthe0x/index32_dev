using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// The Esc pause menu: Resume + Settings (for now, where the HUD's health/Ruh gauge sits). Owned by the HUD, which
/// enables it only while a Player is bound (i.e. during a run). Esc opens it only when nothing else has the game paused
/// (the attack pick / buff menus own their own pause), and Esc or Resume closes it. Built in code; pauses the tree.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    /// <summary>The player picked a gauge placement (already persisted to <see cref="SaveData"/>).</summary>
    public event System.Action<GaugePlacement> GaugePlacementChanged;

    private readonly Dictionary<GaugePlacement, Button> _placementButtons = new();
    private Button _resume;
    private bool _enabled;

    /// <summary>Whether Esc may open the menu. Disabling while open closes it.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value && Visible)
                Close();
        }
    }

    public override void _Ready()
    {
        Layer = 110;                          // above the HUD (100)
        ProcessMode = ProcessModeEnum.Always; // must hear Esc while the tree is paused
        Visible = false;
        Build();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed("ui_cancel"))
            return;
        if (Visible)
            Close();
        else if (_enabled && !GetTree().Paused)
            Open();
        else
            return;
        GetViewport().SetInputAsHandled();
    }

    private void Open()
    {
        GaugePlacement current = SaveData.GetGaugePlacement();
        foreach (var (g, b) in _placementButtons)
            b.SetPressedNoSignal(g == current);
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Visible = true;
        _resume.GrabFocus();
    }

    private void Close()
    {
        Visible = false;
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Hidden; // back to play — hide the cursor
    }

    private void Choose(GaugePlacement g)
    {
        SaveData.SetGaugePlacement(g);
        GaugePlacementChanged?.Invoke(g);
    }

    private static string PlacementLabel(GaugePlacement g) => g switch
    {
        GaugePlacement.Screen => "FIXED",
        GaugePlacement.FollowKhalid => "FOLLOW KHALID",
        _ => g.ToString(),
    };

    // --- construction ---------------------------------------------------------

    private void Build()
    {
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiStyle.Framed(UiStyle.PanelBg, UiStyle.Gold, 3, 6));
        center.AddChild(panel);

        var pad = new MarginContainer();
        foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 20);
        panel.AddChild(pad);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        pad.AddChild(col);

        var title = MkLabel("PAUSED", 22, UiStyle.TitleText);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);

        _resume = new Button { Text = "RESUME", CustomMinimumSize = new Vector2(0, 34) };
        _resume.AddThemeFontSizeOverride("font_size", 16);
        _resume.Pressed += Close;
        col.AddChild(_resume);

        col.AddChild(new HSeparator());
        col.AddChild(MkLabel("SETTINGS", 14, UiStyle.Gold));

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        col.AddChild(row);
        var rowLabel = MkLabel("Health & Ruh gauge", 13, UiStyle.BodyText);
        rowLabel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(rowLabel);

        var group = new ButtonGroup();
        foreach (GaugePlacement g in System.Enum.GetValues<GaugePlacement>())
        {
            var b = new Button { Text = PlacementLabel(g), ToggleMode = true, ButtonGroup = group, CustomMinimumSize = new Vector2(0, 30) };
            b.AddThemeFontSizeOverride("font_size", 13);
            b.Toggled += on =>
            {
                if (on)
                    Choose(g);
            };
            row.AddChild(b);
            _placementButtons[g] = b;
        }
    }

    private static Label MkLabel(string text, int fontSize, Color col)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", fontSize);
        l.AddThemeColorOverride("font_color", col);
        return l;
    }
}
