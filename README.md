# mygamedev

> **✅ C# migration complete.** The game is fully C# (only build/QA `.gd` tools remain), with a typed-data pass
> (enums/records/ids). Run/verify with **`godot-mono`**, not `godot`. History + interop rules:
> [`docs/csharp-migration.md`](docs/csharp-migration.md).
>
> **🔭 Run-loop pivot (design-frozen, NOT yet built).** The game is moving to a new **Fissure / Seal / Warden**
> arena loop (three Wardens that charge until you seal their Fissures, then hunt you — **no main boss, no
> greed**) that supersedes the reward-door / 5-level loop this README describes. Source of truth:
> [`docs/game-loop.md`](docs/game-loop.md) + [`docs/buff-catalog.md`](docs/buff-catalog.md). Until it's
> implemented, the sections below describe the **current (old-loop) code**.

A 2D pixel-art action platformer in **Godot 4.7**. A character-agnostic player controller
drives the playable character. This repo ships **Khalid only** — four other characters were
parked in the gitignored `playground/` directory (for a future separate repo); the engine
stays fully character-agnostic, so bringing one back is just its assets + data rows.


Main scene: `scenes/palette_preview.tscn` (the pre-game colour pickers) — press F5; picking a look loads the run scene `scenes/arena.tscn` (root `RunManager`). To jump straight into a run, open `scenes/arena.tscn` and press F6.

**Game premise & the run loop:** *(mid-migration toward the pivot — see the banner above +
[`docs/game-loop.md`](docs/game-loop.md))* see [`docs/game-design.md`](docs/game-design.md) — a roguelite
arena crawler. **Levels/exits are retired:** it's now ONE endless arena where enemies **trickle in at a
steady rate** from a mixed roster and you survive. Cast **specials** (now **free and unlimited**), and
spend **Ruh** on your **Aegis** surge for an on-demand burst of invincibility (each use costs one
**Ruh** charge — you start a run with 3, and refill Ruh by **landing hits**; Ruh is the only gate, no
cooldown). Killing enemies drops **Fada Figs** + (at a ramping chance) a random **buff** you grab off the
ground. Attack is chosen at run start and locked; die and the run restarts. *(The old reward-door / next-level
machinery is parked, not wired — see `scripts/run/README.md`.)*

> Potential names for the game:
> - Index32
> - Way of All Flesh

---

## Layout

```
assets/portraits/     Painted 1080x1080 character portraits (HUD art)
configs/              Tuning DATA -- attack table, layers, feel, rosters (see configs/README.md)
helpers/              Shared static utilities -- boxes, node lookup, anim meta (see helpers/README.md)
vfx/                  All visual effects -- particles + drawn slashes (see vfx/README.md)
resources/characters/ GENERATED SpriteFrames -- do not hand-edit
resources/enemies/    GENERATED enemy SpriteFrames -- do not hand-edit
scenes/               player, level, hud
scripts/              player, hud
scripts/run/          the roguelite run: continuous arena, steady spawner, Ruh, buff drops, attack picker (see scripts/run/README.md)
scripts/abilities/    Passive/Buff base (C#) + reward passives (Leech/ParryMend/ReaperEdge.cs) + reward-tier/trigger types (RewardTypes.cs)
scripts/combat/       Hurtbox, hitbox, Combatant base, health bar, floating text, status overlay — all C# now (constants -> configs/Combat.cs)
scripts/enemies/      Enemy base + projectile
sprites/characters/   Source pixel-art sheets, one folder per character
sprites/enemies/      Source enemy sheets, one folder per enemy
tools/                Generator + verification scripts (not shipped)
```

## Controls

| Input | Action | Notes |
|---|---|---|
| A / D | `move_left` / `move_right` | |
| S / ↓ | `drop` | Tap to fall through the one-way platform you're on (ground only; a no-op on solid floor). Controller: D-pad down / left-stick down; remappable in the Input Map |
| Space | `jump` | Press again in the air to **double jump** (`max_air_jumps`) — the air jump re-boosts and spawns the character's jump particles; the ground jump is silent |
| Shift | `dash` | Has a cooldown. **Dash into a launch orb** and it magnets you through and flings you up + forward (see Launch orbs) |
| Left mouse | `attack` | The current *attack* — each press advances the combo (or, for a `"flurry"` attack like Khalid's, **hold** to keep punching). **Ground only** by default — an attack whose Action is tagged `"air"` (e.g. Zahluq) is the exception and can be used mid-air (`Player._air_attack_ok`) |
| Right mouse | `special` | On the ground: the current *special* (committed full-animation move) — **free, no Ruh cost**; most have only a tiny anti-spam lag, though a strong one can set its own cooldown (**Come Closer = 3s**). **In the air: performs the ground slam instead** (characters with a `slam` sheet) |
| Ctrl (RT / R2) | `surge` | Fires the equipped **Surge** — a passive ability (**Aegis** = ~5s invincibility; **Jnoon** = ~5s ×2 damage dealt / ×0.5 taken; **Asra** = ~5s ×2 move speed) applied *without* interrupting your attacking/moving — **except Nem**, a committed sleep that locks you in place and heals ~50% HP over 5s (a hit wakes/cancels it), and **Wara**, which *arms* and waits: the next enemy hit is negated and AoE-stuns everyone near you (2s). **Spends one Ruh charge per use** — Ruh is the only gate, no cooldown. RT on the pad because dash owns LT |
| Z / X | `debug_damage` / `debug_heal` | Dev only |
| 0 | `debug_respawn` | Dev only — rebuild the current level fresh |

Bound to **physical** keycodes, so they stay in the same place on AZERTY/Dvorak.
Rebind under `Project > Project Settings > Input Map`.

**Facing follows movement.** The character faces the direction it last moved
(A/D or the stick), and **attacks/specials strike that way** — where the character
is actually facing, controller-friendly. Standing still keeps the last facing; the
mouse cursor does **not** steer facing or aim (a previous mouse-look experiment was
removed). If you want cursor-aim back for keyboard+mouse without breaking controller
play, the clean way is "last input device wins" — ask and I'll wire it.

**Launch orbs — magnet traversal (`State.LAUNCH`).** Levels place **launch orbs**
(`scripts/things/LaunchOrb.cs`, `LaunchOrb`) — levitating orbs above/between platforms. **Dash into (or
near) one** and it acts as a **magnet**: it sucks Khalid through and flings him out the far side with the
orb's **own set impulse** — a strong **up + forward** along his facing (`SwingOrb.launch_up` /
`launch_forward`). He keeps brief dash i-frames and **air-dashes** the rest of the way to the next
platform. Fully automatic — no aiming, no pumping. (This replaced an earlier controllable-swing design.)

- **Reliable trigger.** Capture is checked at the dash **press** *and* every dash frame, over a generous
  body radius (`LAUNCH_PULL_RANGE` ~96px) — the press check matters because Khalid's default dash is a
  **blink/teleport** that could otherwise skip past the orb. On capture he's magneted to the orb over
  `LAUNCH_MAGNET_TIME` (i-frames on), then flung; `LAUNCH_CD` blocks an instant re-trigger. Tuning is the
  `LAUNCH_*` consts at the top of `Player.cs` plus each orb's `launch_*` exports.
- **The orb is dumb by design** (`LaunchOrb`). It bobs and joins the `"orbs"` group; the Player owns the
  range test (`_orb_in_pull_range`, the way it scans `"enemies"`), drives which orb is lit (`set_near`),
  and owns the magnet/launch. Adding one to a level is a single `Vector2` in that level's **`orbs`** list
  (`scripts/run/Levels.cs`); RunManager instantiates them in `_build_level`.
- **The orb reshades to the power palette AND glows.** Its red art is baked into its *texture* (which
  `VfxPalette.RecolorTree` can't touch) **and is dark** (luma ~0.19 — a hue-swap alone rendered
  near-black). So it wears `vfx/shaders/thing_recolor.gdshader`, which paints every pixel a single
  **`tint`** (set to the family colour via `VfxPalette.Recolor`) scaled by the baked pixel's brightness,
  times a **`glow`** lift so it blooms — same simple shape as the proven `enemy_glow` shader (no hsv, no
  `COLOR` multiply, which is what a straight canvas shader renders reliably). A `shine` uniform adds the
  proximity glow. Net: pick teal powers and the orb glows bright teal.
- **SFX live under `things/` (not `character/`).** Two cues in **`SfxWorld`**, both placeholder:
  `launch_orb` — the ambient hum it emits **on loop** (positional, via the new `Sfx.make_loop_2d`, parented
  to the orb) — and `launch_orb_use`, the one-shot when Khalid uses it (`LaunchOrb.play_use`). Files:
  `sfx/things/traversal/launch_orb/`.
- **Level layout.** Level 1 (*The Shallows*) is a large testbed: small HIGH platforms ~800px apart over a
  wide floor (falling just drops you to the ground). The `khalid_swing_frames` sheet is currently unused
  (the swing animation was discarded); `State.LAUNCH` plays the `dash` anim through the magnet.

**Which character you play is chosen in code.** In-game Q/E switching is **gone** — set the
**`START_CHARACTER`** constant near the top of `scripts/run/RunManager.cs` to any id in
`CharacterConfig.IDS` (`khalid` — the others are parked in `playground/`). The dev keys (Z/X
debug damage/heal, `0` rebuild-level) live in that same file.

**The game is a roguelite run** (premise: [`docs/game-design.md`](docs/game-design.md)). At run start
you **pick an attack** (locked for the run; scrollable picker built to scale to 12+). You drop into
a low, mostly-horizontal **endless arena** that **trickles enemies in at a steady rate** (a mixed roster,
proximity-spawned near you — capped so it never floods). You **start each
run with 3 Ruh charges** — the surge meter, shown in charges (100 each), no decay — and refill it by
**landing hits** (~5 hits = 1 charge; kills don't count, and a special's own hits don't self-pay).
**Specials are now free and unlimited** (only a tiny anti-spam lag). Ruh instead fuels the **Aegis
surge** (a passive on its own button): it grants ~5s of invincibility on demand without interrupting
you, and **each use spends one Ruh charge** — Ruh is the only gate, no cooldown (see below). **HP is separate**: damage hits it only, heals
*only* from buffs. **Killing an enemy** always drops **Fada Figs** (the run currency) and, at a chance that
**ramps from 40% up to a 70% cap** as the run wears on, a **`BuffDrop`** — a glowing orb you touch to gain a
random generally-useful buff (tier weighted low; move-gated buffs stay out of the drop pool so a pickup is
never wasted). Moves are independent — they **upgrade by layering buffs**, not by turning into a different move
(Dual Executioner & Redere Frisbee are now standalone swaps, not successors). Take 0 HP and the run restarts
(a fresh arena; buffs cleared, HP + Ruh refilled). All of this — the spawner, the enemy roster, the buff pool,
the attack picker — lives in [`scripts/run/`](scripts/run/README.md) (`RunManager` is `arena.tscn`'s root;
`EnemyKits` is the roster, `BuffDrop` + `BuffCatalog` the drops; the old `Levels` / `Rewards` / `RewardsCatalog`
/ `Build` reward-door code is **parked, not wired**, kept for the pivot's reward phase). The `.tscn` stays minimal because the editor
clobbers it, so the level content is built in code from that data. The **look** of the terrain itself is the
hand-painted `TileMapLayer` in each stage layout (see the run README); the old procedural tileset/plant/tree
"skin" is **retired**, and `configs/Terrain.cs` now only holds the backdrop config.
The **background** (`RunManager.BuildBg`/`LayoutBg`, on a `-100` CanvasLayer) is a **single** star image
(`assets/terrain/stage1/bg1.png`, no tiling) centred and scaled to `Terrain.BackgroundZoom` of the viewport
(**1.0 = fills**, lower = zoomed out a little, over a dark backing sampled from the image's own edge so the
gap never reads as a cut). Over it sits an optional **animated element** — an orbiting planet
(`stage1/planet_moon.png`, a 10-frame 48px strip) placed by `BackgroundAnimRatio` *within the image's rect*
and scaled to match it. Both live in `Terrain.cs`, re-layout on viewport resize, under the per-level colour
tint (`BackgroundTintAlpha`).

**Fada Figs** — the collectible run currency (the Chest spends these; the old "Lira" idea is retired). Every enemy
drops some on death: `RunManager.OnEnemyDied` → `SpawnFadaFigs` scatters `Enemy.fada_fig_drop` copies of
[`scenes/fada_fig.tscn`](scenes/fada_fig.tscn) ([`scripts/collectibles/FadaFig.cs`](scripts/collectibles/FadaFig.cs), a
`RigidBody2D`) which pop out, bounce/tumble, and settle on the terrain. The player collects one by **physically
touching** it — the Fada Fig's child `Pickup` Area detects the `PlayerBody`, calls `Player.collect_fada_fig`, plays
`fada_fig_collect` (placeholder sfx), and frees. `FadaFig.magnetize(target)` is a ready hook for a FUTURE reward that
makes loose Fada Figs fly to the player like a Ruh soul (not wired yet). Drop counts default by advisory
`EnemyTier` (`RunManager.FadaFigsForTier`: Chip 1 / Mid 2 / Strong 3), overridable per kit — **Wardens drop 12**
(`KROJ` kit). The banked total shows on the HUD next to the Ruh meter (`HUD.SetFadaFigs`); `Player.begin_run` zeroes it.
The sprite wears a shared **`vfx/shaders/world/pulse_glow.gdshader`** material (assigned in `FadaFig._Ready`, one
instance for all Fada Figs) that breathes its brightness above 1.0 so it **blooms** — reads as an energy mote and pops.

---

## The sprite pipeline

This is the part worth understanding, because the source sheets are irregular.

### The problem

Each character has up to several sheets (`idle`, `run`, `jump`, `fall`, `land`,
`dash`, `slam`, `attack`, `special`, `death`, `spawn`) — `fall`, `land`, `slam`, `death`,
and `spawn` are optional, so a character without that sheet just can't do it (the mechanic
no-ops for them; no `death` sheet → respawn instantly, no `spawn` sheet → appear instantly
— see below). They are single-row grids, but nothing else is consistent:

- Frame counts vary (2-13+) between animations *and* between characters — the
  slicer (`frame_count`) auto-detects the count, up to a generous cap, so long
  combo/dash sheets (e.g. a 13-frame dash) work without configuration
- Frame sizes vary wildly — khalid's idle is 32x32, his attack is 143x48
- Some sheets have a constant horizontal padding bias (a dash can sit
  ~24px left of centre)

Slicing them naively makes the character jump sideways and change height every
time the animation changes.

### The fix

`tools/gen_spriteframes.py` analyses each sheet and normalises every frame onto
one shared canvas (**currently 128x80**):

- **Vertically** — the frame bottom becomes the canvas bottom. The sheets are
  foot-anchored, so this puts feet on a fixed line.
- **Horizontally** — anchored on **frame 0**, the static idle-reference pose
  present at the start of every sheet (see "Frame 0" below). Its bounding box
  matches the idle pose, so anchoring on it aligns every animation to idle.
  Anchoring on the average instead would let a dash's fire trail or an
  attack's swing arc drag the body off-centre. Later frames keep their own
  offsets, so lunges still lunge.

### Palette cleanup

The character sheets were AI-generated and downscaled to ~32px, which smeared
every region into hundreds of near-identical shades (Khalid's idle sheet had
**1595 unique colours across 1795 opaque pixels**) and wrapped everything in a
muddy black antialiasing rind.

That cleanup is **not done here** — it belongs to the art pipeline, because the
`.aseprite` masters are the source and this repo only consumes the exported
sheets. It lives in the art repo:

    mygame/tools/repalette/          <- scripts + full README

It collapses each sheet onto a hand-authored palette (6 materials x 5 shades +
a rim, 36 colours) and writes the result back into the `.aseprite` masters, so
the art repo's git hooks regenerate the GIFs and `frames/*.png` from it.

Nothing in *this* repo needs to run it. The only thing to know downstream is
that a re-palette changes the sheets under `sprites/`, so re-run
`gen_spriteframes.py` and re-verify after pulling one.

### Frame 0 is the idle reference

The **first frame of every sheet is a static idle pose**, included so the art
lines up with idle and giving the generator its alignment anchor. It is *not*
part of the action: for every animation except `idle`, the generator **drops
frame 0** and playback starts on the real first frame. `idle` keeps all its
frames (frame 0 belongs to it).

Consequences worth knowing:
- Frame indices in `OVERRIDES` / `HIT_FRAMES` are **sheet-relative** (they count
  frame 0); the generator subtracts 1 to get the emitted index the player sees.
- An action sheet needs at least 2 frames (idle-ref + one real frame).
- Anything tied to a specific played frame — e.g. a special's `WIND_UP` —
  is expressed in emitted indices and must be retimed if the layout changes.

Normalisation is stored as `AtlasTexture.margin`, so **no images are rewritten
and no extra VRAM is used** — the atlases still point at the original PNGs.

Because every character lands on the same canvas, swapping is a one-line
`sprite_frames` swap. No per-character offsets or colliders.

**The canvas size is derived, not fixed** — it grows to fit the widest padded
frame, so art changes can move it (156x71 -> 164x80 -> 128x80 so far).
`Player.cs` reads the frame size on load and sets the sprite offset from it
(origin at the feet), so nothing has to be updated by hand when it moves.

### Target frame size: 128x80

All 30 sheets are now **128x80**. The generator doesn't require uniformity (it
still handles mixed sizes), but standardising means every sheet shares a grid.

**Frame size alone doesn't shrink the canvas — centring does.** The canvas must
be wide enough to hold every animation once frame 0 is aligned, so a sheet whose
character sits off-centre in frame 0 forces padding on *every* character, so the canvas settles once each character's frame 0 is centred.

So the rule for new art is both halves:

1. 128x80 frames
2. **In frame 0, the character horizontally centred in the frame.** Later frames
   can lunge or trail VFX freely — only frame 0 is the anchor.

