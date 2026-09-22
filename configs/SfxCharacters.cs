using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// CHARACTER sounds — PURE DATA (the <see cref="Sfx"/> service + <see cref="ParticleDirector"/> read this).
/// C# port of <c>configs/sfx_characters.gd</c>. <see cref="CUES"/> is the master key→path list (reference by key,
/// e.g. <c>Sfx.play("dash")</c>); <see cref="FRAMES"/> is character → animation → { sheet_frame: cue_key }, played by
/// ParticleDirector when an animation reaches that frame (SHEET-relative, same numbering as Emitters / HIT_FRAMES).
/// Key convention: <c>&lt;name&gt;</c> for a whole cue, <c>&lt;name&gt;.&lt;frame&gt;</c> for a frame-specific hit.
/// </summary>
public static class SfxCharacters
{
    public static readonly GDict CUES = new()
    {
        // --- movement / feedback (played by code on an event) ---
        ["dash"] = "res://sfx/character/dash/dash.wav",
        ["jump"] = "res://sfx/character/jump/jump.wav",
        // Slam: slam_down = descent whoosh; slam = ground impact (cuts the descent whoosh on landing).
        ["slam_down"] = "res://sfx/character/slam/slam_down.wav",
        ["slam"] = "res://sfx/character/slam/slam.wav",
        ["run"] = "res://sfx/character/run.wav", // looping footsteps (Sfx.make_loop)
        ["ruh_absorb"] = "res://sfx/character/ruh_absorb.wav", // a Ruh soul lands on Khalid
        ["player_death"] = "res://sfx/character/death/player_death.wav", // death sting/tone — PLACEHOLDER
        // Low-HP warnings — fired ONCE by Player.take_damage when HP crosses DOWN through a threshold (re-arms if healed).
        ["health_half"] = "res://sfx/character/health/health_half.wav", // crossed 50% HP — PLACEHOLDER
        ["health_low"] = "res://sfx/character/health/health_low.wav",   // crossed 20% HP — PLACEHOLDER
        // Hurt grunts — one picked at RANDOM per hit (Sfx.play_random). Drop 2-3; a missing one is just never picked.
        ["hurt.1"] = "res://sfx/character/hurt/hurt_1.wav",
        ["hurt.2"] = "res://sfx/character/hurt/hurt_2.wav",
        ["hurt.3"] = "res://sfx/character/hurt/hurt_3.wav",
        // --- attack / special HITS (played by the director on the FRAMES below) ---
        ["twin_reaper.3"] = "res://sfx/character/attack/twin_reaper/twin_reaper_3.wav",
        ["twin_reaper.4"] = "res://sfx/character/attack/twin_reaper/twin_reaper_4.wav",
        ["twin_reaper.6"] = "res://sfx/character/attack/twin_reaper/twin_reaper_6.wav",
        ["twin_reaper.7"] = "res://sfx/character/attack/twin_reaper/twin_reaper_7.wav",
        ["twin_reaper.9"] = "res://sfx/character/attack/twin_reaper/twin_reaper_9.wav",
        ["ora_ora.2"] = "res://sfx/character/attack/ora_ora/ora_ora_2.wav",
        ["ora_ora.4"] = "res://sfx/character/attack/ora_ora/ora_ora_4.wav",
        ["cherry_shots.3"] = "res://sfx/character/attack/cherry_shots/cherry_shots_3.wav",
        ["cherry_shots.7"] = "res://sfx/character/attack/cherry_shots/cherry_shots_7.wav",
        ["spear.6"] = "res://sfx/character/attack/spear/spear_6.wav",
        ["spear.9"] = "res://sfx/character/attack/spear/spear_9.wav",
        ["spear.13"] = "res://sfx/character/attack/spear/spear_13.wav",
        ["bakshen"] = "res://sfx/character/attack/bakshen/bakshen.wav", // one big charged slash (no per-frame hits)
        ["zahluq"] = "res://sfx/character/attack/zahluq/zahluq.wav", // the dash-attack whoosh (on the burst frame)
        // Dual Executioner (upgraded Twin Reaper) — hit frames 6/9/14/16. A missing/omitted cue is just silent.
        ["dual_executioner.6"] = "res://sfx/character/attack/dual_executioner/dual_executioner_6.wav",
        ["dual_executioner.9"] = "res://sfx/character/attack/dual_executioner/dual_executioner_9.wav",
        ["dual_executioner.14"] = "res://sfx/character/attack/dual_executioner/dual_executioner_14.wav",
        ["dual_executioner.16"] = "res://sfx/character/attack/dual_executioner/dual_executioner_16.wav",
        ["frenemy"] = "res://sfx/character/special/frenemy/frenemy.wav",
        ["ground_breaker.3"] = "res://sfx/character/special/ground_breaker/ground_breaker_3.wav",
        ["ground_breaker"] = "res://sfx/character/special/ground_breaker/ground_breaker.wav",
        // SURGES (passive abilities on the `surge` button) — an activation cue played by CODE on trigger, not frame-synced.
        ["surge_aegis"] = "res://sfx/character/surge/aegis/surge_aegis.wav",
        ["surge_jnoon"] = "res://sfx/character/surge/jnoon/surge_jnoon.wav", // PLACEHOLDER (copy of aegis)
        ["surge_asra"] = "res://sfx/character/surge/asra/surge_asra.wav", // PLACEHOLDER (copy of aegis)
        ["surge_nem"] = "res://sfx/character/surge/nem/surge_nem.wav", // PLACEHOLDER (copy of aegis)
        ["surge_wara"] = "res://sfx/character/surge/wara/surge_wara.wav", // PLACEHOLDER — cast/arm cue
        ["surge_wara_trigger"] = "res://sfx/character/surge/wara/surge_wara_trigger.wav", // PLACEHOLDER — fired cue
        // New specials — cast cues on the strike frame (silent until the .wav exists).
        ["come_closer"] = "res://sfx/character/special/come_closer/come_closer.wav",
        ["redere_shield"] = "res://sfx/character/special/redere_shield/redere_shield.wav",
        // Shield BLOCK events — played by CODE in Player._on_hurt (bright PERFECT-PARRY vs duller plain-BLOCK).
        ["redere_shield_parry"] = "res://sfx/character/special/redere_shield/redere_shield_parry.wav",
        ["redere_shield_block"] = "res://sfx/character/special/redere_shield/redere_shield_block.wav",
        ["redere_frisbee.1"] = "res://sfx/character/special/redere_frisbee/redere_frisbee_1.wav",
        ["redere_frisbee.impact"] = "res://sfx/character/special/redere_frisbee/redere_frisbee_impact.wav",
    };

