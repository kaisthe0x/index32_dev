using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// The carried Dekken vials in the player's hands: Q / RB drinks the selected one, Tab / LB selects the next, and the
/// HUD shows what he carries. The rules — what a vial does, how many he can carry — are the run's
/// <see cref="PerkLedger"/>; this is only the input and the feedback. A child of <see cref="RunManager"/>, which hands
/// it each new run's ledger.
/// </summary>
public partial class VialControls : Node
{
    private const string DrinkAction = "vial_drink"; // Q / RB — drink the selected carried vial
    private const string CycleAction = "vial_cycle"; // Tab / LB — select the next carried vial
    private static readonly Vector2 TextOffset = new(0, -52);
    private static readonly Color DrunkColor = new(1.0f, 0.78f, 0.25f);
    private static readonly Color BlockedColor = new(0.72f, 0.72f, 0.78f);

    private readonly Player _player;
    private readonly Sfx _sfx;

    /// <summary>This run's perks (set by RunManager when it builds an arena).</summary>
    public PerkLedger? Ledger { get; set; }

    public VialControls(Player player, Sfx sfx)
    {
        _player = player;
        _sfx = sfx;
    }

    public override void _Ready() => EnsureActions();

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(DrinkAction))
            Drink();
        else if (@event.IsActionPressed(CycleAction))
            Ledger?.CycleHeld();
    }

    /// <summary>Register the vial actions (physical keys = layout-independent, + the pad bumpers) if the project doesn't
    /// define them — like the stalls' <c>interact</c>, so they work without a project.godot edit.</summary>
    private static void EnsureActions()
    {
        AddAction(DrinkAction, Key.Q, JoyButton.RightShoulder);
        AddAction(CycleAction, Key.Tab, JoyButton.LeftShoulder);
    }

    private static void AddAction(string action, Key key, JoyButton button)
    {
        if (InputMap.HasAction(action))
            return;
        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
    }

    /// <summary>Drink the selected carried vial (instant) and say so over Khalid — its name, or why it didn't go down
    /// (e.g. FULL HEALTH: the vial stays in the pocket).</summary>
    private void Drink()
    {
        if (Ledger == null || _player.IsDead())
            return;
        var (text, drunk) = Ledger.DrinkHeld();
        if (text == "")
            return;
        FloatingText.Emit(FloatingTextType.Damage, _player, TextOffset, text, 0.0f, drunk ? DrunkColor : BlockedColor);
        if (drunk)
            _sfx.Play("buff_select"); // PLACEHOLDER cue
    }

    /// <summary>Show <paramref name="ledger"/>'s carried vials in the HUD (it calls this whenever they change — and once
    /// when a run's ledger is created, which clears the last run's).</summary>
    public void ShowVials(PerkLedger ledger)
    {
        var names = new List<string>();
        foreach (string id in ledger.Held)
            names.Add(Dekken.Get(id).Name);
        GetNodeOrNull<HUD>("/root/HUD")?.SetVials(names, ledger.Selected);
    }
}
