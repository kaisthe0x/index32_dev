using Godot;
using System.Linq;

namespace MyGame;

/// <summary>
/// Sleeper archetype: dozes in place (idle only, no patrol) until the player enters <see cref="RageZone"/>,
/// then RAGES — a ground AoE erupts on the attack's hit frame and the attack loops; keeps raging for
/// <see cref="RageLinger"/> after the player leaves. A MELEE hit STUNS it (ranged only chips — see Hit.ranged).
/// Nasen is one instance (an EnemyKits entry). C# port of <c>scripts/enemies/nasen.gd</c>, reframed as a type.
/// </summary>
[GlobalClass]
public partial class SleeperEnemy : Enemy
{
    [ExportGroup("Sleeper")]
    [Export] public float RageZone { get; set; } = 100.0f;
    [Export] public float RageLinger { get; set; } = 2.0f;
    [Export] public float RageStunTime { get; set; } = 1.5f;
    [Export] public float RageDamage { get; set; } = 14.0f;
    [Export] public float RageKnockback { get; set; } = 130.0f;
    [Export] public Vector2 RageExtents { get; set; } = new(52, 22);

    private static readonly string RageKey = StrikeType.Aoe.Key();   // names its animation, effect row and sound cue
    private static readonly StringName RageAnim = "attack_" + RageKey;

    private float _rageLeft;
    private bool _eruptedThisYell;   // the AoE has gone off in the current pass of the rage animation

    /// <summary>Stationary sleeper AI — no patrol. Wake + rage while the player is in the zone (linger after).</summary>
    protected override void Act(float delta)
    {
        Velocity = new Vector2(0.0f, Velocity.Y); // rooted -- only ever sleeps or rages in place
        _rageLeft = Mathf.Max(_rageLeft - delta, 0.0f);

        var player = Player();
        if (player != null)
        {
            Vector2 to = player.GlobalPosition - GlobalPosition;
            if (Mathf.Abs(to.Y) <= AttackAlignY && Mathf.Abs(to.X) <= RageZone)
            {
                _rageLeft = RageLinger; // disturbed -> (re)fill the linger timer
                if (to.X != 0.0f)
                    Face(Mathf.Sign(to.X));
            }
        }
        if (_rageLeft > 0.0f && State == EState.Idle)
        {
            Engaged = true;
            StartRage();
        }
    }

    /// <summary>Play (a cycle of) the rage attack. <paramref name="fromFrame"/> = 0 plays the wake; a replay skips it.</summary>
    private void StartRage(int fromFrame = 0)
    {
        SetState(EState.Rage);
        if (fromFrame == 0)
            Sounds.PlayStart(RageKey); // the wake/attack cue -- once per rage, not every yell loop
        _eruptedThisYell = false;
        Impacted = false;
        ReplayFrom(RageAnim, fromFrame);
    }

    protected override void OnFrameChanged()
    {
        Sounds.PlayFrame(Sprite.Animation, Sprite.Frame);
        if (State == EState.Rage && !_eruptedThisYell && HitFramesOf(RageAnim).Contains(Sprite.Frame))
        {
            _eruptedThisYell = true;
            SpawnRageAoe();
            BeginHitstop();
        }
    }

    protected override void OnAnimFinished()
    {
        if (State == EState.Dead)
        {
            QueueFree();
            return;
        }
        if (State == EState.Rage)
        {
            if (_rageLeft > 0.0f)
                StartRage(LoopFrom(RageAnim)); // keep raging -- loop from loop_from (wake plays once)
            else
            {
                Engaged = false;
                SetState(EState.Idle); // doze off
            }
        }
    }

    /// <summary>Melee halts him (stun -> rage restarts after); a projectile only chips, so ranged is the safe approach.</summary>
    protected override void OnHurt(Hit hit)
    {
        if (State == EState.Dead)
            return;
        Health = Mathf.Max(Health - hit.Amount, 0.0f);
        Bar.SetRatio(Health / MaxHealth);
        Flash(Sprite);
        if (Health <= 0.0f)
        {
            Die();
            return;
        }
        if (!hit.Ranged)
        {
            StunLeft = RageStunTime;
            SetState(EState.Stun);
            CancelChannel();
        }
    }

    /// <summary>The rage AoE: the `aoe` Strike scene centred on us, our rage numbers injected.</summary>
    private void SpawnRageAoe()
    {
        var node = SpawnAttack(EffectScene(RageKey),
            new SegmentData { Damage = RageDamage, Knockback = RageKnockback }, false, EffectPos(RageKey));
        if (node != null)
            GroundContour.Conform(node, GetWorld2D()?.DirectSpaceState); // ground-band flames hug the slope, like the slam
    }
}
