using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// The enemy roster — one named kit per enemy TYPE, referenced by the level/wave tables in <see cref="Levels"/>.
/// A kit is a spawn spec: an `id` (built from scenes/enemy.tscn) or a custom `scene`, plus Enemy @export overrides
/// (combat tuning), applied by RunManager via <c>enemy.Set(key, value)</c> — so a kit stays a by-name override BAG
/// (its keys mirror Enemy's [Export] names), not a fixed record. `tier` (<see cref="EnemyTier"/>) is advisory
/// wave-building shorthand (RunManager skips it). IDs use <see cref="EnemyIds"/>; close_type/far_type (the
/// enemy's close-range / far-range attack) use the <see cref="StrikeType"/> taxonomy. C# port of
/// <c>scripts/run/enemies.gd</c> (pure data).
/// </summary>
public static class EnemyKits
{
	public static readonly GDict KEBUS = new()
	{
		{ "id", EnemyIds.Kebus }, { "tier", (int)EnemyTier.Strong }, { "movement", (int)EnemyMovement.Ground },
		{ "close_type", StrikeType.Melee.Key() }, { "far_type", StrikeType.Projectile.Key() },
		// far_mode defaults to "aimed": tracks the player + aims at the body, tilt capped to ±45° (never vertical);
		// attack_align_y is wide so he'll engage you a level up/down.
		{ "far_aim_cap", 45.0 }, { "attack_align_y", 120.0 }, { "far_hitbox_extents", new Vector2(7, 10) },
		{ "projectile_speed", 200.0 },
	};

	public static readonly GDict BAGHEL = new()
	{
		{ "id", EnemyIds.Baghel }, { "tier", (int)EnemyTier.Chip }, { "movement", (int)EnemyMovement.Ground }, { "far_type", StrikeType.Projectile.Key() },
		{ "far_mode", "ground_wave" }, { "far_range", 130.0 }, { "far_travel", 100.0 }, { "projectile_speed", 200.0 },
		{ "far_hitbox_extents", new Vector2(4, 15) }, { "far_hitbox_offset", new Vector2(0, -9) }, { "far_damage", 7.0 },
		{ "idle_time_min", 5.0 }, { "idle_time_max", 7.0 },
	};

	public static readonly GDict MAZAB = new()
	{
		{ "id", EnemyIds.Mazab }, { "tier", (int)EnemyTier.Mid }, { "movement", (int)EnemyMovement.Ground }, { "far_type", StrikeType.DelayedProjectile.Key() },
		{ "far_mode", "lob" }, { "far_range", 260.0 }, { "attack_align_y", 120.0 }, { "attack_cooldown", 2.2 },
		{ "far_damage", 16.0 }, { "far_knockback", 160.0 }, { "far_stun", 0.25 },
		{ "lob_arc_time", 0.9 }, { "lob_dwell", 1.0 }, { "lob_explosion_extents", new Vector2(48, 26) },
	};

	public static readonly GDict NASEN = new()
	{
		{ "scene", "res://scenes/sleeper_enemy.tscn" }, { "id", EnemyIds.Nasen }, { "display_name", "Nasen" },
		{ "max_health", 90.0 }, { "tier", (int)EnemyTier.Strong }, { "movement", (int)EnemyMovement.Stationary }, { "optional", true }, { "close_type", StrikeType.Aoe.Key() }, { "conform_ground", true },
	};

	public static readonly GDict EIN = new()
	{
		{ "scene", "res://scenes/diver_enemy.tscn" }, { "id", EnemyIds.Ein }, { "display_name", "Ein" }, { "max_health", 28.0 }, { "air", true }, { "movement", (int)EnemyMovement.Flying }, { "close_type", StrikeType.Kamikaze.Key() },
		{ "body_size", new Vector2(22, 22) }, { "hurtbox_size", new Vector2(26, 26) }, { "move_speed", 34.0 },
		{ "patrol_distance", 70.0 }, { "tier", (int)EnemyTier.Mid },
	};

