using Godot;
using System.Collections.Generic;
using System.Linq;

namespace MyGame;

/// <summary>
/// Reusable ground enemy — the config-driven "standard type" (kebus/baghel/mazab/matat/tarri/breski/ventilator are all
/// this + a kit). Patrols, aggros/pursues, and attacks (melee when close, ranged otherwise; melee/blast/aoe/
/// projectile/lob selected by config). Carries its own sprite, hurtbox, contact box, floating health bar, and
/// status overlays. C# port of <c>scripts/enemies/enemy.gd</c>; behaviour archetypes subclass it (SleeperEnemy,
/// DiverEnemy).
///
/// MIGRATION NOTES: combat deps (Hit/Hitbox/Strike/…), the UI helpers (FloatingHealthBar/StatusIcons/
/// OverheadStatus), the config readers (Emitters/SfxEnemies/AnimMeta) and the Sfx autoload are ALL typed C# now.
/// Shapes/Nodes helpers are inlined. The <c>[Export]</c>s are set per type by its <see cref="EnemyKit"/>.
/// </summary>
[GlobalClass]
public partial class Enemy : Combatant
{
	protected virtual string FramesPath => "res://resources/enemies/{0}.tres";  // WardenEnemy overrides -> resources/wardens/
	private const string GlowMaterial = "res://resources/enemy_glow.tres";
	private const float NoCloseAttackRange = 30.0f;   // how near an enemy with no close attack walks up to its target

	[Signal] public delegate void DiedEventHandler();
	[Signal] public delegate void DamagedEventHandler(float amount, Node? source);

	[Export] public string EnemyId { get; set; } = "kebus";
	[Export] public string DisplayName { get; set; } = "Kebus";
	[Export] public bool Optional { get; set; }
	[ExportGroup("Stats")]
	[Export] public float MaxHealth { get; set; } = 60.0f;
	[Export] public float Gravity { get; set; } = 900.0f;
	[Export] public Vector2 BodySize { get; set; } = new(18, 30);
	[Export] public Vector2 HurtboxSize { get; set; } = new(20, 34);

	[ExportGroup("Drops")]
	[Export] public int LiraDrop { get; set; } = 1;          // Lira coins on death (RunManager defaults it by tier)
	[Export] public float FigChance { get; set; } = 0.1f;    // chance a kill also drops ONE fada_fig (per-kit override)

	[ExportGroup("Patrol")]
	[Export] public float MoveSpeed { get; set; } = 40.0f;
	[Export] public float PatrolDistance { get; set; } = 90.0f;
	[Export] public float IdleTimeMin { get; set; } = 2.0f;
	[Export] public float IdleTimeMax { get; set; } = 3.0f;
	[Export] public float EdgeCheckX { get; set; } = 14.0f;
	[Export] public int IdleLoopFrom { get; set; } = 1;
	[Export] public int IdleLoopTo { get; set; }

	[ExportGroup("Attacking")]
	/// <summary>The target must be within this many px of the enemy's height for it to attack (or hold its ground).</summary>
	[Export] public float AttackAlignY { get; set; } = 40.0f;
	[Export] public float AttackCooldown { get; set; } = 1.1f;
	/// <summary>An enemy with NO far attack walks in on a lined-up target within this distance, even before it has
	/// noticed him (one with a far attack uses that attack's range instead).</summary>
	[Export] public float EngageRange { get; set; } = 300.0f;

	/// <summary>The close-range attack, if the kit gives one (see <see cref="EnemyAttack"/>).</summary>
	public EnemyAttack? Close { get; set; }
	/// <summary>The far attack, if the kit gives one. Tried when the target is beyond <see cref="Close"/>'s range.</summary>
	public EnemyAttack? Far { get; set; }

	[ExportGroup("Behaviour")]
	[Export] public bool Aggro { get; set; } = true;
	[Export] public float AggroRange { get; set; } = 320.0f; // how near (real distance, px) the player has to be for an
	                                                          // enemy to notice + chase him; otherwise it patrols its spawn spot
	[Export] public float AlertDuration { get; set; } = 5.0f;
	[Export] public bool FriendlyFire { get; set; }
	/// <summary>World Y past which an enemy has fallen off into the void below the platforms → it dies (see _PhysicsProcess).
	/// A bit deeper than the player's own fall line so it's unambiguously the void.</summary>
	private const float FallDeathY = 360.0f;
	/// <summary>Set when this enemy died from falling into the void (RunManager skips its loot — it'd be unreachable).</summary>
	public bool FellOff { get; private set; }
	[Export] public float ContactDamage { get; set; }
	[Export] public float ContactKnockback { get; set; } = 120.0f;
	[Export] public float ContactInterval { get; set; } = 0.6f;

