using Godot;

namespace MyGame;

/// <summary>
/// An enemy's attack sounds: the start cue of each attack type (<c>&lt;enemy&gt;.&lt;type&gt;</c>) and the cues
/// tied to animation frames (<see cref="SfxEnemies.FramesFor"/>). A normal attack's sounds are fire-and-forget; a
/// held CHANNEL's play on their own players so a stagger that cuts the channel short can stop them
/// (<see cref="Stop"/>).
/// </summary>
public sealed class AttackSounds
{
    private readonly Node2D _owner;
    private readonly string _enemyId;
    private readonly Dictionary<string, Dictionary<int, string>> _frameCues = new(); // anim -> { emitted frame -> cue }
    private readonly List<AudioStreamPlayer2D> _channelPlayers = new();
    private bool _channel;   // the attack in progress is a held channel

    public AttackSounds(Node2D owner, string enemyId, SpriteFrames frames)
    {
        _owner = owner;
        _enemyId = enemyId;
        foreach (var (anim, cues) in SfxEnemies.FramesFor(enemyId))
        {
            if (!frames.HasAnimation(anim))
                continue;
            int start = AnimMeta.SheetStart(frames, anim);   // the table is in sheet frames
            var map = new Dictionary<int, string>();
            foreach (var (sheetFrame, cue) in cues)
                map[sheetFrame - start] = cue;
            _frameCues[anim] = map;
        }
    }

    /// <summary>Play the start cue of the attack type <paramref name="typeKey"/>, cutting off any attack sound still
    /// playing. <paramref name="channel"/> = the attack is a held channel (its sounds, this one and the per-frame
    /// ones, must be stoppable).</summary>
    public void PlayStart(string typeKey, bool channel = false)
    {
        _channel = channel;
        Stop();
        Play($"{_enemyId}.{typeKey}");
    }

    /// <summary>Play the cue tied to <paramref name="frame"/> of <paramref name="anim"/>, if there is one.</summary>
    public void PlayFrame(StringName anim, int frame)
    {
        if (_frameCues.Count == 0)
            return;
        if (_frameCues.TryGetValue(anim, out var map) && map.TryGetValue(frame, out string? cue))
            Play(cue);
    }

    /// <summary>Silence the channel's sounds (the attack was interrupted, or the enemy died).</summary>
    public void Stop()
    {
        foreach (var pl in _channelPlayers)
            if (GodotObject.IsInstanceValid(pl))
            {
                pl.Stop();
                pl.QueueFree();
            }
        _channelPlayers.Clear();
    }

    private void Play(string cue)
    {
        if (cue == "")
            return;
        var sfx = _owner.GetNodeOrNull<Sfx>("/root/Sfx");
        if (!_channel)
        {
            sfx?.PlayAt(cue, _owner.GlobalPosition);
            return;
        }
        var pl = sfx?.MakeOneshot2D(cue);
        if (pl == null)
            return;
        _owner.AddChild(pl);
        _channelPlayers.Add(pl);
        pl.Finished += () =>
        {
            _channelPlayers.Remove(pl);
            pl.QueueFree();
        };
        pl.Play();
    }
}
