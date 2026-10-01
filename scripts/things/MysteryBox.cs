using Godot;

namespace MyGame;

/// <summary>
/// A MYSTERY BOX (<c>scenes/things/mystery_box.tscn</c> — placeholder art: a purple "?" crate), a <see cref="Stall"/>: stand next to it and press <b>E</b> to
/// spend <see cref="Cost"/> fada_figs for a gamble. It is DELIBERATELY stingy: <see cref="DudChanceBase"/>
/// of pulls give nothing; on a WIN it fires <see cref="won"/> and RunManager opens a pick-1-of-3 menu of POWERFUL
/// buffs (a `RewardUI`, above-rare tiers). Every win raises the dud chance further (capped by
/// <see cref="DudChanceCap"/>), so repeat wins get rarer within a run. One box spawns per arena
/// (RunManager.BuildArena) — its escalation is that box's.
/// </summary>
public partial class MysteryBox : Stall
{
    /// <summary>A pull beat the dud roll — RunManager opens the 3-choice powerful buff menu.</summary>
    [Signal] public delegate void wonEventHandler();

    private const int Cost = 8;                  // fada_figs spent per pull (figs are rare — ~10 % of kills)
    private const float DudChanceBase = 0.2f;    // chance a pull gives nothing (tune here)
    private const float DudChanceGrowth = 0.01f; // + per win, so wins get rarer
    private const float DudChanceCap = 0.995f;

    private float _dudChance = DudChanceBase;

    protected override void Interact(Player p)
    {
        var sfx = GetNodeOrNull<Sfx>("/root/Sfx");
        if (!p.spend_fada_figs(Cost))
        {
            FloatingText.Emit(FloatingTextType.Damage, this, new Vector2(0, -40), $"NEED {Cost}", 0.0f, new Color(0.72f, 0.72f, 0.78f));
            return;
        }
        Pop();
        if (GD.Randf() < _dudChance)
        {
            FloatingText.Emit(FloatingTextType.Damage, this, new Vector2(0, -40), "…nothing", 0.0f, new Color(0.6f, 0.6f, 0.66f));
            sfx?.play_at("fada_fig_collect", GlobalPosition); // PLACEHOLDER sfx
            return;
        }
        // Win: hand off to RunManager to open the 3-choice POWERFUL buff menu.
        sfx?.play_at("fada_fig_collect", GlobalPosition); // PLACEHOLDER sfx
        _dudChance = Mathf.Min(DudChanceCap, _dudChance + DudChanceGrowth); // each win makes the next pull rarer
        EmitSignal(SignalName.won);
    }
}
