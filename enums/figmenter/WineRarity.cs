namespace MyGame;

/// <summary>How aged a carried <see cref="Wine"/> is — and so how strong the buff in it comes out
/// (<see cref="BuffCatalog"/> holds one value per rarity). A wine is Common when it leaves the Figmenter and climbs a
/// step every <see cref="FigmenterRules.AgeRounds"/> rounds it is carried. Colours: <see cref="FigmenterRules.RarityColor"/>.</summary>
public enum WineRarity
{
    Common,
    Rare,
    Hot,
    Sensational,
    Epic,
}
