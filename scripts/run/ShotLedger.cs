using System;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// A run's Needle Point state + rules (docs/game-loop.md § Economy): each shot's upgrade LEVEL (kept for the run) and
/// which shots are ACTIVE on Khalid (as <see cref="Shot"/> passives). RunManager owns one per run and calls
/// <see cref="OnRoundClear"/>; the stall's menu calls <see cref="Buy"/> / <see cref="Upgrade"/>. Prices/values are data
/// in <see cref="NeedlePoint"/>.
///
/// <para>The stall is open only in the BREAK between rounds (<see cref="Open"/>). <b>Buy</b> (Lira) makes the shot
/// active at once, lasting through the next <see cref="ShotDef.Rounds"/> rounds; buying it again renews it to full —
/// never stacks, so there's no prepaying. <b>Upgrade</b> (figs) raises its level for the run and grants it at the new
/// level, the same way. Each round clear spends a round; a shot out of rounds ends.</para>
/// </summary>
public sealed class ShotLedger
{
    private readonly Player _player;
    private readonly Func<bool> _inBreak;                            // is it the break between rounds right now?
    private readonly Dictionary<string, int> _level = new();         // shot id → upgrade level (0 = base)
    private readonly Dictionary<string, Shot> _active = new();       // shot id → the Shot on the player

    public ShotLedger(Player player, Func<bool> inBreak)
    {
        _player = player;
        _inBreak = inBreak;
    }

    /// <summary>Whether Needle Point is open (the break between rounds).</summary>
    public bool Open => _inBreak();

    public int Level(string id) => _level.GetValueOrDefault(id);
    public bool IsActive(string id) => _active.ContainsKey(id);

    /// <summary>Rounds the active shot still covers (0 if it isn't active).</summary>
    public int RoundsLeft(string id) => _active.TryGetValue(id, out var s) ? s.RoundsLeft : 0;

    /// <summary>Whether buying <paramref name="id"/> would do anything — false while it's already at its full duration.</summary>
    public bool CanRenew(string id) => RoundsLeft(id) < NeedlePoint.Get(id).Rounds;

    public int Price(string id) => NeedlePoint.PriceAt(NeedlePoint.Get(id), Level(id));
    public bool AtMaxLevel(string id) => Level(id) >= NeedlePoint.MaxLevel(NeedlePoint.Get(id));
    public int UpgradeFigs(string id) => NeedlePoint.UpgradeFigs(Level(id));

    /// <summary>Buy <paramref name="id"/>: active now, for its full duration. False if the stall is closed, the shot is
    /// already at full duration, or it's unaffordable.</summary>
    public bool Buy(string id)
    {
        if (!Open || !CanRenew(id) || !_player.spend_lira(Price(id)))
            return false;
        Activate(id);
        return true;
    }

    /// <summary>Spend figs to raise <paramref name="id"/>'s level for the run, and grant it at that level (active now,
    /// full duration). False if the stall is closed, it's maxed, or it's unaffordable.</summary>
    public bool Upgrade(string id)
    {
        if (!Open || AtMaxLevel(id) || !_player.spend_fada_figs(UpgradeFigs(id)))
            return false;
        _level[id] = Level(id) + 1;
        Activate(id);
        return true;
    }

    /// <summary>A round was cleared: every active shot spends a round; those out of rounds end.</summary>
    public void OnRoundClear()
    {
        foreach (var (id, shot) in new List<KeyValuePair<string, Shot>>(_active))
            if (--shot.RoundsLeft <= 0)
            {
                _active.Remove(id);
                _player.remove_passive(shot);
            }
        _player.refresh_buff_hud();
    }

    /// <summary>Put <paramref name="id"/> on the player at its current level for its full duration, replacing any copy.</summary>
    private void Activate(string id)
    {
        if (_active.TryGetValue(id, out var old))
            _player.remove_passive(old);
        var def = NeedlePoint.Get(id);
        var shot = new Shot(def, Level(id), def.Rounds);
        _active[id] = shot;
        _player.add_passive(shot);
    }
}
