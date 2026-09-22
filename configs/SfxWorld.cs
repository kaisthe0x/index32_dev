using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// RUN / UI / ENVIRONMENT sounds — PURE DATA (read by the <see cref="Sfx"/> service). C# port of
/// <c>configs/sfx_world.gd</c>. <c>key</c> → path, played by code on an event (<c>Sfx.play("level_cleared")</c>).
/// </summary>
public static class SfxWorld
{
	public static readonly GDict CUES = new()
	{
		["level_cleared"] = "res://sfx/world/level_cleared.wav",  // last required enemy down, exit opens (RunManager)
		["fada_fig_collect"] = "res://sfx/character/fada_fig_pickup.wav",  // PLACEHOLDER — player touches a fada_fig (FadaFig.OnBodyEntered)
		["buff_levelup"] = "res://sfx/world/level_up.wav",   // buff milestone reached (RunManager)
		["buff_select"] = "res://sfx/character/fada_fig_pickup.wav",  // PLACEHOLDER — a buff is picked from the menu (RunManager.OnBuffChosen)
																   // Launch orb (traversal thing) — both PLACEHOLDER. launch_orb = the looping ambient hum it emits
																   // (positional, via Sfx.make_loop_2d in LaunchOrb); launch_orb_use = the one-shot when Khalid uses it.
		["launch_orb"] = "res://sfx/things/traversal/launch_orb/launch_orb.wav", // PLACEHOLDER — looping emitter hum
		["launch_orb_use"] = "res://sfx/things/traversal/launch_orb/launch_orb_use.wav", // PLACEHOLDER — on use
	};

	/// <summary>Per-cue base VOLUME in decibels (negative = quieter), applied on top of any call-site volume_db.
	/// Only list cues that need trimming; unlisted cues play at 0 dB. The one place to tame a too-loud sound file.</summary>
	public static readonly GDict VOLUMES = new()
	{
		["buff_levelup"] = -10.0f, // level_up.wav is hot — trim it here
	};
}
