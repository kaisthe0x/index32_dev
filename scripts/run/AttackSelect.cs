using Godot;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// The run-start "choose your attack" screen. The player picks ONE attack, LOCKED for the whole run (only
/// buffed after, via the Attack door). RunManager opens this at the start of every run and awaits
/// <c>chosen(id)</c>, then equips it. Built in code, pauses the game. C# port of <c>scripts/run/attack_select.gd</c>.
/// Also owns the shared <see cref="CardBody"/> used by <see cref="RewardUI"/>.
/// </summary>
[GlobalClass]
public partial class AttackSelect : CanvasLayer
{
    [Signal] public delegate void chosenEventHandler(string id);

    private const int Columns = 4;
    private static readonly Vector2 Card = new(150, 150);
    private static readonly Vector2 View = new(660, 470);

    public AttackSelect()
    {
        Layer = 60;
        ProcessMode = ProcessModeEnum.Always; // keep working while the tree is paused
    }

    public void Open(string character)
    {
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible; // need the cursor to click a card

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.72f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        center.AddChild(col);

        var title = new Label { Text = "CHOOSE YOUR ATTACK", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        col.AddChild(title);

        var sub = new Label
        {
            Text = "Locked in for the whole run — buff it at Attack doors.",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        sub.AddThemeFontSizeOverride("font_size", 12);
        sub.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f));
        col.AddChild(sub);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = View,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        col.AddChild(scroll);

        var grid = new GridContainer { Columns = Columns };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 14);
        scroll.AddChild(grid);

        Button first = null;
        foreach (string id in Actions.Ids(character, "attacks"))
        {
            var a = Actions.GetAction(character, "attacks", id);
            if (a == null)
                continue;
            var card = MakeCard(id, a);
            grid.AddChild(card);
            first ??= card;
        }
        first?.GrabFocus();
    }

    private Button MakeCard(string id, Action action)
    {
        var card = new Button { CustomMinimumSize = Card, ClipContents = true };
        var sb = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.15f, 0.96f), BorderColor = new Color(0.55f, 0.47f, 0.16f) };
        sb.SetBorderWidthAll(2);
        sb.SetCornerRadiusAll(4);
        card.AddThemeStyleboxOverride("normal", sb);
        card.AddThemeStyleboxOverride("hover", sb);
        string text = $"{action.Name}\ndmg {Dmg(action)}";
        card.AddChild(CardBody(Icons.LoadPath(action.Icon), text, Card.X));
        card.Pressed += () => Pick(id);
        return card;
    }

    /// <summary>A centered fixed-size icon over a wrapped label, filling a card and transparent to the mouse
    /// (so the parent Button gets the click). Shared card content; `cardW` bounds the label width.</summary>
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
            CustomMinimumSize = new Vector2(46, 46),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
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

    /// <summary>Damage summary: a multi-segment combo shows "a/b/c", a single hit its damage, no hitbox "scene".</summary>
    private static string Dmg(Action action)
    {
        if (action.Hit == null || action.Hit.Segments.Length == 0)
            return "scene";
        var parts = new List<string>();
        foreach (var s in action.Hit.Segments)
            parts.Add(s.Damage.HasValue ? s.Damage.Value.ToString() : "0");
        return parts.Count == 1 ? parts[0] : string.Join("/", parts);
    }

    private void Pick(string id)
    {
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Hidden; // back to play — hide the cursor
        EmitSignal(SignalName.chosen, id);
        QueueFree();
    }
}
