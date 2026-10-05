using Godot;

namespace MyGame;

/// <summary>
/// DEKKEN, the perk shop (<c>scenes/things/dekken.tscn</c> — a triangular vending machine full of vials, <c>assets/things/dekken.png</c>), a
/// <see cref="Stall"/>: press E to open its <see cref="DekkenMenu"/> over the run's <see cref="PerkLedger"/> (set by
/// RunManager). One per arena.
/// </summary>
public partial class DekkenStall : Stall
{
    public PerkLedger Ledger;

    protected override void Interact(Player p)
    {
        Pop();
        var menu = new DekkenMenu();
        GetTree().Root.AddChild(menu);
        menu.Open(Ledger, p);
    }
}
