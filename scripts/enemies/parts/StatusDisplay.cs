using Godot;

namespace MyGame;

/// <summary>
/// How an enemy SHOWS the statuses it is under: the pips beside its health bar (<see cref="StatusIcons"/>), the halo
/// over its head (<see cref="OverheadStatus"/>), and a coloured flash over its sprite (<see cref="StatusOverlay"/>).
/// Which statuses apply is the enemy's business; it tells this every tick.
/// </summary>
public sealed class StatusDisplay
{
    private const float IconGap = 3.0f;   // px between the health bar's right end and the first pip

    private readonly StatusOverlay _overlay = new();
    private readonly StatusIcons _icons = new();
    private readonly OverheadStatus _overhead = new();
    private int _shown;   // the reap / stun / charm flags currently displayed

    /// <summary>Adds the three display nodes to <paramref name="body"/>, placed against its health bar and head.</summary>
    public StatusDisplay(Node2D body, AnimatedSprite2D sprite, FloatingHealthBar bar, float headY)
    {
        body.AddChild(_overlay);
        _overlay.Setup(sprite);
        body.AddChild(_icons);
        _icons.Position = bar.Position + new Vector2(bar.BarWidth / 2.0f + IconGap, -bar.BarHeight / 2.0f);
        body.AddChild(_overhead);
        _overhead.Setup(headY);
    }

    /// <summary>Show exactly these statuses. Runs every tick, so it compares three flags and only rebuilds the list
    /// when one of them changed.</summary>
    public void Show(bool reap, bool stun, bool charm)
    {
        int flags = (reap ? 1 : 0) | (stun ? 2 : 0) | (charm ? 4 : 0);
        if (flags == _shown)
            return;
        _shown = flags;
        var ids = new List<StatusType>();
        if (reap) ids.Add(StatusType.Reap);
        if (stun) ids.Add(StatusType.Stun);
        if (charm) ids.Add(StatusType.Charm);
        _icons.SetActive(ids);
        _overhead.SetActive(ids);
    }

    /// <summary>Tint the sprite <paramref name="color"/> for <paramref name="seconds"/> (a hit that carries a status colour).</summary>
    public void Flash(Color color, float seconds) => _overlay.ShowFor(color, seconds);

    /// <summary>Take everything down (the enemy died).</summary>
    public void Clear()
    {
        _shown = 0;
        _icons.SetActive(new List<StatusType>());
        _overhead.SetActive(new List<StatusType>());
        _overlay.Clear();
    }
}
