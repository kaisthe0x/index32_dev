namespace MyGame;

/// <summary>
/// One bottle of fig wine: what <see cref="Kind"/> of thing it holds, the <see cref="Id"/> of the buff / special it
/// grants when drunk, and the <see cref="Name"/> + <see cref="Description"/> on its label. Its rarity is not here — it
/// grows while the wine is carried (<see cref="FigmenterLedger.CarriedRarity"/>). Made by <see cref="FigmenterLedger"/>.
/// </summary>
public sealed record Wine(WineKind Kind, string Id, string Name, string Description);
