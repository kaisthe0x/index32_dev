using Godot;

namespace MyGame;

/// <summary>
/// Tuning for the arena's BIRDS — PURE DATA; the readers are <see cref="Bird"/> (one bird) and <see cref="BirdFlock"/>
/// (how many there are and where they sit). Birds are scenery: they perch on random tiles, and take off when Khalid or
/// an enemy comes close. They deal and take no damage and count toward nothing.
///
/// <para>The art is ONE 24-frame strip (<see cref="SheetPath"/>, from <c>index32_art/art/creatures/bird/</c>): frames
/// 0–<see cref="PerchFrame"/> settle onto the perch, the rest take off, fly and break apart (the last frame is empty).</para>
/// </summary>
public static class Birds
{
    // --- the sheet --------------------------------------------------------------------------------------------------
    public const string SheetPath = "res://sprites/creatures/bird/bird.png";
    public const int FrameSize = 32;         // each frame is 32×32 px, drawn with its feet on the frame's bottom edge
    public const float Fps = 10.0f;          // 100 ms a frame, as drawn — the flight (20 frames) lasts 2 s

    /// <summary>The frame a bird holds while perched — reached by playing from frame 0 the moment it comes on screen.</summary>
    public const int PerchFrame = 3;
    /// <summary>The frame the take-off sound (<c>bird_flee</c>) plays on — the first frame after the perch.</summary>
    public const int FleeSoundFrame = 4;

    // --- how many, and where ----------------------------------------------------------------------------------------
    public const int Count = 4;              // birds in the arena at once
    // A bird that flew off is replaced after a random RespawnMin–RespawnMax seconds, on a tile the camera can't see.
    public const float RespawnMin = 6.0f;
    public const float RespawnMax = 14.0f;
    public const float OffscreenMargin = 64.0f; // a new bird's tile is at least this far outside the camera's view
    public const float MinSpacing = 96.0f;      // …and this far from every other bird (3 tiles)
    public const int SpawnTries = 12;           // random tiles tried per attempt; none fits = try again next tick

    // --- taking off -------------------------------------------------------------------------------------------------
    public const float ScareRadius = 80.0f;  // Khalid or an enemy this close (2.5 tiles) scares a perched bird
    /// <summary>Flight velocity (px/s) once up to speed, for a bird fleeing to the RIGHT (X is mirrored for the left):
    /// up and away, covering ~210 px across and ~170 px up over the 2 s flight.</summary>
    public static readonly Vector2 FleeVelocity = new(120.0f, -95.0f);
    public const float FleeRampTime = 0.5f;  // seconds to reach that speed — the first frames are the crouch and the jump
}
