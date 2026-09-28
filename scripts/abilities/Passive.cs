using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// Base class for a BEHAVIOURAL build capability — a character's intrinsic ability OR a reward-granted
/// passive. The <see cref="Player"/> holds a LIST of active passives (<c>_passives</c>) and dispatches these
/// hooks to each at the right moment; a passive overrides only the hooks it needs. C# port of
/// <c>scripts/abilities/passive.gd</c>.
///
/// <para>Two flavours, same interface: a character's INTRINSIC ability (<see cref="CharacterAbility"/>, seeded
/// first on equip) and a REWARD-granted passive (added at runtime via <c>Player.add_passive</c> when its reward
/// is taken, torn down on run restart). <see cref="Buff"/> extends this with move-scope + tier + duration.</para>
///
/// <para><c>action</c> params are the <c>Action</c> object, carried as <see cref="GodotObject"/> (bridge:
/// <c>action.Get("id")</c> / <c>.Call("segment", seg)</c>).</para>
/// </summary>
[GlobalClass]
public partial class Passive : RefCounted
{
    /// <summary>Stable id (Build queries / dedup / debug); concrete passives set it in their constructor.</summary>
    public string Id = "";

    /// <summary>Once, right after this passive is added (equip or grant). One-off setup — bump a stat, cache state.</summary>
    public virtual void Setup(Player player) { }

    /// <summary>Once when removed (run restart / character change). UNDO whatever <see cref="Setup"/> left behind.</summary>
    public virtual void Teardown(Player player) { }

    /// <summary>Every physics frame, after the state machine sets velocity but before move_and_slide — override movement here.</summary>
    public virtual void Physics(Player player, double delta) { }

    /// <summary>Once when the special reaches its strike frame (as the melee hitbox fires) — an on-strike effect.</summary>
    public virtual void OnSpecialStrike(Player player) { }

    /// <summary>The instant a special is CAST (before the wind-up), with the special Action — a cast-triggered effect.</summary>
    public virtual void OnSpecialCast(Player player, Action action) { }

    /// <summary>On a PERFECT PARRY with the Redere Shield (the reflect branch), with the parried hit — a parry payoff.</summary>
    public virtual void OnParry(Player player, Hit hit) { }

    /// <summary>THE OUTGOING-TUNING HOOK. Called for every attack/special swing so a buff can alter this move's
    /// per-hit numbers. <paramref name="tuning"/> is a private copy — mutate + return it. <paramref name="action"/>
    /// is the move, <paramref name="seg"/> its combo segment, so a per-move buff can gate on it.</summary>
    public virtual SegmentData ModifyTuning(Player player, Action action, int seg, SegmentData tuning) => tuning;

    /// <summary>When the player takes a combat hit (as it lands) — retaliation, a defensive reaction, etc.</summary>
    public virtual void OnHurt(Player player, Hit hit) { }

    /// <summary>On every touchdown, with how far the player DROPPED this airborne stretch (px) and the landing speed.</summary>
    public virtual void OnLand(Player player, float fallDistance, float fallSpeed) { }

    /// <summary>When the player DEALS damage to an enemy (<paramref name="amount"/> HP removed) — lifesteal, on-hit procs.</summary>
    public virtual void OnHitDealt(Player player, float amount, Node target) { }

    // --- the reward doc's GROWING movement/attack trigger set (see Trigger). Dispatched by Player at the
    //     matching moment; override to react. The harder ones (OnMiss / OnPerfectDodge / level timer) are
    //     reserved in Trigger until the player learns to emit them. ---

    /// <summary>The instant a dash begins (doc: "On Dash").</summary>
    public virtual void OnDash(Player player) { }

    /// <summary>A ground jump off the floor (doc: "On Ground Jump").</summary>
    public virtual void OnGroundJump(Player player) { }

    /// <summary>A mid-air jump (doc: "On Air Jump").</summary>
    public virtual void OnAirJump(Player player) { }

    /// <summary>A slam is triggered / committed (doc: "On Slam Trigger").</summary>
    public virtual void OnSlamTrigger(Player player) { }

    /// <summary>A slam's impact lands, with the plunge distance/speed (doc: "On Slam Land").</summary>
    public virtual void OnSlamLand(Player player, float fallDistance, float fallSpeed) { }

    /// <summary>An attack SWING's animation finished and recovered back to neutral (doc: "On Attack Animation End").
    /// Fires once when a melee attack concludes without chaining/cancelling — the Follow-through immunity window.</summary>
    public virtual void OnAnimEnd(Player player) { }

    /// <summary>A player attack hitbox deactivated having struck NOBODY — a WHIFF (doc: "On Miss"). Fires per
    /// attack hitbox; single-box attacks (e.g. Zahluq) get exactly one per swing.</summary>
    public virtual void OnMiss(Player player) { }
}
