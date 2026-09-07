using Godot;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// Persistent run record + the current run's progress, shared between RunManager (writes) and the HUD (reads), plus
/// saved character COLOUR SCHEMES from the picker. C# port of <c>scripts/save_data.gd</c>. The RECORD (most WAVES
/// survived in one run, ever — levels are retired) persists to user://; the current run's wave count is session-only,
/// in memory. All static: one record, no instance needed.
/// </summary>
public static class SaveData
{
    private const string PATH = "user://save.cfg";

    private static int _record = -1;   // lazy-loaded best-ever waves survived (-1 = not read from disk yet)
    /// <summary>Waves survived in the CURRENT run. RunManager sets it; the HUD shows it next to the record.</summary>
    public static int CurrentWaves = 0;

    public static void SetCurrentWaves(int n) => CurrentWaves = n;
    public static int GetCurrentWaves() => CurrentWaves;

    /// <summary>The record: most waves survived in a single run, ever. Read from disk once, then cached.</summary>
    public static int WavesRecord()
    {
        if (_record < 0)
        {
            var cfg = new ConfigFile();
            _record = cfg.Load(PATH) == Error.Ok ? cfg.GetValue("run", "waves_record", 0).As<int>() : 0;
        }
        return _record;
    }

    /// <summary>Report a finished run's wave count; persist a new best if it beats the record. True on a new best.</summary>
    public static bool ReportRun(int waves)
    {
        if (waves <= WavesRecord())
            return false;
        _record = waves;
        var cfg = new ConfigFile();
        cfg.Load(PATH); // keep any other keys already saved
        cfg.SetValue("run", "waves_record", _record);
        cfg.Save(PATH);
        return true;
    }

    // --- colour schemes (from the picker) -----------------------------------
    // Up to MAX_SCHEMES named slots + an "active" index. Each scheme: {"body": {material→Color}, "power":
    // {family→Color}}; empty dicts mean "defaults". ConfigFile serialises Color/Dictionary/Array natively.
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
            _schemes.Add(new GDict { { "body", new GDict() }, { "power", new GDict() } });
        // -1 == the built-in DEFAULT look (always available, never overwritten); 0..MAX-1 == a saved slot.
        _active = Mathf.Clamp(_active, -1, MAX_SCHEMES - 1);
        _colorsLoaded = true;
    }

    /// <summary>All MAX_SCHEMES slots (index 0..4); each {"body":{}, "power":{}}. Empty dicts = an unused slot.</summary>
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
        return !(bodyEmpty && powerEmpty);
    }

    /// <summary>Write slot `i` from the chosen picks and (by default) make it the active/startup scheme.</summary>
    public static void SaveScheme(int i, GDict body, GDict power, bool makeActive = true)
    {
        LoadColors();
        _schemes[i] = new GDict { { "body", body.Duplicate() }, { "power", power.Duplicate() } };
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
}