	[ExportGroup("Attack feel")]
	[Export] public float AttackHitstop { get; set; } = 0.18f;
	[Export] public float AttackShake { get; set; } = 2.5f;

	protected enum EState { Idle, Patrol, Attack, Stun, Dead, Rage, Charge }

	protected float Health;
	protected EState State = EState.Idle;
	/// <summary>Which way it faces: +1 right, -1 left.</summary>
	public int Facing { get; private set; } = -1;
	protected bool HasDeath, HasWalk;
	private EnemyAttack? _attack;   // the attack in progress (State == Attack)
	private readonly List<Node> _patrolTrailEmitters = new();
	private readonly Dictionary<string, Dictionary<int, string>> _frameSfx = new(); // anim -> { emitted frame -> cue }
	protected float AttackCd;
	protected float PointA, PointB, PatrolTarget;
	private float _idleTimer;
	protected float StunLeft;
	private BlastStrike? _activeChannel;
	private readonly List<AudioStreamPlayer2D> _attackSfx = new();
	private bool _channelSfx;   // the attack in progress is a held channel (PlayAttackStartSfx)
	private float _dotLeft, _dotTick, _dotAccum;
	private Node? _dotSource;
	private bool _reaped;
	private Node2D? _magnetAnchor;
	private float _magnetArrive = 60.0f, _magnetSpeed = 320.0f, _magnetStun = 3.0f;
	private float _frenemyLeft;
	private float _contactCd;
	private Hitbox? _contactHitbox;
	private bool _idleBack;
	protected bool Engaged;
	private float _alertLeft;
	private bool _hunting; // a STRAGGLER: chases the player wherever he is (see hunt)
	private float _huntSpeedMult = 1.0f; // a straggler's chase-speed multiplier (its walk animation speeds up to match)
	private float _hitstopLeft, _hitstopDur;
	protected bool Impacted;

	protected AnimatedSprite2D Sprite = null!;
	protected Hurtbox Hurt = null!;
	protected FloatingHealthBar Bar = null!;
	private StatusOverlay _status = null!;
	private StatusIcons _statusIcons = null!;
	private OverheadStatus _overhead = null!;
	protected float HeadY;
	private int _shownStatus;   // the reap / stun / charm flags currently displayed (RefreshStatusIcons)
	private RayCast2D _edgeRayLeft = null!, _edgeRayRight = null!;

	public bool LastHitFromSpecial;

	public override void _Ready()
	{
		AddToGroup("enemies");
		CollisionLayer = (uint)Combat.Layer.EnemyBody;
		CollisionMask = Combat.GroundMask;

		Combat.ApplyFloorHandling(this); // shared slope handling (walkable angle, snap, constant speed)

		BuildSprite();
		BuildBody();
		BuildHurtbox();
		BuildContactHitbox();
		BuildHealthBar();
		BuildEdgeRays();

		_status = new StatusOverlay();
		AddChild(_status);
		_status.Setup(Sprite);

		_statusIcons = new StatusIcons();
		AddChild(_statusIcons);
		float barW = Bar.BarWidth;
		float barH = Bar.BarHeight;
		_statusIcons.Position = Bar.Position + new Vector2(barW / 2.0f + 3.0f, -barH / 2.0f);

		_overhead = new OverheadStatus();
		AddChild(_overhead);
		_overhead.Setup(HeadY);

		HasDeath = Sprite.SpriteFrames.HasAnimation("death");
		HasWalk = Sprite.SpriteFrames.HasAnimation("walk");
		BuildFrameSfx();
		BuildPatrolTrail();
		Close?.Attach(this);
		Far?.Attach(this);

		Health = MaxHealth;
		Bar.SetRatio(1.0f);

		PointA = GlobalPosition.X;
		PointB = GlobalPosition.X + PatrolDistance;
		PatrolTarget = PointB;

		Sprite.FrameChanged += OnFrameChanged;
		Sprite.AnimationFinished += OnAnimFinished;
		Face(Facing);
		Play(HasWalk ? "walk" : "idle");
	}

	// --- construction -------------------------------------------------------

	private void BuildSprite()
	{
		Sprite = new AnimatedSprite2D();
		string path = string.Format(FramesPath, EnemyId);
		if (!ResourceLoader.Exists(path))
		{
			GD.PushError($"Enemy '{EnemyId}': no SpriteFrames at {path}");
			return;
		}
		Sprite.SpriteFrames = GD.Load<SpriteFrames>(path);
		if (ResourceLoader.Exists(GlowMaterial))
			Sprite.Material = (Material)GD.Load<Material>(GlowMaterial).Duplicate(); // per-instance so a hit-flash tints only THIS enemy
		AnchorToFeet(Sprite);
		AddChild(Sprite);
	}