The generator prints the worst offenders whenever the canvas exceeds the widest
frame, so it's obvious which sheets still need re-centring.

### Regenerating

> **Replacing a PNG is not enough — you must regenerate.** The `.tres` files
> store hardcoded `region` rectangles (`Rect2(0, 0, 32, 32)` and so on). Swap in
> a sheet with different frame sizes and those rectangles now slice the wrong
> part of the image, so frames render blank or clipped. **Symptom: frames
> "disappear" on some actions after an art update.** Fix: re-run the generator.

```bash
python3 tools/gen_spriteframes.py                    # rewrites resources/characters/*.tres
godot --headless --script tools/verify_frames.gd     # asserts it all loads
```

Run the generator after adding a character or re-exporting any sheet. Frame
counts, sizes and canvas are all re-derived — nothing is hardcoded.

Animation speed and looping live in the `ANIMS` dict at the top of the
generator:

| Animation | FPS | Loops |
|---|---|---|
| idle | 6 | yes |
| run | 10 | yes |
| jump | 10 | no |
| fall | 10 | yes |
| land | 12 | no |
| slam | 12 | no |
| dash | 12 | no |
| attack | 12 | no |
| special | 10 | no |
| death | 10 | no |
| spawn | 10 | no |

### Per-character timing

Frame counts vary enough that one fps makes some swings drag and others snap, so
`OVERRIDES` (just below `ANIMS`) layers per-character tweaks on top:

- **`fps`** — retime that one animation
- **`hold_last`** — multiply the final frame's duration, letting a pose land
  before the character retracts
- **`FRAME_DURATIONS`** (a separate config below `HIT_FRAMES`) — multiply the
  duration of *any individual frame*, `(char, anim) -> {sheet_frame: multiplier}`.
  Each frame normally shows for `1/fps`; `2.0` holds it twice as long, `0.5` half —
  so you can make a wind-up snap or a key pose linger without retiming the whole
  animation. Sheet-relative indices (idle-ref frame 0 counts), same numbering as
  `HIT_FRAMES`/`loop_from`. It's the general form of `hold_last` (which only
  targets the last frame); a value here wins over `hold_last` for that frame. Out-
  of-range indices are rejected. Godot stores it as each frame's `duration` in the
  `.tres`, so it survives regeneration. **Making a frame last N seconds:** the frame
  time is `multiplier / fps`, so set `multiplier = N * fps`. e.g. Khalid's `bakshen`
  charges for 1s on its wind-up frame via `fps 10` + `FRAME_DURATIONS {1: 10.0}`.
- **`loop_from`** — for a looping animation, the sheet frame the cycle restarts
  at. Frames before it play once as an intro; the tail repeats forever.
- **`loop_to`** — optional end of that loop (sheet frame, inclusive). Without it
  the cycle runs to the last frame; with it the loop is `loop_from..loop_to` and
  any frames past `loop_to` show only in the one-time intro pass. Lets a character
  loop a **mid-sheet range** — e.g. an idle plays 0-1 to settle in, then
  loops his 2-8 raise-a-flame flourish forever until you press something.

`loop_from` / `loop_to` exist because Godot's `loop` flag is all-or-nothing. The
generator writes the (emitted) indices as resource metadata; **it doesn't set the loop
flag** — the reader decides what to do with the range. For a **looping** anim `Player.cs`
jumps back to `loop_from` when the anim wraps (`_on_animation_looped`) or steps past
`loop_to` (`_on_frame_changed`). For a **non-looping** anim an enemy uses `loop_from` as
the frame a *re-played* attack restarts at (`Enemy._replay_from`), so a channel/rage's
wind-up plays once — that's why they no longer require `loop=True`. A run can use `loop_from`; an idle can use
both; Nasen's rage attack (an enemy) uses it non-looping. They're
sheet-relative (count the idle-ref frame 0), same numbering as `HIT_FRAMES`.

Special attacks are tuned toward a ~0.5s feel: khalid `hold_last` 2.5 (few frames,
so the last pose sits rather than the whole swing slowing). The generator prints resulting durations,
marks overridden entries with `*`, notes loop ranges as `[loop N-M]` and hit
frames as `[hits...]`.

### Attack hit frames — `HIT_FRAMES`

A separate config next to `OVERRIDES` maps `(character, "attack")` to the
**sheet-relative** frames that are combo hits. An attack plays one *segment* per
click, each segment ending on a hit frame, with the frames between hits animating
for smoothness (see **Player → Attack combo**). Any attack not listed defaults to
"every frame is a hit" — one frame per click, the older snap feel. Emitted as
`metadata/hit_frames`, read by `Player.cs`.

**Snappy timing (automatic).** For any character `attack_*`/`special_*` with hit frames, the
generator plays the **non-hit frames at `BETWEEN_HIT_MULT` (0.25×)** a hit frame's duration — so
the wind-up/in-between poses snap and only the hits (and any `hold_last` finisher/recovery) linger.
No per-attack config needed; per-frame `FRAME_DURATIONS` and an explicit `hold_last` still override
it, and enemy telegraphs are left untouched.

