using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Central SOUND-EFFECTS service (autoload <c>Sfx</c>) — the runtime that PLAYS sounds; the catalog of which
/// sounds exist lives in the pure-DATA configs (SfxCharacters/SfxEnemies/SfxWorld). Every sound is a
/// stable <c>key</c>; an unregistered key is a silent no-op. C# port of <c>scripts/audio/sfx.gd</c>.
///
/// <para><b>Every cue plays at the same loudness, automatically.</b> On boot each cue's file is measured
/// (<see cref="SfxLoudness"/>: its peak momentary loudness) and given the gain that brings it to
/// <see cref="TargetLoudness"/> — so a new WAV needs no mastering or trim: drop it in, register its key. The per-cue
/// VOLUMES tables are then only for DELIBERATE mix choices on top (a quiet ambient hum), never loudness fixes.</para>
///
/// <para>The PUBLIC surface is still snake_case, a leftover of the GDScript port (docs/standards.md, Known debt).</para>
/// </summary>
public partial class Sfx : Node
{
    private static readonly StringName Bus = "SFX";
    private const int Pool = 12;
    private const float LimiterCeilingDb = -2.0f; // peak ceiling for the summed SFX bus (see InstallLimiter)
    // The one loudness every cue is normalized to (peak momentary LUFS — see SfxLoudness). Raise/lower the whole SFX
    // mix against the music here; a single cue that should sit differently goes in a VOLUMES table instead.
    public const float TargetLoudness = -26.0f;   // the SFX library's median — so the overall mix stays put
    private const float MaxNormalizeDb = 24.0f;   // clamp on the auto gain, so a near-silent file isn't blown up
    // Output device. "" (default) = follow the SYSTEM DEFAULT, so audio goes wherever the OS routes it — it
    // switches to headphones when you plug them in / make them the default sink. Only set a specific device name
    // here (from AudioServer.GetOutputDeviceList()) to FORCE one on a machine whose default is misrouted; leaving
    // a device pinned overrides the OS and ignores headphones.
    private const string PreferredOutput = "";

    private readonly List<AudioStreamPlayer> _flat = new();
    private readonly List<AudioStreamPlayer2D> _pos = new();
    private int _fi, _pi;
    private readonly Dictionary<string, AudioStream> _cache = new();
    private readonly Dictionary<string, string> _cues = new();  // key -> path, merged from the per-area configs
    private readonly Dictionary<string, float> _vol = new();    // key -> deliberate per-cue mix offset (dB), merged; unlisted = 0
    private readonly Dictionary<string, Vector2> _pitch = new(); // key or dotted-prefix group -> random pitch range (min,max), merged; unlisted = fixed
    private readonly Dictionary<string, float> _normalize = new(); // file path -> gain (dB) bringing it to TargetLoudness
    private StringName _bus = "Master";

    public override void _Ready()
    {
        Merge(_cues, SfxCharacters.CUES, SfxEnemies.CUES, SfxWorld.CUES);
        Merge(_vol, SfxCharacters.VOLUMES, SfxEnemies.VOLUMES, SfxWorld.VOLUMES);
        Merge(_pitch, SfxCharacters.PITCH, SfxEnemies.PITCH, SfxWorld.PITCH);
        if (PreferredOutput != "" && System.Array.IndexOf(AudioServer.GetOutputDeviceList(), PreferredOutput) != -1)
            AudioServer.OutputDevice = PreferredOutput;
        _bus = AudioServer.GetBusIndex(Bus) != -1 ? Bus : "Master";
        foreach (string key in _cues.Keys)
            Stream(key); // load + measure every cue now, so no first-play hitch mid-fight
        for (int i = 0; i < Pool; i++)
        {
            // ProcessMode.Always so one-shots (UI/level-up cues) still play while the game is paused (menus).
            var f = new AudioStreamPlayer { Bus = _bus, ProcessMode = Node.ProcessModeEnum.Always };
            AddChild(f);
            _flat.Add(f);
            var p = new AudioStreamPlayer2D { Bus = _bus, ProcessMode = Node.ProcessModeEnum.Always };
            AddChild(p);
            _pos.Add(p);
        }
        InstallLimiter();
    }

