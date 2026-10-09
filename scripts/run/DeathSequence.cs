using Godot;

namespace MyGame;

/// <summary>
/// What plays between Khalid dying and the run restarting. A child of <see cref="RunManager"/>, which calls
/// <see cref="Tick"/> every physics tick while he is dead and restarts the run when it returns true, then calls
/// <see cref="End"/>.
///
/// <para>Two endings. KILLED: the death cinematic — the music stops, the camera zooms in on him, the world fades to
/// black behind him, his death animation is held for a beat then released, and once it and the death sound have
/// finished there is a short hold. FELL OUT of the arena: none of that — the camera stays where it is and he drops out
/// of frame; the music stops and the run ends once the fall sound has played.</para>
/// </summary>
public partial class DeathSequence : Node
{
    private const float Hold = 0.7f;        // after the death animation + sound, before the restart
    private const float FadeIn = 0.55f;     // the black overlay coming up
    private const float FadeOut = 0.6f;     // ... and clearing once the new run has started
    private const float Freeze = 0.5f;      // how long the first death frame is held
    private const float FallHold = 1.2f;    // min seconds the run lingers after a fall-death (the fall sound may run longer)
    private const float OverlayHalfSize = 20000.0f;

    private readonly Player _player;
    private readonly RunCamera _camera;
    private readonly Music _music;
    private bool _started;
    private float _holdLeft;
    private float _soundLeft;       // the death sound still playing
    private Polygon2D? _overlay;

    public DeathSequence(Player player, RunCamera camera, Music music)
    {
        _player = player;
        _camera = camera;
        _music = music;
    }

    /// <summary>Advance the sequence for a dead player. True once it has run its course and the run should restart.</summary>
    public bool Tick(float delta)
    {
        if (_player.FellOut())
            return TickFall(delta);
        if (!_started)
        {
            _started = true;
            _holdLeft = Hold;
            _camera.ZoomToDeath();
            BeginCinematic();
        }
        _soundLeft = Mathf.Max(_soundLeft - delta, 0.0f);
        _camera.EaseOnto(_player);
        if (!_player.DeathComplete() || _soundLeft > 0.0f)
            return false;
        _holdLeft -= delta;
        return _holdLeft <= 0.0f;
    }

    private bool TickFall(float delta)
    {
        if (!_started)
        {
            _started = true;
            _music.Stop();
            _holdLeft = Mathf.Max(FallHold, CueLength("player_fall_death"));
        }
        return (_holdLeft -= delta) <= 0.0f;
    }

    private void BeginCinematic()
    {
        _soundLeft = CueLength("player_death");
        _music.Stop();
        _player.ZIndex = WorldZ.DeathPlayer;
        _player.ZAsRelative = false;
        if (_overlay != null && IsInstanceValid(_overlay))
            _overlay.QueueFree();
        const float s = OverlayHalfSize;
        _overlay = new Polygon2D
        {
            Polygon = new Vector2[] { new(-s, -s), new(s, -s), new(s, s), new(-s, s) },
            Color = Colors.Black,
            Modulate = new Color(1, 1, 1, 0.0f),
            ZIndex = WorldZ.DeathOverlay,
            ZAsRelative = false,
        };
        Node host = _camera.Node != null ? _camera.Node : GetParent();
        host.AddChild(_overlay);
        CreateTween().TweenProperty(_overlay, "modulate:a", 1.0, FadeIn);
        GetTree().CreateTimer(Freeze).Timeout += () =>
        {
            if (_player.IsDead())
                _player.ReleaseDeath();
        };
    }

    /// <summary>The new run has started: fade the overlay out and put Khalid back in the world's draw order.</summary>
    public void End()
    {
        _started = false;
        if (_overlay == null || !IsInstanceValid(_overlay))
        {
            ResetPlayerZ();
            return;
        }
        var ov = _overlay;
        _overlay = null;
        var tw = CreateTween();
        tw.TweenProperty(ov, "modulate:a", 0.0, FadeOut);
        tw.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(ov))
                ov.QueueFree();
            ResetPlayerZ();
        }));
    }

    private void ResetPlayerZ()
    {
        _player.ZIndex = WorldZ.Actors;
        _player.ZAsRelative = true;
    }

    /// <summary>Length in seconds of a character sound cue (0 if the cue or its file is missing).</summary>
    private static float CueLength(string cue)
    {
        if (!SfxCharacters.Cues.TryGetValue(cue, out string? path) || !ResourceLoader.Exists(path))
            return 0.0f;
        var s = GD.Load<AudioStream>(path);
        return s != null ? (float)s.GetLength() : 0.0f;
    }
}
