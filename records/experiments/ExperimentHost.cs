using Godot;

namespace MyGame;

/// <summary>What an <see cref="IExperiment"/> is given: the player, and the arena root it adds its nodes under. Only
/// what an experiment needs today; an idea that needs more of the game is ready to become real game code
/// (docs/standards.md, rule C8).</summary>
public sealed record ExperimentHost(Player Player, Node Arena);
