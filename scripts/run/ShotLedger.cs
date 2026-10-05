using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// A run's Needle Point state + rules (docs/game-loop.md § Economy): how many ranks of each shot Khalid owns, and the
/// <see cref="Shot"/> passive that applies it. RunManager owns one per run; the stall's menu calls <see cref="Buy"/>.
/// Prices/values are data in <see cref="NeedlePoint"/>.
///
/// <para>Each <b>Buy</b> (Lira) raises a shot one rank, permanently for the run, at a higher price than the last — until
/// it's maxed. The stall is always open.</para>
/// </summary>
public sealed class ShotLedger
{
    private readonly Player _player;
    private readonly Dictionary<string, Shot> _owned = new();        // shot id → the Shot on the player (its rank)

    public ShotLedger(Player player) => _player = player;

    /// <summary>Ranks of <paramref name="id"/> owned (0 = never bought).</summary>
    public int Rank(string id) => _owned.TryGetValue(id, out var s) ? s.Rank : 0;

    public bool Maxed(string id) => Rank(id) >= NeedlePoint.Get(id).MaxRank;

    /// <summary>The Lira price of <paramref name="id"/>'s next rank.</summary>
    public int Price(string id) => NeedlePoint.PriceOfNext(NeedlePoint.Get(id), Rank(id));

    /// <summary>Buy <paramref name="id"/>'s next rank — permanent for the run. False if the shot is maxed or it's
    /// unaffordable.</summary>
    public bool Buy(string id)
    {
        if (Maxed(id) || !_player.spend_lira(Price(id)))
            return false;
        int rank = Rank(id) + 1;
        if (_owned.TryGetValue(id, out var old))
            _player.remove_passive(old);
        var shot = new Shot(NeedlePoint.Get(id), rank);
        _owned[id] = shot;
        _player.add_passive(shot);
        return true;
    }
}
