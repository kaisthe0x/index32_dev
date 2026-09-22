using Godot;

namespace MyGame;

/// <summary>
/// One thing a character PERFORMS — an attack / special / movement / surge. Identity + cadence + OPTIONALLY a
/// <see cref="Hit"/> (<see cref="HitData"/>), <see cref="Move"/> (<see cref="Locomotion"/>), or <see cref="Surge"/>
/// (<see cref="SurgeSpec"/>). Presentation is keyed off <see cref="Animation"/>. Typed record (replaces the old
/// stringly-typed GDict rows); authored in <see cref="ActionsKhalid"/>, with <see cref="Id"/>/<see cref="Category"/>
/// injected by the <see cref="Actions"/> accessor via a <c>with</c> expression.
/// </summary>
public sealed record Action
{
    private readonly string _name;

    public string Id { get; init; } = "";
    /// <summary>Display name; falls back to a title-cased <see cref="Id"/> when unset.</summary>
    public string Name { get => _name ?? Capitalize(Id); init => _name = value; }
    public string Icon { get; init; } = "";
    /// <summary>Player-facing blurb for selection screens (the attack picker's details panel). Optional.</summary>
    public string Description { get; init; } = "";
    public ActionCategory Category { get; init; } = ActionCategory.Attack;
    public ActionStyle Style { get; init; } = ActionStyle.Standard;
    public string[] Tags { get; init; } = [];
    public float Cooldown { get; init; } = 0.0f;
    public HitData Hit { get; init; } = null;
    public Locomotion Move { get; init; } = null;
    public SurgeSpec Surge { get; init; } = null;
    /// <summary>Explicit animation name; when null the animation is derived from <see cref="Category"/> + <see cref="Id"/>.</summary>
    public string AnimationOverride { get; init; } = null;

    /// <summary>The sprite animation this action plays — <c>attack_&lt;id&gt;</c> / <c>special_&lt;id&gt;</c> / <c>surge_&lt;id&gt;</c>, or the override.</summary>
    public StringName Animation => AnimationOverride ?? Category switch
    {
        ActionCategory.Attack => $"attack_{Id}",
        ActionCategory.Special => $"special_{Id}",
        ActionCategory.Surge => $"surge_{Id}",
        _ => "",
    };

    /// <summary>The hitbox tuning for combo segment <paramref name="seg"/> (empty when this action has no hitbox).</summary>
    public SegmentData Segment(int seg) => Hit?.Segment(seg) ?? new SegmentData();

    public bool IsFlurry => Style == ActionStyle.Flurry;

    public bool HasTag(string tag) => System.Array.IndexOf(Tags, tag) >= 0;

    // GDScript String.capitalize(): "twin_reaper" -> "Twin Reaper".
    private static string Capitalize(string id)
    {
        var parts = id.Split('_');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0)
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }
}
