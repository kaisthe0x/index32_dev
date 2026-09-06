using Godot;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// Central SOUND-EFFECTS service (autoload <c>Sfx</c>) — the runtime that PLAYS sounds; the catalog of which
/// sounds exist lives in the pure-DATA configs (SfxCharacters/SfxEnemies/SfxWorld, bridged). Every sound is a
/// stable <c>key</c>; an unregistered key is a silent no-op. C# port of <c>scripts/audio/sfx.gd</c>.
///
/// <para>PUBLIC surface stays snake_case: GDScript calls <c>Sfx.play(...)</c> on the autoload singleton and the
/// still-bridged C# callers use <c>GetNode("/root/Sfx").Call("play", …)</c> — both address these by exact name.</para>
/// </summary>
public partial class Sfx : Node
{
    private static readonly StringName Bus = "SFX";
    private const int Pool = 12;
    private const float LimiterCeilingDb = -2.0f; // peak ceiling for the summed SFX bus (see InstallLimiter)
    // Pin a specific output device (this machine routes "Default" to a silent sink). "" = system default.
    private const string PreferredOutput = "alsa_output.usb-ACTIONS_Pebble_V3-00.analog-stereo";

    private readonly List<AudioStreamPlayer> _flat = new();
    private readonly List<AudioStreamPlayer2D> _pos = new();
    private int _fi, _pi;
    private readonly Dictionary<string, AudioStream> _cache = new();
    private readonly GDict _cues = new(); // key -> path, merged from the per-area configs
    private StringName _bus = "Master";

    public override void _Ready()
    {
        _cues.Merge(SfxCharacters.CUES);
        _cues.Merge(SfxEnemies.CUES);
        _cues.Merge(SfxWorld.CUES);
        if (PreferredOutput != "" && System.Array.IndexOf(AudioServer.GetOutputDeviceList(), PreferredOutput) != -1)
            AudioServer.OutputDevice = PreferredOutput;
        _bus = AudioServer.GetBusIndex(Bus) != -1 ? Bus : "Master";
        for (int i = 0; i < Pool; i++)
        {
            var f = new AudioStreamPlayer { Bus = _bus };
            AddChild(f);
            _flat.Add(f);
            var p = new AudioStreamPlayer2D { Bus = _bus };
            AddChild(p);
            _pos.Add(p);
        }
        InstallLimiter();
    }

    /// <summary>Brick-wall PEAK CEILING on the SFX bus. Sounds SUM (many overlapping cues in a fight stack their
    /// waveforms — two identical copies ≈ +6 dB), so busy moments spike far past a single cue's authored level. The
    /// limiter caps the summed output at <see cref="LimiterCeilingDb"/>: inaudible under light load, only clamping the
    /// heat-of-battle peaks. Added in CODE (not the .tres bus layout) so it can't be clobbered by an open editor.
    ///
    /// TODO(sfx-loudness): the limiter is a safety net, not a full fix. Ways to improve later —
    ///   1. A gentle COMPRESSOR before it (AudioEffectCompressor, ~-18 dB threshold / ~4:1) so loud moments duck
    ///      smoothly instead of hard-clamping — more polished dynamics.
    ///   2. Per-cue CONCURRENCY CAP / dedupe: skip or duck a cue already playing N copies (or fired within a few ms),
    ///      which tackles the ROOT (the same sound stacking) rather than the summed symptom.
    ///   3. Author per-cue base levels (a `volume_db` in the Sfx* configs) so loud cues (Bakshen/Nasen) sit lower at
    ///      the source, reducing how hard the limiter has to work.
    ///   4. Expose the ceiling / limiter on-off to the options menu for players who want it off.
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
        if (_cues.ContainsKey(key))
        {
            string path = _cues[key].AsString();
            if (ResourceLoader.Exists(path))
                s = GD.Load<AudioStream>(path);
            else
                GD.PushWarning($"Sfx: cue '{key}' -> {path} not found (playing nothing)");
        }
        _cache[key] = s;
        return s;
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
        pl.VolumeDb = volume_db;
        pl.PitchScale = pitch;
        pl.Play();
    }

    /// <summary>Fire ONE random variant from `keys` (skips unregistered / missing). No-op if none resolve.</summary>
    public void play_random(GArr keys, float volume_db = 0.0f, float pitch = 1.0f)
    {
        var valid = new List<string>();
        foreach (Variant k in keys)
            if (Stream(k.AsString()) != null)
                valid.Add(k.AsString());
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

    /// <summary>A dedicated LOOPING player for `key` the CALLER owns + parents (footsteps, a hum). Null if missing.</summary>
    public AudioStreamPlayer make_loop(string key)
    {
        var s = LoopedStream(key);
        return s == null ? null : new AudioStreamPlayer { Bus = _bus, Stream = s };
    }

    /// <summary>A dedicated ONE-SHOT player the CALLER owns (stoppable early, e.g. a slam whoosh). Null if missing.</summary>
    public AudioStreamPlayer make_oneshot(string key)
    {
        var s = Stream(key);
        return s == null ? null : new AudioStreamPlayer { Bus = _bus, Stream = s };
    }

    /// <summary>Positional twin of make_oneshot(): a one-shot AudioStreamPlayer2D the caller parents on a world object.</summary>
    public AudioStreamPlayer2D make_oneshot_2d(string key)
    {
        var s = Stream(key);
        return s == null ? null : new AudioStreamPlayer2D { Bus = _bus, Stream = s };
    }

    /// <summary>Positional twin of make_loop(): a looping AudioStreamPlayer2D the caller parents at a world spot (an orb hum).</summary>
    public AudioStreamPlayer2D make_loop_2d(string key)
    {
        var s = LoopedStream(key);
        return s == null ? null : new AudioStreamPlayer2D { Bus = _bus, Stream = s };
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
        pl.VolumeDb = volume_db;
        pl.PitchScale = pitch;
        pl.Play();
    }
}
