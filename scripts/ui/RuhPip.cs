namespace MyGame;

/// <summary>One Ruh BLOCK as an orb that fills from the bottom like liquid as hits bank Ruh (level = that block's
/// share, 0..1). Any partial level shows at least the bottom row, so progress is always visible.</summary>
public partial class RuhPip : PixelPip
{
    private static readonly string[] OrbMask =
    {
        "..###..",
        ".#s###.",
        "#s#####",
        "#######",
        "#######",
        ".#####.",
        "..###..",
    };

    protected override string[] Mask => OrbMask;

    protected override bool IsFilled(int x, int y, int w, int h) => h - 1 - y < Level * h;
}
