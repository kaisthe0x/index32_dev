using System;
using System.Collections.Generic;
using Godot;

namespace MyGame;

/// <summary>
/// A run's Dekken state + rules (docs/game-loop.md § Economy): the current STOCK (<see cref="Dekken.StockSize"/> random
/// perks, rerolled every round), the VIALS the player is carrying (<see cref="Held"/> — up to
/// <see cref="Dekken.CarrySlots"/>, never two of a kind), the timed / whole-run perks ACTIVE on Khalid (as
/// <see cref="Perk"/> passives), and the whole-run perks already OWNED (they leave the pool). RunManager owns one per
/// run and calls <see cref="OnRoundClear"/>; the stall's menu calls <see cref="BuyDrink"/> / <see cref="BuyKeep"/>; the
/// drink key calls <see cref="DrinkHeld"/>.
///
/// <para>Always open. A vial bought to DRINK takes effect at once; one bought to KEEP waits in a carry slot until it's
/// drunk — instantly, mid-fight. Either way the perk is the same: a one-use perk happens (a heal, a teleport); a timed
/// one lasts the round it's drunk in + <see cref="PerkDef.Rounds"/> − 1 more (another renews it — never stacks); a
/// whole-run one lasts the run and leaves the pool (and can't be kept).</para>
/// </summary>
public sealed class PerkLedger
{
    private readonly Player _player;
    private readonly System.Action _fastTravel;              // teleport the player to the mystery box
    private readonly System.Action<PerkLedger> _heldChanged; // the carried vials / the selection changed (the HUD redraws)
    private readonly List<string> _stock = new();
    private readonly List<string> _held = new();             // carried vials, in the order they were kept
    private int _selected;                                   // which carried vial the drink key drinks
    private readonly Dictionary<string, Perk> _active = new();
    private readonly HashSet<string> _owned = new();  // whole-run perks already bought

    public PerkLedger(Player player, System.Action fastTravel, System.Action<PerkLedger> heldChanged)
    {
        _player = player;
        _fastTravel = fastTravel;
        _heldChanged = heldChanged;
        RollStock();
        _heldChanged(this);
    }

    /// <summary>This round's perks, in shop order.</summary>
    public IReadOnlyList<string> Stock => _stock;

    /// <summary>The vials being carried, and which one the drink key drinks.</summary>
    public IReadOnlyList<string> Held => _held;
    public int Selected => _selected;

    public bool IsActive(string id) => _active.ContainsKey(id);
    public int RoundsLeft(string id) => _active.TryGetValue(id, out var p) ? p.RoundsLeft : 0;
    public int Price(string id) => Dekken.Get(id).Price;

    /// <summary>Why DRINKING <paramref name="id"/> would do nothing right now ("" = it would work).</summary>
    public string Blocked(string id)
    {
        var def = Dekken.Get(id);
        return def.Duration switch
        {
            PerkDuration.OneUse when id == PerkIds.Heal && _player.Health >= _player.MaxHealth => "FULL HEALTH",
            PerkDuration.Run when _owned.Contains(id) => "OWNED",
            PerkDuration.Rounds when RoundsLeft(id) >= def.Rounds => "ACTIVE",
            _ => "",
        };
    }

    /// <summary>Why <paramref name="id"/> can't be KEPT right now ("" = it can): a whole-run perk is drink-only, one of
    /// a kind at most, and only <see cref="Dekken.CarrySlots"/> in all.</summary>
    public string KeepBlocked(string id)
    {
        if (Dekken.Get(id).Duration == PerkDuration.Run)
            return "DRINK ONLY";
        if (_held.Contains(id))
            return "HELD";
        return _held.Count >= Dekken.CarrySlots ? "POCKETS FULL" : "";
    }

    /// <summary>Buy <paramref name="id"/> and drink it at the machine. False if it isn't stocked, drinking it would do
    /// nothing (<see cref="Blocked"/>), or it's unaffordable.</summary>
    public bool BuyDrink(string id)
    {
        if (!_stock.Contains(id) || Blocked(id) != "" || !_player.Wallet.SpendLira(Price(id)))
            return false;
        Apply(Dekken.Get(id));
        return true;
    }

