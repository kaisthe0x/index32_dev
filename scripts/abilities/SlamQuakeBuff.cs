using Godot;

namespace MyGame;

/// <summary>
/// Slam Quake: on a slam landing, stun every enemy within <see cref="QuakeRadius"/> for a set duration
/// (<c>Player.StunNearby</c>, the surge's stun-sweep pattern). Radius is a fixed tunable. Built by <see cref="BuffCatalog"/>.
/// </summary>
public partial class SlamQuakeBuff : Buff
{
    private const float QuakeRadius = 140.0f;  // stun reach around the slam point (tunable)

    private readonly float _secs;  // stun seconds

    public SlamQuakeBuff(string id, float secs)
    {
        Id = id;
        Trigger = Trigger.OnSlamLand;
        _secs = secs;
    }

    public override void OnSlamLand(Player p, float fallDistance, float fallSpeed) =>
        p.StunNearby(QuakeRadius, _secs);
}
