using Godot;

namespace MyGame;

/// <summary>
/// The HUD's gauge: health stars over Ruh orbs over the special's cooldown bar. It sits where the player's
/// <see cref="GaugePlacement"/> setting says — fixed at bottom-centre of the screen, or following under Khalid's feet —
/// and idles dim so it doesn't clutter the fight: any change wakes it to full brightness for a moment, and it stays
/// bright while health is low. Owned by the <see cref="HUD"/>, which feeds it the player's numbers.
///
/// <para>FollowKhalid placement: the pips hang off an anchor on their own camera-following CanvasLayer, ABOVE the
/// low-health grade (so they stay legible exactly when HP is low) and below the screen HUD. A RemoteTransform2D on the
/// Player drags the anchor along — set during physics, so physics interpolation smooths it in step with Khalid — and
/// the pips are NOT a Player child, so the player's hit-flash / blink modulate never bleeds into them.</para>
/// </summary>
public partial class HudGauge : Node
{
    private const int PipGap = 1;              // pip pixels between pips
    private const int RowGap = 1;              // pip pixels between the gauge's rows
    // Extra pixels above the special bar: the stars' pointed tips leave a lot of visual air above the orbs, while the
    // orbs' rounded bottoms sit almost flush on the flat bar — this evens the two gaps out to the eye.
    private const int SpecialBarTopGap = 2;
    private const float ScreenY = 0.9f;        // Screen placement: gauge top, as a fraction of screen height
    // Screen placement: a fixed, readable pixel scale for the pips — screen UI, independent of the camera zoom.
    // (FollowKhalid is in world units instead, so it scales with the camera zoom along with the sprites.)
    private const float PixelScale = 1.5f;
    private const float FeetGap = 3.0f;        // FollowKhalid: world px below Khalid's origin (his feet)
    private const float IdleAlpha = 0.6f;
    private const float WakeTime = 1.6f;
    private const float Fade = 4.0f;           // alpha per second

    private static readonly Color HpEmpty = new(0.26f, 0.26f, 0.31f);
    // Ruh in the red family, like the in-world Ruh orbs — recoloured to the Power-1 pick at bind (VfxPalette.Recolor).
    private static readonly Color RuhFillBase = new(0.80f, 0.16f, 0.20f);
    private static readonly Color RuhEmpty = new(0.30f, 0.14f, 0.17f);

