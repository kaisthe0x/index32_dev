using Godot;

namespace MyGame;

/// <summary>
/// Bakshen Overcharge: each hit the Bakshen SPECIAL lands shaves a per-tier chunk off the special cooldown (Epic = full
/// reset via a huge value), so rapid Bakshen chaining ramps damage. Offer-gated to the Bakshen special
/// (<see cref="Buff.AppliesTo"/>); only special hits feed it (an attack's hit doesn't). Built by <see cref="BuffCatalog"/>.
/// </summary>
public partial class OverchargeBuff : Buff
{
    private readonly float[] _secs;  // per-tier cooldown reduction seconds, indexed Common..Epic (Epic = huge = full)

    public OverchargeBuff(string id, float[] secs)
    {
        Id = id;
        Trigger = Trigger.OnHitDealt;
        _secs = secs;
    }

    public override void OnHitDealt(Player p, float amount, Node target)
    {
        if (target is Enemy e && e.last_hit_from_special)
            p.reduce_special_cooldown(_secs[Mathf.Clamp((int)Tier, 0, _secs.Length - 1)]);
    }
}
