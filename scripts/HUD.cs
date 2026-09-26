using Godot;

namespace MyGame;

/// <summary>
/// The player HUD. Health stars + Ruh orbs + the special's cooldown bar sit in the <see cref="_gauge"/> — fixed at bottom-centre, or following under
/// Khalid's feet, per the player's <see cref="GaugePlacement"/> setting (dim at rest, bright on any change or at low
/// HP). Also: the fada_fig ring (next-buff progress + spendable count, top-left), the ROUND / n LEFT / BEST block (top
/// centre, pushed by RunManager), the active-buff
/// list, off-screen enemy arrows, the low-HP effect, and the Esc <see cref="PauseMenu"/> (where that setting lives).
/// An autoload, so it exists in every scene; binds to whatever <see cref="Player"/> enters the tree and hides when
/// there's none. Built entirely in code.
/// </summary>
public partial class HUD : CanvasLayer
{
	private Player _player;

	private CanvasLayer _lowHpLayer;
	private ShaderMaterial _lowHpMat;
	private float _lowHpLevel = 0.0f;
	private float _lowHpTarget = 0.0f;
	private float _lowHpTime = 0.0f;

	private const float LowHpRatio = 0.34f;  // screen effect kicks in at ~1 block left
	private const float LowHpMin = 0.35f;
	private const float LowHpFade = 3.5f;
	private const float LowHpBeatHz = 1.15f;
	private const float LowHpPulseBase = 0.72f;
	private const float LowHpPulsePunch = 0.6f;

	private Control _root;
	private OffscreenMarkers _markers;
	private VBoxContainer _gauge;     // health stars over Ruh orbs; reparented between the two placements
	private float _gaugeWake = 0.0f;  // seconds left at full brightness after the last change
	private GaugePlacement _placement;
	// FollowKhalid placement: the gauge hangs off _gaugeAnchor on its own camera-following CanvasLayer, ABOVE the low-HP
	// grade (so it stays legible exactly when HP is low) and below the screen HUD. A RemoteTransform2D on the Player drags
	// the anchor along — set during physics, so physics interpolation smooths it in step with Khalid — and it's NOT a
	// Player child, so the player's hit-flash / blink modulate never bleeds into it.
	private CanvasLayer _gaugeLayer;
	private Node2D _gaugeAnchor;
	private RemoteTransform2D _gaugeFollow; // only while a Player is bound AND the placement is FollowKhalid
	private PauseMenu _pauseMenu;
	private HBoxContainer _hpRow;
	private readonly List<HealthPip> _stars = new();
	private readonly List<float> _starLevels = new();
	private HBoxContainer _ruhRow;
	private readonly List<RuhPip> _orbs = new();
	private readonly List<float> _orbLevels = new();
	private SpecialBar _specialBar;   // the special's cooldown, under the Ruh orbs (always shown; pulses when ready)
	private Color _ruhFill;
	private FigRing _figRing;
	private Label _figLabel;
	private int _figCount = 0;
	private Label _roundLabel;
	private Label _leftLabel;   // "n LEFT" — shown only once few quota enemies remain
	private Label _nextLabel;   // "NEXT ROUND IN n" — shown only during a breather
	private Label _bestLabel;
	private int _shownRound = 0; // the round whose intro has played (a higher one plays the intro again)
	private Label _roundIntro;   // the big "ROUND n" flying from screen centre into _roundLabel (only while animating)
	private VBoxContainer _buffPanel;

