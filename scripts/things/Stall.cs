using Godot;

namespace MyGame;

/// <summary>
/// Something in the arena Khalid stands next to and presses <b>E</b> (the <c>interact</c> action) to use — the mystery
/// box, Needle Point, and the stalls to come. Owns the shared part: an interact range around it, the floating "E"
/// prompt while he's in it, and the key press; a subclass builds its look (<see cref="BuildVisual"/>) and says what
/// using it does (<see cref="Interact"/>). A stall can be CLOSED (<see cref="SetOpen"/> — e.g. a shop outside the break):
/// it dims, the prompt reads "CLOSED", and E does nothing. Built entirely in code.
/// </summary>
public abstract partial class Stall : Node2D
{
    private const string InteractAction = "interact"; // E (registered in _Ready if the project hasn't)
    private static readonly Vector2 RangeSize = new(64, 56); // a little reach around the stall
    private static readonly Vector2 RangeOffset = new(0, -20);
    private static readonly Vector2 PromptOffset = new(-6, -60);
    private static readonly Vector2 ClosedPromptOffset = new(-26, -60);
    private static readonly Color ClosedTint = new(0.45f, 0.45f, 0.5f);

    private Player _inRange;   // the player while standing in range (null otherwise)
    private Label _prompt;
    private bool _open = true;

    /// <summary>The stall's look, drawn under this node (its base sits at the node's origin).</summary>
    protected Node2D Visual { get; private set; }

    public override void _Ready()
    {
        EnsureInteractAction();
        Visual = new Node2D();
        AddChild(Visual);
        BuildVisual(Visual);
        _prompt = new Label { Text = "E", Position = PromptOffset, Visible = false };
        _prompt.AddThemeFontSizeOverride("font_size", 16);
        _prompt.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        _prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _prompt.AddThemeConstantOverride("outline_size", 4);
        AddChild(_prompt);
        ApplyOpen();
        var range = new Area2D { CollisionLayer = 0, CollisionMask = (uint)Combat.Layer.PlayerBody };
        range.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = RangeSize }, Position = RangeOffset });
        range.BodyEntered += body =>
        {
            if (body is not Player p)
                return;
            _inRange = p;
            _prompt.Visible = true;
        };
        range.BodyExited += body =>
        {
            if (body != _inRange)
                return;
            _inRange = null;
            _prompt.Visible = false;
        };
        AddChild(range);
    }

    /// <summary>Open or close the stall (closed: dimmed, "CLOSED" prompt, E ignored).</summary>
    public void SetOpen(bool open)
    {
        _open = open;
        if (IsNodeReady())
            ApplyOpen();
    }

    private void ApplyOpen()
    {
        Visual.Modulate = _open ? Colors.White : ClosedTint;
        _prompt.Text = _open ? "E" : "CLOSED";
        _prompt.Position = _open ? PromptOffset : ClosedPromptOffset;
    }

    /// <summary>Build this stall's look under <paramref name="visual"/>.</summary>
    protected abstract void BuildVisual(Node2D visual);

    /// <summary>The player pressed E in range.</summary>
    protected abstract void Interact(Player p);

    /// <summary>Register the <c>interact</c> action (physical E) if the project doesn't already define it — so stalls
    /// work without a project.godot edit. Physical keycode = layout-independent, matching the other actions.</summary>
    private static void EnsureInteractAction()
    {
        if (InputMap.HasAction(InteractAction))
            return;
        InputMap.AddAction(InteractAction);
        InputMap.ActionAddEvent(InteractAction, new InputEventKey { PhysicalKeycode = Key.E });
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_open && _inRange != null && @event.IsActionPressed(InteractAction))
        {
            Interact(_inRange);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>A little bounce of the look — feedback that the press registered.</summary>
    protected void Pop()
    {
        Visual.Scale = new Vector2(1.25f, 1.25f);
        CreateTween().TweenProperty(Visual, "scale", Vector2.One, 0.22f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }
}
