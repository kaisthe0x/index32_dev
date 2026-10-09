namespace MyGame;

/// <summary>
/// The player character registry: the roster of ids + resource-path templates. The engine is character-agnostic
/// (one animation set + canvas). Ships Khalid only. C# port of <c>configs/character_config.gd</c>. (Player mirrors
/// these templates inline; kept here as the canonical registry for adding a character.)
/// </summary>
public static class CharacterConfig
{
    public static readonly string[] Ids = { "khalid" };
    public const string FramesPath = "res://resources/characters/{0}.tres";
    public const string PortraitPath = "res://assets/portraits/{0}.png";
    public const string AbilityPath = "res://scripts/abilities/{0}.gd";
}
