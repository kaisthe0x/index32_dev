using Godot;

namespace MyGame;

/// <summary>
/// RARITY tiers for a Buff — the reward doc's ladder. A tier scales a buff's magnitude and can add
/// effects (e.g. Redere Frisbee: +bounces per tier); a higher tier of the SAME <see cref="Buff.Family"/>
/// REPLACES a lower one (Player.AddPassive's replace-in-place). Colours mirror the doc:
/// Common = none, Rare = blue, Hot = orange, Sensational = purple, Epic = red. See docs/rewards-design.md.
/// </summary>
public enum Tier { Common, Rare, Hot, Sensational, Epic }

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

/// <summary>Presentation helpers for <see cref="Tier"/> (label + badge colour), per the reward doc.</summary>
public static class Tiers
{
    /// <summary>The badge colour for a tier (Common has none — plain white).</summary>
    public static Color ColorOf(Tier t) => t switch
    {
        Tier.Rare => new Color(0.30f, 0.60f, 1.00f),        // blue
        Tier.Hot => new Color(1.00f, 0.55f, 0.15f),         // orange
        Tier.Sensational => new Color(0.70f, 0.35f, 1.00f), // purple
        Tier.Epic => new Color(1.00f, 0.25f, 0.25f),        // red
        _ => Colors.White,                                  // Common: no colour
    };

    public static string Label(Tier t) => t switch
    {
        Tier.Rare => "Rare",
        Tier.Hot => "Hot",
        Tier.Sensational => "Sensational",
        Tier.Epic => "Epic",
        _ => "Common",
    };
}
