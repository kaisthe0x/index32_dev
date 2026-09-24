using Godot;

namespace MyGame;

/// <summary>
/// The game's ONE UI look — the "Arcane Void" dark-neon palette (near-black violet base, neon-violet frames, electric-blue
/// highlights), the Sixtyfour retro font, square pixel-style boxes, and a shared <see cref="Theme"/> that every menu root
/// applies (Controls under a CanvasLayer don't inherit a theme from the window, so each root sets
/// <c>Theme = UiStyle.Theme</c>). Named text styles are theme type variations (<see cref="Title"/>, <see cref="Heading"/>,
/// <see cref="Muted"/>, <see cref="PrimaryButton"/>) — set <c>ThemeTypeVariation</c> instead of per-node overrides.
/// <see cref="Install"/> also makes Sixtyfour the global fallback font, so un-themed text (world labels) matches.
/// </summary>
public static class UiStyle
{
    // --- palette ----------------------------------------------------------------------------------------------------
    public static readonly Color PanelBg = new(0.04f, 0.03f, 0.08f, 0.97f);   // panel fill
    public static readonly Color RaisedBg = new(0.10f, 0.07f, 0.18f);         // buttons, cells, rows
    public static readonly Color PrimaryBg = new(0.34f, 0.18f, 0.64f);        // the call-to-action button's fill
    public static readonly Color RowBg = new(0.58f, 0.34f, 1.0f, 0.06f);      // subtle list-row strip
    public static readonly Color Frame = new(0.58f, 0.34f, 1.0f);             // neon violet: panel borders, headings
    public static readonly Color FrameDim = new(0.30f, 0.20f, 0.52f);         // idle borders, rules
    public static readonly Color Accent = new(0.40f, 0.86f, 1.0f);            // electric blue: titles, hover, selected
    public static readonly Color Text = new(0.84f, 0.80f, 0.96f);             // pale lavender body text
    public static readonly Color TextBright = new(0.97f, 0.95f, 1.0f);        // text on a filled (primary) button
    public static readonly Color TextDim = new(0.56f, 0.52f, 0.70f);          // captions, disabled
    public static readonly Color Backdrop = new(0.02f, 0.01f, 0.05f, 0.72f);  // modal dim behind a menu

    // --- type (Sixtyfour is drawn on an 8px grid, so sizes are multiples of 8) ---------------------------------------
    public const int SizeBody = 8;
    public const int SizeTitle = 16;
    public const int SizeBanner = 32;

    // --- named styles (theme type variations) -------------------------------------------------------------------------
    public const string Title = "TitleLabel";       // big, CRT-scanline font, electric blue
    public const string Heading = "HeadingLabel";   // section header, neon violet
    public const string Muted = "MutedLabel";       // captions / secondary text
    public const string PrimaryButton = "PrimaryButton"; // the one call-to-action on a screen (filled deep violet)

    private const string FontPath = "res://assets/fonts/Sixtyfour-Regular-VariableFont_BLED,SCAN.ttf";
    // Sixtyfour's variable axes: SCAN (-53..100; negative = CRT scanline gaps) and BLED (0..100; phosphor bleed/weight).
    private const float BodyScan = 0.0f, BodyBleed = 15.0f;    // clean + a touch of weight, legible at 8px
    private const float TitleScan = -30.0f, TitleBleed = 45.0f; // visible scanlines + glow-y bleed for the CRT look

    public static readonly FontVariation BodyFont = MakeFont(BodyScan, BodyBleed);
    public static readonly FontVariation TitleFont = MakeFont(TitleScan, TitleBleed);

    private static Theme _theme;
    /// <summary>The shared menu theme (built once).</summary>
    public static Theme Theme => _theme ??= BuildTheme();

    /// <summary>Make Sixtyfour the global fallback font (any text without a theme font). Call once at startup, before
    /// any UI is built — the HUD autoload does, as the first UI to exist.</summary>
    public static void Install() => ThemeDB.FallbackFont = BodyFont;

    /// <summary>A square flat box (pixel look — no rounded corners) with an optional uniform border, no content margin.</summary>
    public static StyleBoxFlat Box(Color bg, Color border = default, int borderW = 0)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetBorderWidthAll(borderW);
        s.SetContentMarginAll(0);
        return s;
    }

    // --- construction -------------------------------------------------------------------------------------------------

    private static FontVariation MakeFont(float scan, float bleed)
    {
        var ts = TextServerManager.GetPrimaryInterface();
        return new FontVariation
        {
            BaseFont = GD.Load<FontFile>(FontPath),
            VariationOpentype = new Godot.Collections.Dictionary { { ts.NameToTag("SCAN"), scan }, { ts.NameToTag("BLED"), bleed } },
        };
    }

    private static Theme BuildTheme()
    {
        var t = new Theme { DefaultFont = BodyFont, DefaultFontSize = SizeBody };

        t.SetColor("font_color", "Label", Text);

        t.SetTypeVariation(Title, "Label");
        t.SetFont("font", Title, TitleFont);
        t.SetFontSize("font_size", Title, SizeTitle);
        t.SetColor("font_color", Title, Accent);

        t.SetTypeVariation(Heading, "Label");
        t.SetColor("font_color", Heading, Frame);

        t.SetTypeVariation(Muted, "Label");
        t.SetColor("font_color", Muted, TextDim);

        t.SetStylebox("panel", "PanelContainer", Box(PanelBg, Frame, 2));
        t.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = FrameDim, Thickness = 1 });

        StyleButtons(t, "Button", RaisedBg, FrameDim, Text, Accent);
        t.SetTypeVariation(PrimaryButton, "Button");
        StyleButtons(t, PrimaryButton, PrimaryBg, Frame, TextBright, Accent);
        return t;
    }

    /// <summary>Button states for <paramref name="type"/>: idle = <paramref name="bg"/> + <paramref name="border"/>;
    /// hover / pressed (toggled on) / focus light the border electric blue (text → <paramref name="hotText"/>); disabled dims.</summary>
    private static void StyleButtons(Theme t, string type, Color bg, Color border, Color text, Color hotText)
    {
        t.SetStylebox("normal", type, ButtonBox(bg, border));
        t.SetStylebox("hover", type, ButtonBox(bg.Lightened(0.08f), Accent));
        t.SetStylebox("pressed", type, ButtonBox(bg.Lerp(Frame, 0.35f), Accent));
        t.SetStylebox("hover_pressed", type, ButtonBox(bg.Lerp(Frame, 0.45f), Accent));
        t.SetStylebox("disabled", type, ButtonBox(bg.Darkened(0.4f), FrameDim.Darkened(0.3f)));
        var focus = Box(Colors.Transparent, Accent, 1);
        focus.DrawCenter = false;
        t.SetStylebox("focus", type, focus);

        t.SetColor("font_color", type, text);
        t.SetColor("font_hover_color", type, hotText);
        t.SetColor("font_pressed_color", type, hotText);
        t.SetColor("font_hover_pressed_color", type, hotText);
        t.SetColor("font_focus_color", type, text);
        t.SetColor("font_disabled_color", type, TextDim);
    }

    private static StyleBoxFlat ButtonBox(Color bg, Color border)
    {
        var s = Box(bg, border, 1);
        s.SetContentMarginAll(6);
        s.ContentMarginLeft = s.ContentMarginRight = 10;
        return s;
    }
}
