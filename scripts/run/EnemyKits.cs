using Godot;

namespace MyGame;

/// <summary>
/// The enemy roster — one <see cref="EnemyKit"/> per enemy TYPE. RunManager's spawn pool draws from these, and its
/// pressure spawns (the kamikaze, the Ventilator) name them directly. Each kit's <c>Tune</c> sets only what differs
/// from the defaults declared on <see cref="Enemy"/>. <c>CloseType</c> / <c>FarType</c> (the close-range and
/// far-range attack) use the <see cref="StrikeType"/> taxonomy's keys, which also name the attack's animation, effect
/// and sound.
/// </summary>
public static class EnemyKits
{
	public static readonly EnemyKit KEBUS = new(EnemyIds.Kebus, "Kebus", EnemyTier.Strong, EnemyMovement.Ground, e =>
	{
		e.CloseType = StrikeType.Melee.Key();
		e.FarType = StrikeType.Projectile.Key();
		// FarMode stays Aimed: tracks the player + aims at the body, tilt capped to ±45° (never vertical);
		// AttackAlignY is wide so he'll engage you a level up/down.
		e.FarAimCap = 45.0f;
		e.AttackAlignY = 120.0f;
		e.FarHitboxExtents = new Vector2(7, 10);
		e.ProjectileSpeed = 200.0f;
		e.FigChance = 0.25f; // the hardest grunt — better fig odds than the 10 % default
	});

	public static readonly EnemyKit BAGHEL = new(EnemyIds.Baghel, "Baghel", EnemyTier.Chip, EnemyMovement.Ground, e =>
	{
		e.FarType = StrikeType.Projectile.Key();
		e.FarMode = FarMode.GroundWave;
		e.FarRange = 130.0f;
		e.FarTravel = 100.0f;
		e.ProjectileSpeed = 200.0f;
		e.FarHitboxExtents = new Vector2(4, 15);
		e.FarHitboxOffset = new Vector2(0, -9);
		e.FarDamage = 7.0f;
		e.IdleTimeMin = 5.0f;
		e.IdleTimeMax = 7.0f;
	});

	public static readonly EnemyKit MAZAB = new(EnemyIds.Mazab, "Mazab", EnemyTier.Mid, EnemyMovement.Ground, e =>
	{
		e.FarType = StrikeType.DelayedProjectile.Key();
		e.FarMode = FarMode.Lob;
		e.FarRange = 260.0f;
		e.AttackAlignY = 120.0f;
		e.AttackCooldown = 2.2f;
		e.FarDamage = 16.0f;
		e.FarKnockback = 160.0f;
		e.FarStun = 0.25f;
		e.LobArcTime = 0.9f;
		e.LobDwell = 1.0f;
		e.LobExplosionExtents = new Vector2(48, 26);
	});

	// The stationary sleeper. Optional: it needn't be killed to clear a round.
	public static readonly EnemyKit NASEN = EnemyKit.Of<SleeperEnemy>(EnemyIds.Nasen, "Nasen", EnemyTier.Strong,
		EnemyMovement.Stationary, "res://scenes/sleeper_enemy.tscn", e =>
	{
		e.MaxHealth = 90.0f;
		e.Optional = true;
		e.CloseType = StrikeType.Aoe.Key();
		e.ConformGround = true;
		// The rage AoE hits OTHER enemies too — it still only TRIGGERS on player detection (SleeperEnemy.RageZone).
		e.FriendlyFire = true;
	}) with { SpawnCap = 1 };

	// The stand-still KAMIKAZE (RunManager's pressure spawn — not in the round's spawn pool): optional (not part of the
	// round), drops nothing (no farming by standing still), and notices from far enough to dive at once.
	public static readonly EnemyKit EIN = EnemyKit.Of<DiverEnemy>(EnemyIds.Ein, "Ein", EnemyTier.Mid,
		EnemyMovement.Flying, "res://scenes/diver_enemy.tscn", e =>
	{
		e.MaxHealth = 28.0f;
		e.CloseType = StrikeType.Kamikaze.Key();
		e.Optional = true;
		e.FigChance = 0.0f;
		e.DetectRange = 320.0f;
		e.BodySize = new Vector2(22, 22);
		e.HurtboxSize = new Vector2(26, 26);
		e.MoveSpeed = 34.0f;
		e.PatrolDistance = 70.0f;
	}) with { LiraDrop = 0 };

	// The EDGE enemy (RunManager's edge spawn — not in the round's spawn pool): appears on the inland side when the
	// player is near either end of the arena, and blasts WIND (CloseGust) that does no damage but flings him outward
	// — off the edge unless he air-jumps / dashes back. Optional (not part of the round) but drops Lira + figs as usual.
	// Like Tarri, the blast fires on the LAST attack frame and he holds + vibrates there (the blast's EmitDuration).
	public static readonly EnemyKit VENTILATOR = new(EnemyIds.Ventilator, "Ventilator", EnemyTier.Mid, EnemyMovement.Ground, e =>
	{
		e.CloseType = StrikeType.Blast.Key();
		e.Optional = true;
		e.MaxHealth = 60.0f;
		e.BodySize = new Vector2(18, 36);
		e.HurtboxSize = new Vector2(22, 42);
		e.MoveSpeed = 40.0f;
		e.PatrolDistance = 80.0f;
		e.CloseRange = 150.0f;
		e.AttackAlignY = 52.0f;
		e.AttackCooldown = 2.4f;
		e.CloseDamage = 0.0f;
		e.CloseKnockback = 0.0f;
		e.CloseStun = 0.0f;
		e.CloseGust = 540.0f;
		e.AttackHitstop = 2.0f;
		e.AttackShake = 1.5f;
	});

