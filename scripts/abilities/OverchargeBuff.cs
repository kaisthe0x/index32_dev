using Godot;

namespace MyGame;

/// <summary>
/// Bakshen Overcharge: each hit the Bakshen SPECIAL lands shaves a chunk off the special cooldown, so rapid Bakshen chaining ramps damage. Offer-gated to the Bakshen special
/// (<see cref="Buff.AppliesTo"/>); only special hits feed it (an attack's hit doesn't). Built by <see cref="BuffCatalog"/>.
/// </summary>
public partial class OverchargeBuff : Buff
{
    private readonly float _secs;  // cooldown reduction per hit, seconds

    public OverchargeBuff(string id, float secs)
    {
        Id = id;
        Trigger = Trigger.OnHitDealt;
        _secs = secs;
    }

    public override void OnHitDealt(Player p, float amount, Node target)
    {
        if (target is Enemy e && e.LastHitFromSpecial)
            p.ReduceSpecialCooldown(_secs);
    }
}
