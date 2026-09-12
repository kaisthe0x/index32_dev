using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Background MUSIC service (autoload <c>Music</c>) — a CROSSFADING bed driven by per-stage PLAYLISTS. Each stage's
/// tracks are whatever audio files sit in <c>music/&lt;stage&gt;/</c> (auto-discovered — see <see cref="StageTracks"/>),
/// so you just drop files in the folder, no code edit + names don't matter. <c>play_stage(id)</c> plays them in order
/// (sorted by filename), each crossfading into the next and wrapping back to the first, forever. Two AudioStreamPlayers
/// ping-pong so a track can fade in while the previous fades out. C# port of <c>scripts/audio/music.gd</c>. Snake_case
/// public surface (the bridged C# callers address <c>play_stage/stop</c> by exact name).
/// </summary>
public partial class Music : Node
{
    private const string MusicDir = "res://music/"; // each stage's playlist = the audio files in MusicDir + "<stage>/"

    private static readonly StringName Bus = "Music";
    private const float DefaultVolumeDb = -1.0f; // the "full" music level once faded in
    private const float SilenceDb = -60.0f;
    private const float StartFade = 1.5f;         // fade when a stage's music first starts / when it stops
    private const float CrossfadeBetween = 3.0f;  // the slight crossfade between one playlist track and the next

    private readonly List<AudioStreamPlayer> _players = new();
    private readonly Tween[] _tweens = new Tween[2];
    private int _active = 0;
    private readonly Dictionary<string, AudioStream> _cache = new();

    // Active playlist state.
    private string[] _playlist = System.Array.Empty<string>();
    private int _trackIndex = 0;
    private bool _playlistOn = false;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always; // advance the playlist even if the game pauses
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

    public override void _Process(double delta)
    {
        // Crossfade into the next track as the active one nears its end (position-based, so the fade lands right at
        // the seam). A single-track playlist just loops (nothing to advance to).
        if (!_playlistOn || _playlist.Length < 2)
            return;
        var p = _players[_active];
        if (p.Stream == null || !p.Playing)
            return;
        float len = (float)p.Stream.GetLength();
        if (len > 0.0f && len - p.GetPlaybackPosition() <= CrossfadeBetween)
            AdvanceTrack();
    }

    /// <summary>Start (or restart) a stage's looping background playlist — its tracks play in order, each crossfading
    /// into the next, wrapping back to the first. Stops + warns if the stage folder has no tracks.</summary>
    public void play_stage(string stage)
    {
        var list = StageTracks(stage);
        if (list.Length == 0)
        {
            stop();
            GD.PushWarning($"Music: no audio files in {MusicDir}{stage}/ (playing nothing)");
            return;
        }
        _playlist = list;
        _trackIndex = 0;
        _playlistOn = true;
        PlayTrack(_trackIndex, StartFade);
    }

    /// <summary>A stage's playlist = every audio file in <c>music/&lt;stage&gt;/</c>, sorted by filename (so the sequence
    /// is deterministic — name tracks to control order, e.g. a <c>1_</c>/<c>2_</c> prefix). Drop files in the folder and
    /// they play; no code change, and the names themselves don't matter beyond that sort. Scanned fresh each call (cheap,
    /// only on run start / restart), so swapping files just needs a run restart. Mirrors <c>RunManager.StageLayoutPaths</c>.</summary>
    private static string[] StageTracks(string stage)
    {
        var list = new List<string>();
        string dir = MusicDir + stage + "/";
        using var da = DirAccess.Open(dir);
        if (da != null)
        {
            da.ListDirBegin();
            for (string f = da.GetNext(); f != ""; f = da.GetNext())
            {
                if (da.CurrentIsDir())
                    continue;
                string name = f.TrimSuffix(".remap"); // exported builds serve <track>.ogg.remap
                string lower = name.ToLower();
                if (lower.EndsWith(".ogg") || lower.EndsWith(".mp3") || lower.EndsWith(".wav"))
                    list.Add(dir + name);
            }
            da.ListDirEnd();
        }
        list.Sort(); // deterministic play order (filename)
        return list.ToArray();
    }

    private void AdvanceTrack()
    {
        _trackIndex = (_trackIndex + 1) % _playlist.Length;
        PlayTrack(_trackIndex, CrossfadeBetween);
    }

    private void PlayTrack(int index, float fade)
    {
        var s = Stream(_playlist[index]);
        if (s == null)
            return;
        int outI = _active;
        int inI = 1 - _active;
        _active = inI;
        FadeTo(outI, SilenceDb, fade, true); // old track: fade out, then stop
        var p = _players[inI];
        p.Stream = s;
        p.VolumeDb = SilenceDb;
        p.StreamPaused = false;
        p.Play();
        FadeTo(inI, DefaultVolumeDb, fade, false); // new track: fade in from silence
    }

    /// <summary>The stream for a track path (cached). Force-looped as a SAFETY NET — if a per-frame crossfade tick is
    /// ever missed, the track repeats rather than going silent (the playlist normally crossfades away before then).</summary>
    private AudioStream Stream(string path)
    {
        if (_cache.TryGetValue(path, out var cached))
            return cached;
        AudioStream s = null;
        if (path != "" && ResourceLoader.Exists(path))
        {
            s = GD.Load<AudioStream>(path);
            // Duplicate before flipping Loop so we don't mutate the shared cached import.
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
            GD.PushWarning($"Music: track {path} not found (playing nothing)");
        }
        _cache[path] = s;
        return s;
    }

    /// <summary>Fade the current track out to silence over `fade` seconds, then stop. Ends the playlist.</summary>
    public void stop(float fade = StartFade)
    {
        _playlistOn = false;
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
