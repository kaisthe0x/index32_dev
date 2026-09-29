using Godot;

namespace MyGame;

/// <summary>
/// The Needle Point menu (a <see cref="StallMenu"/>, open in the break between rounds): every shot as a row — its name in
/// its LEVEL colour, what it does at that level, its status (active + rounds left), and two actions: <b>BUY</b> (Lira —
/// active now, through the next round; renews an active one) and <b>UPGRADE</b> (figs — +1 level for the run, and
/// granted now).
/// </summary>
public partial class NeedlePointMenu : StallMenu
{
    private const float NameWidth = 136.0f;
    private const float EffectWidth = 232.0f;
    private const float StatusWidth = 152.0f;
    private const float BuyWidth = 70.0f;
    private const float UpgradeWidth = 130.0f;

    private ShotLedger _ledger;

    public void Open(ShotLedger ledger, Player player)
    {
        _ledger = ledger;
        OpenFrame(player, "NEEDLE POINT", "Shots are active now and last through the next round · UPGRADE lasts the run");
    }

    protected override void FillRows(VBoxContainer rows)
    {
        foreach (ShotDef def in NeedlePoint.SHOTS)
            rows.AddChild(Row(def));
    }

    private Control Row(ShotDef def)
    {
        string id = def.Id;
        int level = _ledger.Level(id);
        var (strip, row) = NewRow();
        var name = Cell($"{def.Name}  {Roman(level + 1)}", NameWidth);
        name.AddThemeColorOverride("font_color", NeedlePoint.LevelColor(level)); // the level colour is semantic
        row.AddChild(name);
        string rounds = def.Rounds == 1 ? "1 round" : $"{def.Rounds} rounds";
        row.AddChild(Cell($"{Shot.EffectText(def, level)} · {rounds}", EffectWidth));
        row.AddChild(Cell(Status(id), StatusWidth, UiStyle.Muted));
        row.AddChild(ActionButton($"BUY {_ledger.Price(id)}", BuyWidth,
            _ledger.CanRenew(id) && Player.lira >= _ledger.Price(id), () => _ledger.Buy(id)));
        bool maxed = _ledger.AtMaxLevel(id);
        row.AddChild(ActionButton(maxed ? "MAX" : $"UPGRADE {_ledger.UpgradeFigs(id)} FIGS", UpgradeWidth,
            !maxed && Player.fada_figs >= _ledger.UpgradeFigs(id), () => _ledger.Upgrade(id)));
        return strip;
    }

    /// <summary>What the shot is doing right now, in words.</summary>
    private string Status(string id)
    {
        if (!_ledger.IsActive(id))
            return "";
        int left = _ledger.RoundsLeft(id);
        return left == 1 ? "ACTIVE · 1 ROUND" : $"ACTIVE · {left} ROUNDS";
    }

    private static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };
}
