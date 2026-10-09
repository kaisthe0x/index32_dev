using Godot;

namespace MyGame;

/// <summary>
/// The enemy roster — one <see cref="EnemyKit"/> per enemy TYPE. RunManager's spawn pool draws from these, and its
/// pressure spawns (the kamikaze, the Ventilator) name them directly. Each kit's <c>Tune</c> sets only what differs
/// from the defaults declared on <see cref="Enemy"/>. <c>close_type</c> / <c>far_type</c> (the close-range and
/// far-range attack) use the <see cref="StrikeType"/> taxonomy's keys, which also name the attack's animation, effect
/// and sound.
/// </summary>
public static class EnemyKits
{
	public static readonly EnemyKit KEBUS = new(EnemyIds.Kebus, "Kebus", EnemyTier.Strong, EnemyMovement.Ground, e =>
	{
		e.close_type = StrikeType.Melee.Key();
		e.far_type = StrikeType.Projectile.Key();
		// far_mode stays Aimed: tracks the player + aims at the body, tilt capped to ±45° (never vertical);
		// attack_align_y is wide so he'll engage you a level up/down.
		e.far_aim_cap = 45.0f;
		e.attack_align_y = 120.0f;
		e.far_hitbox_extents = new Vector2(7, 10);
		e.projectile_speed = 200.0f;
		e.fig_chance = 0.25f; // the hardest grunt — better fig odds than the 10 % default
	});

	public static readonly EnemyKit BAGHEL = new(EnemyIds.Baghel, "Baghel", EnemyTier.Chip, EnemyMovement.Ground, e =>
	{
		e.far_type = StrikeType.Projectile.Key();
		e.far_mode = FarMode.GroundWave;
		e.far_range = 130.0f;
		e.far_travel = 100.0f;
		e.projectile_speed = 200.0f;
		e.far_hitbox_extents = new Vector2(4, 15);
		e.far_hitbox_offset = new Vector2(0, -9);
		e.far_damage = 7.0f;
		e.idle_time_min = 5.0f;
		e.idle_time_max = 7.0f;
	});

	public static readonly EnemyKit MAZAB = new(EnemyIds.Mazab, "Mazab", EnemyTier.Mid, EnemyMovement.Ground, e =>
	{
		e.far_type = StrikeType.DelayedProjectile.Key();
		e.far_mode = FarMode.Lob;
		e.far_range = 260.0f;
		e.attack_align_y = 120.0f;
		e.attack_cooldown = 2.2f;
		e.far_damage = 16.0f;
		e.far_knockback = 160.0f;
		e.far_stun = 0.25f;
		e.lob_arc_time = 0.9f;
		e.lob_dwell = 1.0f;
		e.lob_explosion_extents = new Vector2(48, 26);
	});

	// The stationary sleeper. Optional: it needn't be killed to clear a round.
	public static readonly EnemyKit NASEN = EnemyKit.Of<SleeperEnemy>(EnemyIds.Nasen, "Nasen", EnemyTier.Strong,
		EnemyMovement.Stationary, "res://scenes/sleeper_enemy.tscn", e =>
	{
		e.max_health = 90.0f;
		e.optional = true;
		e.close_type = StrikeType.Aoe.Key();
		e.conform_ground = true;
		// The rage AoE hits OTHER enemies too — it still only TRIGGERS on player detection (SleeperEnemy.rage_zone).
		e.friendly_fire = true;
	}) with { SpawnCap = 1 };

	// The stand-still KAMIKAZE (RunManager's pressure spawn — not in the round's spawn pool): optional (not part of the
	// round), drops nothing (no farming by standing still), and notices from far enough to dive at once.
	public static readonly EnemyKit EIN = EnemyKit.Of<DiverEnemy>(EnemyIds.Ein, "Ein", EnemyTier.Mid,
		EnemyMovement.Flying, "res://scenes/diver_enemy.tscn", e =>
	{
		e.max_health = 28.0f;
		e.close_type = StrikeType.Kamikaze.Key();
		e.optional = true;
		e.fig_chance = 0.0f;
		e.detect_range = 320.0f;
		e.body_size = new Vector2(22, 22);
		e.hurtbox_size = new Vector2(26, 26);
		e.move_speed = 34.0f;
		e.patrol_distance = 70.0f;
	}) with { LiraDrop = 0 };

