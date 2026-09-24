using Godot;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// Persistent player data in <c>user://save.cfg</c>: the run RECORD (highest round reached, ever), the character
/// COLOUR SCHEMES from the picker, and player SETTINGS from the pause menu. C# port of <c>scripts/save_data.gd</c>.
/// All static: one record, no instance needed.
/// </summary>
public static class SaveData
{
    private const string PATH = "user://save.cfg";

    private static int _record = -1;   // lazy-loaded best-ever round reached (-1 = not read from disk yet)

    /// <summary>The record: the highest round reached in a single run, ever. Read from disk once, then cached.</summary>
    public static int RoundsRecord()
    {
        if (_record < 0)
        {
            var cfg = new ConfigFile();
            _record = cfg.Load(PATH) == Error.Ok ? cfg.GetValue("run", "rounds_record", 0).As<int>() : 0;
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
        cfg.Load(PATH); // keep any other keys already saved
        cfg.SetValue("run", "rounds_record", _record);
        cfg.Save(PATH);
        return true;
    }

    // --- colour schemes (from the picker) -----------------------------------
    // Up to MAX_SCHEMES named slots + an "active" index. Each scheme: {"body": {material→Color}, "power":
    // {family→Color}, "ui": {UiStyle.PickFrame/PickAccent→Color}}; empty (or missing, in older saves) dicts mean
    // "defaults". ConfigFile serialises Color/Dictionary/Array natively.
    public const int MAX_SCHEMES = 5;
    private static GArr _schemes = new();
    private static int _active = 0;
    private static bool _colorsLoaded = false;

    private static void LoadColors()
    {
        if (_colorsLoaded)
            return;
        var cfg = new ConfigFile();
        if (cfg.Load(PATH) == Error.Ok)
        {
            _schemes = cfg.GetValue("colors", "schemes", new GArr()).As<GArr>();
            _active = cfg.GetValue("colors", "active", -1).As<int>();
        }
        // Normalise to exactly MAX_SCHEMES slots so the UI can index them freely.
        _schemes = _schemes.Slice(0, Mathf.Min(_schemes.Count, MAX_SCHEMES));
        while (_schemes.Count < MAX_SCHEMES)
            _schemes.Add(new GDict { { "body", new GDict() }, { "power", new GDict() }, { "ui", new GDict() } });
        // -1 == the built-in DEFAULT look (always available, never overwritten); 0..MAX-1 == a saved slot.
        _active = Mathf.Clamp(_active, -1, MAX_SCHEMES - 1);
        _colorsLoaded = true;
    }

    /// <summary>All MAX_SCHEMES slots (index 0..4); each {"body":{}, "power":{}, "ui":{}}. Empty dicts = an unused slot.</summary>
    public static GArr ColorSchemes()
    {
        LoadColors();
        return _schemes;
    }

    /// <summary>The slot index applied on startup (and currently selected in the picker). -1 == the DEFAULT look.</summary>
    public static int ActiveScheme()
    {
        LoadColors();
        return _active;
    }

    /// <summary>Whether slot `i` has any saved picks (so the UI can mark used vs empty slots).</summary>
    public static bool SchemeUsed(int i)
    {
        LoadColors();
        var s = _schemes[i].As<GDict>();
        bool bodyEmpty = !s.ContainsKey("body") || s["body"].As<GDict>().Count == 0;
        bool powerEmpty = !s.ContainsKey("power") || s["power"].As<GDict>().Count == 0;
        bool uiEmpty = !s.ContainsKey("ui") || s["ui"].As<GDict>().Count == 0;
        return !(bodyEmpty && powerEmpty && uiEmpty);
    }

    /// <summary>Write slot `i` from the chosen picks and (by default) make it the active/startup scheme.</summary>
    public static void SaveScheme(int i, GDict body, GDict power, GDict ui, bool makeActive = true)
    {
        LoadColors();
        _schemes[i] = new GDict { { "body", body.Duplicate() }, { "power", power.Duplicate() }, { "ui", ui.Duplicate() } };
        if (makeActive)
            _active = i;
        PersistColors();
    }

    /// <summary>Just change which scheme applies on startup (no scheme edit). -1 == the DEFAULT look.</summary>
    public static void SetActive(int i)
    {
        LoadColors();
        _active = Mathf.Clamp(i, -1, MAX_SCHEMES - 1);
        PersistColors();
    }

    private static void PersistColors()
    {
        var cfg = new ConfigFile();
        cfg.Load(PATH); // keep the run record + anything else already saved
        cfg.SetValue("colors", "schemes", _schemes);
        cfg.SetValue("colors", "active", _active);
        cfg.Save(PATH);
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
            string name = cfg.Load(PATH) == Error.Ok ? cfg.GetValue("settings", "gauge_placement", "").AsString() : "";
            _gaugePlacement = System.Enum.TryParse(name, out GaugePlacement g) ? g : GaugePlacement.Screen;
        }
        return _gaugePlacement.Value;
    }

    public static void SetGaugePlacement(GaugePlacement g)
    {
        _gaugePlacement = g;
        var cfg = new ConfigFile();
        cfg.Load(PATH); // keep the run record + colours + anything else already saved
        cfg.SetValue("settings", "gauge_placement", g.ToString());
        cfg.Save(PATH);
    }
}
