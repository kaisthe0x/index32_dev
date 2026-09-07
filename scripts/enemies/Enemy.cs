using Godot;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// Reusable ground enemy — the config-driven "standard type" (kebus/baghel/mazab/matat/tarri/breski are all
/// this + a kit). Patrols, aggros/pursues, and attacks (melee when close, ranged otherwise; melee/blast/aoe/
/// projectile/lob selected by config). Carries its own sprite, hurtbox, contact box, floating health bar, and
/// status overlays. C# port of <c>scripts/enemies/enemy.gd</c>; behaviour archetypes subclass it (SleeperEnemy,
/// DiverEnemy).
///
/// MIGRATION NOTES: combat deps (Hit/Hitbox/Strike/…), the UI helpers (FloatingHealthBar/StatusIcons/
/// OverheadStatus), the config readers (Emitters/SfxEnemies/AnimMeta) and the Sfx autoload are ALL typed C# now.
/// Shapes/Nodes helpers are inlined. Snake_case public surface (kit-set by RunManager); idiomatic PascalCase
/// internal surface (the C# subclasses use it). Remaining dynamic Call/Get is C#→C# polymorphism (apply_tuning,
/// is_dead/is_frenemy), not a GDScript bridge.
/// </summary>
[GlobalClass]
public partial class Enemy : Combatant
{
	protected virtual string FramesPath => "res://resources/enemies/{0}.tres";  // WardenEnemy overrides -> resources/wardens/
	private const string GlowMaterial = "res://resources/enemy_glow.tres";
	private static readonly Vector2 DefaultMuzzle = new(20, -46);

	[Signal] public delegate void diedEventHandler();
	[Signal] public delegate void damagedEventHandler(float amount, Node source);

	[Export] public string enemy_id { get; set; } = "kebus";
	[Export] public string display_name { get; set; } = "Kebus";
	[Export] public bool optional { get; set; }
	[Export] public string close_type { get; set; } = "";
	[Export] public string far_type { get; set; } = "";
	[Export] public bool conform_ground { get; set; } // a ground AoE (Matat/Nasen) hugs the terrain surface on spawn

	[ExportGroup("Stats")]
	[Export] public float max_health { get; set; } = 60.0f;
	[Export] public float gravity { get; set; } = 900.0f;
	[Export] public Vector2 body_size { get; set; } = new(18, 30);
	[Export] public Vector2 hurtbox_size { get; set; } = new(20, 34);

	[ExportGroup("Patrol")]
	[Export] public float move_speed { get; set; } = 40.0f;
	[Export] public int fada_fig_drop { get; set; } = 1;   // fada_figs dropped on death (RunManager defaults it by tier)
	[Export] public float patrol_distance { get; set; } = 90.0f;
	[Export] public float idle_time_min { get; set; } = 2.0f;
	[Export] public float idle_time_max { get; set; } = 3.0f;
	[Export] public float edge_check_x { get; set; } = 14.0f;
	[Export] public int idle_loop_from { get; set; } = 1;
	[Export] public int idle_loop_to { get; set; }

	[ExportGroup("Combat ranges")]
	[Export] public float close_range { get; set; } = 30.0f;
	[Export] public float far_range { get; set; } = 300.0f;
	[Export] public float attack_align_y { get; set; } = 40.0f;
	[Export] public float attack_cooldown { get; set; } = 1.1f;
	[Export] public bool attack_loops { get; set; }
	[Export] public float close_damage { get; set; } = 12.0f;
	[Export] public float far_damage { get; set; } = 8.0f;
	[Export] public float close_knockback { get; set; } = 90.0f;
	[Export] public float close_stun { get; set; }
	[Export] public float far_knockback { get; set; }
	[Export] public float far_stun { get; set; }
	[Export] public float close_hitbox_x { get; set; } = 20.0f;
	[Export] public Vector2 close_hitbox_extents { get; set; } = new(16, 16);
	[Export] public float close_strike_lifetime { get; set; } = 0.15f;
	[Export] public float close_lunge { get; set; }  // forward impulse on the hit frame (0 = none); a lunge-type attack (Kroj) slides in
	[Export] public float projectile_speed { get; set; } = 260.0f;
	[Export(PropertyHint.Enum, "aimed,forward,ground_wave,lob")] public string far_mode { get; set; } = "aimed";
	[Export] public float far_travel { get; set; } = 100.0f;
	[Export] public float far_aim_cap { get; set; } = 0.0f; // aimed mode: cap the shot's tilt to ±this° off horizontal (0 = no cap); keeps it off vertical
	[Export] public Vector2 far_hitbox_extents { get; set; } = new(5, 5);
	[Export] public Vector2 far_hitbox_offset { get; set; } = Vector2.Zero;

