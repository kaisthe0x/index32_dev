using Godot;

namespace MyGame;

/// <summary>
/// The Dekken menu (a <see cref="StallMenu"/>): this round's stock of perks as rows —
/// name, what it does + how long it lasts, its status (active / owned / why it can't be bought), and <b>BUY</b> (Lira —
/// takes effect at once).
/// </summary>
public partial class DekkenMenu : StallMenu
{
    private const float NameWidth = 104.0f;
    private const float DescWidth = 296.0f;
    private const float LastsWidth = 88.0f;
    private const float StatusWidth = 136.0f;
    private const float BuyWidth = 70.0f;

    private PerkLedger _ledger;

    public void Open(PerkLedger ledger, Player player)
    {
        _ledger = ledger;
        OpenFrame(player, "DEKKEN", "Perks take effect now · new stock every round");
    }

    protected override void FillRows(VBoxContainer rows)
    {
        foreach (string id in _ledger.Stock)
            rows.AddChild(Row(Dekken.Get(id)));
    }

    private Control Row(PerkDef def)
    {
        string id = def.Id;
        var (strip, row) = NewRow();
        row.AddChild(Cell(def.Name, NameWidth));
        row.AddChild(Cell(def.Description, DescWidth));
        row.AddChild(Cell(Lasts(def), LastsWidth, UiStyle.Muted));
        string blocked = _ledger.Blocked(id);
        row.AddChild(Cell(Status(id, blocked), StatusWidth, UiStyle.Muted));
        row.AddChild(ActionButton($"BUY {_ledger.Price(id)}", BuyWidth,
            blocked == "" && Player.lira >= _ledger.Price(id), () => _ledger.Buy(id)));
        return strip;
    }

    private static string Lasts(PerkDef def) => def.Duration switch
    {
        PerkDuration.OneUse => "now",
        PerkDuration.Run => "whole run",
        _ => def.Rounds switch { 1 => "this round", 2 => "this + next", _ => $"this + {def.Rounds - 1} more" },
    };

    /// <summary>The perk's state in words: why it can't be bought, else how long it has left if it's running.</summary>
    private string Status(string id, string blocked)
    {
        if (blocked != "" && blocked != "ACTIVE")
            return blocked;
        if (!_ledger.IsActive(id))
            return "";
        int left = _ledger.RoundsLeft(id);
        return left == 1 ? "ACTIVE · 1 ROUND" : $"ACTIVE · {left} ROUNDS";
    }
}
