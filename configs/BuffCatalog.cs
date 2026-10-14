using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGame;

/// <summary>
/// The FIGMENTER's buff registry: <c>id → a factory that builds the buff</c>. The single place a Figmenter buff is
/// instantiated by id (<see cref="FigmenterLedger"/> calls <see cref="Make"/>). Stat boosts aren't here — they are Needle Point
/// shots (<see cref="NeedlePoint"/>); perks are Dekken's (<see cref="Dekken"/>).
/// Only IMPLEMENTED buffs live here; the FULL catalogue is <see cref="BuffIds"/> + docs/buff-catalog.md, and each
/// lands as its mechanic is built.
///
/// <para>Figmenter buffs are MECHANICS, and each comes in FIVE STRENGTHS — one value per <see cref="WineRarity"/>
/// (Common → Epic), listed in that order in its factory. The wine that grants it decides which: a wine drunk young
/// gives the first value, one carried until Epic the last. A buff is permanent for the run and the Figmenter never
/// makes one the player already holds (<see cref="Pool"/>).</para>
/// </summary>
public static class BuffCatalog
{
    public static readonly Dictionary<string, Func<WineRarity, Buff>> Factories = new()
    {
        // --- lifesteal (LifestealBuff via OnHitDealt): chance per hit to restore half a block ---
        [BuffIds.Bloodrush] = r => new LifestealBuff(BuffIds.Bloodrush, At(r, 0.03f, 0.05f, 0.08f, 0.12f, 0.18f)),
        [BuffIds.Skim] = r => new LifestealBuff(BuffIds.Skim, At(r, 0.01f, 0.02f, 0.03f, 0.04f, 0.06f)),

        // --- immunity windows (InvulnBuff via GrantInvuln, routed by trigger): seconds ---
        [BuffIds.DashImmunity] = r => new InvulnBuff(BuffIds.DashImmunity, Trigger.OnDash, At(r, 0.5f, 1.0f, 1.5f, 2.0f, 3.0f)),
        [BuffIds.JumpImmunity] = r => new InvulnBuff(BuffIds.JumpImmunity, Trigger.OnGroundJump, At(r, 0.5f, 0.75f, 1.0f, 1.5f, 2.0f)),
        [BuffIds.SlamImmunity] = r => new InvulnBuff(BuffIds.SlamImmunity, Trigger.OnSlamLand, At(r, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f)),
        [BuffIds.HitGuard] = r => new InvulnBuff(BuffIds.HitGuard, Trigger.OnHitDealt, At(r, 0.1f, 0.25f, 0.4f, 0.6f, 1.0f)),
        // Follow-through: immunity window at attack-anim end (OnAnimEnd, dispatched when a swing recovers to neutral).
        [BuffIds.FollowThrough] = r => new InvulnBuff(BuffIds.FollowThrough, Trigger.OnAnimEnd, At(r, 0.5f, 1.0f, 1.5f, 2.0f, 3.0f)) { AppliesTo = { "attack" } },

        // --- Slam Spring (one-shot jump boost, primed OnSlamLand): next ground jump's height multiplier ---
        [BuffIds.SlamSpring] = r => new SlamSpringBuff(BuffIds.SlamSpring, At(r, 1.30f, 1.50f, 1.70f, 1.90f, 2.20f)),

        // --- slam on-land procs (OnSlamLand) ---
        [BuffIds.SlamQuake] = r => new SlamQuakeBuff(BuffIds.SlamQuake, At(r, 1.0f, 1.5f, 2.0f, 3.0f, 4.0f)),  // stun seconds
        [BuffIds.SlamWrath] = r => new SlamWrathBuff(BuffIds.SlamWrath,
            At(r, 1.30f, 1.50f, 1.70f, 1.90f, 2.20f),   // attack-damage mult
            At(r, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f))        // window seconds
            { AppliesTo = { "attack" } },

        // --- per-special (only made while that special is equipped): Bakshen Overcharge — its hits cut its cooldown by
        //     this many seconds (Epic = the whole cooldown) ---
        [BuffIds.Overcharge] = r => new OverchargeBuff(BuffIds.Overcharge, At(r, 0.5f, 1.0f, 1.5f, 2.0f, 9999f)) { AppliesTo = { SpecialIds.Bakshen } },

        // --- per-special: Zahluq Instant Reset (OnMiss → full cooldown reset; one strength) — PARKED, see Parked ---
        [BuffIds.InstantReset] = _ => new InstantResetBuff(BuffIds.InstantReset) { AppliesTo = { SpecialIds.Zahluq } },

        // --- attack ramp: Momentum (OnHitDealt → stacking damage; resets when a full swing/combo whiffs, via OnAnimEnd) ---
        [BuffIds.Momentum] = r => new MomentumBuff(BuffIds.Momentum, At(r, 1.15f, 1.25f, 1.40f, 1.60f, 2.00f)) { AppliesTo = { "attack" } },
    };

    /// <summary>The value for <paramref name="rarity"/> out of a buff's five (Common, Rare, Hot, Sensational, Epic).</summary>
    private static float At(WineRarity rarity, float common, float rare, float hot, float sensational, float epic) => rarity switch
    {
        WineRarity.Common => common,
        WineRarity.Rare => rare,
        WineRarity.Hot => hot,
        WineRarity.Sensational => sensational,
        _ => epic,
    };

    /// <summary>Player-facing name + one-line description per buff id (HUD + offers). Keep in sync with
    /// <see cref="Factories"/> as buffs are added.</summary>
    public static readonly Dictionary<string, (string Name, string Desc)> Info = new()
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

    /// <summary>Built but kept OUT of the Figmenter pool until their mechanic works end to end (each class says why).</summary>
    public static readonly HashSet<string> Parked = new() { BuffIds.InstantReset };

    /// <summary>Build the <see cref="Buff"/> for <paramref name="id"/> at <paramref name="rarity"/>'s strength (null if it
    /// isn't implemented), with its Name + Description filled from <see cref="Info"/>.</summary>
    public static Buff? Make(string id, WineRarity rarity)
    {
        if (!Factories.TryGetValue(id, out var f))
            return null;
        var buff = f(rarity);
        buff.Rarity = rarity;
        if (Info.TryGetValue(id, out var info))
        {
            buff.Name = info.Name;
            buff.Description = info.Desc;
        }
        return buff;
    }

    /// <summary>What a wine could hold for <paramref name="player"/> right now: every implemented, un-parked buff he doesn't
    /// hold yet — a buff tied to ONE move (its <see cref="Buff.AppliesTo"/> names a move id, not just
    /// "*"/"attack"/"special") only while that move is equipped.</summary>
    public static List<string> Pool(Player player)
    {
        var equipped = new HashSet<string>
        {
            player.LoadoutId(LoadoutCategory.Attack), player.LoadoutId(LoadoutCategory.Special),
        };
        var ids = new List<string>();
        foreach (string id in Factories.Keys)
        {
            if (Parked.Contains(id) || player.HasPassive(id))
                continue;
            if (Make(id, WineRarity.Common) is { } b && b.AppliesTo.All(a => a is "*" or "attack" or "special" || equipped.Contains(a)))
                ids.Add(id);
        }
        return ids;
    }
}
