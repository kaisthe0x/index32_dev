using Godot;
using System;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// Character colour-customisation preview + SCHEME manager (the pre-game main scene). C# port of
/// <c>scripts/ui/palette_preview.gd</c>. Runs Khalid's `idle` cycle on an adjustable backdrop with a live-recoloured
/// portrait, a colour picker per body part + per power family, and up to SaveData.MAX_SCHEMES saved schemes you switch,
/// Save, and Start a run with. BODY recolour uses the material-aware palette LUT (<see cref="PaletteConfig"/>); POWERS
/// recolour via <see cref="VfxPalette"/>; the portrait follows body picks by hue. Selecting a slot loads + makes it
/// active; "Save" writes the current picks into the active slot; "Start run" only applies them (Save is the commit).
/// </summary>
public partial class PalettePreview : Control
{
    private const string FRAMES_PATH = "res://resources/characters/khalid.tres";
    private const string PORTRAIT_PATH = "res://assets/portraits/Khalid.png";
    private const string RUN_SCENE = "res://scenes/arena.tscn";
    private const float SPRITE_SCALE = 5.0f;
    private const string SAMPLE_FX = "res://vfx/character/khalid/run/default/run_default.tscn";

    // Body pickers, in MATERIALS order -> a friendly label. All six recolour (pants included).
    private static readonly Dictionary<string, string> BODY_LABELS = new()
    {
        ["hair"] = "Hair (red)", ["skin"] = "Skin (teal)", ["jacket"] = "Coat (brown)",
        ["trim"] = "Trim (yellow)", ["pants"] = "Pants (green)", ["metal"] = "Metal (grey)",
    };

    // Power/VFX families (dedicated). Labelled Power 1/2/3 in the UI; internal keys stay red/gold/teal.
    private static readonly Dictionary<string, Color> POWER_FAMILIES = new()
    {
        ["red"] = new Color(0.77f, 0.04f, 0.04f), ["gold"] = new Color(0.82f, 0.75f, 0.08f),
        ["teal"] = new Color(0.08f, 0.53f, 0.49f),
    };
    private static readonly Dictionary<string, string> POWER_LABELS = new()
        { ["red"] = "Power 1", ["gold"] = "Power 2", ["teal"] = "Power 3" };
    private static readonly string[] POWER_ORDER = { "red", "gold", "teal" };

    private ShaderMaterial _mat, _portraitMat;
    private ColorRect _backdrop;
    private AnimatedSprite2D _sprite;
    private TextureRect _portrait;
    private PanelContainer _portraitFrame;
    private ScrollContainer _scroll;
    private VBoxContainer _col;
    private Node2D _sample;
    private readonly GDict _bodyPicks = new();   // material -> picked Color (missing = default shade ramp)
    private readonly GDict _powerPicks = new();  // family -> picked Color (missing = family default)
    private readonly Dictionary<string, ColorPickerButton> _bodyPickers = new();
    private readonly Dictionary<string, ColorPickerButton> _powerPickers = new();
    private readonly List<Button> _slotButtons = new();
    private Button _saveButton;
    private int _activeSlot = -1;  // -1 == the built-in DEFAULT look; 0..MAX-1 == a saved slot

