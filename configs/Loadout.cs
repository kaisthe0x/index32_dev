namespace MyGame;

/// <summary>
/// The player's LOADOUT layer: per <see cref="LoadoutCategory"/> a character has one or more options, each an
/// Action. This answers which one a character starts with.
/// </summary>
public static class Loadout
{
    /// <summary>The default (starting) option id for a category.</summary>
    public static string DefaultId(string character, LoadoutCategory category)
    {
        var a = Actions.GetAction(character, category.Kind());
        return a != null ? a.Id : "";
    }
}