	public static readonly EnemyKit MATAT = new(EnemyIds.Matat, "Matat", EnemyTier.Strong, EnemyMovement.Ground, e =>
	{
		e.CloseType = StrikeType.Aoe.Key();
		e.ConformGround = true;
		e.MaxHealth = 95.0f;
		e.BodySize = new Vector2(20, 34);
		e.HurtboxSize = new Vector2(24, 40);
		e.MoveSpeed = 40.0f;
		e.PatrolDistance = 90.0f;
		e.FarRange = 300.0f;
		e.CloseRange = 52.0f;
		e.AttackAlignY = 44.0f;
		e.AttackCooldown = 1.2f;
		e.AttackLoops = true;
		e.AttackHitstop = 0.0f;
		e.CloseDamage = 11.0f;
		e.CloseKnockback = 150.0f;
		e.CloseStun = 0.25f;
		e.CloseHitboxX = 0.0f;
		e.CloseHitboxExtents = new Vector2(46, 30);
		e.CloseStrikeLifetime = 0.35f;
	});

	public static readonly EnemyKit TARRI = new(EnemyIds.Tarri, "Tarri", EnemyTier.Mid, EnemyMovement.Ground, e =>
	{
		e.CloseType = StrikeType.Blast.Key();
		e.MaxHealth = 70.0f;
		e.BodySize = new Vector2(18, 24);
		e.HurtboxSize = new Vector2(22, 28);
		e.MoveSpeed = 34.0f;
		e.PatrolDistance = 100.0f;
		e.CloseRange = 140.0f;
		e.AttackAlignY = 52.0f;
		e.AttackCooldown = 2.6f;
		e.CloseHitboxX = 70.0f;
		e.CloseHitboxExtents = new Vector2(70, 22);
		e.CloseStrikeLifetime = 2.0f;
		e.CloseDamage = 16.0f;
		e.CloseKnockback = 120.0f;
		e.CloseStun = 0.3f;
		e.AttackHitstop = 2.0f;
		e.AttackShake = 1.5f;
	});

	public static readonly EnemyKit BRESKI = new(EnemyIds.Breski, "Breski", EnemyTier.Strong, EnemyMovement.Ground, e =>
	{
		e.CloseType = StrikeType.Melee.Key();
		e.MaxHealth = 110.0f;
		e.BodySize = new Vector2(18, 28);
		e.HurtboxSize = new Vector2(22, 34);
		e.MoveSpeed = 46.0f;
		e.PatrolDistance = 90.0f;
		e.CloseRange = 56.0f;
		e.AttackAlignY = 44.0f;
		e.AttackCooldown = 1.8f;
		e.CloseDamage = 10.0f;
		e.CloseKnockback = 130.0f;
		e.CloseStun = 0.2f;
		e.AttackHitstop = 0.12f;
		e.AttackShake = 1.0f;
	});

	// --- Wardens (elite tier: WardenEnemy — teleporting lunger, cinematic spawn, persistent corpse) ---
	public static readonly EnemyKit KROJ = EnemyKit.Of<WardenEnemy>(EnemyIds.Kroj, "Kroj", EnemyTier.Strong,
		EnemyMovement.Ground, "res://scenes/warden.tscn", e =>
	{
		e.MaxHealth = 300.0f;
		e.BodySize = new Vector2(28, 44);
		e.HurtboxSize = new Vector2(34, 52);
		e.MoveSpeed = 55.0f;
		e.Aggro = true;
		e.AggroRange = 640.0f;
		// Attack = a LUNGE: he closes and body-checks; CloseLunge is the forward impulse.
		e.CloseType = StrikeType.Lunge.Key();
		e.CloseRange = 130.0f;
		e.CloseLunge = 460.0f;
		e.CloseDamage = 22.0f;
		e.CloseKnockback = 190.0f;
		e.CloseStun = 0.3f;
		e.CloseHitboxX = 30.0f;
		e.CloseHitboxExtents = new Vector2(40, 40);
		e.CloseStrikeLifetime = 0.3f;
		e.AttackCooldown = 2.0f;
		e.AttackAlignY = 54.0f;
		e.AttackHitstop = 0.0f;
		// Teleport pursuit — warp in when the player stays far, landing outside lunge range.
		e.TeleportRange = 360.0f;
		e.TeleportDelay = 1.6f;
		e.TeleportLandOffset = 96.0f;
	}) with { LiraDrop = 12 };
}
