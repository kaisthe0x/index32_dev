using Godot;

namespace MyGame;

/// <summary>
/// Engulfs a sprite in a coloured additive overlay for a duration — e.g. the green cast of a freeze. Mirrors
/// the target <see cref="AnimatedSprite2D"/> (frame, flip, offset, scale) with a tint drawn on top, so the
/// character shape glows in that colour whether animating or frozen. C# port of <c>scripts/combat/status_overlay.gd</c>.
/// Only the C# Player/Enemy use it (plain class).
/// </summary>
public partial class StatusOverlay : Node2D
{
    private AnimatedSprite2D _target = null!;
    private AnimatedSprite2D _overlay = null!;
    private float _time = 0.0f;
    private Color _color = Colors.White;
    private float _phase = 0.0f;

    /// <summary>Build the additive overlay sprite that mirrors `target`, hidden until <see cref="ShowFor"/>.</summary>
    public void Setup(AnimatedSprite2D target)
    {
        _target = target;
        _overlay = new AnimatedSprite2D { ZIndex = 1, Visible = false };
        var mat = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
        _overlay.Material = mat;
        AddChild(_overlay);
        SetProcess(false);
    }

    /// <summary>Show the overlay tinted `color` for `duration` seconds.</summary>
    public void ShowFor(Color color, float duration)
    {
        if (_target == null || duration <= 0.0f)
            return;
        _color = color;
        _phase = 0.0f;
        _overlay.Modulate = color;
        _time = duration;
        _overlay.Visible = true;
        Sync();
        SetProcess(true);
    }

    /// <summary>Kill any active tint NOW (not on its timer) — e.g. the enemy died and its overlays should clear instantly.</summary>
    public void Clear()
    {
        _time = 0.0f;
        if (_overlay != null)
            _overlay.Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        _time -= (float)delta;
        if (_time <= 0.0f)
        {
            _overlay.Visible = false;
            SetProcess(false);
            return;
        }
        _phase += (float)delta;
        _overlay.Modulate = _color * (0.68f + 0.32f * Mathf.Sin(_phase * 7.5f));
        Sync();
    }

    /// <summary>Copy the target sprite's frame/flip/offset/scale onto the overlay so the tint tracks the exact pose.</summary>
    private void Sync()
    {
        _overlay.SpriteFrames = _target.SpriteFrames;
        _overlay.Animation = _target.Animation;
        _overlay.Frame = _target.Frame;
        _overlay.FlipH = _target.FlipH;
        _overlay.Centered = _target.Centered;
        _overlay.Offset = _target.Offset;
        _overlay.Scale = _target.Scale;
    }
}
