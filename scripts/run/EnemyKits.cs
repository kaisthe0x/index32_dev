using Godot;

namespace MyGame;

/// <summary>
/// The enemy roster — one <see cref="EnemyKit"/> per enemy TYPE. <see cref="EnemySpawner.SpawnPool"/> draws from these, and
/// <see cref="PressureSpawns"/> (the kamikaze, the Ventilator) names its two directly. Each kit's <c>Tune</c> sets only what differs
/// from the defaults declared on <see cref="Enemy"/>. <c>CloseType</c> / <c>FarType</c> (the close-range and
/// far-range attack) use the <see cref="StrikeType"/> taxonomy's keys, which also name the attack's animation, effect
/// and sound.
/// </summary>
public static class EnemyKits
{
	public static readonly EnemyKit Kebus = new(EnemyIds.Kebus, "Kebus", EnemyTier.Strong, EnemyMovement.Ground, e =>
	{
		e.Close = new MeleeAttack();   // a point-blank jab — no effect scene, a bare hitbox
		// Aimed (the default path): tracks the player and aims at his body, tilt capped to ±45° (never vertical).
		e.Far = new ShotAttack { Speed = 200.0f, AimCap = 45.0f };
		e.AttackAlignY = 120.0f;   // wide, so he'll engage you a level up or down
		e.FigChance = 0.25f; // the hardest grunt — better fig odds than the 10 % default
	});

	public static readonly EnemyKit Baghel = new(EnemyIds.Baghel, "Baghel", EnemyTier.Chip, EnemyMovement.Ground, e =>
	{
		e.Far = new ShotAttack { Path = ShotPath.GroundWave, Range = 130.0f, Travel = 100.0f, Speed = 200.0f, Damage = 7.0f };
		e.IdleTimeMin = 5.0f;
		e.IdleTimeMax = 7.0f;
	});

	public static readonly EnemyKit Mazab = new(EnemyIds.Mazab, "Mazab", EnemyTier.Mid, EnemyMovement.Ground, e =>
	{
		e.Far = new LobAttack
		{
			Range = 260.0f, Damage = 16.0f, Knockback = 160.0f, Stun = 0.25f,
			ArcTime = 0.9f, Dwell = 1.0f, ExplosionExtents = new Vector2(48, 26),
		};
		e.AttackAlignY = 120.0f;
		e.AttackCooldown = 2.2f;
	});

	// The stationary sleeper. Optional: it needn't be killed to clear a round.
	public static readonly EnemyKit Nasen = EnemyKit.Of<SleeperEnemy>(EnemyIds.Nasen, "Nasen", EnemyTier.Strong,
		EnemyMovement.Stationary, "res://scenes/sleeper_enemy.tscn", e =>
	{
		e.MaxHealth = 90.0f;
		e.Optional = true;
		// The rage AoE hits OTHER enemies too — it still only TRIGGERS on player detection (SleeperEnemy.RageZone).
		e.FriendlyFire = true;
	}) with { SpawnCap = 1 };

	// The stand-still KAMIKAZE (<see cref="PressureSpawns"/> — not in the round's spawn pool): optional (not part of the
	// round), drops nothing (no farming by standing still), and notices from far enough to dive at once.
	public static readonly EnemyKit Ein = EnemyKit.Of<DiverEnemy>(EnemyIds.Ein, "Ein", EnemyTier.Mid,
		EnemyMovement.Flying, "res://scenes/diver_enemy.tscn", e =>
	{
		e.MaxHealth = 28.0f;
		e.Optional = true;
		e.FigChance = 0.0f;
		e.DetectRange = 320.0f;
		e.BodySize = new Vector2(22, 22);
		e.HurtboxSize = new Vector2(26, 26);
		e.MoveSpeed = 34.0f;
		e.PatrolDistance = 70.0f;
	}) with { LiraDrop = 0 };