	[ExportSubgroup("Lob (far_mode = lob)")]
	[Export] public float lob_arc_time { get; set; } = 0.9f;
	[Export] public float lob_gravity { get; set; } = 900.0f;
	[Export] public float lob_dwell { get; set; } = 1.0f;
	[Export] public float lob_max_life { get; set; } = 3.0f;
	[Export] public Vector2 lob_explosion_extents { get; set; } = new(48, 26);
	[Export] public float lob_land_offset { get; set; } = 22.0f;

	[ExportGroup("Behaviour")]
	[Export] public bool aggro { get; set; } = true;
	[Export] public float aggro_range { get; set; } = 480.0f;
	[Export] public float alert_duration { get; set; } = 5.0f;
	[Export] public bool friendly_fire { get; set; }
	[Export] public float contact_damage { get; set; }
	[Export] public float contact_knockback { get; set; } = 120.0f;
	[Export] public float contact_interval { get; set; } = 0.6f;

	[ExportGroup("Attack feel")]
	[Export] public float attack_hitstop { get; set; } = 0.18f;
	[Export] public float attack_shake { get; set; } = 2.5f;

	protected enum EState { Idle, Patrol, Close, Far, Stun, Dead, Rage, Charge }

	protected float Health;
	protected EState State = EState.Idle;
	protected int Facing = -1;
	protected bool HasClose, HasFar, HasDeath, HasWalk;
	protected string CloseAnim = "", FarAnim = "";
	private readonly List<Node> _patrolTrailEmitters = new();
	private GDict _frameSfx = new();
	protected float AttackCd;
	protected bool AttackFired;
	protected float PointA, PointB, PatrolTarget;
	private float _idleTimer;
	protected float StunLeft;
	private BlastStrike? _activeChannel;
	private bool _isChannel;
	private readonly List<AudioStreamPlayer2D> _attackSfx = new();
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
	private float _hitstopLeft, _hitstopDur;
	protected bool Impacted;

	protected AnimatedSprite2D Sprite = null!;
	protected Hurtbox Hurt = null!;
	protected FloatingHealthBar Bar = null!;
	private StatusOverlay _status = null!;
	private StatusIcons _statusIcons = null!;
	private OverheadStatus _overhead = null!;
	protected float HeadY;
	private string _shownStatus = "";
	private RayCast2D _edgeRayLeft = null!, _edgeRayRight = null!;

	public bool last_hit_from_special;

	public override void _Ready()
	{
		AddToGroup("enemies");
		CollisionLayer = (uint)Combat.Layer.EnemyBody;
		CollisionMask = (uint)Combat.Layer.World;

		// Slope-friendly floor handling: snap keeps them glued to the ground going DOWN a slope (no float/bounce);
		// constant speed stops them slowing to a crawl going UP one. Default snap (1px) detaches on any descent.
		UpDirection = Vector2.Up;
		FloorSnapLength = 16.0f;
		FloorConstantSpeed = true;

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

		CloseAnim = close_type != "" ? "attack_" + close_type : "";
		FarAnim = far_type != "" ? "attack_" + far_type : "";
		HasClose = CloseAnim != "" && Sprite.SpriteFrames.HasAnimation(CloseAnim);
		HasFar = FarAnim != "" && Sprite.SpriteFrames.HasAnimation(FarAnim);
		HasDeath = Sprite.SpriteFrames.HasAnimation("death");
		HasWalk = Sprite.SpriteFrames.HasAnimation("walk");
		BuildFrameSfx();
		BuildPatrolTrail();
		if (HasClose)
		{
			float reach = MeleeReach();
			if (reach > 0.0f)
				close_range = reach;
		}
		if (HasFar && (far_mode == "forward" || far_mode == "ground_wave"))
			far_range = Mathf.Min(far_range, far_travel);

		Health = max_health;
		Bar.SetRatio(1.0f);

		PointA = GlobalPosition.X;
		PointB = GlobalPosition.X + patrol_distance;
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
		string path = string.Format(FramesPath, enemy_id);
		if (!ResourceLoader.Exists(path))
		{
			GD.PushError($"Enemy '{enemy_id}': no SpriteFrames at {path}");
			return;
		}
		Sprite.SpriteFrames = GD.Load<SpriteFrames>(path);
		if (ResourceLoader.Exists(GlowMaterial))
			Sprite.Material = GD.Load<Material>(GlowMaterial);
		AnchorToFeet(Sprite);
		AddChild(Sprite);
	}

