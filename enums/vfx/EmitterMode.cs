namespace MyGame;

/// <summary>How a character's effect row (<see cref="EmitterDef"/>) plays against its animation.</summary>
public enum EmitterMode
{
    Burst,     // spawned fresh each time the animation reaches one of the row's frames (or when fired from code)
    Sustained, // one instance kept on the character, emitting only while the animation is on the row's frames
}