	// The EDGE enemy (<see cref="PressureSpawns"/> — not in the round's spawn pool): appears on the inland side when the
	// player is near either end of the arena, and blasts WIND (CloseGust) that does no damage but flings him outward
	// — off the edge unless he air-jumps / dashes back. Optional (not part of the round) but drops Lira + figs as usual.
	// Like Tarri, the blast fires on the LAST attack frame and he holds + vibrates there (the blast's EmitDuration).
	public static readonly EnemyKit Ventilator = new(EnemyIds.Ventilator, "Ventilator", EnemyTier.Mid, EnemyMovement.Ground, e =>
	{
		// A held gust: no damage — it flings the player (tuned so an air jump within ~0.3 s or a dash recovers).
		e.Close = new MeleeAttack(StrikeType.Blast) { Range = 150.0f, Damage = 0.0f, Knockback = 0.0f, Gust = 540.0f };
		e.Optional = true;
		e.MaxHealth = 60.0f;
		e.BodySize = new Vector2(18, 36);
		e.HurtboxSize = new Vector2(22, 42);
		e.MoveSpeed = 40.0f;
		e.PatrolDistance = 80.0f;
		e.AttackAlignY = 52.0f;
		e.AttackCooldown = 2.4f;
		e.AttackHitstop = 2.0f;
		e.AttackShake = 1.5f;
	});

	public static readonly EnemyKit Matat = new(EnemyIds.Matat, "Matat", EnemyTier.Strong, EnemyMovement.Ground, e =>
	{
		e.Close = new MeleeAttack(StrikeType.Aoe)
		{
			Range = 52.0f, Damage = 11.0f, Knockback = 150.0f, Stun = 0.25f, Loops = true, ConformGround = true,
		};
		e.MaxHealth = 95.0f;
		e.BodySize = new Vector2(20, 34);
		e.HurtboxSize = new Vector2(24, 40);
		e.MoveSpeed = 40.0f;
		e.PatrolDistance = 90.0f;
		e.AttackAlignY = 44.0f;
		e.AttackCooldown = 1.2f;
		e.AttackHitstop = 0.0f;
	});

	public static readonly EnemyKit Tarri = new(EnemyIds.Tarri, "Tarri", EnemyTier.Mid, EnemyMovement.Ground, e =>
	{
		e.Close = new MeleeAttack(StrikeType.Blast) { Range = 140.0f, Damage = 16.0f, Knockback = 120.0f, Stun = 0.3f };
		e.MaxHealth = 70.0f;
		e.BodySize = new Vector2(18, 24);
		e.HurtboxSize = new Vector2(22, 28);
		e.MoveSpeed = 34.0f;
		e.PatrolDistance = 100.0f;
		e.AttackAlignY = 52.0f;
		e.AttackCooldown = 2.6f;
		e.AttackHitstop = 2.0f;
		e.AttackShake = 1.5f;
	});

	public static readonly EnemyKit Breski = new(EnemyIds.Breski, "Breski", EnemyTier.Strong, EnemyMovement.Ground, e =>
	{
		e.Close = new MeleeAttack { Range = 56.0f, Damage = 10.0f, Knockback = 130.0f, Stun = 0.2f };   // a two-hit combo
		e.MaxHealth = 110.0f;
		e.BodySize = new Vector2(18, 28);
		e.HurtboxSize = new Vector2(22, 34);
		e.MoveSpeed = 46.0f;
		e.PatrolDistance = 90.0f;
		e.AttackAlignY = 44.0f;
		e.AttackCooldown = 1.8f;
		e.AttackHitstop = 0.12f;
		e.AttackShake = 1.0f;
	});

	// --- Wardens (elite tier: WardenEnemy — teleporting lunger, cinematic spawn, persistent corpse) ---
	public static readonly EnemyKit Kroj = EnemyKit.Of<WardenEnemy>(EnemyIds.Kroj, "Kroj", EnemyTier.Strong,
		EnemyMovement.Ground, "res://scenes/warden.tscn", e =>
	{
		// A LUNGE: he closes and body-checks; Lunge is the forward impulse on the hit frame.
		e.Close = new MeleeAttack(StrikeType.Lunge)
		{
			Range = 130.0f, Damage = 22.0f, Knockback = 190.0f, Stun = 0.3f, Lunge = 460.0f,
		};
		e.MaxHealth = 300.0f;
		e.BodySize = new Vector2(28, 44);
		e.HurtboxSize = new Vector2(34, 52);
		e.MoveSpeed = 55.0f;
		e.Aggro = true;
		e.AggroRange = 640.0f;
		e.AttackCooldown = 2.0f;
		e.AttackAlignY = 54.0f;
		e.AttackHitstop = 0.0f;
		// Teleport pursuit — warp in when the player stays far, landing outside lunge range.
		e.TeleportRange = 360.0f;
		e.TeleportDelay = 1.6f;
		e.TeleportLandOffset = 96.0f;
	}) with { LiraDrop = 12 };
}
