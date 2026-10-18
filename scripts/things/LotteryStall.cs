using Godot;

namespace MyGame;

/// <summary>
/// The LOTTERY STALL (<c>scenes/things/lottery_stall.tscn</c> — the owner's dome-and-beam machine), a
/// <see cref="Stall"/>: press E to PAY for a pull, the orb in the beam spins for <see cref="Lottery.SpinTime"/> and
/// lands on an item that takes effect at once. Live — the game never pauses for it, so a pull is taken under fire. The
/// rules (price, pulls, the wheel) are the run's <see cref="LotteryLedger"/> (set by RunManager); this is only the
/// press, the spin and the feedback. The prompt shows the next price and the pulls left this round.
///
/// <para>There is no icon per item yet, so the spin flickers the items' NAMES at the orb (<c>Visual/Orb</c>, a marker)
/// and the landed one pops there — a PLACEHOLDER until the icons exist.</para>
/// </summary>
public partial class LotteryStall : Stall
{
    private const string OrbNode = "Orb";       // where the item shows in the beam, under the scene's Visual node
    private static readonly Vector2 OrbTextSize = new(120, 16); // the spinning name is centred on the orb in this box
    private const int OrbTextFontSize = 11;
    private const int SpinSteps = 14;           // name changes in one spin (they slow down towards the landing)
    private const string LandCue = "buff_select";
    private static readonly Vector2 TextOffset = new(0, -52);
    private static readonly Color LandedColor = new(1.0f, 0.78f, 0.25f);
    private static readonly Color BlockedColor = new(0.72f, 0.72f, 0.78f);

    private Vector2 _orb;          // the orb's position in this stall's space
    private Label _spinText = null!; // the names flickering past while the orb spins
    private Sfx _sfx = null!;
    private LotteryLedger _ledger = null!;
    private bool _spinning;

    /// <summary>This run's lottery (set by RunManager when it builds the arena).</summary>
    public LotteryLedger Ledger
    {
        get => _ledger;
        set
        {
            _ledger = value;
            _ledger.Changed += RefreshPrompt;
            RefreshPrompt();
        }
    }

    public override void _Ready()
    {
        base._Ready();
        _orb = Visual.Position + Visual.GetNode<Marker2D>(OrbNode).Position;
        _sfx = GetNode<Sfx>("/root/Sfx");
        _spinText = UiStyle.HudLabel(UiStyle.HudMuted);
        _spinText.HorizontalAlignment = HorizontalAlignment.Center;
        _spinText.VerticalAlignment = VerticalAlignment.Center;
        _spinText.AddThemeFontSizeOverride("font_size", OrbTextFontSize);
        _spinText.Size = OrbTextSize;
        _spinText.Position = _orb - OrbTextSize / 2.0f;
        _spinText.Visible = false;
        AddChild(_spinText);
    }

    public override void _ExitTree() => _ledger.Changed -= RefreshPrompt;

    protected override void Interact(Player p)
    {
        if (_spinning)
            return;
        if (_ledger.TryPull() is not { } item)
        {
            FloatingText.Emit(FloatingTextType.Damage, p, TextOffset, _ledger.PullBlocked(), 0.0f, BlockedColor);
            return;
        }
        Pop();
        Spin(item);
    }

    /// <summary>Flicker the item names at the orb, fast then slowing, and land on <paramref name="item"/> (already paid
    /// for).</summary>
    private void Spin(LotteryItemDef item)
    {
        _spinning = true;
        _spinText.Visible = true;
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(ShowSpinStep), 0.0f, SpinSteps, Lottery.SpinTime)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() => Land(item)));
    }

    private void ShowSpinStep(float step) => _spinText.Text = Lottery.Items[(int)step % Lottery.Items.Length].Name;

    private void Land(LotteryItemDef item)
    {
        _spinning = false;
        _spinText.Visible = false;
        _ledger.Apply(item);
        FloatingText.Emit(FloatingTextType.Damage, this, _orb, item.Name, 0.0f, LandedColor);
        _sfx.Play(LandCue); // PLACEHOLDER cue
    }

    private void RefreshPrompt() =>
        SetPrompt(_ledger.PullsLeft > 0 ? $"E · {_ledger.Price}\n{_ledger.PullsLeft} LEFT" : "NO PULLS LEFT");
}
