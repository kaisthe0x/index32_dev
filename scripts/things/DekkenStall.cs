using Godot;

namespace MyGame;

/// <summary>
/// DEKKEN, the perk shop (placeholder art — an amber market stall with an awning), a <see cref="Stall"/>: press E to
/// open its <see cref="DekkenMenu"/> over the run's <see cref="PerkLedger"/> (set by RunManager before it's added).
/// One per arena. Built entirely in code.
/// </summary>
public partial class DekkenStall : Stall
{
    public PerkLedger Ledger;

    protected override void BuildVisual(Node2D visual)
    {
        visual.AddChild(new Polygon2D // the counter
        {
            Polygon = new Vector2[] { new(-18, -20), new(18, -20), new(18, -2), new(-18, -2) },
            Color = new Color(0.36f, 0.24f, 0.16f),
        });
        var glow = new Color(1.6f, 1.05f, 0.35f); // HDR amber so it reads as lit
        visual.AddChild(new Polygon2D // awning
        {
            Polygon = new Vector2[] { new(-22, -34), new(22, -34), new(18, -26), new(-18, -26) },
            Color = glow,
        });
        foreach (float x in new[] { -16f, 14f }) // posts
            visual.AddChild(new Polygon2D
            {
                Polygon = new Vector2[] { new(x, -26), new(x + 2, -26), new(x + 2, -20), new(x, -20) },
                Color = glow,
            });
    }

    protected override void Interact(Player p)
    {
        Pop();
        var menu = new DekkenMenu();
        GetTree().Root.AddChild(menu);
        menu.Open(Ledger, p);
    }
}
