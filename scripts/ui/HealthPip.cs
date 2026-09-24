namespace MyGame;

/// <summary>One health BLOCK as an eight-point star that splits down the middle: level 1 = full, 0.5 = left half,
/// 0 = empty (every hit costs exactly half a block, so those are the only states).</summary>
public partial class HealthPip : PixelPip
{
    private static readonly string[] StarMask =
    {
        ".....#.....",
        ".#..###..#.",
        "..#.###.#..",
        "...#s###...",
        ".##s######.",
        "###########",
        ".#########.",
        "...#####...",
        "..#.###.#..",
        ".#..###..#.",
        ".....#.....",
    };

    protected override string[] Mask => StarMask;

    protected override bool IsFilled(int x, int y, int w, int h) => x < Level * w;
}
