using Godot;

namespace MyGame;

/// <summary>
/// Edge-of-screen arrows pointing at OFF-SCREEN enemies — so you know where they are while airborne / after an
/// orb launch, when the tight camera has left them out of frame. Lives in the HUD CanvasLayer (screen space);
/// each frame it projects every enemy through the camera and draws a clamped, rotated, per-enemy-tinted chevron
/// faded by world distance. C# port of <c>scripts/ui/offscreen_markers.gd</c>. HUD-only consumer.
/// </summary>
public partial class OffscreenMarkers : Control
{
    private const float Margin = 24.0f;
    private const float SizeNear = 13.0f;
    private const float SizeFar = 7.0f;
    private const float FadeStart = 250.0f;
    private const float FadeEnd = 1600.0f;
    private const float AlphaNear = 0.95f;
    private const float AlphaFar = 0.40f;

    public override void _Ready()
    {
        SetAnchorsPreset(Control.LayoutPreset.FullRect);
        MouseFilter = Control.MouseFilterEnum.Ignore;
        SetProcess(true);
    }

    public override void _Process(double delta) => QueueRedraw(); // enemies + camera move each frame

    public override void _Draw()
    {
        Transform2D xform = GetViewport().CanvasTransform; // world -> screen (the active camera)
        Vector2 view = GetViewportRect().Size;
        Vector2 center = view * 0.5f;
        Vector2 camCenter = xform.AffineInverse() * center; // camera centre in WORLD space (for the fade)
        Vector2 lo = new(Margin, Margin);
        Vector2 hi = view - new Vector2(Margin, Margin);

        foreach (Node e in GetTree().GetNodesInGroup("enemies"))
        {
            if (e is not Enemy enemy)
                continue;
            Vector2 screen = xform * enemy.GlobalPosition;
            if (screen.X >= 0.0f && screen.X <= view.X && screen.Y >= 0.0f && screen.Y <= view.Y)
                continue; // on-screen -> no arrow
            Vector2 dir = screen - center;
            if (dir.Length() < 0.001f)
                continue;
            Vector2 edge = ClampToRect(center, dir, lo, hi);
            float worldDist = enemy.GlobalPosition.DistanceTo(camCenter);
            float t = Mathf.Clamp((worldDist - FadeStart) / Mathf.Max(FadeEnd - FadeStart, 1.0f), 0.0f, 1.0f);
            Color col = EnemyMarkers.ColorFor(enemy.enemy_id);
            col.A = Mathf.Lerp(AlphaNear, AlphaFar, t);
            DrawChevron(edge, dir.Angle(), Mathf.Lerp(SizeNear, SizeFar, t), col);
        }
    }

    /// <summary>Where the ray from `c` in direction `dir` first crosses the inset rect [lo, hi]. Returns `c` if degenerate.</summary>
    private static Vector2 ClampToRect(Vector2 c, Vector2 dir, Vector2 lo, Vector2 hi)
    {
        float t = float.PositiveInfinity;
        if (dir.X > 0.001f)
            t = Mathf.Min(t, (hi.X - c.X) / dir.X);
        else if (dir.X < -0.001f)
            t = Mathf.Min(t, (lo.X - c.X) / dir.X);
        if (dir.Y > 0.001f)
            t = Mathf.Min(t, (hi.Y - c.Y) / dir.Y);
        else if (dir.Y < -0.001f)
            t = Mathf.Min(t, (lo.Y - c.Y) / dir.Y);
        return float.IsFinite(t) ? c + dir * t : c;
    }

    /// <summary>A filled triangle pointing along `angle` at `pos`, with a faint dark outline. `half` ≈ tip distance.</summary>
    private void DrawChevron(Vector2 pos, float angle, float half, Color col)
    {
        Vector2 fwd = Vector2.Right.Rotated(angle);
        Vector2 side = fwd.Orthogonal();
        Vector2 tip = pos + fwd * half;
        Vector2 a = pos - fwd * (half * 0.55f) + side * (half * 0.85f);
        Vector2 b = pos - fwd * (half * 0.55f) - side * (half * 0.85f);
        DrawColoredPolygon(new[] { tip, a, b }, col);
        DrawPolyline(new[] { tip, a, b, tip }, new Color(0.0f, 0.0f, 0.0f, col.A * 0.55f), 1.5f);
    }
}
