using Godot;

namespace MyGame;

/// <summary>
/// One Lira coin — the common currency (docs/game-loop.md § Economy). It pops off a dying enemy and flies to Khalid on
/// the Ruh soul's curve (<see cref="ArcFlight"/>); it's banked on ARRIVAL, so the HUD counter ticks as each coin lands.
/// RunManager spawns <c>Enemy.lira_drop</c> of these per kill. Each coin's arc height + flight time are jittered so a
/// multi-coin drop fans out instead of flying as one stacked sprite.
/// </summary>
public partial class Lira : ArcFlight
{
    private const float ArcJitter = 0.35f;    // ± fraction of arc_height
    private const float FlightJitter = 0.15f; // ± fraction of flight_time

    // One shared pulse-glow material for every coin (like the Fada Fig's).
    private static ShaderMaterial _glowMaterial;

    public override void _Ready()
    {
        base._Ready();
        _glowMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://vfx/shaders/world/pulse_glow.gdshader") };
        GetNode<Sprite2D>("Sprite2D").Material = _glowMaterial;
    }

    /// <summary>Send this coin curving toward <paramref name="target"/> (the player).</summary>
    public void launch(Node2D target)
    {
        arc_height *= 1.0f + (float)GD.RandRange(-ArcJitter, ArcJitter);
        flight_time *= 1.0f + (float)GD.RandRange(-FlightJitter, FlightJitter);
        Fly(target);
    }

    protected override void OnArrived(Node2D target)
    {
        if (target is not Player p)
            return;
        p.collect_lira(1);
        GetNodeOrNull<Sfx>("/root/Sfx")?.play_at("lira_collect", GlobalPosition);
    }
}
