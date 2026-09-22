using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// ENEMY sounds — PURE DATA (read by the <see cref="Sfx"/> service + <see cref="Enemy"/>). Same shape as
/// <see cref="SfxCharacters"/>. C# port of <c>configs/sfx_enemies.gd</c>. Naming mirrors EmittersEnemies: keys are
/// the attack's STRIKE TYPE. Conventions the code relies on: <c>enemy_death</c>/<c>enemy_spawn</c> (shared),
/// <c>&lt;id&gt;.&lt;type&gt;</c> (attack start), <c>&lt;id&gt;.&lt;type&gt;.&lt;frame&gt;</c> (per-frame hit, from FRAMES),
/// <c>&lt;id&gt;.delayed_projectile_burst</c> (a lob's delayed explosion). A key with no entry = silent no-op.
/// </summary>
public static class SfxEnemies
{
    public static readonly GDict CUES = new()
    {
        ["enemy_death"] = "res://sfx/enemy/enemy_death.wav",  // any enemy dies (positional)
        ["enemy_spawn"] = "res://sfx/enemy/enemy_spawn.wav",  // a batch enemy spawns w/ the puff — PLACEHOLDER
        // --- attack starts (<id>.<type>) ---
        ["kebus.melee"] = "res://sfx/enemy/kebus/attack/melee.wav",
        ["kebus.projectile"] = "res://sfx/enemy/kebus/attack/projectile.wav",
        ["baghel.projectile"] = "res://sfx/enemy/baghel/attack/projectile.wav",
        ["mazab.delayed_projectile"] = "res://sfx/enemy/mazab/attack/delayed_projectile.wav",
        ["nasen.aoe"] = "res://sfx/enemy/nasen/attack/aoe.wav",
        ["matat.aoe"] = "res://sfx/enemy/matat/attack/aoe.wav",  // PLACEHOLDER — AoE wind-up/roar
        ["tarri.blast"] = "res://sfx/enemy/tarri/attack/blast.wav",  // PLACEHOLDER — blast channel wind-up
        ["breski.melee"] = "res://sfx/enemy/breski/attack/melee.wav",  // PLACEHOLDER — combo wind-up
        ["ein.kamikaze"] = "res://sfx/enemy/ein/attack/kamikaze.wav",  // ein's arrival blast (self-destruct)
        // --- KROJ (warden). PLACEHOLDER cues (existing wavs) -- swap for bespoke warden sfx. Death is DISTINCT from grunts.
        ["kroj.lunge"] = "res://sfx/enemy/breski/attack/melee.wav",   // PLACEHOLDER -- lunge/body-check
        ["kroj.spawn"] = "res://sfx/enemy/enemy_spawn.wav",           // PLACEHOLDER -- cinematic entrance
        ["kroj.warp"] = "res://sfx/enemy/matat/attack/aoe.wav",       // PLACEHOLDER -- teleport telegraph + arrival
        ["kroj.death"] = "res://sfx/enemy/tarri/attack/blast.wav",    // PLACEHOLDER -- warden death (louder than enemy_death)
        // --- delayed_projectile bursts (<id>.delayed_projectile_burst) ---
        ["mazab.delayed_projectile_burst"] = "res://sfx/enemy/mazab/attack/delayed_projectile_burst.wav",
        // --- per-frame hit cues (referenced from FRAMES) ---
        ["baghel.projectile.4"] = "res://sfx/enemy/baghel/attack/projectile_4.wav",
        ["kebus.projectile.3"] = "res://sfx/enemy/kebus/attack/projectile_3.wav",
        ["nasen.aoe.2"] = "res://sfx/enemy/nasen/attack/aoe_2.wav",
        ["matat.aoe.4"] = "res://sfx/enemy/matat/attack/aoe_4.wav",  // PLACEHOLDER — the AoE erupt/impact
        ["tarri.blast.3"] = "res://sfx/enemy/tarri/attack/blast_3.wav",  // PLACEHOLDER — the blast FIRES (last frame)
        ["breski.melee.4"] = "res://sfx/enemy/breski/attack/melee_4.wav",  // PLACEHOLDER — combo hit 1
        ["breski.melee.9"] = "res://sfx/enemy/breski/attack/melee_9.wav",  // PLACEHOLDER — combo hit 2
    };

    /// <summary>Per-cue base VOLUME in decibels (negative = quieter), applied on top of any call-site volume_db.
    /// Only list cues that need trimming; unlisted cues play at 0 dB.</summary>
    public static readonly GDict VOLUMES = new();

    public static readonly GDict FRAMES = new()
    {
        ["baghel"] = new GDict { ["attack_projectile"] = new GDict { [4] = "baghel.projectile.4" } },
        ["kebus"] = new GDict { ["attack_projectile"] = new GDict { [3] = "kebus.projectile.3" } },
        ["nasen"] = new GDict { ["attack_aoe"] = new GDict { [2] = "nasen.aoe.2" } },  // rage AoE erupts on this frame
        ["matat"] = new GDict { ["attack_aoe"] = new GDict { [4] = "matat.aoe.4" } },  // AoE erupts (sheet-relative)
        ["tarri"] = new GDict { ["attack_blast"] = new GDict { [3] = "tarri.blast.3" } },  // blast erupts on last frame
        ["breski"] = new GDict { ["attack_melee"] = new GDict { [4] = "breski.melee.4", [9] = "breski.melee.9" } },  // 2-hit combo
        // (ein's arrival blast is a CODE event, not a sprite frame — played from DiverEnemy via "ein.kamikaze".)
    };

    /// <summary>The per-frame cue map for one enemy (empty if none) — anim → { sheet_frame: cue_key }.</summary>
    public static GDict FramesFor(string id) =>
        FRAMES.ContainsKey(id) ? FRAMES[id].As<GDict>() : new GDict();
}
