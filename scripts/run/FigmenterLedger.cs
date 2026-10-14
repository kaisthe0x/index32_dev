using System.Collections.Generic;
using Godot;

namespace MyGame;

/// <summary>
/// A run's FIGMENTER state + rules (docs/game-loop.md § Economy; numbers in <see cref="FigmenterRules"/>): the brew in
/// the machine, and the one bottle of wine the player can carry. RunManager owns one per run and calls
/// <see cref="OnRoundClear"/>; the machine (<see cref="Figmenter"/>) calls <see cref="StartBrew"/> and
/// <see cref="Collect"/>; the wine key (<see cref="WineControls"/>) calls <see cref="DrinkCarried"/>. Everyone who
/// shows any of it listens to <see cref="Changed"/>.
///
/// <para>The wine's contents are decided as the brew STARTS (so paying is never wasted on an empty pool) and shown
/// when it is READY. It never duplicates a buff the player holds or the one in the bottle he is carrying. Only a
/// carried wine ages — one waiting in the machine stays as it is.</para>
/// </summary>
public sealed class FigmenterLedger
{
    private readonly Player _player;
    private Wine? _brew;        // what is in the machine: brewing, or ready once _roundsBrewed reaches BrewRounds
    private int _roundsBrewed;
    private Wine? _carried;     // the bottle in the player's wine slot
    private int _roundsCarried;

    /// <summary>The machine or the carried bottle changed (a brew started / advanced / finished, a wine was collected,
    /// aged or drunk).</summary>
    public event System.Action? Changed;

    public FigmenterLedger(Player player) => _player = player;

    public BrewState State => _brew == null ? BrewState.Empty
        : _roundsBrewed < FigmenterRules.BrewRounds ? BrewState.Brewing : BrewState.Ready;

    /// <summary>How far the brew is, 0 (empty / just started) to 1 (ready) — the machine's fill level.</summary>
    public float Fill => _brew == null ? 0.0f : (float)_roundsBrewed / FigmenterRules.BrewRounds;

    /// <summary>Rounds still to clear before the brew is ready (0 unless brewing).</summary>
    public int RoundsLeft => State == BrewState.Brewing ? FigmenterRules.BrewRounds - _roundsBrewed : 0;

    /// <summary>The finished wine waiting in the machine (null unless <see cref="BrewState.Ready"/>).</summary>
    public Wine? Ready => State == BrewState.Ready ? _brew : null;

    /// <summary>The bottle the player carries (null if none), and how aged it is.</summary>
    public Wine? Carried => _carried;
    public WineRarity CarriedRarity =>
        (WineRarity)Mathf.Min(_roundsCarried / FigmenterRules.AgeRounds, (int)WineRarity.Epic);

    /// <summary>Why a brew can't start right now ("" = it can): nothing left to make, or not enough figs.</summary>
    public string BrewBlocked()
    {
        if (BuffPool().Count == 0 && SpecialPool().Count == 0)
            return "EMPTY";
        return _player.Wallet.FadaFigs < FigmenterRules.Cost ? $"NEED {FigmenterRules.Cost}" : "";
    }

    /// <summary>Pay for a brew and start it. False if the machine is busy, it's <see cref="BrewBlocked"/>, or the figs
    /// aren't there.</summary>
    public bool StartBrew()
    {
        if (State != BrewState.Empty || BrewBlocked() != "" || !_player.Wallet.SpendFadaFigs(FigmenterRules.Cost))
            return false;
        _brew = Roll();
        _roundsBrewed = 0;
        Changed?.Invoke();
        return true;
    }

    /// <summary>A round was cleared: the brew fills a step, and a carried wine ages.</summary>
    public void OnRoundClear()
    {
        if (State == BrewState.Brewing)
            _roundsBrewed += 1;
        if (_carried != null)
            _roundsCarried += 1;
        Changed?.Invoke();
    }

    /// <summary>Why the ready wine can't be collected ("" = it can): the wine slot is taken.</summary>
    public string CollectBlocked() => _carried != null ? "DRINK YOUR WINE FIRST" : "";

    /// <summary>Move the ready wine from the machine into the player's wine slot (as a Common). False if none is ready
    /// or the slot is taken.</summary>
    public bool Collect()
    {
        if (Ready is not { } wine || CollectBlocked() != "")
            return false;
        _carried = wine;
        _roundsCarried = 0;
        _brew = null;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Drink the carried wine: its buff joins his passives for the run at the wine's rarity, or its special
    /// replaces his. Returns the wine and the rarity it was drunk at, or null if he carries none.</summary>
    public (Wine Wine, WineRarity Rarity)? DrinkCarried()
    {
        if (_carried is not { } wine)
            return null;
        WineRarity rarity = CarriedRarity;
        if (wine.Kind == WineKind.Buff && BuffCatalog.Make(wine.Id, rarity) is { } buff)
            _player.AddPassive(buff);
        else if (wine.Kind == WineKind.Special)
            _player.Equip(LoadoutCategory.Special, wine.Id);
        _carried = null;
        Changed?.Invoke();
        return (wine, rarity);
    }

    /// <summary>Decide what a new brew will hold: rarely a special-swap, otherwise a buff.</summary>
    private Wine Roll()
    {
        var buffs = BuffPool();
        var specials = SpecialPool();
        if (specials.Count > 0 && (buffs.Count == 0 || GD.Randf() < FigmenterRules.SpecialChance))
        {
            string id = specials[(int)(GD.Randi() % (uint)specials.Count)];
            Action? special = Actions.GetAction(_player.Character, "specials", id);
            return new Wine(WineKind.Special, id, special?.Name ?? id, $"SPECIAL — replaces yours. {special?.Description}");
        }
        string buffId = buffs[(int)(GD.Randi() % (uint)buffs.Count)];
        var info = BuffCatalog.Info[buffId];
        return new Wine(WineKind.Buff, buffId, info.Name, info.Desc);
    }

    /// <summary>The buffs a new brew may hold: the catalog's pool for him, minus the one he is carrying.</summary>
    private List<string> BuffPool()
    {
        var pool = BuffCatalog.Pool(_player);
        if (_carried is { Kind: WineKind.Buff } carried)
            pool.Remove(carried.Id);
        return pool;
    }

    /// <summary>The Figmenter-only specials he neither has equipped nor carries.</summary>
    private List<string> SpecialPool()
    {
        string current = _player.LoadoutId(LoadoutCategory.Special);
        var pool = new List<string>();
        foreach (string id in FigmenterRules.Specials)
            if (id != current && !(_carried is { Kind: WineKind.Special } carried && carried.Id == id))
                pool.Add(id);
        return pool;
    }
}
