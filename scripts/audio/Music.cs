using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Background MUSIC service (autoload <c>Music</c>) — a CROSSFADING bed. Two AudioStreamPlayers ping-pong:
/// <c>play(key)</c> fades the current track out while the new one fades in on the other, always restarted from
/// the top. C# port of <c>scripts/audio/music.gd</c>. Snake_case public surface (GDScript + the bridged C#
/// callers address <c>Music.play/stop</c> by exact name).
/// </summary>
public partial class Music : Node
{
    // key -> res:// path. ONE entry per track.
    private static readonly Dictionary<string, string> Tracks = new()
    {
        { "level", "res://music/bg_music.ogg" },              // main gameplay loop
        { "base_rest", "res://music/base_rest.mp3" },         // calmer bed while a cleared exit/reward is open
    };

    private static readonly StringName Bus = "Music";
    private const float DefaultVolumeDb = -3.0f; // the "full" music level once faded in
    private const float SilenceDb = -60.0f;
    private const float Fade = 1.5f;

    private readonly List<AudioStreamPlayer> _players = new();
    private readonly Tween[] _tweens = new Tween[2];
    private int _active = 0;
    private string _current = "";
    private readonly Dictionary<string, AudioStream> _cache = new();

    public override void _Ready()
    {
        StringName bus = AudioServer.GetBusIndex(Bus) != -1 ? Bus : "Master";
        for (int i = 0; i < 2; i++)
        {
            var p = new AudioStreamPlayer
            {
                Bus = bus,
                VolumeDb = SilenceDb,
                ProcessMode = ProcessModeEnum.Always, // keep music going if the game pauses
            };
            AddChild(p);
            _players.Add(p);
        }
    }

    /// <summary>The looping stream for a key (cached), or null if unregistered / missing.</summary>
    private AudioStream Stream(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
            return cached;
        AudioStream s = null;
        string path = Tracks.GetValueOrDefault(key, "");
        if (path != "" && ResourceLoader.Exists(path))
        {
            s = GD.Load<AudioStream>(path);
            // Force looping so a bed never just ends. Duplicate so we don't mutate the cached import.
            if (s is AudioStreamMP3 mp3 && !mp3.Loop)
            {
                s = (AudioStreamMP3)mp3.Duplicate();
                ((AudioStreamMP3)s).Loop = true;
            }
            else if (s is AudioStreamOggVorbis ogg && !ogg.Loop)
            {
                s = (AudioStreamOggVorbis)ogg.Duplicate();
                ((AudioStreamOggVorbis)s).Loop = true;
            }
            else if (s is AudioStreamWav wav && wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled)
            {
                var w = (AudioStreamWav)wav.Duplicate();
                w.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                w.LoopBegin = 0;
                w.LoopEnd = (int)Mathf.Round(w.GetLength() * w.MixRate);
                s = w;
            }
        }
        else if (path != "")
        {
            GD.PushWarning($"Music: '{key}' -> {path} not found (playing nothing)");
        }
        _cache[key] = s;
        return s;
    }

    /// <summary>Crossfade to `key`, started FROM THE TOP, over `fade` seconds. No-op if unregistered / missing.</summary>
    public void play(string key, float fade = Fade, float volume_db = DefaultVolumeDb)
    {
        var s = Stream(key);
        if (s == null)
            return;
        _current = key;
        int outI = _active;
        int inI = 1 - _active;
        _active = inI;
        FadeTo(outI, SilenceDb, fade, true); // old track: fade out, then stop
        var p = _players[inI];
        p.Stream = s;
        p.VolumeDb = SilenceDb;
        p.StreamPaused = false;
        p.Play();
        FadeTo(inI, volume_db, fade, false); // new track: fade in from silence
    }

    /// <summary>Fade the current track out to silence over `fade` seconds, then stop.</summary>
    public void stop(float fade = Fade)
    {
        _current = "";
        FadeTo(_active, SilenceDb, fade, true);
    }

    /// <summary>Freeze / continue the current track at its position (a menu). Not a fade.</summary>
    public void pause() => _players[_active].StreamPaused = true;
    public void resume() => _players[_active].StreamPaused = false;

    /// <summary>Tween player `i`'s volume to `toDb` over `dur`; optionally stop it at the end. Kills any running fade.</summary>
    private void FadeTo(int i, float toDb, float dur, bool stopAfter)
    {
        var p = _players[i];
        if (_tweens[i] != null && _tweens[i].IsValid())
            _tweens[i].Kill();
        var t = CreateTween();
        t.TweenProperty(p, "volume_db", toDb, dur);
        if (stopAfter)
            t.TweenCallback(Callable.From(p.Stop));
        _tweens[i] = t;
    }

    public void set_volume(float v) => AudioBus.SetVolumeLinear(Bus, v);
    public float get_volume() => AudioBus.GetVolumeLinear(Bus);
    public void set_muted(bool on) => AudioBus.SetMuted(Bus, on);
}
