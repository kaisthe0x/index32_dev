namespace MyGame;

/// <summary>
/// An active Dekken perk that lasts (a timed or whole-run one — one-use perks just happen, see <see cref="PerkLedger"/>),
/// held as a <see cref="Passive"/>: <see cref="Setup"/> turns it on, <see cref="Teardown"/> undoes it exactly, and the
/// round-scoped ones re-arm in <see cref="OnRoundStart"/>. The ledger keeps <see cref="RoundsLeft"/> current for the HUD.
/// </summary>
public partial class Perk : Passive
{
    public readonly PerkDef Def;

    /// <summary>Rounds this perk still covers (timed perks only) — counted down at each round clear.</summary>
    public int RoundsLeft;

    public Perk(PerkDef def)
    {
        Id = def.Id;
        Def = def;
        RoundsLeft = def.Rounds;
    }

    public override void Setup(Player p)
    {
        switch (Def.Id)
        {
            case PerkIds.FigChance: p.FigChanceBonus += Def.Value; break;
            case PerkIds.Magnet: p.FigMagnetRange += Def.Value; break;
            case PerkIds.Shield: p.HitShields = (int)Def.Value; break;
            case PerkIds.WiderPull: p.MagnetTargetBonus += (int)Def.Value; break;
        }
    }

    public override void Teardown(Player p)
    {
        switch (Def.Id)
        {
            case PerkIds.FigChance: p.FigChanceBonus -= Def.Value; break;
            case PerkIds.Magnet: p.FigMagnetRange -= Def.Value; break;
            case PerkIds.Shield: p.HitShields = 0; break;
            case PerkIds.WiderPull: p.MagnetTargetBonus -= (int)Def.Value; break;
        }
    }

    public override void OnRoundStart(Player p)
    {
        switch (Def.Id)
        {
            case PerkIds.Shield: p.HitShields = (int)Def.Value; break; // a fresh block each round it covers
            case PerkIds.Prepared: p.SurgeFree(); break;
        }
    }
}
