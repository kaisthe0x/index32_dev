using Godot;

namespace MyGame;

/// <summary>
/// The special's cooldown bar in the HUD gauge (under the Ruh orbs). Always shown: it fills as the special recharges
/// (<see cref="SetProgress"/>, 0..1) and, once READY, pops and then pulses + glows until the special is used. Drawn
/// in the gauge's pixel units (1 unit = 1 sprite pixel, like the pips), centred and NARROWER than the orb row, so the
/// gauge tapers stars → orbs → bar like a triangle; the fill uses the UI accent.
/// </summary>
public partial class SpecialBar : Control
{
    private const int BarWidth = 19;          // pixel columns incl. outline — narrower than the 3-orb row (29), odd to centre
    private const int BarHeight = 4;          // pixel rows of fill (plus a 1px outline all round)
    private const float PulseHz = 1.6f;
    private const float GlowMax = 1.9f;       // HDR brightness at the top of the pulse (blooms on HDR 2D)
    private static readonly Color Outline = new(0.03f, 0.03f, 0.05f);
    private static readonly Color Track = new(0.26f, 0.26f, 0.31f);

    private float _progress = 1.0f;
    private float _pulseTime = 0.0f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(BarWidth, BarHeight + 2);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter; // keep its own width, centred under the orbs
        SetProcess(_progress >= 1.0f); // only ticks while ready (pulsing) — which it is from the start
    }

    /// <summary>Fraction 0..1 of the cooldown elapsed (1 = ready). Returns true on the frame it BECOMES ready.</summary>
    public bool SetProgress(float progress)
    {
        progress = Mathf.Clamp(progress, 0.0f, 1.0f);
        if (progress == _progress)
            return false;
        bool becameReady = progress >= 1.0f && _progress < 1.0f;
        _progress = progress;
        bool ready = progress >= 1.0f;
        SetProcess(ready);
        if (!ready)
            SelfModulate = Colors.White;
        else if (becameReady)
        {
            _pulseTime = 0.0f;
            HudFx.Pop(this);
        }
        QueueRedraw();
        return becameReady;
    }

    public override void _Process(double delta)
    {
        _pulseTime += (float)delta;
        float k = 0.5f + 0.5f * Mathf.Sin(_pulseTime * Mathf.Tau * PulseHz);
        float g = Mathf.Lerp(1.0f, GlowMax, k);
        SelfModulate = new Color(g, g, g);
    }

    public override void _Draw()
    {
        float w = Mathf.Round(Size.X);
        DrawRect(new Rect2(0, 0, w, BarHeight + 2), Outline);
        DrawRect(new Rect2(1, 1, w - 2, BarHeight), Track);
        float fill = Mathf.Floor((w - 2) * _progress); // whole pixels, so it fills in pixel steps
        if (fill > 0)
            DrawRect(new Rect2(1, 1, fill, BarHeight), UiStyle.Accent);
    }
}
