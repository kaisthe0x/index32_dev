using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGame;

/// <summary>
/// The MYSTERY BOX's buff registry: <c>id → a factory that builds the buff</c>. The single place a box buff is
/// instantiated by id (<see cref="BoxLedger"/> calls <see cref="Make"/>). Stat boosts aren't here — they are Needle Point
/// shots (<see cref="NeedlePoint"/>); perks are Dekken's (<see cref="Dekken"/>).
/// Only IMPLEMENTED buffs live here; the FULL catalogue is <see cref="BuffIds"/> + docs/buff-catalog.md, and each
/// lands as its mechanic is built.
///
/// <para>Box buffs are MECHANICS with a SINGLE VERSION (no tiers): each factory carries its one value. A buff is
/// permanent for the run and the box never gives one the player already holds (<see cref="Pool"/>).</para>
/// </summary>
public static class BuffCatalog
{
    public static readonly Dictionary<string, Func<Buff>> FACTORIES = new()
    {
        // --- lifesteal (LifestealBuff via OnHitDealt): chance per hit to restore half a block ---
        [BuffIds.Bloodrush] = () => new LifestealBuff(BuffIds.Bloodrush, 0.08f),
        [BuffIds.Skim] = () => new LifestealBuff(BuffIds.Skim, 0.03f),

        // --- immunity windows (InvulnBuff via grant_invuln, routed by trigger): seconds ---
        [BuffIds.DashImmunity] = () => new InvulnBuff(BuffIds.DashImmunity, Trigger.OnDash, 1.5f),
        [BuffIds.JumpImmunity] = () => new InvulnBuff(BuffIds.JumpImmunity, Trigger.OnGroundJump, 1.0f),
        [BuffIds.SlamImmunity] = () => new InvulnBuff(BuffIds.SlamImmunity, Trigger.OnSlamLand, 2.0f),
        [BuffIds.HitGuard] = () => new InvulnBuff(BuffIds.HitGuard, Trigger.OnHitDealt, 0.4f),
        // Follow-through: immunity window at attack-anim end (OnAnimEnd, dispatched when a swing recovers to neutral).
        [BuffIds.FollowThrough] = () => new InvulnBuff(BuffIds.FollowThrough, Trigger.OnAnimEnd, 1.5f) { AppliesTo = { "attack" } },

        // --- Slam Spring (one-shot jump boost, primed OnSlamLand): next ground jump's height multiplier ---
        [BuffIds.SlamSpring] = () => new SlamSpringBuff(BuffIds.SlamSpring, 1.70f),

        // --- slam on-land procs (OnSlamLand) ---
        [BuffIds.SlamQuake] = () => new SlamQuakeBuff(BuffIds.SlamQuake, 2.0f),  // stun seconds
        [BuffIds.SlamWrath] = () => new SlamWrathBuff(BuffIds.SlamWrath, 1.70f, 2.0f)  // attack-damage mult, window seconds
            { AppliesTo = { "attack" } },

        // --- per-special (only offered while that special is equipped): Bakshen Overcharge (its hits cut its cooldown) ---
        [BuffIds.Overcharge] = () => new OverchargeBuff(BuffIds.Overcharge, 1.5f) { AppliesTo = { SpecialIds.Bakshen } },

        // --- per-special: Zahluq Instant Reset (OnMiss → full cooldown reset) — PARKED, see Parked ---
        [BuffIds.InstantReset] = () => new InstantResetBuff(BuffIds.InstantReset) { AppliesTo = { SpecialIds.Zahluq } },

        // --- attack ramp: Momentum (OnHitDealt → stacking damage; resets when a full swing/combo whiffs, via OnAnimEnd) ---
        [BuffIds.Momentum] = () => new MomentumBuff(BuffIds.Momentum, 1.40f) { AppliesTo = { "attack" } },
    };

    /// <summary>Player-facing name + one-line description per buff id (HUD + offers). Complements the per-tier
    /// scaling in FACTORIES. Keep in sync with FACTORIES as buffs are added.</summary>
    public static readonly Dictionary<string, (string Name, string Desc)> INFO = new()
    {
        [BuffIds.Bloodrush] = ("Bloodrush", "Landing a hit has a good chance to restore half a health block."),
        [BuffIds.Skim] = ("Skim", "Landing a hit has a small chance to restore half a health block."),
        [BuffIds.DashImmunity] = ("Phase Dash", "Briefly invulnerable right after you dash."),
        [BuffIds.JumpImmunity] = ("Leap of Faith", "Briefly invulnerable right after a ground jump."),
        [BuffIds.SlamImmunity] = ("Ground Zero", "Invulnerable for a moment after you slam-land."),
        [BuffIds.HitGuard] = ("Hit Guard", "A flicker of invulnerability whenever you land a hit."),
        [BuffIds.FollowThrough] = ("Follow-through", "Briefly invulnerable as an attack finishes."),
        [BuffIds.SlamSpring] = ("Coiled Spring", "Your first ground jump after a slam launches you much higher."),
        [BuffIds.SlamQuake] = ("Quake", "Slam-landing stuns nearby enemies."),
        [BuffIds.SlamWrath] = ("Wrath", "After a slam, your attacks deal bonus damage for a few seconds."),
        [BuffIds.Overcharge] = ("Overcharge", "Landing a Bakshen hit cuts its cooldown."),
        [BuffIds.InstantReset] = ("Instant Reset", "Whiffing Zahluq instantly resets its cooldown."),
        [BuffIds.Momentum] = ("Momentum", "Each consecutive hit deals more — until you whiff."),
    };

    /// <summary>Built but kept OUT of the box pool until their mechanic works end to end (each class says why).</summary>
    public static readonly HashSet<string> Parked = new() { BuffIds.InstantReset };

    /// <summary>Build the <see cref="Buff"/> for <paramref name="id"/> (null if it isn't implemented), with its Name +
    /// Description filled from <see cref="INFO"/>.</summary>
    public static Buff Make(string id)
    {
        if (!FACTORIES.TryGetValue(id, out var f))
            return null;
        var buff = f();
        if (INFO.TryGetValue(id, out var info))
        {
            buff.Name = info.Name;
            buff.Description = info.Desc;
        }
        return buff;
    }

    /// <summary>What the box can give <paramref name="player"/> right now: every implemented, un-parked buff he doesn't
    /// hold yet — a buff tied to ONE move (its <see cref="Buff.AppliesTo"/> names a move id, not just
    /// "*"/"attack"/"special") only while that move is equipped.</summary>
    public static List<string> Pool(Player player)
    {
        var equipped = new HashSet<string>
        {
            player.loadout_id(LoadoutCategory.Attack), player.loadout_id(LoadoutCategory.Special),
        };
        var ids = new List<string>();
        foreach (string id in FACTORIES.Keys)
        {
            if (Parked.Contains(id) || player.has_passive(id))
                continue;
            Buff b = Make(id);
            if (b.AppliesTo.All(a => a is "*" or "attack" or "special" || equipped.Contains(a)))
                ids.Add(id);
        }
        return ids;
    }
}
