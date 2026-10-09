using Godot;

namespace MyGame;

/// <summary>
/// One attack an <see cref="Enemy"/> can make: its numbers, and what happens on the frames of its animation. A kit
/// gives an enemy up to two — <see cref="Enemy.Close"/> and <see cref="Enemy.Far"/> — and the enemy decides WHEN to
/// start one (target lined up, within <see cref="Range"/>, off cooldown); the attack decides WHAT it does.
///
/// <para>Its <see cref="Type"/> names everything presentation looks up: the animation <c>attack_&lt;key&gt;</c>, the
/// emitter row(s) in <see cref="EmittersEnemies"/> and the sound cue <c>&lt;enemy&gt;.&lt;key&gt;</c>. Each enemy
/// gets its own instance (the kit builds it), so an attack may keep state between frames.</para>
/// </summary>
public abstract class EnemyAttack
{
    protected EnemyAttack(StrikeType type)
    {
        Type = type;
        Key = type.Key();
        Animation = "attack_" + Key;
    }

    public StrikeType Type { get; }
    /// <summary>The type's table key ("melee", "projectile", …).</summary>
    public string Key { get; }
    public StringName Animation { get; }

    /// <summary>How near (px, horizontally) the target has to be for the enemy to start this attack.</summary>
    public float Range { get; set; }
    public float Damage { get; init; }
    public float Knockback { get; init; }
    public float Stun { get; init; }

    /// <summary>Whether the enemy can make this attack at all (its sprite has the animation).</summary>
    public bool Usable { get; private set; }
    /// <summary>Replay the animation, from its loop point, for as long as the target stays in reach.</summary>
    public virtual bool Repeats => false;
    /// <summary>The attack is a sustained channel that a stagger can cut short — its sounds must be stoppable.</summary>
    public virtual bool Channels => false;

    /// <summary>The enemy making this attack (set once, by <see cref="Attach"/>).</summary>
    protected Enemy Owner { get; private set; } = null!;

    /// <summary>Bind the attack to its enemy. Called by the enemy once its sprite exists.</summary>
    public void Attach(Enemy owner)
    {
        Owner = owner;
        Usable = owner.HasAnimation(Animation);
        if (Usable)
            Setup();
    }

    /// <summary>Read what the attack needs from the enemy's sprite and effects (hit frames, reach).</summary>
    protected virtual void Setup() { }

    /// <summary>The attack's animation is starting (or looping).</summary>
    public virtual void Begin() { }

    /// <summary>A frame of the attack's animation is now showing.</summary>
    public abstract void OnFrame(int frame);
}