	private void BuildBody() => AddChild(MakeBox(body_size, new Vector2(0, -body_size.Y / 2.0f)));

	private void BuildHurtbox()
	{
		Hurt = new Hurtbox { CollisionLayer = (uint)Combat.Layer.EnemyHurt, CollisionMask = 0 };
		Hurt.AddChild(MakeBox(hurtbox_size, new Vector2(0, -hurtbox_size.Y / 2.0f)));
		AddChild(Hurt);
		Hurt.hurt += OnHurt;
	}

	private void BuildContactHitbox()
	{
		if (contact_damage <= 0.0f)
			return;
		_contactHitbox = new Hitbox
		{
			CollisionLayer = (uint)Combat.Layer.EnemyHit,
			CollisionMask = (uint)Combat.Layer.PlayerHurt,
			damage = contact_damage,
			knockback = contact_knockback,
			source = this,
		};
		_contactHitbox.AddChild(MakeBox(hurtbox_size, new Vector2(0, -hurtbox_size.Y / 2.0f)));
		AddChild(_contactHitbox);
	}

	private void BuildEdgeRays()
	{
		_edgeRayLeft = MakeEdgeRay(-edge_check_x);
		_edgeRayRight = MakeEdgeRay(edge_check_x);
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
			CollisionMask = (uint)Combat.Layer.World,
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
		Bar.Setup(display_name);
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

		TickDot(d);
		if (State == EState.Dead)
			return;
		RefreshStatusIcons();
		if (_patrolTrailEmitters.Count > 0)
		{
			bool moving = Mathf.Abs(Velocity.X) > 5.0f;
			foreach (var em in _patrolTrailEmitters)
				em.Set("emitting", moving);
		}

		if (_frenemyLeft > 0.0f)
		{
			_frenemyLeft -= d;
			if (_frenemyLeft <= 0.0f)
				EndFrenemy();
		}

		if (!IsOnFloor())
			Velocity = new Vector2(Velocity.X, Velocity.Y + gravity * d);

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
		else if (State == EState.Close || State == EState.Far)
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
		int lo = Mathf.Clamp(idle_loop_from, 1, Mathf.Max(last, 1));
		int hi = idle_loop_to > lo ? idle_loop_to : last;
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
		if (_contactHitbox == null || is_frenemy())
			return;
		_contactCd = Mathf.Max(_contactCd - delta, 0.0f);
		if (_contactCd <= 0.0f)
		{
			_contactHitbox.activate();
			_contactCd = contact_interval;
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
			bool aligned = Mathf.Abs(player.GlobalPosition.Y - GlobalPosition.Y) <= attack_align_y;
			if (aligned && AttackCd <= 0.0f)
			{
				if (HasClose && dist <= close_range)
				{
					StartAttack(EState.Close, CloseAnim, player);
					return;
				}
				if (HasFar && dist <= far_range)
				{
					StartAttack(EState.Far, FarAnim, player);
					return;
				}
			}
			int dir = Mathf.Sign(toPlayer);
			bool pursue = _alertLeft > 0.0f || (aggro && dist <= aggro_range);
			bool hold = aligned && dist <= far_range;
			bool closeIn = pursue || (hold && HasClose && !HasFar);
			float reach = (HasFar ? far_range : close_range) - 4.0f;
			if (pursue || hold)
			{
				Engaged = true;
				if (closeIn && dist > reach && FloorAhead(dir))
				{
					Velocity = new Vector2(dir * move_speed, Velocity.Y);
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
			_idleTimer = (float)GD.RandRange(idle_time_min, idle_time_max);
			PatrolTarget = Mathf.IsEqualApprox(PatrolTarget, PointB) ? PointA : PointB;
			SetState(EState.Idle);
			return;
		}

		Velocity = new Vector2(dir * move_speed, Velocity.Y);
		Face(dir);
		SetState(EState.Patrol);
	}

	// --- attacks ------------------------------------------------------------

	protected void StartAttack(EState state, StringName anim, Node2D player)
	{
		SetState(state);
		PlayAttackStartSfx(anim);
		Velocity = new Vector2(0.0f, Velocity.Y);
		AttackFired = false;
		Impacted = false;
		Engaged = true;
		Face(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X));
		Play(anim);
	}

	private void BuildFrameSfx()
	{
		_frameSfx = new GDict();
		var byAnim = SfxEnemies.FramesFor(enemy_id);
		var sf = Sprite.SpriteFrames;
		foreach (var animKey in byAnim.Keys)
		{
			var a = (StringName)animKey.AsString();
			if (!sf.HasAnimation(a))
				continue;
			int start = SheetStart(a);
			var map = new GDict();
			var frames = byAnim[animKey].AsGodotDictionary();
			foreach (var sheetFrame in frames.Keys)
				map[sheetFrame.AsInt32() - start] = frames[sheetFrame];
			_frameSfx[a.ToString()] = map;
		}
	}

	protected void PlayAttackStartSfx(StringName anim)
	{
		string kind = (FarAnim != "" && anim == FarAnim) ? far_type : close_type;
		StopAttackSfx();
		PlayAttackSfx($"{enemy_id}.{kind}");
	}

	protected void PlayFrameSfx()
	{
		if (_frameSfx.Count == 0)
			return;
		if (!_frameSfx.TryGetValue(Sprite.Animation.ToString(), out var mapV))
			return;
		var map = mapV.AsGodotDictionary();
		string cue = map.TryGetValue(Sprite.Frame, out var c) ? c.AsString() : "";
		PlayAttackSfx(cue);
	}

	private void PlayAttackSfx(string cue)
	{
		if (cue == "")
			return;
		if (!_isChannel)
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
		if (State == EState.Close && HitFramesOf(CloseAnim).Contains(Sprite.Frame))
		{
			SpawnMeleeStrike(MeleeVfxKey(Sprite.Frame));
			if (close_lunge > 0.0f)
				// Lunge forward on the commit frame; slides to a stop via the Close-state deceleration.
				// No hitstop — it would zero the velocity and freeze the slide (this IS the Zahluq pattern).
				Velocity = new Vector2(close_lunge * Facing, Velocity.Y);
			else
			{
				float hold = IsInstanceValid(_activeChannel) ? _activeChannel!.emit_duration : attack_hitstop;
				BeginHitstop(hold);
			}
		}
		else if (State == EState.Far && !AttackFired && Sprite.Frame >= FireFrame())
		{
			AttackFired = true;
			FireProjectile();
			BeginHitstop();
		}
		else if (State == EState.Idle)
		{
			IdleBounce();
		}
	}

	protected void BeginHitstop(float dur = -1.0f)
	{
		if (dur < 0.0f)
			dur = attack_hitstop;
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
		if (State == EState.Close || State == EState.Far || State == EState.Rage)
			Sprite.Play();
	}

	private void ApplyShake()
	{
		if (attack_shake <= 0.0f || _hitstopDur <= 0.0f)
		{
			Sprite.Position = Vector2.Zero;
			return;
		}
		float amp = attack_shake * (_hitstopLeft / _hitstopDur);
		Sprite.Position = new Vector2((float)GD.RandRange(-amp, amp), (float)GD.RandRange(-amp, amp));
	}

	protected virtual void OnAnimFinished()
	{
		if (State == EState.Dead)
		{
			QueueFree();
			return;
		}
		if (State == EState.Close && attack_loops && InCloseReach())
		{
			AttackFired = false;
			Impacted = false;
			ReplayFrom(CloseAnim, LoopFrom(CloseAnim));
			return;
		}
		if (State == EState.Close || State == EState.Far)
		{
			AttackCd = attack_cooldown;
			SetState(EState.Idle);
		}
	}

	protected Vector2 VfxPos(string effect, Vector2 fallback = default)
	{
		Vector2 p = EnemyEffect(effect).TryGetValue("pos", out var v) ? v.AsVector2() : fallback;
		return new Vector2(p.X * Facing, p.Y);
	}

	protected PackedScene? VfxScene(string effect) =>
		EnemyEffect(effect).TryGetValue("scene", out var v) ? v.As<PackedScene>() : null;

	private void BuildPatrolTrail()
	{
		var scene = VfxScene("walk_trail");
		if (scene == null)
			return;
		var trail = scene.Instantiate();
		AddChild(trail);
		if (trail is Node2D n)
			n.Position = VfxPos("walk_trail");
		if (trail is CpuParticles2D || trail is GpuParticles2D)
			_patrolTrailEmitters.Add(trail);
		foreach (var e in trail.FindChildren("*", "CpuParticles2D", true, false))
			_patrolTrailEmitters.Add(e);
		foreach (var e in trail.FindChildren("*", "GpuParticles2D", true, false))
			_patrolTrailEmitters.Add(e);
		foreach (var em in _patrolTrailEmitters)
			em.Set("emitting", false);
	}

	protected Node2D? MakeVfx(string effect)
	{
		var scene = VfxScene(effect);
		if (scene == null)
			return null;
		var node = scene.Instantiate();
		if (node is Node2D n)
			n.Position = VfxPos(effect);
		return node as Node2D;
	}

	protected int LoopFrom(StringName anim) => Mathf.Max(LoopBound(anim, "loop_from"), 0);

	protected void ReplayFrom(StringName anim, int from)
	{
		if (Sprite.Animation != anim)
			Sprite.Play(anim);
		int last = Sprite.SpriteFrames.GetFrameCount(anim) - 1;
		Sprite.SetFrameAndProgress(Mathf.Clamp(from, 0, last), 0.0f);
		Sprite.Play();
	}

	private bool InCloseReach()
	{
		var t = Target();
		if (t == null)
			return false;
		Vector2 to = t.GlobalPosition - GlobalPosition;
		return Mathf.Abs(to.Y) <= attack_align_y && Mathf.Abs(to.X) <= close_range;
	}

	/// <summary>Spawn a self-contained attack SCENE (Strike or Projectile), mirror by facing, inject tuning, arm it.</summary>
	protected Node2D? SpawnAttack(PackedScene? scene, SegmentData tuning, bool toWorld = false, Vector2 at = default)
	{
		if (scene == null)
			return null;
		if (scene.Instantiate() is not Node2D node)
			return null;
		node.Set("hostile", !is_frenemy());
		node.Set("friendly_fire", friendly_fire);
		node.Set("source", this);
		node.Scale = new Vector2(Mathf.Abs(node.Scale.X) * Facing, node.Scale.Y);
		node.Position = at;
		if (toWorld)
			GetParent().AddChild(node);
		else
			AddChild(node);
		if (node is ITunable tn)
			tn.apply_tuning(tuning, this);
		foreach (var a in node.FindChildren("*", "Area2D", true, false))
			if (a is Hitbox hb)
			{
				hb.source = this;
				hb.activate();
			}
		if (!toWorld && node is BlastStrike bs)
			_activeChannel = bs;
		return node;
	}

	protected void CancelChannel()
	{
		if (IsInstanceValid(_activeChannel) && _activeChannel!.interrupt_on_hurt)
		{
			_activeChannel.cancel();
			_activeChannel = null;
			StopAttackSfx();
		}
	}

	protected void SpawnMeleeStrike(string vfxKey = "")
	{
		string key = vfxKey != "" ? vfxKey : (close_type != "" ? close_type : "aoe");
		var scene = VfxScene(key);
		if (scene != null)
		{
			var node = SpawnAttack(scene, new SegmentData
			{
				Damage = close_damage,
				Knockback = close_knockback,
				Stun = close_stun,
			}, false, VfxPos(key));
			if (conform_ground && node != null)
				GroundContour.Conform(node, GetWorld2D()?.DirectSpaceState);
			return;
		}
		// No authored close-attack SCENE (e.g. Kebus's point-blank jab -- a far-attack enemy with a close
		// anim but no close VFX): build a bare CODE hitbox from close_hitbox_x/extents so the swing still connects.
		// These exports were dead before, so a scene-less enemy's melee dealt no damage at all.
		SpawnCodeMeleeStrike();
	}

	/// <summary>A visual-less melee hitbox for an enemy with no authored melee scene — sized/placed from
	/// <see cref="close_hitbox_x"/> + <see cref="close_hitbox_extents"/> (half-size), armed with our melee
	/// tuning, and freed after <see cref="close_strike_lifetime"/>. Built like the contact hitbox.</summary>
	private void SpawnCodeMeleeStrike()
	{
		bool hostile = !is_frenemy();
		var hb = new Hitbox
		{
			CollisionLayer = Combat.HitLayer(hostile),
			CollisionMask = Combat.HurtMask(hostile, friendly_fire),
			damage = close_damage,
			knockback = close_knockback,
			stun = close_stun,
			source = this,
		};
		hb.AddChild(MakeBox(close_hitbox_extents * 2.0f, new Vector2(close_hitbox_x * Facing, -hurtbox_size.Y / 2.0f)));
		AddChild(hb);
		hb.activate();
		GetTree().CreateTimer(close_strike_lifetime).Timeout += hb.QueueFree;
	}

	private string MeleeVfxKey(int emittedFrame)
	{
		string @base = close_type != "" ? close_type : "aoe";
		int sheetFrame = emittedFrame + SheetStart(CloseAnim);
		string framed = $"{@base}_{sheetFrame}";
		return VfxScene(framed) != null ? framed : @base;
	}

	private float MeleeReach()
	{
		string key = close_type != "" ? close_type : "aoe";
		var hits = HitFramesOf(CloseAnim);
		if (hits.Count > 0)
			key = MeleeVfxKey(hits[0].AsInt32());
		var scene = VfxScene(key);
		if (scene == null)
			return 0.0f;
		var inst = scene.Instantiate();
		_isChannel = inst is BlastStrike;
		float far = 0.0f;
		foreach (var node in inst.FindChildren("*", "CollisionShape2D", true, false))
			if (node is CollisionShape2D cs && cs.Shape is RectangleShape2D rect)
				far = Mathf.Max(far, cs.Position.X + rect.Size.X * 0.5f);
		inst.Free();
		if (far <= 0.0f)
			return 0.0f;
		Vector2 pos = EnemyEffect(key).TryGetValue("pos", out var v) ? v.AsVector2() : Vector2.Zero;
		return far + pos.X;
	}

	private void FireProjectile()
	{
		if (far_mode == "lob")
		{
			FireLob();
			return;
		}
		Vector2 muzzle = GlobalPosition + VfxPos("projectile", DefaultMuzzle);
		var scene = VfxScene(far_type != "" ? far_type : "projectile");
		if (scene == null)
			return;
		if (scene.Instantiate() is not Projectile proj)
			return;
		proj.hostile = !is_frenemy();
		proj.friendly_fire = friendly_fire;
		proj.homing = 0.0f;
		proj.rotate_to_heading = false;
		proj.source = this;

		if (far_mode == "ground_wave")
		{
			// Baghel's floor surge: rolls forward horizontally and hugs the terrain surface as it goes.
			proj.velocity = new Vector2(projectile_speed * Facing, 0.0f);
			proj.max_range = far_travel;
			proj.ground_trail = true;
			proj.ground_follow = true;
		}
		else if (far_mode == "forward")
		{
			// A straight, non-tracking bolt: flies forward in the facing direction for far_travel px.
			proj.velocity = new Vector2(projectile_speed * Facing, 0.0f);
			proj.max_range = far_travel;
		}
		else
		{
			// Aimed (default): fire at the player's BODY and track their elevation, but cap the tilt at
			// ±far_aim_cap off horizontal so a player far above/below never makes the shot near-vertical.
			var aim = Target();
			Vector2 target = aim != null ? aim.GlobalPosition + new Vector2(0, -15) : muzzle + new Vector2(Facing, 0);
			Vector2 to = target - muzzle;
			if (far_aim_cap > 0.0f && to.LengthSquared() > 0.0001f)
			{
				float cap = Mathf.DegToRad(far_aim_cap);
				float ang = Mathf.Clamp(Mathf.Atan2(to.Y, Mathf.Abs(to.X)), -cap, cap); // tilt off horizontal
				float sign = Mathf.Abs(to.X) < 0.001f ? Facing : Mathf.Sign(to.X);
				to = new Vector2(sign * Mathf.Cos(ang), Mathf.Sin(ang));
			}
			proj.velocity = to.Normalized() * projectile_speed;
			proj.max_life = 3.0f;
			proj.can_fly_up = true;        // aim up/down at an elevated player instead of being flattened to the floor
			proj.rotate_to_heading = true; // point the bolt along its flight
		}

		GetParent().AddChild(proj);
		PlaceAt(proj, muzzle);
		proj.apply_tuning(new SegmentData
		{
			Damage = far_damage,
			Knockback = far_knockback,
			Stun = far_stun,
		}, this);
	}

	private void FireLob()
	{
		Vector2 muzzle = GlobalPosition + VfxPos("delayed_projectile", DefaultMuzzle);
		var lob = new LobProjectile
		{
			hostile = !is_frenemy(),
			friendly_fire = friendly_fire,
			source = this,
			arc_time = lob_arc_time,
			gravity = lob_gravity,
			dwell_time = lob_dwell,
			max_life = lob_max_life,
			explosion_extents = lob_explosion_extents,
			explosion_damage = far_damage,
			explosion_knockback = far_knockback,
			explosion_stun = far_stun,
			explosion_effect = VfxScene("delayed_projectile_burst"),
			explosion_sfx = $"{enemy_id}.delayed_projectile_burst",
		};
		lob.explosion_effect_pos = EnemyEffect("delayed_projectile_burst").TryGetValue("pos", out var v)
			? v.AsVector2() : Vector2.Zero;
		var vis = VfxScene("delayed_projectile");
		if (vis != null)
			lob.AddChild(vis.Instantiate());

		var aim = Target();
		Vector2 land = muzzle + new Vector2(Facing * 90.0f, 30.0f);
		if (aim != null)
		{
			float side = -Mathf.Sign(aim.GlobalPosition.X - GlobalPosition.X);
			land = aim.GlobalPosition + new Vector2(side * lob_land_offset, 0.0f);
		}
		lob.target = land;

		GetParent().AddChild(lob);
		PlaceAt(lob, muzzle);
	}

	private int FireFrame()
	{
		var hits = HitFramesOf(FarAnim);
		if (hits.Count > 0)
			return hits[0].AsInt32();
		return Mathf.Max(1, Sprite.SpriteFrames.GetFrameCount(FarAnim) / 2);
	}

	protected GArray HitFramesOf(StringName anim) =>
		AnimMeta.HitFrames(Sprite.SpriteFrames, anim);

	protected string CurrentAttackType() => State == EState.Far ? far_type : close_type;

	// --- damage / death -----------------------------------------------------

	protected virtual void OnHurt(Hit hit)
	{
		if (State == EState.Dead)
			return;
		last_hit_from_special = hit.from_special;
		float before = Health;
		Health = Mathf.Max(Health - hit.amount, 0.0f);
		EmitSignal(SignalName.damaged, before - Health, hit.source);
		Bar.SetRatio(Health / max_health);
		HitReact(Sprite, hit.amount);
		if (alert_duration > 0.0f)
		{
			_alertLeft = alert_duration;
			if (hit.source is Node2D src)
				Face(Mathf.Sign(src.GlobalPosition.X - GlobalPosition.X));
		}
		if (Health <= 0.0f)
		{
			Die();
			return;
		}
		if (hit.victim_vfx != null)
			SpawnVictimVfx(hit.victim_vfx, hit.victim_vfx_time, hurtbox_size.Y, true);
		if (hit.frenemy_time > 0.0f)
			become_frenemy(hit.frenemy_time);
		if (hit.dot_percent > 0.0f && hit.dot_time > 0.0f && !_reaped)
		{
			_reaped = true;
			_dotTick = max_health * hit.dot_percent;
			_dotLeft = hit.dot_time;
			_dotSource = hit.source;
		}
		float stagger = ApplyKnockback(hit, Facing);
		if (stagger > 0.0f)
		{
			StunLeft = Mathf.Max(StunLeft, stagger);
			SetState(EState.Stun);
			CancelChannel();
			if (hit.status_color.A > 0.0f)
				_status.ShowFor(hit.status_color, hit.status_time);
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
		EmitSignal(SignalName.died);
		RemoveFromGroup("enemies");
		Hurt.SetDeferred(Area2D.PropertyName.Monitorable, false);
		SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, 0);
		Bar.Visible = false;
		_statusIcons.SetActive(new System.Collections.Generic.List<StatusType>());
		_overhead.SetActive(new System.Collections.Generic.List<StatusType>());
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
		last_hit_from_special = false;
		EmitSignal(SignalName.damaged, dealt, (IsInstanceValid(_dotSource) ? _dotSource : null)!);
		Bar.SetRatio(Health / max_health);
		if (Health <= 0.0f)
		{
			_dotLeft = 0.0f;
			Die();
		}
	}

	// --- status pips --------------------------------------------------------

	private void RefreshStatusIcons()
	{
		var ids = new System.Collections.Generic.List<StatusType>();
		if (_dotLeft > 0.0f) ids.Add(StatusType.Reap);
		if (State == EState.Stun || StunLeft > 0.0f) ids.Add(StatusType.Stun);
		if (_frenemyLeft > 0.0f) ids.Add(StatusType.Charm);
		string key = string.Join(",", ids);
		if (key == _shownStatus)
			return;
		_shownStatus = key;
		_statusIcons.SetActive(ids);
		_overhead.SetActive(ids);
	}

	// --- helpers ------------------------------------------------------------

	protected Node2D? Player()
	{
		var p = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (p != null && p.HasMethod("is_dead") && p.Call("is_dead").AsBool())
			return null;
		return p;
	}

	public bool is_frenemy() => _frenemyLeft > 0.0f;

	protected Node2D? Target() => is_frenemy() ? NearestHostileEnemy() : Player();

	private Node2D? NearestHostileEnemy()
	{
		Node2D? best = null;
		float bestD = float.PositiveInfinity;
		foreach (var e in GetTree().GetNodesInGroup("enemies"))
		{
			if (e == this || e is not Node2D n)
				continue;
			if (e.HasMethod("is_frenemy") && e.Call("is_frenemy").AsBool())
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

	public void become_frenemy(float duration)
	{
		if (duration <= 0.0f || State == EState.Dead)
			return;
		_frenemyLeft = Mathf.Max(_frenemyLeft, duration);
		_contactHitbox?.SetDeferred(Area2D.PropertyName.Monitoring, false);
	}

	private void EndFrenemy() => _frenemyLeft = 0.0f;

	public void magnetize(Node2D anchor, float arriveDist, float speed, float stunTime)
	{
		if (State == EState.Dead || anchor == null)
			return;
		_magnetAnchor = anchor;
		_magnetArrive = arriveDist;
		_magnetSpeed = speed;
		_magnetStun = stunTime;
	}

	public void apply_hit(Hit hit) => Hurt?.take_hit(hit);

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
				Play(HasWalk ? "walk" : "idle");
				break;
		}
	}

	protected void Play(StringName anim)
	{
		if (Sprite.Animation != anim || !Sprite.IsPlaying())
			Sprite.Play(anim);
	}

	// --- bridges (GDScript configs / UI / autoload / util) ------------------

	private GDict EnemyEffect(string effect) => Emitters.EnemyEffect(enemy_id, effect);

	private int SheetStart(StringName anim) =>
		AnimMeta.SheetStart(Sprite.SpriteFrames, anim);

	private int LoopBound(StringName anim, string key) =>
		AnimMeta.LoopBound(Sprite.SpriteFrames, anim, key);

	protected void SfxPlayAt(string cue, Vector2 pos) =>
		GetNodeOrNull<Sfx>("/root/Sfx")?.play_at(cue, pos);

	private AudioStreamPlayer2D? SfxMakeOneshot2d(string cue) =>
		GetNodeOrNull<Sfx>("/root/Sfx")?.make_oneshot_2d(cue);

	protected static CollisionShape2D MakeBox(Vector2 size, Vector2 offset = default) =>
		new() { Position = offset, Shape = new RectangleShape2D { Size = size } };

	protected static void PlaceAt(Node2D node, Vector2 pos)
	{
		node.GlobalPosition = pos;
		node.ResetPhysicsInterpolation();
	}
}
