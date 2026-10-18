using System.Collections.Generic;
using Godot;

namespace MyGame;

/// <summary>
/// A run's LOTTERY STALL state + rules (docs/game-loop.md § The lottery stall; numbers in <see cref="Lottery"/>): this
/// round's pulls and price, the roll, and the items HELD on Khalid (as <see cref="LotteryEffect"/> passives) with what
/// they have left. RunManager owns one per run and tells it when a round starts and when an enemy dies; the stall
/// (<see cref="LotteryStall"/>) pays for a pull with <see cref="TryPull"/> and hands the landed item back to
/// <see cref="Apply"/> once its spin ends.
///
/// <para>Always open, never paused. Each pull is paid up front and can't be declined; the wheel leaves out what
/// wouldn't help right now and keeps heal items to a capped share. A duplicate makes a held item last longer, never
/// stronger; a held item ends when its kills / uses run out.</para>
/// </summary>
public sealed class LotteryLedger
{
    private readonly Player _player;
    private readonly List<LotteryEffect> _held = new();
    private readonly List<LotteryItemDef> _wheel = new(); // reused by each roll
    private int _pulls;                                     // pulls made this round

    public LotteryLedger(Player player) => _player = player;

    /// <summary>Pulls or the price changed (the stall's prompt redraws).</summary>
    public event System.Action? Changed;

    /// <summary>What the next pull costs.</summary>
    public int Price => Lottery.BasePrice + _pulls * Lottery.PriceStep;

    /// <summary>Pulls still allowed this round.</summary>
    public int PullsLeft => Lottery.PullsPerRound - _pulls;

    /// <summary>Every kill's Lira multiplier right now (Bounty).</summary>
    public int LiraMultiplier
    {
        get
        {
            int mult = 1;
            foreach (var e in _held)
                mult = Mathf.Max(mult, e.LiraMultiplier);
            return mult;
        }
    }

    /// <summary>Why a pull can't happen now ("" = it can): no pulls left this round, or not enough Lira.</summary>
    public string PullBlocked()
    {
        if (PullsLeft <= 0)
            return "NO PULLS LEFT";
        return _player.Wallet.Lira < Price ? "NOT ENOUGH LIRA" : "";
    }

    /// <summary>Pay for a pull and decide what it lands on. Null if it's <see cref="PullBlocked"/> or the wheel is empty
    /// (then nothing is paid).</summary>
    public LotteryItemDef? TryPull()
    {
        if (PullBlocked() != "" || Roll() is not { } item || !_player.Wallet.SpendLira(Price))
            return null;
        _pulls += 1;
        Changed?.Invoke();
        return item;
    }

    /// <summary>The orb landed on <paramref name="item"/>: it happens (instant), lasts longer (already held), or starts.</summary>
    public void Apply(LotteryItemDef item)
    {
        var effect = Lottery.Factories[item.Id](item);
        if (item.Measure == LotteryMeasure.Instant)
        {
            effect.Setup(_player);
            return;
        }
        if (Held(item.Id) is { } held)
            held.Extend();
        else
        {
            effect.SpentOut = OnSpentOut;
            _held.Add(effect);
            _player.AddPassive(effect);
        }
        _player.RefreshBuffHud();
    }

    /// <summary>A round started: the pulls and the price reset.</summary>
    public void OnRoundStart()
    {
        _pulls = 0;
        Changed?.Invoke();
    }

    /// <summary>An enemy died: held items react (Tithe), then each kill-measured one counts down and those out of kills
    /// end.</summary>
    public void OnKill()
    {
        if (_held.Count == 0)
            return;
        for (int i = _held.Count - 1; i >= 0; i--)
        {
            var e = _held[i];
            e.OnKill(_player);
            if (e.Def.Measure == LotteryMeasure.Kills && e.CountKill())
                Remove(e);
        }
        _player.RefreshBuffHud();
    }

    /// <summary>A use-spent item ran out inside a player hook: remove it once that hook has finished.</summary>
    private void OnSpentOut(LotteryEffect effect) => Callable.From(() => Remove(effect)).CallDeferred();

    private void Remove(LotteryEffect effect)
    {
        if (!_held.Remove(effect))
            return;
        _player.RemovePassive(effect); // refreshes the buff list
    }

    private LotteryEffect? Held(string id)
    {
        foreach (var e in _held)
            if (e.Id == id)
                return e;
        return null;
    }

    /// <summary>A weighted draw from the items that would help right now: not one gated to a special he hasn't
    /// equipped, not a second primed item, not one that would do nothing (<see cref="LotteryEffect.IsUseful"/>). The
    /// heal items' weight is scaled down when it would exceed <see cref="Lottery.HealShare"/> of the rest, so a heal
    /// stays at most 1 in 5 whatever was left out. Null if nothing qualifies.</summary>
    private LotteryItemDef? Roll()
    {
        _wheel.Clear();
        float healWeight = 0.0f, otherWeight = 0.0f;
        string special = _player.LoadoutId(LoadoutCategory.Special);
        foreach (var item in Lottery.Items)
        {
            if (item.RequiresSpecial is { } needs && needs != special)
                continue;
            if (item.Measure == LotteryMeasure.Primed && Held(item.Id) != null)
                continue;
            if (!Lottery.Factories[item.Id](item).IsUseful(_player))
                continue;
            _wheel.Add(item);
            if (item.Heals)
                healWeight += item.Weight;
            else
                otherWeight += item.Weight;
        }
        if (_wheel.Count == 0)
            return null;
        float healScale = otherWeight > 0.0f && healWeight > otherWeight * Lottery.HealShare
            ? otherWeight * Lottery.HealShare / healWeight
            : 1.0f;
        float pick = GD.Randf() * (otherWeight + healWeight * healScale);
        foreach (var item in _wheel)
        {
            pick -= item.Heals ? item.Weight * healScale : item.Weight;
            if (pick <= 0.0f)
                return item;
        }
        return _wheel[^1];
    }
}
