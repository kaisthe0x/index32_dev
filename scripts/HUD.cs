using Godot;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// Portrait + HP BLOCK meter + Ruh BLOCK meter + next-buff progress bar (+ a debug stats panel + off-screen enemy arrows + low-HP screen effect)
/// for the active character. An autoload, so it exists in every scene; binds to whatever <see cref="Player"/>
/// enters the tree and hides when there's none. Built entirely in code. C# port of <c>scripts/hud.gd</c>.
/// Bridges the GDScript config statics (SaveData / PaletteConfig / Loadout) it reads.
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
	private TextureRect _portrait;
	private Label _nameLabel;
	private Control _hpArea;
	private readonly List<ColorRect> _hpCells = new();
	private float _hpCellW = 0.0f;
	private Control _ruhArea;
	private readonly List<ColorRect> _ruhCells = new();
	private float _ruhCellW = 0.0f;
	private Label _ruhLabel;
	private Label _levelsLabel;
	private TextureRect _fadaFigIcon;
	private Label _fadaFigLabel;
	private ProgressBar _buffBar;    // progress toward the next buff milestone
	private Label _buffLabel;
	private Label _controls;
	private VBoxContainer _buffPanel;
	private PanelContainer _stats;
	private Label _statsLabel;

	private static readonly Vector2 RuhMeterSize = new(248, 16);
	private const float RuhCellGap = 3.0f;
	private static readonly Color RuhFill = new(0.80f, 0.16f, 0.20f);
	private static readonly Color RuhEmpty = new(0.16f, 0.08f, 0.10f, 0.9f);

	// Health BLOCKS (slot health): one cell per block, each fills 0/half/full (green→red by ratio).
	private static readonly Vector2 HpMeterSize = new(248, 20);
	private const float HpCellGap = 4.0f;
	private static readonly Color HpEmpty = new(0.09f, 0.09f, 0.11f, 0.9f);

	private static readonly string[] StrikeTypeNames =
		{ "MELEE", "PROJECTILE", "DELAYED_PROJECTILE", "AOE", "DELAYED_AOE", "BLAST", "TRAP" };


	public override void _Ready()
	{
		Layer = 100;
		BuildHud();
		BuildLowHealth();
		BuildStats();
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
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		AddChild(_root);

		_markers = new OffscreenMarkers();
		AddChild(_markers);

		var frame = new Panel { Position = new Vector2(16, 16), Size = new Vector2(112, 112) };
		frame.AddThemeStyleboxOverride("panel", Framed(new Color(0.07f, 0.07f, 0.09f, 0.85f), new Color(0.85f, 0.72f, 0.18f)));
		_root.AddChild(frame);

		_portrait = new TextureRect
		{
			Position = new Vector2(4, 4),
			Size = new Vector2(104, 104),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
		};
		frame.AddChild(_portrait);

		const float infoX = 140.0f;
		_nameLabel = MkLabel(new Vector2(infoX, 14), 20, new Color(0.93f, 0.87f, 0.62f));
		_nameLabel.Text = "CHARACTER";

		_hpArea = new Control { Position = new Vector2(infoX, 48), Size = HpMeterSize, MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.AddChild(_hpArea);

		_ruhArea = new Control { Position = new Vector2(infoX, 76), Size = RuhMeterSize, MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.AddChild(_ruhArea);

		_ruhLabel = MkLabel(new Vector2(infoX + RuhMeterSize.X + 8, 74), 12, new Color(0.95f, 0.75f, 0.8f));
		_ruhLabel.Size = new Vector2(120, 18);
		_ruhLabel.VerticalAlignment = VerticalAlignment.Center;

		_levelsLabel = MkLabel(new Vector2(infoX, 100), 15, new Color(0.85f, 0.72f, 0.18f));

		// FadaFig counter — icon + count, to the right of the Ruh meter row.
		float fadaFigX = infoX + RuhMeterSize.X + 8.0f;
		_fadaFigIcon = new TextureRect
		{
			Position = new Vector2(fadaFigX, 98),
			Size = new Vector2(22, 22),
			Texture = GD.Load<Texture2D>("res://assets/things/fada_fig.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_root.AddChild(_fadaFigIcon);
		_fadaFigLabel = MkLabel(new Vector2(fadaFigX + 26, 100), 16, new Color(0.72f, 0.86f, 1.0f));
		_fadaFigLabel.Text = "0";

		// Progress toward the next buff milestone (figs collected).
		_buffBar = new ProgressBar { Position = new Vector2(infoX, 124), Size = new Vector2(200, 12), CustomMinimumSize = new Vector2(200, 12), ShowPercentage = false, MinValue = 0, MaxValue = 1 };
		var buffBg = new StyleBoxFlat { BgColor = new Color(0.10f, 0.10f, 0.13f, 0.9f) }; buffBg.SetCornerRadiusAll(2);
		var buffFill = new StyleBoxFlat { BgColor = new Color(0.72f, 0.86f, 1.0f) }; buffFill.SetCornerRadiusAll(2);
		_buffBar.AddThemeStyleboxOverride("background", buffBg);
		_buffBar.AddThemeStyleboxOverride("fill", buffFill);
		_root.AddChild(_buffBar);
		_buffLabel = MkLabel(new Vector2(infoX + 206, 122), 11, new Color(0.72f, 0.86f, 1.0f));
		_buffLabel.Text = "";

		_controls = MkLabel(new Vector2(16, 140), 12, new Color(0.62f, 0.62f, 0.68f));
		//_controls.Text = "A/D move   Space jump   Shift dash   LMB attack   RMB special/slam   E mystery box   Z hurt   X +ruh   0 rebuild";

		// Active-buff list. _root uses the TopLeft preset (zero-sized), so anchors don't resolve here — position it
		// ABSOLUTELY (top-right) in RefreshBuffs from the live viewport width, like every other HUD element.
		_buffPanel = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_root.AddChild(_buffPanel);
	}

	/// <summary>Rebuild the top-right active-buff list from the player's passives (call on grant / clear).</summary>
	public void RefreshBuffs(System.Collections.Generic.List<Passive> passives)
	{
		if (_buffPanel == null)
			return;
		_buffPanel.Position = new Vector2(GetViewport().GetVisibleRect().Size.X - 272.0f, 14.0f);  // top-right, live width
		foreach (Node child in _buffPanel.GetChildren())
			child.QueueFree();
		bool any = false;
		foreach (Passive p in passives)
		{
			if (p is not Buff b)
				continue;
			any = true;
			var name = new Label { Text = b.Name != "" ? $"{b.Name}   [{Tiers.Label(b.Tier)}]" : b.Id };
			name.AddThemeFontSizeOverride("font_size", 13);
			name.AddThemeColorOverride("font_color", Tiers.ColorOf(b.Tier));
			name.AddThemeColorOverride("font_outline_color", Colors.Black);
			name.AddThemeConstantOverride("outline_size", 4);
			_buffPanel.AddChild(name);

			var desc = new Label { Text = b.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart };
			desc.CustomMinimumSize = new Vector2(258, 0);
			desc.AddThemeFontSizeOverride("font_size", 10);
			desc.AddThemeColorOverride("font_color", new Color(0.76f, 0.76f, 0.82f));
			desc.AddThemeColorOverride("font_outline_color", Colors.Black);
			desc.AddThemeConstantOverride("outline_size", 3);
			_buffPanel.AddChild(desc);

			_buffPanel.AddChild(new Control { CustomMinimumSize = new Vector2(0, 5) }); // row spacer
		}
		_buffPanel.Visible = any;
	}

	private void BuildLowHealth()
	{
		_lowHpLayer = new CanvasLayer { Layer = 50, Visible = false };
		AddChild(_lowHpLayer);

		var rect = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore };
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_lowHpMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://vfx/shaders/low_health.gdshader") };
		_lowHpMat.SetShaderParameter("intensity", 0.0);
		rect.Material = _lowHpMat;
		_lowHpLayer.AddChild(rect);
	}

	/// <summary>Set the collected-fada_figs count shown next to the Ruh meter (pushed by <c>Player.collect_fada_fig</c>).</summary>
	public void SetFadaFigs(int count)
	{
		if (_fadaFigLabel != null)
			_fadaFigLabel.Text = count.ToString();
	}

	/// <summary>Update the "next buff" progress bar: <paramref name="have"/> of <paramref name="need"/> figs collected
	/// toward the next milestone (pushed by RunManager). Shows the remaining count.</summary>
	public void SetBuffProgress(int have, int need)
	{
		if (_buffBar == null)
			return;
		need = Mathf.Max(need, 1);
		have = Mathf.Clamp(have, 0, need);
		_buffBar.MaxValue = need;
		_buffBar.Value = have;
		_buffLabel.Text = have >= need ? "buff!" : $"{need - have} to buff";
	}

	private Label MkLabel(Vector2 pos, int fontSize, Color col)
	{
		var l = new Label { Position = pos };
		l.AddThemeFontSizeOverride("font_size", fontSize);
		l.AddThemeColorOverride("font_color", col);
		l.AddThemeColorOverride("font_outline_color", Colors.Black);
		l.AddThemeConstantOverride("outline_size", 4);
		_root.AddChild(l);
		return l;
	}

	/// <summary>(Re)build one HP cell per health BLOCK, evenly across HpMeterSize (mirrors the Ruh meter).</summary>
	private void BuildHealthMeter(int blockCount)
	{
		foreach (var c in _hpArea.GetChildren())
			c.QueueFree();
		_hpCells.Clear();
		blockCount = Mathf.Max(blockCount, 1);
		_hpCellW = (HpMeterSize.X - HpCellGap * (blockCount - 1)) / blockCount;
		for (int i = 0; i < blockCount; i++)
		{
			float x = i * (_hpCellW + HpCellGap);
			_hpArea.AddChild(new ColorRect { Position = new Vector2(x, 0), Size = new Vector2(_hpCellW, HpMeterSize.Y), Color = HpEmpty });
			var fill = new ColorRect { Position = new Vector2(x, 0), Size = new Vector2(0, HpMeterSize.Y) };
			_hpArea.AddChild(fill);
			_hpCells.Add(fill);
		}
	}

	/// <summary>Fill each block cell from `current` half-blocks (2 per block) — a block shows full / half / empty —
	/// and tint the fill green→orange→red by the overall ratio (same bands as the floating enemy bars).</summary>
	private void UpdateHealthMeter(float current, float maximum)
	{
		Color fill = FloatingHealthBar.ColorForRatio(maximum > 0.0f ? current / maximum : 0.0f);
		for (int i = 0; i < _hpCells.Count; i++)
		{
			float ratio = Mathf.Clamp(current / 2.0f - i, 0.0f, 1.0f); // 2 half-blocks per block
			_hpCells[i].Size = new Vector2(_hpCellW * ratio, _hpCells[i].Size.Y);
			_hpCells[i].Color = fill;
		}
	}

	/// <summary>(Re)build the block cells so there's one per ruh block. Cells laid out evenly across RuhMeterSize.</summary>
	private void BuildRuhMeter(int blockCount)
	{
		foreach (var c in _ruhArea.GetChildren())
			c.QueueFree();
		_ruhCells.Clear();
		blockCount = Mathf.Max(blockCount, 1);
		_ruhCellW = (RuhMeterSize.X - RuhCellGap * (blockCount - 1)) / blockCount;
		for (int i = 0; i < blockCount; i++)
		{
			float x = i * (_ruhCellW + RuhCellGap);
			var bg = new ColorRect { Position = new Vector2(x, 0), Size = new Vector2(_ruhCellW, RuhMeterSize.Y), Color = RuhEmpty };
			_ruhArea.AddChild(bg);
			var fill = new ColorRect { Position = new Vector2(x, 0), Size = new Vector2(0, RuhMeterSize.Y), Color = RuhFill };
			_ruhArea.AddChild(fill);
			_ruhCells.Add(fill);
		}
	}

	private void UpdateRuhMeter(float current)
	{
		float per = _player != null ? _player.RUH_PER_BLOCK : 50.0f;
		for (int i = 0; i < _ruhCells.Count; i++)
		{
			float ratio = Mathf.Clamp(current / per - i, 0.0f, 1.0f);
			_ruhCells[i].Size = new Vector2(_ruhCellW * ratio, _ruhCells[i].Size.Y);
		}
	}

	private static StyleBoxFlat Framed(Color bg, Color border)
	{
		var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
		s.SetBorderWidthAll(2);
		s.SetCornerRadiusAll(3);
		return s;
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
		_player.character_changed += OnCharacterChanged;
		_player.health_changed += OnHealthChanged;
		_player.ruh_changed += OnRuhChanged;
		_player.TreeExiting += Unbind;
		OnCharacterChanged(_player.character);
		OnHealthChanged(_player.health, _player.max_health);
		OnRuhChanged(_player.ruh, _player.ruh_cap);
		SetShown(true);
	}

	private void Unbind()
	{
		if (_player != null && IsInstanceValid(_player))
		{
			_player.character_changed -= OnCharacterChanged;
			_player.health_changed -= OnHealthChanged;
			_player.ruh_changed -= OnRuhChanged;
			_player.TreeExiting -= Unbind;
		}
		_player = null;
		SetShown(false);
	}

	private void SetShown(bool shown)
	{
		_root.Visible = shown;
		_stats.Visible = shown;
		_markers.Visible = shown;
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
		_levelsLabel.Text = $"WAVES  {SaveData.GetCurrentWaves()}   ·   BEST {SaveData.WavesRecord()}";
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

	/// <summary>Heartbeat envelope 0..1: a sharp "lub" thump plus a softer "dub", so the pulse punches.</summary>
	private static float Heartbeat(float t)
	{
		float ph = Mathf.PosMod(t * LowHpBeatHz, 1.0f);
		float lub = Mathf.Exp(-Mathf.Pow(ph / 0.055f, 2.0f));
		float dub = 0.6f * Mathf.Exp(-Mathf.Pow((ph - 0.17f) / 0.07f, 2.0f));
		return Mathf.Min(lub + dub, 1.0f);
	}

	private void OnCharacterChanged(string id)
	{
		_nameLabel.Text = id.ToUpper();
		string path = _player.portrait_path();
		_portrait.Texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		_portrait.Material = PaletteConfig.MakePortraitMaterial();
	}

	private void OnHealthChanged(double current, double maximum)
	{
		int blocks = Mathf.Max(Mathf.RoundToInt((float)maximum / 2.0f), 1); // 2 half-blocks per block
		if (blocks != _hpCells.Count)
			BuildHealthMeter(blocks);
		UpdateHealthMeter((float)current, (float)maximum);
		float ratio = maximum > 0.0 ? (float)(current / maximum) : 0.0f;
		if (ratio >= LowHpRatio)
			_lowHpTarget = 0.0f;
		else
		{
			float t = Mathf.Clamp((LowHpRatio - ratio) / LowHpRatio, 0.0f, 1.0f);
			_lowHpTarget = Mathf.Lerp(LowHpMin, 1.0f, t);
		}
	}

	private void OnRuhChanged(double current, double maximum)
	{
		float per = _player != null ? _player.RUH_PER_BLOCK : 50.0f;
		int blocks = Mathf.Max(Mathf.RoundToInt((float)maximum / per), 1);
		if (blocks != _ruhCells.Count)
			BuildRuhMeter(blocks);
		UpdateRuhMeter((float)current);
		_ruhLabel.Text = $"RUH  {Mathf.FloorToInt((float)current / per)} ▮";
	}

	// --- debug stats panel (top-right) ----------------------------------------

	private void BuildStats()
	{
		_stats = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 1.0f,
			AnchorRight = 1.0f,
			GrowHorizontal = Control.GrowDirection.Begin,
			GrowVertical = Control.GrowDirection.End,
			OffsetLeft = -10.0f,
			OffsetRight = -10.0f,
			OffsetTop = 10.0f,
		};
		var sb = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.55f) };
		sb.SetContentMarginAll(8.0f);
		sb.SetCornerRadiusAll(4);
		_stats.AddThemeStyleboxOverride("panel", sb);
		_statsLabel = new Label();
		_statsLabel.AddThemeFontSizeOverride("font_size", 12);
		_stats.AddChild(_statsLabel);
		AddChild(_stats);
	}


	private string MoveLine(string label, Action a)
	{
		if (a == null)
			return $"{label}: none";
		string kindName = a.Hit != null ? StrikeTypeName(a.Hit.Type) : "—";
		return $"{label}: {a.Id} [{kindName}]  dmg {Dmg(a)}";
	}

	private static string StrikeTypeName(StrikeType t) => StrikeTypeNames[(int)t];

	private static string Dmg(Action a)
	{
		if (a.Hit == null || a.Hit.Segments.Length == 0)
			return "scene";
		var parts = new List<string>();
		foreach (var s in a.Hit.Segments)
			parts.Add(s.Damage.HasValue ? s.Damage.Value.ToString() : "0");
		return parts.Count == 1 ? parts[0] : string.Join("/", parts);
	}

	private static string Yn(bool b) => b ? "y" : "n";
}
