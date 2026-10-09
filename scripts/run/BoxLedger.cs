using System.Collections.Generic;
using Godot;

namespace MyGame;

/// <summary>
/// A run's MYSTERY BOX state + rules (docs/game-loop.md § Economy; numbers in <see cref="BoxRules"/>): charging a spin,
/// rolling what it comes up as, and granting what the player takes. RunManager owns one per run; the box
/// (<see cref="MysteryBox"/>) drives it — <see cref="Spin"/> when the player pays, then <see cref="Take"/> if he takes
/// the result, or <see cref="Refund"/> on a teddy bear. There's no other state: "no duplicates" is simply that the pool
/// (<see cref="BuffCatalog.Pool"/>) leaves out what the player already holds.
/// </summary>
public sealed class BoxLedger
{
    private readonly Player _player;

    public BoxLedger(Player player) => _player = player;

    /// <summary>Why a spin can't happen right now ("" = it can): no buff left to give, or not enough figs.</summary>
    public string Blocked()
    {
        if (BuffCatalog.Pool(_player).Count == 0)
            return "EMPTY";
        return _player.fada_figs < BoxRules.Cost ? $"NEED {BoxRules.Cost}" : "";
    }

    /// <summary>The names the box flickers through while it spins — everything it could give right now.</summary>
    public List<string> SpinNames()
    {
        var names = new List<string>();
        foreach (string id in BuffCatalog.Pool(_player))
            names.Add(BuffCatalog.INFO[id].Name);
        return names;
    }

    /// <summary>Pay for a spin and roll its result; null if it's <see cref="Blocked"/>. A teddy bear is only possible
    /// when the box <paramref name="canRelocate"/> (the layout has another spot for it).</summary>
    public BoxRoll? Spin(bool canRelocate)
    {
        if (Blocked() != "" || !_player.spend_fada_figs(BoxRules.Cost))
            return null;
        if (canRelocate && GD.Randf() < BoxRules.TeddyChance)
            return new BoxRoll(BoxOutcome.Teddy, "", "TEDDY BEAR", "The box moves on.");
        var buffs = BuffCatalog.Pool(_player);
        var specials = SpecialPool();
        if (specials.Count > 0 && GD.Randf() < BoxRules.SpecialChance)
        {
            string id = specials[(int)(GD.Randi() % (uint)specials.Count)];
            Action? special = Actions.GetAction(_player.character, "specials", id);
            return new BoxRoll(BoxOutcome.Special, id, special?.Name ?? id, $"SPECIAL — replaces yours. {special?.Description}");
        }
        string buffId = buffs[(int)(GD.Randi() % (uint)buffs.Count)];
        var info = BuffCatalog.INFO[buffId];
        return new BoxRoll(BoxOutcome.Buff, buffId, info.Name, info.Desc);
    }

    /// <summary>Give the spin's figs back (the teddy bear).</summary>
    public void Refund() => _player.collect_fada_fig(BoxRules.Cost);

    /// <summary>The player took <paramref name="roll"/>: a buff joins his passives for the run; a special replaces his.</summary>
    public void Take(BoxRoll roll)
    {
        if (roll.Outcome == BoxOutcome.Buff && BuffCatalog.Make(roll.Id) is { } buff)
            _player.add_passive(buff);
        else if (roll.Outcome == BoxOutcome.Special)
            _player.equip(LoadoutCategory.Special, roll.Id);
    }

    /// <summary>The box-only specials he doesn't have equipped.</summary>
    private List<string> SpecialPool()
    {
        string current = _player.loadout_id(LoadoutCategory.Special);
        var pool = new List<string>();
        foreach (string id in BoxRules.SPECIALS)
            if (id != current)
                pool.Add(id);
        return pool;
    }
}
