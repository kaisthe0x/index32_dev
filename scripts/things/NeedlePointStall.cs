using Godot;

namespace MyGame;

/// <summary>
/// NEEDLE POINT, the stat stall (placeholder art — a teal cabinet with a syringe), a <see cref="Stall"/>: press E to
/// open its <see cref="NeedlePointMenu"/> over the run's <see cref="ShotLedger"/> (set by RunManager before it's added).
/// One per arena, near the spawn. Built entirely in code.
/// </summary>
public partial class NeedlePointStall : Stall
{
    public ShotLedger Ledger;

    protected override void BuildVisual(Node2D visual)
    {
        visual.AddChild(new Polygon2D // the cabinet
        {
            Polygon = new Vector2[] { new(-16, -34), new(16, -34), new(16, -2), new(-16, -2) },
            Color = new Color(0.16f, 0.36f, 0.40f),
        });
        var glow = new Color(0.55f, 1.6f, 1.5f); // HDR teal so it reads as lit
        visual.AddChild(new Polygon2D // syringe barrel
        {
            Polygon = new Vector2[] { new(-3, -28), new(3, -28), new(3, -12), new(-3, -12) },
            Color = glow,
        });
        visual.AddChild(new Polygon2D // plunger
        {
            Polygon = new Vector2[] { new(-6, -31), new(6, -31), new(6, -29), new(-6, -29) },
            Color = glow,
        });
        visual.AddChild(new Polygon2D // needle
        {
            Polygon = new Vector2[] { new(-0.5f, -12), new(0.5f, -12), new(0.5f, -6), new(-0.5f, -6) },
            Color = glow,
        });
    }

    protected override void Interact(Player p)
    {
        Pop();
        var menu = new NeedlePointMenu();
        GetTree().Root.AddChild(menu);
        menu.Open(Ledger, p);
    }
}