	private void BuildBody() => AddChild(MakeBox(BodySize, new Vector2(0, -BodySize.Y / 2.0f)));

	private void BuildHurtbox()
	{
		Hurt = new Hurtbox { CollisionLayer = (uint)Combat.Layer.EnemyHurt, CollisionMask = 0 };
		Hurt.AddChild(MakeBox(HurtboxSize, new Vector2(0, -HurtboxSize.Y / 2.0f)));
		AddChild(Hurt);
		Hurt.Hurt += OnHurt;
	}

	private void BuildContactHitbox()
	{
		if (ContactDamage <= 0.0f)
			return;
		_contactHitbox = new Hitbox
		{
			CollisionLayer = (uint)Combat.Layer.EnemyHit,
			CollisionMask = (uint)Combat.Layer.PlayerHurt,
			Damage = ContactDamage,
			Knockback = ContactKnockback,
			Source = this,
		};
		_contactHitbox.AddChild(MakeBox(HurtboxSize, new Vector2(0, -HurtboxSize.Y / 2.0f)));
		AddChild(_contactHitbox);
	}

	private void BuildEdgeRays()
	{
		_edgeRayLeft = MakeEdgeRay(-EdgeCheckX);
		_edgeRayRight = MakeEdgeRay(EdgeCheckX);
	}

	private RayCast2D MakeEdgeRay(float x)
	{
		// Tall vertical span so a SLOPE isn't mistaken for a cliff. Reaches UP 14px (an upslope's rising floor;
		// HitFromInside also catches steep climbs where the origin embeds in terrain) and DOWN ~a tile (28px) so a
		// DESCENDING floor is still "ahead" and the enemy keeps chasing down instead of stopping at the lip. A true
		// drop deeper than ~a tile still reads as a cliff and halts it (no diving off high platforms).
		var ray = new RayCast2D
		{
			Position = new Vector2(x, -14),
			TargetPosition = new Vector2(0, 42),
			HitFromInside = true,
			CollisionMask = Combat.GroundMask, // platforms count as footing too
		};
		AddChild(ray);
		return ray;
	}

	protected bool FloorAhead(int dir)
	{
		var ray = dir < 0 ? _edgeRayLeft : _edgeRayRight;
		ray.ForceRaycastUpdate();
		return ray.IsColliding();
	}

	private void BuildHealthBar()
	{
		Bar = new FloatingHealthBar { RatioColors = true };
		AddChild(Bar);
		Bar.Setup(DisplayName);
		var frame = Sprite.SpriteFrames.GetFrameTexture("idle", 0);
		HeadY = -(frame != null ? frame.GetHeight() : 70) + 8;
		Bar.Position = new Vector2(0, HeadY);
	}

	// --- loop ---------------------------------------------------------------

