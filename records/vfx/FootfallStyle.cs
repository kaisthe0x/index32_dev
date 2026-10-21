using Godot;

namespace MyGame;

/// <summary>
/// What a footstep kicks up off one kind of ground (<see cref="Footfalls.ByTileset"/>): soil specks in
/// <paramref name="Dirt"/>'s colours and grass blades in <paramref name="Grass"/>'s. Each particle takes exactly one
/// of the listed colours (the tile's own), never a blend, so the spray reads as pixel art.
/// </summary>
public sealed record FootfallStyle(Color[] Dirt, Color[] Grass);