    /// <summary>Buy <paramref name="id"/> and pocket it for later. False if it isn't stocked, can't be kept
    /// (<see cref="KeepBlocked"/>), or it's unaffordable.</summary>
    public bool BuyKeep(string id)
    {
        if (!_stock.Contains(id) || KeepBlocked(id) != "" || !_player.Wallet.SpendLira(Price(id)))
            return false;
        _held.Add(id);
        _heldChanged(this);
        return true;
    }

    /// <summary>Drink the selected carried vial. Returns what to tell the player: the perk's name if it went down, the
    /// reason if drinking it now would do nothing (it stays in the pocket), or "" if he carries none.</summary>
    public (string Text, bool Drunk) DrinkHeld()
    {
        if (_held.Count == 0)
            return ("", false);
        string id = _held[_selected];
        string blocked = Blocked(id);
        if (blocked != "")
            return (blocked, false);
        _held.RemoveAt(_selected);
        _selected = Mathf.Clamp(_selected, 0, Mathf.Max(_held.Count - 1, 0));
        var def = Dekken.Get(id);
        Apply(def);
        _heldChanged(this);
        return (def.Name, true);
    }

    /// <summary>Select the next carried vial (wraps).</summary>
    public void CycleHeld()
    {
        if (_held.Count < 2)
            return;
        _selected = (_selected + 1) % _held.Count;
        _heldChanged(this);
    }

    /// <summary>A vial goes down: its perk happens (one-use) or starts (timed / whole-run).</summary>
    private void Apply(PerkDef def)
    {
        switch (def.Duration)
        {
            case PerkDuration.OneUse:
                if (def.Id == PerkIds.Heal)
                    _player.Heal(def.Value);
                else if (def.Id == PerkIds.FastTravel)
                    _fastTravel();
                break;
            case PerkDuration.Run:
                _owned.Add(def.Id);
                Activate(def);
                break;
            case PerkDuration.Rounds:
                Activate(def);
                break;
        }
    }

    /// <summary>A round was cleared: timed perks spend a round (those out of rounds end), and the stock rerolls for the
    /// next round.</summary>
    public void OnRoundClear()
    {
        foreach (var (id, perk) in new List<KeyValuePair<string, Perk>>(_active))
            if (perk.Def.Duration == PerkDuration.Rounds && --perk.RoundsLeft <= 0)
            {
                _active.Remove(id);
                _player.RemovePassive(perk);
            }
        RollStock();
        _player.RefreshBuffHud();
    }

    /// <summary>Put a fresh <paramref name="def"/> perk on the player, replacing any running copy (a renewal).</summary>
    private void Activate(PerkDef def)
    {
        if (_active.TryGetValue(def.Id, out var old))
            _player.RemovePassive(old);
        var perk = new Perk(def);
        _active[def.Id] = perk;
        _player.AddPassive(perk);
    }

    /// <summary>Draw a round's stock: up to <see cref="Dekken.StockSize"/> distinct eligible perks — not a whole-run
    /// perk already owned, and not one gated to a special the player hasn't equipped.</summary>
    private void RollStock()
    {
        var pool = new List<string>();
        string special = _player.LoadoutId(LoadoutCategory.Special);
        foreach (PerkDef d in Dekken.Perks)
            if (!_owned.Contains(d.Id) && (d.RequiresSpecial == null || d.RequiresSpecial == special))
                pool.Add(d.Id);
        _stock.Clear();
        while (_stock.Count < Dekken.StockSize && pool.Count > 0)
        {
            int i = (int)(GD.Randi() % (uint)pool.Count);
            _stock.Add(pool[i]);
            pool.RemoveAt(i);
        }
        _stock.Sort((a, b) => Array.FindIndex(Dekken.Perks, d => d.Id == a).CompareTo(Array.FindIndex(Dekken.Perks, d => d.Id == b)));
    }
}
