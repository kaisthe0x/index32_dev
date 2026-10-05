namespace MyGame;

/// <summary>How long a Dekken perk lasts once its vial is drunk (see <see cref="PerkDef"/>).</summary>
public enum PerkDuration
{
    Rounds, // active from the drink, through PerkDef.Rounds rounds; drinking another renews it
    OneUse, // happens once, on the drink (a heal, a teleport)
    Run,    // the rest of the run; drunk at the machine only, and it leaves the pool
}
