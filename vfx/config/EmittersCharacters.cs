using System.Collections.Generic;
using Godot;

namespace MyGame;

/// <summary>
/// Per-CHARACTER particle emitters, driven by <see cref="ParticleDirector"/> on animation frames. Keyed
/// id → animation → rows, each an <see cref="EmitterDef"/> (which documents every field). Scenes are loaded when this
/// table is first touched, so they are resident before use. Hand-edit freely — this IS the source of truth.
/// </summary>
public static class EmittersCharacters
{
	private static PackedScene S(string path) => GD.Load<PackedScene>(path);

	/// <summary>Cherry Shots' bolts home in hard and may fly upward.</summary>
	private static void HomingShot(Node2D node)
	{
		var shot = (Projectile)node;
		shot.Homing = 8.0f;
		shot.CanFlyUp = true;
	}

	public static readonly Dictionary<string, Dictionary<string, EmitterDef[]>> Table = new()
	{
		["khalid"] = new()
		{
			["spawn"] = new[] { new EmitterDef(S("res://vfx/character/khalid/spawn/default/spawn_default.tscn"), new Vector2(0, -16)) { Frames = new[] { 1 } } },
			["death"] = new[] { new EmitterDef(S("res://vfx/character/khalid/death/default/death_default.tscn"), new Vector2(0, 0)) { Mode = EmitterMode.Sustained, Frames = new[] { 5, 6, 7 } } },
			["run"] = new[] { new EmitterDef(S("res://vfx/character/khalid/run/default/run_default.tscn"), new Vector2(-17, -17)) { Mode = EmitterMode.Sustained, AllFrames = true } },
			["jump"] = new[] { new EmitterDef(S("res://vfx/character/khalid/other/general_wind_streaks.tscn"), new Vector2(0, 0)) { Frames = new[] { 0, 1 } } },
			["fall"] = new[] { new EmitterDef(S("res://vfx/character/khalid/other/general_wind_streaks.tscn"), new Vector2(0, 0)) { Mode = EmitterMode.Sustained, AllFrames = true } },
			// DASH EFFECTS ("dash_*"): code-fired on dash-start (Player._dash_effect); a "Trail" node FOLLOWS
			// the player, everything else LINGERS. No "frames" — fired via FireEffect, not on a frame.
			["dash_default"] = new[] { new EmitterDef(S("res://vfx/character/khalid/dash/default/dash_default.tscn"), new Vector2(0, -3)) },
			["dash_crimson_vortex"] = new[] { new EmitterDef(S("res://vfx/character/khalid/dash/crimson_vortex/dash_crimson_vortex.tscn"), new Vector2(0, -16)) },
			["double_jump"] = new[] { new EmitterDef(S("res://vfx/character/khalid/jump/default/jump_default.tscn"), new Vector2(0, -3)) },
			["blink_out"] = new[] { new EmitterDef(S("res://vfx/character/khalid/other/blink_out.tscn"), new Vector2(0, -18)) },
			["blink_in"] = new[] { new EmitterDef(S("res://vfx/character/khalid/other/blink_in.tscn"), new Vector2(0, -18)) },
			// Ora ora: fist burst on the two punch frames (sheet 2 & 4). Per-punch SOUNDS in SfxCharacters.Frames.
			["attack_ora_ora"] = new[] { new EmitterDef(S("res://vfx/character/khalid/attack/ora_ora/attack_ora_ora.tscn"), new Vector2(23, -22)) { Frames = new[] { 2, 4 } } },
			// Bakshen: one charged slash — the Strike (hitbox + red burst) fires on the last frame.
			["attack_bakshen"] = new[] { new EmitterDef(S("res://vfx/character/khalid/attack/bakshen/attack_bakshen.tscn"), new Vector2(15, -18)) { Frames = new[] { 3 } } },
			// Zahluq: burst-forward dash-attack. follow:true -> the ONE Strike + hitbox ride the player through the
			// whole slide (single frame, else overlapping hitboxes double-hit). Strike lifetime covers the dash.
			["attack_zahluq"] = new[] { new EmitterDef(S("res://vfx/character/khalid/attack/zahluq/attack_zahluq.tscn"), new Vector2(0, 0)) { Frames = new[] { 0, 1, 2, 3 }, Follow = true } },
			// Twin Reaper: 5-hit spinning flurry — each hit its OWN scene, named _<sheetframe> (fires on that frame).
			["attack_twin_reaper"] = new[]
			{
				new EmitterDef(S("res://vfx/character/khalid/attack/twin_reaper/attack_twin_reaper_3.tscn"), new Vector2(14, -18)) { Frames = new[] { 3 } },
				new EmitterDef(S("res://vfx/character/khalid/attack/twin_reaper/attack_twin_reaper_4.tscn"), new Vector2(14, -18)) { Frames = new[] { 4 } },
				new EmitterDef(S("res://vfx/character/khalid/attack/twin_reaper/attack_twin_reaper_6.tscn"), new Vector2(14, -18)) { Frames = new[] { 6 } },
				new EmitterDef(S("res://vfx/character/khalid/attack/twin_reaper/attack_twin_reaper_7.tscn"), new Vector2(14, -18)) { Frames = new[] { 7 } },
				new EmitterDef(S("res://vfx/character/khalid/attack/twin_reaper/attack_twin_reaper_9.tscn"), new Vector2(14, -18)) { Frames = new[] { 9 } },
			},
			// Rope Dart: upgraded Twin Reaper, 17-frame spin. Hit frames 6/9/14/16, each its own scene.
			["attack_rope_dart"] = new[]
			{
				new EmitterDef(S("res://vfx/character/khalid/attack/rope_dart/attack_rope_dart_6.tscn"), new Vector2(14, -18)) { Frames = new[] { 6 } },
				new EmitterDef(S("res://vfx/character/khalid/attack/rope_dart/attack_rope_dart_9.tscn"), new Vector2(10, -18)) { Frames = new[] { 9 } },
				new EmitterDef(S("res://vfx/character/khalid/attack/rope_dart/attack_rope_dart_14.tscn"), new Vector2(4, -27)) { Frames = new[] { 14 } },
				new EmitterDef(S("res://vfx/character/khalid/attack/rope_dart/attack_rope_dart_16.tscn"), new Vector2(14, -18)) { Frames = new[] { 16 } },
			},
			// Cherry Shots: two laser Projectiles, each its own file (_3 small bolt on f3, _7 big on f7).
			["attack_cherry_shots"] = new[]
			{
				new EmitterDef(S("res://vfx/character/khalid/attack/cherry_shots/attack_cherry_shots_3.tscn"), new Vector2(16, -22)) { Frames = new[] { 3 }, Configure = HomingShot },
				new EmitterDef(S("res://vfx/character/khalid/attack/cherry_shots/attack_cherry_shots_7.tscn"), new Vector2(16, -22)) { Frames = new[] { 7 }, Configure = HomingShot },
			},
			// Spear: 3-hit combo — one file per hit (thrust, thrust, big finisher), named by frame.
			["attack_spear"] = new[]
			{
				new EmitterDef(S("res://vfx/character/khalid/attack/spear/attack_spear_6.tscn"), new Vector2(20, -18)) { Frames = new[] { 6 } },
				new EmitterDef(S("res://vfx/character/khalid/attack/spear/attack_spear_9.tscn"), new Vector2(22, -18)) { Frames = new[] { 9 } },
				new EmitterDef(S("res://vfx/character/khalid/attack/spear/attack_spear_13.tscn"), new Vector2(10, -18)) { Frames = new[] { 13 } },
			},
			["special_ground_breaker"] = new[] { new EmitterDef(S("res://vfx/character/khalid/special/ground_breaker/special_ground_breaker.tscn"), new Vector2(0, 0)) { Frames = new[] { 6 }, ConformToGround = true } },
			["special_frenemy"] = new[] { new EmitterDef(S("res://vfx/character/khalid/special/frenemy/special_frenemy.tscn"), new Vector2(40, -20)) { Frames = new[] { 3 } } },
			// Come Closer: the magnet FIELD spawns in front on the beckon frame. Particles are placeholder.
			["special_come_closer"] = new[] { new EmitterDef(S("res://vfx/character/khalid/special/come_closer/special_come_closer.tscn"), new Vector2(60, -18)) { Frames = new[] { 3 } } },
			// Redere Shield: a deploy flash (block/reflect is player-side).
			["special_redere_shield"] = new[] { new EmitterDef(S("res://vfx/character/khalid/special/redere_shield/special_redere_shield.tscn"), new Vector2(0, -20)) { Frames = new[] { 3 } } },
			// Redere Frisbee: the thrown-shield Projectile, launched on the release frame (fed the Action's hit).
			["special_redere_frisbee"] = new[] { new EmitterDef(S("res://vfx/character/khalid/special/redere_frisbee/special_redere_frisbee.tscn"), new Vector2(20, -22)) { Frames = new[] { 3 } } },
			// SURGES (e.g. Aegis): their aura is spawned by Player.grant_special_invuln, NOT a director burst — no row here.
			["slam"] = new[]
			{
				new EmitterDef(S("res://vfx/character/khalid/other/slam_wind_streaks.tscn"), new Vector2(0, -12)) { Mode = EmitterMode.Sustained, Frames = new[] { 0, 1, 2 } },
				new EmitterDef(S("res://vfx/character/khalid/slam/default/slam_default.tscn"), new Vector2(0, 0)) { Frames = new[] { 3, 4 }, ConformToGround = true },
			},
		},
	};
}