    public override void _Ready()
    {
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Theme = UiStyle.Theme;
        Input.MouseMode = Input.MouseModeEnum.Visible; // pre-game colour pickers are mouse-driven (and reset it if we came from a run)

        // Open on the active scheme (applies on startup) -- may be the DEFAULT look (-1).
        _activeSlot = SaveData.ActiveScheme();
        LoadActive();

        _backdrop = new ColorRect { Color = new Color(0.04f, 0.045f, 0.06f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_backdrop);

        // The SAME builder the in-game player uses, so the preview matches the run exactly.
        _mat = PaletteConfig.MakeMaterial(_bodyPicks);
        _sprite = new AnimatedSprite2D { SpriteFrames = GD.Load<SpriteFrames>(FRAMES_PATH), Material = _mat };
        _sprite.Scale = new Vector2(SPRITE_SCALE, SPRITE_SCALE);
        if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation("idle"))
            _sprite.Play("idle");
        AddChild(_sprite);

        // Portrait, recoloured to follow the body picks by hue -- in a framed panel, scaled to fit.
        _portraitMat = PaletteConfig.MakePortraitMaterial(_bodyPicks);
        _portrait = new TextureRect
        {
            Texture = GD.Load<Texture2D>(PORTRAIT_PATH), Material = _portraitMat,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(240, 240),
        };
        _portraitFrame = FramedBox();
        var pv = new VBoxContainer();
        pv.AddThemeConstantOverride("separation", 6);
        pv.AddChild(_portrait);
        pv.AddChild(new Label { Text = "PORTRAIT", ThemeTypeVariation = UiStyle.Muted, HorizontalAlignment = HorizontalAlignment.Center });
        _portraitFrame.AddChild(pv);
        AddChild(_portraitFrame);

        BuildControls();
        PushStatics();
        RebuildSample();
        Resized += Reposition;
        Reposition();
        Callable.From(Reposition).CallDeferred();  // recompute once children have real sizes (scroll cap needs them)
    }

    private void Reposition()
    {
        if (_sprite != null)
            _sprite.Position = new Vector2(Size.X * 0.56f, Size.Y * 0.56f);
        if (_portraitFrame != null)
            _portraitFrame.Position = new Vector2(Size.X - _portraitFrame.Size.X - 28, 28);
        if (_scroll != null && _col != null)
        {
            // Cap the scrollable area to the screen height; shrink to content when it fits.
            float cap = Size.Y - 100.0f;
            _scroll.CustomMinimumSize = new Vector2(_scroll.CustomMinimumSize.X, Mathf.Min(_col.GetCombinedMinimumSize().Y, cap));
        }
    }

    // --- scheme <-> working picks -------------------------------------------

    private void LoadActive()
    {
        GDict scheme = _activeSlot < 0
            ? new GDict { { "body", new GDict() }, { "power", new GDict() } }
            : SaveData.ColorSchemes()[_activeSlot].As<GDict>();
        ReadSchemeIntoWorking(scheme);
    }

    private void ReadSchemeIntoWorking(GDict scheme)
    {
        _bodyPicks.Clear();
        var body = scheme.ContainsKey("body") ? scheme["body"].As<GDict>() : new GDict();
        foreach (var mK in body.Keys)
            _bodyPicks[mK] = body[mK];
        _powerPicks.Clear();
        var savedPower = scheme.ContainsKey("power") ? scheme["power"].As<GDict>() : new GDict();
        foreach (var fam in POWER_ORDER)
            _powerPicks[fam] = savedPower.ContainsKey(fam) ? savedPower[fam] : POWER_FAMILIES[fam];
    }

    /// <summary>Push the current working picks to every live view.</summary>
    private void RefreshAll()
    {
        ApplyBodyDst();
        PaletteConfig.ApplyPortraitHues(_portraitMat, _bodyPicks);
        foreach (var (m, picker) in _bodyPickers)
            picker.Color = _bodyPicks.ContainsKey(m) ? _bodyPicks[m].As<Color>() : new Color(PaletteConfig.DEFAULT[m][1]);
        foreach (var (fam, picker) in _powerPickers)
            picker.Color = _powerPicks[fam].As<Color>();
        PushStatics();
        RebuildSample();
    }

    private void PushStatics()
    {
        PaletteConfig.SetPicks(_bodyPicks);
        VfxPalette.SetPicks(_powerPicks);
    }

    private void ApplyBodyDst() =>
        _mat.SetShaderParameter("dst", PaletteConfig.ToLinearVec3(PaletteConfig.BuildTargets(_bodyPicks)));

    // --- UI -----------------------------------------------------------------

