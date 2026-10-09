namespace MyGame;

/// <summary>
/// A slot in the player's swappable loadout. Closed set: three combat slots (Attack/Special/Surge) + four
/// movement slots (Run/Jump/Dash/Slam). <see cref="LoadoutCategories.Kind"/> names the slot's pool in <see cref="Actions"/>.
/// </summary>
public enum LoadoutCategory
{
    Attack,
    Special,
    Surge,
    Run,
    Jump,
    Dash,
    Slam,
}

/// <summary>Helpers for <see cref="LoadoutCategory"/> — the movement set, the snake key and the Actions pool kind.</summary>
public static class LoadoutCategories
{
    public static readonly LoadoutCategory[] Movement =
        { LoadoutCategory.Run, LoadoutCategory.Jump, LoadoutCategory.Dash, LoadoutCategory.Slam };

    /// <summary>The snake key ("attack"/"special"/… ) — a movement slot's pool name in <see cref="Actions"/>.</summary>
    public static string Key(this LoadoutCategory c) => c.ToString().ToLowerInvariant();

    /// <summary>The <see cref="Actions"/> pool name: combat slots pluralise (attack→attacks); movement slots are 1:1.</summary>
    public static string Kind(this LoadoutCategory c) => c switch
    {
        LoadoutCategory.Attack => "attacks",
        LoadoutCategory.Special => "specials",
        LoadoutCategory.Surge => "surges",
        _ => c.Key(),
    };
}
