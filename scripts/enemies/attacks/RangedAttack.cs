using Godot;

namespace MyGame;

/// <summary>
/// A far attack that releases ONE thing per animation — a bolt, a bomb — on its fire frame (the animation's first
/// authored hit frame, or its middle if none is authored), from the muzzle the type's emitter row places.
/// </summary>
public abstract class RangedAttack : EnemyAttack
{
    private static readonly Vector2 DefaultMuzzle = new(20, -46);

    private int _fireFrame;
    private bool _fired;

    protected RangedAttack(StrikeType type) : base(type) => Range = 300.0f;

    protected override void Setup()
    {
        var hits = Owner.HitFramesOf(Animation);
        _fireFrame = hits.Count > 0 ? hits[0] : Mathf.Max(1, Owner.FrameCount(Animation) / 2);
    }

    public override void Begin() => _fired = false;

    public override void OnFrame(int frame)
    {
        if (_fired || frame < _fireFrame)
            return;
        _fired = true;
        Fire(Owner.GlobalPosition + Owner.EffectPos(Key, DefaultMuzzle));
        Owner.BeginHitstop();
    }

    /// <summary>Release the shot from <paramref name="muzzle"/> (world position).</summary>
    protected abstract void Fire(Vector2 muzzle);
}
