namespace MyGame;

/// <summary>
/// One mystery-box spin's result: its <see cref="Outcome"/>, and for a buff / special the <see cref="Id"/> it would
/// grant plus the <see cref="Name"/> + <see cref="Description"/> the box shows while it's on offer. A teddy bear has
/// no id. Rolled by <see cref="BoxLedger.Spin"/>, granted by <see cref="BoxLedger.Take"/>.
/// </summary>
public sealed record BoxRoll(BoxOutcome Outcome, string Id, string Name, string Description);
