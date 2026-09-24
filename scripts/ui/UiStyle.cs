using Godot;

namespace MyGame;

/// <summary>
/// The game's ONE UI look — a dark-neon palette (near-black base, neon frames, bright highlights; "Arcane Void" violet +
/// electric blue by default, player-recolourable per colour scheme via <see cref="SetColors"/>), the Sixtyfour retro
/// font, square pixel-style boxes, and a shared <see cref="Theme"/> that every menu root
/// applies (Controls under a CanvasLayer don't inherit a theme from the window, so each root sets
/// <c>Theme = UiStyle.Theme</c>). Named text styles are theme type variations (<see cref="Title"/>, <see cref="Heading"/>,
/// <see cref="Muted"/>, <see cref="PrimaryButton"/>) — set <c>ThemeTypeVariation</c> instead of per-node overrides.
/// <see cref="Install"/> also makes Sixtyfour the global fallback font, so un-themed text (world labels) matches.
/// </summary>
public static class UiStyle
{
    // --- palette ----------------------------------------------------------------------------------------------------
    // Two player picks drive it: FRAME (the UI's main colour) and ACCENT (highlights). Every other role is DERIVED from
    // the frame's hue (see Derive), so any pick yields a coherent set. Defaults = the "Arcane Void" look.
    public static readonly Color DefaultFrame = new(0.58f, 0.34f, 1.0f);   // neon violet
    public static readonly Color DefaultAccent = new(0.40f, 0.86f, 1.0f);  // electric blue

    /// <summary>Keys of a colour scheme's "ui" picks dict (SaveData / PalettePreview).</summary>
    public const string PickFrame = "frame";
    public const string PickAccent = "accent";

    public static Color Frame { get; private set; }       // panel borders, headings
    public static Color Accent { get; private set; }      // titles, hover, selected
    public static Color PanelBg { get; private set; }     // panel fill
    public static Color RaisedBg { get; private set; }    // buttons, cells, rows
    public static Color PrimaryBg { get; private set; }   // the call-to-action button's fill
    public static Color RowBg { get; private set; }       // subtle list-row strip
    public static Color FrameDim { get; private set; }    // idle borders, rules
    public static Color Text { get; private set; }        // body text (a pale tint of the frame hue)
    public static Color TextBright { get; private set; }  // text on a filled (primary) button
    public static Color TextDim { get; private set; }     // captions, disabled
    public static Color Backdrop { get; private set; }    // modal dim behind a menu

    // --- type (Sixtyfour is drawn on an 8px grid, so sizes are multiples of 8) ---------------------------------------
    public const int SizeBody = 8;
    public const int SizeTitle = 16;
    public const int SizeBanner = 32;

    // --- named styles (theme type variations) -------------------------------------------------------------------------
    public const string Title = "TitleLabel";       // big, CRT-scanline font, accent colour
    public const string Heading = "HeadingLabel";   // section header, frame colour
    public const string Muted = "MutedLabel";       // captions / secondary text
    public const string PrimaryButton = "PrimaryButton"; // the one call-to-action on a screen (filled, deep frame hue)
    public const string RowPanel = "RowPanel";      // a subtle list-row strip (PanelContainer)
    // HUD text floats over the world, so its styles carry a black outline.
    public const string HudHeading = "HudHeading";  // frame colour
    public const string HudValue = "HudValue";      // accent colour, title size (counters)
    public const string HudMuted = "HudMuted";      // secondary text

    private const string FontPath = "res://assets/fonts/Sixtyfour-Regular-VariableFont_BLED,SCAN.ttf";
    // Sixtyfour's variable axes: SCAN (-53..100; negative = CRT scanline gaps) and BLED (0..100; phosphor bleed/weight).
    private const float BodyScan = 0.0f, BodyBleed = 15.0f;    // clean + a touch of weight, legible at 8px
    private const float TitleScan = -30.0f, TitleBleed = 45.0f; // visible scanlines + glow-y bleed for the CRT look

    public static readonly FontVariation BodyFont = MakeFont(BodyScan, BodyBleed);
    public static readonly FontVariation TitleFont = MakeFont(TitleScan, TitleBleed);