    private void BuildControls()
    {
        var panel = new PanelContainer { Position = new Vector2(32, 32) };
        AddChild(panel);

        var pad = new MarginContainer();
        foreach (var s in new[] { "left", "right", "top", "bottom" })
            pad.AddThemeConstantOverride("margin_" + s, 18);
        panel.AddChild(pad);

        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        pad.AddChild(_scroll);

        var col = new VBoxContainer { CustomMinimumSize = new Vector2(300, 0) };
        col.AddThemeConstantOverride("separation", 7);
        _scroll.AddChild(col);
        _col = col;

        var title = new Label { Text = "KHALID", ThemeTypeVariation = UiStyle.Title };
        title.AddThemeFontSizeOverride("font_size", UiStyle.SizeTitle * 2); // the screen's name — larger than a menu title
        col.AddChild(title);
        col.AddChild(new Label { Text = "COLOUR SCHEMES", ThemeTypeVariation = UiStyle.Muted });

        // Scheme selector: radio toggles. "Default" (always available) + the 5 saved slots.
        col.AddChild(Header("SCHEME"));
        var slotRow = new HBoxContainer();
        slotRow.AddThemeConstantOverride("separation", 5);
        var group = new ButtonGroup();
        var def = new Button { ToggleMode = true, ButtonGroup = group, Text = "Default", CustomMinimumSize = new Vector2(66, 34) };
        def.ButtonPressed = _activeSlot == -1;
        def.Pressed += () => OnSlot(-1);
        slotRow.AddChild(def);
        for (int i = 0; i < SaveData.MAX_SCHEMES; i++)
        {
            var b = new Button { ToggleMode = true, ButtonGroup = group, CustomMinimumSize = new Vector2(38, 34) };
            b.ButtonPressed = i == _activeSlot;
            int idx = i;
            b.Pressed += () => OnSlot(idx);
            _slotButtons.Add(b);
            slotRow.AddChild(b);
        }
        col.AddChild(slotRow);
        RefreshSlotLabels();

        col.AddChild(Header("BODY"));
        foreach (var m in PaletteConfig.MATERIALS)
        {
            string mat = m;
            col.AddChild(SwatchRow(BODY_LABELS[m], BodyPickFor(m), c => OnBodyColour(c, mat), _bodyPickers, m));
        }

        col.AddChild(Header("POWERS / VFX"));
        foreach (var fam in POWER_ORDER)
        {
            string f = fam;
            col.AddChild(SwatchRow(POWER_LABELS[fam], _powerPicks[fam].As<Color>(), c => OnPowerColour(c, f), _powerPickers, fam));
        }

        col.AddChild(Header("BACKDROP"));
        var bgPick = new ColorPickerButton { Color = _backdrop.Color };
        bgPick.ColorChanged += c => _backdrop.Color = c;
        col.AddChild(SwatchRow("Background", _backdrop.Color, null, null, "", bgPick));

        col.AddChild(Spacer(6));
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        var save = new Button { Text = "Save scheme", CustomMinimumSize = new Vector2(150, 42), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        save.Pressed += OnSave;
        save.Disabled = _activeSlot < 0;  // can't overwrite the built-in Default -- pick a slot to save
        _saveButton = save;
        buttons.AddChild(save);
        var start = new Button
        {
            Text = "Start run →", ThemeTypeVariation = UiStyle.PrimaryButton,
            CustomMinimumSize = new Vector2(150, 42), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        start.Pressed += OnStart;
        buttons.AddChild(start);
        col.AddChild(buttons);
    }

    private Color BodyPickFor(string matName) =>
        _bodyPicks.ContainsKey(matName) ? _bodyPicks[matName].As<Color>() : new Color(PaletteConfig.DEFAULT[matName][1]);

    /// <summary>One labelled row in a subtle strip. If `swatch` is given it's used; else a ColorPickerButton
    /// is made, seeded to `col`, wired to `cb`, and stored in `store[key]`.</summary>
    private PanelContainer SwatchRow(string labelText, Color col, Action<Color> cb,
        Dictionary<string, ColorPickerButton> store, string key, Control swatch = null)
    {
        var strip = new PanelContainer();
        strip.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.RowBg));
        var pad = new MarginContainer();
        pad.AddThemeConstantOverride("margin_left", 8);
        pad.AddThemeConstantOverride("margin_right", 6);
        pad.AddThemeConstantOverride("margin_top", 3);
        pad.AddThemeConstantOverride("margin_bottom", 3);
        strip.AddChild(pad);
        var row = new HBoxContainer();
        pad.AddChild(row);
        row.AddChild(new Label { Text = labelText, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
        Control pick = swatch;
        if (pick == null)
        {
            var cpb = new ColorPickerButton { Color = col };
            cpb.ColorChanged += c => cb(c);
            store[key] = cpb;
            pick = cpb;
        }
        pick.CustomMinimumSize = new Vector2(116, 30);
        row.AddChild(pick);
        return strip;
    }

    // --- styling helpers ----------------------------------------------------

    private PanelContainer FramedBox()
    {
        var p = new PanelContainer();
        var sb = UiStyle.Box(UiStyle.PanelBg, UiStyle.Frame, 2);
        sb.SetContentMarginAll(10);
        p.AddThemeStyleboxOverride("panel", sb);
        return p;
    }

    private VBoxContainer Header(string text)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        var top = new Control { CustomMinimumSize = new Vector2(0, 6) };
        box.AddChild(top);
        box.AddChild(new Label { Text = text, ThemeTypeVariation = UiStyle.Heading });
        var rule = new PanelContainer { CustomMinimumSize = new Vector2(0, 2) };
        rule.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.FrameDim));
        box.AddChild(rule);
        return box;
    }

    private static Control Spacer(int h) => new() { CustomMinimumSize = new Vector2(0, h) };

    private void RefreshSlotLabels()
    {
        for (int i = 0; i < _slotButtons.Count; i++)
            _slotButtons[i].Text = $"{i + 1}{(SaveData.SchemeUsed(i) ? "•" : "")}";
    }

    // --- handlers -----------------------------------------------------------

    private void OnBodyColour(Color colour, string matName)
    {
        _bodyPicks[matName] = colour;
        ApplyBodyDst();
        PaletteConfig.ApplyPortraitHues(_portraitMat, _bodyPicks);
        PaletteConfig.SetPicks(_bodyPicks);
    }

    private void OnPowerColour(Color colour, string fam)
    {
        _powerPicks[fam] = colour;
        VfxPalette.SetPicks(_powerPicks);
        RebuildSample();
    }

    /// <summary>Select a scheme: make it active (persist so it applies on startup) and load its colours. -1 = DEFAULT.</summary>
    private void OnSlot(int i)
    {
        _activeSlot = i;
        SaveData.SetActive(i);
        LoadActive();
        RefreshAll();
        if (_saveButton != null)
            _saveButton.Disabled = i < 0;
    }

    /// <summary>Write the current picks into the active slot (and keep it active). No-op on Default.</summary>
    private void OnSave()
    {
        if (_activeSlot < 0)
            return;
        SaveData.SaveScheme(_activeSlot, _bodyPicks, _powerPicks);
        RefreshSlotLabels();
    }

    /// <summary>Apply the current picks to the run (statics already mirror them) and enter the game. Does NOT save.</summary>
    private void OnStart()
    {
        PushStatics();
        GetTree().ChangeSceneToFile(RUN_SCENE);
    }

    /// <summary>Spawn a fresh copy of the sample effect and recolour it. Rebuilt on every change (recolor_tree is one-way).</summary>
    private void RebuildSample()
    {
        if (_sample != null && IsInstanceValid(_sample))
            _sample.QueueFree();
        var scn = GD.Load<PackedScene>(SAMPLE_FX);
        if (scn == null)
            return;
        _sample = scn.Instantiate() as Node2D;
        if (_sample == null)
            return;
        VfxPalette.RecolorTree(_sample);
        AddChild(_sample);
        _sample.Position = new Vector2(Size.X * 0.55f, Size.Y * 0.74f);
    }
}
