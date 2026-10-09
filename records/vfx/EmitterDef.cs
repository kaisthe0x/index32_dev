using Godot;

namespace MyGame;

/// <summary>
/// One effect row in the emitter tables (<see cref="EmittersCharacters"/>, <see cref="EmittersEnemies"/>): the
/// <paramref name="Scene"/> to spawn and where (<paramref name="Pos"/>, relative to the body's feet, mirrored with
/// facing). An enemy row is just that pair — its code decides when. A character row also says when and how the
/// <see cref="ParticleDirector"/> plays it:
/// <list type="bullet">
/// <item><see cref="Mode"/> — a fresh burst per frame, or one sustained instance.</item>
/// <item><see cref="Frames"/> — the sheet-relative animation frames it plays on (same numbering as the hit-frame and
///   sound tables), or <see cref="AllFrames"/> for the whole animation. Neither = fired from code only.</item>
/// <item><see cref="ConformToGround"/> — lay the effect along the terrain under it (ground slams).</item>
/// <item><see cref="Follow"/> — keep the burst on the character instead of leaving it in the world.</item>
/// <item><see cref="Configure"/> — set typed properties on the spawned node (e.g. a projectile's homing).</item>
/// </list>
/// </summary>
public sealed record EmitterDef(PackedScene Scene, Vector2 Pos)
{
    public EmitterMode Mode { get; init; } = EmitterMode.Burst;
    public int[] Frames { get; init; } = System.Array.Empty<int>();
    public bool AllFrames { get; init; }
    public bool ConformToGround { get; init; }
    public bool Follow { get; init; }
    public System.Action<Node2D>? Configure { get; init; }
}
