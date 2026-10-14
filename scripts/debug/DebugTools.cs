using Godot;

namespace MyGame;

/// <summary>
/// DEBUG: the developer keys, in one place (rule <c>C5</c> in <c>docs/standards.md</c>). <see cref="RunManager"/> adds
/// one as a child and asks it only whether <see cref="Recording"/> is on; every key, and what it does, lives here and
/// in this folder. Remove the debug tools by deleting <c>scripts/debug/</c> and RunManager's <c>_debug</c> lines (see
/// the folder's README).
/// <list type="bullet">
/// <item><b>0</b> (<c>debug_respawn</c>) — rebuild the arena fresh.</item>
/// <item><b>Z</b> (<c>debug_damage</c>) — hurt the player.</item>
/// <item><b>X</b> (<c>debug_heal</c>) — give the player one block of Ruh.</item>
/// <item><b>B</b> / <b>N</b> — grant the next catalog buff / clear the granted buffs (<see cref="BuffCycler"/>).</item>
/// <item><b>H</b> — recording mode on / off (<see cref="RecordingMode"/>).</item>
/// </list>
/// </summary>
public partial class DebugTools : Node
{
    private const float Damage = 12.0f; // what Z deals (any hit costs a flat half-block today)

    private readonly Player _player;
    private readonly System.Action _rebuildArena;
    private readonly BuffCycler _buffs;
    private readonly RecordingMode _recording;

    /// <param name="rebuildArena">Rebuilds the arena fresh (<c>RunManager.BuildArena</c>).</param>
    public DebugTools(Player player, System.Action rebuildArena)
    {
        _player = player;
        _rebuildArena = rebuildArena;
        _buffs = new BuffCycler(player);
        _recording = new RecordingMode(rebuildArena);
    }

    /// <summary>Recording mode is on: the run must not tick its rounds or pressure spawns.</summary>
    public bool Recording => _recording.On;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("debug_respawn"))
            _rebuildArena();
        else if (@event.IsActionPressed("debug_damage"))
            _player.TakeDamage(Damage);
        else if (@event.IsActionPressed("debug_heal"))
            _player.Ruh += Player.RuhPerBlock;
        else if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            switch (key.Keycode)
            {
                case Key.B:
                    _buffs.GrantNext();
                    break;
                case Key.N:
                    _buffs.Clear();
                    break;
                case Key.H:
                    _recording.Toggle(this);
                    break;
            }
        }
    }
}
