using Godot;

namespace MyGame;

/// <summary>
/// The Dekken menu (a <see cref="StallMenu"/>): this round's stock of vials as rows — name, what it does + how long
/// it lasts, its status (active / held / why it can't be drunk), and two ways to buy it (Lira): <b>DRINK</b> (takes
/// effect at once) or <b>KEEP</b> (pocket it — <see cref="Dekken.CarrySlots"/> at most, one of a kind — to drink later).
/// </summary>
public partial class DekkenMenu : StallMenu
{
    private const float NameWidth = 104.0f;
    private const float DescWidth = 280.0f;
    private const float LastsWidth = 80.0f;
    private const float StatusWidth = 136.0f;
    private const float DrinkWidth = 90.0f;
    private const float KeepWidth = 82.0f;

    private PerkLedger _ledger;

    public void Open(PerkLedger ledger, Player player)
    {
        _ledger = ledger;
        OpenFrame(player, "DEKKEN", $"Drink now, or keep up to {Dekken.CarrySlots} (one of a kind) · Q drinks · TAB switches");
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
        string keepBlocked = _ledger.KeepBlocked(id);
        bool affordable = Player.lira >= _ledger.Price(id);
        row.AddChild(Cell(Status(id, blocked), StatusWidth, UiStyle.Muted));
        row.AddChild(ActionButton($"DRINK {_ledger.Price(id)}", DrinkWidth, blocked == "" && affordable, () => _ledger.BuyDrink(id)));
        string keepText = def.Duration == PerkDuration.Run ? "—" : _ledger.Held.Contains(id) ? "HELD" : $"KEEP {_ledger.Price(id)}";
        row.AddChild(ActionButton(keepText, KeepWidth, keepBlocked == "" && affordable, () => _ledger.BuyKeep(id)));
        return strip;
    }

    private static string Lasts(PerkDef def) => def.Duration switch
    {
        PerkDuration.OneUse => "instant",
        PerkDuration.Run => "whole run",
        _ => def.Rounds == 1 ? "1 round" : $"{def.Rounds} rounds",
    };

    /// <summary>The perk's state in words: why it can't be drunk now, else how long it has left if it's running.</summary>
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
