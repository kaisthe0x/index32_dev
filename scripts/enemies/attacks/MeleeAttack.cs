using Godot;
using System.Linq;

namespace MyGame;

/// <summary>
/// A close-range attack that lands on the animation's hit frames: a slash, a ground shockwave, a held blast, a lunge.
/// Each hit frame spawns the attack's effect scene — the row named after the type in <see cref="EmittersEnemies"/>, or
/// <c>&lt;type&gt;_&lt;sheet frame&gt;</c> when a combo has a scene per hit (Breski) — armed with these numbers. With
/// no scene at all it falls back to a small bare hitbox in front of the enemy (Kebus's point-blank jab). When there is a scene, <see cref="EnemyAttack.Range"/> is taken from how far its
/// hitbox reaches, so the enemy starts the swing exactly where it can connect.
/// </summary>
public sealed class MeleeAttack : EnemyAttack
{
    public MeleeAttack(StrikeType type = StrikeType.Melee) : base(type)
    {
        Range = 30.0f;
        Damage = 12.0f;
        Knockback = 90.0f;
    }

    /// <summary>&gt; 0 = the hit is a GUST: no damage, it flings the target at this speed (the Ventilator).</summary>
    public float Gust { get; init; }
    /// <summary>Forward impulse on the hit frame (0 = none): the enemy slides into the swing instead of freezing on it.</summary>
    public float Lunge { get; init; }
    /// <summary>Keep attacking, without a cooldown, while the target stays in reach (Matat).</summary>
    public bool Loops { get; init; }
    /// <summary>The effect is a ground band that should hug the terrain's surface where it spawns.</summary>
    public bool ConformGround { get; init; }

    // The fallback hitbox, for an attack with no effect scene.
    private const float BareHitboxX = 20.0f;                              // its centre, ahead of the enemy
    private static readonly Vector2 BareHitboxSize = new(32, 32);
    private const float BareHitboxLifetime = 0.15f;

    private IReadOnlyList<int> _hitFrames = System.Array.Empty<int>();
    private bool _channels;

    public override bool Repeats => Loops;
    public override bool Channels => _channels;

    protected override void Setup()
    {
        _hitFrames = Owner.HitFramesOf(Animation);
        float reach = SceneReach();
        if (reach > 0.0f)
            Range = reach;
    }

    public override void OnFrame(int frame)
    {
        if (!_hitFrames.Contains(frame))
            return;
        Strike(EffectKey(frame));
        if (Lunge > 0.0f)
            // Lunge forward on the commit frame; the enemy slides to a stop while it attacks. No hitstop — it would
            // zero the velocity and freeze the slide.
            Owner.Lunge(Lunge);
        else
            Owner.BeginStrikeHitstop();
    }

    private void Strike(string effectKey)
    {
        var scene = Owner.EffectScene(effectKey);
        if (scene == null)
        {
            StrikeWithBareHitbox();
            return;
        }
        var node = Owner.SpawnAttack(scene, new SegmentData
        {
            Damage = Damage,
            Knockback = Knockback,
            Gust = Gust,
            Stun = Stun,
        }, false, Owner.EffectPos(effectKey));
        if (ConformGround && node != null)
            GroundContour.Conform(node, Owner.GetWorld2D()?.DirectSpaceState);
    }

    /// <summary>A visual-less hitbox for an attack with no authored scene, freed after <see cref="BareHitboxLifetime"/>.</summary>
    private void StrikeWithBareHitbox()
    {
        bool hostile = !Owner.IsFrenemy();
        var hb = new Hitbox
        {
            CollisionLayer = Combat.HitLayer(hostile),
            CollisionMask = Combat.HurtMask(hostile, Owner.FriendlyFire),
            Damage = Damage,
            Knockback = Knockback,
            Stun = Stun,
            Source = Owner,
        };
        hb.AddChild(Enemy.MakeBox(BareHitboxSize, new Vector2(BareHitboxX * Owner.Facing, -Owner.HurtboxSize.Y / 2.0f)));
        Owner.AddChild(hb);
        hb.Activate();
        Owner.GetTree().CreateTimer(BareHitboxLifetime).Timeout += hb.QueueFree;
    }

    /// <summary>The effect row for the hit on <paramref name="emittedFrame"/>: the per-hit row if there is one, else the type's.</summary>
    private string EffectKey(int emittedFrame)
    {
        string framed = $"{Key}_{emittedFrame + Owner.SheetStart(Animation)}";
        return Owner.EffectScene(framed) != null ? framed : Key;
    }

    /// <summary>How far ahead the first hit's effect scene reaches (0 if it has no scene or no box) — measured by
    /// instancing it once. Also notes whether the scene is a held blast.</summary>
    private float SceneReach()
    {
        string key = _hitFrames.Count > 0 ? EffectKey(_hitFrames[0]) : Key;
        var scene = Owner.EffectScene(key);
        if (scene == null)
            return 0.0f;
        var inst = scene.Instantiate();
        _channels = inst is BlastStrike;
        float far = 0.0f;
        foreach (var node in inst.FindChildren("*", "CollisionShape2D", true, false))
            if (node is CollisionShape2D cs && cs.Shape is RectangleShape2D rect)
                far = Mathf.Max(far, cs.Position.X + rect.Size.X * 0.5f);
        inst.Free();
        if (far <= 0.0f)
            return 0.0f;
        return far + (Owner.Effect(key)?.Pos.X ?? 0.0f);
    }
}
