using Godot;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// A pick-a-card popup: RunManager creates one, calls <see cref="Open"/> with a set of cards ({id, name, desc,
/// optional tier/icon}) + a title, and awaits <c>chosen(id)</c>; the player clicks a card, we un-pause and report
/// it. Built in code, pauses the game while up. Used by the fada-fig milestone BUFF menu.
/// </summary>
[GlobalClass]
public partial class RewardUI : CanvasLayer
{
    [Signal] public delegate void chosenEventHandler(string id);

    public RewardUI()
    {
        Layer = 50;
        ProcessMode = ProcessModeEnum.Always; // keep working while the tree is paused
    }

    /// <summary>Show a card per entry ({id, name, desc}, optional tier/icon) under <paramref name="title"/> and pause
    /// until one is picked.</summary>
    public void Open(GArr rewards, string title)
    {
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible; // need the cursor to click a card

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.62f), MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 16);
        center.AddChild(col);

        var titleLabel = new Label
        {
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        col.AddChild(titleLabel);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 18);
        col.AddChild(row);

        const float cardW = 190.0f;
        Button first = null;
        foreach (Variant rv in rewards)
        {
            var r = rv.As<GDict>();
            var card = new Button { CustomMinimumSize = new Vector2(cardW, 150), ClipContents = true };
            // A swap card carries its Action's own icon PATH; a buff falls back to the buff-id registry icon.
            Texture2D tex = r.ContainsKey("icon") ? Icons.LoadPath(r["icon"].AsString()) : Icons.Texture($"buff:{r["id"].AsString()}");
            card.AddChild(AttackSelect.CardBody(tex, $"{r["name"].AsString()}\n\n{r["desc"].AsString()}", cardW));
            string id = r["id"].AsString();
            card.Pressed += () => Pick(id);
            row.AddChild(card);
            // Tiered reward cards carry a Tier -- badge it + tint the border.
            if (r.ContainsKey("tier"))
            {
                var tier = (Tier)r["tier"].As<int>();
                Color tcol = Tiers.ColorOf(tier);
                var badge = new Label { Text = Tiers.Label(tier).ToUpper(), Position = new Vector2(8, 6) };
                badge.AddThemeFontSizeOverride("font_size", 12);
                badge.AddThemeColorOverride("font_color", tcol);
                badge.AddThemeColorOverride("font_outline_color", Colors.Black);
                badge.AddThemeConstantOverride("outline_size", 3);
                card.AddChild(badge);
                var sb = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.15f, 0.96f), BorderColor = tcol };
                sb.SetBorderWidthAll(2);
                sb.SetCornerRadiusAll(4);
                card.AddThemeStyleboxOverride("normal", sb);
                card.AddThemeStyleboxOverride("hover", sb);
            }
            first ??= card;
        }
        first?.GrabFocus();
    }

    private void Pick(string id)
    {
        GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Hidden; // back to play — hide the cursor
        EmitSignal(SignalName.chosen, id);
        QueueFree();
    }
}