    private static Theme _theme;
    /// <summary>The shared menu theme (built once; <see cref="SetColors"/> repaints it in place, so every Control using it
    /// updates live).</summary>
    public static Theme Theme => _theme ??= Populate(new Theme());

    static UiStyle() => Derive(DefaultFrame, DefaultAccent);

    /// <summary>Recolour the whole UI from the player's two picks: re-derive the palette and repaint the shared theme.
    /// Nodes that copy a palette value when they're BUILT (menus opened later) pick it up then.</summary>
    public static void SetColors(Color frame, Color accent)
    {
        Derive(frame, accent);
        if (_theme != null)
            Populate(_theme);
    }

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

    /// <summary>Each derived role keeps its default brightness (value) — so panels stay near-black and text stays pale —
    /// takes the FRAME's hue, and scales its saturation by the frame's (a grey pick gives a neutral UI).</summary>
    private static void Derive(Color frame, Color accent)
    {
        float h = frame.H;
        float satScale = frame.S / DefaultFrame.S;
        Color Tone(float s, float v, float a = 1.0f) => Color.FromHsv(h, Mathf.Clamp(s * satScale, 0.0f, 1.0f), v, a);

        Frame = frame;
        Accent = accent;
        PanelBg = Tone(0.62f, 0.08f, 0.97f);
        RaisedBg = Tone(0.61f, 0.18f);
        PrimaryBg = Tone(0.72f, 0.64f);
        RowBg = new Color(frame, 0.06f);
        FrameDim = Tone(0.62f, 0.52f);
        Text = Tone(0.17f, 0.96f);
        TextBright = Tone(0.05f, 1.0f);
        TextDim = Tone(0.26f, 0.70f);
        Backdrop = Tone(0.80f, 0.05f, 0.72f);
    }

    private static FontVariation MakeFont(float scan, float bleed)
    {
        var ts = TextServerManager.GetPrimaryInterface();
        return new FontVariation
        {
            BaseFont = GD.Load<FontFile>(FontPath),
            VariationOpentype = new Godot.Collections.Dictionary { { ts.NameToTag("SCAN"), scan }, { ts.NameToTag("BLED"), bleed } },
        };
    }

    /// <summary>(Re)write every palette-dependent item of <paramref name="t"/> from the current palette.</summary>
    private static Theme Populate(Theme t)
    {
        t.DefaultFont = BodyFont;
        t.DefaultFontSize = SizeBody;

        t.SetColor("font_color", "Label", Text);

        t.SetTypeVariation(Title, "Label");
        t.SetFont("font", Title, TitleFont);
        t.SetFontSize("font_size", Title, SizeTitle);
        t.SetColor("font_color", Title, Accent);

        t.SetTypeVariation(Heading, "Label");
        t.SetColor("font_color", Heading, Frame);

        t.SetTypeVariation(Muted, "Label");
        t.SetColor("font_color", Muted, TextDim);

        HudLabel(t, HudHeading, Frame);
        HudLabel(t, HudValue, Accent);
        t.SetFontSize("font_size", HudValue, SizeTitle);
        HudLabel(t, HudMuted, TextDim);

        t.SetStylebox("panel", "PanelContainer", Box(PanelBg, Frame, 2));
        t.SetTypeVariation(RowPanel, "PanelContainer");
        t.SetStylebox("panel", RowPanel, Box(RowBg));
        t.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = FrameDim, Thickness = 1 });

        StyleButtons(t, "Button", RaisedBg, FrameDim, Text, Accent);
        t.SetTypeVariation(PrimaryButton, "Button");
        StyleButtons(t, PrimaryButton, PrimaryBg, Frame, TextBright, Accent);
        return t;
    }

    private static void HudLabel(Theme t, string type, Color color)
    {
        t.SetTypeVariation(type, "Label");
        t.SetColor("font_color", type, color);
        t.SetColor("font_outline_color", type, Colors.Black);
        t.SetConstant("outline_size", type, 3);
    }

    /// <summary>Button states for <paramref name="type"/>: idle = <paramref name="bg"/> + <paramref name="border"/>;
    /// hover / pressed (toggled on) / focus light the border in the accent (text → <paramref name="hotText"/>); disabled dims.</summary>
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
