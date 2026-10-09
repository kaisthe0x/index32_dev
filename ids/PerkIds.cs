namespace MyGame;

/// <summary>Stable string IDs for the Dekken perks (see <see cref="AttackIds"/> for why const string, not enum): the id
/// is the key into <see cref="Dekken.Perks"/> and what <see cref="Perk"/> / <see cref="PerkLedger"/> switch on.</summary>
public static class PerkIds
{
    public const string Heal = "heal";
    public const string FastTravel = "fast_travel";
    public const string FigChance = "fig_chance";
    public const string Magnet = "magnet";
    public const string Shield = "shield";
    public const string Prepared = "prepared";
    public const string WiderPull = "wider_pull";
}