	public override void _PhysicsProcess(double delta)
	{
		float d = (float)delta;
		if (State == EState.Dead)
			return;

		// Fell off a platform into the void below → die (frees a spawn-cap slot; no loot, it'd be unreachable).
		if (GlobalPosition.Y > FallDeathY)
		{
			FellOff = true;
			Die();
			return;
		}

		TickDot(d);
		if (State == EState.Dead)
			return;
		RefreshStatusIcons();
		if (_patrolTrailEmitters.Count > 0)
		{
			bool moving = Mathf.Abs(Velocity.X) > 5.0f;
			foreach (var em in _patrolTrailEmitters)
				ParticleNodes.SetEmitting(em, moving);
		}

		if (_frenemyLeft > 0.0f)
		{
			_frenemyLeft -= d;
			if (_frenemyLeft <= 0.0f)
				EndFrenemy();
		}

		if (!IsOnFloor())
			Velocity = new Vector2(Velocity.X, Velocity.Y + Gravity * d);

		if (_hitstopLeft > 0.0f)
		{
			_hitstopLeft -= d;
			Velocity = new Vector2(0.0f, Velocity.Y);
			ApplyShake();
			if (_hitstopLeft <= 0.0f)
				EndHitstop();
			MoveAndSlide();
			return;
		}

		if (_magnetAnchor != null)
		{
			if (!IsInstanceValid(_magnetAnchor))
			{
				_magnetAnchor = null;
			}
			else
			{
				float dx = _magnetAnchor.GlobalPosition.X - GlobalPosition.X;
				if (Mathf.Abs(dx) <= _magnetArrive)
				{
					Velocity = new Vector2(0.0f, Velocity.Y);
					StunLeft = Mathf.Max(StunLeft, _magnetStun);
					SetState(EState.Stun);
					CancelChannel();
					_status.ShowFor(new Color(0.6f, 0.4f, 1.0f, 0.6f), _magnetStun);
					_magnetAnchor = null;
				}
				else
				{
					float speed = _magnetSpeed * Mathf.Clamp(Mathf.Abs(dx) / (_magnetArrive * 2.0f), 0.25f, 1.0f);
					Velocity = new Vector2(Mathf.Sign(dx) * speed, Velocity.Y);
					Face(Mathf.Sign(dx));
					MoveAndSlide();
					return;
				}
			}
		}

		if (State == EState.Stun)
		{
			StunLeft -= d;
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0.0f, 300.0f * d), Velocity.Y);
			if (StunLeft <= 0.0f)
				SetState(EState.Idle);
		}
		else if (State == EState.Attack)
		{
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0.0f, 600.0f * d), Velocity.Y);
		}
		else
		{
			Act(d);
		}

		if (State == EState.Idle)
			KeepIdleLive();
		TickContact(d);
		MoveAndSlide();
	}

	private void KeepIdleLive()
	{
		if (Sprite.Animation != "idle" || !Sprite.IsPlaying())
			Sprite.Play("idle");
	}

	private void IdleBounce()
	{
		if (State != EState.Idle || Sprite.Animation != "idle")
			return;
		int last = Sprite.SpriteFrames.GetFrameCount("idle") - 1;
		int lo = Mathf.Clamp(IdleLoopFrom, 1, Mathf.Max(last, 1));
		int hi = IdleLoopTo > lo ? IdleLoopTo : last;
		hi = Mathf.Clamp(hi, lo, last);
		if (hi <= lo)
			return;
		int f = Sprite.Frame;
		if (!_idleBack)
		{
			if (f >= hi)
			{
				_idleBack = true;
				Sprite.PlayBackwards("idle");
			}
		}
		else if (f <= lo)
		{
			_idleBack = false;
			Sprite.Play("idle");
		}
	}

	private void TickContact(float delta)
	{
		if (_contactHitbox == null || IsFrenemy())
			return;
		_contactCd = Mathf.Max(_contactCd - delta, 0.0f);
		if (_contactCd <= 0.0f)
		{
			_contactHitbox.Activate();
			_contactCd = ContactInterval;
		}
	}

	protected virtual void Act(float delta)
	{
		AttackCd = Mathf.Max(AttackCd - delta, 0.0f);
		_alertLeft = Mathf.Max(_alertLeft - delta, 0.0f);

		if (IsInstanceValid(_activeChannel))
		{
			Velocity = new Vector2(Mathf.MoveToward(Velocity.X, 0.0f, 600.0f * delta), Velocity.Y);
			return;
		}

		Node2D? player = Target();
		if (player != null)
		{
			float toPlayer = player.GlobalPosition.X - GlobalPosition.X;
			float dist = Mathf.Abs(toPlayer);
			bool aligned = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y) <= AttackAlignY;
			EnemyAttack? close = Close is { Usable: true } ? Close : null;
			EnemyAttack? far = Far is { Usable: true } ? Far : null;
			if (aligned && AttackCd <= 0.0f)
			{
				if (close != null && dist <= close.Range)
				{
					StartAttack(close, player);
					return;
				}
				if (far != null && dist <= far.Range)
				{
					StartAttack(far, player);
					return;
				}
			}
			int dir = Mathf.Sign(toPlayer);
			bool pursue = _hunting || _alertLeft > 0.0f
				|| (Aggro && GlobalPosition.DistanceTo(player.GlobalPosition) <= AggroRange);
			bool hold = aligned && dist <= (Far?.Range ?? EngageRange);
			bool closeIn = pursue || (hold && close != null && far == null);
			float reach = (far != null ? far.Range : Close?.Range ?? NoCloseAttackRange) - 4.0f;
			if (pursue || hold)
			{
				Engaged = true;
				if (closeIn && dist > reach && FloorAhead(dir))
				{
					Velocity = new Vector2(dir * MoveSpeed * _huntSpeedMult, Velocity.Y);
					Face(dir);
					SetState(EState.Patrol);
				}
				else
				{
					Velocity = new Vector2(0.0f, Velocity.Y);
					Face(dir);
					SetState(EState.Idle);
				}
				return;
			}
		}

		Engaged = false;
		Patrol(delta);
	}

	private void Patrol(float delta)
	{
		if (_idleTimer > 0.0f)
		{
			_idleTimer -= delta;
			Velocity = new Vector2(0.0f, Velocity.Y);
			SetState(EState.Idle);
			return;
		}

		int dir = Mathf.Sign(PatrolTarget - GlobalPosition.X);
		bool arrived = dir == 0 || Mathf.Abs(PatrolTarget - GlobalPosition.X) <= 2.0f;
		if (arrived || !FloorAhead(dir))
		{
			Velocity = new Vector2(0.0f, Velocity.Y);
			_idleTimer = (float)GD.RandRange(IdleTimeMin, IdleTimeMax);
			PatrolTarget = Mathf.IsEqualApprox(PatrolTarget, PointB) ? PointA : PointB;
			SetState(EState.Idle);
			return;
		}

		Velocity = new Vector2(dir * MoveSpeed, Velocity.Y);
		Face(dir);
		SetState(EState.Patrol);
	}

	// --- attacks ------------------------------------------------------------

	private void StartAttack(EnemyAttack attack, Node2D target)
	{
		_attack = attack;
		SetState(EState.Attack);
		PlayAttackStartSfx(attack.Key, attack.Channels);
		Velocity = new Vector2(0.0f, Velocity.Y);
		Impacted = false;
		Engaged = true;
		Face(Mathf.Sign(target.GlobalPosition.X - GlobalPosition.X));
		attack.Begin();
		Play(attack.Animation);
	}

	private void BuildFrameSfx()
	{
		_frameSfx.Clear();
		var sf = Sprite.SpriteFrames;
		foreach (var (anim, frames) in SfxEnemies.FramesFor(EnemyId))
		{
			if (!sf.HasAnimation(anim))
				continue;
			int start = SheetStart(anim);
			var map = new Dictionary<int, string>();
			foreach (var (sheetFrame, cue) in frames)
				map[sheetFrame - start] = cue;
			_frameSfx[anim] = map;
		}
	}

	/// <summary>Play the start cue of the attack type <paramref name="typeKey"/> (<c>&lt;enemy&gt;.&lt;type&gt;</c>),
	/// cutting off any attack sound still playing. <paramref name="channel"/> = the attack is a held channel: its
	/// sounds (this one and the per-frame ones) play on players a stagger can stop.</summary>
	protected void PlayAttackStartSfx(string typeKey, bool channel = false)
	{
		_channelSfx = channel;
		StopAttackSfx();
		PlayAttackSfx($"{EnemyId}.{typeKey}");
	}

	protected void PlayFrameSfx()
	{
		if (_frameSfx.Count == 0)
			return;
		if (_frameSfx.TryGetValue(Sprite.Animation, out var map) && map.TryGetValue(Sprite.Frame, out string? cue))
			PlayAttackSfx(cue);
	}

	private void PlayAttackSfx(string cue)
	{
		if (cue == "")
			return;
		if (!_channelSfx)
		{
			SfxPlayAt(cue, GlobalPosition);
			return;
		}
		var pl = SfxMakeOneshot2d(cue);
		if (pl == null)
			return;
		AddChild(pl);
		_attackSfx.Add(pl);
		pl.Finished += () =>
		{
			_attackSfx.Remove(pl);
			pl.QueueFree();
		};
		pl.Play();
	}

	protected void StopAttackSfx()
	{
		foreach (var pl in _attackSfx)
			if (IsInstanceValid(pl))
			{
				pl.Stop();
				pl.QueueFree();
			}
		_attackSfx.Clear();
	}

	protected virtual void OnFrameChanged()
	{
		PlayFrameSfx();
		if (State == EState.Attack)
			_attack?.OnFrame(Sprite.Frame);
		else if (State == EState.Idle)
			IdleBounce();
	}

	/// <summary>Slide forward at <paramref name="impulse"/> px/s, the way it faces (a lunging attack's commit frame).</summary>
	public void Lunge(float impulse) => Velocity = new Vector2(impulse * Facing, Velocity.Y);

	/// <summary>Freeze on a melee hit: for as long as the held blast it just started emits, else the usual hitstop.</summary>
	public void BeginStrikeHitstop() =>
		BeginHitstop(IsInstanceValid(_activeChannel) ? _activeChannel!.EmitDuration : AttackHitstop);

	/// <summary>Freeze the animation (and shake) for <paramref name="dur"/> seconds — the default is
	/// <see cref="AttackHitstop"/>. Once per attack: a second call before it ends is ignored.</summary>
	public void BeginHitstop(float dur = -1.0f)
	{
		if (dur < 0.0f)
			dur = AttackHitstop;
		if (Impacted || dur <= 0.0f)
			return;
		Impacted = true;
		_hitstopDur = dur;
		_hitstopLeft = dur;
		Sprite.Pause();
	}

	private void EndHitstop()
	{
		_hitstopLeft = 0.0f;
		Impacted = false;
		Sprite.Position = Vector2.Zero;
		if (State == EState.Attack || State == EState.Rage)
			Sprite.Play();
	}

	private void ApplyShake()
	{
		if (AttackShake <= 0.0f || _hitstopDur <= 0.0f)
		{
			Sprite.Position = Vector2.Zero;
			return;
		}
		float amp = AttackShake * (_hitstopLeft / _hitstopDur);
		Sprite.Position = new Vector2((float)GD.RandRange(-amp, amp), (float)GD.RandRange(-amp, amp));
	}

	protected virtual void OnAnimFinished()
	{
		if (State == EState.Dead)
		{
			QueueFree();
			return;
		}
		if (State == EState.Attack && _attack is { Repeats: true } looping && InReach(looping))
		{
			looping.Begin();
			Impacted = false;
			ReplayFrom(looping.Animation, LoopFrom(looping.Animation));
			return;
		}
		if (State == EState.Attack)
		{
			AttackCd = AttackCooldown;
			SetState(EState.Idle);
		}
	}

	/// <summary>Where the effect row <paramref name="effect"/> sits, relative to the enemy and mirrored by its facing
	/// (<paramref name="fallback"/> if this enemy has no such row).</summary>
	public Vector2 EffectPos(string effect, Vector2 fallback = default)
	{
		Vector2 p = Effect(effect)?.Pos ?? fallback;
		return new Vector2(p.X * Facing, p.Y);
	}

	/// <summary>The scene of this enemy's effect row <paramref name="effect"/>, or null if it has none.</summary>
	public PackedScene? EffectScene(string effect) => Effect(effect)?.Scene;

	private void BuildPatrolTrail()
	{
		var scene = EffectScene("walk_trail");
		if (scene == null)
			return;
		var trail = scene.Instantiate();
		AddChild(trail);
		if (trail is Node2D n)
			n.Position = EffectPos("walk_trail");
		if (trail is CpuParticles2D || trail is GpuParticles2D)
			_patrolTrailEmitters.Add(trail);
		foreach (var e in trail.FindChildren("*", "CpuParticles2D", true, false))
			_patrolTrailEmitters.Add(e);
		foreach (var e in trail.FindChildren("*", "GpuParticles2D", true, false))
			_patrolTrailEmitters.Add(e);
		foreach (var em in _patrolTrailEmitters)
			ParticleNodes.SetEmitting(em, false);
	}

	protected Node2D? MakeVfx(string effect)
	{
		var scene = EffectScene(effect);
		if (scene == null)
			return null;
		var node = scene.Instantiate();
		if (node is Node2D n)
			n.Position = EffectPos(effect);
		return node as Node2D;
	}

	protected int LoopFrom(StringName anim) => Mathf.Max(AnimMeta.LoopFrom(Sprite.SpriteFrames, anim), 0);

	protected void ReplayFrom(StringName anim, int from)
	{
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
		int last = Sprite.SpriteFrames.GetFrameCount(anim) - 1;
		Sprite.SetFrameAndProgress(Mathf.Clamp(from, 0, last), 0.0f);
		Sprite.Play();
	}

	private bool InReach(EnemyAttack attack)
	{
		var t = Target();
		if (t == null)
			return false;
		Vector2 to = t.GlobalPosition - GlobalPosition;
		return Mathf.Abs(to.Y) <= AttackAlignY && Mathf.Abs(to.X) <= attack.Range;
	}

	/// <summary>Spawn a self-contained attack SCENE (Strike or Projectile), mirror by facing, inject tuning, arm it.</summary>
	public Node2D? SpawnAttack(PackedScene? scene, SegmentData tuning, bool toWorld = false, Vector2 at = default)
	{
		if (scene == null)
			return null;
		if (scene.Instantiate() is not Node2D node)
			return null;
		if (node is ISidedAttack attack)
		{
			attack.Hostile = !IsFrenemy();
			attack.FriendlyFire = FriendlyFire;
			attack.Source = this;
		}
		node.Scale = new Vector2(Mathf.Abs(node.Scale.X) * Facing, node.Scale.Y);
		node.Position = at;
		if (toWorld)
			GetParent().AddChild(node);
		else
			AddChild(node);
		if (node is ITunable tn)
			tn.ApplyTuning(tuning, this);
		foreach (var a in node.FindChildren("*", "Area2D", true, false))
			if (a is Hitbox hb)
			{
				hb.Source = this;
				hb.Activate();
			}
		if (!toWorld && node is BlastStrike bs)
			_activeChannel = bs;
		return node;
	}

	protected void CancelChannel()
	{
		if (IsInstanceValid(_activeChannel) && _activeChannel!.InterruptOnHurt)
		{
			_activeChannel.Cancel();
			_activeChannel = null;
			StopAttackSfx();
		}
	}

	public IReadOnlyList<int> HitFramesOf(StringName anim) =>
		AnimMeta.HitFrames(Sprite.SpriteFrames, anim);

	public bool HasAnimation(StringName anim) => Sprite.SpriteFrames.HasAnimation(anim);

	public int FrameCount(StringName anim) => Sprite.SpriteFrames.GetFrameCount(anim);


	// --- damage / death -----------------------------------------------------

	protected virtual void OnHurt(Hit hit)
	{
		if (State == EState.Dead)
			return;
		if (hit.Gust > 0.0f)
		{
			// A GUST (a charmed Ventilator's wind): no damage — just flung, and held in stun so the AI doesn't cancel it.
			Velocity = GustVelocity(hit, Facing);
			StunLeft = Mathf.Max(StunLeft, Combat.GustEnemyStagger);
			SetState(EState.Stun);
			CancelChannel();
			return;
		}
		LastHitFromSpecial = hit.FromSpecial;
		float before = Health;
		Health = Mathf.Max(Health - hit.Amount, 0.0f);
		EmitSignalDamaged(before - Health, hit.Source);
		Bar.SetRatio(Health / MaxHealth);
		HitReact(Sprite, hit.Amount);
		if (AlertDuration > 0.0f)
		{
			_alertLeft = AlertDuration;
			if (hit.Source is Node2D src)
				Face(Mathf.Sign(src.GlobalPosition.X - GlobalPosition.X));
		}
		if (Health <= 0.0f)
		{
			Die();
			return;
		}
		if (hit.VictimVfx != null)
			SpawnVictimVfx(hit.VictimVfx, hit.VictimVfxTime, HurtboxSize.Y, true);
		if (hit.FrenemyTime > 0.0f)
			BecomeFrenemy(hit.FrenemyTime);
		if (hit.DotPercent > 0.0f && hit.DotTime > 0.0f && !_reaped)
		{
			_reaped = true;
			_dotTick = MaxHealth * hit.DotPercent;
			_dotLeft = hit.DotTime;
			_dotSource = hit.Source;
		}
		float stagger = ApplyKnockback(hit, Facing);
		if (stagger > 0.0f)
		{
			StunLeft = Mathf.Max(StunLeft, stagger);
			SetState(EState.Stun);
			CancelChannel();
			if (hit.StatusColor.A > 0.0f)
				_status.ShowFor(hit.StatusColor, hit.StatusTime);
		}
	}

	/// <summary>Death SFX cue key — overridable so Wardens get their own (see WardenEnemy).</summary>
	protected virtual string DeathSfxKey() => "enemy_death";

	protected virtual void Die()
	{
		SetState(EState.Dead);
		CancelChannel();
		StopAttackSfx();
		SfxPlayAt(DeathSfxKey(), GlobalPosition);
		EmitSignal(SignalName.Died);
		RemoveFromGroup("enemies");
		Hurt.SetDeferred(Area2D.PropertyName.Monitorable, false);
		SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 0);
		Bar.Visible = false;
		_statusIcons.SetActive(new List<StatusType>());
		_overhead.SetActive(new List<StatusType>());
		_status.Clear();
		if (HasDeath)
			Play("death");
		else
			FadeAndFree();
	}

	private void FadeAndFree()
	{
		var tw = CreateTween();
		tw.TweenInterval(0.4);
		tw.TweenProperty(Sprite, "modulate:a", 0.0, 0.6);
		tw.TweenCallback(Callable.From(QueueFree));
	}

	// --- reap ---------------------------------------------------------------

	private void TickDot(float delta)
	{
		if (_dotLeft <= 0.0f)
			return;
		_dotLeft -= delta;
		_dotAccum += delta;
		while (_dotAccum >= 1.0f && State != EState.Dead)
		{
			_dotAccum -= 1.0f;
			ReapTick();
		}
		if (_dotLeft <= 0.0f)
		{
			_dotAccum = 0.0f;
			_dotSource = null;
		}
	}

	private void ReapTick()
	{
		float before = Health;
		Health = Mathf.Max(Health - _dotTick, 0.0f);
		float dealt = before - Health;
		if (dealt <= 0.0f)
			return;
		LastHitFromSpecial = false;
		EmitSignalDamaged(dealt, IsInstanceValid(_dotSource) ? _dotSource : null);
		Bar.SetRatio(Health / MaxHealth);
		if (Health <= 0.0f)
		{
			_dotLeft = 0.0f;
			Die();
		}
	}

	// --- status pips --------------------------------------------------------

	/// <summary>Show the statuses this enemy is under (pips beside the health bar + the overhead halo). Runs every
	/// tick, so it compares three flags and only builds the list when one of them changed.</summary>
	private void RefreshStatusIcons()
	{
		bool reap = _dotLeft > 0.0f;
		bool stun = State == EState.Stun || StunLeft > 0.0f;
		bool charm = _frenemyLeft > 0.0f;
		int shown = (reap ? 1 : 0) | (stun ? 2 : 0) | (charm ? 4 : 0);
		if (shown == _shownStatus)
			return;
		_shownStatus = shown;
		var ids = new List<StatusType>();
		if (reap) ids.Add(StatusType.Reap);
		if (stun) ids.Add(StatusType.Stun);
		if (charm) ids.Add(StatusType.Charm);
		_statusIcons.SetActive(ids);
		_overhead.SetActive(ids);
	}

	// --- helpers ------------------------------------------------------------

	protected Node2D? Player()
	{
		var p = GetTree().GetFirstNodeInGroup("player") as MyGame.Player;
		return p != null && p.IsDead() ? null : p;
	}

	public bool IsFrenemy() => _frenemyLeft > 0.0f;

	/// <summary>Who it is fighting: the player — or, while charmed, the nearest enemy that is not. Null if there is none.</summary>
	public Node2D? Target() => IsFrenemy() ? NearestHostileEnemy() : Player();

	private Node2D? NearestHostileEnemy()
	{
		Node2D? best = null;
		float bestD = float.PositiveInfinity;
		foreach (var e in GetTree().GetNodesInGroup("enemies"))
		{
			if (e == this || e is not Enemy n || n.IsFrenemy())
				continue;
			float dd = GlobalPosition.DistanceSquaredTo(n.GlobalPosition);
			if (dd < bestD)
			{
				bestD = dd;
				best = n;
			}
		}
		return best;
	}

	public void BecomeFrenemy(float duration)
	{
		if (duration <= 0.0f || State == EState.Dead)
			return;
		_frenemyLeft = Mathf.Max(_frenemyLeft, duration);
		_contactHitbox?.SetDeferred(Area2D.PropertyName.Monitoring, false);
	}

	private void EndFrenemy() => _frenemyLeft = 0.0f;

	public void Magnetize(Node2D anchor, float arriveDist, float speed, float stunTime)
	{
		if (State == EState.Dead || anchor == null)
			return;
		_magnetAnchor = anchor;
		_magnetArrive = arriveDist;
		_magnetSpeed = speed;
		_magnetStun = stunTime;
	}

	public void ApplyHit(Hit hit) => Hurt?.TakeHit(hit);

	/// <summary>Make this enemy a STRAGGLER: from now on it chases the player wherever he is, ignoring
	/// <see cref="AggroRange"/>, at <paramref name="speedMult"/> × its move speed (RunManager calls it on a round's last
	/// few, so the round can't stall on one he can't find).</summary>
	public void Hunt(float speedMult)
	{
		_hunting = true;
		_huntSpeedMult = speedMult;
		if (State == EState.Patrol)
			PlayWalk(); // already walking — pick up the faster pace now
	}

	protected void Face(int dir)
	{
		if (dir == 0)
			return;
		Facing = dir;
		Sprite.FlipH = dir < 0;
	}

	protected void SetState(EState state)
	{
		if (State == state)
			return;
		State = state;
		switch (state)
		{
			case EState.Idle:
				_idleBack = false;
				Play("idle");
				break;
			case EState.Stun:
				Sprite.Pause();
				break;
			case EState.Patrol:
				PlayWalk();
				break;
		}
	}

	/// <summary>The walk (or idle, if it has none) — sped up by <see cref="_huntSpeedMult"/> for a straggler so its feet
	/// keep pace with its chase.</summary>
	private void PlayWalk() => Sprite.Play(HasWalk ? "walk" : "idle", _huntSpeedMult);

	protected void Play(StringName anim)
	{
		if (Sprite.Animation != anim || !Sprite.IsPlaying())
			Sprite.Play(anim);
	}

	// --- bridges (GDScript configs / UI / autoload / util) ------------------

	/// <summary>This enemy's row <paramref name="effect"/> in <see cref="EmittersEnemies"/>, or null.</summary>
	public EmitterDef? Effect(string effect) => Emitters.EnemyEffect(EnemyId, effect);

	public int SheetStart(StringName anim) =>
		AnimMeta.SheetStart(Sprite.SpriteFrames, anim);

	protected void SfxPlayAt(string cue, Vector2 pos) =>
		GetNodeOrNull<Sfx>("/root/Sfx")?.PlayAt(cue, pos);

	private AudioStreamPlayer2D? SfxMakeOneshot2d(string cue) =>
		GetNodeOrNull<Sfx>("/root/Sfx")?.MakeOneshot2D(cue);

	public static CollisionShape2D MakeBox(Vector2 size, Vector2 offset = default) =>
		new() { Position = offset, Shape = new RectangleShape2D { Size = size } };
}