	private static readonly Vector2 FigRowPos = new(16, 14);
	// Round block placement: RoundBlockAnchor is the screen point (as fractions of width/height) the block's TOP-CENTRE
	// sits on — (0.5, 0) = top-centre, (0.5, 0.5) = dead centre, (0.5, 0.85) = low centre — and RoundBlockOffset nudges
	// it from there in pixels (+x right, +y down).
	private static readonly Vector2 RoundBlockAnchor = new(0.5f, 0.0f);
	private static readonly Vector2 RoundBlockOffset = new(0.0f, 30.0f);
	// Round intro: the big "ROUND n" fades in at screen centre, holds, then flies up + shrinks into the ROUND label.
	private const float IntroFadeIn = 0.25f;
	private const float IntroHold = 1.0f;
	private const float IntroFly = 0.7f;
	private const float IntroGlow = 1.8f;   // HDR multiplier on the accent while it's big (blooms), settling to 1
	private const int PipGap = 1;              // pip pixels between pips
	private const int RowGap = 1;              // pip pixels between the gauge's rows
	// Extra pixels above the special bar: the stars' pointed tips leave a lot of visual air above the orbs, while the
	// orbs' rounded bottoms sit almost flush on the flat bar — this evens the two gaps out to the eye.
	private const int SpecialBarTopGap = 2;
	private const float GaugeScreenY = 0.9f;   // Screen placement: gauge top, as a fraction of screen height
	// Screen placement: pips are pixel art, so scale them by the normal camera zoom (RunManager.CamZoomNormal) — one pip
	// pixel is then the same size on screen as one sprite pixel. (FollowKhalid is in world units, so it matches natively.)
	private const float GaugePixelScale = 1.5f;
	private const float GaugeFeetGap = 3.0f;   // FollowKhalid: world px below Khalid's origin (his feet)
	private const float GaugeIdleAlpha = 0.6f;
	private const float GaugeWakeTime = 1.6f;
	private const float GaugeFade = 4.0f;    // alpha per second

	private static readonly Color HpEmpty = new(0.26f, 0.26f, 0.31f);
	// Ruh in the red family, like the in-world Ruh orbs — recoloured to the Power-1 pick at bind (VfxPalette.Recolor).
	private static readonly Color RuhFillBase = new(0.80f, 0.16f, 0.20f);
	private static readonly Color RuhEmpty = new(0.30f, 0.14f, 0.17f);


	public override void _Ready()
	{
		Layer = UiLayers.Hud;
		UiStyle.Install(); // first UI to exist (autoload) — make Sixtyfour the global fallback font before anything builds
		BuildHud();
		BuildLowHealth();
		_pauseMenu = new PauseMenu();
		AddChild(_pauseMenu);
		_pauseMenu.GaugePlacementChanged += ApplyGaugePlacement;
		SetShown(false);
		GetTree().NodeAdded += OnNodeAdded;
		SetProcess(true);
		var existing = FindPlayer();
		if (existing != null)
			Bind(existing);
	}

	// --- construction ---------------------------------------------------------

	private void BuildHud()
	{
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiStyle.Theme };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		_markers = new OffscreenMarkers();
		AddChild(_markers);

