using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Presets for <see cref="FloatingText.Emit"/> — PURE DATA, one <see cref="FloatingTextStyle"/> per
/// <see cref="FloatingTextType"/> (its look + in/out transition). Unset fields fall back to the record defaults.
/// </summary>
public static class FloatingTextTypes
{
    private const string Font = "res://assets/fonts/Sixtyfour-Regular-VariableFont_BLED,SCAN.ttf";

    public static readonly Dictionary<FloatingTextType, FloatingTextStyle> TYPES = new()
    {
        // Damage numbers: small white light hits -> big hot-gold heavy hits (ramped by magnitude).
        [FloatingTextType.Damage] = new FloatingTextStyle
        {
            Font = Font, SizeLo = 6, SizeHi = 8, MagLo = 8.0f, MagHi = 45.0f,
            ColorLo = new Color(1, 1, 1), ColorHi = new Color(1.4f, 0.55f, 0.15f),
            Rise = 26.0f, Drift = 12.0f, Jitter = 8.0f, Life = 0.8f, PopScale = 0.7f, PopTime = 0.14f, Hold = 0.4f,
        },
        // Damage dealt by a SPECIAL — same motion, magenta so special hits read distinctly.
        [FloatingTextType.DamageSpecial] = new FloatingTextStyle
        {
            Font = Font, SizeLo = 6, SizeHi = 8, MagLo = 8.0f, MagHi = 45.0f,
            ColorLo = new Color(1.2f, 0.7f, 1.3f), ColorHi = new Color(1.5f, 0.35f, 1.2f),
            Rise = 26.0f, Drift = 12.0f, Jitter = 8.0f, Life = 0.8f, PopScale = 0.7f, PopTime = 0.14f, Hold = 0.4f,
        },
        // Damage the PLAYER takes — Player.TakeDamage OVERRIDES the colour per-call with the run's hair pick.
        [FloatingTextType.PlayerDamage] = new FloatingTextStyle
        {
            Font = Font, SizeLo = 8, SizeHi = 12, MagLo = 5.0f, MagHi = 40.0f,
            Color = new Color(0.58f, 0.12f, 0.12f),
            Rise = 20.0f, Drift = 7.0f, Jitter = 8.0f, Life = 0.95f, PopScale = 0.7f, PopTime = 0.14f, Hold = 0.4f,
        },
    };
}