    private readonly Control _screen;   // the screen HUD's root: the pips' home in the Screen placement
    private readonly VBoxContainer _box = new() { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, IdleAlpha) };
    private readonly CanvasLayer _worldLayer = new() { Layer = UiLayers.Gauge, FollowViewportEnabled = true };
    private readonly Node2D _anchor = new();   // the pips' home in the FollowKhalid placement
    private readonly List<HealthPip> _stars = new();
    private readonly List<float> _starLevels = new();
    private readonly List<RuhPip> _orbs = new();
    private readonly List<float> _orbLevels = new();
    private readonly SpecialBar _specialBar = new();   // always shown; pulses when ready
    private HBoxContainer _hpRow = null!;
    private HBoxContainer _ruhRow = null!;
    private GaugePlacement _placement;
    private Player? _player;
    private RemoteTransform2D? _follow;   // only while a Player is bound AND the placement is FollowKhalid
    private float _wake = 0.0f;           // seconds left at full brightness after the last change
    private Color _ruhFill = RuhFillBase;

    public HudGauge(Control screen) => _screen = screen;

    /// <summary>Whether the world-following layer draws (off while there is no player to follow).</summary>
    public bool WorldLayerShown { set => _worldLayer.Visible = value; }

    public override void _Ready()
    {
        _box.AddThemeConstantOverride("separation", RowGap);
        _box.Resized += Layout;
        _screen.AddChild(_box);
        _hpRow = MkPipRow(_box);
        _ruhRow = MkPipRow(_box);
        var barPad = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        barPad.AddThemeConstantOverride("margin_top", SpecialBarTopGap);
        barPad.AddChild(_specialBar);
        _box.AddChild(barPad);
        AddChild(_worldLayer);
        _worldLayer.AddChild(_anchor);
        ApplyPlacement(SaveData.GetGaugePlacement());
    }

    /// <summary>Move the gauge to <paramref name="g"/>: Screen = anchored bottom-centre in the screen HUD, scaled to sprite
    /// pixel size; FollowKhalid = under the world-space anchor at world scale. Live — the pause menu calls it.</summary>
    public void ApplyPlacement(GaugePlacement g)
    {
        _placement = g;
        if (g == GaugePlacement.Screen)
        {
            _box.Reparent(_screen, false);
            _box.AnchorLeft = 0.5f;
            _box.AnchorRight = 0.5f;
            _box.AnchorTop = ScreenY;
            _box.AnchorBottom = ScreenY;
            _box.GrowHorizontal = Control.GrowDirection.Both; // grows both ways from the centre anchor
            _box.Scale = new Vector2(PixelScale, PixelScale);
        }
        else
        {
            _box.Reparent(_anchor, false);
            _box.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _box.Scale = Vector2.One;
        }
        _box.OffsetLeft = _box.OffsetRight = _box.OffsetTop = _box.OffsetBottom = 0.0f; // shrink to content
        Layout();
        SyncFollow();
    }

    /// <summary>The player whose numbers this shows (null = none). Picks up the run's Ruh colour and, in the FollowKhalid
    /// placement, starts or stops following.</summary>
    public void Bind(Player? player)
    {
        _player = player;
        if (player != null)
            _ruhFill = VfxPalette.Recolor(RuhFillBase);
        SyncFollow();
    }

    /// <summary>Show health in half-blocks (2 per star). <paramref name="animate"/> = it just changed: a lost half
    /// wiggles, a gain pops, and the gauge wakes.</summary>
    public void SetHealth(float current, float maximum, bool animate)
    {
        int blocks = Mathf.RoundToInt(maximum / 2.0f); // 2 half-blocks per block
        ResizePips(_hpRow, _stars, _starLevels, blocks);
        UpdateStars(current, maximum, animate);
        if (animate)
            _wake = WakeTime;
    }

    /// <summary>Show Ruh, one orb per charge. <paramref name="animate"/> = it just changed.</summary>
    public void SetRuh(float current, float maximum, bool animate)
    {
        int blocks = Mathf.RoundToInt(maximum / Player.RuhPerBlock);
        ResizePips(_ruhRow, _orbs, _orbLevels, blocks);
        UpdateOrbs(current, animate);
        if (animate)
            _wake = WakeTime;
    }

    /// <summary>One frame: the special bar's fill (0..1, 1 = ready) and the idle / awake brightness.
    /// <paramref name="stayBright"/> holds it at full (health is low).</summary>
    public void Tick(float delta, float specialReady, bool stayBright)
    {
        if (_specialBar.SetProgress(specialReady))
            _wake = WakeTime; // the special just became ready — light the gauge up
        _wake = Mathf.Max(_wake - delta, 0.0f);
        float target = _wake > 0.0f || stayBright ? 1.0f : IdleAlpha;
        Color m = _box.Modulate;
        m.A = Mathf.MoveToward(m.A, target, Fade * delta);
        _box.Modulate = m;
    }

    /// <summary>Re-centre the gauge for its size: Screen scales about its top-centre (so scaling keeps it centred);
    /// FollowKhalid sits centred just under the anchor (his feet).</summary>
    private void Layout()
    {
        if (_placement == GaugePlacement.Screen)
            _box.PivotOffset = new Vector2(_box.Size.X / 2.0f, 0.0f);
        else
        {
            _box.PivotOffset = Vector2.Zero;
            _box.Position = new Vector2(Mathf.Round(-_box.Size.X / 2.0f), FeetGap);
        }
    }

    /// <summary>Give the bound Player a RemoteTransform2D driving the anchor exactly when the placement is
    /// FollowKhalid; remove it otherwise (or once unbound).</summary>
    private void SyncFollow()
    {
        if (_player is { } player && _placement == GaugePlacement.FollowKhalid)
        {
            if (_follow != null)
                return;
            _follow = new RemoteTransform2D { UpdateRotation = false, UpdateScale = false, RemotePath = _anchor.GetPath() };
            _follow.Ready += () => _anchor.ResetPhysicsInterpolation(); // snap to Khalid, don't sweep in
            // Deferred: the HUD binds from node_added, while the Player is still mid-enter-tree.
            player.CallDeferred(Node.MethodName.AddChild, _follow);
        }
        else if (_follow != null)
        {
            if (IsInstanceValid(_follow))
                _follow.QueueFree();
            _follow = null;
        }
    }

    private static HBoxContainer MkPipRow(Control parent)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", PipGap);
        parent.AddChild(row);
        return row;
    }

    /// <summary>Make <paramref name="row"/> hold exactly <paramref name="count"/> pips (adds/frees at the end, so the
    /// surviving pips keep their levels + any running tween).</summary>
    private static void ResizePips<T>(HBoxContainer row, List<T> pips, List<float> levels, int count) where T : PixelPip, new()
    {
        count = Mathf.Max(count, 1);
        while (pips.Count < count)
        {
            var pip = new T();
            row.AddChild(pip);
            pips.Add(pip);
            levels.Add(0.0f);
        }
        while (pips.Count > count)
        {
            int last = pips.Count - 1;
            pips[last].QueueFree();
            pips.RemoveAt(last);
            levels.RemoveAt(last);
        }
    }

    /// <summary>Each star shows its share of `current` half-blocks (2 per block): full / half / empty, all tinted
    /// green→orange→red by the overall ratio (same bands as the floating enemy bars). When <paramref name="animate"/>,
    /// a lost half wiggles and a gain pops.</summary>
    private void UpdateStars(float current, float maximum, bool animate)
    {
        Color fill = FloatingHealthBar.ColorForRatio(maximum > 0.0f ? current / maximum : 0.0f);
        for (int i = 0; i < _stars.Count; i++)
        {
            float level = Mathf.Clamp(current / 2.0f - i, 0.0f, 1.0f);
            if (animate && level < _starLevels[i])
                HudFx.Wiggle(_stars[i]);
            else if (animate && level > _starLevels[i])
                HudFx.Pop(_stars[i]);
            _starLevels[i] = level;
            _stars[i].SetLevel(level, fill, HpEmpty);
        }
    }

    /// <summary>Each orb fills with its block's share of `current` Ruh; when <paramref name="animate"/>, an orb pops the
    /// moment it becomes full.</summary>
    private void UpdateOrbs(float current, bool animate)
    {
        float per = Player.RuhPerBlock;
        for (int i = 0; i < _orbs.Count; i++)
        {
            float level = Mathf.Clamp(current / per - i, 0.0f, 1.0f);
            if (animate && level >= 1.0f && _orbLevels[i] < 1.0f)
                HudFx.Pop(_orbs[i]);
            _orbLevels[i] = level;
            _orbs[i].SetLevel(level, _ruhFill, RuhEmpty);
        }
    }
}