	public static readonly GDict MATAT = new()
	{
		{ "id", EnemyIds.Matat }, { "display_name", "Matat" }, { "tier", (int)EnemyTier.Strong }, { "movement", (int)EnemyMovement.Ground }, { "close_type", StrikeType.Aoe.Key() }, { "conform_ground", true },
		{ "max_health", 95.0 }, { "body_size", new Vector2(20, 34) }, { "hurtbox_size", new Vector2(24, 40) },
		{ "move_speed", 40.0 }, { "patrol_distance", 90.0 }, { "far_range", 300.0 },
		{ "close_range", 52.0 }, { "attack_align_y", 44.0 }, { "attack_cooldown", 1.2 },
		{ "attack_loops", true }, { "attack_hitstop", 0.0 },
		{ "close_damage", 11.0 }, { "close_knockback", 150.0 }, { "close_stun", 0.25 },
		{ "close_hitbox_x", 0.0 }, { "close_hitbox_extents", new Vector2(46, 30) }, { "close_strike_lifetime", 0.35 },
	};

	public static readonly GDict TARRI = new()
	{
		{ "id", EnemyIds.Tarri }, { "display_name", "Tarri" }, { "tier", (int)EnemyTier.Mid }, { "movement", (int)EnemyMovement.Ground }, { "close_type", StrikeType.Blast.Key() },
		{ "max_health", 70.0 }, { "body_size", new Vector2(18, 24) }, { "hurtbox_size", new Vector2(22, 28) },
		{ "move_speed", 34.0 }, { "patrol_distance", 100.0 },
		{ "close_range", 140.0 }, { "attack_align_y", 52.0 }, { "attack_cooldown", 2.6 },
		{ "close_hitbox_x", 70.0 }, { "close_hitbox_extents", new Vector2(70, 22) }, { "close_strike_lifetime", 2.0 },
		{ "close_damage", 16.0 }, { "close_knockback", 120.0 }, { "close_stun", 0.3 },
		{ "attack_hitstop", 2.0 }, { "attack_shake", 1.5 },
	};

	public static readonly GDict BRESKI = new()
	{
		{ "id", EnemyIds.Breski }, { "display_name", "Breski" }, { "tier", (int)EnemyTier.Strong }, { "movement", (int)EnemyMovement.Ground }, { "close_type", StrikeType.Melee.Key() },
		{ "max_health", 110.0 }, { "body_size", new Vector2(18, 28) }, { "hurtbox_size", new Vector2(22, 34) },
		{ "move_speed", 46.0 }, { "patrol_distance", 90.0 },
		{ "close_range", 56.0 }, { "attack_align_y", 44.0 }, { "attack_cooldown", 1.8 },
		{ "close_damage", 10.0 }, { "close_knockback", 130.0 }, { "close_stun", 0.2 },
		{ "attack_hitstop", 0.12 }, { "attack_shake", 1.0 },
	};

	// --- Wardens (elite tier: WardenEnemy — teleporting lunger, cinematic spawn, persistent corpse) ---
	public static readonly GDict KROJ = new()
	{
		{ "scene", "res://scenes/warden.tscn" }, { "id", EnemyIds.Kroj }, { "display_name", "Kroj" },
		{ "movement", (int)EnemyMovement.Ground }, { "tier", (int)EnemyTier.Strong },
		{ "max_health", 300.0 }, { "body_size", new Vector2(28, 44) }, { "hurtbox_size", new Vector2(34, 52) },
		{ "move_speed", 55.0 }, { "aggro", true }, { "aggro_range", 640.0 }, { "fada_fig_drop", 12 },
        // Attack = a LUNGE (close_type=lunge): he closes and body-checks; close_lunge is the forward impulse.
        { "close_type", StrikeType.Lunge.Key() }, { "close_range", 130.0 }, { "close_lunge", 460.0 },
		{ "close_damage", 22.0 }, { "close_knockback", 190.0 }, { "close_stun", 0.3 },
		{ "close_hitbox_x", 30.0 }, { "close_hitbox_extents", new Vector2(40, 40) }, { "close_strike_lifetime", 0.3 },
		{ "attack_cooldown", 2.0 }, { "attack_align_y", 54.0 }, { "attack_hitstop", 0.0 },
        // Teleport pursuit (WardenEnemy exports) — warp in when the player stays far, landing outside lunge range.
        { "teleport_range", 360.0 }, { "teleport_delay", 1.6 }, { "teleport_land_offset", 96.0 },
	};
}
