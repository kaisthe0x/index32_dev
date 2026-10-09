using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// The MYSTERY BOX (<c>scenes/things/mystery_box.tscn</c> — placeholder art: a purple "?" crate), a <see cref="Stall"/>:
/// stand at it and press <b>E</b> to spend figs on a SPIN (rules + numbers: <see cref="BoxRules"/>, run state:
/// <see cref="BoxLedger"/>). It all happens in REAL TIME — the game never pauses:
/// <list type="number">
/// <item><b>Spin</b> — names flicker over the box for <see cref="BoxRules.SpinTime"/>.</item>
/// <item><b>Offer</b> — the result hangs over the box for <see cref="BoxRules.OfferTime"/>; press <b>E</b> to take it,
///   or leave it and it's gone (the figs are spent either way).</item>
/// <item><b>Teddy bear</b> — instead of an offer: the figs come back and the box RELOCATES to another of the layout's
///   box spots (a hard one <see cref="BoxRules.HardSpotChance"/> of the time), under a beam until the player reaches it.</item>
/// </list>
/// RunManager calls <see cref="Setup"/> once per run with the ledger + the layout's spots; the box starts on an easy one.
/// </summary>
public partial class MysteryBox : Stall
{
    private enum Phase { Idle, Spinning, Offering, Leaving }

    private const float FlickerInterval = 0.08f;  // how fast names change during the spin
    private const float LeaveTime = 1.2f;         // the teddy bear shows this long before the box moves
    private const float FadeTime = 0.3f;          // the box fading out / in around a move
    private const float TextWidth = 260.0f;
    private static readonly Vector2 NoticeOffset = new(0, -40);
    private static readonly Color NoticeColor = new(0.72f, 0.72f, 0.78f);
    private static readonly Color TeddyColor = new(1.0f, 0.55f, 0.65f);
    private static readonly Color OfferColor = new(1.6f, 1.35f, 0.35f);   // HDR gold, like the "?" on the crate
    private static readonly Color BeamColor = new(1.4f, 1.15f, 0.4f, 0.55f);
    private const float BeamWidth = 14.0f;
    private const float BeamHeight = 900.0f;

    private BoxLedger? _ledger;
    private List<Vector2> _easy = new(), _hard = new();
    private Phase _phase = Phase.Idle;
    private float _left;        // time left in the current phase
    private float _flicker;     // until the next name change while spinning
    private BoxRoll? _roll;      // the spin in progress / on offer
    private List<string> _spinNames = new();
    private Label _title = null!, _detail = null!;
    private Polygon2D _beam = null!;
    private Sfx? _sfx;

    public override void _Ready()
    {
        base._Ready();
        _sfx = GetNodeOrNull<Sfx>("/root/Sfx");
        Vector2 prompt = GetNode<Marker2D>("Prompt").Position;
        _title = MakeText(prompt + new Vector2(0, -34), 16, OfferColor);
        _detail = MakeText(prompt + new Vector2(0, -14), 8, Colors.White);
        // The relocation beam: a tall column of light over the box, fading out toward the top.
        _beam = new Polygon2D
        {
            Polygon = new[] { new Vector2(-BeamWidth, 0), new Vector2(BeamWidth, 0), new Vector2(BeamWidth, -BeamHeight), new Vector2(-BeamWidth, -BeamHeight) },
            VertexColors = new[] { BeamColor, BeamColor, new Color(BeamColor, 0.0f), new Color(BeamColor, 0.0f) },
            Visible = false,
            ShowBehindParent = true,
        };
        AddChild(_beam);
    }

    /// <summary>Hand the box this run's ledger and the layout's box spots, and put it on a random EASY one.</summary>
    public void Setup(BoxLedger ledger, List<Vector2> easy, List<Vector2> hard)
    {
        _ledger = ledger;
        _easy = easy;
        _hard = hard;
        if (_easy.Count > 0)
            GlobalPosition = _easy[(int)(GD.Randi() % (uint)_easy.Count)];
    }

