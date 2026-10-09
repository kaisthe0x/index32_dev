using Godot;

namespace MyGame;

/// <summary>
/// DEKKEN, the perk shop (<c>scenes/things/dekken.tscn</c> — a triangular vending machine full of vials, <c>assets/things/dekken.png</c>), a
/// <see cref="Stall"/>: press E to open its <see cref="DekkenMenu"/> over the run's <see cref="PerkLedger"/> (set by
/// RunManager). One per arena. The vials in its art wear Khalid's HAIR colour — the default red, or whatever he
/// picked for the run (<see cref="PaletteConfig.HairColor"/>) — via <c>vial_recolor.gdshader</c>.
/// </summary>
public partial class DekkenStall : Stall
{
    private const string VialShader = "res://vfx/shaders/vial_recolor.gdshader";
    private const string TintParam = "tint";   // the shader's vial colour
    private const string ArtNode = "Art";      // the machine's sprite, under the scene's Visual node

    public PerkLedger Ledger;

    public override void _Ready()
    {
        base._Ready();
        TintVials();
    }

    /// <summary>Paint the vials (the art's only red) the hair colour at FULL brightness — the shader scales it by each
    /// painted pixel's own brightness, so the pick sets the hue and the art keeps the shading.</summary>
    private void TintVials()
    {
        Color hair = PaletteConfig.HairColor();
        float peak = Mathf.Max(hair.R, Mathf.Max(hair.G, hair.B));
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>(VialShader) };
        mat.SetShaderParameter(TintParam, peak > 0.0f ? new Color(hair.R / peak, hair.G / peak, hair.B / peak) : hair);
        Visual.GetNode<Sprite2D>(ArtNode).Material = mat;
    }

    protected override void Interact(Player p)
    {
        Pop();
        var menu = new DekkenMenu();
        GetTree().Root.AddChild(menu);
        menu.Open(Ledger, p);
    }
}
