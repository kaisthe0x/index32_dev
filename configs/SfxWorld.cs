using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// RUN / UI / ENVIRONMENT sounds — PURE DATA (read by the <see cref="Sfx"/> service). C# port of
/// <c>configs/sfx_world.gd</c>. <c>key</c> → path, played by code on an event (<c>Sfx.play("round_start")</c>).
/// </summary>
public static class SfxWorld
{
	public static readonly GDict CUES = new()
	{
		["fada_fig_collect"] = "res://sfx/character/fada_fig_pickup.wav",  // PLACEHOLDER — player touches a fada_fig (FadaFig.OnBodyEntered)
		["lira_collect"] = "res://sfx/character/lira_pickup.wav",  // a Lira coin reaches Khalid (Lira.OnArrived)
		["buff_select"] = "res://sfx/character/fada_fig_pickup.wav",  // PLACEHOLDER — a buff is picked from the menu (RunManager.OnBuffChosen)
																   // Launch orb (traversal thing) — both PLACEHOLDER. launch_orb = the looping ambient hum it emits
																   // (positional, via Sfx.make_loop_2d in LaunchOrb); launch_orb_use = the one-shot when Khalid uses it.
		["launch_orb"] = "res://sfx/things/traversal/launch_orb/launch_orb.wav", // PLACEHOLDER — looping emitter hum
		["launch_orb_use"] = "res://sfx/things/traversal/launch_orb/launch_orb_use.wav", // PLACEHOLDER — on use
		// Rounds (RunManager): round_start plays as each round begins (the "ROUND n" label pops).
		["round_start"] = "res://sfx/world/round/round_start.wav",
	};

	/// <summary>Per-cue MIX offset in decibels (negative = quieter), on top of the automatic loudness normalization
	/// (every cue already plays at <see cref="Sfx.TargetLoudness"/>). Only for DELIBERATE choices — a cue that should
	/// sit under or over the rest — never to fix a hot or quiet file. Unlisted = 0.</summary>
	public static readonly GDict VOLUMES = new()
	{
		["launch_orb"] = -16.0f, // the orb's looping hum is an ambient bed, well under the action
	};

	/// <summary>Per-cue random PITCH range as (min, max) offsets from normal pitch — <c>new(-0.06f, 0.06f)</c> = ±6%,
	/// <c>new(0f, 0.08f)</c> = same-or-up to +8% — re-rolled every play so repeated sounds don't sound copy-pasted. A key
	/// may name a GROUP: a cue with no entry of its own uses its nearest dotted prefix. Unlisted = fixed pitch.</summary>
	public static readonly GDict PITCH = new()
	{
		["lira_collect"] = new Vector2(0.0f, 0.08f), // coins land in bursts — vary them, but never below the real pitch
	};
}
