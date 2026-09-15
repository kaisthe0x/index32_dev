using Godot;

namespace MyGame;

/// <summary>
/// SLOT-HEALTH lifesteal: each hit the player lands has a per-tier CHANCE to restore half a block, via
/// <see cref="Passive.OnHitDealt"/> — the tiered, data-driven sustain buff (Bloodrush, Skim, …). A fraction-of-damage
/// heal would trivially refill the 6-half-block bar, so the per-tier array is read as a PROBABILITY per hit instead.
/// Built by <see cref="BuffCatalog"/>.
/// </summary>
public partial class LifestealBuff : Buff
{
    private const float HealHalfBlock = 1.0f;   // one proc = half a block
    private readonly float[] _chance;           // per-tier chance-per-hit, indexed Common..Epic

    public LifestealBuff(string id, float[] chance)
    {
        Id = id;
        _chance = chance;
    }

    public override void OnHitDealt(Player player, float amount, Node target)
    {
        if (amount > 0.0f && GD.Randf() < _chance[Mathf.Clamp((int)Tier, 0, _chance.Length - 1)])
            player.heal(HealHalfBlock);
    }
}
