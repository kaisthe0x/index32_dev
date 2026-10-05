using Godot;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// Per-ENEMY particle emitters. Same { scene, pos } schema as <see cref="EmittersCharacters"/>, but enemy
/// effects are attached in code by state/event (not fired on animation frames), so rows carry no frames/mode.
/// C# port of <c>vfx/config/emitters_enemies.gd</c>. A row's key is the attack's STRIKE TYPE (configs/strike_spec):
/// melee/projectile/delayed_projectile/aoe/delayed_aoe/kamikaze/blast/tackle/trap; a component appends a role (<c>_burst</c>/<c>_trail</c>).
/// AUTHORITATIVE for presence: no row = no emitter (combat still runs). Hand-edit freely — this IS the source of truth.
/// </summary>
public static class EmittersEnemies
{
    private static PackedScene S(string path) => GD.Load<PackedScene>(path);

    public static readonly GDict TABLE = new()
    {
        // --- projectile (a straight/aimed shot) ---
        ["kebus"] = new GDict { ["projectile"] = new GDict { ["scene"] = S("res://vfx/enemy/kebus/attack/kebus_projectile.tscn"), ["pos"] = new Vector2(18, -22) } },
        ["baghel"] = new GDict { ["projectile"] = new GDict { ["scene"] = S("res://vfx/enemy/baghel/attack/baghel_projectile.tscn"), ["pos"] = new Vector2(16, 1) } },
        // --- delayed_projectile (a lobbed bomb that dwells, then bursts) ---
        ["mazab"] = new GDict
        {
            ["delayed_projectile"] = new GDict { ["scene"] = S("res://vfx/enemy/mazab/attack/mazab_delayed_projectile.tscn"), ["pos"] = new Vector2(18, -40) },
            ["delayed_projectile_burst"] = new GDict { ["scene"] = S("res://vfx/enemy/mazab/attack/mazab_delayed_projectile_burst.tscn"), ["pos"] = new Vector2(0, 0) },
        },
        // --- aoe (a shockwave erupting in place) ---
        ["nasen"] = new GDict { ["aoe"] = new GDict { ["scene"] = S("res://vfx/enemy/nasen/attack/nasen_aoe.tscn"), ["pos"] = new Vector2(0, 0) } },
        ["matat"] = new GDict { ["aoe"] = new GDict { ["scene"] = S("res://vfx/enemy/matat/attack/matat_aoe.tscn"), ["pos"] = new Vector2(0, -10) } },
        // --- kamikaze (a charge/dive that AoE-blasts on arrival + self-destructs) + its dive trail ---
        ["ein"] = new GDict
        {
            ["kamikaze"] = new GDict { ["scene"] = S("res://vfx/enemy/ein/attack/ein_kamikaze.tscn"), ["pos"] = new Vector2(0, -16) },
            ["kamikaze_trail"] = new GDict { ["scene"] = S("res://vfx/enemy/ein/attack/ein_kamikaze_trail.tscn"), ["pos"] = new Vector2(0, -12) },
        },
        // --- blast (a wide STATIONARY forward blast) + walk trail. Blast rides the strike, so pos is a body-height nudge.
        ["tarri"] = new GDict
        {
            ["blast"] = new GDict { ["scene"] = S("res://vfx/enemy/tarri/attack/tarri_blast.tscn"), ["pos"] = new Vector2(15, -17) },
            ["walk_trail"] = new GDict { ["scene"] = S("res://vfx/enemy/tarri/walk/tarri_walk_trail.tscn"), ["pos"] = new Vector2(0, -6) },
        },
        // --- blast, WIND (a stationary forward gust that flings instead of hurting — Hit.Gust). Same channel as Tarri's.
        ["ventilator"] = new GDict
        {
            ["blast"] = new GDict { ["scene"] = S("res://vfx/enemy/ventilator/attack/ventilator_blast.tscn"), ["pos"] = new Vector2(10, -20) },
        },
        // --- melee (a 2-hit COMBO — each hit its own Strike scene, keyed by the SHEET FRAME: melee_4 jab, melee_9 follow-up.
        ["breski"] = new GDict
        {
            ["melee_4"] = new GDict { ["scene"] = S("res://vfx/enemy/breski/attack/breski_melee_4.tscn"), ["pos"] = new Vector2(26, -18) },
            ["melee_9"] = new GDict { ["scene"] = S("res://vfx/enemy/breski/attack/breski_melee_9.tscn"), ["pos"] = new Vector2(32, -20) },
        },
        // --- KROJ (warden). PLACEHOLDER effects (pointed at existing enemy scenes) — swap for bespoke warden vfx.
        // Keys used in code: "lunge" (base close-attack vfx), "spawn"/"warp"/"death_burst" (WardenEnemy), "walk_trail" (base).
        ["kroj"] = new GDict
        {
            ["lunge"] = new GDict { ["scene"] = S("res://vfx/enemy/matat/attack/matat_aoe.tscn"), ["pos"] = new Vector2(34, -22) },
            ["spawn"] = new GDict { ["scene"] = S("res://vfx/enemy/nasen/attack/nasen_aoe.tscn"), ["pos"] = new Vector2(0, -24) },
            ["warp"] = new GDict { ["scene"] = S("res://vfx/enemy/ein/attack/ein_kamikaze.tscn"), ["pos"] = new Vector2(0, -24) },
            ["death_burst"] = new GDict { ["scene"] = S("res://vfx/enemy/tarri/attack/tarri_blast.tscn"), ["pos"] = new Vector2(0, -24) },
            ["walk_trail"] = new GDict { ["scene"] = S("res://vfx/enemy/tarri/walk/tarri_walk_trail.tscn"), ["pos"] = new Vector2(0, -8) },
        },
    };
}
