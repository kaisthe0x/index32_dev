using Godot;

namespace MyGame;

/// <summary>
/// NEEDLE POINT, the stat stall (<c>scenes/things/needle_point.tscn</c>), a <see cref="Stall"/>: a tall booth with a
/// stepped dais Khalid walks up. The scene carries its collision as editable nodes — <c>Dais</c> (two ramps following
/// the stairs, ~37° so they're walkable — a body can't climb separate steps — and the flat top) and <c>Roof</c> (a
/// jump-through platform: land on it from below, drop through with S / Down); this script only sets their physics layers
/// from <see cref="Combat"/>. The E prompt shows on the dais top, and E opens its <see cref="NeedlePointMenu"/> over the
/// run's <see cref="ShotLedger"/> (set by RunManager).
/// </summary>
public partial class NeedlePointStall : Stall
{
    public ShotLedger Ledger = null!;

    public override void _Ready()
    {
        base._Ready();
        var dais = GetNode<StaticBody2D>("Dais");
        dais.CollisionLayer = (uint)Combat.Layer.World;
        dais.CollisionMask = 0;
        var roof = GetNode<StaticBody2D>("Roof");
        roof.CollisionLayer = (uint)Combat.Layer.Platform; // one-way (set on its shape in the scene) → jump/drop-through
        roof.CollisionMask = 0;
    }

    protected override void Interact(Player p)
    {
        var menu = new NeedlePointMenu();
        GetTree().Root.AddChild(menu);
        menu.Open(Ledger, p);
    }
}
