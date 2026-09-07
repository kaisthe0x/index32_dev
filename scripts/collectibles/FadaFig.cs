using Godot;

namespace MyGame;

/// <summary>
/// A collectible Fada Fig dropped by a dying enemy — the run currency (spent later at the Chest). It pops out of the
/// corpse with a little bounce + tumble (RigidBody physics) and settles on the ground; the player collects it by
/// physically touching it — the child <c>Pickup</c> Area detects the player's body. There is intentionally no
/// wide magnet: a FUTURE reward calls <see cref="magnetize"/> to make loose Fada Figs fly to the player like a Ruh soul.
/// RunManager spawns this scene on enemy death (count = <c>Enemy.fada_fig_drop</c>).
/// </summary>
public partial class FadaFig : RigidBody2D
{
    [Export] public float pop_up_min { get; set; } = 120.0f;
    [Export] public float pop_up_max { get; set; } = 200.0f;
    [Export] public float pop_side { get; set; } = 85.0f;
    [Export] public float life_seconds { get; set; } = 30.0f;   // despawn if never collected (avoids clutter)
    [Export] public float magnet_speed { get; set; } = 540.0f;  // used only once magnetized

    private Node2D _magnetTarget;
    private bool _collected;

    // One shared pulse-glow material for every Fada Fig (a scene-wide resource, not per-instance).
    private static ShaderMaterial _glowMaterial;

    public override void _Ready()
    {
        // Scatter pop: up + a little sideways, with a spin so it tumbles/rolls before settling.
        LinearVelocity = new Vector2(
            (float)GD.RandRange(-pop_side, pop_side),
            -(float)GD.RandRange(pop_up_min, pop_up_max));
        AngularVelocity = (float)GD.RandRange(-8.0, 8.0);

        _glowMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://vfx/shaders/world/pulse_glow.gdshader") };
        GetNode<Sprite2D>("Sprite2D").Material = _glowMaterial;

        GetNode<Area2D>("Pickup").BodyEntered += OnBodyEntered;

        if (life_seconds > 0.0f)
            GetTree().CreateTimer(life_seconds).Timeout += () => { if (!_collected) QueueFree(); };
    }

    /// <summary>FUTURE magnet-reward hook: pull this Fada Fig toward <paramref name="target"/> (the player) instead of
    /// resting on the ground — it then flies in and is collected on contact, exactly like a Ruh soul.</summary>
    public void magnetize(Node2D target)
    {
        _magnetTarget = target;
        GravityScale = 0.0f;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_collected && _magnetTarget != null && IsInstanceValid(_magnetTarget))
            LinearVelocity = (_magnetTarget.GlobalPosition - GlobalPosition).Normalized() * magnet_speed;
    }

    private void OnBodyEntered(Node body)
    {
        if (_collected || body is not Player p)
            return;
        _collected = true;
        p.collect_fada_fig(1);
        GetNodeOrNull<Sfx>("/root/Sfx")?.play_at("fada_fig_collect", GlobalPosition);
        QueueFree();
    }
}
