using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// A MOVE-SCOPED build capability — the item/build layer. A Buff IS a <see cref="Passive"/> (granted the same
/// way: a reward's passive → <c>Player.add_passive</c>, torn down on run restart), plus the reward doc's extras.
/// C# port of <c>scripts/abilities/buff.gd</c>, extended to the doc's model. See docs/rewards-design.md.
///
/// <list type="bullet">
/// <item><see cref="AppliesTo"/> — WHICH move(s) this touches: a move id ("twin_reaper"), a family keyword
///   ("attack"/"special", matched on Action.category), a tag ("shield"/"charm", matched on Action.tags), or "*"
///   for everything (empty = "*"). One field expresses a tailor-made per-move buff AND a shared/general one.</item>
/// <item><see cref="Family"/> — a REPLACE-IN-PLACE group. Granting a buff whose family is already held removes the
///   old one first, so a TIER upgrade supersedes its predecessor instead of stacking. "" = independent. The doc's
///   rule: same buff, different tier → replace (by family); a DIFFERENT buff → stacks.</item>
/// <item><see cref="Tier"/> — the doc's rarity (Common→Epic). Carries the badge colour; the per-tier magnitude
///   lives in the concrete buff (it reads its own Tier to scale). Higher tier replaces lower within a family.</item>
/// <item><see cref="Trigger"/> — the primary hook this buff binds to (data/display; the working mechanism is still
///   overriding the Passive hook). Lets a future data-driven buff declare its moment without a subclass.</item>
/// </list>
///
/// <para>Two ways a buff takes effect (either or both): NUMBERS — override <see cref="Passive.ModifyTuning"/>,
/// gated with <see cref="AppliesToAction"/>; BEHAVIOUR — override an event hook (self-scoping). Concrete buffs
/// live at <c>scripts/abilities/*.cs</c> and set these in their constructor.</para>
/// </summary>
[GlobalClass]
public partial class Buff : Passive
{
    /// <summary>Move ids / family keywords / tags this buff modifies (empty or "*" = all).</summary>
    public List<string> AppliesTo = new();

    /// <summary>Replace-in-place group ("" = never auto-replaced). Tiers of one buff share a family.</summary>
    public string Family = "";

    /// <summary>Rarity tier (doc: Common→Epic). Drives the badge colour + the concrete buff's per-tier scaling.</summary>
    public Tier Tier = Tier.Common;

    /// <summary>The primary event hook this buff binds to (data/display; see <see cref="MyGame.Trigger"/>).</summary>
    public Trigger Trigger = Trigger.None;

    /// <summary>Player-facing display name (HUD + offers). Set by <see cref="BuffCatalog.Make"/>.</summary>
    public string Name = "";

    /// <summary>One-line explanation shown to the player (HUD + offers). Set by <see cref="BuffCatalog.Make"/>.</summary>
    public string Description = "";

    /// <summary>
    /// True if this buff should act on <paramref name="action"/> — by id, by category keyword ("attack"/"special"),
    /// by a tag, or unconditionally ("*"/empty). Gate <see cref="Passive.ModifyTuning"/> (and move-specific work) with it.
    /// </summary>
    public bool AppliesToAction(Action action)
    {
        if (action == null)
            return false;
        if (AppliesTo.Count == 0 || AppliesTo.Contains("*"))
            return true;
        if (AppliesTo.Contains(action.Id))
            return true;
        string catKw = action.Category switch
        {
            ActionCategory.Attack => "attack",
            ActionCategory.Special => "special",
            _ => "",
        };
        if (catKw != "" && AppliesTo.Contains(catKw))
            return true;
        foreach (string t in action.Tags)
            if (AppliesTo.Contains(t))
                return true;
        return false;
    }
}
