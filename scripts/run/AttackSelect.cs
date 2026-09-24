using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// The run-start "choose your attack" screen — an INVENTORY-style grid of weapon ICONS in a framed panel. Selecting
/// one (click / hover-focus) reveals its name + stats + description in the details pane below; a Confirm (or clicking
/// the already-selected weapon) locks it in for the whole run. RunManager opens this and awaits <c>chosen(id)</c>,
/// then equips it. Built in code, pauses the game. Also owns the shared <see cref="CardBody"/> used by <see cref="RewardUI"/>.
/// </summary>
[GlobalClass]
public partial class AttackSelect : CanvasLayer
{
    [Signal] public delegate void chosenEventHandler(string id);

    private const int Columns = 4;
    private const int GridGap = 10;                       // separation between cells (and the details pane width math)
    private const int DetailsHeight = 120;               // FIXED details-pane height (worst case) so the panel never resizes
    private static readonly Vector2 Cell = new(84, 84);   // icon button size

    private string _character = "khalid";
    private string _selectedId = "";
    private readonly Dictionary<string, Button> _cells = new();
    private Label _detailName;
    private Label _detailType;
    private Label _detailDesc;

    public AttackSelect()
    {
        Layer = 60;
        ProcessMode = ProcessModeEnum.Always; // keep working while the tree is paused
    }

    public void Open(string character)
    {
        _character = character;
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible; // grid is mouse-driven

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = Control.MouseFilterEnum.Stop };
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

        var title = new Label { Text = "CHOOSE YOUR ATTACK", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", UiStyle.TitleText);
        col.AddChild(title);

        var grid = new GridContainer { Columns = Columns };
        grid.AddThemeConstantOverride("h_separation", GridGap);
        grid.AddThemeConstantOverride("v_separation", GridGap);
        col.AddChild(grid);

        Button firstCell = null;
        foreach (string id in Actions.Ids(_character, "attacks"))
        {
            var a = Actions.GetAction(_character, "attacks", id);
            if (a == null)
                continue;
            var cell = MakeCell(id, a);
            grid.AddChild(cell);
            _cells[id] = cell;
            firstCell ??= cell;
            if (_selectedId == "")
                _selectedId = id;
        }

        col.AddChild(new HSeparator());

        // Details pane — a FIXED-height holder (sized for the worst case) that CLIPS overflow, so switching weapons
        // never resizes the panel (no bounce). Shorter weapons just leave blank space below. The panel itself is
        // fixed-size as a result, and canvas_items stretch scales the whole thing across resolutions.
        float paneW = Cell.X * Columns + GridGap * (Columns - 1);
        var detailsHolder = new Control { CustomMinimumSize = new Vector2(paneW, DetailsHeight), ClipContents = true };
        col.AddChild(detailsHolder);
        var details = new VBoxContainer();
        details.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide); // fill width, top-anchored; height = content (clipped)
        details.AddThemeConstantOverride("separation", 8);
        detailsHolder.AddChild(details);

        _detailName = new Label();
        _detailName.AddThemeFontSizeOverride("font_size", 20);
        _detailName.AddThemeColorOverride("font_color", UiStyle.TitleText);
        details.AddChild(_detailName);

        _detailType = new Label();
        _detailType.AddThemeFontSizeOverride("font_size", 13);
        _detailType.AddThemeColorOverride("font_color", new Color(0.60f, 0.78f, 1.0f)); // a small "kind" tag under the name
        details.AddChild(_detailType);

        _detailDesc = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailDesc.CustomMinimumSize = new Vector2(paneW, 0);
        _detailDesc.AddThemeFontSizeOverride("font_size", 13);
        _detailDesc.AddThemeColorOverride("font_color", UiStyle.BodyText);
        details.AddChild(_detailDesc);

        var confirm = new Button { Text = "CONFIRM", CustomMinimumSize = new Vector2(0, 34) };
        confirm.AddThemeFontSizeOverride("font_size", 16);
        confirm.Pressed += () => Pick(_selectedId);
        col.AddChild(confirm);

        Select(_selectedId);         // show the first weapon's details immediately
        firstCell?.GrabFocus();
    }

    /// <summary>An icon-only cell button. Click / hover / focus selects it (shows its details); clicking the ALREADY
    /// selected weapon confirms.</summary>
    private Button MakeCell(string id, Action action)
    {
        var cell = new Button { CustomMinimumSize = Cell, ClipContents = true };
        StyleCell(cell, false);
        var icon = new TextureRect
        {
            Texture = Icons.LoadPath(action.Icon),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        icon.OffsetLeft = 10; icon.OffsetTop = 10; icon.OffsetRight = -10; icon.OffsetBottom = -10; // ~64px icon inset in the cell
        cell.AddChild(icon);
        cell.Pressed += () => { if (_selectedId == id) Pick(id); else Select(id); };
        cell.MouseEntered += () => Select(id);
        cell.FocusEntered += () => Select(id);
        return cell;
    }

    private void Select(string id)
    {
        _selectedId = id;
        foreach (var (cid, cell) in _cells)
            StyleCell(cell, cid == id);
        var a = Actions.GetAction(_character, "attacks", id);
        if (a == null)
            return;
        _detailName.Text = a.Name;
        _detailType.Text = TypeLabel(a);
        _detailDesc.Text = a.Description;
    }

    /// <summary>The attack's "kind" tag — its Style, in player-friendly words.</summary>
    private static string TypeLabel(Action a) => a.Style switch
    {
        ActionStyle.Flurry => "Flurry",
        ActionStyle.Cooldown => "Charged",
        _ => "Combo",
    };

    /// <summary>Selected cell gets a bright gold frame + lifted bg; others a dim one.</summary>
    private static void StyleCell(Button cell, bool selected)
    {
        var sb = new StyleBoxFlat
        {
            BgColor = selected ? new Color(0.18f, 0.16f, 0.10f, 1f) : new Color(0.12f, 0.12f, 0.15f, 1f),
            BorderColor = selected ? UiStyle.Gold : new Color(0.30f, 0.30f, 0.36f),
        };
        sb.SetBorderWidthAll(selected ? 3 : 2);
        sb.SetCornerRadiusAll(4);
        cell.AddThemeStyleboxOverride("normal", sb);
        cell.AddThemeStyleboxOverride("hover", sb);
        cell.AddThemeStyleboxOverride("pressed", sb);
        cell.AddThemeStyleboxOverride("focus", sb);
    }

    /// <summary>A centered fixed-size icon over a wrapped label, filling a card and transparent to the mouse
    /// (so the parent Button gets the click). Shared card content (RewardUI); `cardW` bounds the label width.</summary>
    public static Control CardBody(Texture2D tex, string text, float cardW)
    {
        var box = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        box.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        box.AddThemeConstantOverride("separation", 6);
        var icon = new TextureRect
        {
            Texture = tex,
            CustomMinimumSize = new Vector2(64, 64), // icon is the hero of the card
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps, // smooth any scale (icons are hi-res, not pixel-art)
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        box.AddChild(icon);
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(cardW - 14, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        box.AddChild(label);
        return box;
    }

    private void Pick(string id)
    {
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Hidden; // back to play — hide the cursor
        EmitSignal(SignalName.chosen, id);
        QueueFree();
    }
}