    /// <summary>Brick-wall PEAK CEILING on the SFX bus. Sounds SUM (many overlapping cues in a fight stack their
    /// waveforms — two identical copies ≈ +6 dB), so busy moments spike far past a single cue's authored level. The
    /// limiter caps the summed output at <see cref="LimiterCeilingDb"/>: inaudible under light load, only clamping the
    /// heat-of-battle peaks. Added in CODE (not the .tres bus layout) so it can't be clobbered by an open editor.
    /// It is a safety net, not a mix: see docs/future-enhancements-and-fixes.md for what would do it properly.</summary>
    private void InstallLimiter()
    {
        if (AudioServer.GetBusIndex(_bus) == -1)
            return;
        AudioBus.AddEffect(_bus, new AudioEffectHardLimiter { CeilingDb = LimiterCeilingDb });
    }

    public void set_volume(float v) => AudioBus.SetVolumeLinear(Bus, v);
    public float get_volume() => AudioBus.GetVolumeLinear(Bus);
    public void set_muted(bool on) => AudioBus.SetMuted(Bus, on);

    /// <summary>The stream for a cue key (cached), or null. Unregistered = silent no-op; registered-but-missing warns.</summary>
    private AudioStream Stream(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
            return cached;
        AudioStream s = null;
        if (_cues.TryGetValue(key, out string path))
        {
            if (ResourceLoader.Exists(path))
            {
                s = GD.Load<AudioStream>(path);
                if (s != null && !_normalize.ContainsKey(path))
                    _normalize[path] = NormalizeGain(s, path);
            }
            else
                GD.PushWarning($"Sfx: cue '{key}' -> {path} not found (playing nothing)");
        }
        _cache[key] = s;
        return s;
    }

    /// <summary>Copy every entry of each of <paramref name="tables"/> into <paramref name="into"/> (a later table wins
    /// a shared key).</summary>
    private static void Merge<T>(Dictionary<string, T> into, params Dictionary<string, T>[] tables)
    {
        foreach (var table in tables)
            foreach (var (key, value) in table)
                into[key] = value;
    }

    /// <summary>The gain (dB) that brings <paramref name="s"/> to <see cref="TargetLoudness"/>; 0 (with a warning) if it
    /// can't be measured — a compressed import (re-import the WAV uncompressed) or pure silence.</summary>
    private static float NormalizeGain(AudioStream s, string path)
    {
        float? lufs = s is AudioStreamWav wav ? SfxLoudness.Measure(wav) : null;
        if (lufs is not float measured)
        {
            GD.PushWarning($"Sfx: can't measure {path} (compressed import or silent) — it plays un-normalized. " +
                "Import WAVs with compress/mode = Disabled.");
            return 0.0f;
        }
        return Mathf.Clamp(TargetLoudness - measured, -MaxNormalizeDb, MaxNormalizeDb);
    }

    /// <summary>Fire a one-shot (non-positional). No-op if the key is unregistered or its file is missing.</summary>
    public void play(string key, float volume_db = 0.0f, float pitch = 1.0f)
    {
        var s = Stream(key);
        if (s == null || _flat.Count == 0)
            return;
        var pl = _flat[_fi];
        _fi = (_fi + 1) % _flat.Count;
        pl.Stream = s;
        pl.VolumeDb = volume_db + GainFor(key);
        pl.PitchScale = pitch * PitchJitter(key);
        pl.Play();
    }

    /// <summary>A random pitch multiplier for <paramref name="key"/> from the PITCH tables — 1 + a random offset in its
    /// entry's (min, max) range, else its nearest dotted prefix's ("kebus.projectile.3" → "kebus.projectile" →
    /// "kebus"); 1 if none.</summary>
    private float PitchJitter(string key)
    {
        for (string k = key; ; k = k[..k.LastIndexOf('.')])
        {
            if (_pitch.TryGetValue(k, out Vector2 range))
                return 1.0f + (float)GD.RandRange(range.X, range.Y);
            if (!k.Contains('.'))
                return 1.0f;
        }
    }

