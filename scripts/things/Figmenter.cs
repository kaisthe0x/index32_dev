using Godot;

namespace MyGame;

/// <summary>
/// The FIGMENTER (<c>scenes/things/figmenter.tscn</c>) — a steel vat that turns Fada Figs into fig wine (it was the
/// "mystery box" until 2026-10-10). A <see cref="Stall"/> that stays where the layout puts it. Rules + numbers:
/// <see cref="FigmenterRules"/>; the run's state: <see cref="FigmenterLedger"/>. Press <b>E</b> at it:
/// <list type="bullet">
/// <item><b>Empty</b> — pay figs to start a brew.</item>
/// <item><b>Brewing</b> — it says how many rounds are left; the tank fills a step each round.</item>
/// <item><b>Ready</b> — the wine's name and what it does hang over it; E collects it into the wine slot.</item>
/// </list>
/// It only shows the ledger: it redraws when the ledger changes and does nothing per frame.
/// </summary>
public partial class Figmenter : Stall
{
    private const float TextWidth = 260.0f;
    private static readonly Vector2 NoticeOffset = new(0, -40);
    private static readonly Color NoticeColor = new(0.72f, 0.72f, 0.78f);
    private static readonly Color ReadyColor = new(1.6f, 1.35f, 0.35f);   // HDR gold: a wine is waiting

    private FigmenterLedger _ledger = null!;
    private Sprite2D _art = null!;
    private Label _title = null!, _detail = null!;
    private Sfx? _sfx;
    private BrewState _shown = BrewState.Empty; // the state last drawn (to notice a brew finishing)

    /// <summary>This run's Figmenter state and rules (set by RunManager).</summary>
    public FigmenterLedger Ledger
    {
        get => _ledger;
        set
        {
            _ledger = value;
            _shown = _ledger.State;
            _ledger.Changed += Refresh;
            Refresh();
        }
    }

    public override void _Ready()
    {
        base._Ready();
        _sfx = GetNodeOrNull<Sfx>("/root/Sfx");
        _art = Visual.GetNode<Sprite2D>("Art");
        Vector2 prompt = GetNode<Marker2D>("Prompt").Position;
        _title = MakeText(prompt + new Vector2(0, -34), 16, ReadyColor);
        _detail = MakeText(prompt + new Vector2(0, -14), 8, Colors.White);
    }

    protected override void Interact(Player p)
    {
        switch (_ledger.State)
        {
            case BrewState.Empty:
                string blocked = _ledger.BrewBlocked();
                if (blocked != "" || !_ledger.StartBrew())
                {
                    Notice(blocked);
                    return;
                }
                Pop();
                _sfx?.PlayAt("figmenter_brew", GlobalPosition);
                Notice("BREWING");
                break;
            case BrewState.Brewing:
                Notice(RoundsText(_ledger.RoundsLeft));
                break;
            case BrewState.Ready:
                string full = _ledger.CollectBlocked();
                if (full != "" || _ledger.Ready is not { } wine || !_ledger.Collect())
                {
                    Notice(full);
                    return;
                }
                Pop();
                _sfx?.PlayAt("wine_collect", GlobalPosition);
                FloatingText.Emit(FloatingTextType.Damage, this, NoticeOffset, wine.Name, 0.0f, ReadyColor);
                break;
        }
    }

    /// <summary>Show the ledger: the tank's fill frame, and the text over it (rounds left while brewing, the wine's
    /// label once it is ready).</summary>
    private void Refresh()
    {
        _art.Frame = Mathf.RoundToInt(_ledger.Fill * (_art.Hframes - 1)); // frame 0 = empty … the last = full
        Wine? ready = _ledger.Ready;
        _title.Visible = ready != null;
        _title.Text = ready?.Name ?? "";
        _detail.Visible = _ledger.State != BrewState.Empty;
        _detail.Text = ready?.Description ?? RoundsText(_ledger.RoundsLeft);
        if (_shown == BrewState.Brewing && _ledger.State == BrewState.Ready)
        {
            Pop();
            _sfx?.PlayAt("figmenter_ready", GlobalPosition);
        }
        _shown = _ledger.State;
    }

    private static string RoundsText(int rounds) => rounds == 1 ? "1 ROUND" : $"{rounds} ROUNDS";

    /// <summary>A short grey line that floats up from the Figmenter (why a press did nothing, or what it started).</summary>
    private void Notice(string text)
    {
        if (text != "")
            FloatingText.Emit(FloatingTextType.Damage, this, NoticeOffset, text, 0.0f, NoticeColor);
    }

    /// <summary>A centred, outlined line of text over the Figmenter (hidden until there is something to say).</summary>
    private Label MakeText(Vector2 at, int size, Color color)
    {
        var label = new Label
        {
            Position = at - new Vector2(TextWidth / 2.0f, 0.0f),
            Size = new Vector2(TextWidth, 0.0f),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Visible = false,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 4);
        AddChild(label);
        return label;
    }
}
