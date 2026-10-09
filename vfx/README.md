# vfx — visual effects

Everything that draws an effect over the gameplay lives here: the **frame-indexed
particle system** (data-driven emitters layered on the sprites), the drawn
**`Strike`** slashes, and the per-enemy attack VFX. One folder owns the look,
the config, the code, and the build tools.

Combat lives elsewhere on purpose: `Hitbox`/`Hurtbox`/`Combatant` are in
`scripts/combat/`, and the abilities that *trigger* effects are in
`scripts/abilities/`. This folder is the effects themselves, not the damage model
or who fires them.

## Folder layout

```
vfx/
  config/
    emitters.gd            Emitters — registry aggregating the two tables below
    emitters_characters.gd EmittersCharacters.TABLE — per-character, frame-indexed (preload scenes)
    emitters_enemies.gd    EmittersEnemies.TABLE — per-enemy, code-driven (preload scenes)
  script/
    particle_director.gd   ParticleDirector — watches the sprite, emits per frame
    build_particles.gd     Scaffold particle-type scenes (skips existing)
    gen_particle_textures.py  Regenerate the shared particle textures
    # The attack COMPONENTS a scene attaches -- Strike (slash/blast/ground) and
    # Projectile (a shot, player or enemy) -- live in scripts/combat/, with Hitbox.
  character/<id>/          Per-character effects, grouped by category:
    attack/<move>/           a named attack + the art ONLY it uses
                             (khalid/attack/ora_ora/attack_ora_ora.tscn)
    special/<move>/          a named special (khalid/special/ground_breaker/…)
    dash/default/            movement effects, nested under a variant folder
    jump/default/            (default today; room for alt variants later)
    run/default/
    slam/default/
    other/                   shared per-character bits (general_wind_streaks,
                             slam_wind_streaks, special_ready) — flat, not nested
    shared/textures/         static textures reused across THIS character's effects
                             (smoke, poison, sparks)
  enemy/<id>/attack/        Per-enemy attack scenes (baghel/attack/baghel_projectile.tscn,
                            kebus/attack/kebus_projectile.tscn). Referenced (preloaded) from
                            EmittersEnemies.TABLE by enemy_id, not the director's frame index.
    death/default/           death burst, tinted to this character's palette
    spawn/default/           spawn (materialize) burst, tinted to this character's palette
  shared/textures/          Cross-entity building blocks: pixel_ember.png, soft_dot.png
                            (used by enemies and several characters)
  stage/                    Ambient / background stage effects
```

