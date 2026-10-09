using Godot;
using System;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Character colour-customisation preview + SCHEME manager (the pre-game main scene). C# port of
/// <c>scripts/ui/palette_preview.gd</c>. Runs Khalid's `idle` cycle on an adjustable backdrop with a live-recoloured
/// portrait, a colour picker per body part + per power family + the UI's two colours (frame / highlight, which recolour
/// every menu and HUD label live), and up to SaveData.MAX_SCHEMES saved schemes you switch,
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

    // UI colours: the two picks UiStyle derives the whole menu palette from.
    private static readonly string[] UI_ORDER = { UiStyle.PickFrame, UiStyle.PickAccent };
    private static readonly Dictionary<string, string> UI_LABELS = new()
        { [UiStyle.PickFrame] = "Frame", [UiStyle.PickAccent] = "Highlight" };
    private static readonly Dictionary<string, Color> UI_DEFAULTS = new()
        { [UiStyle.PickFrame] = UiStyle.DefaultFrame, [UiStyle.PickAccent] = UiStyle.DefaultAccent };

    private ShaderMaterial _mat = null!, _portraitMat = null!;
    private ColorRect _backdrop = null!;
    private AnimatedSprite2D _sprite = null!;
    private TextureRect _portrait = null!;
    private PanelContainer _portraitFrame = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _col = null!;
    private Node2D? _sample;
    private readonly Dictionary<string, Color> _bodyPicks = new();   // material -> picked Color (missing = default shade ramp)
    private readonly Dictionary<string, Color> _powerPicks = new();  // family -> picked Color (missing = family default)
    private readonly Dictionary<string, ColorPickerButton> _bodyPickers = new();
    private readonly Dictionary<string, ColorPickerButton> _powerPickers = new();
    private readonly Dictionary<string, Color> _uiPicks = new();     // UiStyle.PickFrame/PickAccent -> picked Color (always both set)
    private readonly Dictionary<string, ColorPickerButton> _uiPickers = new();
    private readonly List<Button> _slotButtons = new();
    private Button _saveButton = null!;
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
        var portraitPad = FramedBox(out _portraitFrame);
        var pv = new VBoxContainer();
        pv.AddThemeConstantOverride("separation", 6);
        pv.AddChild(_portrait);
        pv.AddChild(new Label { Text = "PORTRAIT", ThemeTypeVariation = UiStyle.Muted, HorizontalAlignment = HorizontalAlignment.Center });
        portraitPad.AddChild(pv);
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

    /// <summary>Load the active slot's scheme into the working picks; power and UI picks fall back to their defaults.</summary>
    private void LoadActive()
    {
        ColorScheme scheme = _activeSlot < 0 ? ColorScheme.Empty : SaveData.Scheme(_activeSlot);
        _bodyPicks.Clear();
        foreach (var (material, colour) in scheme.Body)
            _bodyPicks[material] = colour;
        _powerPicks.Clear();
        foreach (var fam in POWER_ORDER)
            _powerPicks[fam] = scheme.Power.GetValueOrDefault(fam, POWER_FAMILIES[fam]);
        _uiPicks.Clear();
        foreach (var k in UI_ORDER)
            _uiPicks[k] = scheme.Ui.GetValueOrDefault(k, UI_DEFAULTS[k]);
    }

    /// <summary>Push the current working picks to every live view.</summary>
    private void RefreshAll()
    {
        ApplyBodyDst();
        PaletteConfig.ApplyPortraitHues(_portraitMat, _bodyPicks);
        foreach (var (m, picker) in _bodyPickers)
            picker.Color = BodyPickFor(m);
        foreach (var (fam, picker) in _powerPickers)
            picker.Color = _powerPicks[fam];
        foreach (var (k, picker) in _uiPickers)
            picker.Color = _uiPicks[k];
        PushStatics();
        RebuildSample();
    }

    private void PushStatics()
    {
        PaletteConfig.SetPicks(_bodyPicks);
        VfxPalette.SetPicks(_powerPicks);
        ApplyUiPicks();
    }

    /// <summary>Recolour the whole UI (this screen live, plus every menu/HUD the run builds) from the UI picks.</summary>
    private void ApplyUiPicks() =>
        UiStyle.SetColors(_uiPicks[UiStyle.PickFrame], _uiPicks[UiStyle.PickAccent]);

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
            col.AddChild(PickerRow(BODY_LABELS[m], BodyPickFor(m), c => OnBodyColour(c, mat), _bodyPickers, m));
        }

        col.AddChild(Header("POWERS / VFX"));
        foreach (var fam in POWER_ORDER)
        {
            string f = fam;
            col.AddChild(PickerRow(POWER_LABELS[fam], _powerPicks[fam], c => OnPowerColour(c, f), _powerPickers, fam));
        }

        col.AddChild(Header("UI"));
        foreach (var k in UI_ORDER)
        {
            string key = k;
            col.AddChild(PickerRow(UI_LABELS[k], _uiPicks[k], c => OnUiColour(c, key), _uiPickers, k));
        }

        col.AddChild(Header("BACKDROP"));
        var bgPick = new ColorPickerButton { Color = _backdrop.Color };
        bgPick.ColorChanged += c => _backdrop.Color = c;
        col.AddChild(SwatchRow("Background", bgPick));

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
        _bodyPicks.GetValueOrDefault(matName, new Color(PaletteConfig.DEFAULT[matName][1]));

    /// <summary>A labelled row holding a new ColorPickerButton seeded to `col`, wired to `onPick`, and kept in
    /// `store[key]` so a scheme switch can update it.</summary>
    private PanelContainer PickerRow(string labelText, Color col, Action<Color> onPick,
        Dictionary<string, ColorPickerButton> store, string key)
    {
        var picker = new ColorPickerButton { Color = col };
        picker.ColorChanged += c => onPick(c);
        store[key] = picker;
        return SwatchRow(labelText, picker);
    }

    /// <summary>One labelled row in a subtle strip, with `swatch` on its right.</summary>
    private PanelContainer SwatchRow(string labelText, Control swatch)
    {
        var strip = new PanelContainer();
        strip.ThemeTypeVariation = UiStyle.RowPanel;
        var pad = new MarginContainer();
        pad.AddThemeConstantOverride("margin_left", 8);
        pad.AddThemeConstantOverride("margin_right", 6);
        pad.AddThemeConstantOverride("margin_top", 3);
        pad.AddThemeConstantOverride("margin_bottom", 3);
        strip.AddChild(pad);
        var row = new HBoxContainer();
        pad.AddChild(row);
        row.AddChild(new Label { Text = labelText, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
        swatch.CustomMinimumSize = new Vector2(116, 30);
        row.AddChild(swatch);
        return strip;
    }

    // --- styling helpers ----------------------------------------------------

    /// <summary>A themed panel (so it follows UI recolours live) with a 10px inner pad; returns the pad to fill.</summary>
    private static MarginContainer FramedBox(out PanelContainer panel)
    {
        panel = new PanelContainer();
        var pad = new MarginContainer();
        foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            pad.AddThemeConstantOverride(m, 10);
        panel.AddChild(pad);
        return pad;
    }

    private VBoxContainer Header(string text)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        var top = new Control { CustomMinimumSize = new Vector2(0, 6) };
        box.AddChild(top);
        box.AddChild(new Label { Text = text, ThemeTypeVariation = UiStyle.Heading });
        box.AddChild(new HSeparator());
        return box;
    }

    private static Control Spacer(int h) => new() { CustomMinimumSize = new Vector2(0, h) };

    private void RefreshSlotLabels()
    {
        for (int i = 0; i < _slotButtons.Count; i++)
            _slotButtons[i].Text = $"{i + 1}{(!SaveData.Scheme(i).IsEmpty ? "•" : "")}";
    }

    // --- handlers -----------------------------------------------------------

    private void OnBodyColour(Color colour, string matName)
    {
        _bodyPicks[matName] = colour;
        ApplyBodyDst();
        PaletteConfig.ApplyPortraitHues(_portraitMat, _bodyPicks);
        PaletteConfig.SetPicks(_bodyPicks);
    }

    private void OnUiColour(Color colour, string key)
    {
        _uiPicks[key] = colour;
        ApplyUiPicks();
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
        SaveData.SaveScheme(_activeSlot, new ColorScheme(_bodyPicks, _powerPicks, _uiPicks));
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
