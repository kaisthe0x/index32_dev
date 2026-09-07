using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// Sleeper archetype: dozes in place (idle only, no patrol) until the player enters <see cref="rage_zone"/>,
/// then RAGES — a ground AoE erupts on the attack's hit frame and the attack loops; keeps raging for
/// <see cref="rage_linger"/> after the player leaves. A MELEE hit STUNS it (ranged only chips — see Hit.ranged).
/// Nasen is one instance (an EnemyKits entry). C# port of <c>scripts/enemies/nasen.gd</c>, reframed as a type.
/// </summary>
[GlobalClass]
public partial class SleeperEnemy : Enemy
{
    [ExportGroup("Sleeper")]
    [Export] public float rage_zone { get; set; } = 100.0f;
    [Export] public float rage_linger { get; set; } = 2.0f;
    [Export] public float rage_stun_time { get; set; } = 1.5f;
    [Export] public float rage_damage { get; set; } = 14.0f;
    [Export] public float rage_knockback { get; set; } = 130.0f;
    [Export] public Vector2 rage_extents { get; set; } = new(52, 22);

    private float _rageLeft;

    /// <summary>Stationary sleeper AI — no patrol. Wake + rage while the player is in the zone (linger after).</summary>
    protected override void Act(float delta)
    {
        Velocity = new Vector2(0.0f, Velocity.Y); // rooted -- only ever sleeps or rages in place
        _rageLeft = Mathf.Max(_rageLeft - delta, 0.0f);

        var player = Player();
        if (player != null)
        {
            Vector2 to = player.GlobalPosition - GlobalPosition;
            if (Mathf.Abs(to.Y) <= attack_align_y && Mathf.Abs(to.X) <= rage_zone)
            {
                _rageLeft = rage_linger; // disturbed -> (re)fill the linger timer
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
            PlayAttackStartSfx(CloseAnim); // the wake/attack cue -- once per rage, not every yell loop
        AttackFired = false;
        Impacted = false;
        ReplayFrom(CloseAnim, fromFrame);
    }

    protected override void OnFrameChanged()
    {
        PlayFrameSfx();
        if (State == EState.Rage && !AttackFired && HitFramesOf(CloseAnim).Contains(Sprite.Frame))
        {
            AttackFired = true;
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
                StartRage(LoopFrom(CloseAnim)); // keep raging -- loop from loop_from (wake plays once)
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
        Health = Mathf.Max(Health - hit.amount, 0.0f);
        Bar.SetRatio(Health / max_health);
        Flash(Sprite);
        if (Health <= 0.0f)
        {
            Die();
            return;
        }
        if (!hit.ranged)
        {
            StunLeft = rage_stun_time;
            SetState(EState.Stun);
            CancelChannel();
        }
    }

    /// <summary>The rage AoE: the `aoe` Strike scene centred on us, our rage numbers injected.</summary>
    private void SpawnRageAoe()
    {
        var node = SpawnAttack(VfxScene("aoe"),
            new SegmentData { Damage = rage_damage, Knockback = rage_knockback }, false, VfxPos("aoe"));
        if (conform_ground && node != null)
            GroundContour.Conform(node, GetWorld2D()?.DirectSpaceState); // ground-band flames hug the slope, like the slam

    }
}
