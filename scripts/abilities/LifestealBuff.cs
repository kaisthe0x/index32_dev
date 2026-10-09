using Godot;

namespace MyGame;

/// <summary>
/// SLOT-HEALTH lifesteal: each hit the player lands has a CHANCE to restore half a block, via
/// <see cref="Passive.OnHitDealt"/> — the sustain buff (Bloodrush, Skim, …). A fraction-of-damage heal would trivially
/// refill the 6-half-block bar, so the value is a PROBABILITY per hit instead.
/// Built by <see cref="BuffCatalog"/>.
/// </summary>
public partial class LifestealBuff : Buff
{
    private const float HealHalfBlock = 1.0f;   // one proc = half a block
    private readonly float _chance;             // chance per hit

    public LifestealBuff(string id, float chance)
    {
        Id = id;
        _chance = chance;
    }

    public override void OnHitDealt(Player player, float amount, Node target)
    {
        if (amount > 0.0f && GD.Randf() < _chance)
            player.Heal(HealHalfBlock);
    }
}
