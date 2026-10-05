using Godot;

namespace MyGame;

/// <summary>
/// Something in the arena Khalid stands at and presses <b>E</b> (the <c>interact</c> action) to use — the mystery box,
/// Needle Point, Dekken. Each is a SCENE (<c>scenes/things/</c>) you place in a layout, built from these children:
/// <list type="bullet">
/// <item><c>Visual</c> (Node2D) — its look, bottom-centre on the scene's origin (the ground where it stands).</item>
/// <item><c>Interact</c> (Area2D + a shape) — where the player has to stand to use it.</item>
/// <item><c>Prompt</c> (Marker2D) — where the floating "E" prompt sits (its top-centre).</item>
/// </list>
/// This base owns the shared behaviour: the prompt while the player is in range and the key press. A subclass says what
/// using it does (<see cref="Interact"/>).
/// </summary>
public abstract partial class Stall : Node2D
{
    private const string InteractAction = "interact"; // E (registered in _Ready if the project hasn't)
    private const float PromptWidth = 80.0f;             // the prompt label is centred on the Prompt marker in this width

    private Player _inRange;   // the player while standing in range (null otherwise)
    private Label _prompt;

    /// <summary>The stall's look (the scene's <c>Visual</c> node).</summary>
    protected Node2D Visual { get; private set; }

    public override void _Ready()
    {
        ZIndex = WorldZ.Stalls; // behind the tiles, in front of the scenery (WorldZ)
        EnsureInteractAction();
        Visual = GetNode<Node2D>("Visual");

        _prompt = new Label
        {
            Text = "E",
            Position = GetNode<Marker2D>("Prompt").Position - new Vector2(PromptWidth / 2.0f, 0.0f),
            Size = new Vector2(PromptWidth, 0.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        _prompt.AddThemeFontSizeOverride("font_size", 16);
        _prompt.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        _prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _prompt.AddThemeConstantOverride("outline_size", 4);
        AddChild(_prompt);

        var range = GetNode<Area2D>("Interact");
        range.CollisionLayer = 0; // layers from Combat (not the scene), so they can't drift
        range.CollisionMask = (uint)Combat.Layer.PlayerBody;
        range.BodyEntered += body =>
        {
            if (body is not Player p)
                return;
            _inRange = p;
            _prompt.Visible = true;
            PlayerArrived();
        };
        range.BodyExited += body =>
        {
            if (body != _inRange)
                return;
            _inRange = null;
            _prompt.Visible = false;
        };
    }

    /// <summary>The player pressed E in range.</summary>
    protected abstract void Interact(Player p);

    /// <summary>The player just stepped into range (the prompt is up).</summary>
    protected virtual void PlayerArrived() { }

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
        if (_inRange != null && @event.IsActionPressed(InteractAction))
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
