using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGame;

/// <summary>
/// The MYSTERY BOX's buff registry: <c>id → a factory that builds the buff at a given <see cref="Tier"/></c>. The single
/// place a granted buff is instantiated by id (the box's menu calls <see cref="Make"/>). Stat boosts aren't here — they
/// are Needle Point shots (<see cref="NeedlePoint"/>).
/// Only IMPLEMENTED buffs live here; the FULL catalogue is <see cref="BuffIds"/> + docs/buff-catalog.md, and each
/// lands as its mechanic is built (Phase 2 = NEW-mechanic buffs + the reserved triggers; Phase 3 = delivery).
///
/// <para>Tier-scaling lives in the factory's per-tier arrays (Common..Epic). Family gives replace-in-place, so a
/// higher tier of the same buff supersedes the lower (Player.add_passive). Per-attack buffs set AppliesTo but are
/// primarily gated at OFFER time (only shown for the equipped attack).</para>
/// </summary>
public static class BuffCatalog
{
    public static readonly Dictionary<string, Func<Tier, Buff>> FACTORIES = new()
    {
        // --- lifesteal (LifestealBuff via OnHitDealt) ---
        [BuffIds.Bloodrush] = t => new LifestealBuff(BuffIds.Bloodrush,
            new[] { 0.03f, 0.05f, 0.08f, 0.12f, 0.18f }) { Tier = t, Family = "bloodrush" },
        [BuffIds.Skim] = t => new LifestealBuff(BuffIds.Skim,
            new[] { 0.01f, 0.02f, 0.03f, 0.04f, 0.06f }) { Tier = t, Family = "skim" },

        // --- immunity windows (InvulnBuff via grant_invuln, routed by trigger) ---
        [BuffIds.DashImmunity] = t => new InvulnBuff(BuffIds.DashImmunity, Trigger.OnDash,
            new[] { 0.5f, 1.0f, 1.5f, 2.0f, 3.0f }) { Tier = t, Family = "dash_immunity" },
        [BuffIds.JumpImmunity] = t => new InvulnBuff(BuffIds.JumpImmunity, Trigger.OnGroundJump,
            new[] { 0.5f, 0.75f, 1.0f, 1.5f, 2.0f }) { Tier = t, Family = "jump_immunity" },
        [BuffIds.SlamImmunity] = t => new InvulnBuff(BuffIds.SlamImmunity, Trigger.OnSlamLand,
            new[] { 1.0f, 1.5f, 2.0f, 2.5f, 3.0f }) { Tier = t, Family = "slam_immunity" },
        [BuffIds.HitGuard] = t => new InvulnBuff(BuffIds.HitGuard, Trigger.OnHitDealt,
            new[] { 0.1f, 0.25f, 0.4f, 0.6f, 1.0f }) { Tier = t, Family = "hit_guard" },
        // Follow-through: immunity window at attack-anim end (OnAnimEnd, dispatched when a swing recovers to neutral).
        [BuffIds.FollowThrough] = t => new InvulnBuff(BuffIds.FollowThrough, Trigger.OnAnimEnd,
            new[] { 0.5f, 1.0f, 1.5f, 2.0f, 3.0f }) { Tier = t, Family = "follow_through", AppliesTo = { "attack" } },

        // --- Slam Spring (one-shot jump boost, primed OnSlamLand) ---
        [BuffIds.SlamSpring] = t => new SlamSpringBuff(BuffIds.SlamSpring,
            new[] { 1.30f, 1.50f, 1.70f, 1.90f, 2.20f }) { Tier = t, Family = "slam_spring" },

        // --- slam on-land procs (OnSlamLand) ---
        [BuffIds.SlamQuake] = t => new SlamQuakeBuff(BuffIds.SlamQuake,
            new[] { 1.0f, 1.5f, 2.0f, 3.0f, 4.0f }) { Tier = t, Family = "slam_quake" },
        [BuffIds.SlamWrath] = t => new SlamWrathBuff(BuffIds.SlamWrath,
            new[] { 1.30f, 1.50f, 1.70f, 1.90f, 2.20f },     // attack-damage mult
            new[] { 1.0f, 1.5f, 2.0f, 2.5f, 3.0f })          // window seconds
            { Tier = t, Family = "slam_wrath", AppliesTo = { "attack" } },

        // --- per-special (offer-gated): Bakshen Overcharge (its hits cut its cooldown; Epic = full reset) ---
        [BuffIds.Overcharge] = t => new OverchargeBuff(BuffIds.Overcharge,
            new[] { 0.5f, 1.0f, 1.5f, 2.0f, 9999f }) { Tier = t, Family = "overcharge", AppliesTo = { SpecialIds.Bakshen } },

        // --- per-attack (offer-gated): Zahluq Instant Reset (OnMiss → full attack-cooldown reset) ---
        [BuffIds.InstantReset] = t => new InstantResetBuff(BuffIds.InstantReset)
            { Tier = t, Family = "instant_reset", AppliesTo = { SpecialIds.Zahluq } },

        // --- attack ramp: Momentum (OnHitDealt → stacking damage; resets when a full swing/combo whiffs, via OnAnimEnd) ---
        [BuffIds.Momentum] = t => new MomentumBuff(BuffIds.Momentum,
            new[] { 1.15f, 1.25f, 1.40f, 1.60f, 2.00f }) { Tier = t, Family = "momentum", AppliesTo = { "attack" } },

        // TODO(slam_feast): no enemy kill-count is available at OnSlamLand — the slam's damage Strike (slam_default)
        //   is a burst the ParticleDirector spawns on the slam anim frames 3/4, i.e. AFTER SlamRelease dispatches
        //   OnSlamLand, so no kills are counted yet at the hook — defer until a slam-kill tally lands.
        // TODO(backstab): needs the victim's position vs the player's facing at CONTACT; damage is baked into the
        //   Hitbox at activate time and applied in Hitbox.OnAreaEntered (amount already fixed), with no pre-contact
        //   per-victim tuning hook — defer until an on-contact tuning seam exists.
        // TODO(pd_haste/pd_fury/pd_aegis): a dash dodge can't be detected cleanly — dash i-frames work by making the
        //   player's Hurtbox non-Monitorable during the active dash (Player._PhysicsProcess), so an incoming hit
        //   never reaches OnHurt (no Area overlap fires) and there is no "avoided due to dash" event. Emitting would
        //   require flipping that hurtbox logic and guessing intent — risks breaking the dodge — defer until a clean
        //   perfect-dodge window is added.
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

    /// <summary>Build a granted <see cref="Buff"/> for <paramref name="id"/> at <paramref name="tier"/> (null if
    /// not implemented — see <see cref="Implemented"/>), with its Name + Description filled from <see cref="INFO"/>.</summary>
    public static Buff Make(string id, Tier tier)
    {
        if (!FACTORIES.TryGetValue(id, out var f))
            return null;
        var buff = f(tier);
        if (INFO.TryGetValue(id, out var info))
        {
            buff.Name = info.Name;
            buff.Description = info.Desc;
        }
        return buff;
    }

    /// <summary>Whether <paramref name="id"/> has a working factory (vs. catalogued-only, pending its mechanic).</summary>
    public static bool Implemented(string id) => FACTORIES.ContainsKey(id);

    // --- the mystery box's offer pool ---------------------------------------------------------------------------
    private static string[] _general, _powerful;

    /// <summary>Implemented ids that aren't gated to a SPECIFIC move (AppliesTo only "*"/"attack"/"special", or none)
    /// — so offering one is always meaningful. Cached.</summary>
    private static string[] General()
    {
        if (_general != null)
            return _general;
        var ids = new List<string>();
        foreach (string id in FACTORIES.Keys)
        {
            if (Make(id, Tier.Common) is not Buff b)
                continue;
            if (b.AppliesTo.All(a => a is "*" or "attack" or "special"))
                ids.Add(id);
        }
        _general = ids.ToArray();
        return _general;
    }

    /// <summary>POWERFUL pool — every general (not move-gated) buff. The mystery box draws from these at above-rare tiers.</summary>
    public static string[] PowerfulIds() => _powerful ??= General().ToArray();
}
