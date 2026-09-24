using Godot;

namespace MyGame;

/// <summary>
/// A small pixel-art HUD icon drawn from a character <see cref="Mask"/>, one unit per mask cell, with an auto-traced
/// 1-cell outline. <c>#</c> = body, <c>s</c> = body shine (drawn lighter when filled), <c>.</c> = empty. The owner
/// scales the pip's container to set the on-screen pixel size (the HUD matches the sprites' pixel size).
/// A pip holds a fill <see cref="Level"/> (0..1); the subclass decides which body pixels that level fills — a health
/// star splits down the middle (<see cref="HealthPip"/>), a Ruh orb fills from the bottom like liquid (<see cref="RuhPip"/>).
/// </summary>
public abstract partial class PixelPip : Control
{
    private static readonly Color Outline = new(0.03f, 0.03f, 0.05f);
    private const float ShineLift = 0.45f;

    protected abstract string[] Mask { get; }
    protected abstract bool IsFilled(int x, int y, int w, int h);

    /// <summary>Fill fraction 0..1 (set via <see cref="SetLevel"/>).</summary>
    protected float Level { get; private set; }
    private Color _fill;
    private Color _empty;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(Mask[0].Length + 2, Mask.Length + 2);
    }

    public void SetLevel(float level, Color fill, Color empty)
    {
        level = Mathf.Clamp(level, 0.0f, 1.0f);
        if (level == Level && fill == _fill && empty == _empty)
            return;
        Level = level;
        _fill = fill;
        _empty = empty;
        QueueRedraw();
    }

    public override void _Draw()
    {
        string[] m = Mask;
        int h = m.Length, w = m[0].Length;
        Color shine = _fill.Lightened(ShineLift);
        for (int y = -1; y <= h; y++)
        {
            for (int x = -1; x <= w; x++)
            {
                char cell = Cell(m, x, y);
                if (cell == '.')
                {
                    if (Cell(m, x - 1, y) != '.' || Cell(m, x + 1, y) != '.' || Cell(m, x, y - 1) != '.' || Cell(m, x, y + 1) != '.')
                        DrawRect(PixelRect(x, y), Outline);
                    continue;
                }
                Color c = !IsFilled(x, y, w, h) ? _empty : cell == 's' ? shine : _fill;
                DrawRect(PixelRect(x, y), c);
            }
        }
    }

    private static char Cell(string[] m, int x, int y) =>
        y < 0 || y >= m.Length || x < 0 || x >= m[y].Length ? '.' : m[y][x];

    private static Rect2 PixelRect(int x, int y) => new(x + 1, y + 1, 1, 1);
}
