using Godot;

namespace MyGame;

/// <summary>
/// A glowing "soul" that pops off a struck enemy and floats to the player — the visual receipt for a Ruh gain (the Ruh
/// itself is banked at the hit). Flies the shared <see cref="ArcFlight"/> curve; on arrival fires the player's absorb
/// reaction. RunManager instantiates the scene + calls <see cref="launch"/>.
/// </summary>
public partial class RuhOrb : ArcFlight
{
    private bool _completedCharge;

    /// <summary>Send this orb curving toward `target` (the player). `completedCharge` = the soul that topped off a full charge.</summary>
    public void launch(Node2D target, bool completedCharge)
    {
        _completedCharge = completedCharge;
        Fly(target);
    }

    protected override void OnArrived(Node2D target)
    {
        if (target is Player p)
            p.on_ruh_absorbed(_completedCharge);
    }
}