    /// <summary>Per-cue base VOLUME in decibels (negative = quieter), applied on top of any call-site volume_db.
    /// Only list cues that need trimming; unlisted cues play at 0 dB.</summary>
    public static readonly GDict VOLUMES = new();

    public static readonly GDict FRAMES = new()
    {
        ["khalid"] = new GDict
        {
            ["attack_twin_reaper"] = new GDict { [3] = "twin_reaper.3", [4] = "twin_reaper.4", [6] = "twin_reaper.6", [7] = "twin_reaper.7", [9] = "twin_reaper.9" },
            ["attack_dual_executioner"] = new GDict { [6] = "dual_executioner.6", [9] = "dual_executioner.9", [14] = "dual_executioner.14", [16] = "dual_executioner.16" },
            ["attack_ora_ora"] = new GDict { [2] = "ora_ora.2", [4] = "ora_ora.4" },
            ["attack_cherry_shots"] = new GDict { [3] = "cherry_shots.3", [7] = "cherry_shots.7" },
            ["attack_spear"] = new GDict { [6] = "spear.6", [9] = "spear.9", [13] = "spear.13" },
            ["attack_bakshen"] = new GDict { [3] = "bakshen" }, // slash sound on the RELEASE frame (3), not the 1s windup (frame 1) — so a stagger mid-windup makes no sound
            ["attack_zahluq"] = new GDict { [2] = "zahluq" },
            ["special_ground_breaker"] = new GDict { [1] = "ground_breaker", [3] = "ground_breaker.3" },
            ["special_frenemy"] = new GDict { [3] = "frenemy" },
            ["special_come_closer"] = new GDict { [3] = "come_closer" },
            ["special_redere_shield"] = new GDict { [3] = "redere_shield" },
            ["special_redere_frisbee"] = new GDict { [1] = "redere_frisbee.1" },
        },
    };
}
