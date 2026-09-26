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
        Layer = UiLayers.Menu;
        ProcessMode = ProcessModeEnum.Always; // keep working while the tree is paused
    }

    /// <summary>Show a card per entry ({id, name, desc}, optional tier/icon) under <paramref name="title"/> and pause
    /// until one is picked.</summary>
    public void Open(GArr rewards, string title)
    {
        GetTree().Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible; // need the cursor to click a card

        var dim = new ColorRect { Color = UiStyle.Backdrop, MouseFilter = Control.MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer { Theme = UiStyle.Theme };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 16);
        center.AddChild(col);

        col.AddChild(new Label { Text = title, ThemeTypeVariation = UiStyle.Title, HorizontalAlignment = HorizontalAlignment.Center });

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
                badge.AddThemeColorOverride("font_color", tcol);
                card.AddChild(badge);
                card.AddThemeStyleboxOverride("normal", UiStyle.Box(UiStyle.RaisedBg, tcol, 2));
                card.AddThemeStyleboxOverride("hover", UiStyle.Box(UiStyle.RaisedBg.Lightened(0.08f), tcol, 2));
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