		_gauge = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, GaugeIdleAlpha) };
		_gauge.AddThemeConstantOverride("separation", RowGap);
		_gauge.Resized += LayoutGauge;
		_root.AddChild(_gauge);
		_hpRow = MkPipRow(_gauge);
		_ruhRow = MkPipRow(_gauge);
		_specialBar = new SpecialBar();
		var barPad = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		barPad.AddThemeConstantOverride("margin_top", SpecialBarTopGap);
		barPad.AddChild(_specialBar);
		_gauge.AddChild(barPad);
		_gaugeLayer = new CanvasLayer { Layer = UiLayers.Gauge, FollowViewportEnabled = true };
		AddChild(_gaugeLayer);
		_gaugeAnchor = new Node2D();
		_gaugeLayer.AddChild(_gaugeAnchor);
		ApplyGaugePlacement(SaveData.GetGaugePlacement());

		var figRow = new HBoxContainer { Position = FigRowPos, MouseFilter = Control.MouseFilterEnum.Ignore };
		figRow.AddThemeConstantOverride("separation", 6);
		_root.AddChild(figRow);
		_figRing = new FigRing();
		figRow.AddChild(_figRing);
		_figLabel = MkLabel(UiStyle.HudValue);
		_figLabel.Text = "0";
		figRow.AddChild(_figLabel);

		// Round block (placed by RoundBlockAnchor/Offset): ROUND n / n LEFT (late in a round) or NEXT ROUND IN n (breather)
		// / BEST n. Grows both ways from its anchor, so it stays centred on it.
		var roundBox = new VBoxContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = RoundBlockAnchor.X,
			AnchorRight = RoundBlockAnchor.X,
			AnchorTop = RoundBlockAnchor.Y,
			AnchorBottom = RoundBlockAnchor.Y,
			OffsetLeft = RoundBlockOffset.X,
			OffsetRight = RoundBlockOffset.X,
			OffsetTop = RoundBlockOffset.Y,
			OffsetBottom = RoundBlockOffset.Y,
			GrowHorizontal = Control.GrowDirection.Both,
		};
		roundBox.AddThemeConstantOverride("separation", 2);
		_root.AddChild(roundBox);
		_roundLabel = MkLabel(UiStyle.HudTitle);
		_leftLabel = MkLabel(UiStyle.HudHeading);
		_nextLabel = MkLabel(UiStyle.HudHeading);
		_bestLabel = MkLabel(UiStyle.HudMuted);
		foreach (var l in new[] { _roundLabel, _leftLabel, _nextLabel, _bestLabel })
		{
			l.HorizontalAlignment = HorizontalAlignment.Center;
			roundBox.AddChild(l);
		}

		// Active-buff list, pinned top-right and growing leftward to fit its widest line.
		_buffPanel = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_buffPanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		_buffPanel.GrowHorizontal = Control.GrowDirection.Begin;
		_buffPanel.OffsetRight = -14.0f;
		_buffPanel.OffsetTop = 14.0f;
		_root.AddChild(_buffPanel);
	}

	/// <summary>Move the gauge to <paramref name="g"/>: Screen = anchored bottom-centre in the screen HUD, scaled to sprite
	/// pixel size; FollowKhalid = under the world-space anchor at world scale. Live — the pause menu calls it.</summary>
	private void ApplyGaugePlacement(GaugePlacement g)
	{
		_placement = g;
		if (g == GaugePlacement.Screen)
		{
			_gauge.Reparent(_root, false);
			_gauge.AnchorLeft = 0.5f;
			_gauge.AnchorRight = 0.5f;
			_gauge.AnchorTop = GaugeScreenY;
			_gauge.AnchorBottom = GaugeScreenY;
			_gauge.GrowHorizontal = Control.GrowDirection.Both; // grows both ways from the centre anchor
			_gauge.Scale = new Vector2(GaugePixelScale, GaugePixelScale);
		}
		else
		{
			_gauge.Reparent(_gaugeAnchor, false);
			_gauge.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
			_gauge.Scale = Vector2.One;
		}
		_gauge.OffsetLeft = _gauge.OffsetRight = _gauge.OffsetTop = _gauge.OffsetBottom = 0.0f; // shrink to content
		LayoutGauge();
		SyncGaugeFollow();
	}

	/// <summary>Re-centre the gauge for its size: Screen scales about its top-centre (so scaling keeps it centred);
	/// FollowKhalid sits centred just under the anchor (his feet).</summary>
	private void LayoutGauge()
	{
		if (_placement == GaugePlacement.Screen)
			_gauge.PivotOffset = new Vector2(_gauge.Size.X / 2.0f, 0.0f);
		else
		{
			_gauge.PivotOffset = Vector2.Zero;
			_gauge.Position = new Vector2(Mathf.Round(-_gauge.Size.X / 2.0f), GaugeFeetGap);
		}
	}

	/// <summary>Give the bound Player a RemoteTransform2D driving the gauge anchor exactly when the placement is
	/// FollowKhalid; remove it otherwise (or once unbound).</summary>
	private void SyncGaugeFollow()
	{
		bool want = _player != null && _placement == GaugePlacement.FollowKhalid;
		if (want && _gaugeFollow == null)
		{
			_gaugeFollow = new RemoteTransform2D { UpdateRotation = false, UpdateScale = false, RemotePath = _gaugeAnchor.GetPath() };
			_gaugeFollow.Ready += () => _gaugeAnchor.ResetPhysicsInterpolation(); // snap to Khalid, don't sweep in
			// Deferred: Bind runs from node_added, while the Player is still mid-enter-tree.
			_player.CallDeferred(Node.MethodName.AddChild, _gaugeFollow);
		}
		else if (!want && _gaugeFollow != null)
		{
			if (IsInstanceValid(_gaugeFollow))
				_gaugeFollow.QueueFree();
			_gaugeFollow = null;
		}
	}

	private static HBoxContainer MkPipRow(Control parent)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", PipGap);
		parent.AddChild(row);
		return row;
	}

	/// <summary>A HUD label in one of UiStyle's outlined HUD styles (text floats over the world).</summary>
	private static Label MkLabel(string style) =>
		new() { ThemeTypeVariation = style, MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };

	/// <summary>Rebuild the top-right active-buff list from the player's passives (call on grant / clear).</summary>
	public void RefreshBuffs(List<Passive> passives)
	{
		if (_buffPanel == null)
			return;
		foreach (Node child in _buffPanel.GetChildren())
			child.QueueFree();
		bool any = false;
		foreach (Passive p in passives)
		{
			if (p is not Buff b)
				continue;
			any = true;
			var name = MkLabel(UiStyle.HudHeading);
			name.AddThemeColorOverride("font_color", Tiers.ColorOf(b.Tier)); // tier colour is semantic, not UI chrome
			name.Text = b.Name != "" ? $"{b.Name} [{Tiers.Label(b.Tier)}]" : b.Id;
			_buffPanel.AddChild(name);

			var desc = MkLabel(UiStyle.HudMuted);
			desc.Text = b.Description;
			desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			desc.CustomMinimumSize = new Vector2(258, 0);
			_buffPanel.AddChild(desc);

			_buffPanel.AddChild(new Control { CustomMinimumSize = new Vector2(0, 5) }); // row spacer
		}
		_buffPanel.Visible = any;
	}

	private void BuildLowHealth()
	{
		_lowHpLayer = new CanvasLayer { Layer = UiLayers.LowHealth, Visible = false };
		AddChild(_lowHpLayer);

		var rect = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_lowHpMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://vfx/shaders/low_health.gdshader") };
		_lowHpMat.SetShaderParameter("intensity", 0.0);
		rect.Material = _lowHpMat;
		_lowHpLayer.AddChild(rect);
	}

	// --- fada_figs ------------------------------------------------------------

	/// <summary>Show round <paramref name="round"/> (0 = before round 1: blank), <paramref name="left"/> quota enemies
	/// remaining (0 = hidden — RunManager only passes it once few remain), the breather <paramref name="countdown"/> in
	/// whole seconds until the next round (0 = hidden — mid-round), and the <paramref name="best"/> round record.</summary>
	public void SetRound(int round, int left, int countdown, int best)
	{
		if (_roundLabel == null)
			return;
		_roundLabel.Text = round > 0 ? $"ROUND {round}" : "";
		if (round > _shownRound)
			PlayRoundIntro(round);
		_shownRound = round; // a new run resets to 0, so round 1 plays again
		_leftLabel.Text = $"{left} LEFT";
		_leftLabel.Visible = left > 0;
		_nextLabel.Text = $"NEXT ROUND IN {countdown}";
		_nextLabel.Visible = countdown > 0;
		_bestLabel.Text = best > 0 ? $"BEST {best}" : "";
	}

	/// <summary>The round-start intro: a big "ROUND n" (the title font at exactly 2x the label's size) fades in at screen
	/// centre, holds, then glides up and shrinks to 0.5x onto <see cref="_roundLabel"/>'s rect — so it lands pixel-
	/// aligned wherever the round block is placed — while its glow settles to the label's colour, then hands over to
	/// the real label (kept invisible, not hidden, meanwhile so the block's layout doesn't jump).</summary>
	private async void PlayRoundIntro(int round)
	{
		_roundIntro?.QueueFree();
		var intro = new Label
		{
			Text = $"ROUND {round}",
			ThemeTypeVariation = UiStyle.HudTitle,
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0),
		};
		intro.AddThemeFontSizeOverride("font_size", UiStyle.SizeTitle * 2);
		Color glow = new(UiStyle.Accent.R * IntroGlow, UiStyle.Accent.G * IntroGlow, UiStyle.Accent.B * IntroGlow);
		intro.AddThemeColorOverride("font_color", glow);
		_roundIntro = intro;
		_root.AddChild(intro);
		_roundLabel.Modulate = new Color(1, 1, 1, 0);

		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); // let the round block lay out the new text
		if (_roundIntro != intro)
			return; // a newer intro replaced this one

		const float scale = 0.5f; // SizeTitle / (SizeTitle * 2)
		intro.Size = _roundLabel.Size / scale;
		intro.Position = (_root.Size - intro.Size) / 2.0f;
		var t = intro.CreateTween();
		t.TweenProperty(intro, "modulate:a", 1.0f, IntroFadeIn);
		t.TweenInterval(IntroHold);
		t.SetParallel();
		t.TweenProperty(intro, "global_position", _roundLabel.GlobalPosition, IntroFly).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		t.TweenProperty(intro, "scale", new Vector2(scale, scale), IntroFly).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		t.TweenProperty(intro, "theme_override_colors/font_color", UiStyle.Accent, IntroFly);
		t.SetParallel(false);
		t.TweenCallback(Callable.From(() =>
		{
			_roundLabel.Modulate = Colors.White;
			intro.QueueFree();
			if (_roundIntro == intro)
				_roundIntro = null;
		}));
	}

	/// <summary>Set the spendable fada_fig balance shown beside the ring (pushed by <c>Player</c>); the ring pops on a gain.</summary>
	public void SetFadaFigs(int count)
	{
		if (_figLabel == null)
			return;
		if (count > _figCount)
			HudFx.Pop(_figRing);
		_figCount = count;
		_figLabel.Text = count.ToString();
	}

	/// <summary>Fill the fig ring to <paramref name="have"/> of <paramref name="need"/> figs toward the next buff
	/// milestone (pushed by RunManager).</summary>
	public void SetBuffProgress(int have, int need)
	{
		_figRing?.SetProgress((float)have / Mathf.Max(need, 1));
	}

	// --- health stars + Ruh orbs (the gauge) ----------------------------------

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
		float per = _player.RUH_PER_BLOCK;
		for (int i = 0; i < _orbs.Count; i++)
		{
			float level = Mathf.Clamp(current / per - i, 0.0f, 1.0f);
			if (animate && level >= 1.0f && _orbLevels[i] < 1.0f)
				HudFx.Pop(_orbs[i]);
			_orbLevels[i] = level;
			_orbs[i].SetLevel(level, _ruhFill, RuhEmpty);
		}
	}

	// --- binding --------------------------------------------------------------

	private void OnNodeAdded(Node node)
	{
		if (node is Player p)
			Bind(p);
	}

	private Player FindPlayer()
	{
		var scene = GetTree().CurrentScene;
		if (scene == null)
			return null;
		if (scene is Player sp)
			return sp;
		foreach (var child in scene.GetChildren())
			if (child is Player cp)
				return cp;
		return null;
	}

	private void Bind(Player player)
	{
		if (player == _player)
			return;
		Unbind();
		_player = player;
		_ruhFill = VfxPalette.Recolor(RuhFillBase);
		_player.health_changed += OnHealthChanged;
		_player.ruh_changed += OnRuhChanged;
		_player.TreeExiting += Unbind;
		SyncHealth((float)_player.health, (float)_player.max_health, false); // seed silently — no pop-in on bind
		SyncRuh((float)_player.ruh, (float)_player.ruh_cap, false);
		SyncGaugeFollow();
		SetShown(true);
	}

	private void Unbind()
	{
		if (_player != null && IsInstanceValid(_player))
		{
			_player.health_changed -= OnHealthChanged;
			_player.ruh_changed -= OnRuhChanged;
			_player.TreeExiting -= Unbind;
		}
		_player = null;
		SyncGaugeFollow();
		SetShown(false);
	}

	private void SetShown(bool shown)
	{
		_root.Visible = shown;
		_markers.Visible = shown;
		_gaugeLayer.Visible = shown;
		_pauseMenu.Enabled = shown; // Esc pauses only during a run
		if (!shown)
		{
			_lowHpTarget = 0.0f;
			_lowHpLevel = 0.0f;
			if (_lowHpLayer != null)
				_lowHpLayer.Visible = false;
		}
	}

	public override void _Process(double deltaD)
	{
		float delta = (float)deltaD;
		if (_player == null)
		{
			var p = FindPlayer();
			if (p != null)
				Bind(p);
			return;
		}
		UpdateLowHealth(delta);
		if (_specialBar.SetProgress(_player.special_ready()))
			_gaugeWake = GaugeWakeTime; // the special just became ready — light the gauge up
		UpdateGaugeAlpha(delta);
	}

	private void UpdateLowHealth(float delta)
	{
		if (_lowHpLayer == null)
			return;
		_lowHpTime += delta;
		_lowHpLevel = Mathf.MoveToward(_lowHpLevel, _lowHpTarget, LowHpFade * delta);
		bool on = _lowHpLevel > 0.001f;
		_lowHpLayer.Visible = on;
		if (on)
		{
			float mult = LowHpPulseBase + LowHpPulsePunch * Heartbeat(_lowHpTime);
			_lowHpMat.SetShaderParameter("intensity", Mathf.Clamp(_lowHpLevel * mult, 0.0f, 1.0f));
		}
	}

	/// <summary>The gauge idles dim so it doesn't clutter the fight; any change wakes it to full for a moment, and it stays
	/// full while HP is low (whenever the low-HP screen effect is on).</summary>
	private void UpdateGaugeAlpha(float delta)
	{
		_gaugeWake = Mathf.Max(_gaugeWake - delta, 0.0f);
		float target = _gaugeWake > 0.0f || _lowHpTarget > 0.0f ? 1.0f : GaugeIdleAlpha;
		Color m = _gauge.Modulate;
		m.A = Mathf.MoveToward(m.A, target, GaugeFade * delta);
		_gauge.Modulate = m;
	}

	/// <summary>Heartbeat envelope 0..1: a sharp "lub" thump plus a softer "dub", so the pulse punches.</summary>
	private static float Heartbeat(float t)
	{
		float ph = Mathf.PosMod(t * LowHpBeatHz, 1.0f);
		float lub = Mathf.Exp(-Mathf.Pow(ph / 0.055f, 2.0f));
		float dub = 0.6f * Mathf.Exp(-Mathf.Pow((ph - 0.17f) / 0.07f, 2.0f));
		return Mathf.Min(lub + dub, 1.0f);
	}

	private void OnHealthChanged(double current, double maximum) => SyncHealth((float)current, (float)maximum, true);

	private void OnRuhChanged(double current, double maximum) => SyncRuh((float)current, (float)maximum, true);

	private void SyncHealth(float current, float maximum, bool animate)
	{
		int blocks = Mathf.RoundToInt(maximum / 2.0f); // 2 half-blocks per block
		ResizePips(_hpRow, _stars, _starLevels, blocks);
		UpdateStars(current, maximum, animate);
		if (animate)
			_gaugeWake = GaugeWakeTime;
		float ratio = maximum > 0.0f ? current / maximum : 0.0f;
		if (ratio >= LowHpRatio)
			_lowHpTarget = 0.0f;
		else
		{
			float t = Mathf.Clamp((LowHpRatio - ratio) / LowHpRatio, 0.0f, 1.0f);
			_lowHpTarget = Mathf.Lerp(LowHpMin, 1.0f, t);
		}
	}

	private void SyncRuh(float current, float maximum, bool animate)
	{
		int blocks = Mathf.RoundToInt(maximum / _player.RUH_PER_BLOCK);
		ResizePips(_ruhRow, _orbs, _orbLevels, blocks);
		UpdateOrbs(current, animate);
		if (animate)
			_gaugeWake = GaugeWakeTime;
	}
}
