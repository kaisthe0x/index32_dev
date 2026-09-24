namespace MyGame;

/// <summary>
/// The draw order of every <see cref="Godot.CanvasLayer"/> — ONE table, so screens can't silently cover each other.
/// Higher draws on top. Always use these instead of a literal layer number.
/// </summary>
public static class UiLayers
{
    public const int Background = -100; // the arena backdrop (RunManager)
    public const int LowHealth = 50;    // the low-HP screen grade — tints the world, stays under all UI
    public const int Gauge = 60;        // the FollowKhalid health/Ruh gauge — above the grade, below the HUD
    public const int Hud = 100;         // the HUD autoload
    public const int Banner = 105;      // the centred "LEVEL UP!" banner
    public const int Menu = 110;        // modal pick menus: the attack picker, the buff cards
    public const int Pause = 120;       // the Esc pause menu — above everything
}
