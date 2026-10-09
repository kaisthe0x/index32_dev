using Godot;

namespace MyGame;

/// <summary>
/// Fires the type's projectile scene (a <see cref="Projectile"/>): aimed at the target, straight ahead, or rolling
/// along the ground — see <see cref="ShotPath"/>. An attack with no scene in <see cref="EmittersEnemies"/> fires nothing.
/// </summary>
public sealed class ShotAttack : RangedAttack
{
    private static readonly Vector2 AimAtBody = new(0, -15);   // aim at the target's body, not his feet
    private const float AimedLife = 3.0f;                      // seconds an aimed shot lives if it hits nothing

    public ShotAttack(StrikeType type = StrikeType.Projectile) : base(type) => Damage = 8.0f;

    public float Speed { get; init; } = 260.0f;
    public ShotPath Path { get; init; } = ShotPath.Aimed;
    /// <summary>How far a <see cref="ShotPath.Forward"/> or <see cref="ShotPath.GroundWave"/> shot travels (px). The
    /// attack's <see cref="EnemyAttack.Range"/> is capped to it: no point firing at a target it cannot reach.</summary>
    public float Travel { get; init; } = 100.0f;
    /// <summary><see cref="ShotPath.Aimed"/>: cap the shot's tilt to ± this many degrees off horizontal (0 = no cap),
    /// so a target far above or below never makes it near-vertical.</summary>
    public float AimCap { get; init; }

    protected override void Setup()
    {
        base.Setup();
        if (Path != ShotPath.Aimed)
            Range = Mathf.Min(Range, Travel);
    }

    protected override void Fire(Vector2 muzzle)
    {
        if (Owner.EffectScene(Key)?.Instantiate() is not Projectile proj)
            return;
        int facing = Owner.Facing;
        proj.Hostile = !Owner.IsFrenemy();
        proj.FriendlyFire = Owner.FriendlyFire;
        proj.Homing = 0.0f;
        proj.RotateToHeading = false;
        proj.Source = Owner;

        switch (Path)
        {
            case ShotPath.GroundWave:
                proj.Velocity = new Vector2(Speed * facing, 0.0f);
                proj.MaxRange = Travel;
                proj.GroundTrail = true;
                proj.GroundFollow = true;
                break;
            case ShotPath.Forward:
                proj.Velocity = new Vector2(Speed * facing, 0.0f);
                proj.MaxRange = Travel;
                break;
            default:
                var aim = Owner.Target();
                Vector2 target = aim != null ? aim.GlobalPosition + AimAtBody : muzzle + new Vector2(facing, 0);
                Vector2 to = target - muzzle;
                if (AimCap > 0.0f && to.LengthSquared() > 0.0001f)
                {
                    float cap = Mathf.DegToRad(AimCap);
                    float ang = Mathf.Clamp(Mathf.Atan2(to.Y, Mathf.Abs(to.X)), -cap, cap); // tilt off horizontal
                    float sign = Mathf.Abs(to.X) < 0.001f ? facing : Mathf.Sign(to.X);
                    to = new Vector2(sign * Mathf.Cos(ang), Mathf.Sin(ang));
                }
                proj.Velocity = to.Normalized() * Speed;
                proj.MaxLife = AimedLife;
                proj.CanFlyUp = true;        // aim up/down at an elevated target instead of being flattened to the floor
                proj.RotateToHeading = true; // point the bolt along its flight
                break;
        }

        Owner.GetParent().AddChild(proj);
        Nodes.PlaceAt(proj, muzzle);
        proj.ApplyTuning(new SegmentData
        {
            Damage = Damage,
            Knockback = Knockback,
            Stun = Stun,
        }, Owner);
    }
}
