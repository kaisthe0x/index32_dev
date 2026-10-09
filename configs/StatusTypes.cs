using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Registry of enemy STATUS effects shown as a small icon next to the floating health bar (see StatusIcons).
/// Each entry pairs a tint + a human label; the icon TEXTURE comes from <see cref="Icons"/> under <c>status:&lt;id&gt;</c>.
/// <see cref="Order"/> fixes the left-to-right layout; <see cref="Overhead"/> is the optional hovering halo.
/// Pure data. Add a status = a <see cref="StatusType"/> value + a DEFS/ORDER entry + an Icons path.
/// </summary>
public static class StatusTypes
{
    public static readonly Dictionary<StatusType, StatusDef> Defs = new()
    {
        [StatusType.Reap] = new(new Color(0.55f, 0.95f, 0.45f), "Reaped"),
        [StatusType.Stun] = new(new Color(1.0f, 0.86f, 0.28f), "Stunned"),
        [StatusType.Slow] = new(new Color(0.45f, 0.7f, 1.0f), "Slowed"),
        [StatusType.Charm] = new(new Color(1.0f, 0.5f, 0.75f), "Charmed"),
    };

    /// <summary>Fixed draw order (left → right).</summary>
    public static readonly StatusType[] Order = { StatusType.Reap, StatusType.Stun, StatusType.Slow, StatusType.Charm };

    /// <summary>Optional over-head halo per status. ONE shows at a time (ORDER priority).</summary>
    public static readonly Dictionary<StatusType, OverheadHalo> Overhead = new()
    {
        [StatusType.Reap] = new("res://sprites/things/state/dying.png", 12, 12.0, 0.3f, 22.0f),
        [StatusType.Stun] = new("res://sprites/things/state/stunned.png", 4, 12.0, 0.3f, 20.0f),
    };

    /// <summary>The tint for a status (white if somehow undefined), so StatusIcons can colour a placeholder pip.</summary>
    public static Color ColorOf(StatusType s) => Defs.TryGetValue(s, out var d) ? d.Color : Colors.White;

    /// <summary>The snake config key for a status (indexes <see cref="Icons"/>: <c>status:&lt;key&gt;</c>).</summary>
    public static string Key(this StatusType s) => s.ToString().ToLowerInvariant();
}
