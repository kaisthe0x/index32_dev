using Godot;

namespace MyGame;

/// <summary>
/// Tiny tween "juice" shared by the HUD widgets (<see cref="PixelPip"/>, <see cref="FigRing"/>). Scale/rotation only —
/// never position — so they're safe inside containers, which own their children's positions.
/// </summary>
public static class HudFx
{
    private const float PopScale = 1.35f;
    private const float PopTime = 0.2f;
    private const float WiggleAngle = 0.35f;  // radians
    private const float WiggleStep = 0.05f;   // seconds per swing
    private static readonly Color Flash = new(2.2f, 2.2f, 2.2f);

    /// <summary>Swell + flash, then settle back (a gain: an orb filled, a heart healed, a fig banked).</summary>
    public static void Pop(Control c)
    {
        c.PivotOffset = c.Size / 2.0f;
        c.Scale = new Vector2(PopScale, PopScale);
        c.Modulate = Flash;
        var t = c.CreateTween().SetParallel();
        t.TweenProperty(c, "scale", Vector2.One, PopTime).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(c, "modulate", Colors.White, PopTime);
    }

    /// <summary>Flash + a quick side-to-side wiggle (a loss: a heart took a hit).</summary>
    public static void Wiggle(Control c)
    {
        c.PivotOffset = c.Size / 2.0f;
        c.Modulate = Flash;
        var fade = c.CreateTween();
        fade.TweenProperty(c, "modulate", Colors.White, WiggleStep * 4.0f);
        var swing = c.CreateTween();
        swing.TweenProperty(c, "rotation", WiggleAngle, WiggleStep);
        swing.TweenProperty(c, "rotation", -WiggleAngle, WiggleStep * 2.0f);
        swing.TweenProperty(c, "rotation", 0.0f, WiggleStep);
    }
}
