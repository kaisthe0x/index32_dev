using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Khalid's ACTION catalog — PURE DATA (the <see cref="Actions"/> accessor injects Id/Category into these rows).
/// One table per category; each row is a typed <see cref="Action"/>. Presentation lives elsewhere, keyed by
/// <see cref="Action.Animation"/>. Rows omit Id/Category (the accessor fills them from the pool key + kind).
/// </summary>
public static class ActionsKhalid
{
	private const string Ember = "res://vfx/shared/textures/pixel_ember.png";
	private const string Blast1 = "res://vfx/shared/textures/blast1.png";
	private const string Bolt = "res://vfx/shared/impervious/bolt.png";
	private const string SoftDot = "res://vfx/shared/textures/soft_dot.png";
	private const string Shield = "res://vfx/shared/impervious/shield.png";

	public static readonly Dictionary<string, Action> ATTACKS = new()
	{
		[AttackIds.OraOra] = new Action
		{
			Name = "Ora Ora",
			Icon = Ember,
			Style = ActionStyle.Flurry,
			Hit = new HitData(StrikeType.Melee, new SegmentData { Damage = 15, Knockback = 0, Stun = 0.1f, Extents = new Vector2(32, 22) }),
		},
		[AttackIds.Spear] = new Action
		{
			Name = "Spear",
			Icon = Blast1,
			Hit = new HitData(StrikeType.Melee,
				new SegmentData { Damage = 10, Knockback = 40 },
				new SegmentData { Damage = 20, Knockback = 60 },
				new SegmentData { Damage = 35, Knockback = 140 }),
		},
		[AttackIds.Bakshen] = new Action
		{
			Name = "Bakshen",
			Icon = Bolt,
			Style = ActionStyle.Cooldown,
			Cooldown = 3.0f,
			Hit = new HitData(StrikeType.Melee, new SegmentData { Damage = 65, Knockback = 0, Stun = 0.0f }),
		},
		[AttackIds.Zahluq] = new Action
		{
			Name = "Zahluq",
			Icon = Bolt,
			Style = ActionStyle.Cooldown,
			Cooldown = 3.0f,
			Tags = ["air"],
			Hit = new HitData(StrikeType.Melee, new SegmentData
			{
				Damage = 45,
				Knockback = 90,
				Stun = 0.2f,
				Lunge = 1100.0f,
				Hold = 0.4f,
				SuperArmor = 0.4f,
				Extents = new Vector2(40, 28),
			}),
		},
		[AttackIds.CherryShots] = new Action
		{
			Name = "Cherry Shots",
			Icon = SoftDot,
			Hit = new HitData(StrikeType.Projectile,
				new SegmentData { Damage = 4, Knockback = 0 },
				new SegmentData { Damage = 7, Knockback = 0 }),
		},
		[AttackIds.TwinReaper] = new Action
		{
			Name = "Twin Reaper",
			Icon = Blast1,
			Style = ActionStyle.Flurry,
			Tags = ["reaper"],
			Hit = new HitData(StrikeType.Melee, new SegmentData { Damage = 12, Knockback = 0, Reap = 0.12f, ReapTime = 5.0f }),
		},
		[AttackIds.DualExecutioner] = new Action
		{
			Name = "Dual Executioner",
			Icon = Blast1,
			Style = ActionStyle.Flurry,
			Tags = ["reaper"],
			Hit = new HitData(StrikeType.Melee, new SegmentData { Damage = 22, Knockback = 0, Stun = 0.3f }),
		},
	};

