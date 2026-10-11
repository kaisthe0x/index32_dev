using Godot;

namespace MyGame;

/// <summary>
/// The carried bottle of fig wine in the player's hands: F / D-pad up drinks it, and the HUD shows what he carries and
/// how aged it is. The rules — what a wine holds, how it ages — are the run's <see cref="FigmenterLedger"/>; this is
/// only the input and the feedback. A child of <see cref="RunManager"/>, which hands it each new run's ledger.
/// </summary>
public partial class WineControls : Node
{
    private const string DrinkAction = "wine_drink"; // F / D-pad up — drink the carried wine
    private static readonly Vector2 TextOffset = new(0, -52);

    private readonly Player _player;
    private readonly Sfx _sfx;
    private FigmenterLedger? _ledger;

    /// <summary>This run's Figmenter state (set by RunManager when it builds an arena). Setting it shows the new run's
    /// (empty) wine slot and keeps it current.</summary>
    public FigmenterLedger? Ledger
    {
        get => _ledger;
        set
        {
            if (_ledger != null)
                _ledger.Changed -= ShowWine;
            _ledger = value;
            if (_ledger != null)
                _ledger.Changed += ShowWine;
            ShowWine();
        }
    }

    public WineControls(Player player, Sfx sfx)
    {
        _player = player;
        _sfx = sfx;
    }

    public override void _Ready() => EnsureAction();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(DrinkAction))
            Drink();
    }

    /// <summary>Register the wine action (a physical key = layout-independent, + a pad button) if the project doesn't
    /// define it — like the stalls' <c>interact</c>, so it works without a project.godot edit.</summary>
    private static void EnsureAction()
    {
        if (InputMap.HasAction(DrinkAction))
            return;
        InputMap.AddAction(DrinkAction);
        InputMap.ActionAddEvent(DrinkAction, new InputEventKey { PhysicalKeycode = Key.F });
        InputMap.ActionAddEvent(DrinkAction, new InputEventJoypadButton { ButtonIndex = JoyButton.DpadUp });
    }

    /// <summary>Drink the carried wine (instant) and say so over Khalid — its name, in its rarity's colour.</summary>
    private void Drink()
    {
        if (_ledger == null || _player.IsDead() || _ledger.DrinkCarried() is not { } drunk)
            return;
        FloatingText.Emit(FloatingTextType.Damage, _player, TextOffset, drunk.Wine.Name, 0.0f, FigmenterRules.RarityColor(drunk.Rarity));
        _sfx.Play("wine_drink");
    }

    /// <summary>Show the carried wine (or the empty slot) in the HUD.</summary>
    private void ShowWine()
    {
        if (!IsInsideTree())
            return;
        GetNodeOrNull<HUD>("/root/HUD")?.SetWine(_ledger?.Carried, _ledger?.CarriedRarity ?? WineRarity.Common);
    }
}
