using Godot;

namespace MyGame;

/// <summary>
/// The player HUD — an autoload, so it exists in every scene; it binds to whatever <see cref="Player"/> enters the tree
/// and hides when there's none. Built entirely in code. It owns the screen layout and the binding; each part is its own
/// class: the <see cref="HudGauge"/> (health stars, Ruh orbs, special bar), the <see cref="RoundBanner"/> (ROUND / n LEFT
/// / BEST, top centre), the Lira + Fada Fig <see cref="CurrencyCounter"/>s and the <see cref="VialRow"/> (top-left), the
/// <see cref="BuffList"/> (top-right), the <see cref="OffscreenMarkers"/> (enemy arrows), the
/// <see cref="LowHealthVignette"/>, and the Esc <see cref="PauseMenu"/>. The run pushes values in through the
/// <c>Set…</c> methods here.
/// </summary>
public partial class HUD : CanvasLayer
{
	private static readonly Vector2 CurrencyPos = new(16, 14);

	private Player? _player;
	private Control _root = null!;                 // the full-screen root of the screen HUD
	private OffscreenMarkers _markers = null!;
	private HudGauge _gauge = null!;
	private LowHealthVignette _lowHealth = null!;
	private PauseMenu _pauseMenu = null!;
	private CurrencyCounter _liraCounter = null!;
	private CurrencyCounter _figCounter = null!;
	private VialRow _vialRow = null!;
	private RoundBanner _roundBanner = null!;
	private BuffList _buffList = null!;

	public override void _Ready()
	{
		Layer = UiLayers.Hud;
		BuildLog.PrintLastCompile(); // DEBUG: the first autoload to start says how long the last compile took
		UiStyle.Install(); // first UI to exist (autoload) — make Sixtyfour the global fallback font before anything builds
		BuildHud();
		_lowHealth = new LowHealthVignette();
		AddChild(_lowHealth);
		_pauseMenu = new PauseMenu();
		AddChild(_pauseMenu);
		_pauseMenu.GaugePlacementChanged += _gauge.ApplyPlacement;
		SetShown(false);
		GetTree().NodeAdded += OnNodeAdded;
		SetProcess(true);
		var existing = FindPlayer();
		if (existing != null)
			Bind(existing);
	}

	private void BuildHud()
	{
		_root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiStyle.Theme };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		_markers = new OffscreenMarkers();
		AddChild(_markers);

		_gauge = new HudGauge(_root);
		AddChild(_gauge);

		var currencies = new VBoxContainer { Position = CurrencyPos, MouseFilter = Control.MouseFilterEnum.Ignore };
		currencies.AddThemeConstantOverride("separation", 4);
		_root.AddChild(currencies);
		_liraCounter = new CurrencyCounter("res://assets/things/lira.png");
		currencies.AddChild(_liraCounter);
		_figCounter = new CurrencyCounter("res://assets/things/fada_fig.png");
		currencies.AddChild(_figCounter);
		_vialRow = new VialRow();
		currencies.AddChild(_vialRow);

		_roundBanner = new RoundBanner(_root);
		_root.AddChild(_roundBanner);

		_buffList = new BuffList();
		_root.AddChild(_buffList);
	}

	// --- what the run pushes in ------------------------------------------------

	/// <summary>Show round <paramref name="round"/> (0 = before round 1: blank), <paramref name="left"/> quota enemies
	/// remaining (0 = hidden — RunManager only passes it once few remain), and the <paramref name="best"/> round record.</summary>
	public void SetRound(int round, int left, int best) => _roundBanner.SetRound(round, left, best);

	/// <summary>Show the carried Dekken vials: a held vial's name per slot, the <paramref name="selected"/> one (what the
	/// drink key drinks) framed in the accent colour.</summary>
	public void SetVials(IReadOnlyList<string> names, int selected) => _vialRow.SetVials(names, selected);

	/// <summary>Rebuild the top-right active-buff list from the player's passives (call on grant / clear).</summary>
	public void RefreshBuffs(List<Passive> passives) => _buffList.SetPassives(passives);

	// --- binding --------------------------------------------------------------

	private void OnNodeAdded(Node node)
	{
		if (node is Player p)
			Bind(p);
	}

	private Player? FindPlayer()
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
		_player.HealthChanged += OnHealthChanged;
		_player.RuhChanged += OnRuhChanged;
		_player.TreeExiting += Unbind;
		_player.Wallet.Changed += ShowWallet;
		ShowWallet();
		_gauge.Bind(player);
		SyncHealth((float)_player.Health, (float)_player.MaxHealth, false); // seed silently — no pop-in on bind
		_gauge.SetRuh((float)_player.Ruh, (float)_player.RuhCap, false);
		SetShown(true);
	}

	private void Unbind()
	{
		if (_player != null && IsInstanceValid(_player))
		{
			_player.HealthChanged -= OnHealthChanged;
			_player.RuhChanged -= OnRuhChanged;
			_player.TreeExiting -= Unbind;
			_player.Wallet.Changed -= ShowWallet;
		}
		_player = null;
		_gauge.Bind(null);
		SetShown(false);
	}

	private void SetShown(bool shown)
	{
		_root.Visible = shown;
		_markers.Visible = shown;
		_gauge.WorldLayerShown = shown;
		_pauseMenu.Enabled = shown; // Esc pauses only during a run
		if (!shown)
			_lowHealth.Clear();
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
		_lowHealth.Tick(delta);
		_gauge.Tick(delta, _player.SpecialReady(), _lowHealth.Active);
	}

	/// <summary>The bound player's Lira or Fada Figs changed (a gain pops its icon).</summary>
	private void ShowWallet()
	{
		if (_player == null)
			return;
		_liraCounter.SetCount(_player.Wallet.Lira);
		_figCounter.SetCount(_player.Wallet.FadaFigs);
	}

	private void OnHealthChanged(double current, double maximum) => SyncHealth((float)current, (float)maximum, true);

	private void OnRuhChanged(double current, double maximum) => _gauge.SetRuh((float)current, (float)maximum, true);

	private void SyncHealth(float current, float maximum, bool animate)
	{
		_gauge.SetHealth(current, maximum, animate);
		_lowHealth.SetHealthRatio(maximum > 0.0f ? current / maximum : 0.0f);
	}
}
