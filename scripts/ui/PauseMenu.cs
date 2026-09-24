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
    private ColorRect _dim;
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
        _dim.Color = UiStyle.Backdrop; // the UI palette may have been recoloured since Build
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
        _dim = new ColorRect { MouseFilter = Control.MouseFilterEnum.Stop };
        _dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_dim);

        var center = new CenterContainer { Theme = UiStyle.Theme };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var pad = new MarginContainer();
        foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 20);
        panel.AddChild(pad);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        pad.AddChild(col);

        col.AddChild(new Label { Text = "PAUSED", ThemeTypeVariation = UiStyle.Title, HorizontalAlignment = HorizontalAlignment.Center });

        _resume = new Button { Text = "RESUME", ThemeTypeVariation = UiStyle.PrimaryButton };
        _resume.Pressed += Close;
        col.AddChild(_resume);

        col.AddChild(new HSeparator());
        col.AddChild(new Label { Text = "SETTINGS", ThemeTypeVariation = UiStyle.Heading });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        col.AddChild(row);
        row.AddChild(new Label { Text = "Health & Ruh gauge", ThemeTypeVariation = UiStyle.Muted, VerticalAlignment = VerticalAlignment.Center });

        var group = new ButtonGroup();
        foreach (GaugePlacement g in System.Enum.GetValues<GaugePlacement>())
        {
            var b = new Button { Text = PlacementLabel(g), ToggleMode = true, ButtonGroup = group };
            b.Toggled += on =>
            {
                if (on)
                    Choose(g);
            };
            row.AddChild(b);
            _placementButtons[g] = b;
        }
    }
}
