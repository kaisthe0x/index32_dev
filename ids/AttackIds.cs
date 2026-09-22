namespace MyGame;

/// <summary>
/// Stable string IDs for Khalid's ATTACKS. `const string` (not an enum) because the value IS the id AND the config
/// key — it builds the animation name (<c>attack_&lt;id&gt;</c>) and indexes the Emitters + SfxCharacters tables — so
/// there's nothing to convert. Reference them everywhere (<c>AttackIds.TwinReaper</c>) instead of raw string literals.
/// </summary>
public static class AttackIds
{
    public const string OraOra = "ora_ora";
    public const string Spear = "spear";
    public const string Bakshen = "bakshen";
    public const string Zahluq = "zahluq";
    public const string CherryShots = "cherry_shots";
    public const string TwinReaper = "twin_reaper";
    public const string RopeDart = "rope_dart";
}
