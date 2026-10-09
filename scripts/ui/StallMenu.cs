using Godot;

namespace MyGame;

/// <summary>
/// The shared frame of a stall's shop menu (Needle Point, Dekken): a dimmed backdrop, a framed panel with the title,
/// the player's Lira / fig balance, a list of rows the subclass fills (<see cref="FillRows"/>), a one-line footer, and
/// DONE. Pauses the game while up; E / Esc / DONE close it. Rows rebuild after every purchase (<see cref="Refresh"/>) so
/// prices, balances and states stay current. Subclasses build rows from the helpers here so the menus look alike.
/// </summary>
public abstract partial class StallMenu : CanvasLayer
{
    protected Player Player { get; private set; } = null!;   // set by OpenFrame, before anything reads it
    private VBoxContainer _rows = null!;
    private Label _balance = null!;

    protected StallMenu()
    {
        Layer = UiLayers.Menu;
        ProcessMode = ProcessModeEnum.Always; // keep working while the tree is paused
    }

    /// <summary>Put up the menu for <paramref name="player"/> and pause.</summary>
    protected void OpenFrame(Player player, string title, string footer)
    {
        Player = player;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        var dim = new ColorRect { Color = UiStyle.Backdrop, MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);
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
        col.AddThemeConstantOverride("separation", 12);
        pad.AddChild(col);

        col.AddChild(new Label { Text = title, ThemeTypeVariation = UiStyle.Title, HorizontalAlignment = HorizontalAlignment.Center });
        _balance = new Label { ThemeTypeVariation = UiStyle.Heading, HorizontalAlignment = HorizontalAlignment.Center };
        col.AddChild(_balance);
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 4);
        col.AddChild(_rows);
        col.AddChild(new Label { Text = footer, ThemeTypeVariation = UiStyle.Muted, HorizontalAlignment = HorizontalAlignment.Center });
        var done = new Button { Text = "DONE", ThemeTypeVariation = UiStyle.PrimaryButton };
        done.Pressed += Close;
        col.AddChild(done);

        Refresh();
        done.GrabFocus();
    }

    /// <summary>Add this menu's rows to <paramref name="rows"/> (called on open and after every purchase).</summary>
    protected abstract void FillRows(VBoxContainer rows);

    /// <summary>Rebuild the balance line and every row.</summary>
    protected void Refresh()
    {
        _balance.Text = $"LIRA {Player.Lira}    FIGS {Player.FadaFigs}";
        foreach (Node child in _rows.GetChildren())
            child.QueueFree();
        FillRows(_rows);
    }

    public override void _Input(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("interact"))
        {
            Close();
            GetViewport().SetInputAsHandled(); // don't also open the pause menu
        }
    }

    /// <summary>A row strip + its horizontal box (add the row's cells to the box).</summary>
    protected static (PanelContainer Strip, HBoxContainer Row) NewRow()
    {
        var strip = new PanelContainer { ThemeTypeVariation = UiStyle.RowPanel };
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        strip.AddChild(row);
        return (strip, row);
    }

    /// <summary>A fixed-width, clipped text cell — so no row's text can push its buttons out of line with the others.
    /// Sixtyfour is monospaced (~8 px per glyph): size <paramref name="width"/> for the longest text.</summary>
    protected static Label Cell(string text, float width, string style = "")
    {
        var label = new Label { Text = text, CustomMinimumSize = new Vector2(width, 0), ClipText = true };
        if (style != "")
            label.ThemeTypeVariation = style;
        return label;
    }

    /// <summary>A row action button of a fixed <paramref name="width"/>: <paramref name="enabled"/> gates it; pressing runs
    /// <paramref name="act"/> and, if it went through, plays the purchase cue and refreshes the menu.</summary>
    protected Button ActionButton(string text, float width, bool enabled, System.Func<bool> act)
    {
        var b = new Button { Text = text, Disabled = !enabled, CustomMinimumSize = new Vector2(width, 0) };
        b.Pressed += () =>
        {
            if (!act())
                return;
            GetNodeOrNull<Sfx>("/root/Sfx")?.Play("buff_select"); // PLACEHOLDER cue
            Refresh();
        };
        return b;
    }

    protected void Close()
    {
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Hidden; // back to play — hide the cursor
        QueueFree();
    }
}