Configured so far (keyed by the move's animation, since a character has several),
with wind-up / in-between frames between the hits:

| Who | Animation | `HIT_FRAMES` (sheet indices) |
|---|---|---|
| khalid | `attack_spear` | `[6, 9, 13]` — thrust, thrust, big spinning finisher (his Elite swap) |
| khalid | `attack_bakshen` | `[3]` — one charged slash on the last frame (~1s wind-up) |
| khalid | `attack_zahluq` | `[2]` — the burst frame; the Strike + `lunge` slide fire here (dash-attack) |
| khalid | `attack_cherry_shots` | `[3, 7]` — two laser Projectiles: small bolt, then a bigger one |
| khalid | `special_ground_breaker` | `[6]` — the overhead ground crack |
| khalid | `special_stay` | `[3]` — a short stun blast (little dmg, 5s stun) |
| kebus (enemy) | `attack_melee` | `[3]` |
| baghel (enemy) | `attack_projectile` | `[6]` |
| nasen (enemy) | `attack_aoe` | `[2]` — the rage AoE erupts here |
| mazab (enemy) | `attack_delayed_projectile` | `[5]` — the lobbed bomb leaves his hand here |
| tarri (enemy) | `attack_blast` | `[3]` — the stationary blast erupts on the last frame |
| breski (enemy) | `attack_melee` | `[4, 9]` — a 2-hit melee combo: jab, then a heavier follow-up |

Specials list a strike frame the same way (keyed by the special's animation);
without one the special lands on its middle frame.

> **Indices, not frame numbers.** These are 0-based sheet indices, where index 0
> is the idle-reference frame. If you're counting frames 1-N in an image editor,
> subtract one. The generator errors if an index is out of range, which usually
> means the numbering slipped (or the sheet's frame count changed).

---

## Adding a character

1. Drop the six sheets in `sprites/characters/<name>/` named
   `<name>_<anim>_frames.png` (lower case).
2. Add a 1080x1080 portrait at `assets/portraits/<Name>.png`
   (**capitalised** — the lookup expects it).
3. Add the name to `CharacterIds` and the `[Export(PropertyHint.Enum, …)]` list in
   `scripts/Player.cs`.
4. Import in Godot (`godot --headless --import`) so the PNGs get UIDs, then run
   the generator and the verifier.
5. Optionally add a C# `CharacterAbility` (wired in `Player.CharacterAbilityFor`) — see **Character abilities**.

### Rules the art must follow

The generator adapts to any frame count (1-12), size, and padding. These four
things it assumes — and they **fail silently as misalignment, not as an error**:

1. **Frame 0 is the static idle-reference pose, no VFX.** It is the horizontal
   anchor *and* is dropped from action playback. If a dash's frame 0 already has
   the fire lit, that whole animation sits off-centre and the drop eats a real
   frame.
2. **Feet touch the bottom edge.** Trailing transparent rows make the character
   float.
3. **Single row.** Frame detection only divides horizontally.
4. **Up to ~32 frames** (the `frame_count` cap), and the naming above.

**Trailing blank frames are OK** (a sheet padded to a wider power-of-two than its real
count — e.g. 7 death frames in an 8-slot 1024px sheet). `frame_count` keeps the frame
width and trims the blank tail off playback. But an **interior** blank frame still breaks
detection (it reads as a gap, not padding) and merges frames — keep gaps out of the
middle.

Adding a new *animation type* (`hurt`, `death`, ...) means one line in `ANIMS`
and a matching case in `_animation_for()` in `Player.cs`.

---

## Player

`scripts/Player.cs` — a `CharacterBody2D` (extends the C# `Combatant`) with a small state machine
(`IDLE / RUN / JUMP / FALL / DASH / ATTACK / SPECIAL / LAND / SLAM / DEATH / SPAWN / HURT / SURGE / LAUNCH`).

**Movement stats live in typed config, not the inspector.** Every movement/physics knob is a
`Locomotion` (`configs/Locomotion.cs`, the shared baseline) attached to a **movement Action**
(run/jump/dash/slam), with per-character deviations in each character's `MOVEMENTS` catalog
(`configs/actions_<char>.gd`). The Player seeds its runtime movement vars from the equipped movement
Actions on every character change / swap (`_apply_movement`). Only non-movement feel (health/ruh,
attack pacing, hit-stop juice) stays as inspector `@export`s.

| Locomotion (typed config) | Key values |
|---|---|
| run | `run_speed` 160 (**Khalid 230**), `acceleration` 1200, `friction` 1400, `run_anim_speed` 1.5 |
| jump / arc / land | `jump_velocity` -330, `air_jumps` 2, `gravity` 900, `fall_gravity_scale` 1.35, `land_min_fall_speed` 140, `land_predict_distance` 22 |
| dash | `dash_speed` 420, `dash_time` 0.18, `dash_anim_time` 0.30, `dash_cooldown` 0.45, `dash_gravity_scale` 0.35, `blink` (**Khalid true**) |
| slam | `slam_speed` 1200, `slam_min_clearance` 50, `slam_hold_frame` 2, `slam_impact_distance` 30, `slam_min_drop` 120 / `slam_max_drop` 700 / `slam_max_damage_mult` 2.5 (drop-scaled damage) |
| `@export` (inspector) | Health `max_health` 100; Attack `attack_recovery` 0.12, `combo_reset_time` 0.45 |

**Per-character movement feel.** A movement Action's `move` (Locomotion) lists only the fields a
character deviates on; everything else falls to the shared baseline. **Khalid runs a touch faster
(`run_speed` 230)** and **blink-dashes** — the rest of his movement is baseline. The run *animation*
cadence auto-scales to each character's speed, so faster runners don't foot-slide. Reward **buffs**
layer over the config base so a loadout swap never wipes them — run speed via `run_mult`, extra air
jumps via `air_jump_bonus` (each re-applies its category on grant).

**Blink dash (per character).** A character's dash can be a **blink** (instant teleport)
instead of the glide-lunge — set by the equipped dash Action's `move.blink` (Khalid's `blink_dash`
option is `true`; the baseline glides). When on, `_enter(State.DASH)` calls
`Player._do_blink()`: it displaces `dash_speed × dash_time` ahead (the *same* reach the
glide would cover, just instant) via `move_and_collide` so it **stops at walls** and
**passes through enemies**, fires the character's own `other/blink_out.tscn` /
`blink_in.tscn` poofs (`fire_effect`, tinted to their dash palette) and a brief bright
flash. The lunge is skipped (`_dash_custom`) but the dash i-frames, cooldown, and
animation still run as the "materialize". `_blink_phase_walls` (default off) is the buff
seam to blink *through* walls. Every character has blink poofs ready, so flipping the
flag is all it takes. (This was Khalid's ability hook; it's now this universal config so
it works for characters that have no ability file.)

**Dash lunge vs. animation.** The lunge (`dash_speed` for `dash_time`) is decoupled
from the dash *animation*, which plays over `dash_anim_time`. When that's longer
than the lunge, the character keeps its full snappy dash then settles to a stop
over the extra time while the remaining frames play out — so you see the dash
instead of a fast-forward. The reach is unchanged (the settle decelerates to a stop
within the window) and the i-frames still last only the lunge. Set
`dash_anim_time <= dash_time` for the old squeezed-into-the-lunge look; raise it to
see the frames more.

**Dash-cancel into attack.** An `attack` pressed any time during a dash is **buffered**
(`_buffered_attack`) and fires the instant the lunge/i-frame window (`dash_time`) ends,
cancelling the dash's *recovery tail* (`dash_anim_time − dash_time`). So dash→attack is
responsive: the press isn't swallowed by the dash animation and you don't have to re-press.
It's gated on `_dash_left` (not `_dash_custom`), so a **blink** dash still holds its full
i-frame window before the attack comes out — the dodge isn't cancelled on frame one. Mirrors
the existing `_buffered_special` light→special buffer. The buffer clears on dash entry and
on character swap.

**Airborne arc: `JUMP → FALL → LAND`.** The full sequence when a character has the
sheets for it:
- **`JUMP`** plays the launch/rise once (its launch only replays on a *real* jump —
  see the fall-pose note above).
- **`FALL`** (looping) takes over the moment the jump animation finishes while still
  airborne, or whenever you enter the air any other way (walk off a ledge, an air
  action ends). A character with no `fall` sheet just holds the last jump frame, as
  before.
- **`LAND`** starts **predictively** — a downward ray (`land_predict_distance`, 22px,
  against the body's `collision_mask` so it catches solid ground *and* one-way
  platforms) fires it while falling at ≥ `land_min_fall_speed`, so the squash plays
  *through* touchdown instead of after it. It also still triggers on touchdown
  (`_just_landed`) as a fallback. It's **fully cancelable** — any action breaks out
  (air-rules during the brief pre-land: specials become the slam, attacks are
  grounded-only); left alone it plays once and hands back to idle/run. A character
  with no `land` sheet skips straight to idle/run.

Each phase is **opt-in per character** by the presence of the `fall` / `land` sheet.
**Khalid has both** (the working game is Khalid-only; other characters are parked in `playground/`). (Khalid's full kit — movement VFX
(run/jump/fall/dash/double-jump), a **ground slam** (`slam_default` + `slam_wind_streaks`),
and his **Ground Breaker** special (an overhead-slam AOE `Strike`, damage from
the `Actions` catalog) — is wired in the `Emitters` config, all in his red-teal-gold palette. Only his
light **attack** still lacks an effect scene, so it deals no damage for now.)

**Khalid's specials** (all in the `Actions` catalog; presentation keyed by `special_<id>` animation):
- **Ground Breaker** — AOE slam `Strike` (stun + a ground-crack).
- **Frenemy** — a charm blast: the hit enemy becomes a temporary ally (`Hit.frenemy_time` → `Enemy.become_frenemy`).
- **Come Closer** — a magnet: the `special_come_closer` effect scene (`scripts/combat/MagnetField.cs`) grabs
  the **nearest** enemy in range and `Enemy.magnetize()`s it toward Khalid, stunning it on arrival (no damage).
  The field's **`max_targets`** (=1 today) caps how many it yanks — bump it to 3 later for a wider pull. The
  grab is measured from **Khalid's** position, *not* the field's own transform: the director spawns the field
  with `add_child()` (which runs its `_ready` scan) and only sets its world position with `Nodes.place_at()`
  **afterwards**, so reading `self.global_position` in `_ready` saw a stale pre-placement transform (near world
  origin) — the pull then only landed when Khalid stood near x≈0 (i.e. right after load) and missed once he
  moved into the level. Has its own **1s cooldown** (`ActionsKhalid.cs`).
- **Redere Shield** — a held guard: the block is *state-based* — active only while Khalid is in the shield
  special (`Player._is_shielding()`), so it drops the instant he releases or is staggered (no lingering timer,
  so a hit taken right after the guard is down lands — and sounds — normally). It **blocks** all front-side
  damage; a hit caught in the brief `parry_window` right after the raise is a **perfect parry** that reflects
  to the attacker (`_on_hurt` → `Enemy.apply_hit`) — just holding only blocks. Tune `parry_window` /
  `shield_reflect_mult` on the Player.
- **Redere Frisbee** — an independent special that throws the shield as a `Projectile` (fed the Action's `hit`).
  It **ricochets**: the `Projectile`'s `bounces` (=3 on the frisbee scene) makes it chain to the next-nearest
  *un-hit* enemy after each hit — up to 4 enemies, each struck once (the Hitbox dedupes victims across the whole
  flight, so it never ping-pongs; the chain ends when no fresh target is in `bounce_range`). Each ricochet leg
  gets a fresh `max_range` and snaps its heading at the new target, then homes (`bounce_homing`) so it tracks a
  mover. A standalone Special-door swap (no longer gated on owning Redere Shield); it upgrades via its own buffs.

**Surges (abilities on the `surge` button).** Separate from specials: a **Surge** is an ability fired with
one press (Ctrl / RT) that applies a **timed self-buff** which runs independently for its full duration.
There is **no cooldown** — **Ruh is the only gate**: each use spends its `SurgeSpec.cost` (100 Ruh = one
charge), so you surge as long as you have the Ruh (re-triggering, if you can pay, refreshes it). On trigger it plays a **brief activation flex**
(`State.SURGE`, the `surge_<id>` sprite anim, ~0.5s) — a short commit — while the buff carries on
regardless; the SFX plays on trigger and each surge names its **own** aura scene (`SurgeSpec.aura`,
recoloured by the power picks) spawned for the buff's duration. `Player._try_surge()` runs every frame in
`_physics_process` (any state, no-op while dead or spawning) and gates on `if ruh < s.cost: return` then
`ruh -= s.cost`. `Player._begin_surge(s)` applies the effect flags on the `_surge_left` timer; `_end_surge`
clears them together. The data lives as `Action.Category.SURGE` rows carrying a **`SurgeSpec`**
(`configs/SurgeSpec.cs`: `cost` + `duration` + `invuln` + `damage_mult` + `damage_taken_mult` + `aura`)
in the `ActionsKhalid.SURGES` catalog (`DEFAULT_SURGE = "aegis"`). Two ship:
- **Aegis** (`aegis`) — full damage **immunity for 5s** (`invuln`; drops the hurtbox, same channel as dash
  i-frames). The old `special_default` "Flex/Impervious" promoted out of the specials pool.
- **Jnoon** (`jnoon`) — for 5s Khalid **deals ×2 damage and takes ×0.5** (`damage_mult 2.0` /
  `damage_taken_mult 0.5`; *not* immune — hits still land, just softened). The mults **stack on** the
  reward `damage_mult` / `damage_taken_mult` (folded in `resolve_tuning` / `take_damage`).
- **Asra** (`asra`) — for 5s Khalid **moves ×2 as fast** (`speed_mult 2.0`, applied via `Player._run_speed()`
  at the run/dash-blend movement sites; the anim-rate calc keeps base `run_speed`, so the run animation
  speeds up on its own instead of sliding). The looping run **footsteps** pitch up by the same
  `speed_ratio` (velocity ÷ base `run_speed`) so their tempo tracks his real speed — normal run stays 1.0,
  Asra ≈ 2.0 (`_update_animation`, `State.RUN`).

- **Nem** (`nem`) — a committed **sleep/heal CHANNEL** (not a passive buff, `channel: true`): Khalid locks
  in place, the flex plays to its **second-to-last frame** (head down, asleep) and **pauses** there, then
  he heals **`heal_frac` (0.5) of MAX hp over 5s**. Heal is capped at max, so being already **≥50% HP
  restores the whole bar**. A **hit from an enemy wakes him** — the channel cancels and he keeps whatever
  health he'd gained. Driven in `_process_surge` (`_surge_channel` / `_surge_asleep`): the wind-up watches
  `_sprite.frame` for the sleep frame, pauses playback + starts the window; `_on_hurt` cancels it.
- **Wara** (`wara`) — a **REACTIVE / counter** surge (`trigger: "hit"`, a new surge *type*). Triggering it
  **arms** it — the aura orbits with **no timer** — until an enemy attack lands. That hit deals **no
  damage**, every enemy within `stun_radius` (150) is **stunned** `stun_time` (2s), and the orbit aura is
  replaced by a one-shot AoE `burst`. Then it's spent. State: `_surge_armed` / `_armed_surge`; the arm is
  set in `_begin_surge` (no `_surge_left`), and `_on_hurt` fires `_trigger_wara` (AoE `Enemy.apply_hit` a
  stun-only `Hit`, spawn the burst, cue `surge_wara_trigger`, `_end_surge`) **before** `take_damage`.
  Two VFX (persistent `aura` + on-trigger `burst`) and two SFX (`surge_wara` cast + `surge_wara_trigger`).

Jnoon's / Asra's / Nem's auras + flex sheets (`surge_jnoon` / `surge_asra` / `surge_nem`) + activation cues
are **placeholders** (copies of Aegis) pending art/audio.

Both **cost 100 Ruh / 1 charge, no cooldown — Ruh-gated**, and last the same 5s (+ the **Fortitude** reward's
`special_invuln_bonus`, which now extends any surge). Having two Surges makes the category **swappable** — a
gate reward offers the trade (loadout convention: a category with >1 option becomes a swap).

**Ground slam (`SLAM`).** A universal air move on the **`special` button**: in the
air, press `special` to plunge straight down at `slam_speed` (1200 — far faster than
a normal fall, so it reads as committed). Like a special it's committed (no cancel
mid-plunge), and the `special` button is context-sensitive: **on the ground it does
the character's special, in the air it slams** (attacks and specials are both
grounded-only). Characters without a `slam` sheet can't slam (the air press no-ops);
**Khalid has a `slam` sheet.**

*Tall-plunge handling* — a long drop would finish the `slam` animation (firing its
impact frames) **before** touchdown, so the impact particles would emit in mid-air.
So while high, the animation **locks on its last descent frame** (`slam_hold_frame`,
sheet-relative to match the `Emitters` config) and the **sprite is hidden** — only the
sustained wind-streak particles show, reading as a fast blur. Once the ground is
within `slam_impact_distance` (a downward ray, like the predictive land) it **releases**:
the sprite reappears and the remaining impact frames play into the ground, so the
`burst` fires where it lands. A short slam never locks — it just plays through.
Ends via `_on_animation_finished` → idle.

*Damage scales with the plunge.* On release the player measures the drop (from the y
where `SLAM` began to impact) and sets `_active_hit = {"damage_scale": mult}` —
**1.0×** up to `slam_min_drop` (120px), lerping to `slam_max_damage_mult` (**2.5×**) at
`slam_max_drop` (700px). The director *multiplies* the slam hitboxes' baked damage by it
(a new `damage_scale` path in `_inject_tuning`, applied over `damage`), so **both**
boxes scale while keeping their reach/impact ratio. It's the offensive mirror of
the slam-damage curve — slam from higher, hit harder.

Slam **particles** are authored per character in `EmittersCharacters` under the `slam`
animation: a `sustained` wind-streak trail on the descent frames (`0–2`) and a `burst`
on the impact frames (`3–4`). Keep those frame ranges consistent so `slam_hold_frame`
(the last descent frame) lines up.

**Ground attacks hug the terrain.** Two shared building blocks in `scripts/combat/`:
[`GroundProbe`](scripts/combat/GroundProbe.cs) (`TryAt` — one downward `World` ray → surface point +
normal, the single "where's the ground here?") and [`GroundContour`](scripts/combat/GroundContour.cs)
(walks `GroundProbe` across a width into one contiguous contour centred on the impact, stopping at the
first gap each side so an effect doesn't leap a pit). Used three ways:

- **Static bursts (`conform_to_ground`)** — the slam + Ground Breaker emitter rows flag it;
  `ParticleDirector` calls `GroundContour.Conform(node, space)` at spawn. Each Rectangle **emitter** is
  switched to `DirectedPoints` (contour as `emission_points` + `emission_normals`, so dust erupts
  perpendicular to the slope); each box **hitbox** swaps its rect for a `CollisionPolygon2D` **band**
  following the contour (as tall as the rect). No ground → the burst is discarded. (Replaced the old
  horizontal-only `clip_to_ground`, a no-op on the TileMapLayer.)
- **Enemy static AoEs (`conform_ground` kit flag)** — Matat sets it (`Enemy.SpawnMeleeStrike`) and Nasen
  sets it (`SleeperEnemy.SpawnRageAoe` — the sleeper spawns its rage aoe on its own path, not the melee one);
  each runs the *same* `GroundContour.Conform` on the spawned `AoeStrike`. (Enemies bypass `ParticleDirector`,
  hence a kit flag rather than an emitter-row flag — but one conform path. Any new enemy attack spawned by a
  bespoke method must call `GroundContour.Conform` itself to honour the flag.)
- **Traveling wave (`Projectile.ground_follow`)** — Baghel's `ground_wave` `far_mode` sets it; each
  `_PhysicsProcess` the projectile `GroundProbe.TryAt`s under itself, snaps its Y to the surface
  (`ground_follow_offset` above it) and tilts to the normal, so the wave ripples up/down slopes as it
  rolls; it runs off a ledge → `Expire`. Facing stays on `Scale.X`, tilt on `Rotation`.

**Slam needs room below (`slam_min_clearance`, 50px).** The air press only slams when
the nearest platform *straight down* is at least `slam_min_clearance` away — a ray from
the feet down that distance (against the body's own `collision_mask`, so it catches
solid ground **and** one-way platforms; no floor below = clear). Too close to the
ground and the press just no-ops, so you can't slam with no room to build a plunge. Set
`slam_min_clearance` to 0 to always allow.

**Double jump.** After the ground jump, `max_air_jumps` (default 1) extra jumps are
allowed in mid-air; the counter refreshes on every touchdown. The **ground jump is
silent**; each **air jump** re-boosts *and* spawns the character's jump particles.
Because the particle director is frame-indexed and can't tell a first jump from a
second, the jump effect is a **code-triggered burst**: it's configured under a
`double_jump` key in the `Emitters` config (deliberately *not* a real sprite-animation
name, so it never auto-fires on a frame), and `_air_jump()` fires it via
`ParticleDirector.fire_effect("double_jump")`. That burst is combat-capable — give
its scene a `Hitbox` and the air jump deals damage / applies a buff, same as any
other burst.

**Jump vs. fall pose.** The `jump` animation doubles as the airborne/fall pose, but
its *launch* only replays when a jump is actually triggered (`_jump_launch`). Entering
the airborne state without jumping — a dash ending mid-air, or walking off a ledge —
holds the animation's last (fall) frame instead of re-launching, so you don't get a
phantom second jump before landing.

**Attack combo (LMB).** One press plays one *segment* — the frames up to the
next hit animate, then the sprite holds the hit frame for a short
`attack_recovery` and hands control back to idle. Hit frames come from the
`HIT_FRAMES` config via SpriteFrames metadata (`_attack_hits()`); an attack with
no entry treats every frame as a hit, so each click advances one frame.
The combo system supports multi-hit attacks — several hits on chosen frames, with wind-up /
in-between frames animating between them (see `HIT_FRAMES`). Khalid's `ora_ora` isn't a combo
but a **flurry** (below); single-hit attacks just list one hit frame.

**Attack styles (`Action.style`, an enum).** Most attacks are `STANDARD` (the click-per-segment
swing above). Khalid's `ora_ora` is a `FLURRY`: **hold** the attack button and the
animation loops fast (marked `loop: true` in the generator `OVERRIDES`), its punch
frames (2, 4 in the `Emitters` config) firing the `attack_ora_ora` `Strike` on every pass —
so the hits come at the loop's rate, not per click. Releasing ends it back to idle; a
buffered special still cancels it. `_advance_combo()` routes the first press to
`_start_flurry()`, and `_process_attack()` holds it while `attack` is pressed. Per-punch
damage/knockback are low (the `Actions` catalog) since the DPS comes from the cadence.

Khalid's `twin_reaper` (Elite) is a second flurry: **hold** and the whole spin loops
(`loop: true`, `fps 20`), each pass firing its five `Slash1`–`Slash5` `Strike` nodes on the
emitter frames (3/4/6/7/9) — one node per hit so each keeps its own hitbox + particles for
tuning. A flurry feeds a *single* `tuning` to every hit (12 dmg, `knockback 0` to keep enemies
caught in the spin), so — like `ora_ora` — it has **no** `HIT_FRAMES` entry (those drive the
click-combo segmentation, not a flurry).

**Reap (a damage-over-time mark).** Twin Reaper's tuning also carries `reap` (fraction of the
victim's *max* health drained per 1-second bite) + `reap_time` (how long the drain lasts) — `0.12`/s
for `5s`. **One-and-done:** the mark latches on the enemy's *first* reap hit (`Enemy._reaped`) and
plays out its fixed window once; the spin's later hits deal only normal damage — they never re-arm or
extend the drain, so the rate of death can't be stacked up. The keys ride the same tuning path as `damage`/`stun`:
`Strike.apply_tuning` (and `Projectile._inject_tuning`) copy them onto the `Hitbox` → `Hit.dot_percent`
/ `dot_time` → the victim. On an enemy, `_on_hurt` snapshots a per-tick HP and `_tick_dot` drains it in
discrete once-a-second bites (`Enemy._reap_tick`) — ticking even while stunned, and creditable as a
normal (Ruh-eligible) kill. Bump `reap` toward `0.15` in `ActionsKhalid.cs` for a deadlier mark. Any
future move (melee **or** ranged) can carry a `reap` to inflict a DoT.

**Attack cooldown (`Action.style` `COOLDOWN` + `Action.cooldown`).** A heavy one-shot can carry a
`cooldown` (seconds) in the `Actions` catalog so it can't be spammed — Khalid's `bakshen` uses `3.0`.
While it recharges,
`_advance_combo()` swallows the attack press (the swing simply doesn't start), and a small
gold **fill bar floats over Khalid's head** (`FloatingHealthBar`, the same world-space bar the
enemies use, tinted for "charge") growing empty→full as `_attack_cd` counts down; it hides once
ready or for any attack with `cooldown 0`. The timer starts the instant the swing fires and
resets to 0 on run-start / character swap. This is the per-**attack** cooldown; specials have a
short anti-spam window (`SPECIAL_COOLDOWN`, 0.6s) **but a special can also set its own real
`cooldown`** — `Player._start_special` takes the larger (e.g. **Come Closer = 3s**, since its
pull+stun is strong), and the same overhead bar shows it (a cooldown special takes the bar while it
recharges). A cooldown attack is effectively a single heavy hit — the gate blocks re-entry, so it
doesn't chain combo segments.

**Dash-attacks (the `lunge` seam).** Khalid's **`zahluq`** is a `COOLDOWN` attack that *bursts him
forward* — a heavy hit that's less than `bakshen` but slides him a long way. Its tuning keys, read by
`_process_attack` and the `Strike` at spawn:
- **`lunge`** — the burst speed. `Strike.apply_tuning` → `Player.apply_lunge` sets `velocity.x`.
- **`hold`** — seconds to **freeze on the strike frame** while sliding. During this window a lunge
  attack keeps its velocity **constant** (no friction), so the dash covers a predictable **`lunge × hold`**
  (~1100 × 0.4 ≈ 440px) with the sprite paused on the burst pose, then **stops crisply** (velocity zeroed)
  — no run-off. Non-lunge attacks are unaffected (still friction-rooted, `attack_recovery` freeze).
- **`super_armor`** — commits the dash so a hit mid-slide won't stagger him out of it (set ≈ `hold`).
- **`extents`** — the hitbox, made wide + tall so it **surrounds him** as he slides through enemies.

The hitbox *sweeps* with him via the emitter row's **`follow: true`** — the director parents the effect
(and its hitbox) onto **itself** (a child of the player, so it tracks him, centered on his body) instead
of anchoring it in the world (`ParticleDirector._fire_burst`). So it fires on a **single** frame (one
following box; multiple frames would double-hit) and the Strike's `lifetime` spans the whole animation,
keeping the box live the entire dash — you connect no matter how far from an enemy you start. Recipe for
any dash-attack: `lunge` + `hold` + `super_armor` in the tuning, `follow: true` on the emitter.

**Air attacks (opt-in).** Attacks are grounded-only *except* those whose Action carries an `"air"` tag —
the air-attack allow-list (`Player._air_attack_ok`, checked at every attack gate). Zahluq is tagged `"air"`,
so it doubles as an aerial dash. A dash-attack flies **level** in the air: while it holds the strike frame
`_process_attack` pins `velocity.y = 0` (gravity off), so it goes straight instead of arcing down; gravity
resumes the instant the dash ends. Untagged attacks stay ground-only.

**RUN needs input.** `_process_normal` enters `State.RUN` only when a move key is *actually held* (not
merely `velocity.x > 5`), so residual momentum from a dash-attack slide or a knockback decelerates in
IDLE instead of reading as a phantom run.

Two separate timers, which matters — coupling them once made the hit frame
freeze for the whole chain window:
- **`attack_recovery`** — how long the hit frame holds before idle resumes. Keep
  it short; it's just enough to read the hit.
- **`combo_reset_time`** — how long a follow-up press still *continues* the combo
  rather than restarting it. It keeps ticking after control returns to idle, so
  you can chain even once you're moving again. Lapsing it (or pressing past the
  finisher) restarts at segment one.

Clicks mid-segment are dropped (keeps the rhythm) — change `_process_attack` if
you want light-attack input buffering. A **special** press *is* buffered, though:
pressing RMB any time during a light swing is remembered and fires the instant
that hit lands (`_buffered_special`), so a fast light→special always cancels into the
special instead of the press being swallowed by the recovery frames.

**Special attack (RMB).** Deliberately *not* a combo — one press plays the entire
animation, roots the player, and ignores all input until it finishes. It also
clears any light combo in progress (all three entry points go through
`_start_special()`). The strike lands on the frame given by the
`special` entry in `hit_frames` metadata (`_special_strike_frame()`) — e.g.
A special's lands on its authored strike frame — or, if none, on
the middle frame as a default. Durations are hand-tuned per character — see
**Per-character timing** above.

**Dash.** Frame counts differ per character (3-13+), so a fixed `dash_time` would
clip the longer ones. Playback is stretched to fit instead (`speed_scale`
derived from the anim length ÷ `dash_time`), which keeps dash *distance*
identical for every character while always playing the full animation — so even
A 13-frame dash plays fully inside the 0.18s window. Grounded dashes stay level; air dashes keep
falling at `dash_gravity_scale` so they arc instead of hanging on an invisible
floor.

**API for other systems:** `take_damage()` (HP only) / `heal()` (the only HP restore),
`gain_ruh_on_hit()` (the Ruh surge meter — see
[`docs/game-design.md`](docs/game-design.md)), `grant_special_invuln(duration)` (the invuln window, now
the **Aegis surge**'s effect), `begin_run()`, `is_dead()`, `death_complete()`, `spawn()`, `set_character()`,
`portrait_path()`, and the `health_changed` / `ruh_changed` / `character_changed` signals. Ruh fills
by landing hits (no decay) and is **spent on surges** (specials are free); it never shields HP. (Enemies deal real damage;
a lethal hit runs the full death lifecycle — see **Death** / **Spawn** below.)

**Getting hit.** A landed hit (past the shield/super-armor/death guards) drops Khalid into a brief `HURT`
state that plays his `hurt` flinch animation, then hands back to idle/run. **Flinch policy** is a toggle,
`flinch_on_all_damage` on the Player (default **on**): on = react to *every* hit; off = only hits that
**stagger** (knockback > 0 — mazab/ein/nasen) flinch, while no-knockback chip/ranged hits (baghel, kebus,
which carry `knockback 0 + stun 0`) just deal damage + a grunt. The state is held for `max(stagger,
hurt-anim length)` so a tiny or zero stagger never cuts the flinch off. A fresh hit **while already
flinching** (a barrage / multiple enemies) only *extends* it — it does **not** restart the anim at frame 0,
or a continuous pummel would freeze it on the first frame and never visibly play. One smooth flinch plays
and holds until the barrage ends. (Per-enemy knockback/stun live in [`scripts/run/EnemyKits.cs`](scripts/run/EnemyKits.cs).) `take_damage()` also fires one of a few random hurt grunts (`hurt.1/2/3`, pitch-wobbled)
so he doesn't repeat, and a **low-HP warning cue** when a hit crosses a threshold **downward** —
`health_half` at 50%, `health_low` at 20% (`_warn_low_health`, `HEALTH_WARN_HALF`/`_LOW`). It's a
stateless edge trigger: it plays only on the crossing (never spams while you sit low), re-arms once a
heal lifts you back above the line, and the lower/urgent cue wins if one big hit crosses both. A character with no `hurt` sheet falls back to the old idle-during-stagger look. The
shield's block/parry is a separate feedback (a `_shake` sprite vibrate, not a state) — see **Redere Shield**.

**Low-health screen effect.** Under **20% HP** a full-screen red overlay kicks in — the **whole screen**
washes evenly toward red (a slight desaturate + red multiply, applied uniformly — no framed border or
untouched centre), with only a **subtle edge darken** for depth, deepening as HP falls to 0 and
**pulsing with a punchy lub-dub heartbeat** (`_heartbeat`: a sharp thump + softer second beat that swells
the red, not a gentle sine). It's a screen-space post shader (`vfx/shaders/low_health.gdshader`, one
`intensity` uniform: whole-screen red grade + a soft multiplicative vignette) on a `ColorRect` the **HUD** builds on its own
`CanvasLayer` at **layer 50** — above the world so it tints it, below the HUD (layer 100) so the UI stays
crisp. The HUD drives `intensity` off `_on_health_changed` (`_update_low_health`, eased in `_process`):
target ramps `LOW_HP_MIN`→1.0 from the 20% threshold down to 0 HP, re-arming on heal; the whole layer is
hidden (no screen-read cost) whenever it's fully faded out. Tunables are consts on the HUD
(`LOW_HP_RATIO`/`_MIN`/`_FADE`/`_PULSE_*`) + the shader's `hue_amount`/`vignette_*` uniforms.

---

## Passives, abilities & buffs

**This is the place to add behavioural rules** — event-driven code that reacts to the game — the
Player itself stays generic, with no per-character or per-reward branching. A **`Passive`** is one
such rule bundle; the Player holds a **list** of them (`_passives`) and dispatches every hook to each.
Two flavours, identical interface:

The passive/buff stack + the Player are **C#** now (`scripts/abilities/*.cs`, `Player.cs`) — they ported
together because an `extends` chain must be one language (see `docs/csharp-migration.md`). Concrete passives
are `[GlobalClass]` so the still-GDScript `Rewards` service can `Leech.new()` them by name.

- a character's **intrinsic ability** — a `CharacterAbility` (which *is* a `Passive`) returned by
  `Player.CharacterAbilityFor(id)`, seeded FIRST in the list. Khalid ships without one.
- a **reward-granted passive** — a `Passive` subclass, added at runtime via `Player.add_passive()` when its
  reward is taken (a reward row's `passive: "<id>"` → `Rewards._make_passive` `.new()`s the C# class), and
  cleared on run restart (each passive's `Teardown` runs so it can undo lingering effects).

```csharp
[GlobalClass]
public partial class Leech : Passive   // (or : Buff for a move-scoped one, : CharacterAbility for intrinsic)
{
    public Leech() => Id = "leech";
    public override void OnHitDealt(Player player, float amount, Node target)
        => player.heal(amount * 0.08f);   // lifesteal (scripts/abilities/Leech.cs)
}
```

Hooks, all optional (override only what you need):

| Hook | When | Use for |
|---|---|---|
| `setup(player)` / `teardown(player)` | Once, on add / remove | One-off changes; undo them on teardown so nothing leaks across runs |
| `physics(player, delta)` | Every physics frame, **after** the state machine sets velocity and **before** `move_and_slide()` | Movement overrides — whatever you set here wins |
| `on_special_strike(player)` | The special's strike frame | Spawn a code-driven effect/projectile on connect |
| `on_special_cast(player, action)` | The instant a special is cast (before wind-up) | Cast-triggered effects (**currently unused** — Impervious moved to the Aegis surge; hook kept as a seam) |
| `on_hurt(player, hit)` | Player takes a combat hit | React to damage — retaliation, defensive buff |
| `on_parry(player, hit)` | A **perfect parry** with Redere Shield (reflect branch only) | Parry payoffs — heal, counter buff |
| `on_land(player, fall_distance, fall_speed)` | Every touchdown | Fall damage, landing shockwaves (`fall_distance` = px dropped from the apex) |
| `on_hit_dealt(player, amount, target)` | Player deals damage (via RunManager) | Lifesteal, on-hit procs, stacks |
| `modify_tuning(player, action, seg, tuning) → Dictionary` | Inside `resolve_tuning`, for every swing | **Alter a move's numbers** — damage/knockback/keys; the buff path |

`Physics` runs last on purpose, so a passive can override anything the state machine decided.
`player.get_state()` exposes the current state, and the whole Player API — `take_damage()`,
`Velocity`, `add_passive()`, every tunable — is available. Each rule is "on EVENT, if CONDITION, do
ACTION"; add new event hooks to `Passive.cs` + fire them from the player as more are needed.

**The reward doc's trigger set is landing here** (`docs/rewards-design.md`). Beyond the hooks above, `Passive`
now also fires the movement/attack moments the doc organises buffs by — `OnDash` / `OnGroundJump` /
`OnAirJump` / `OnSlamTrigger` / `OnSlamLand`, plus **`OnAnimEnd`** (a melee swing recovers to neutral —
`Player.NotifyAttackAnimEnd`) and **`OnMiss`** (a player attack hitbox deactivates having struck nobody —
`Hitbox.deactivate` → `Player.notify_miss`, gated to `source is Player && !from_special`) — and a `Trigger`
enum names the whole growing vocabulary. Still reserved (no clean emit site yet): `OnPerfectDodge` (dash
i-frames disable the hurtbox, so an avoided hit fires no event) and a level/stage timer.

### Current passives

- **Leech** (`scripts/abilities/Leech.cs`) — a reward-granted passive: heal 8% of damage dealt, via
  `OnHitDealt`. The worked example of a rewardable behavioural ability.
- **No character-intrinsic ability ships** — Khalid has no C# `CharacterAbility`; his
  attacks/specials are all data (`Actions`) + effect scenes. Dropping that file gives him an intrinsic
  hook set. The parked characters in `playground/` had abilities (fall-damage-on-land, a channeled
  special that cancels when hit) — reference for what the hooks can do. Khalid used to carry the blink
  as an ability; it's now a **per-character dash option** (see **Blink dash** above).

### Buffs — move-scoped passives (`scripts/abilities/Buff.cs`)

A **`Buff` IS a `Passive`** (so it grants, dispatches, and tears down through the exact same machinery —
a reward row's `passive: "<id>"` → `add_passive`), plus the **item/build layer** the reward doc calls for
(`docs/rewards-design.md`):

- **`AppliesTo`** — *which* move(s) it touches: a move id (`"twin_reaper"`), a family keyword
  (`"attack"`/`"special"`, matched on `Action.category`), a tag (matched on `Action.tags`), or `"*"`.
  Empty = all. One field expresses both a **tailor-made per-move** buff and a **general** (category-wide)
  one. Gate a `ModifyTuning` override with `AppliesToAction(action)`; behavioural hooks (`OnParry`, …)
  already self-scope to their fire site, so `AppliesTo` there is for reward gating / display.
- **`Family`** — a **replace-in-place** group: granting a buff whose `Family` is already held tears down
  the old one first (in `add_passive`), so a higher **tier** *supersedes* its predecessor rather than
  stacking. The doc's rule: same buff, different tier → replace (by family); a *different* buff → stacks.
- **`Tier`** — the doc's rarity ladder, `Common → Rare → Hot → Sensational → Epic` (`RewardTypes.cs`),
  carrying the badge colour (none/blue/orange/purple/red); the concrete buff reads its own `Tier` to scale
  its magnitude (and can add effects per tier, e.g. +bounces).
- **`DurationLevels`** — the doc's lifetime: `null` = permanent (whole run), `N` = expires after N level
  advances. `Player.advance_level()` ticks it down each level and tears the buff out when it runs out.

Two ways a buff acts (either/both): **numbers** — override `ModifyTuning` to change a move's tuning dict
(folded in last inside `resolve_tuning`); **behaviour** — override an event hook. Current buffs:

- **Reaper's Edge** (`ReaperEdge.cs`, `["twin_reaper"]`) — +25% Twin Reaper damage via `ModifyTuning`.
  The worked example of the numbers path (a single move, unlike the global "+12% attack damage" reward).
- **Guardian's Mend** (`ParryMend.cs`, `["redere_shield"]`) — a perfect parry also heals, via `OnParry`.

#### The tiered buff catalog (`configs/BuffCatalog.cs`)

The **pivot's** buff system (`docs/buff-catalog.md`) is a data registry: `BuffCatalog.FACTORIES` maps a
`BuffIds.*` id → a `Func<Tier, Buff>` that builds the buff at a granted tier (per-tier magnitudes live in the
factory's arrays; `Family` gives replace-in-place so a higher tier supersedes a lower). Most entries reuse a
**generic buff class** rather than a bespoke one:

- **`StatBuff`** — scales one `SegmentData` stat (Damage/Reach/Knockback/Stun) by a per-tier mult via
  `ModifyTuning` (Long Reach).
- **`LifestealBuff`** — heals a per-tier fraction of damage dealt, via `OnHitDealt` (Bloodrush, Skim).
- **`InvulnBuff`** — grants an i-frame window on its bound `Trigger` (Dash/Jump/Slam/Hit immunity, plus
  **Follow-through** on `OnAnimEnd`), via `Player.grant_invuln`.
- **`ExtraAirJumpBuff`** — Setup adds air jumps (`Player.add_air_jumps`), Teardown restores.
- **`RunStatBuff`** — Setup/Teardown scales a run-scoped Player mult-field: **High Jump** (`jump_velocity_bonus`,
  folded into the applied jump velocity) and **Slam Force** (`slam_damage_mult`, read in `SlamRelease`).
- **`SlamSpringBuff`** — `OnSlamLand` primes the next ground jump's height (`Player.set_jump_spring`, one-shot).
- **`SlamQuakeBuff`** — `OnSlamLand` stuns nearby enemies (`Player.stun_nearby`, the surge stun-sweep pattern).
- **`SlamWrathBuff`** — `OnSlamLand` opens a timed attack-damage window; self-contained (ticks in `Physics`,
  boosts via `ModifyTuning` gated to `"attack"`).
- **`ChainDashBuff`** — `OnDash` zeroes the dash cooldown (`Player.reset_dash_cooldown`); minimal (tier riders TODO).
- **`OverchargeBuff`** — Bakshen `OnHitDealt` cuts the attack cooldown (`Player.reduce_attack_cooldown`; Epic = full).
- **`InstantResetBuff`** — Zahluq `OnMiss` fully resets the attack cooldown (`reduce_attack_cooldown`, huge value);
  Zahluq fires one hitbox per swing so a whiff = one reset.
- **`WiderPullBuff`** — Setup bumps `Player.magnet_target_bonus` (read by `MagnetField` on spawn) for Come Closer.
- **`MomentumBuff`** — a consecutive-hit damage ramp: `OnHitDealt` stacks a per-tier multiplier (capped at
  `MaxStacks`, applied via `ModifyTuning`), and `OnAnimEnd` resets it when a full swing/combo recovered having
  connected nothing (per-swing whiff, sidestepping the per-hitbox `OnMiss`). `MaxStacks` is a placeholder — tune at playtest.

**Deferred** (left out of `FACTORIES`, with a `// TODO(id)` in `BuffCatalog.cs`): *Slam Feast* (the slam damage
Strike spawns AFTER `OnSlamLand`, so no kill-count is available at the hook), *Backstab* (damage is baked into the
`Hitbox` at activate time and applied before the victim is known — no on-contact tuning seam), and *Perfect-Dodge
Haste/Fury/Aegis* (dash i-frames disable the hurtbox, so a dash-avoided hit fires no event to hang
`OnPerfectDodge` on). Trap/dash-hitbox/Seal-category buffs await their new mechanics.

---

## Visual effects (particles + drawn slashes)

All VFX — the frame-indexed particle system and the drawn `Strike` slashes —
live in **`vfx/`**, documented in **[vfx/README.md](vfx/README.md)**. In short:

- **Particles:** data-driven emitters layered on the sprites. A `ParticleDirector`
  (`vfx/script/ParticleDirector.cs`, a child of the player) watches the sprite and
  emits authored types at authored frames. It resolves a type by recursively
  indexing `vfx/character/<char>/` + `vfx/shared/`, so a scene resolves wherever it's
  filed. Adding one = a scene under `vfx/character/<id>/…` + a line in
  `EmittersCharacters`, no code.
- **Drawn slashes:** a directional crescent that must mirror with facing is a
  **`Strike`** (`scripts/combat/Strike.cs`) — a `Sprite2D`/`AnimatedSprite2D` + a
  `Hitbox` that grows/fades and self-frees, and covers melee slashes, blasts, and ground
  AoEs. Use it instead of a `CPUParticles2D` when the texture itself must h-flip (a directional drawn slash). Its projectile sibling is **`Projectile`** (`scripts/combat/Projectile.cs`).
- **Where to add an attack effect:** a visual → `EmittersCharacters`; a hit's
  numbers → the action's `hit.segments` in `configs/actions_<char>.gd`; a spawned thing/behavior → a
  `scripts/abilities/<id>.gd` hook. Full walkthrough (composites, `boost`,
  `Local Coords`, per-child positioning) in [vfx/README.md](vfx/README.md).

### Sprite tint shaders (Khalid's living hair + recolourable outfit)

A character's sprite can carry a `canvas_item` shader for a permanent, animated
tint. `Player.cs`'s `_apply_character()` looks for `res://resources/<char>_tint.tres`
after loading the SpriteFrames and, if present, assigns it as `sprite.material`
(else clears it) — so it's pure convention, no per-character code.

Khalid has one: `vfx/shaders/sprite_tint.gdshader` + `resources/khalid_tint.tres`.
It has **five independent colour-keyed channels**, so each only touches the part you
mean, and channels 2–5 are **off by default** (their `*_amount = 0`):

**Hair channel** — keys the bright, saturated red hair pixels (his coat is a dull
reddish-brown, keyed out by brightness/saturation), keeps their drawn shading, and
drives them with a moving palette (a base colour + two accents flowing through). The
palette is **HDR (>1.0)**, so those pixels clear the WorldEnvironment bloom's `1.0`
threshold and glow like his particles. Knobs:
- `intensity`/`vibrancy`/`glow` — strength, colour punch, bloom push.
- `flow_speed`/`flow_amount` — how fast the colour moves, how much the accents bleed in.
- `base_red`/`accent_a`/`accent_b` — the three palette colours (source_color). Set all
  three to reds for a pure-red shimmer, or vary them for a multi-colour aura.
- `key_val`/`key_sat`/`key_hue` — **the hair mask.** These are LOW thresholds
  (a pixel must be *brighter than* `key_val`, etc.); **higher = more restrictive**,
  and set too high the mask selects nothing and the effect vanishes. Ranges are
  slider-clamped to keep them sane. Raise `key_val` if the coat starts shimmering.

**Metal channel (shoes + gauntlets)** — keys the grey-ish metal (LOW saturation, mid
brightness) and recolours it toward a tint. **Off by default** (`metal_amount = 0`), so
it changes nothing until you raise it. Knobs:
- `metal_amount` — 0 = untouched; raise to blend in the tint. `metal_tint` — the colour
  (HDR to make it glow); `metal_glow` — bloom push.
- `metal_sat_max` — metal is grey, so only pixels *less saturated* than this count (raise
  if some of the metal is missed, lower if coloured cloth starts getting tinted).
- `metal_val_min`/`metal_val_max` — brightness band; excludes dark outlines and near-white
  highlights.

**Collar / Jacket / Skin channels (hue-keyed)** — three more channels, one per outfit
region, keyed by **hue band** (collar = yellow ~60°, jacket = brown coat ~30°, skin =
teal face ~170°). Grouped in the inspector under `collar` / `jacket` / `skin`. Each has:
- `<region>_amount` — 0 = off (default); raise to blend the tint in. `<region>_tint` —
  the colour (HDR to glow); `<region>_glow` — bloom push.
- `<region>_hue` / `<region>_hue_width` — **the target.** `hue` is the centre hue
  (0–1 = 0–360°), `hue_width` the tolerance. If a channel grabs neighbouring colours,
  narrow `hue_width`; if it's off-target, nudge `hue`. A shared saturation + brightness
  floor keeps all three off the grey metal, the dull shadow-tones, and the black outlines.
  Note the gold collar is only a few pixels per frame, so its effect is small by nature.

**Colour space (important).** The project runs with `viewport/hdr_2d = true`, so a canvas
shader samples `TEXTURE` in **linear** space — a mid grey reads ~0.21, not 0.5. The metal
and collar/jacket/skin keys are therefore computed on an **sRGB-converted** copy (`khsv` in
the shader, via `lin2srgb`), so their `*_val_*` / `*_sat_*` thresholds line up with what you
see in a colour picker. (The hair key was tuned against the raw linear value and is left as
such.) If you ever add a channel and its key only grabs the *brightest* pixels of a region,
that's the linear-vs-sRGB trap — key it off `khsv`, not `hsv`.

> The shader + HDR bloom only fully render in the running game (F5) — a `--headless`
> still shows the raw sprite, so tune it live, not from screenshots.

#### Colour-picker preview (`scenes/palette_preview.tscn`)

A standalone pre-game screen (`godot-mono res://scenes/palette_preview.tscn`, controller
`scripts/ui/PalettePreview.cs`) runs Khalid's `run` cycle on a black (adjustable)
backdrop with one picker per body part **and** per power-colour family.

**Body recolour = the material-aware palette LUT** (`vfx/shaders/sprite_palette.gdshader`
+ `configs/PaletteConfig.cs`), *not* the tint shader. The repalette baked the sprite to
exactly 36 known colours (6 materials × 6 shades). The shader matches each pixel to its
`src` slot and outputs the `dst` slot, so a picker that rewrites `dst` recolours **every**
pixel of a part — fixing the old tint-shader problem where the hair *hue-key* only caught
the bright red pixels and left the darker hair unchanged. Because the shader knows each
pixel's material (`slot / 6`) and shade (`slot % 6`), it layers the effects back on:
per-material **HDR glow** (`glow[6]`) and the living-hair **flow** (dark accents rippling
through the bright shades on a moving wave). Same effects, now on an exact + complete
recolour. All six parts recolour — **including pants** (no hue-key gap anymore).

`PaletteConfig.Derive()` is **anchor-by-value**: the picked colour lands verbatim on the
shade whose lightness is nearest it, and the rest of the ramp shifts by the same delta —
so the colour you pick is the colour that covers most of the part (fixing "I picked bright
but it showed deeper"), the light→dark shading survives, and a dark pick doesn't collapse
the part to black (the nearest-shade anchor keeps the shift small).

**Where to tweak:**
- **Glow / vibrance / flow** — the `VIBRANCY`/`FLOW_*` consts + the per-material `MATERIAL_GLOW`
  defaults in `PaletteConfig.cs` (fed to the shader by `PaletteConfig.MakeMaterial()`). The flow
  maths itself is in `sprite_palette.gdshader`.
- **"Colour chosen == colour shown" (accuracy)** — `PaletteConfig.Derive()`. It anchors the
  pick to its natural shade; adjust how the ramp shifts there if you want a different feel.
- **Gauntlets vs boots** — still one `metal` material; split it into two materials (add a
  seventh to `MATERIALS`/`DEFAULT`, re-swatch the sprite) to pick them independently. TODO.

> **Wired into the run.** The preview screen is now the **boot scene** (`project.godot`
> `main_scene`), and its **Start run** button stamps the picks into `PaletteConfig.picks`
> (body) + `VfxPalette.picks` (powers) — both statics that survive the scene change — then
> loads `level.tscn`. In `Player.cs`, `_apply_character()` builds Khalid's body material from
> `PaletteConfig.MakeMaterial()` (the SAME builder the preview uses, so run == preview), and
> the Ruh-absorb hair flare now drives the LUT's `hair_surge` uniform. The old tint shader
> (`khalid_tint.tres`) is retained only as a legacy path for non-Khalid characters.
>
> **Scheme slots (saved across sessions).** The selector is **Default + up to `SaveData.MAX_SCHEMES`
> (5) slots**, plus an **active** index, persisted to `user://save.cfg` (`[colors]` section, alongside
> the run record; ConfigFile serialises `Color`/`Dictionary`/`Array` natively). **Default** (active
> `= -1`) is the built-in palette — always selectable and never overwritten, so the default look stays
> reachable even when all 5 slots are customised (Save is disabled while it's selected). Selecting a
> slot loads it and makes it active; **Save scheme** writes the current picks into the active slot;
> **Start run** only *applies* the picks to the run (it does **not** save — Save is the explicit
> commit). On boot the preview opens on the active scheme, so it "applies on startup" (a fresh save
> starts on Default). Power families are labelled **Power 1/2/3** (internal keys stay red/gold/teal for
> `VfxPalette`). Filled slots show a `•` on their button.

#### Portrait recolour (`vfx/shaders/portrait_recolor.gdshader`)

The HUD portrait (`assets/portraits/Khalid.png`) is painted in the same stylised palette as the
sprite (red hair, teal skin, yellow collar+eyes, brown coat), so it follows the **body** picks. The
shader classifies each pixel into a family — hair (red) / coat (brown) / trim (yellow) / skin (teal)
— and adopts that family's picked **hue + saturation**, keeping the pixel's own **value** (its painted
shading) — the same rule the body LUT's `derive()` uses. (Hue-*only* was too weak on the dark, mostly
occluded coat: swapping the hue of a near-black pixel is invisible; taking the pick's saturation too
makes even a dark coat read clearly as the new colour.) Only families with a pick recolour (target
alpha `< 0` = leave untouched). The **yellow eyes ride the trim/collar band on purpose** — they track
the trim pick. `PaletteConfig.make_portrait_material()` maps picks → colour uniforms; the HUD sets it
in `_on_character_changed`, the preview shows it live next to the sprite.

- **Tuning** — the hue bands + `sat_floor` (0.10) are uniforms in `portrait_recolor.gdshader`; if a
  region is mis-classified (e.g. the dark background teal grabbing the skin band), narrow the band or
  raise `sat_floor`. The coat stays dark by design (dark in the source art) — it now reads as its hue,
  but making it *brighter* would need a value lift, which would flatten its shading.

**Ruh-absorb hair flare follows the scheme.** The flare (`Player.cs` `_hair_surge`) drives the body
LUT's `hair_surge` uniform toward `hair_surge_color`, which `make_material()` sets to
`VfxPalette.recolor(PaletteConfig.RUH_CORE)` — the Ruh orb's core colour run through the *power* picks.
So it matches the recoloured Ruh soul (pick Power 1 = blue → blue orb **and** blue flare) instead of a
fixed gold. No picks → the default red flare (matching the default red Ruh).

#### Power / VFX recolour (`configs/VfxPalette.cs`)

The emitter-side counterpart to the body tint. A colour audit showed Khalid's ~40 effect
colours collapse to **three well-separated hue families** — red (~0°, the signature crimson
+ its HDR/pink/brown variants), gold (~50°), teal (~176°) — plus neutrals (white/grey/black
cores) and rare outliers (come_closer's purple). Because the families are far apart in hue,
**nothing is pre-baked**: effects recolour at *spawn time*.

`VfxPalette.recolor_tree(node)` walks a freshly-instantiated effect and, for every colour it
carries — `color` / `self_modulate` / `Line2D.default_color`, both particle ramps
(`color_ramp` / `color_initial_ramp`), the `ParticleProcessMaterial` colour + ramps, **and any
`GradientTexture` assigned to a `texture`** (a common trick: the dash Trail colours its particles
via a gradient set as the *texture*, not `color_ramp` — miss this and the dash stays red) —
classifies it by hue into a family and swaps **only the hue** to the player's picked colour,
keeping saturation, brightness (incl. HDR `>1` for bloom) and alpha. Gradients / process
materials are **copied before edit** (scene sub-resources are shared across instances, so an
in-place swap would compound across spawns). So "blue attacks" is today's red effect rotated in
hue: glow, fade and HDR bloom all survive. Neutrals (below `SAT_FLOOR`) and unmatched hues (the
purple, `> HUE_TOL`) are left untouched.

- **`VfxPalette.picks`** — `{family -> Color}`, set once per run (`set_picks`); empty = the
  default red/gold/teal look. **Dedicated to VFX**, independent of the body pickers.
- **Choke points** — `ParticleDirector._spawn()` calls `recolor_tree` on every effect it fires
  (dash / run / all attacks / all specials / slam / spawn / death / blink). The surge aura
  recolours its code-set `moon_color` in `Player.cs`; the **Ruh orb** (in
  `vfx/character/khalid/ruh_orb/`) is recoloured at its spawn in `run_manager`; the **status
  overlays** (`vfx/character/khalid/status/` — ground_breaker + frenemy stun) are recoloured in
  `Combatant.spawn_victim_vfx(..., recolor: true)`, passed **only** from the enemy-victim path
  (`scripts/enemies/Enemy.cs`) so an enemy effect landing on the *player* keeps its own colour. Everything a
  Khalid power emits lives under `vfx/character/` and is recoloured; a regression test
  instantiates all 37 `.tscn` there under picks and asserts no red survives.
- **Where to tweak** — family hue centres, `SAT_FLOOR`, `HUE_TOL` in `configs/VfxPalette.cs`.

---

## Audio (SFX + Music)

Sound effects split **config from code**, mirroring `Emitters`. The **catalog** of what sounds exist
is pure data in per-area files — **`SfxCharacters`**, **`SfxEnemies`**, **`SfxWorld`** (`configs/Sfx*.cs`)
— and the autoload **`Sfx`** (`scripts/audio/Sfx.cs`) is just the runtime that plays them. Files live in
**`sfx/`**.

Background **music** has its own sibling autoload, **`Music`** (`scripts/audio/Music.cs`), files in
**`music/`**. It's a **two-player crossfader**: `Music.play("key")` fades the current track out on one
player while the new one fades in on the other, **always started from the top** — so switching beds is
smooth and re-entering a level restarts its music fresh. `Music.stop()` fades to silence;
`Music.pause()`/`resume()` freeze/continue at position. `.mp3`/`.ogg`/`.wav` are all force-looped.
Register tracks in `Music.TRACKS`. In the run: the `"level"` bed starts (from the top) when the arena
builds via `RunManager.BuildArena` (and again on a death-restart). *(The old `"base_rest"` clear/exit
crossfade is gone with levels; that track is registered but no longer triggered by the run.)* Plays on a
`"Music"` bus if present (else Master), and is a silent no-op until the file exists. Same "drop a file,
add one line" workflow as `Sfx`.

- **The one place to check what sounds we use:** each config's **`CUES`** dict — a `key → path`
  master list per area. **Paths live only there**; nothing else hardcodes a `res://sfx/…` path.
- **Add a sound:** drop the file in `sfx/`, add one `CUES` line to the right config, then reference
  it by **key** — never a path.
- **Trigger it two ways:**
  - **Code event** — `Sfx.play("dash")` / `Sfx.play_at("enemy_death", pos)` (one-shots, pooled so
    overlaps don't cut off) or `Sfx.make_loop("run")` (a looping player the caller owns). The
    *trigger* lives in the script (that's where the event is); the *file* lives in the config.
  - **Frame-synced hit** — declare `anim → { sheet_frame: cue }` in the config's **`FRAMES`** dict;
    the presentation driver plays it when the animation reaches that frame (the audio twin of the
    particle bursts). This is symmetric to VFX — the **Emitters config stays particles-only**.
- **Enemy sound keys** follow conventions the code composes: `enemy_death` / `enemy_spawn` (shared,
  positional — death on `_die`, spawn with the puff in `RunManager._spawn_fx`), `<id>.<type>`
  (attack start, `type` = the enemy's `close_type` / `far_type` — the StrikeType of its close-range /
  far-range attack, picked by which attack fired),
  `<id>.delayed_projectile_burst` (a lob's delayed explosion), and per-frame
  hits in `SfxEnemies.FRAMES` — all in `SfxEnemies`, keyed by `enemy_id`.
- **Unregistered key = silent** (an `<id>.<type>` with no cue just plays nothing); a **registered
  key whose file is missing = one warning** — so a cue can be listed before its audio lands.
- **Buses & mixing:** the mixer is split **Master → SFX + Music** (`default_bus_layout.tres`), so the
  two categories have independent volume + effects; `Sfx`/`Music` players auto-route to their bus.
  Control them at runtime with **`AudioBus`** (`scripts/audio/AudioBus.cs`) or the convenience
  wrappers **`Sfx.set_volume(0..1)`** / **`Music.set_volume(0..1)`** (+ `set_muted`) — bind a settings
  slider straight to those. **Effects** (EQ to tweak frequencies, low/high-pass filters, reverb,
  compressor, …) go on a bus: author them in the editor's **Audio panel** (bottom dock) for anything
  permanent, or add/tweak them live from code (`AudioBus.add_effect(&"SFX", AudioEffectEQ.new())`,
  `AudioBus.get_effect`/`set_effect_enabled`) for dynamic changes (an underwater muffle, boss-room
  reverb). Music also has its own per-track fade envelope on top of its bus volume.
- **The rule:** *what* sounds exist is declared in the `CUES` configs (checkable in one place per
  area); *when* they fire is either a `Sfx.play(key)` at a code event or a `FRAMES` entry for a
  frame-synced hit. `sfx/ruh_absorb.wav` is a synthesized **placeholder** (replace freely).

---

## Enemies & combat

### Enemy sprites

Enemies use the **same pipeline** as characters, just a different group and
animation set (`idle`, `walk`, `attack_<type>` derived per the enemy's close/far attack, `death`). Source sheets
live in `sprites/enemies/<id>/`; `gen_spriteframes.py` processes both groups (see
`GROUPS` at the top) and writes `resources/enemies/<id>.tres`. Enemies share their
own normalised canvas, independent of the character canvas. Same 128x80 + frame-0
idle-reference rules apply.

The sheets are **repaletted** (collapsed from hundreds/thousands of AI-shading colours to ~5–15)
by the `grunt` profile in the art repo's repalette tool, each enemy carrying its own **body hue**
(`GRUNT_ENEMIES` there — Nasen blue, Mazab crimson, …). In-engine they wear a **shared accent-glow**
material — `resources/enemy_glow.tres` → `vfx/shaders/enemy_glow.gdshader`, applied to every enemy
sprite in `scripts/enemies/Enemy.cs` (`GLOW_MATERIAL`). It gates on saturation + brightness so **only the bright accent**
(eyes / orb / iris) blooms while the dark body stays flat — a fraction of Khalid's glow, so they read as
"alive in the dark" without lighting up. **Tweak** `glow` / `sat_min` / `val_min` on the material (raise
`sat_min`/`val_min` if the body starts glowing; raise `glow` for punchier eyes). Bloom only renders in the
running game (F5), not headless. Colour-*tiers* (per-spawn recolour driving behaviour) are a later step.

### The `Enemy` node (`scripts/enemies/Enemy.cs`, `scenes/enemy.tscn`)

One reusable ground enemy. `enemy.tscn` is a thin wrapper (root + script) so it
can be dropped into a level and tuned in the inspector; the enemy's **body** (sprite,
hurtbox, contact box, health bar) is built in code, but its **attacks** are
self-contained SCENES (see below), so the scene has nothing fragile to hand-wire. Key traits:

- **Capabilities come from `close_type` / `far_type`.** Each names the StrikeType of the enemy's
  close-range / far-range attack; the animation is **DERIVED** as `attack_<type>` (e.g. `attack_aoe`,
  `attack_projectile`) and that attack is enabled when its sheet exists. An enemy with only one —
  or, like a stationary sleeper, **no `walk`** — just works; missing animations are never used (a
  walk-less enemy stands instead of patrolling).
- **Attacks are self-contained scenes (the PLAYER pattern).** Every enemy attack — a melee `Strike`,
  a `Projectile`, or the lob's `Strike` explosion — is authored AS a scene: the component script on the
  root + its **own `Hitbox`** (shape authored) + the visual. `Enemy._spawn_attack` (shared by the melee
  strike, Nasen's rage, and Ein's blast; `_fire_projectile` and the lob's `_explode` do the same) instantiates
  it, **mirrors the whole thing by facing** (so a directional strike/beam comes out the way the enemy
  faces — this is what fixed Tarri's blast), and **injects** the numbers (`damage`/`knockback`/`stun`)
  into the scene's hitbox via `apply_tuning`, exactly like the `ParticleDirector` does for the player. The
  hitbox SHAPE + lifetime + emit window live in the scene now — so the old `close_hitbox_extents`/`_x` /
  `close_strike_lifetime` / `far_hitbox_extents` exports are **superseded by the authored scene** (the
  scene wins). This replaced the earlier code-built-hitbox approach so enemies and the player are consistent.
  **Fallback:** an enemy with a close-attack anim but **no close-attack scene** — a far-attack enemy like Kebus
  doing a point-blank jab — instead builds a bare **code hitbox** from `close_hitbox_x` + `close_hitbox_extents`
  (half-size) + `close_strike_lifetime` (`Enemy.SpawnCodeMeleeStrike`), so the swing still connects. (Without
  it those enemies' melee dealt no damage — a long-standing gap, fixed 2026-08.)
  Two knobs ride on this: the `EmittersEnemies` **`pos`** anchors the whole attack (mirrored by facing —
  move it to reposition a strike's beam/box together), and the enemy **engages at its REAL reach** — on
  `_ready` `close_range` is derived from the attack scene's hitbox far-edge (+ `pos.x`) via `_melee_reach()`,
  and a `"forward"` shot's `far_range` is clamped to `far_travel`. So an enemy closes exactly as far
  as it can hit, and shrinking a hitbox brings it *closer* — no separate range to keep in sync with the box.
  **Authoring the particles:** the `Strike` leaves emission to the scene, so a `CPUParticles2D` at its default
  `one_shot = false` **loops for the strike's whole life** (reads as "it keeps emitting"). For a single hit
  flash set **`one_shot = true`** (+ high `explosiveness` for a crisp pop). And since `pos` moves the whole
  strike, the clean workflow is: **author each emitter dead-centre `(0,0)` and position it with `pos`** — no
  hand-nudging nodes in the editor. (`pos` carries the hitbox too, so the burst and the box stay together.)
- **Close jab vs. AoE.** The close attack's hit frame(s) fire the strike scene (`EmittersEnemies` `<id> -> aoe`
  / `close_type`); its **Hitbox is authored in that scene** — a jab is a small box in front, an **AoE swing**
  a wide box centred on the body with a long lifetime (it still hits once — `Hitbox` dedups). **Matat** is the worked example: a
  chasing bruiser who sweeps his arms for a wide orange shockwave (kit `EnemyKits.MATAT`; VFX
  `vfx/enemy/matat/attack/matat_aoe.tscn`; the AoE erupts on attack frame 4). His `attack_loops = true`
  so the whole swing **cycles continuously** (re-erupting the AoE) while you stay in reach — no
  one-swing-then-freeze-on-cooldown — and `attack_hitstop = 0` keeps that loop smooth; damage is low
  since it hits every cycle.
- **Channelled stationary blast.** **Tarri** is a Bakshen-style caster with more reach: a lit-yellow mob who,
  on his **last attack frame**, **holds + vibrates** (the freeze-on-fire-frame hit-stop + `attack_shake`)
  while he ERUPTS a **wide stationary forward blast in front — a melee `Strike`, NOT a travelling shot**.
  He's close-only (anim `attack_blast`), so he walks into `close_range` (his blast reach) and swings;
  `tarri_blast.tscn` (a `Strike` scene) authors the forward hitbox + a **beam that emits from his body and
  mirrors with facing** (fixed via `_spawn_attack`). **The hold length is the blast's own `emit_duration`,
  not `attack_hitstop`:** when a fire frame spawns a channeled Strike, `_on_frame_changed` feeds that Strike's
  `emit_duration` to `_begin_hitstop`, so the fire-frame freeze ends **exactly** when the emission does — he
  snaps back to idle the instant the blast is gone, never stranded on the attack frame (tweak `emit_duration`
  in `tarri_blast.tscn` to retime the whole thing). Non-channel melees (Matat's instant `aoe`) still use the
  plain `attack_hitstop`. His `close_type = "blast"` keys both the SFX (`tarri.blast`
  at the start, `tarri.blast.3` on the fire frame) **and** the strike VFX — `_spawn_melee_strike` looks up
  the effect under the enemy's `close_type` (falling back to `aoe`), so a typed strike (Matat's `aoe`, Tarri's `blast`) finds its scene
  under that key. Kit `EnemyKits.TARRI`; VFX `vfx/enemy/tarri/attack/tarri_blast.tscn`.
  **Getting hit mid-channel cancels it — visuals AND audio.** A channeled Strike (`emit_duration > 0`) is
  remembered as `Enemy._active_channel` when it spawns; if a hit **staggers or stuns** us before the window
  ends (`_on_hurt`, the magnet stun, Nasen's rage-break — and `_die`), `_cancel_channel()` calls the Strike's
  `cancel()` so the emission + beam break **with** the enemy instead of playing out while he stands frozen —
  honouring the Strike's own `interrupt_on_hurt` opt-out. The **sound stops too**: a channel attack's cues
  (start + fire-frame) don't go through the fire-and-forget pool — `_play_attack_sfx` routes them through
  **enemy-owned `AudioStreamPlayer2D`s** (`_attack_sfx`, detected once via the `_is_channel` peek in
  `_melee_reach`), which `_stop_attack_sfx()` cuts on the same interrupt. So a stunned Tarri goes fully
  silent, not a beam that vanishes while the blast is still roaring. (`Sfx.make_oneshot_2d` is the stoppable,
  positional one-shot this uses; non-channel attacks keep using the pool.)
  **He stays committed until the blast fully clears.** The fire-frame freeze ends with the *emission*, but
  the blast's hitbox + particles linger a beat longer; while `_active_channel` is still valid, `_act`
  holds his ground **and facing** (early-returns before the face/pursue logic), so a player who dashes
  behind him can't yank him around to chase while his blast is still firing the other way. `_active_channel`
  goes invalid the instant the Strike frees, releasing him exactly when the blast is gone.
- **Multi-hit melee combo (per-hit VFX + SFX).** A close attack (e.g. `attack_melee`) with **several `HIT_FRAMES`** fires one
  strike **per hit frame** — each an independent self-contained Strike scene, so a combo's swings can look
  and sound different. **Breski** is the worked example: a blood-red bruiser whose `attack_melee` hits on frames
  **4 (a jab)** and **9 (a heavier follow-up)** (kit `EnemyKits.BRESKI`; `close_type = "melee"`). The
  per-hit scene is keyed by the **frame it fires on** — `_melee_vfx_key(_sprite.frame)` names the
  `EmittersEnemies` row `<type>_<sheet-frame>`: **`melee_4`** (the jab) and **`melee_9`** (the heavy),
  matching `HIT_FRAMES` and the SFX cue numbers so a hit's scene, sound, and frame all read the same number.
  (`_sprite` reports emitted frames, so `AnimMeta.sheet_start` is added back to name the row; a single-hit
  attack has no framed row and falls back to the bare close/far type, so Matat's `aoe` / Tarri's `blast`
  still work.) SFX follow the standard per-frame path (`SfxEnemies.FRAMES` → `breski.melee.4` /
  `breski.melee.9`) plus the shared start cue (`breski.melee`). Each hit also gets **its own hit-stop** —
  `_end_hitstop` re-arms `_impacted`, so the 2nd swing freezes/shakes too instead of only the 1st. VFX:
  `vfx/enemy/breski/attack/breski_melee_4.tscn` (jab) + `breski_melee_9.tscn` (heavy).
- **Passive walk trail.** An enemy with a `walk_trail` row in `EmittersEnemies` wears a particle trail
  while it walks: `Enemy._build_patrol_trail` attaches the scene once and `_physics_process` toggles its
  emitters by whether it's actually moving (author the emitters `local_coords = false` so the particles
  linger in the world). Tarri uses one (`vfx/enemy/tarri/walk/tarri_walk_trail.tscn`).
- **Emitter naming (`EmittersEnemies`).** An enemy's particle rows are keyed by the attack's **strike
  type** (`enums/combat/StrikeType.cs`: `melee`, `projectile`, `delayed_projectile`, `aoe`, `delayed_aoe`,
  `kamikaze`, `blast`, `tackle`, `trap`), never an ad-hoc name. A component of an attack appends a role:
  `<type>_burst` (a projectile's explosion, e.g. `mazab → delayed_projectile + delayed_projectile_burst`),
  `<type>_trail` (the motion trail into it, e.g. `ein → kamikaze + kamikaze_trail`). A passive movement trail is
  `<state>_trail` (`walk_trail`, worn by Tarri). So `nasen`/`matat → aoe`, `kebus`/`baghel → projectile`,
  `tarri → blast`. **The SFX
  side (`SfxEnemies`) uses the same type keys** — `<id>.<type>` where `type` is the enemy's
  `close_type` / `far_type` (`@export`, set per kit; picked by which attack fired) — and the
  scene/wav **filenames** follow suit (`nasen_aoe.tscn`, `mazab_delayed_projectile.tscn`, `aoe.wav`, …).
- **Behaviour:** patrols between its spawn point and `spawn + patrol_distance`,
  pausing `idle_time_min..max` seconds at each end. If the player enters its line (aligned + within
  `far_range`) it engages — its **close attack** (the `attack_<close_type>` strike) within `close_range`,
  else its **far attack** (the `attack_<far_type>` shot). A **close-only** enemy (a close attack, no far) then
  **walks in** to close the gap even without `aggro` — otherwise a close-range mob would just stand and wait;
  a far-attack one holds its ground and fires. While engaged it plays a **live idle loop** (a breathing
  ready-stance), not a frozen frame.
- **Height-aware engagement (`attack_align_y`, default 40px):** both boxes are
  horizontal, so an enemy only *engages* — attacks, holds, or (with `aggro`) chases —
  when the player is roughly at its own height (feet-to-feet within the band). A
  player on a platform above/below is treated as out of reach: the enemy **keeps
  patrolling** instead of freezing to face someone it can't fight. Keep the
  band under the platform spacing.
- **Edge-aware:** a downward probe `edge_check_x` ahead of each foot stops it
  walking off ledges — it turns around on patrol and won't chase off a platform.
  So enemies can patrol on platforms safely. The probe spans a **tall vertical range**
  (feet −14px up to +28px down, `HitFromInside`) so a **slope** reads as continuous floor, not a
  cliff — it reaches up for a rising floor and down ~a tile for a descending one, so enemies chase
  *down* slopes too; a real drop deeper than ~a tile still stops it. Enemies also run with
  `FloorSnapLength=16` + `FloorConstantSpeed` so they glide up/down slopes without
  floating off descents or crawling up climbs.
- **`aggro`** (default **on** — enemies are hunters): it *chases* the player up to
  `aggro_range` (the **give-up leash**: get farther and it drops back to patrol), instead of only
  fighting whoever wanders into its line. It chases to its **attack reach** — a *far-attack* mob closes
  only to **firing range** (`far_range`) and holds (it won't run its bow into your face), a
  *close-only* mob closes to `close_range` and swings. It's a per-instance export, so set it **false**
  per-kit for a mob that should just guard a spot.
- **`alert_duration`** (default **5s**): getting hit **alerts** the enemy — it then
  detects and *pursues* the attacker for that long **regardless of its normal range**
  (re-hits refresh it), so a shot from off-screen doesn't go unanswered. It still only
  *lands* an attack when it's at your height (`attack_align_y`); alert just gets it
  moving toward you. 0 = never alerts.
- **`friendly_fire`** (default **off**): when on, **this** enemy's attacks also hit
  *other* enemies, not just the player (it never hits itself — the Hitbox skips its own
  `source`, and `Combat.hurt_mask` ORs in the ally hurt layer). **Per instance** — flag
  one mob for chaos, not the roster. The seam for enemies fighting each other.
- **`contact_damage`** (default **0 = off**): when set, touching the player
  deals it on `contact_interval`. Also per-instance.
- **The far attack** fires from the **muzzle** (the `Emitters` config `<id> → projectile → pos`) on the
  animation's hit frame (`hit_frames` metadata). Four `far_mode`s:
  - `"aimed"` (the default) — a `scripts/combat/Projectile.cs` that fires at the player's **body** and
    **tracks their elevation** at fire time (`can_fly_up` + `rotate_to_heading`, so it can angle up/down at a
    player a level away and points along its flight). **`far_aim_cap`** clamps the tilt to ±that° off horizontal
    (Kebus = 45), so a player far above/below never makes the shot near-vertical — it just fires at the cap.
    Pair with a wide **`attack_align_y`** (Kebus = 120) so he'll *engage* across levels, not only when level with you.
    The shot doesn't steer after firing (`homing = 0`). Kebus' staff bolt.
  - `"forward"` — a straight, **non-tracking** bolt: flies dead ahead in the enemy's facing for `far_travel`
    px then fizzles, ignoring where you are. (Use it for a dumb straight shooter; `aggro` still governs chasing.)
  - `"ground_wave"` — a `Projectile` that rolls forward along the ground and **hugs the terrain surface**
    (`ground_follow`, + a scorch `ground_trail`), rippling up/down slopes; fizzles at `far_travel` or when
    it runs off a ledge — Baghel's red energy surge.
  - `"lob"` — a **`LobProjectile`** (`scripts/combat/LobProjectile.cs`), a *thrown bomb*
    (Mazab). It arcs out of the muzzle **aimed** at a spot next to the player (`lob_land_offset`,
    biased toward the thrower), then **flies ballistically** until it lands on a real surface,
    where it sits **harmless but blinking** for `lob_dwell` (~1s) and **explodes** into a wide
    ground AoE. It deals **no damage in the air or on landing** — only the blast hurts, so it's
    *dodgeable*: clear the landing spot before the timer ends. Three phases — **ARC** → **DWELL**
    → **EXPLODE** (spawns a hostile `Strike`, the same AoE component nasen's aoe / the
    ground-breaker use, sized by `lob_explosion_extents` and using
    `far_damage`/`far_knockback`/`far_stun`). Two things keep it honest:
    - **`lob_arc_time`** only *solves the launch velocity* to aim the toss (arc height/angle);
      it does **not** decide where it stops. The bomb keeps falling until it actually crosses an
      **`L_WORLD`** surface **while descending** (a per-step ray, so it can't tunnel through a
      thin ledge; one-way platforms are passed through on the way up) — so a player who steps
      out from under it never leaves it hanging in mid-air.
    - **`lob_max_life`** (default 3s) is the safety net: a bomb thrown over a ledge with nothing
      below **detonates mid-air** (no dwell) when it elapses, rather than falling forever.

    The thrown-object look (Mazab's steel-blue `mazab_rock.tscn`, spun as it tumbles) and the
    blast look (`mazab_explosion.tscn`) come from the `Emitters` config (`mazab → projectile /
    explosion`), like every enemy emitter. Give Mazab a wider `attack_align_y` so his arc can
    reach a player one platform up/down.
  - **Look** — the projectile's particle scene comes from **the `Emitters` config**
    (`<id> → projectile → scene`, e.g. Baghel's `attack_ground_wave.tscn`, Kebus' `attack_bolt.tscn`),
    which the projectile instances as its visual — you edit/preview it in the editor like any scene
    (they're built `emitting = true`). Empty = a simple orb trail built in code (the
    `scripts/combat/Projectile.cs` fallback). `far_hitbox_extents` / `far_hitbox_offset` size the collider
    (a small box for a bolt, a tall slab rising from the ground for a wave).
    Baghel's wave is a **crest**: chunks kick up-and-forward out of a
    ground-hugging emission strip and arc back down under gravity while the
    projectile outruns them (`local_coords = off`), so they trail into a rolling
    swell. Keep his `projectile` `pos.y` (the muzzle, in the `Emitters` config) near 0 so the
    emission base sits on the ground — a negative y lifts the whole wave off it.
  - **Ground trail** — a `"forward"` shot sets `proj.ground_trail`, so
    `scripts/combat/Projectile.cs` adds a second, code-built emitter that lays longer-lived red
    embers along the floor (`local_coords = off`, so they stay put as the shot
    rolls on) that linger and fade behind it. Its colour is **sampled from the
    wave's gradient** (`_sample_visual_color`), so it always matches whatever red
    you tint `attack_ground_wave.tscn` to in the editor — no second gradient to keep in
    sync.
  - **Graceful fade** — on impact or when `life` runs out, a projectile doesn't
    `queue_free` instantly (which would vaporise every live particle). It
    `_expire()`s: stops damaging/moving, sets `emitting = false` on all its
    emitters, and frees only after the longest particle lifetime, so the wave and
    its trail fade out instead of popping.
  - **Impact vfx** — authored as a child node named **`Impact`** *inside the projectile's own scene* (a
    Node2D/particle node, left dormant with `emitting = false`), so you build + tune it right there in the
    scene — no separate file, registry, or path convention. On hit (`_on_struck` → `_spawn_impact`, fired
    each hit so a ricochet bursts at every enemy) the projectile `find_child("Impact")`s it, **duplicates**
    it (so a bouncing shot gets a fresh one per enemy), drops the copy into the world, fires its emitters
    once, and frees it after the longest particle lifetime. Two deliberate placements: it spawns **on the
    thing it hit** — the struck victim's hurtbox collision-shape centre / torso via `_hit_point(victim)`,
    NOT the projectile's own origin (a sized hitbox fires while the body is still a *variable* distance
    short — worse with a big box / bounce / homing — so spawning at the projectile looked random + short);
    and its `z_index` is lifted to **50** so it renders *over* the enemy sprite instead of behind it. No
    `Impact` node = no impact. Every straight-shot projectile carries one, colour-matched to its shot
    (frisbee/cherry red, kebus green, baghel red-orange); a player shot's `Impact` is a child of its scene
    root, an enemy shot's lives inside its **visual** scene (`kebus_projectile.tscn` etc.). `impact_sfx` is
    the audio twin (played positionally at the same spot). Lobs explode via `LobProjectile`, separate from this.
- **The close attack** enables a hitbox in front on the animation's hit frame (from the
  `hit_frames` metadata — Kebus: sheet frame 3).
- **`attack_loops`** (default **off**): when on, the close attack **loops** while the
  player stays in close reach (a channel/flurry) instead of one swing per cooldown. Each
  cycle re-plays from the anim's `loop_from` (`gen_spriteframes`), so a wind-up lead-in
  plays once and only the strike cycle repeats; when the player leaves reach it ends with
  the normal cooldown → idle. (Built on the same `_loop_from`/`_replay_from` helpers Nasen's
  rage uses.)
- **Idle: settle once, then breathe (ping-pong).** The idle animation plays **once from frame 0** — the
  settle/intro pose (e.g. Baghel dropping *both* arms) — then loops the rest **back and forth** between
  `idle_loop_from` and `idle_loop_to`, never landing back on frame 0. Looping the *whole* clip made that
  intro pose twitch on every cycle (arms slam down, scratch, slam down…); the bounce keeps only the
  natural motion (the one-armed scratch) alive. Driven by `_idle_bounce` on `frame_changed`, which reverses
  playback (`play` ⇄ `play_backwards`) **at** each edge so it never wraps past it. Defaults suit every
  enemy: `idle_loop_from = 1` (skip just the intro), `idle_loop_to = 0` → the **last** idle frame. Set an
  explicit `to` to keep tail frames out of the bounce; a range under 2 frames wide just sits. This is the
  **universal** idle behaviour now — the old `idle_loop_time` full-cycle flourish is gone.
- **Combat vs resting idle.** An `_engaged` flag tracks whether the player is in reach (attacking distance).
  Engaged or resting, the idle **breathes via the same bounce** (a paused sprite reads as a bug); `_engaged`
  only gates patrolling. The moment the player leaves reach `_engaged` clears and normal patrol/idle resume.
- **Attack feel — hit-stop + shake.** On the impact frame (melee contact / the
  ranged smash), `_begin_hitstop()` freezes the sprite on that pose for
  `attack_hitstop` s and jitters it by up to `attack_shake` px (decaying to 0),
  giving the blow weight; the physics loop resumes the swing afterward. Both
  default on (0.18 s / 2.5 px); set either to 0 to disable.
- Carries its own **hurtbox**, **floating health bar + name**, and a **red
  hit-flash**. Attacks carry `*_knockback` / `*_stun` (see below). The health bar is
  **colour-coded by fill** — green when healthy, orange as it drops, red when low
  (`FloatingHealthBar.ratio_colors`; the thresholds/colours live in one place,
  `color_for_ratio`, so the HUD player HP bar reads from the exact same bands).
- **Status pips** (`scripts/combat/StatusIcons.cs` → `StatusIcons`). A small row of tinted icons
  sits just to the right of the health bar, one per active status — **reap** (dying/DoT), **stun**,
  **charm** (frenemy); **slow** is reserved for a future effect. `Enemy._refresh_status_icons()`
  recomputes the active set each frame from the enemy's own timers and only redraws when the set
  changes (a joined-key compare). Icon **art** comes from the shared `Icons` registry under
  `status:<id>` keys and the tint/label from `configs/StatusTypes.cs` (`StatusTypes.DEFS` + `ORDER`
  for the fixed left→right slot order), so all four are **temp placeholders** today — swap the paths
  in `configs/Icons.cs` (one line each) when real pips are drawn, no code change. Add a status by an
  entry in `StatusTypes`, a `status:<id>` path in `Icons`, and one line in `_refresh_status_icons`.
- **Over-head halo** (`scripts/combat/OverheadStatus.cs` → `OverheadStatus`). The over-head twin of the
  pips: a looping animation that **hovers over the enemy's head** while a status is active. Two today: the
  swirling-stars **stun halo** (`sprites/things/state/stunned.png`, 256×64 / 4 frames) and the pulsing
  skull **dying halo** for a reaped enemy (`sprites/things/state/dying.png`, 768×64 / 12 frames — only
  Twin Reaper's `reap` DoT applies it for now). Built in code (no scene), it's fed the **same
  active-status set** as `StatusIcons` from `_refresh_status_icons()` and shows the highest-priority status
  (by `StatusTypes.ORDER`, so `reap` beats `stun`) that has an over-head anim — one at a time — bobbing
  gently. The anim/scale/`y_off` come from `StatusTypes.OVERHEAD` (sliced once into a shared, cached
  `SpriteFrames`), so giving another status its own halo is a config line + art — no code change. Anchored
  at the enemy's head line (just under the floating bar); pixel-filtered like the rest of the art.
- **Floating text** (`scripts/combat/FloatingText.cs`, Risk-of-Rain style): a general, config-driven
  label emitter — `FloatingText.emit(type, host, local_pos, text, magnitude, overrides)`. It parents the label to
  the `host` and animates it (an explicit per-frame lerp, no Tween) in the host's *local* space, so it
  rides above a moving enemy/player and is immune to both the camera chasing the player and the host's
  own knockback/patrol (the two things that dragged world-space / screen-space versions across the
  screen). **Every label TYPE is a preset** in [`configs/FloatingTextTypes.cs`](configs/FloatingTextTypes.cs) —
  its own size/colour (fixed or magnitude-ramped), font, `italic` slant, and independent in/out
  transition — so different events read and animate distinctly with no code change. The only live type
  live types today are the **`damage`** number over enemies (white → hot gold; `damage_special` =
  magenta), emitted as `FloatingText.emit("damage"/"damage_special", enemy, …, amount)` off the
  `enemy.damaged` signal in `RunManager._on_enemy_damaged`; and the **`player_damage`** number over
  Khalid, popped in `Player.take_damage` for the actual HP lost (after Thick Hide / Jnoon mitigation).
  Its colour is **overridden per-call with the run's chosen PRIMARY (hair) colour** —
  `overrides {"color": PaletteConfig.picks["hair"]}`, the same source that recolours his hair (default
  red `#941E1E` when unpicked) — so his own numbers match his palette. The optional `overrides` dict
  patches any preset key for one call (it wins over the preset). Add a label type = add a row to the
  preset table (the file keeps a commented word-callout example — a parry "Nice", a "LEVEL UP").
- **Death** — on lethal damage it enters the `DEAD` state (AI + collisions off, no more
  hits) and **leaves the `enemies` group immediately**, then, if it has a `death` sheet,
  plays that animation once and **vanishes the instant it finishes** (`_on_anim_finished` →
  `queue_free`) — the animation plays out in full with no lingering hold or fade on the last
  frame. An enemy with **no** `death` sheet has no animation to play out, so it does a straight
  alpha-fade instead (`_fade_and_free`). Leaving the group the instant it dies matters for
  **homing**: the node lingers for the death anim's duration, so a tracking shot re-checks
  `is_in_group("enemies")` every frame (`scripts/combat/Projectile.cs::_target_alive()`) and **straightens
  onto its launch heading the moment the target dies** instead of curving down into the corpse.
  `_has_death` is inferred from the art, same as `_has_melee` / `_has_ranged`. `_die()` also **clears the
  UI overlays immediately** — hides the health bar, empties the status pips + stun halo, and stops the
  body-tint throb (`StatusOverlay.clear()`) — because `_physics_process` bails on `DEAD`, so nothing else
  would clear them and they'd otherwise linger frozen on the corpse for the death anim's ~2s.
- Exposed knobs: health, speed, patrol, ranges, cooldown, damages, knockback,
  stun, hitbox sizes/offsets, aggro, contact damage, `attack_hitstop` /
  `attack_shake` (the freeze + vibrate on the fire frame — stretch it for a
  channel), and **`body_size` / `hurtbox_size`** (per-enemy colliders, so a bigger
  or smaller enemy fits its own sprite instead of a shared hardcoded box). Tune per enemy.

> **Bosses are not Enemies.** They get their own scene/script so their move-sets
> aren't constrained to melee/ranged. `Enemy` is for regular mobs.

### Nasen — a sleeper (`scripts/enemies/SleeperEnemy.cs`, `scenes/nasen.tscn`)

A worked example of a **custom enemy that subclasses `Enemy`**: it reuses all the
infrastructure (sprite / hurtbox / health-bar / hit-flash / death / hit-stop) and only
overrides the AI (`_act`) and the attack/hurt hooks. He has **idle + attack_aoe + death, no
walk**, so he never patrols — he **sleeps in place**. When the player comes within
`rage_zone` (and on his level) he wakes and **RAGES** (a new `Enemy.State`): the `attack_aoe`
loops and, on its hit frame, a **ground AoE erupts around him** — a hostile `Strike` built
in code with a wide centred hitbox plus a particle-only look
(`vfx/enemy/nasen/attack/nasen_rage.tscn`, rising floor flames). Leave the zone and he
keeps raging for `rage_linger` (2s) before dozing off.

- **Melee stuns him, projectiles don't.** He reads the new **`Hit.ranged`** flag: a
  strike (`ranged = false`) halts his rage for `rage_stun_time` (~1.5s), then he wakes and
  starts over; a projectile (`ranged = true`) only chips his health. So **shooting him
  from range is the safe way in** — melee is riskier but interrupts him.
- **Wake once, then loop the yell.** His `attack_aoe` sheet is `[wake, yell, yell, yell]`. He
  re-plays it each rage cycle, but on every cycle after the first it restarts at the anim's
  **`loop_from`** (set in `gen_spriteframes`) — so the wake plays once and only the yell
  repeats. That looping is a **reusable `Enemy` capability**, not nasen-specific:
  `Enemy._loop_from(anim)` reads the generator's `loop_from` metadata and `_replay_from(anim,
  frame)` re-plays skipping the lead-in — any enemy or subclass calls them; the caller just
  decides *when* to loop (nasen: still raging; generic melee: player still in reach — see
  `attack_loops`).
- Spawned via the roster's **`scene`** key (below), not the default `enemy.tscn`.

### Ein — a floating kamikaze (`scripts/enemies/DiverEnemy.cs`, `scenes/ein.tscn`)

A second custom subclass, and the first that **floats**. Ein is an orb with a dagger in its
eye. He overrides `Enemy`'s grounded `_physics_process` entirely — **no gravity, floor, or
edge patrol** (he sets `collision_mask = 0` and moves by `global_position`, not
`move_and_slide`) — while reusing the sprite/hurtbox/health-bar/hit-flash/death as usual. His
loop:

- **Patrol** — drifts between his patrol points with a gentle vertical **bob**, wearing the
  `walk_trail` effect *if* one is configured (it's optional — see below).
- **Detect → lock → charge** — when the player enters `detect_range` (a radius), he **locks the
  player's position at that instant** as a fixed target, swaps to the aggressive `attack_trail`,
  and flies straight at that point in the **`CHARGE`** state (a new `Enemy.State`), the `attack_kamikaze`
  (stab) anim **looping** the whole dive (`OVERRIDES` `("ein","attack_kamikaze"): loop`). He does **not**
  re-track — dodging out of the way makes him miss.
- **Erupt on arrival** — reaching the locked point (hit or miss) he **explodes**: a hostile
  `Strike` (box hitbox from `explosion_*`, centred on the orb via `explosion_offset`, `ranged`)
  plus the `explosion` burst, then his **death burst** plays and he's gone.
- **Erupt on contact (any time)** — a body-sized **contact detector** (`_build_contact_detector`,
  a bare `Area2D` scanning `L_PLAYER_HURT`, not a Hitbox — no damage, no flash) erupts him the
  instant the player *touches* him, patrolling or charging, so you can't just walk/jump into him
  for free. It reuses the same `_arrive()` eruption (deferred out of the physics area-flush so
  spawning the blast's hitbox is legal); the blast's AoE does the damage, catching the
  point-blank player. **A dash passes through safely** — the player's hurtbox is `monitorable =
  false` during the dash lunge, so the detector never sees them and the blast can't hit them.
- **Killed first** — a lethal hit before he arrives (even before he ever detects you) just plays
  the same death burst; no explosion. His `_on_hurt` takes damage + flashes but **never stuns or
  knocks him back** — once diving he commits.
- Trails are swapped by state (`_set_trail`). When a trail is swapped or Ein dies it's **retired,
  not culled** — `Nodes.retire_particles` re-parents it into the level and stops it emitting so
  its airborne wisps **dissipate** rather than vanishing with him (a child emitter would otherwise
  be freed along with its owner).
- **Which** scene each effect emits, **where**, and **whether it exists** are all config, not
  code: **the `Emitters` config** (`ein → walk_trail / kamikaze_trail / kamikaze →
  {scene, pos}`), read via `Enemy._vfx_scene` / `_vfx_pos` / `_make_vfx`. It's **authoritative** —
  delete a row and that emitter is gone (so Ein ships with **no** `walk_trail` row = no walk
  trail). One file controls every enemy's emitters; see `vfx/README.md`.

### Combat model (`scripts/combat/`)

Damage flows **Hitbox → Hurtbox**, with teams enforced by physics layers (see
`[layer_names]` in project.godot and `configs/Combat.cs`), so by default there's no friendly
fire and no group checks — a box scans only the opposing team's hurt layer. (Opt in
per attacker with `friendly_fire`: `Combat.hurt_mask(hostile, true)` also scans its own
team's layer, and the Hitbox skips its own `source` so it never hits itself. Used for
the enemy `friendly_fire` flag above.)

- **`Hurtbox`** (Area2D) receives hits and relays them via a `hurt` signal; the
  owner (player/enemy) turns that into `take_damage`.
- **`Hitbox`** (Area2D) deals damage while active, once per activation, and
  carries optional **`knockback`** (px/s shove away from the source) and
  **`stun`** (seconds frozen). Melee boxes toggle on for their active frames;
  projectiles stay on for their life.
- The **player's** hurtbox + attack hitbox are built in code (`_build_combat`),
  like the particle director, to avoid touching `player.tscn`. Light-attack
  hits fire on each combo hit frame; the special lands on its authored
  `special` hit frame (or the middle frame if none). Whoever is hit applies
  the knockback/stun and takes a brief stagger.
- **`Combatant`** (`scripts/combat/Combatant.cs`, C#) is the shared base for `Player`
  and `Enemy` (both `: Combatant`, itself a `CharacterBody2D`). It holds the
  pieces they'd otherwise each reimplement: `anchor_to_feet` (sprite offset),
  `make_box` (rect collider), `apply_knockback` (turns a `Hit`'s knockback into a
  shove + returns the stagger time), and two "took a hit" tells:
  - `flash(sprite)` — the plain red modulate flash.
  - **`hit_react(sprite, damage)`** — the punchy one enemies use: a white-hot HDR
    flash (blooms) **plus a feet-anchored squash**, both scaled by damage. It fires
    even at `knockback = 0`, so a flurry like ora_ora reads as impacts instead of a
    flat tint. Squash uses `sprite.scale` (enemies flip via `flip_h`, so scale is
    free); it re-punches cleanly on rapid hits. Feel constants live on `Combat`:
    `KNOCKBACK_POP`, `MIN_STAGGER`, `STRIKE_ACTIVE`, `HIT_FLASH`, `HIT_FLASH_TIME`.
- **`StatusOverlay`** (`scripts/combat/StatusOverlay.cs`) engulfs a stunned body in
  an additive tint that mirrors its pose (frame/flip/offset/**scale**) and **throbs**
  for visibility. Driven by a `Hit`'s `status_color` / `status_time`; Khalid's
  `special_stay` sets a red HDR colour (`>1`, so the bloom makes the frozen enemy glow).

### On-hit effects — the `Hit` object

An attack delivers a `Hit` (`scripts/combat/Hit.cs`) — `amount`, `knockback`,
`stun`, `source`, `ranged`, an optional status overlay (`status_color` / `status_time`),
and an optional **reap DoT** (`dot_percent` / `dot_time` — fraction of the victim's max health
drained per 1s tick, and for how long; see Twin Reaper's Reap above).
A `Hitbox`/`Projectile` fills one in; the victim's `_on_hurt(hit)` applies it. Add
a new effect field here and nothing else's signature changes. **`ranged`** marks the hit as
coming from a projectile (`Projectile` sets it on its box; melee `Strike`s leave it false),
so a victim can react by attack type — e.g. nasen is stunned by melee but not projectiles.

- **Enemy attacks** set their fields via exports: `close_knockback/stun`,
  `far_knockback/stun`.
- A knockback always carries a short stagger, or the AI/input would overwrite the
  shove velocity the next frame and nothing would move.
- **Freeze:** while stunned an enemy **pauses on whatever frame it was on** (it does not
  snap to idle), resuming when the stun ends. A follow-up hit takes `maxf(remaining, new)`
  of the stun times — it can *extend* a stun but never cut a long one short (so a jab on a
  stay-stunned enemy won't wake it early).
- **Two ways to dress a hit on the victim** (compose either/both):
  - **`status_color` / `status_time`** — an additive tinted copy of the sprite
    (`StatusOverlay`) that throbs over the victim for the duration. Cheap, code-only.
  - **`victim_effect`** (a res:// **scene** path in the tuning) → `Hit.victim_vfx` — a
    **custom VFX scene spawned on the victim**, the dynamic per-attack hurt reaction.
    `Combatant.spawn_victim_vfx` parents it to the victim (so it tracks their position),
    scales it to fit (`fit_h` / `VICTIM_VFX_REF_H`), and frees it after `victim_time`
    (defaults to the stun/status window) with a fade — or, if that's 0, lets the scene
    free itself (a one-shot burst). This is the extensible seam: a stun aura, a slam
    shock, a burn, an ice crust — one scene each, no code per effect.
    - **Positioning is the scene's job.** The effect is parented at the victim's
      **origin = its feet** (sprites are feet-anchored), so the effect scene's ROOT
      `position` is an offset from the feet: `y = 0` at the feet, **negative y up** the
      body. Adjust it in the scene, not in code. Examples: `stay_stun.tscn` sits at
      `(0, -17)` to engulf the torso; `ground_breaker_stun.tscn` sits at `(0, 0)` to
      erupt from the feet. `fit_h` scales the effect's size; the authored position is
      left as-is.
    - Khalid's specials both use it — `res://vfx/status/stay_stun.tscn` (a bubbling red
      column from the stay pulse texture) and `res://vfx/status/ground_breaker_stun.tscn`
      (a burst up from the feet).

### Player attacks — an `Action` (`hit` tuning) + a spawned `Strike` / `Projectile`

An **`Action`** (`configs/Action.cs`) is what a character performs — typed identity
(`id`/`name`/`icon`), a `category` + cadence `style`, a `tier`, its `animation`, an optional
`cooldown`, and — when it deals damage — a **`hit`** (a `StrikeSpec`: delivery `type` + per-segment
tuning). Actions are pure code-config, one catalog per character (`configs/ActionsKhalid.cs`),
reached through **`Actions`**. Presentation is deliberately NOT on the Action: its `animation` is the
key the particles (`EmittersCharacters`) and sounds (`SfxCharacters`) hang off, so retexturing a move
never touches its data. (This replaces the old `Move`/`configs/ActionsKhalid.cs`.)

There's **no built-in attack box** any more. Every attack is a **spawned node** that
carries its own `Hitbox`: a **`Strike`** (`scripts/combat/Strike.cs` — a melee slash /
blast / ground AoE that stays at the body) or a **`Projectile`**
(`scripts/combat/Projectile.cs` — a shot that leaves the body, used by players *and*
enemies via a `hostile` flag). The `ParticleDirector` fires it on the attack's authored
frames and feeds it the hit's numbers from the **`Action`'s `hit`** — so combat numbers
live in one place, in code, never baked in a `.tscn`.

Each action's `hit.segments` (one tuning dict per combo hit — a single-element list for a one-hit
attack; `hit` is `null` when the effect scene carries its own numbers):

| field | meaning |
|---|---|
| `damage` | hit damage |
| `knockback` | px/s shove away from the attacker |
| `stun` | seconds frozen |
| `reap` / `reap_time` | reap DoT: fraction of the victim's max health per 1s tick, for this long (Twin Reaper) |
| `color` / `color_time` | engulfing status overlay + duration |
| `victim_effect` / `victim_time` | custom VFX scene spawned on the victim + its lifetime |
| `frenemy` | seconds to charm an enemy into a temporary ally |
| `x` | hitbox forward reach (mirrors with facing) |
| `extents` | hitbox half-size |
| `lunge` / `super_armor` / `multi_hit` | `Strike` wielder-effects — dormant hooks the buff system will use |
| `buff_time` / `speed_mult` / `invuln` / `buff_effect` | **self-buff special** fields (see below) |

**How a hit reaches the box (and the buff seam):** on each segment/special start the
player resolves the effective tuning via **`resolve_tuning(action, seg)`** (→ `action.segment(seg)`)
into `_active_hit`. This is the **live buff seam**: after the global reward mults (`damage_mult`,
`attack_reach_mult`) it loops `_passives` calling **`modify_tuning(player, action, seg, tuning)`**, so a
per-move/shared **Buff** layers its changes here (e.g. *Reaper's Edge* +25% on Twin Reaper only). A new
tuning key a buff injects must be handled by the consumer (`Strike.apply_tuning` / `Projectile.apply_tuning`).
When the director arms the attack's `Hitbox` it calls
`_inject_tuning`, passing `_active_hit` to the node's `apply_tuning()` — which sets
damage/knockback/stun and, for a `Strike`, resizes the box from `extents`/`x` and fires
lunge/armor. A **null** `hit` (empty `segments`) means "the effect scene carries its own numbers"
(cherry_shots, whose two shots have different damage one dict can't express — they're per-frame scenes).

- multi-segment combos stay segment-per-click; each segment resolves its own tuning, so the
  three spear hits keep different reach + damage. A combo's `Emitters` config frames must
  match its `HIT_FRAMES` (one effect spawn per segment).
- The hit's **`type`** (`StrikeSpec.Type`: MELEE / PROJECTILE / AOE / BLAST / …) is
  descriptive metadata for the move-select / build UI — it does **not** drive behavior.
- **Self-buff specials** (no enemy hitbox): a special whose tuning carries `buff_time`
  turns into a timed buff on the *caster* instead of an attack. `_start_special` calls
  `apply_self_buff`, which grants `buff_time` seconds of `invuln` (the hurtbox stays off —
  folded into the per-frame `monitorable` calc, same channel as dash i-frames) and a
  `speed_mult` on `run_speed`, wrapped in the aura scene at `buff_effect` (parented to the
  player, freed on expiry). It ticks down in `_physics_process` and clears on death /
  run-restart. **No shipped special uses it right now** — the old *Built Different* / Impervious
  invuln now lives in the **Aegis surge** (a passive on the `surge` button, see **Surges** above) —
  but the seam is live for the item/build system: drop
  `buff_time`/`speed_mult`/`buff_effect` on any special and it becomes a self-buff.
- **Projectile attacks** put `Projectile` nodes (not `Strike`s) in the effect scene; the
  director world-parents them at the muzzle and reads facing from `scale.x` so they fly
  off. Khalid's **Cherry Shots** fires two — a small bolt on frame 3, a big one on frame 7,
  each its own per-frame file (`attack_cherry_shots_3/_7.tscn`), a red laser `Line2D` bolt
  with its own damage from the tuning array. Both **home on the closest enemy ahead**: the
  emitter rows `set` `homing = 8` + `can_fly_up` (overriding the scenes' `homing = 0`), so each
  shot acquires the nearest target via `Projectile.NearestTargetAhead` and steers into it,
  including one a level up (`can_fly_up` skips the `vertical_reach` gate + allows upward steering).
- A character with **no effect scene** for an attack deals no damage (Khalid, for now);
  a character with an **empty specials pool** (`Actions.get_action` returns null) simply can't
  special — the button no-ops.

### Dash i-frames

Dashing is **invulnerable** — the player's hurtbox stops being detectable for the
dash's duration (`_hurtbox.monitorable` is off while in `DASH`), so you can dash
through projectiles and attacks unharmed.

### Spawning & the run

`scripts/run/RunManager.cs` (`RunManager`, the `arena.tscn` root) builds ONE continuous arena in code, then
trickles enemies in at a steady rate. See [`scripts/run/README.md`](scripts/run/README.md) for the full loop;
the build basics:

- **Terrain** — a hand-painted `TileMapLayer` with per-tile collision, loaded from a random `stage1_v*.tscn`
  layout (see the run README + `docs/painting-levels.md`). Its exposed ground tops feed proximity spawning.
- **Enemies** — `SpawnWave()` fires every `SpawnInterval`, picking `EnemiesPerWave` **kits** at random from
  `RunManager.SpawnPool` (up to a `MaxAlive` cap). A **kit** (`EnemyKits.KEBUS`, …) is either an `id` (built
  from the generic `enemy.tscn` with that `enemy_id`) or a `scene` (a custom enemy — `sleeper_enemy.tscn`,
  `diver_enemy.tscn`), plus any Enemy `@export` overrides. `RunManager.SpawnEnemy` applies them; the enemy's
  `died` signal frees a cap slot and rolls the Fada-Fig + buff drops.
  On a Ruh-granting kill it also pops a **Ruh soul** (`vfx/shared/ruh_orb/`, `RuhOrb`): a glowing
  crimson orb that flies a **curved, parabolic path** to the player — a quadratic Bezier from the
  death spot to the player's live position, bowed by `arc_height` — always reaching him at the end
  of `flight_time` (arrival time, not a give-up cap; it only bails if the player is gone). On contact
  it **shrinks into his chest** (`absorb_time`) and **surges Khalid's hair gradient** toward an
  absorb palette and smoothly back — `Player.on_ruh_absorbed` → `_hair_surge`, driving the tint
  shader's `base_red`/`accent_a`/`accent_b` on a per-instance **duplicated** material (so it never
  writes back to the shared `.tres`); stronger/longer for the soul that **completes a full Ruh
  charge**, and rate-limited (`RUH_FLASH_REFRACTORY`) so a cluster of arrivals folds into one surge
  instead of strobing. Ruh is banked at the kill, not the arrival (`RunManager._spawn_ruh_orb` passes
  the charge flag), so the special stays available immediately; the absorb palette (`HAIR_ABSORB_*`
  in `Player.cs`) is the knob to play with. World-parented + no Area2D, so it's safe mid-physics-flush.
- **Camera** follows the player in **`_physics_process`** with a smoothed `lerp`,
  so it tracks at the same rhythm as the player (see below) — you can traverse
  across.
- **Drop through a platform** — tap **`drop` (S / ↓ by default)** while standing on
  a one-way platform to fall through it; on the solid floor it's a no-op. `drop` is
  its own remappable action (controller: D-pad down / left-stick down), so jump is
  now purely jump. `_drop_through_platform()` finds the platform under the feet via
  the slide collisions (only bodies in the `oneway_platform` group qualify, so you
  can't fall through the ground), adds a brief collision exception, and removes it
  after `DROP_THROUGH_TIME`.

#### Pixel-crisp motion (why running isn't blurry)

The real culprit on a high-refresh monitor (144/240Hz) is the **physics tick
(60Hz) vs refresh-rate mismatch**: without interpolation the character's position
only updates 60×/sec, so it judders/smears no matter how crisp each frame is.
Fixes, all in `project.godot`:
- **`physics/common/physics_interpolation`** — renders nodes smoothly *between*
  physics ticks. This is the main fix. Camera + follow run in `_physics_process`
  so both interpolate together; teleports (spawn, respawn) call
  `reset_physics_interpolation()` (`_place()`) so they snap instead of smearing.
  > **Gotcha:** anything `add_child`'d and *then* moved to a spawn point (enemy
  > projectiles / the ground wave, a world-anchored particle burst) must call
  > `reset_physics_interpolation()` after positioning — otherwise it interpolates
  > from the level origin to the spawn spot on the first frame, flashing the
  > effect (and its world-space particles) scattered across the level.
- **`snap_2d_transforms_to_pixel` + `snap_2d_vertices_to_pixel`** — render on
  whole pixels so the interpolated positions stay crisp pixel art.
- **`default_texture_filter = Nearest`** — no linear blur when scaled.

Separate from rendering: a run can still *read* as smeary if the character glides
faster than its legs cycle (**foot-sliding**). `_update_animation` ties the run's
playback to ground speed (`speed / run_speed × run_anim_speed`, clamped), so the
legs keep pace — busier sprinting, slower starting. `run_anim_speed` (default
1.5) is the knob.

> If it *still* looks smeared while moving but each single frame is sharp when you
> pause a screen recording, that's **sample-and-hold display blur** (LCD + eye
> tracking), not a game bug — only higher framerate or lower background contrast
> reduces it.

Separately from rendering sharpness, a run can *read* as smeary if the character
glides faster than its legs cycle (**foot-sliding**). `_update_animation` ties the
run's playback speed to actual ground speed (`speed / run_speed × run_anim_speed`,
clamped), so the legs keep pace — busier when sprinting, slower when starting —
instead of a fixed fps that desyncs the moment speed changes. `run_anim_speed`
(default 1.5) is the tuning knob.
- **Death (0 HP)** — a lethal hit puts the player in the **`DEATH`** state (via
  `_die()` from `take_damage`): input is frozen, the hurtbox turns **off**, any
  swing/channel is cancelled, and the `death` animation plays once. It auto-fires the
  character's own `death/default/` particle (tinted to their dash palette) from
  the `Emitters` config on the **last** death frame; then the **sprite hides**
  (`_death_finished`) so the character *vanishes into that poof* instead of the dead
  frame sitting there until restart (`begin_run()`/`_enter` restores it). **Enemies stop
  attacking**: `Enemy._player()` returns
  `null` for a dead player, so the zone goes quiet. `RunManager` waits for
  `death_complete()` (+ a short `DEATH_HOLD`), then **restarts the whole run** — rebuild level 1
  + `Player.begin_run()` (full HP / a full 3-charge Ruh meter, run-reward buffs cleared). Death is a real fail state
  now (roguelite), not a free respawn.
- **Death cinematic** — a staged sequence: the death anim **freezes on its first frame** while the
  camera **punches in hard** (`CAM_ZOOM_DEATH` 3.0 vs the 1.5 rest zoom) and **the whole world fades
  to black behind him**; then the collapse **plays out on the void**; then respawn clears the black.
  A **death tune** (`Sfx.play("player_death")`) fires in `Player._die`, and the level music **ducks out**
  (`Music.stop`) so it plays clear; the **respawn waits for the whole tune to finish** — `_handle_death`
  gates the restart on `_death_tune_left` (seeded from the stream's `get_length()`) *and* `death_complete`,
  so a long death jingle is never cut off (the level music fades back in when the fresh level restarts it).
  Mechanism: `_die` →
  `_enter(State.DEATH)` **plays + pauses** `death` on frame 0 (`_death_frozen`; `_update_animation`
  won't re-play it, so the pause holds). `RunManager._begin_death_cinematic` lifts Khalid's `z_index`
  to `DEATH_PLAYER_Z` (500) and fades in a huge black `Polygon2D` at `DEATH_OVERLAY_Z` (400) — above
  every world node (z ≤ 0), below him — parented to the camera; after `DEATH_FREEZE` (0.5s) it calls
  `Player.release_death()`, which unpauses the anim so it plays through to `death_complete()`. On
  restart, `_end_death_cinematic` fades the black out (`DEATH_FADE_OUT`) and drops his z back. All the
  timing consts (`CAM_ZOOM_DEATH` / `DEATH_FREEZE` / `DEATH_FADE_IN` / `DEATH_HOLD` / `DEATH_FADE_OUT`)
  live at the top of `RunManager` — tune/disable there.
- **Falling off** — dropping below `DEATH_Y` (alive) just **repositions** you to the level's
  spawn point — no life lost, no death anim. Only a lethal *hit* ends the run.
- **Spawn (materialize)** — every (re)spawn — the initial game start *and* every respawn —
  enters the **`SPAWN`** state: input is frozen and the hurtbox is **off** (spawn
  protection, so it always plays fully) while the `spawn` animation plays, auto-firing the
  character's own `spawn/default/` particle (tinted to their dash palette) on its **first**
  frame; `_on_animation_finished` hands off to idle. `Player.spawn()` drives it (called by
  `RunManager._ready` for the initial spawn and by `begin_run()` on a run restart); a
  character with no `spawn` sheet just drops straight to idle.
- **Spawn flair** — the camera **zooms in** (`CAM_ZOOM_SPAWN`, same 2.25 as the death
  punch-in) and centres on the materializing character while `SPAWN` plays, then **pulls
  back out** to normal the instant it ends. Because the spawn and death zooms match, a
  death → respawn → spawn stays smoothly zoomed the whole way and only reveals the level
  once you have control. Also in `RunManager` — tune/disable there.
- **Dev key `0`** rebuilds the arena fresh (`RunManager.BuildArena`) — a quick way
  to reset while iterating.

---

## HUD

`scenes/hud.tscn` + `scripts/HUD.cs` — portrait, name, health bar, Ruh charge
meter, and a **`LEVELS n · BEST n`** line (levels cleared this run + the best-ever
record). The HP bar's fill is **colour-coded green→orange→red by how full it is**,
retinted each frame as it drains (`_recolor_hp`) from the shared
`FloatingHealthBar.color_for_ratio` bands — so the player and the floating enemy
bars use identical thresholds.

Registered as an **autoload** (`project.godot > [autoload]`), not placed in a
scene. It finds whatever `Player` enters the tree via `get_tree().node_added`,
and hides itself when there is none, so menus and character-select screens stay
clean. This also means no scene file holds a reference to it.

It follows character swaps and health changes over signals — nothing polls.

### Off-screen enemy arrows (`scripts/ui/OffscreenMarkers.cs`)

With the tight 6× camera and the big orb launches, enemies leave the frame constantly — so you can't
see where to slam/approach. `OffscreenMarkers` is a full-viewport overlay the HUD builds (a sibling of
the top-left cluster, shown/hidden with it). Each frame it projects every enemy in the `"enemies"`
group through the camera — `get_viewport().get_canvas_transform() * enemy.global_position` — and for
any that land **outside the view** it draws a **chevron clamped to an inset screen edge**, rotated to
point at the enemy. On-screen enemies get nothing (you can already see them). Each arrow is:
- **tinted per enemy** so you can tell which is where — `EnemyMarkers.color_for(enemy_id)`
  (`configs/EnemyMarkers.cs`: kebus gold, baghel purple, nasen blue, mazab crimson, ein orange, matat
  orange-red, tarri yellow-gold, breski blood-red; tune
  there), and
- **faded + shrunk by world distance** from the camera centre (`FADE_START`/`FADE_END`), so a nearby
  off-screen enemy reads bold and a distant one is small/faint.

It's self-contained (finds enemies + the camera itself; no per-frame wiring) and screen-space, so it's
immune to the zoom. Tunables (`MARGIN`, `SIZE_*`, `FADE_*`, `ALPHA_*`) live at the top of the script.

### Persistent record — `SaveData` (`scripts/SaveData.cs`)

The best-ever *waves survived in one run* survives between sessions (levels are retired). `SaveData` is
an all-static helper backed by a `ConfigFile` at `user://save.cfg`:
- `RunManager.SpawnWave` writes `SaveData.SetCurrentWaves(_waveCount)` each tick; on death
  (`RestartRun`) it calls `SaveData.ReportRun(_waveCount)`, which persists a new best (key `waves_record`).
- The HUD reads `SaveData.GetCurrentWaves()` / `SaveData.WavesRecord()` each frame
  (in-memory after the first load — no per-frame disk I/O), shown as `WAVES n · BEST m`.

It's the first thing saved to disk; add future persisted stats as more keys in the
same file.

---

## Gotchas

### Texture filtering: pixel art vs painted art

The project default is **nearest** (`default_texture_filter=0`) for crisp pixel
art. The portraits are 1080x1080 paintings shown at ~104px, and nearest-
filtering a 10x downscale looks terrible.

So the portrait node overrides `texture_filter = 4` (linear + mipmaps), and the
portrait imports have `mipmaps/generate=true`.

**Rule: pixel art inherits the project default; painted or hi-res art needs the
per-node override plus mipmaps.**

### The Godot editor overwrites scene files

The editor holds open scenes in memory and writes its copy over anything changed
on disk. If a scene is edited outside Godot while that scene is open, the editor
silently wins on its next save.

Related: adding a new `@export` to a script while a scene is open makes the
editor serialise the unknown property as `null` on the instance
(`max_health = null`), which overrides the script default.

**This applies to generated `.tres` files too.** If a character resource is open
in the editor's inspector when `gen_spriteframes.py` runs, the editor writes its
stale copy back and that one character silently keeps the old animation set.
`verify_frames.gd` catches it — a mismatched canvas size in its output means
exactly this.

**If you edit scenes or resources outside the editor, close the tab first**, or
use `Project > Reload Current Project` afterwards. This is why the HUD is an
autoload rather than a node in `level.tscn`.

### GDScript LSP warnings in VS Code

`godot-tools` talks to the language server inside the running Godot editor, so
it can only serve one project accurately and warns defensively. Stale indexes
show bogus errors like `Could not find type "Player"` on code that compiles
fine. **Trust the actual run over the squiggles.**

---

## Tools

| Command | Purpose |
|---|---|
| `python3 tools/gen_spriteframes.py` | Regenerate SpriteFrames from the sheets |
| Palette cleanup | Lives in the art repo, `mygame/tools/repalette/` — see [Palette cleanup](#palette-cleanup) |
| `godot --headless --script tools/verify_frames.gd` | Assert all animations load on a uniform canvas |
| `godot --script tools/capture_shots.gd` | Render every character/animation to PNGs for eyeballing alignment |
| VFX build tools (particle textures/scenes) | Under `vfx/script/` — see [vfx/README.md](vfx/README.md#build-tools) |

---

## Maintaining this file

Keep this README current. When behaviour, controls, tunables, project settings,
or the art pipeline change, update the affected section in the same pass as the
code change.
