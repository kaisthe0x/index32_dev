namespace MyGame;

/// <summary>How long a Dekken perk lasts once bought (see <see cref="PerkDef"/>).</summary>
public enum PerkDuration
{
    Rounds, // active now, through the next PerkDef.Rounds rounds; rebuying renews it
    OneUse, // happens once, on purchase (a heal, a teleport)
    Run,    // the rest of the run; leaves the pool once bought
}
