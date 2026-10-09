using Godot;

namespace MyGame;

/// <summary>
/// Throws a bomb in an arc (a <see cref="LobProjectile"/>): it lands beside the target, sits for a beat, then bursts —
/// only the burst hurts, so it can be dodged. Two emitter rows dress it: the type's own (the bomb in flight) and
/// <c>&lt;type&gt;_burst</c> (the explosion), with the sound cue <c>&lt;enemy&gt;.&lt;type&gt;_burst</c>.
/// </summary>
public sealed class LobAttack : RangedAttack
{
    private static readonly Vector2 BlindThrow = new(90, 30);   // where it lands, ahead of the enemy, with no target

    public LobAttack(StrikeType type = StrikeType.DelayedProjectile) : base(type) => Damage = 8.0f;

    public float ArcTime { get; init; } = 0.9f;
    public float Gravity { get; init; } = 900.0f;
    /// <summary>Seconds the bomb sits where it landed before it bursts.</summary>
    public float Dwell { get; init; } = 1.0f;
    public float MaxLife { get; init; } = 3.0f;
    public Vector2 ExplosionExtents { get; init; } = new(48, 26);
    /// <summary>It lands this far (px) from the target, on the enemy's side of him.</summary>
    public float LandOffset { get; init; } = 22.0f;

    protected override void Fire(Vector2 muzzle)
    {
        string burst = Key + "_burst";
        var lob = new LobProjectile
        {
            Hostile = !Owner.IsFrenemy(),
            FriendlyFire = Owner.FriendlyFire,
            Source = Owner,
            ArcTime = ArcTime,
            Gravity = Gravity,
            DwellTime = Dwell,
            MaxLife = MaxLife,
            ExplosionExtents = ExplosionExtents,
            ExplosionDamage = Damage,
            ExplosionKnockback = Knockback,
            ExplosionStun = Stun,
            ExplosionEffect = Owner.EffectScene(burst),
            ExplosionSfx = $"{Owner.EnemyId}.{burst}",
            ExplosionEffectPos = Owner.Effect(burst)?.Pos ?? Vector2.Zero,
        };
        var visual = Owner.EffectScene(Key);
        if (visual != null)
            lob.AddChild(visual.Instantiate());

        var aim = Owner.Target();
        Vector2 land = muzzle + new Vector2(Owner.Facing * BlindThrow.X, BlindThrow.Y);
        if (aim != null)
        {
            float side = -Mathf.Sign(aim.GlobalPosition.X - Owner.GlobalPosition.X);
            land = aim.GlobalPosition + new Vector2(side * LandOffset, 0.0f);
        }
        lob.Target = land;

        Owner.GetParent().AddChild(lob);
        Nodes.PlaceAt(lob, muzzle);
    }
}
