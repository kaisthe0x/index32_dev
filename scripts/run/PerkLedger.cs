using System;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// A run's Dekken state + rules (docs/game-loop.md § Economy): the current STOCK (<see cref="Dekken.StockSize"/> random
/// perks, rerolled every round), the timed / whole-run perks ACTIVE on Khalid (as <see cref="Perk"/> passives), and the
/// whole-run perks already OWNED (they leave the pool). RunManager owns one per run and calls <see cref="OnRoundClear"/>;
/// the stall's menu calls <see cref="Buy"/>.
///
/// <para>Always open. A perk takes effect at once: a one-use perk happens (a heal, a teleport); a timed one lasts the
/// current round + <see cref="PerkDef.Rounds"/> − 1 more (buying it again renews it — never stacks); a whole-run one
/// lasts the run and leaves the pool.</para>
/// </summary>
public sealed class PerkLedger
{
    private readonly Player _player;
    private readonly System.Action _fastTravel;              // teleport the player to the mystery box
    private readonly List<string> _stock = new();
    private readonly Dictionary<string, Perk> _active = new();
    private readonly HashSet<string> _owned = new();  // whole-run perks already bought

    public PerkLedger(Player player, System.Action fastTravel)
    {
        _player = player;
        _fastTravel = fastTravel;
        RollStock();
    }

    /// <summary>This round's perks, in shop order.</summary>
    public IReadOnlyList<string> Stock => _stock;

    public bool IsActive(string id) => _active.ContainsKey(id);
    public int RoundsLeft(string id) => _active.TryGetValue(id, out var p) ? p.RoundsLeft : 0;
    public int Price(string id) => Dekken.Get(id).Price;

    /// <summary>Why buying <paramref name="id"/> would do nothing right now ("" = it can be bought, money permitting).</summary>
    public string Blocked(string id)
    {
        var def = Dekken.Get(id);
        return def.Duration switch
        {
            PerkDuration.OneUse when id == PerkIds.Heal && _player.health >= _player.max_health => "FULL HEALTH",
            PerkDuration.Run when _owned.Contains(id) => "OWNED",
            PerkDuration.Rounds when RoundsLeft(id) >= def.Rounds => "ACTIVE",
            _ => "",
        };
    }

    /// <summary>Buy <paramref name="id"/> — its effect happens / starts at once. False if the perk isn't stocked, buying
    /// it would do nothing (<see cref="Blocked"/>), or it's unaffordable.</summary>
    public bool Buy(string id)
    {
        if (!_stock.Contains(id) || Blocked(id) != "" || !_player.spend_lira(Price(id)))
            return false;
        var def = Dekken.Get(id);
        switch (def.Duration)
        {
            case PerkDuration.OneUse:
                if (id == PerkIds.Heal)
                    _player.heal(def.Value);
                else if (id == PerkIds.FastTravel)
                    _fastTravel();
                break;
            case PerkDuration.Run:
                _owned.Add(id);
                Activate(def);
                break;
            case PerkDuration.Rounds:
                Activate(def);
                break;
        }
        return true;
    }

    /// <summary>A round was cleared: timed perks spend a round (those out of rounds end), and the stock rerolls for the
    /// next round.</summary>
    public void OnRoundClear()
    {
        foreach (var (id, perk) in new List<KeyValuePair<string, Perk>>(_active))
            if (perk.Def.Duration == PerkDuration.Rounds && --perk.RoundsLeft <= 0)
            {
                _active.Remove(id);
                _player.remove_passive(perk);
            }
        RollStock();
        _player.refresh_buff_hud();
    }

    /// <summary>Put a fresh <paramref name="def"/> perk on the player, replacing any running copy (a renewal).</summary>
    private void Activate(PerkDef def)
    {
        if (_active.TryGetValue(def.Id, out var old))
            _player.remove_passive(old);
        var perk = new Perk(def);
        _active[def.Id] = perk;
        _player.add_passive(perk);
    }

    /// <summary>Draw a round's stock: up to <see cref="Dekken.StockSize"/> distinct eligible perks — not a whole-run
    /// perk already owned, and not one gated to a special the player hasn't equipped.</summary>
    private void RollStock()
    {
        var pool = new List<string>();
        string special = _player.loadout_id(LoadoutCategory.Special);
        foreach (PerkDef d in Dekken.PERKS)
            if (!_owned.Contains(d.Id) && (d.RequiresSpecial == null || d.RequiresSpecial == special))
                pool.Add(d.Id);
        _stock.Clear();
        while (_stock.Count < Dekken.StockSize && pool.Count > 0)
        {
            int i = (int)(Godot.GD.Randi() % (uint)pool.Count);
            _stock.Add(pool[i]);
            pool.RemoveAt(i);
        }
        _stock.Sort((a, b) => Array.FindIndex(Dekken.PERKS, d => d.Id == a).CompareTo(Array.FindIndex(Dekken.PERKS, d => d.Id == b)));
    }
}
