using Godot;

namespace MyGame;

/// <summary>
/// The per-hit hitbox tuning for one combo SEGMENT — the numbers that land on a <c>Hitbox</c> at spawn. Replaces
/// the old stringly-typed tuning <c>Dictionary</c>. Every field is nullable so "not set" is distinct from "set to
/// zero" (an unset field leaves the effect scene's own baked value alone — the exact semantics the old
/// <c>if (t.ContainsKey(key))</c> guards had). This is BOTH the authored catalog data AND the runtime-resolved
/// tuning: the resolve seam <see cref="Clone"/>s the authored segment, applies stat mults, and lets each buff
/// mutate the copy (see <c>Player.ResolveTuning</c> + <c>Passive.ModifyTuning</c>).
/// </summary>
public sealed class SegmentData
{
    public float? Damage;
    public float? Knockback;
    /// <summary>A GUST (px/s): blows the victim away from the source instead of hurting it — see <c>Hit.Gust</c>.</summary>
    public float? Gust;
    public float? Stun;
    /// <summary>Half-extents of the hitbox rect (overrides the scene's).</summary>
    public Vector2? Extents;
    /// <summary>Forward x-offset of the hitbox.</summary>
    public float? X;
    /// <summary>Status tint applied to the victim.</summary>
    public Color? Color;
    public float? ColorTime;
    /// <summary>Scene path of a VFX stamped on the victim.</summary>
    public string? VictimEffect;
    public float? VictimTime;
    /// <summary>Marks the hit as coming from a special (grants no Ruh).</summary>
    public bool? FromSpecial;
    /// <summary>Seconds the victim is charmed (turned frenemy).</summary>
    public float? Frenemy;
    /// <summary>Damage-over-time percent of max HP (the reaper mark).</summary>
    public float? Reap;
    public float? ReapTime;
    /// <summary>Forward shove applied to the WIELDER on the swing (dash-attacks).</summary>
    public float? Lunge;
    /// <summary>Seconds the attack HOLDS its committed pose.</summary>
    public float? Hold;
    /// <summary>Seconds of stagger-immunity granted to the wielder.</summary>
    public float? SuperArmor;
    /// <summary>Times the hitbox re-arms in one activation.</summary>
    public int? MultiHit;
    /// <summary>Multiplier applied OVER the hitbox's own damage (the slam scales by plunge height).</summary>
    public float? DamageScale;
    /// <summary>Buff-injected DoT interval (seconds) — re-pulses the hitbox as a damage-over-time field.</summary>
    public float? Tick;

    public SegmentData Clone() => (SegmentData)MemberwiseClone();
}