    /// <summary>The volume (dB) <paramref name="key"/> plays at: its file's loudness normalization + any deliberate
    /// VOLUMES offset. Every player the service hands out starts from this.</summary>
    private float GainFor(string key)
    {
        float gain = _vol.GetValueOrDefault(key);
        if (_cues.TryGetValue(key, out string path) && _normalize.TryGetValue(path, out float n))
            gain += n;
        return gain;
    }

    /// <summary>Fire ONE random variant from `keys` (skips unregistered / missing). No-op if none resolve.</summary>
    public void play_random(IReadOnlyList<string> keys, float volume_db = 0.0f, float pitch = 1.0f)
    {
        var valid = new List<string>();
        foreach (string k in keys)
            if (Stream(k) != null)
                valid.Add(k);
        if (valid.Count == 0)
            return;
        play(valid[(int)(GD.Randi() % (uint)valid.Count)], volume_db, pitch);
    }

    /// <summary>The stream for `key` forced to LOOP (a duplicate, so the shared one-shot stream is never flipped).</summary>
    private AudioStream LoopedStream(string key)
    {
        var s = Stream(key);
        if (s == null)
            return null;
        // For a WAV, loop_end defaults to 0 (a zero-length loop that "finishes" every frame) — span the whole sample.
        if (s is AudioStreamWav wav && wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled)
        {
            var w = (AudioStreamWav)wav.Duplicate();
            w.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            w.LoopBegin = 0;
            w.LoopEnd = (int)Mathf.Round(w.GetLength() * w.MixRate);
            return w;
        }
        if (s is AudioStreamMP3 mp3 && !mp3.Loop)
        {
            var m = (AudioStreamMP3)mp3.Duplicate();
            m.Loop = true;
            return m;
        }
        if (s is AudioStreamOggVorbis ogg && !ogg.Loop)
        {
            var o = (AudioStreamOggVorbis)ogg.Duplicate();
            o.Loop = true;
            return o;
        }
        return s;
    }

    // The make_* players below the CALLER owns start at the cue's GainFor volume — a caller that wants it quieter/
    // louder ADDS to VolumeDb (never overwrites it, which would drop the normalization).

    /// <summary>A dedicated LOOPING player for `key` the CALLER owns + parents (footsteps, a hum). Null if missing.</summary>
    public AudioStreamPlayer make_loop(string key)
    {
        var s = LoopedStream(key);
        return s == null ? null : new AudioStreamPlayer { Bus = _bus, Stream = s, VolumeDb = GainFor(key) };
    }

    /// <summary>A dedicated ONE-SHOT player the CALLER owns (stoppable early, e.g. a slam whoosh). Null if missing.</summary>
    public AudioStreamPlayer make_oneshot(string key)
    {
        var s = Stream(key);
        return s == null ? null : new AudioStreamPlayer { Bus = _bus, Stream = s, VolumeDb = GainFor(key) };
    }

    /// <summary>Positional twin of make_oneshot(): a one-shot AudioStreamPlayer2D the caller parents on a world object.</summary>
    public AudioStreamPlayer2D make_oneshot_2d(string key)
    {
        var s = Stream(key);
        return s == null ? null : new AudioStreamPlayer2D { Bus = _bus, Stream = s, VolumeDb = GainFor(key) };
    }

    /// <summary>Positional twin of make_loop(): a looping AudioStreamPlayer2D the caller parents at a world spot (an orb hum).</summary>
    public AudioStreamPlayer2D make_loop_2d(string key)
    {
        var s = LoopedStream(key);
        return s == null ? null : new AudioStreamPlayer2D { Bus = _bus, Stream = s, VolumeDb = GainFor(key) };
    }

    /// <summary>Fire a one-shot at a world position (2D panning). No-op if missing.</summary>
    public void play_at(string key, Vector2 world_pos, float volume_db = 0.0f, float pitch = 1.0f)
    {
        var s = Stream(key);
        if (s == null || _pos.Count == 0)
            return;
        var pl = _pos[_pi];
        _pi = (_pi + 1) % _pos.Count;
        pl.Stream = s;
        pl.GlobalPosition = world_pos;
        pl.VolumeDb = volume_db + GainFor(key);
        pl.PitchScale = pitch * PitchJitter(key);
        pl.Play();
    }
}