    protected override void Interact(Player p)
    {
        if (_ledger == null)
            return;
        if (_phase == Phase.Offering && _roll is { } offered)
        {
            _ledger.Take(offered);
            _sfx?.PlayAt("buff_select", GlobalPosition); // PLACEHOLDER cue
            FloatingText.Emit(FloatingTextType.Damage, this, NoticeOffset, offered.Name, 0.0f, OfferColor);
            Pop();
            EndOffer();
            return;
        }
        if (_phase != Phase.Idle)
            return; // mid-spin / leaving — nothing to press yet
        string blocked = _ledger.Blocked();
        if (blocked != "")
        {
            FloatingText.Emit(FloatingTextType.Damage, this, NoticeOffset, blocked, 0.0f, NoticeColor);
            return;
        }
        _spinNames = _ledger.SpinNames(); // before the spin, so the figs it costs don't change the pool
        if (_ledger.Spin(OtherSpots().Count > 0) is not { } roll)
            return;
        _roll = roll;
        Pop();
        _sfx?.PlayAt("box_spin", GlobalPosition);
        _phase = Phase.Spinning;
        _left = BoxRules.SpinTime;
        _flicker = 0.0f;
        _detail.Text = "";
        _title.Visible = _detail.Visible = true;
    }

    public override void _Process(double delta)
    {
        if (_phase == Phase.Idle)
            return;
        float d = (float)delta;
        _left -= d;
        if (_phase == Phase.Spinning)
        {
            _flicker -= d;
            if (_flicker <= 0.0f)
            {
                _flicker = FlickerInterval;
                _title.Text = _spinNames[(int)(GD.Randi() % (uint)_spinNames.Count)];
            }
            if (_left <= 0.0f && _roll is { } rolled)
                Reveal(rolled);
        }
        else if (_phase == Phase.Offering)
        {
            // The last quarter of the offer blinks — it's about to go.
            _title.Visible = _left > BoxRules.OfferTime * 0.25f || Mathf.PosMod(_left, 0.3f) < 0.15f;
            if (_left <= 0.0f)
                EndOffer(); // not taken: declined, the figs stay spent
        }
        else if (_phase == Phase.Leaving && _left <= 0.0f)
        {
            Relocate();
        }
    }

    /// <summary>The spin ends: a teddy bear refunds + sends the box away; anything else goes on offer.</summary>
    private void Reveal(BoxRoll roll)
    {
        _title.Text = roll.Name;
        _detail.Text = roll.Description;
        Pop();
        if (roll.Outcome == BoxOutcome.Teddy)
        {
            _title.AddThemeColorOverride("font_color", TeddyColor);
            _ledger?.Refund();
            _sfx?.PlayAt("box_teddy", GlobalPosition);
            _phase = Phase.Leaving;
            _left = LeaveTime;
            return;
        }
        _sfx?.PlayAt("box_result", GlobalPosition);
        _phase = Phase.Offering;
        _left = BoxRules.OfferTime;
    }

    private void EndOffer()
    {
        _phase = Phase.Idle;
        _roll = null;
        _title.Visible = _detail.Visible = false;
    }

    /// <summary>Move to another box spot — a hard one <see cref="BoxRules.HardSpotChance"/> of the time (when there is
    /// one that isn't where it stands), else an easy one — fading out and back in, with the beam on until the player
    /// arrives.</summary>
    private void Relocate()
    {
        _phase = Phase.Idle;
        _roll = null;
        _title.Visible = _detail.Visible = false;
        _title.AddThemeColorOverride("font_color", OfferColor);
        var hard = Others(_hard);
        var easy = Others(_easy);
        var pool = hard.Count > 0 && (easy.Count == 0 || GD.Randf() < BoxRules.HardSpotChance) ? hard : easy;
        Vector2 to = pool[(int)(GD.Randi() % (uint)pool.Count)];
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0.0f, FadeTime);
        tween.TweenCallback(Callable.From(() =>
        {
            GlobalPosition = to;
            _beam.Visible = true;
        }));
        tween.TweenProperty(this, "modulate:a", 1.0f, FadeTime);
    }

    protected override void PlayerArrived() => _beam.Visible = false; // found it

    /// <summary>The box spots it isn't standing on (where a teddy bear could send it).</summary>
    private List<Vector2> OtherSpots()
    {
        var all = Others(_easy);
        all.AddRange(Others(_hard));
        return all;
    }

    private List<Vector2> Others(List<Vector2> spots) => spots.FindAll(s => !s.IsEqualApprox(GlobalPosition));

    /// <summary>A centred, outlined line of text over the box (hidden until a spin).</summary>
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
