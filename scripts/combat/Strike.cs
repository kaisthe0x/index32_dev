using Godot;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// Base for a non-projectile attack that plants a hitbox at/near the body — the shared component the typed
/// strikes (<see cref="MeleeStrike"/> / <see cref="AoeStrike"/> / <see cref="BlastStrike"/> /
/// <see cref="TimedAoeStrike"/>) specialise. The Strike counterpart to <c>Projectile</c> (which leaves the
/// body). It carries a <see cref="Hitbox"/> + a drawn/particle visual, mirrors with facing, and frees itself
/// when its visual is done. C# port + type-split of <c>scripts/combat/strike.gd</c>.
///
/// Combat NUMBERS (damage/knockback/stun/reach + lunge/super-armor/multi-hit) come from the Actions catalog
/// via the resolve seam and land through <see cref="apply_tuning"/> at spawn — NOT baked here. This owns the
/// LOOK + shared BEHAVIOR (grow/fade, multi-hit re-arm, DoT tick, lunge/armor callbacks).
///
/// NAMING: the surface GDScript still touches (<c>apply_tuning</c>, <c>cancel</c>, <c>source</c>, the
/// <c>[Export]</c>s) stays snake_case through the migration; internals are idiomatic. Concrete (not abstract)
/// so a code path can still build a bare one; author scenes against the typed subclasses.
/// </summary>
[GlobalClass]
public partial class Strike : Node2D, ITunable, ISidedAttack
{
    /// <summary>false = a player strike (hits enemies); true = an enemy strike (hits the player).</summary>
    [Export] public bool hostile { get; set; }
    /// <summary>When true this box also hits its OWN team (never its own <c>source</c>). See Combat.HurtMask.</summary>
    [Export] public bool friendly_fire { get; set; }

    [ExportGroup("Visual")]
    /// <summary>Seconds on screen before it frees itself; a particle strike also waits for its emitters to finish.</summary>
    [Export] public float lifetime { get; set; } = 0.4f;
    /// <summary>Drawn visuals pop from this scale multiple to their authored scale over the first bit. 1.0 = no grow.</summary>
    [Export] public float grow_from { get; set; } = 0.7f;
    /// <summary>DoT INTERVAL (s): while alive, re-hit everyone in the box every <c>tick</c>s. 0 = single hit on contact.</summary>
    [Export] public float tick { get; set; }

    /// <summary>Who struck (knockback credit + lunge/armor target); set by the spawner.</summary>
    public Node? source { get; set; }

    protected Hitbox? Box;
    private Godot.Timer? _tickTimer;
    private bool _fading;

    public override void _Ready()
    {
        Box = FindHitbox();
        if (Box != null)
        {
            Box.CollisionLayer = Combat.HitLayer(hostile);
            Box.CollisionMask = Combat.HurtMask(hostile, friendly_fire);
        }

        // Fire every emitter on spawn regardless of its serialized `emitting` flag (the editor flips a one_shot
        // emitter to false when you open the scene). restart() re-arms + emits either kind. FadeOut stops them.
        foreach (var em in Emitters())
            RestartEmitter(em);

        var vis = Visuals();
        if (vis.Count > 0)
        {
            var tw = CreateTween().SetParallel(true);
            foreach (var v in vis)
            {
                Vector2 target = v.Scale;
                v.Scale = target * grow_from;
                tw.TweenProperty(v, "scale", target, lifetime * 0.45f)
                    .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(v, "modulate:a", 0.0, lifetime).SetEase(Tween.EaseType.In);
            }
        }

        // End of the active window, THEN a graceful fade (FadeOut stops damage + emission and lingers only for
        // the live particles to finish, so a continuous emitter DISSIPATES instead of popping).
        GetTree().CreateTimer(FreeDelay()).Timeout += FadeOut;

        // Authored self-contained DoT (e.g. a vortex): re-hit every `tick`s for the strike's life.
        if (tick > 0.0f && Box != null)
            StartTicking(tick);
    }

    /// <summary>
    /// When the strike stops (schedules the fade). Base = the slash lifetime or the longest emitter's particle
    /// life. <see cref="BlastStrike"/> extends this with its continuous-emission window.
    /// </summary>
    protected virtual float FreeDelay()
    {
        float freeDelay = lifetime;
        foreach (var em in Emitters())
            freeDelay = Mathf.Max(freeDelay, EmitterLife(em));
        return freeDelay;
    }

    /// <summary>
    /// Configure from a resolved tuning dict (Actions catalog via the resolve seam, or an enemy's exports):
    /// set the hitbox numbers + reach, and trigger wielder-effects (lunge/super-armor) on <c>source</c>.
    /// Called by the spawner after add_child, before the hitbox is armed. Absent fields keep authored values.
    /// </summary>
    public void apply_tuning(SegmentData t, Node? striker)
    {
        if (striker != null)
            source = striker;
        Box ??= FindHitbox();
        if (Box != null)
        {
            if (t.Damage.HasValue) Box.damage = t.Damage.Value;
            if (t.Knockback.HasValue) Box.knockback = t.Knockback.Value;
            if (t.Gust.HasValue) Box.gust = t.Gust.Value;
            if (t.Stun.HasValue) Box.stun = t.Stun.Value;
            if (t.Color.HasValue) Box.status_color = t.Color.Value;
            if (t.Color.HasValue || t.Stun.HasValue)
                Box.status_time = t.ColorTime ?? t.Stun ?? 0.0f;
            if (t.VictimEffect != null)
            {
                Box.victim_vfx = GD.Load<PackedScene>(t.VictimEffect);
                Box.victim_vfx_time = t.VictimTime ?? 0.0f; // 0 -> defaults to the stun/status window
            }
            if (t.FromSpecial.HasValue) Box.from_special = t.FromSpecial.Value;
            if (t.Frenemy.HasValue) Box.frenemy_time = t.Frenemy.Value;
            if (t.Reap.HasValue)
            {
                Box.dot_percent = t.Reap.Value;
                Box.dot_time = t.ReapTime ?? 0.0f;
            }
            ResizeHitbox(t);
        }
        // Wielder-effects on the striker: lunge shoves forward, armor shrugs off stagger. A source that isn't an
        // IStrikeWielder (an enemy) gets neither.
        if (source is IStrikeWielder wielder)
        {
            float lunge = t.Lunge ?? 0.0f;
            if (lunge != 0.0f)
                wielder.ApplyLunge(lunge);
            float armor = t.SuperArmor ?? 0.0f;
            if (armor > 0.0f)
                wielder.SetArmor(armor);
        }
        int hits = t.MultiHit ?? 1;
        if (hits > 1 && Box != null)
            SetupMultiHit(hits);
        // Injected DoT from a move's tuning (only if the scene didn't already author one in _Ready).
        if (t.Tick is float tick && tick > 0.0f && Box != null && !IsInstanceValid(_tickTimer))
            StartTicking(tick);
        OnTuningApplied(t);
    }

