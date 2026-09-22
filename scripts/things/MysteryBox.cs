using Godot;

namespace MyGame;

/// <summary>
/// A MYSTERY BOX (placeholder art — a purple "?" crate): stand next to it and press <b>E</b> (the <c>interact</c>
/// action) to spend <see cref="Cost"/> fada_figs for a gamble. It is DELIBERATELY stingy: <see cref="DudChanceBase"/>
/// of pulls give nothing; on a WIN it fires <see cref="won"/> and RunManager opens a pick-1-of-3 menu of POWERFUL
/// buffs (same UI as the milestone menu, above-rare tiers). Every win raises the dud chance further (capped by
/// <see cref="DudChanceCap"/>), so repeat wins get rarer within a run. One box spawns per arena
/// (RunManager.BuildArena) — its escalation is that box's. Built entirely in code.
/// </summary>
public partial class MysteryBox : Node2D
{
    /// <summary>A pull beat the dud roll — RunManager opens the 3-choice powerful buff menu.</summary>
    [Signal] public delegate void wonEventHandler();

    private const string InteractAction = "interact"; // E (registered in _Ready if the project hasn't)
    private const int Cost = 25;                 // fada_figs spent per pull
    private const float DudChanceBase = 0.2f;    // chance a pull gives nothing (tune here)
    private const float DudChanceGrowth = 0.01f; // + per win, so wins get rarer
    private const float DudChanceCap = 0.995f;

    private float _dudChance = DudChanceBase;
    private Player _inRange;   // the player while standing in the box's interact range (null otherwise)
    private Node2D _visual;
    private Label _prompt;

    public override void _Ready()
    {
        EnsureInteractAction();

        _visual = new Node2D();
        AddChild(_visual);
        var crate = new Polygon2D
        {
            Polygon = new Vector2[] { new(-16, -34), new(16, -34), new(16, -2), new(-16, -2) },
            Color = new Color(0.42f, 0.28f, 0.62f),
        };
        _visual.AddChild(crate);
        var q = new Label { Text = "?", Position = new Vector2(-9, -34) };
        q.AddThemeFontSizeOverride("font_size", 30);
        q.AddThemeColorOverride("font_color", new Color(1.6f, 1.35f, 0.35f)); // HDR gold so it glints
        q.AddThemeColorOverride("font_outline_color", Colors.Black);
        q.AddThemeConstantOverride("outline_size", 4);
        _visual.AddChild(q);

        _prompt = new Label { Text = "E", Position = new Vector2(-6, -60), Visible = false };
        _prompt.AddThemeFontSizeOverride("font_size", 16);
        _prompt.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        _prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _prompt.AddThemeConstantOverride("outline_size", 4);
        AddChild(_prompt);

        var range = new Area2D { CollisionLayer = 0, CollisionMask = (uint)Combat.Layer.PlayerBody };
        range.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(64, 56) }, // a little reach around the crate
            Position = new Vector2(0, -20),
        });
        range.BodyEntered += OnBodyEntered;
        range.BodyExited += OnBodyExited;
        AddChild(range);
    }

    /// <summary>Register the <c>interact</c> action (physical E) if the project doesn't already define it — so the box
    /// works without a project.godot edit. Physical keycode = layout-independent, matching the other actions.</summary>
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
            Pull(_inRange);
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnBodyEntered(Node body)
    {
        if (body is Player p)
        {
            _inRange = p;
            _prompt.Visible = true;
        }
    }

    private void OnBodyExited(Node body)
    {
        if (body == _inRange)
        {
            _inRange = null;
            _prompt.Visible = false;
        }
    }

    private void Pull(Player p)
    {
        var sfx = GetNodeOrNull<Sfx>("/root/Sfx");
        if (!p.spend_fada_figs(Cost))
        {
            FloatingText.Emit(FloatingTextType.Damage, this, new Vector2(0, -40), $"NEED {Cost}", 0.0f, new Color(0.72f, 0.72f, 0.78f));
            return;
        }
        Pop();
        if (GD.Randf() < _dudChance)
        {
            FloatingText.Emit(FloatingTextType.Damage, this, new Vector2(0, -40), "…nothing", 0.0f, new Color(0.6f, 0.6f, 0.66f));
            sfx?.play_at("fada_fig_collect", GlobalPosition); // PLACEHOLDER sfx
            return;
        }
        // Win: hand off to RunManager to open the 3-choice POWERFUL buff menu.
        sfx?.play_at("fada_fig_collect", GlobalPosition); // PLACEHOLDER sfx
        _dudChance = Mathf.Min(DudChanceCap, _dudChance + DudChanceGrowth); // each win makes the next pull rarer
        EmitSignal(SignalName.won);
    }

    private void Pop()
    {
        _visual.Scale = new Vector2(1.25f, 1.25f);
        CreateTween().TweenProperty(_visual, "scale", Vector2.One, 0.22f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }
}
