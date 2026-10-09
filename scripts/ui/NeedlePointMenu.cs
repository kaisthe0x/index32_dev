using Godot;

namespace MyGame;

/// <summary>
/// The Needle Point menu (a <see cref="StallMenu"/>): every shot as a row — its name +
/// rank in the rank's colour, what it gives now and what the next rank gives, and <b>BUY</b> (Lira — the next rank, for
/// the rest of the run; <b>MAXED</b> once there's none).
/// </summary>
public partial class NeedlePointMenu : StallMenu
{
    private const float NameWidth = 152.0f;
    private const float NowWidth = 208.0f;
    private const float NextWidth = 104.0f;
    private const float BuyWidth = 80.0f;

    private ShotLedger _ledger = null!;

    public void Open(ShotLedger ledger, Player player)
    {
        _ledger = ledger;
        OpenFrame(player, "NEEDLE POINT", "Shots last the whole run · each rank costs more than the last");
    }

    protected override void FillRows(VBoxContainer rows)
    {
        foreach (ShotDef def in NeedlePoint.Shots)
            rows.AddChild(Row(def));
    }

    private Control Row(ShotDef def)
    {
        string id = def.Id;
        int rank = _ledger.Rank(id);
        bool maxed = _ledger.Maxed(id);
        var (strip, row) = NewRow();
        var name = Cell(rank == 0 ? def.Name : $"{def.Name}  {Shot.Roman(rank)}", NameWidth);
        if (rank > 0)
            name.AddThemeColorOverride("font_color", NeedlePoint.RankColor(rank)); // the rank colour is semantic
        row.AddChild(name);
        // What you have → what the next rank gives.
        row.AddChild(Cell(rank == 0 ? "" : Shot.EffectText(def, rank), NowWidth));
        row.AddChild(Cell(maxed ? "" : $"→ {Shot.FormatValue(def, rank + 1)}", NextWidth, UiStyle.Muted));
        row.AddChild(ActionButton(maxed ? "MAXED" : $"BUY {_ledger.Price(id)}", BuyWidth,
            !maxed && Player.Wallet.Lira >= _ledger.Price(id), () => _ledger.Buy(id)));
        return strip;
    }
}
