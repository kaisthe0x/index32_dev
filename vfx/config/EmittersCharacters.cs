using Godot;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// Per-CHARACTER particle emitters, driven by <see cref="ParticleDirector"/> on animation frames. Keyed
/// id → animation → [ rows ]. Row: { scene, mode ('sustained'|'burst'), frames (sheet-relative ints or "all"),
/// pos, and optional node / conform_to_ground / follow }. C# port of <c>vfx/config/emitters_characters.gd</c>.
/// Scenes are loaded at first access (resident before use). Hand-edit freely — this IS the source of truth.
/// </summary>
public static class EmittersCharacters
{
	private static PackedScene S(string path) => GD.Load<PackedScene>(path);

	public static readonly GDict TABLE = new()
	{
		["khalid"] = new GDict
		{
			["spawn"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/spawn/default/spawn_default.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 1 }, ["pos"] = new Vector2(0, -16) } },
			["death"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/death/default/death_default.tscn"), ["mode"] = "sustained", ["frames"] = new GArr { 5, 6, 7 }, ["pos"] = new Vector2(0, 0) } },
			["run"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/run/default/run_default.tscn"), ["mode"] = "sustained", ["frames"] = "all", ["pos"] = new Vector2(-17, -17) } },
			["jump"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/other/general_wind_streaks.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 0, 1 }, ["pos"] = new Vector2(0, 0) } },
			["fall"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/other/general_wind_streaks.tscn"), ["mode"] = "sustained", ["frames"] = "all", ["pos"] = new Vector2(0, 0) } },
			// DASH EFFECTS ("dash_*"): code-fired on dash-start (Player._dash_effect); a "Trail" node FOLLOWS
			// the player, everything else LINGERS. No "frames" — fired via fire_effect, not on a frame.
			["dash_default"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/dash/default/dash_default.tscn"), ["mode"] = "burst", ["pos"] = new Vector2(0, -3) } },
			["dash_crimson_vortex"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/dash/crimson_vortex/dash_crimson_vortex.tscn"), ["mode"] = "burst", ["pos"] = new Vector2(0, -16) } },
			["double_jump"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/jump/default/jump_default.tscn"), ["mode"] = "burst", ["pos"] = new Vector2(0, -3) } },
			["blink_out"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/other/blink_out.tscn"), ["mode"] = "burst", ["pos"] = new Vector2(0, -18) } },
			["blink_in"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/other/blink_in.tscn"), ["mode"] = "burst", ["pos"] = new Vector2(0, -18) } },
			// Ora ora: fist burst on the two punch frames (sheet 2 & 4). Per-punch SOUNDS in SfxCharacters.FRAMES.
			["attack_ora_ora"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/attack/ora_ora/attack_ora_ora.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 2, 4 }, ["pos"] = new Vector2(23, -22) } },
			// Bakshen: one charged slash — the Strike (hitbox + red burst) fires on the last frame.
			["attack_bakshen"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/attack/bakshen/attack_bakshen.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 3 }, ["pos"] = new Vector2(15, -18) } },
			// Zahluq: burst-forward dash-attack. follow:true -> the ONE Strike + hitbox ride the player through the
			// whole slide (single frame, else overlapping hitboxes double-hit). Strike lifetime covers the dash.
			["attack_zahluq"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/attack/zahluq/attack_zahluq.tscn"), ["mode"] = "continuous", ["frames"] = new GArr { 0, 1, 2, 3 }, ["pos"] = new Vector2(0, 0), ["follow"] = true } },
			// Twin Reaper: 5-hit spinning flurry — each hit its OWN scene, named _<sheetframe> (fires on that frame).
			["attack_twin_reaper"] = new GArr
			{
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/twin_reaper/attack_twin_reaper_3.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 3 }, ["pos"] = new Vector2(14, -18) },
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/twin_reaper/attack_twin_reaper_4.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 4 }, ["pos"] = new Vector2(14, -18) },
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/twin_reaper/attack_twin_reaper_6.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 6 }, ["pos"] = new Vector2(14, -18) },
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/twin_reaper/attack_twin_reaper_7.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 7 }, ["pos"] = new Vector2(14, -18) },
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/twin_reaper/attack_twin_reaper_9.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 9 }, ["pos"] = new Vector2(14, -18) },
			},
			// Dual Executioner: upgraded Twin Reaper, 17-frame spin. Hit frames 6/9/14/16, each its own scene.
			["attack_dual_executioner"] = new GArr
			{
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/dual_executioner/attack_dual_executioner_6.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 6 }, ["pos"] = new Vector2(14, -18) },
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/dual_executioner/attack_dual_executioner_9.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 9 }, ["pos"] = new Vector2(10, -18) },
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/dual_executioner/attack_dual_executioner_14.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 14 }, ["pos"] = new Vector2(4, -27) },
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/dual_executioner/attack_dual_executioner_16.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 16 }, ["pos"] = new Vector2(14, -18) },
			},
			// Cherry Shots: two laser Projectiles, each its own file (_3 small bolt on f3, _7 big on f7).
			["attack_cherry_shots"] = new GArr
			{
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/cherry_shots/attack_cherry_shots_3.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 3 }, ["pos"] = new Vector2(16, -22), ["set"] = new GDict { ["homing"] = 8.0, ["can_fly_up"] = true } },
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/cherry_shots/attack_cherry_shots_7.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 7 }, ["pos"] = new Vector2(16, -22), ["set"] = new GDict { ["homing"] = 8.0, ["can_fly_up"] = true } },
			},
			// Spear: 3-hit combo — one file per hit (thrust, thrust, big finisher), named by frame.
			["attack_spear"] = new GArr
			{
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/spear/attack_spear_6.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 6 }, ["pos"] = new Vector2(20, -18) },
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/spear/attack_spear_9.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 9 }, ["pos"] = new Vector2(22, -18) },
				new GDict { ["scene"] = S("res://vfx/character/khalid/attack/spear/attack_spear_13.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 13 }, ["pos"] = new Vector2(10, -18) },
			},
			["special_ground_breaker"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/special/ground_breaker/special_ground_breaker.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 6 }, ["pos"] = new Vector2(0, 0), ["conform_to_ground"] = true } },
			["special_frenemy"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/special/frenemy/special_frenemy.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 3 }, ["pos"] = new Vector2(40, -20) } },
			// Come Closer: the magnet FIELD spawns in front on the beckon frame. Particles are placeholder.
			["special_come_closer"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/special/come_closer/special_come_closer.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 3 }, ["pos"] = new Vector2(60, -18) } },
			// Redere Shield: a deploy flash (block/reflect is player-side).
			["special_redere_shield"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/special/redere_shield/special_redere_shield.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 3 }, ["pos"] = new Vector2(0, -20) } },
			// Redere Frisbee: the thrown-shield Projectile, launched on the release frame (fed the Action's hit).
			["special_redere_frisbee"] = new GArr { new GDict { ["scene"] = S("res://vfx/character/khalid/special/redere_frisbee/special_redere_frisbee.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 3 }, ["pos"] = new Vector2(20, -22) } },
			// SURGES (e.g. Aegis): their aura is spawned by Player.grant_special_invuln, NOT a director burst — no row here.
			["slam"] = new GArr
			{
				new GDict { ["scene"] = S("res://vfx/character/khalid/other/slam_wind_streaks.tscn"), ["mode"] = "sustained", ["frames"] = new GArr { 0, 1, 2 }, ["pos"] = new Vector2(0, -12) },
				new GDict { ["scene"] = S("res://vfx/character/khalid/slam/default/slam_default.tscn"), ["mode"] = "burst", ["frames"] = new GArr { 3, 4 }, ["pos"] = new Vector2(0, 0), ["conform_to_ground"] = true },
			},
		},
	};
}
