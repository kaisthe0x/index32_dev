using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Accessor over the per-character ACTION catalogs — hands out <see cref="Action"/> records, injecting the pool key
/// as <see cref="Action.Id"/> and the kind as <see cref="Action.Category"/>. `kind` is one of the six pools:
/// "attacks"/"specials"/"surges" (combat) or "run"/"jump"/"dash"/"slam" (movement). Repo ships Khalid only.
/// </summary>
public static class Actions
{
    private static Dictionary<string, Dictionary<string, Action>> Tables(string character)
    {
        if (character == "khalid")
        {
            var mv = ActionsKhalid.Movements;
            return new Dictionary<string, Dictionary<string, Action>>
            {
                { "attacks", ActionsKhalid.Attacks }, { "specials", ActionsKhalid.Specials }, { "surges", ActionsKhalid.Surges },
                { MovementIds.Run, mv[MovementIds.Run] }, { MovementIds.Jump, mv[MovementIds.Jump] },
                { MovementIds.Dash, mv[MovementIds.Dash] }, { MovementIds.Slam, mv[MovementIds.Slam] },
            };
        }
        return new Dictionary<string, Dictionary<string, Action>>();
    }

    private static string DefaultId(string character, string kind)
    {
        if (character == "khalid")
            return kind switch
            {
                "attacks" => ActionsKhalid.DefaultAttack,
                "specials" => ActionsKhalid.DefaultSpecial,
                "surges" => ActionsKhalid.DefaultSurge,
                _ => ActionsKhalid.DefaultMovements.GetValueOrDefault(kind, ""),
            };
        return "";
    }

    private static ActionCategory CategoryOf(string kind) => kind switch
    {
        "attacks" => ActionCategory.Attack,
        "specials" => ActionCategory.Special,
        "surges" => ActionCategory.Surge,
        MovementIds.Run => ActionCategory.Run,
        MovementIds.Jump => ActionCategory.Jump,
        MovementIds.Dash => ActionCategory.Dash,
        MovementIds.Slam => ActionCategory.Slam,
        _ => ActionCategory.Other,
    };

    /// <summary>The Action for a character's pool by id (or the default when id is empty/unknown). Null if the pool is empty.</summary>
    public static Action? GetAction(string character, string kind, string id = "")
    {
        var tables = Tables(character);
        if (!tables.TryGetValue(kind, out var pool) || pool.Count == 0)
            return null;
        if (id == "" || !pool.ContainsKey(id))
            id = DefaultId(character, kind);
        if (!pool.TryGetValue(id, out var action))
            return null;
        return action with { Id = id, Category = CategoryOf(kind) };
    }

    /// <summary>Ids of a character's actions in a pool (Loadout builds swap options from these).</summary>
    public static string[] Ids(string character, string kind)
    {
        var tables = Tables(character);
        return tables.TryGetValue(kind, out var pool) ? new List<string>(pool.Keys).ToArray() : System.Array.Empty<string>();
    }
}
