using Godot;

namespace MyGame;

/// <summary>
/// The HUD's list of what the player is carrying this run, pinned top-right and growing leftward to fit its widest
/// line: Needle Point shots (name, rank, value, in the rank's colour), held lottery items (name, what is left) and
/// Figmenter buffs (name and rarity in the rarity's colour, over the description). PLACEHOLDER look (text) until the buff icons exist.
/// </summary>
public partial class BuffList : VBoxContainer
{
    private const float DescriptionWidth = 258.0f;
    private const float RowSpacer = 5.0f;

    public BuffList()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        SetAnchorsPreset(LayoutPreset.TopRight);
        GrowHorizontal = GrowDirection.Begin;
        OffsetRight = -14.0f;
        OffsetTop = 14.0f;
    }

    /// <summary>Rebuild the list from the player's passives (call on grant / clear). Hidden when there is nothing to list.</summary>
    public void SetPassives(List<Passive> passives)
    {
        foreach (Node child in GetChildren())
            child.QueueFree();
        bool any = false;
        foreach (Passive p in passives)
        {
            if (p is Shot s)
            {
                any = true;
                AddChild(ShotLine(s));
                continue;
            }
            if (p is LotteryEffect item)
            {
                any = true;
                AddChild(LotteryLine(item));
                continue;
            }
            if (p is not Buff b)
                continue;
            any = true;
            var name = UiStyle.HudLabel(UiStyle.HudHeading);
            name.AddThemeColorOverride("font_color", FigmenterRules.RarityColor(b.Rarity));
            name.Text = $"{(b.Name != "" ? b.Name : b.Id)} · {b.Rarity.ToString().ToUpper()}";
            AddChild(name);

            var desc = UiStyle.HudLabel(UiStyle.HudMuted);
            desc.Text = b.Description;
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.CustomMinimumSize = new Vector2(DescriptionWidth, 0);
            AddChild(desc);

            AddChild(new Control { CustomMinimumSize = new Vector2(0, RowSpacer) });
        }
        Visible = any;
    }

    /// <summary>One owned Needle Point shot in the buff list: its name + rank in the rank's colour, and what it gives.</summary>
    private static Control ShotLine(Shot s)
    {
        var name = UiStyle.HudLabel(UiStyle.HudHeading);
        name.AddThemeColorOverride("font_color", NeedlePoint.RankColor(s.Rank));
        name.Text = $"{s.Def.Name} {Shot.Roman(s.Rank)} · {Shot.FormatValue(s.Def, s.Rank)}";
        return name;
    }

    /// <summary>One held lottery item in the buff list: its name and what it has left ("12 KILLS", "2 USES", "READY").</summary>
    private static Control LotteryLine(LotteryEffect e)
    {
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 8);
        var name = UiStyle.HudLabel(UiStyle.HudHeading);
        name.Text = e.Def.Name;
        line.AddChild(name);
        var left = UiStyle.HudLabel(UiStyle.HudMuted);
        left.Text = e.Def.Measure switch
        {
            LotteryMeasure.Kills => e.Remaining == 1 ? "1 KILL" : $"{e.Remaining} KILLS",
            LotteryMeasure.Uses => e.Remaining == 1 ? "1 USE" : $"{e.Remaining} USES",
            _ => "READY",
        };
        line.AddChild(left);
        return line;
    }
}
