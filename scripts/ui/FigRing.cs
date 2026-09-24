using Godot;

namespace MyGame;

/// <summary>The fada_fig icon wrapped in a progress ring: the ring fills clockwise from 12 o'clock toward the next
/// buff milestone (<see cref="SetProgress"/>). The spendable count is a separate label beside it (see HUD).</summary>
public partial class FigRing : Control
{
    private const float IconSize = 24.0f;
    private const float RingRadius = 16.0f;
    private const float RingWidth = 3.0f;
    private const int RingPoints = 48;
    private static readonly Color Track = new(0.26f, 0.26f, 0.31f);

    private Texture2D _icon;
    private float _progress;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        float side = (RingRadius + RingWidth) * 2.0f;
        CustomMinimumSize = new Vector2(side, side);
        // The fig icon is painted (soft gradients), not pixel art — filter it smoothly when downscaled.
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        _icon = GD.Load<Texture2D>("res://assets/things/fada_fig.png");
    }

    /// <summary>Fraction 0..1 of the way to the next buff milestone.</summary>
    public void SetProgress(float progress)
    {
        progress = Mathf.Clamp(progress, 0.0f, 1.0f);
        if (progress == _progress)
            return;
        _progress = progress;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 c = CustomMinimumSize / 2.0f;
        DrawArc(c, RingRadius, 0.0f, Mathf.Tau, RingPoints, Track, RingWidth, true);
        if (_progress > 0.0f)
        {
            float start = -Mathf.Pi / 2.0f;
            DrawArc(c, RingRadius, start, start + Mathf.Tau * _progress, RingPoints, UiStyle.Accent, RingWidth, true);
        }
        DrawTextureRect(_icon, new Rect2(c - new Vector2(IconSize, IconSize) / 2.0f, new Vector2(IconSize, IconSize)), false);
    }
}
