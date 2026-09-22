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
    private const int DetailsHeight = 230;               // FIXED details-pane height (worst case) so the panel never resizes
    private static readonly Vector2 Cell = new(84, 84);   // icon button size
    private static readonly Color Gold = new(0.85f, 0.72f, 0.18f);
    private static readonly Color PanelBg = new(0.06f, 0.06f, 0.08f, 0.98f);

    private string _character = "khalid";
    private string _selectedId = "";
    private readonly Dictionary<string, Button> _cells = new();
    private Label _detailName;
    private GridContainer _detailStats;
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
        panel.AddThemeStyleboxOverride("panel", Framed(PanelBg, Gold, 3, 6));
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
        title.AddThemeColorOverride("font_color", new Color(0.93f, 0.87f, 0.62f));
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
        _detailName.AddThemeColorOverride("font_color", new Color(0.93f, 0.87f, 0.62f));
        details.AddChild(_detailName);

        _detailStats = new GridContainer { Columns = 2 };
        _detailStats.AddThemeConstantOverride("h_separation", 14);
        _detailStats.AddThemeConstantOverride("v_separation", 3);
        details.AddChild(_detailStats);

        _detailDesc = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailDesc.CustomMinimumSize = new Vector2(paneW, 0);
        _detailDesc.AddThemeFontSizeOverride("font_size", 13);
        _detailDesc.AddThemeColorOverride("font_color", new Color(0.72f, 0.72f, 0.80f));
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
        BuildStats(a);
        _detailDesc.Text = a.Description;
    }

    /// <summary>Always show the FULL attribute set (same rows for every weapon, so the block never changes shape);
    /// absent values default to 0 / "None".</summary>
    private void BuildStats(Action a)
    {
        foreach (Node c in _detailStats.GetChildren())
            c.QueueFree();
        float kb = MaxSeg(a, s => s.Knockback);
        float stun = MaxSeg(a, s => s.Stun);
        float reach = MaxSeg(a, s => s.Extents.HasValue ? s.Extents.Value.X : (float?)null);
        AddStat("TYPE", a.Hit != null ? Pretty(a.Hit.Type.Key()) : "None");
        AddStat("DAMAGE", Dmg(a));
        AddStat("KNOCKBACK", Mathf.RoundToInt(kb).ToString());
        AddStat("STUN", $"{stun:0.0}s");
        AddStat("REACH", reach > 0 ? Mathf.RoundToInt(reach).ToString() : "None");
        AddStat("COOLDOWN", a.Cooldown > 0 ? $"{a.Cooldown:0.#}s" : "None");
        AddStat("STYLE", a.Style.ToString());
    }

    private void AddStat(string label, string value)
    {
        var l = new Label { Text = label };
        l.AddThemeFontSizeOverride("font_size", 12);
        l.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.62f));
        _detailStats.AddChild(l);
        var v = new Label { Text = value };
        v.AddThemeFontSizeOverride("font_size", 13);
        v.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f));
        _detailStats.AddChild(v);
    }

    private static float MaxSeg(Action a, System.Func<SegmentData, float?> pick)
    {
        float best = 0.0f;
        if (a.Hit != null)
            foreach (var s in a.Hit.Segments)
                if (pick(s) is float f && f > best)
                    best = f;
        return best;
    }

    /// <summary>Selected cell gets a bright gold frame + lifted bg; others a dim one.</summary>
    private static void StyleCell(Button cell, bool selected)
    {
        var sb = new StyleBoxFlat
        {
            BgColor = selected ? new Color(0.18f, 0.16f, 0.10f, 1f) : new Color(0.12f, 0.12f, 0.15f, 1f),
            BorderColor = selected ? Gold : new Color(0.30f, 0.30f, 0.36f),
        };
        sb.SetBorderWidthAll(selected ? 3 : 2);
        sb.SetCornerRadiusAll(4);
        cell.AddThemeStyleboxOverride("normal", sb);
        cell.AddThemeStyleboxOverride("hover", sb);
        cell.AddThemeStyleboxOverride("pressed", sb);
        cell.AddThemeStyleboxOverride("focus", sb);
    }

    /// <summary>Title-case an underscore/space id: "delayed_projectile" → "Delayed Projectile".</summary>
    private static string Pretty(string s)
    {
        var parts = s.Replace('_', ' ').Split(' ');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }

    private static StyleBoxFlat Framed(Color bg, Color border, int borderW, int radius)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetBorderWidthAll(borderW);
        s.SetCornerRadiusAll(radius);
        s.SetContentMarginAll(0);
        return s;
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

    /// <summary>Damage summary: a multi-segment combo shows "a / b / c", a single hit its damage, no hitbox "scene".</summary>
    private static string Dmg(Action action)
    {
        if (action.Hit == null || action.Hit.Segments.Length == 0)
            return "0";
        var parts = new List<string>();
        foreach (var s in action.Hit.Segments)
            parts.Add(s.Damage.HasValue ? Mathf.RoundToInt(s.Damage.Value).ToString() : "0");
        return parts.Count == 1 ? parts[0] : string.Join(" / ", parts);
    }

    private void Pick(string id)
    {
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Hidden; // back to play — hide the cursor
        EmitSignal(SignalName.chosen, id);
        QueueFree();
    }
}