**Death + spawn particles.** `death` and `spawn` are real animations, so their emitters
auto-fire on the anim frames like `run`/`jump`. Each character has its **own**
`death/default/death_default.tscn` and `spawn/default/spawn_default.tscn` (colours pulled
from that character's `dash/default/` palette). In the `Emitters` config: `"death"` fires a
burst on the **last** death frame (the poof lands as the pose dissolves), `"spawn"` on the
**first** spawn frame (as the character materializes). Retune frame/scene per character.

**Referencing a scene.** A row points at its scene by **`preload("res://…")`** in the
`Emitters` table — validated at parse time, resident before the game runs (no runtime `load`).
Keep scenes role-named (`attack_chainsaw`, `run_default`, `special_ready`) and organized:
**art that only one attack uses lives in that attack's folder**
(e.g. khalid's sit in `khalid/attack/ora_ora/`); art reused across a
character's effects goes in that character's `shared/textures/`; art reused across
*entities* goes in the top-level `vfx/shared/textures/`.

## Particles (frame-indexed VFX)

Extra 2D particles layered over the drawn sprites — e.g. soft embers on top of
the character's effects — driven entirely by data. `script/particle_director.gd` is a child of
the player; it watches the sprite and emits at authored positions during authored
frames. Adding an effect is a scene + a table row, no code.

**Three pieces:**

1. **Particle types** — scenes under `vfx/character/<id>/…`. A type's root may be a
   single `CPUParticles2D`/`GPUParticles2D`, a `Node2D` bundling several as one
   composite attack, a `Projectile` (a shot), or a `Strike` (a slash / blast / ground
   AoE). A `Strike`/`Projectile` carries its own `Hitbox`, fed the hit's numbers from
   the `Action`'s `hit` (`configs/actions_<char>.gd`) at spawn (see the damage section below) — nothing is baked here.

   **How a scene is referenced.** A row names its scene with **`preload("res://…")`** in
   `EmittersCharacters.TABLE` — so it's validated at parse time and resident before the game
   runs (no runtime `load`, no folder-scan index). Reusable building blocks are declared once as
   named consts and referenced across rows (e.g. `WIND_STREAKS`). Files still live under
   `vfx/character/<id>/…` / `vfx/shared/` — that's just where they sit on disk; the table points
   at them by path.

   `script/build_particles.gd` scaffolds a starter scene (it **skips files that
   already exist**, so it never clobbers editor tweaks); shared textures come from
   `script/gen_particle_textures.py`.
2. **Config** — `EmittersCharacters.TABLE` (`vfx/config/EmittersCharacters.cs`), keyed
   `character -> animation -> EmitterDef[]`. A row is a typed **`EmitterDef`** record
   (`records/vfx/EmitterDef.cs`): `new EmitterDef(scene, pos) { Mode, Frames, AllFrames, ConformToGround,
   Follow, Configure }` — the scene and position are required, the rest default to a plain burst. The fields are
   described below under their old short names (`scene`, `mode`, `frames`, `pos`):
   - `scene` — a `preload`ed scene whose root is a single `CPUParticles2D`/`GPUParticles2D`,
     **or a `Node2D` bundling several** as one composite attack (the director drives
     all of them, and mirrors the composite by flipping `scale.x`, so its child
     positions flip too; a single-particle root mirrors `direction`/`gravity` instead,
     keeping its texture). Note: a `CPUParticles2D`'s individual particle **textures**
     do not reliably h-flip under a node scale flip — a directional *drawn* slash
     reads cleanest as a **`Strike`** (`scripts/combat/strike.gd`): a `Node2D`
     bundling a `Sprite2D`/`AnimatedSprite2D` (which *do* mirror texture + rotation
     under the scale flip) + a `Hitbox`. It briefly grows+fades its visuals and frees
     itself, and the director accepts it and arms its hitbox like any burst (see
     khalid's `attack_ora_ora`). Layering separate scenes (several `{…}`) still works.
   - **Per-hit split files (how multi-hit attacks are authored).** Give each hit
     its **own** scene file named `<effect>_<sheetframe>.tscn` (frames it fires on,
     `_`-separated: `_3`, `_1_2` for two frames sharing one look, and a bare name =
     every frame). Each file is a self-contained `Strike` + `Hitbox` + its particles,
     wired as an ordinary row (the whole file fires). This is
     what khalid's `attack_twin_reaper` uses — `attack_twin_reaper_3.tscn` …`_9.tscn`.
     Each hit's emitters **edit in isolation** (open the one file — no overlapping-emitter
     soup), with no cross-hit subresource duplication, and the director spawns exactly the
     file it needs. Frames live in the config row.
     **What does NOT split:** the `_<frame>` convention is only for **frame-scheduled
     multi-hit** effects. A **single-emission** effect (bakshen, ground_breaker, frenemy)
     is already one file — leave it bare-named. An **event-fired** effect (the dashes,
     double-jump — fired by `fire_effect()`, not an animation frame) also stays one scene;
     where it needs a following part *and* a lingering part (the crimson vortex's follow
     `Trail` + lingering `Vortex`), that split is handled **inside** the scene by the
     `Trail` node-name convention (below), not by separate files. Splitting only kicks in
     when one move fires distinct groups on distinct **frames**.
   - `Configure` — *optional* typed hook run on the spawned node, for the rare row that must
     change the instance it spawns (Cherry Shots' last shot turns on homing through
     `EmittersCharacters.HomingShot`). A different LOOK is a different scene, not a hook.
   - **Composite child positions** — in a `Node2D` composite, the director only
     positions the *root* (at `pos`); each child particle keeps the local position
     you authored inside the scene. So lay one child at the feet `(0,0)`, another
     at the head `(0,-42)`, a hand-spark at `(14,-20)`, and `pos` anchors the whole
     cluster. `x` offsets mirror with facing; `y` (head/feet height) does not —
     author facing right.
   - `mode` — **sustained** (emit while any listed frame is on screen; the fire
     trail) or **burst** (one-shot each time a listed frame is entered; impacts,
     footfall dust). A **sustained** effect stays parented to the player, so it
     (and any hitbox) **follows** him — right for a body-attached aura/jet. A
     **burst** is **anchored in the world** at the spot it fires and stays put as
     he moves on — right for a blast/detonation.
     - **Code-triggered bursts** — a `burst` keyed under an animation name the sprite
       never plays won't auto-fire on a frame; call `ParticleDirector.fire_effect(key)`
       to spawn it on a game event instead. That's how the **double jump** works: its
       effect lives under `double_jump` and `player.gd` fires it only on the air jump
       (so the ground jump stays silent). See the double-jump note in the root README.
   - `frames` — **sheet-relative** indices (same numbering as `loop_from` /
     `hit_frames`; the idle-reference frame counts). Converted to emitted indices
     via the `sheet_start` SpriteFrames metadata. Or set **`AllFrames = true`** —
     it expands to *every* frame of that animation, so a whole-animation effect (an
     idle aura, say) needs no frame list and never breaks when the frame count
     changes.
   - Multiple emitters can share an `animation` — list several `{...}` and they all
     play together (that's how you layer several particle scenes at once).
   - `pos` — `[x, y]` pixel offset from the sprite origin (the feet), for facing
     right; auto-mirrored when facing left.
   - **One scene per look and per power level.** A row cannot rescale a scene's particle
     count / speed / size: a stronger version of an effect is its own scene. (The old `boost`,
     `set` and `node` row keys were removed on 2026-10-09 — no row used `boost` or `node`.)

   > **Sounds are NOT here.** This config is particles only. Frame-synced hit sounds live in the
   > parallel **`SfxCharacters.FRAMES`** (same `anim → {frame: cue}` shape, sheet-relative) and are
   > played by the same director in `_refresh`, symmetrically to the bursts. See the Audio section
   > in the top-level README.

   **Author every effect facing right.** The director mirrors the whole thing
   when the character turns: `pos.x`, and for `CPUParticles2D` also `direction.x`
   and `gravity.x`. (`GPUParticles2D` keeps those on a shared
   `ParticleProcessMaterial` which must not be mutated, so it — and a `Strike`
   slash — falls back to flipping the node's `scale.x`, which mirrors a drawn
   texture cleanly.)
3. **Director** — instantiated by `player.gd` at runtime (not in the editor).
   Rebuilds its emitters (and its scene index) on character swap, so switching away
   for a character removes its effects cleanly.

### Making an attack deal damage — a `Strike`/`Projectile` + its `Hitbox`

An attack effect carries its **own `Hitbox`**, and its **numbers come from the `Action`'s `hit`
(`configs/actions_<char>.gd`)** (fed in at spawn), while its **shape/position is authored in the
scene**. Code owns the numbers (clobber-safe, buffable); the editor owns the geometry.

1. Make the effect scene's root a **`Strike`** (`scripts/combat/strike.gd` — a slash /
   blast / ground AoE) or a **`Projectile`** (`scripts/combat/projectile.gd` — a shot),
   and add a **`Hitbox`** child (`Area2D` + `scripts/combat/hitbox.gd`) with a
   `CollisionShape2D`. Author the shape/position; the layers are set for you from the
   node's `hostile` flag. `attack_chainsaw.tscn` and `special_poison_raiser.tscn` are
   worked examples.
2. Put the **numbers** (`damage` / `knockback` / `stun` / `color`, and for a `Strike`
   the `extents`/`x` reach + `lunge`/`super_armor`/`multi_hit`) in the action's `hit.segments`
   in **`configs/actions_<char>.gd`** — NOT on the `Hitbox`. The player resolves them via
   `resolve_tuning()` (the buff seam) into `_active_hit`, and the director's
   `_inject_tuning` passes them to the node's `apply_tuning()` when it arms the box. Any
   value left on the `Hitbox` is a fallback used only when the action's `hit` omits it
   (or is null — the cherry_shots case, where two shots carry their own per-shot damage).
3. The **`ParticleDirector` arms it**: on spawn it sets `source` and switches the box on
   while the effect is emitting (a `sustained` effect's frames, a `burst`'s life). One
   activation = one hit per target (the `Hitbox` dedupes); `multi_hit` re-arms it. The
   box auto-mirrors with facing.

An effect with no `Hitbox` (an aura, a run trail) is unaffected — the director only
arms boxes it finds. There is **no built-in player attack box** any more; every attack,
for players and enemies, is one of these spawned nodes.

**Keep a ground blast on its platform** — add `"clip_to_ground": true` to a `burst`
entry and, at spawn, the director rays straight down through `L_WORLD`, finds the
platform's edges, and clamps the blast's **rectangular emission band and its
hitbox** to them. The clip is asymmetric — only the side hanging over the ledge is
cut. Rectangle emission + rectangle hitbox only; off the edge of everything it just
isn't clipped. Lenny's `special_poison_raiser` and the slams use it.

### `Local Coords` — the one setting that surprises people

Per particle scene, it decides whether the effect **trails** or stays **attached**:

- **Off** (world space) — particles are released into the world and left behind.
  Good for embers/smoke trails. But the emitter is moving with the player, so a
  low-velocity plume smears backwards into a diagonal. This does *not* show in the
  editor preview, where the emitter is stationary.
- **On** (local space) — particles keep the shape you authored and move with the
  player. Matches the editor preview exactly. Good for attached jets/auras.

To get a trail *and* a straight plume, keep it off and give the particles enough
`initial_velocity` that their own motion dominates the player's ~160 px/s.

> **`Local Coords` also gates texture mirroring.** A world-space (`Local Coords` off)
> particle renders decoupled from its node, so a `scale.x` flip never reaches it and
> an **angled texture won't mirror**. If a particle's texture must flip with facing,
> either turn `Local Coords` **on**, or (for a directional drawn effect) use a
> **`Strike`** `Sprite2D` — which mirrors cleanly under the flip — instead of a
> particle. A world-space particle can only ever mirror its *motion*, never its
> texture.

> Soft glowy particles clash with crisp pixel art. Prefer a hard-edged texture,
> **nearest** filtering, **normal** blend, and colours sampled from the drawn art,
> so effects read as pixel art. Keep new types in that style unless a soft glow is
> genuinely wanted.

### Effects DISSIPATE, they don't pop

Two rules keep particle effects from vanishing mid-air (the abrupt cut you get if an emitter
node is freed while its particles are still alive):

- **A `Strike` fades itself out.** At end-of-life (`_fade_out`) it deactivates its hitbox and
  sets `emitting = false`, then frees only after the longest particle `lifetime` — so a
  **continuous** emitter tapers off instead of being cut. (A `one_shot` burst already fades on
  its own; this also covers continuous ones, so you don't *have* to set `one_shot` for a burst,
  though it's still the cleaner choice — all the particles at once, then gone.)
- **An effect parented to a mob dies with the mob** unless you retire it. An enemy trail is a
  *child* of the enemy, so when the enemy frees, the trail (and its airborne particles) go with
  it. `Nodes.retire_particles(node, into)` fixes that: it re-parents the emitter into the level
  (keeping its world position), stops emission, and frees it after the particles fade — so the
  trail lingers and dissipates after its owner is gone. Ein's `_set_trail` uses it.

### Adding a new attack effect — where things plug in

Pick the layer by *what the effect is*:

| Goal | Add it to | Code? |
|---|---|---|
| A **visual** on chosen animation frames (spark, trail, drawn slash) | `EmittersCharacters` (+ a scene under `vfx/character/<id>/…`) | none |
| A hit's **damage / knockback / reach** | the action's `hit.segments` in `configs/actions_<char>.gd` | none |
| A **thing spawned** on a hit, or custom behavior | a `CharacterAbility` script (`scripts/abilities/`) | small |

1. **Frame-indexed particle (no code).** Make/obtain a scene under
   `vfx/character/<id>/<category>/<name>/<name>.tscn`, then register it in
   the `Emitters` config: `<id> → <animation> → [{type, mode ("sustained"|"burst"),
   frames (sheet-relative), pos ([x,y] from feet, auto-mirrored)}]`. Done.
2. **Tune the hit (no code).** The action's `hit.segments` in `configs/actions_<char>.gd` — `damage`,
   `knockback`, `stun`, and hitbox `x`/`extents` (an array = one entry per combo
   segment). The effect keys off the action's `animation` name in the `Emitters` config.
3. **Spawn something / new behavior (a `CharacterAbility`).** Create
   `scripts/abilities/<id>.gd` extending `CharacterAbility` (auto-equipped) and
   override a hook: `on_special_strike(player)` (the moment the special connects),
   `physics(player, delta)` (a per-frame movement/attack override), or
   `setup(player)` (one-time on equip).

## Enemy attack VFX

Enemy effects live under `vfx/enemy/<id>/attack/` (and `…/other/`) and are loaded **by path**,
not the director index. Which scene each enemy emits — and where — comes from
**the `Emitters` config** (see below), keyed by `enemy_id`. `enemy.gd` reads the
`projectile` entry and spawns a **hostile `Projectile`** (the same `scripts/combat/projectile.gd`
players use — team via the `hostile` flag) with that scene as its visual + a `Hitbox` built from
the enemy's ranged tuning; a `lob` reads `delayed_projectile` (the thrown object) and
`delayed_projectile_burst` (the blast). Enemy **melee** is a hostile **`Strike`** spawned the same
way. **Keys are the attack's STRIKE TYPE** (`enums/combat/StrikeType.cs`: `projectile`,
`delayed_projectile`, `aoe`, `delayed_aoe`, `kamikaze`, `blast`, `tackle`, …), a component appends `_burst` / `_trail`, and a
passive movement trail is `<state>_trail`. Two ranged examples:

(Every scene named below is wired through the `Emitters` config, not a script/roster field.)

- **Baghel** `projectile` = `attack/baghel_projectile.tscn` — a `CPUParticles2D` ground surge
  (the `forward` ranged mode, with a scorched `ground_trail`).
- **Kebus** `projectile` = `attack/kebus_projectile.tscn` — an aimed staff bolt: an ember-trail
  `CPUParticles2D` + a soft glow `Core`. (An enemy with no `projectile` scene gets the built-in orb.)
- **Mazab** (`far_mode = FarMode.Lob`) uses **two** config entries for its thrown bomb: `delayed_projectile`
  = `attack/mazab_delayed_projectile.tscn` (a steel-blue glowing `Core` + short dust trail — a `LobProjectile`
  spins it as it arcs) and `delayed_projectile_burst` = `attack/mazab_delayed_projectile_burst.tscn` (a one-shot radial shard
  burst + ground dust, instanced inside the explosion `Strike`, not on the projectile). A lob has
  **no in-flight hitbox**; only the explosion `Strike` deals damage.
- **Ein** (the floating kamikaze, `scenes/diver_enemy.tscn`) has config entries `kamikaze_trail` =
  `attack/ein_kamikaze_trail.tscn` (a hard cyan→red charge streak) and `kamikaze` =
  `attack/ein_kamikaze.tscn` (the arrival burst, inside the explosion `Strike`). A
  `walk_trail` entry (a gentle drift trail under `other/`) is optional — omit it and he
  patrols with no trail.
  The trails are `local_coords = false` so they rake out behind the orb as it moves.

### Enemy emitters — `EmittersEnemies.TABLE` (THE one place)

Every enemy's particle emitters live in one table, `vfx/config/EmittersEnemies.cs` — the enemy
counterpart to `EmittersCharacters`, but simpler: enemy effects are attached in **code** by
state/event (a trail worn while patrolling, a blast on arrival, a projectile visual on the shot),
not fired on animation frames, so there's **no frame scheduling** — only *which* scene and
*where*. Keyed `enemy_id → effect → new EmitterDef(scene, pos)` (the same typed record as the character table):

- **`scene`** — the preloaded scene to emit. The table is **authoritative for presence**: delete a
  row (or clear its `scene`) and the enemy **stops emitting it entirely** — no code change. (That's
  why removing `ein → walk_trail` actually removes the walk trail.) An absent visual doesn't
  stop combat — an AoE's hitbox or a projectile still fires, just with no particle look (a shot
  with no scene falls back to the built-in orb).
- **`pos`** — offset from the sprite origin at the feet, facing right; **auto-mirrored** on x by
  facing. For a **projectile**, `pos` is the **muzzle** it launches from.

The enemy reads it via `Enemy._vfx_scene(effect)` (a `PackedScene` or null) / `_vfx_pos(effect,
fallback)` / `_make_vfx(effect)` (instantiate + position, or null if no scene) — all going through
`Emitters.enemy_effect(id, effect)`. Effect keys today: `ein → attack_trail / explosion`, `nasen →
rage`, `kebus/baghel → projectile`, `mazab → projectile / explosion`. Add an enemy or effect row
to give it a look/position with **no code change**. (This owns the *visual*;
an AoE's *hitbox* size/offset stays a combat `@export` like `explosion_offset` / `rage_extents` /
`lob_explosion_extents`, and combat behavior like `far_mode` stays in the roster.)

## Build tools

Run from the project root. All are idempotent scaffolds — they never clobber
hand-tuned scenes.

| Command | Purpose |
|---|---|
| `python3 vfx/script/gen_particle_textures.py` | Regenerate the shared textures (`vfx/shared/textures/*.png`) |
| `godot --headless --script vfx/script/build_particles.gd` | Scaffold particle-type scenes (skips existing) |
| `godot --headless --script tools/gen_effect_frames.gd` | Slice drawn effect strips → SpriteFrames (below) |

### Drawn projectile animations (an `AnimatedSprite2D`, not particles)

When you want a projectile to play a **hand-drawn frame animation** (a ring forming
and flying, say) instead of a particle emitter repeating one texture, build it as a
`Projectile` that carries an `AnimatedSprite2D`:

1. Export the projectile as a **horizontal strip** named `<name>_anim.png` into the
   attack's folder (e.g. `character/khalid/attack/ora_ora/ora_ora_anim.png`).
   These drawn effect sheets are authored in the art repo.
2. Run `godot --headless --script tools/gen_effect_frames.gd` — it finds every
   `*_anim.png` under `vfx/` and slices it (128px frames by default, or set a count
   in the tool's `OVERRIDES`) into `<name>_anim.tres`, a `SpriteFrames` with one
   `default` animation.
3. In the projectile scene, make the root a `Node2D` with **`projectile.gd`**, and give it
   an `AnimatedSprite2D` child (`sprite_frames` = the `.tres`, `autoplay = "default"`)
   plus a `Hitbox` (anywhere — `projectile.gd` finds it by search, not a fixed path).

The `ParticleDirector` fires it like any other `burst` — it accepts a `Projectile` even
with **no particle emitters**, since it carries its own visual and manages its own
life. Facing is by travel direction: the shot rotates to its heading, so author the
strip pointing **right**.

**`Projectile` exports** (`scripts/combat/projectile.gd`): `speed`, `homing` (steer rate; 0 = fly
straight), `max_range`, `acquire_range`. It locks the **nearest enemy ahead in the facing
direction on the same level** — "same level" is a `±vertical_reach` band (default 40px, under
the platform spacing), so a homing shot **won't dive at someone a platform below just
because they're closer in x**; nearest is measured by horizontal distance among that band.
Set `can_fly_up = true` to drop the band (and let it steer upward). If the target **dies
mid-flight** (or none was ever in range), the shot **stops homing and flies straight along
its launch heading** — so a trailing shot keeps going and can hit an enemy behind the dead
one, or expires, instead of veering off on a stale curve. `impact_effect` (a `PackedScene`)
spawns a one-shot effect at the point of contact when it hits, then self-frees; leave empty
for none.

**End / dissolve animation (`end_frames`).** So a shot that reaches `max_range`
*without hitting anything* dissolves instead of blinking out, give it an `end_frames`
`SpriteFrames`. On expiry the shot freezes, switches off its hitbox + any particle
trail, swaps its `AnimatedSprite2D` to `end_frames`, and frees when that animation
finishes. To make one:

1. Draw the dissolve as a horizontal strip named `<base>_end_anim.png` (the `_end_anim`
   suffix makes `gen_effect_frames.gd` slice it **non-looping** — a dissolve plays
   once). Drop it in the attack's folder beside the fly strip.
2. Run `gen_effect_frames.gd` → `<base>_end_anim.tres`.
3. Point the projectile scene's `Projectile.end_frames` at that `.tres`
   (`attack_ring_kiss.tscn` is the worked example).

This is expiry-only — a hit still uses `impact_effect`. Leave `end_frames` empty to
keep the old blink-out.