    /// <summary>Subclass hook run at the end of <see cref="apply_tuning"/> (BlastStrike holds the caster's pose here).</summary>
    protected virtual void OnTuningApplied(SegmentData t) { }

    /// <summary>Hit `hits` times across the strike's life — a fixed COUNT of pulses (a buff). For a steady interval use <c>tick</c>.</summary>
    private void SetupMultiHit(int hits)
    {
        // Method group (PulseBox), NOT a capturing lambda: a lambda connected to a SceneTreeTimer can be GC'd
        // before it fires; a method group keeps `this` alive.
        float interval = lifetime / hits;
        for (int i = 1; i < hits; i++)
            GetTree().CreateTimer(interval * i).Timeout += PulseBox;
    }

    /// <summary>Re-pulse the hitbox every `interval`s for the strike's life — a DoT field. Freed with this node.</summary>
    private void StartTicking(float interval)
    {
        var timer = new Godot.Timer { WaitTime = interval };
        timer.Timeout += PulseBox;
        AddChild(timer);
        timer.Start();
        _tickTimer = timer;
    }

    private void PulseBox()
    {
        if (IsInstanceValid(Box)) Box!.pulse();
    }

    /// <summary>Stop this effect NOW — the caster's channel was interrupted. Same graceful teardown as end-of-life.</summary>
    public void cancel() => FadeOut();

    /// <summary>Stop damaging + emitting, then free once the live particles fade (DISSIPATE, not pop). Runs once.</summary>
    private void FadeOut()
    {
        if (_fading)
            return;
        _fading = true;
        if (IsInstanceValid(_tickTimer)) _tickTimer!.Stop();
        Box?.deactivate();
        float linger = 0.0f;
        foreach (var em in Emitters())
        {
            SetEmitting(em, false);
            linger = Mathf.Max(linger, EmitterLife(em));
        }
        if (linger <= 0.0f)
            QueueFree();
        else
            GetTree().CreateTimer(linger).Timeout += QueueFree;
    }

    /// <summary>Resize/reposition the hitbox from tuning `extents` (half-size) + `x` (forward reach, right-facing).</summary>
    private void ResizeHitbox(SegmentData t)
    {
        if (Box == null || !(t.Extents.HasValue || t.X.HasValue))
            return;
        foreach (var node in Box.FindChildren("*", "CollisionShape2D", true, false))
        {
            if (node is CollisionShape2D cs && cs.Shape is RectangleShape2D)
            {
                var rect = (RectangleShape2D)cs.Shape.Duplicate();
                cs.Shape = rect;
                if (t.Extents.HasValue) rect.Size = t.Extents.Value * 2.0f;
                if (t.X.HasValue)
                {
                    Vector2 p = cs.Position;
                    p.X = t.X.Value;
                    cs.Position = p;
                }
                return;
            }
        }
    }

    protected Hitbox? FindHitbox()
    {
        foreach (var a in FindChildren("*", "Area2D", true, false))
            if (a is Hitbox hb)
                return hb;
        return null;
    }

    private List<Node2D> Visuals()
    {
        var outList = new List<Node2D>();
        foreach (var n in FindChildren("*", "Sprite2D", true, false)) outList.Add((Node2D)n);
        foreach (var n in FindChildren("*", "AnimatedSprite2D", true, false)) outList.Add((Node2D)n);
        return outList;
    }

    protected List<Node> Emitters()
    {
        var outList = new List<Node>();
        foreach (var n in FindChildren("*", "CpuParticles2D", true, false)) outList.Add(n);
        foreach (var n in FindChildren("*", "GpuParticles2D", true, false)) outList.Add(n);
        return outList;
    }

    // Cpu/Gpu particles share property NAMES but not a C# base — small typed shims keep it strict.
    private static void RestartEmitter(Node em)
    {
        if (em is CpuParticles2D c) c.Restart();
        else if (em is GpuParticles2D g) g.Restart();
    }

    private static void SetEmitting(Node em, bool v)
    {
        if (em is CpuParticles2D c) c.Emitting = v;
        else if (em is GpuParticles2D g) g.Emitting = v;
    }

    protected static float EmitterLife(Node em) => em switch
    {
        CpuParticles2D c => (float)(c.Lifetime * (1.0 + c.LifetimeRandomness)), // lifetime_randomness is CPU-only
        GpuParticles2D g => (float)g.Lifetime,
        _ => 0.0f,
    };

    // --- tuning-dict helpers ---
}
