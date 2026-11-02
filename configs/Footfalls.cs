using System.Collections.Generic;
using Godot;

namespace MyGame;

/// <summary>
/// FOOTFALLS: the dirt and grass a running character kicks up off the ground (<see cref="FootKick"/> plays them).
/// Which ground sheds what is keyed by the tileset image its tiles come from — a tileset with no row (stone, brick)
/// sheds nothing. Colours are the tiles' own palette, so the spray matches the floor it comes off.
/// </summary>
public static class Footfalls
{
    public static readonly Dictionary<string, FootfallStyle> ByTileset = new()
    {
        // Stage 1's grass floor: soil specks in its earth tones, blades in its moss greens. (tileset2, the stone and
        // brick bridge, has no row: stone doesn't kick up.)
        ["res://assets/terrain/stage1/tileset1.png"] = new(
            Dirt: new[] { new Color("363548"), new Color("4a5566"), new Color("5a705e") },
            Grass: new[] { new Color("6f8a66"), new Color("86a071"), new Color("9db582") }),
    };

    /// <summary>character -> animation -> the sheet frames (same numbering as the hit-frame and sound tables) a foot
    /// pushes off the ground on. Khalid's sprint drives off the back foot on 2, 6 and 9.</summary>
    public static readonly Dictionary<string, Dictionary<string, int[]>> KickFrames = new()
    {
        ["khalid"] = new() { ["run"] = new[] { 2, 6, 9 } },
    };

    /// <summary>Where the spray starts, from the character's feet, for one facing right (mirrored when facing left):
    /// just behind the pushing heel.</summary>
    public static readonly Vector2 HeelOffset = new(-9, -1);

    // One kick: specks and blades thrown up and back, high enough (up to ~25 px) to clear the ground line into the
    // dark backdrop -- grass the colour of the grass only reads once it is above it -- falling, gone as they land.
    public const int DirtPerKick = 13;
    public const int GrassPerKick = 5;
    public const float DirtLife = 0.5f;
    public const float GrassLife = 0.4f;
    public const float DirtSizeMin = 1.0f; // px: a speck is 1-2 pixels
    public const float DirtSizeMax = 2.0f;
    public const float SpeedMin = 70.0f; // px/s
    public const float SpeedMax = 135.0f;
    public const float Spread = 45.0f; // degrees either side of the throw
    public static readonly Vector2 Throw = new(-0.75f, -1.6f); // up and BACK (for facing right); normalised when used
    public static readonly Vector2 Gravity = new(0, 380);
}