	// The EDGE enemy (RunManager's edge spawn — not in the round's spawn pool): appears on the inland side when the
	// player is near either end of the arena, and blasts WIND (close_gust) that does no damage but flings him outward
	// — off the edge unless he air-jumps / dashes back. Optional (not part of the round) but drops Lira + figs as usual.
	// Like Tarri, the blast fires on the LAST attack frame and he holds + vibrates there (the blast's emit_duration).
	public static readonly EnemyKit VENTILATOR = new(EnemyIds.Ventilator, "Ventilator", EnemyTier.Mid, EnemyMovement.Ground, e =>
	{
		e.close_type = StrikeType.Blast.Key();
		e.optional = true;
		e.max_health = 60.0f;
		e.body_size = new Vector2(18, 36);
		e.hurtbox_size = new Vector2(22, 42);
		e.move_speed = 40.0f;
		e.patrol_distance = 80.0f;
		e.close_range = 150.0f;
		e.attack_align_y = 52.0f;
		e.attack_cooldown = 2.4f;
		e.close_damage = 0.0f;
		e.close_knockback = 0.0f;
		e.close_stun = 0.0f;
		e.close_gust = 540.0f;
		e.attack_hitstop = 2.0f;
		e.attack_shake = 1.5f;
	});

	public static readonly EnemyKit MATAT = new(EnemyIds.Matat, "Matat", EnemyTier.Strong, EnemyMovement.Ground, e =>
	{
		e.close_type = StrikeType.Aoe.Key();
		e.conform_ground = true;
		e.max_health = 95.0f;
		e.body_size = new Vector2(20, 34);
		e.hurtbox_size = new Vector2(24, 40);
		e.move_speed = 40.0f;
		e.patrol_distance = 90.0f;
		e.far_range = 300.0f;
		e.close_range = 52.0f;
		e.attack_align_y = 44.0f;
		e.attack_cooldown = 1.2f;
		e.attack_loops = true;
		e.attack_hitstop = 0.0f;
		e.close_damage = 11.0f;
		e.close_knockback = 150.0f;
		e.close_stun = 0.25f;
		e.close_hitbox_x = 0.0f;
		e.close_hitbox_extents = new Vector2(46, 30);
		e.close_strike_lifetime = 0.35f;
	});

	public static readonly EnemyKit TARRI = new(EnemyIds.Tarri, "Tarri", EnemyTier.Mid, EnemyMovement.Ground, e =>
	{
		e.close_type = StrikeType.Blast.Key();
		e.max_health = 70.0f;
		e.body_size = new Vector2(18, 24);
		e.hurtbox_size = new Vector2(22, 28);
		e.move_speed = 34.0f;
		e.patrol_distance = 100.0f;
		e.close_range = 140.0f;
		e.attack_align_y = 52.0f;
		e.attack_cooldown = 2.6f;
		e.close_hitbox_x = 70.0f;
		e.close_hitbox_extents = new Vector2(70, 22);
		e.close_strike_lifetime = 2.0f;
		e.close_damage = 16.0f;
		e.close_knockback = 120.0f;
		e.close_stun = 0.3f;
		e.attack_hitstop = 2.0f;
		e.attack_shake = 1.5f;
	});

	public static readonly EnemyKit BRESKI = new(EnemyIds.Breski, "Breski", EnemyTier.Strong, EnemyMovement.Ground, e =>
	{
		e.close_type = StrikeType.Melee.Key();
		e.max_health = 110.0f;
		e.body_size = new Vector2(18, 28);
		e.hurtbox_size = new Vector2(22, 34);
		e.move_speed = 46.0f;
		e.patrol_distance = 90.0f;
		e.close_range = 56.0f;
		e.attack_align_y = 44.0f;
		e.attack_cooldown = 1.8f;
		e.close_damage = 10.0f;
		e.close_knockback = 130.0f;
		e.close_stun = 0.2f;
		e.attack_hitstop = 0.12f;
		e.attack_shake = 1.0f;
	});

	// --- Wardens (elite tier: WardenEnemy — teleporting lunger, cinematic spawn, persistent corpse) ---
	public static readonly EnemyKit KROJ = EnemyKit.Of<WardenEnemy>(EnemyIds.Kroj, "Kroj", EnemyTier.Strong,
		EnemyMovement.Ground, "res://scenes/warden.tscn", e =>
	{
		e.max_health = 300.0f;
		e.body_size = new Vector2(28, 44);
		e.hurtbox_size = new Vector2(34, 52);
		e.move_speed = 55.0f;
		e.aggro = true;
		e.aggro_range = 640.0f;
		// Attack = a LUNGE: he closes and body-checks; close_lunge is the forward impulse.
		e.close_type = StrikeType.Lunge.Key();
		e.close_range = 130.0f;
		e.close_lunge = 460.0f;
		e.close_damage = 22.0f;
		e.close_knockback = 190.0f;
		e.close_stun = 0.3f;
		e.close_hitbox_x = 30.0f;
		e.close_hitbox_extents = new Vector2(40, 40);
		e.close_strike_lifetime = 0.3f;
		e.attack_cooldown = 2.0f;
		e.attack_align_y = 54.0f;
		e.attack_hitstop = 0.0f;
		// Teleport pursuit — warp in when the player stays far, landing outside lunge range.
		e.teleport_range = 360.0f;
		e.teleport_delay = 1.6f;
		e.teleport_land_offset = 96.0f;
	}) with { LiraDrop = 12 };
}
