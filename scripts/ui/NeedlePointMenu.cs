using Godot;

namespace MyGame;

/// <summary>
/// The Needle Point menu (open in the break between rounds): every shot as a row — its name in its LEVEL colour, what it
/// does at that level, its status (active + rounds left), and two actions: <b>BUY</b> (Lira — active now, through the
/// next round; renews an active one) and <b>UPGRADE</b> (figs — +1 level for the run, and granted now). The rows rebuild
/// after every action so prices, balances and states stay current. Pauses the game; E / Esc / DONE close it.
/// </summary>
public partial class NeedlePointMenu : CanvasLayer
{
    // Fixed column widths (sized for the longest text: Sixtyfour is monospaced, ~8 px per glyph) + clipped labels, so no
    // row's text can push its buttons out of line with the rows around it.
    private const float NameWidth = 136.0f;
    private const float EffectWidth = 232.0f;
    private const float StatusWidth = 232.0f;
    private static readonly Vector2 BuySlot = new(70, 0);      // fixed button widths so the columns line up down the list
    private static readonly Vector2 UpgradeSlot = new(130, 0);

    private ShotLedger _ledger;
    private Player _player;
    private VBoxContainer _rows;
    private Label _balance;

    public NeedlePointMenu()
    {
        Layer = UiLayers.Menu;
        ProcessMode = ProcessModeEnum.Always; // keep working while the tree is paused
    }

    public void Open(ShotLedger ledger, Player player)
    {
        _ledger = ledger;
        _player = player;
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

        col.AddChild(new Label { Text = "NEEDLE POINT", ThemeTypeVariation = UiStyle.Title, HorizontalAlignment = HorizontalAlignment.Center });
        _balance = new Label { ThemeTypeVariation = UiStyle.Heading, HorizontalAlignment = HorizontalAlignment.Center };
        col.AddChild(_balance);
        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 4);
        col.AddChild(_rows);
        col.AddChild(new Label
        {
            Text = "Shots are active now and last through the next round · UPGRADE lasts the run",
            ThemeTypeVariation = UiStyle.Muted, HorizontalAlignment = HorizontalAlignment.Center,
        });
        var done = new Button { Text = "DONE", ThemeTypeVariation = UiStyle.PrimaryButton };
        done.Pressed += Close;
        col.AddChild(done);

        Rebuild();
        done.GrabFocus();
    }

    public override void _Input(InputEvent e)
    {
        if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("interact"))
        {
            Close();
            GetViewport().SetInputAsHandled(); // don't also open the pause menu
        }
    }

    private void Rebuild()
    {
        _balance.Text = $"LIRA {_player.lira}    FIGS {_player.fada_figs}";
        foreach (Node child in _rows.GetChildren())
            child.QueueFree();
        foreach (ShotDef def in NeedlePoint.SHOTS)
            _rows.AddChild(Row(def));
    }

    private Control Row(ShotDef def)
    {
        string id = def.Id;
        int level = _ledger.Level(id);
        var strip = new PanelContainer { ThemeTypeVariation = UiStyle.RowPanel };
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        strip.AddChild(row);

        var name = new Label { Text = $"{def.Name}  {Roman(level + 1)}", CustomMinimumSize = new Vector2(NameWidth, 0), ClipText = true };
        name.AddThemeColorOverride("font_color", NeedlePoint.LevelColor(level)); // the level colour is semantic
        row.AddChild(name);
        string rounds = def.Rounds == 1 ? "1 round" : $"{def.Rounds} rounds";
        row.AddChild(new Label { Text = $"{Shot.EffectText(def, level)} · {rounds}", CustomMinimumSize = new Vector2(EffectWidth, 0), ClipText = true });
        row.AddChild(new Label { Text = Status(id), ThemeTypeVariation = UiStyle.Muted, CustomMinimumSize = new Vector2(StatusWidth, 0), ClipText = true });

        row.AddChild(Action($"BUY {_ledger.Price(id)}", BuySlot,
            _ledger.CanRenew(id) && _player.lira >= _ledger.Price(id), () => _ledger.Buy(id)));
        bool maxed = _ledger.AtMaxLevel(id);
        row.AddChild(Action(maxed ? "MAX" : $"UPGRADE {_ledger.UpgradeFigs(id)} FIGS", UpgradeSlot,
            !maxed && _player.fada_figs >= _ledger.UpgradeFigs(id), () => _ledger.Upgrade(id)));
        return strip;
    }

    /// <summary>What the shot is doing right now, in words.</summary>
    private string Status(string id)
    {
        if (!_ledger.IsActive(id))
            return "";
        int left = _ledger.RoundsLeft(id);
        return left == 1 ? "ACTIVE · 1 ROUND" : $"ACTIVE · {left} ROUNDS";
    }

    /// <summary>A row action button of a fixed <paramref name="slot"/> width: <paramref name="enabled"/> gates it; pressing
    /// runs <paramref name="act"/> and, if it went through, refreshes the menu.</summary>
    private Button Action(string text, Vector2 slot, bool enabled, System.Func<bool> act)
    {
        var b = new Button { Text = text, Disabled = !enabled, CustomMinimumSize = slot };
        b.Pressed += () =>
        {
            if (!act())
                return;
            GetNodeOrNull<Sfx>("/root/Sfx")?.play("buff_select"); // PLACEHOLDER cue
            Rebuild();
        };
        return b;
    }

    private static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };

    private void Close()
    {
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Hidden; // back to play — hide the cursor
        QueueFree();
    }
}
