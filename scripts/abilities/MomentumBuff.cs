using Godot;

namespace MyGame;

/// <summary>
/// Momentum: a consecutive-hit damage ramp — each landed hit multiplies the equipped ATTACK's damage by a
/// factor (stacking, capped by <see cref="MaxStacks"/>), and the ramp RESETS when a full swing/combo recovers to
/// neutral having connected nothing. Self-contained: it reads the existing <see cref="OnHitDealt"/> and the
/// <see cref="OnAnimEnd"/> hook (fired only when a swing/combo ends without chaining), so no Player API is needed.
///
/// <para>Reset semantics use per-swing state rather than the raw per-hitbox OnMiss (a multi-hit attack fires one box
/// per hit-frame): <see cref="_hitThisSwing"/> is set by any OnHitDealt and cleared at each OnAnimEnd; a swing that
/// ends with it still false is a genuine whiff → the ramp drops to 1.0. Consecutive connecting swings keep the ramp.</para>
///
/// <para><see cref="MaxStacks"/> is a placeholder cap so the ramp can't compound unbounded — tune during playtest. Built by <see cref="BuffCatalog"/>.</para>
/// </summary>
public partial class MomentumBuff : Buff
{
    /// <summary>How many consecutive hits the ramp counts before it stops growing (placeholder — tune at playtest).</summary>
    private const int MaxStacks = 5;

    private readonly float _factor;  // per-hit multiplier
    private int _stacks = 0;
    private bool _hitThisSwing = false;

    public MomentumBuff(string id, float factor)
    {
        Id = id;
        Trigger = Trigger.OnHitDealt;
        _factor = factor;
    }

    public override void OnHitDealt(Player player, float amount, Node target)
    {
        if (_stacks < MaxStacks)
            _stacks++;
        _hitThisSwing = true;
    }

    public override void OnAnimEnd(Player player)
    {
        if (!_hitThisSwing)
            _stacks = 0;   // a full swing/combo whiffed → the ramp is spent
        _hitThisSwing = false;
    }

    public override SegmentData ModifyTuning(Player p, Action action, int seg, SegmentData t)
    {
        if (_stacks > 0 && AppliesToAction(action) && t.Damage.HasValue)
            t.Damage = t.Damage.Value * Mathf.Pow(_factor, _stacks);
        return t;
    }
}
