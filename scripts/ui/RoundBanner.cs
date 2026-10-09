using Godot;

namespace MyGame;

/// <summary>
/// The round block of the HUD: ROUND n / n LEFT (shown late in a round) / BEST n, stacked and centred on its anchor —
/// and the round-start intro, a big "ROUND n" that fades in at screen centre and flies into the ROUND label.
/// <see cref="RunManager"/> pushes the numbers through <see cref="HUD.SetRound"/>.
/// </summary>
public partial class RoundBanner : VBoxContainer
{
    // Placement: BlockAnchor is the screen point (as fractions of width/height) the block's TOP-CENTRE sits on —
    // (0.5, 0) = top-centre, (0.5, 0.5) = dead centre, (0.5, 0.85) = low centre — and BlockOffset nudges it from
    // there in pixels (+x right, +y down).
    private static readonly Vector2 BlockAnchor = new(0.5f, 0.0f);
    private static readonly Vector2 BlockOffset = new(0.0f, 30.0f);
    // Round intro: the big "ROUND n" fades in at screen centre, holds, then flies up + shrinks into the ROUND label.
    private const float IntroFadeIn = 0.25f;
    private const float IntroHold = 1.0f;
    private const float IntroFly = 0.7f;
    private const float IntroGlow = 1.8f;   // HDR multiplier on the accent while it's big (blooms), settling to 1

    private readonly Control _screen;   // the full-screen HUD root the intro flies across
    private readonly Label _roundLabel = UiStyle.HudLabel(UiStyle.HudTitle);
    private readonly Label _leftLabel = UiStyle.HudLabel(UiStyle.HudHeading);   // "n LEFT" — shown only once few quota enemies remain
    private readonly Label _bestLabel = UiStyle.HudLabel(UiStyle.HudMuted);
    private int _shownRound = 0; // the round whose intro has played (a higher one plays the intro again)
    private Label? _roundIntro;   // the big "ROUND n" flying from screen centre into _roundLabel (only while animating)

    public RoundBanner(Control screen)
    {
        _screen = screen;
        MouseFilter = MouseFilterEnum.Ignore;
        // Grows both ways from its anchor, so it stays centred on it.
        AnchorLeft = AnchorRight = BlockAnchor.X;
        AnchorTop = AnchorBottom = BlockAnchor.Y;
        OffsetLeft = OffsetRight = BlockOffset.X;
        OffsetTop = OffsetBottom = BlockOffset.Y;
        GrowHorizontal = GrowDirection.Both;
        AddThemeConstantOverride("separation", 2);
        foreach (var l in new[] { _roundLabel, _leftLabel, _bestLabel })
        {
            l.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(l);
        }
    }

    /// <summary>Show round <paramref name="round"/> (0 = before round 1: blank), <paramref name="left"/> quota enemies
    /// remaining (0 = hidden — RunManager only passes it once few remain), and the <paramref name="best"/> round record.</summary>
    public void SetRound(int round, int left, int best)
    {
        _roundLabel.Text = round > 0 ? $"ROUND {round}" : "";
        if (round > _shownRound)
            PlayRoundIntro(round);
        _shownRound = round; // a new run resets to 0, so round 1 plays again
        _leftLabel.Text = $"{left} LEFT";
        _leftLabel.Visible = left > 0;
        _bestLabel.Text = best > 0 ? $"BEST {best}" : "";
    }

    /// <summary>The round-start intro: a big "ROUND n" (the title font at exactly 2x the label's size) fades in at screen
    /// centre, holds, then glides up and shrinks to 0.5x onto <see cref="_roundLabel"/>'s rect — so it lands pixel-
    /// aligned wherever the round block is placed — while its glow settles to the label's colour, then hands over to
    /// the real label (kept invisible, not hidden, meanwhile so the block's layout doesn't jump).</summary>
    private async void PlayRoundIntro(int round)
    {
        _roundIntro?.QueueFree();
        var intro = new Label
        {
            Text = $"ROUND {round}",
            ThemeTypeVariation = UiStyle.HudTitle,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0),
        };
        intro.AddThemeFontSizeOverride("font_size", UiStyle.SizeTitle * 2);
        Color glow = new(UiStyle.Accent.R * IntroGlow, UiStyle.Accent.G * IntroGlow, UiStyle.Accent.B * IntroGlow);
        intro.AddThemeColorOverride("font_color", glow);
        _roundIntro = intro;
        _screen.AddChild(intro);
        _roundLabel.Modulate = new Color(1, 1, 1, 0);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); // let the round block lay out the new text
        if (_roundIntro != intro)
            return; // a newer intro replaced this one

        const float scale = 0.5f; // SizeTitle / (SizeTitle * 2)
        intro.Size = _roundLabel.Size / scale;
        intro.Position = (_screen.Size - intro.Size) / 2.0f;
        var t = intro.CreateTween();
        t.TweenProperty(intro, "modulate:a", 1.0f, IntroFadeIn);
        t.TweenInterval(IntroHold);
        t.SetParallel();
        t.TweenProperty(intro, "global_position", _roundLabel.GlobalPosition, IntroFly).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(intro, "scale", new Vector2(scale, scale), IntroFly).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(intro, "theme_override_colors/font_color", UiStyle.Accent, IntroFly);
        t.SetParallel(false);
        t.TweenCallback(Callable.From(() =>
        {
            _roundLabel.Modulate = Colors.White;
            intro.QueueFree();
            if (_roundIntro == intro)
                _roundIntro = null;
        }));
    }
}
