using Godot;

namespace MyGame;

/// <summary>
/// Slam Spring: on a slam landing, prime the NEXT ground jump with a height multiplier (one-shot,
/// consumed on that jump — <c>Player.set_jump_spring</c>). Built by <see cref="BuffCatalog"/>.
/// </summary>
public partial class SlamSpringBuff : Buff
{
    private readonly float _mult;  // next-ground-jump height multiplier

    public SlamSpringBuff(string id, float mult)
    {
        Id = id;
        Trigger = Trigger.OnSlamLand;
        _mult = mult;
    }

    public override void OnSlamLand(Player p, float fallDistance, float fallSpeed) =>
        p.set_jump_spring(_mult);
}