	public static readonly Dictionary<string, Action> SPECIALS = new()
	{
		[SpecialIds.GroundBreaker] = new Action
		{
			Name = "Ground Breaker",
			Icon = Blast1,
			Hit = new HitData(StrikeType.Aoe, new SegmentData
			{
				Damage = 40,
				Knockback = 160,
				Stun = 1.0f,
				VictimEffect = "res://vfx/character/khalid/status/ground_breaker_stun.tscn",
			}),
		},
		[SpecialIds.Frenemy] = new Action
		{
			Name = "Frenemy",
			Icon = Ember,
			Tags = ["charm"],
			Hit = new HitData(StrikeType.Blast, new SegmentData
			{
				Damage = 4,
				Knockback = 0,
				Frenemy = 8.0f,
				VictimEffect = "res://vfx/character/khalid/status/frenemy_stun.tscn",
				VictimTime = 8.0f,
			}),
		},
		[SpecialIds.ComeCloser] = new Action { Name = "Come Closer", Icon = Ember, Tags = ["control"], Cooldown = 1.0f },
		[SpecialIds.RedereShield] = new Action { Name = "Redere Shield", Icon = Shield, Tags = ["shield", "held"] },
		[SpecialIds.RedereFrisbee] = new Action
		{
			Name = "Redere Frisbee",
			Icon = Blast1,
			Tags = ["shield"],
			Hit = new HitData(StrikeType.Projectile, new SegmentData { Damage = 15, Knockback = 120 }),
		},
	};

	public static readonly Dictionary<string, Action> SURGES = new()
	{
		[SurgeIds.Aegis] = new Action
		{
			Name = "Aegis",
			Icon = Shield,
			Surge = new SurgeSpec { duration = 5.0f, invuln = true, cost = 100.0f, aura = "res://vfx/character/khalid/surge/aegis/surge_aegis.tscn" },
		},
		[SurgeIds.Jnoon] = new Action
		{
			Name = "Jnoon",
			Icon = Shield,
			Surge = new SurgeSpec { duration = 5.0f, damage_mult = 2.0f, damage_taken_mult = 0.5f, cost = 100.0f, aura = "res://vfx/character/khalid/surge/jnoon/surge_jnoon.tscn" },
		},
		[SurgeIds.Asra] = new Action
		{
			Name = "Asra",
			Icon = Shield,
			Surge = new SurgeSpec { duration = 5.0f, speed_mult = 2.0f, cost = 100.0f, aura = "res://vfx/character/khalid/surge/asra/surge_asra.tscn" },
		},
		[SurgeIds.Nem] = new Action
		{
			Name = "Nem",
			Icon = Shield,
			Surge = new SurgeSpec { duration = 5.0f, channel = true, heal_frac = 0.5f, cost = 200.0f, aura = "res://vfx/character/khalid/surge/nem/surge_nem.tscn" },
		},
		[SurgeIds.Wara] = new Action
		{
			Name = "Wara",
			Icon = Shield,
			Surge = new SurgeSpec
			{
				trigger = "hit",
				stun_radius = 150.0f,
				stun_time = 2.0f,
				cost = 100.0f,
				aura = "res://vfx/character/khalid/surge/wara/surge_wara.tscn",
				burst = "res://vfx/character/khalid/surge/wara/surge_wara_burst.tscn",
			},
		},
	};

	public static readonly Dictionary<string, Dictionary<string, Action>> MOVEMENTS = new()
	{
		[MovementIds.Run] = new() { [MovementIds.StandardStride] = new Action { Name = "Standard Stride", Icon = Ember, Move = new Locomotion { run_speed = 230.0f } } },
		[MovementIds.Jump] = new() { [MovementIds.StandardLeap] = new Action { Name = "Standard Leap", Icon = SoftDot, Move = new Locomotion { air_jumps = 1 } } },
		[MovementIds.Dash] = new() { [MovementIds.BlinkDash] = new Action { Name = "Blink Dash", Icon = Bolt, Move = new Locomotion { blink = true } } },
		[MovementIds.Slam] = new() { [MovementIds.StandardSlam] = new Action { Name = "Standard Slam", Icon = Blast1, Move = new Locomotion() } },
	};

	public const string DEFAULT_ATTACK = AttackIds.Bakshen;
	public const string DEFAULT_SPECIAL = SpecialIds.GroundBreaker;
	public const string DEFAULT_SURGE = SurgeIds.Nem;
	public static readonly Dictionary<string, string> DEFAULT_MOVEMENTS = new()
	{
		[MovementIds.Run] = MovementIds.StandardStride,
		[MovementIds.Jump] = MovementIds.StandardLeap,
		[MovementIds.Dash] = MovementIds.BlinkDash,
		[MovementIds.Slam] = MovementIds.StandardSlam,
	};
}
