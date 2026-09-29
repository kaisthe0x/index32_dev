namespace MyGame;

/// <summary>
/// One Dekken perk: its <paramref name="Duration"/> kind (and <paramref name="Rounds"/> for a timed one), its Lira
/// <paramref name="Price"/>, and its one tuning number <paramref name="Value"/> (what it means is per perk — see
/// <see cref="Dekken.PERKS"/>). <paramref name="RequiresSpecial"/> gates it to a special (only stocked while that special
/// is equipped); null = always eligible.
/// </summary>
public sealed record PerkDef(string Id, string Name, string Description, PerkDuration Duration, int Rounds, int Price,
    float Value, string RequiresSpecial = null);
