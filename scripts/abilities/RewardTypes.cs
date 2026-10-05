using Godot;

namespace MyGame;

/// <summary>
/// The event HOOKS a Buff/Passive reacts to — the spine of the reward doc (every buff hangs off a moment).
/// The first block maps 1:1 to <see cref="Passive"/>'s virtual methods, all wired + firing today. The second
/// block is the doc's GROWING vocabulary: reserved names that need new player-side detection (a whiff, a
/// last-second dodge, a level timer) and get wired as the design firms up. This is the extensible trigger set
/// the user called out ("we might have different triggers in the future") — add a value, add its emit site.
/// </summary>
public enum Trigger
{
    None,
    // --- wired: dispatched by Player at the matching moment (override the Passive hook to react) ---
    Setup, Physics, ModifyTuning, OnHitDealt, OnHurt, OnLand, OnParry, OnSpecialCast, OnSpecialStrike,
    OnDash, OnGroundJump, OnAirJump, OnSlamTrigger, OnSlamLand, OnMiss, OnAnimEnd, OnRoundStart,
    // --- reserved: need new detection before they can fire (see docs/rewards-design.md §"load-bearing") ---
    OnAttackTrigger, OnPerfectDodge, OnSurge, OnRoundWindow,
}
