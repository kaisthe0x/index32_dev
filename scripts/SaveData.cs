using Godot;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// Persistent player data in <c>user://save.cfg</c>: the run RECORD (highest round reached, ever), the character
/// COLOUR SCHEMES from the picker, and player SETTINGS from the pause menu.
/// All static: one record, no instance needed.
/// </summary>
public static class SaveData
{
    private const string SavePath = "user://save.cfg";

    private static int _record = -1;   // lazy-loaded best-ever round reached (-1 = not read from disk yet)

    /// <summary>The record: the highest round reached in a single run, ever. Read from disk once, then cached.</summary>
    public static int RoundsRecord()
    {
        if (_record < 0)
        {
            var cfg = new ConfigFile();
            _record = cfg.Load(SavePath) == Error.Ok ? cfg.GetValue("run", "rounds_record", 0).As<int>() : 0;
        }
        return _record;
    }

    /// <summary>Report the round a finished run reached; persist a new best if it beats the record. True on a new best.</summary>
    public static bool ReportRun(int round)
    {
        if (round <= RoundsRecord())
            return false;
        _record = round;
        var cfg = new ConfigFile();
        cfg.Load(SavePath); // keep any other keys already saved
        cfg.SetValue("run", "rounds_record", _record);
        cfg.Save(SavePath);
        return true;
    }

    // --- colour schemes (from the picker) -----------------------------------
    // Up to MaxSchemes slots + an "active" index. On disk each scheme is {"body": {material→Color}, "power":
    // {family→Color}, "ui": {UiStyle.PickFrame/PickAccent→Color}} -- ConfigFile serialises Color/Dictionary/Array
    // natively. Engine dictionaries exist ONLY in ReadScheme/WriteScheme; the rest of the game sees ColorScheme.
    public const int MaxSchemes = 5;
    private static readonly ColorScheme[] _schemes = new ColorScheme[MaxSchemes];
    private static int _active = -1;
    private static bool _colorsLoaded = false;

    private static void LoadColors()
    {
        if (_colorsLoaded)
            return;
        var cfg = new ConfigFile();
        var saved = new GArr();
        if (cfg.Load(SavePath) == Error.Ok)
        {
            saved = cfg.GetValue("colors", "schemes", new GArr()).As<GArr>();
            _active = cfg.GetValue("colors", "active", -1).As<int>();
        }
        // Always exactly MaxSchemes slots, so the UI can index them freely.
        for (int i = 0; i < MaxSchemes; i++)
            _schemes[i] = i < saved.Count ? ReadScheme(saved[i].As<GDict>()) : ColorScheme.Empty;
        // -1 == the built-in DEFAULT look (always available, never overwritten); 0..MAX-1 == a saved slot.
        _active = Mathf.Clamp(_active, -1, MaxSchemes - 1);
        _colorsLoaded = true;
    }

    /// <summary>The scheme saved in slot `i` (0..MaxSchemes-1); an unused slot is <see cref="ColorScheme.Empty"/>.</summary>
    public static ColorScheme Scheme(int i)
    {
        LoadColors();
        return _schemes[i];
    }

    /// <summary>The slot index applied on startup (and currently selected in the picker). -1 == the DEFAULT look.</summary>
    public static int ActiveScheme()
    {
        LoadColors();
        return _active;
    }

    /// <summary>Write slot `i` (the scheme is copied) and (by default) make it the active/startup scheme.</summary>
    public static void SaveScheme(int i, ColorScheme scheme, bool makeActive = true)
    {
        LoadColors();
        _schemes[i] = new ColorScheme(
            new Dictionary<string, Color>(scheme.Body), new Dictionary<string, Color>(scheme.Power), new Dictionary<string, Color>(scheme.Ui));
        if (makeActive)
            _active = i;
        PersistColors();
    }

    /// <summary>Just change which scheme applies on startup (no scheme edit). -1 == the DEFAULT look.</summary>
    public static void SetActive(int i)
    {
        LoadColors();
        _active = Mathf.Clamp(i, -1, MaxSchemes - 1);
        PersistColors();
    }

    private static void PersistColors()
    {
        var saved = new GArr();
        foreach (var scheme in _schemes)
            saved.Add(WriteScheme(scheme));
        var cfg = new ConfigFile();
        cfg.Load(SavePath); // keep the run record + anything else already saved
        cfg.SetValue("colors", "schemes", saved);
        cfg.SetValue("colors", "active", _active);
        cfg.Save(SavePath);
    }

    private static ColorScheme ReadScheme(GDict saved) =>
        new(ReadPicks(saved, "body"), ReadPicks(saved, "power"), ReadPicks(saved, "ui"));

    /// <summary>One pick set of a saved scheme; a missing set (older saves have no "ui") reads as no picks.</summary>
    private static Dictionary<string, Color> ReadPicks(GDict saved, string set)
    {
        var picks = new Dictionary<string, Color>();
        if (saved.TryGetValue(set, out Variant value))
            foreach (var (key, colour) in value.As<GDict>())
                picks[key.AsString()] = colour.AsColor();
        return picks;
    }

    private static GDict WriteScheme(ColorScheme scheme) =>
        new() { { "body", WritePicks(scheme.Body) }, { "power", WritePicks(scheme.Power) }, { "ui", WritePicks(scheme.Ui) } };

    private static GDict WritePicks(IReadOnlyDictionary<string, Color> picks)
    {
        var saved = new GDict();
        foreach (var (key, colour) in picks)
            saved[key] = colour;
        return saved;
    }

    // --- player settings (pause menu) ---------------------------------------
    // Enums are stored by NAME, so reordering an enum never remaps a saved choice; an unknown name falls back to the default.

    private static GaugePlacement? _gaugePlacement; // lazy-loaded from disk

    /// <summary>Where the HUD's health + Ruh gauge sits. Read from disk once, then cached; defaults to Screen.</summary>
    public static GaugePlacement GetGaugePlacement()
    {
        if (_gaugePlacement == null)
        {
            var cfg = new ConfigFile();
            string name = cfg.Load(SavePath) == Error.Ok ? cfg.GetValue("settings", "gauge_placement", "").AsString() : "";
            _gaugePlacement = System.Enum.TryParse(name, out GaugePlacement g) ? g : GaugePlacement.Screen;
        }
        return _gaugePlacement.Value;
    }

    public static void SetGaugePlacement(GaugePlacement g)
    {
        _gaugePlacement = g;
        var cfg = new ConfigFile();
        cfg.Load(SavePath); // keep the run record + colours + anything else already saved
        cfg.SetValue("settings", "gauge_placement", g.ToString());
        cfg.Save(SavePath);
    }
}
