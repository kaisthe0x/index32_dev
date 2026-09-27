# Art direction — world / tiles / stages

The look is **dark neon**: a near-black world lit by a few bright, blooming neon accents. The world is a
**quiet dark stage**; the *colour* comes from the actors — Khalid (recolourable), combat effects (HDR red/gold/
teal), and enemy neon accents. Keeping the environment dark + disciplined is what stops the screen turning to
chaos and makes the neon (and, later, the glow bloom) pop.

## The stage is the art unit

- A **stage** = one **tileset** + one **palette** + one **background theme** + one **ambient-particle theme**.
  Every level in the stage SHARES all of it (no per-level recolour). The **boss level** varies *layout*, not colour.
- **Progression → new stage → new design + new palette.** (The current 5 levels are a temporary pseudo-Stage-1.)
- **Enemies are stage-agnostic** — they move between stages — so their palette must stay stage-NEUTRAL. The grunt
  scheme (dark body + one neon accent, warm-red *or* cool-green) is that neutral backbone; it drops into any
  dark-neon stage. Matat/Tarri (bright warm bodies) are judged against real tiles later.

## Engine facts that set the numbers (don't fight these)

| Thing | Value | Consequence for drawing |
|---|---|---|
| Tile | **32×32 px** | Draw native 32px. Fixed in code + colliders. |
| Filter | **nearest** | Crisp pixel art, no anti-aliasing / soft edges. |
| Camera | **1.5× zoom** in play | A 32px tile shows ~48px; ~**24 wide × 13 tall** tiles visible. |
| Character | 128×80 frame, body ≈ 2 tiles tall | Detail budget per tile is SMALL — bold silhouette + a few accent pixels, not fine detail. |
| HDR 2D | on (glow bloom to be added later) | Draw neon glow *cores* near-white so they bloom once Glow is enabled. |

## The palette recipe (every stage, ~8 swatches)

Same philosophy as Khalid's 36-shade LUT, scaled for environment:

- **4× BASE** — near-black → dark, all **one hue**, low saturation. Tiles fill/body + background. ~90% of pixels.
- **2× SURFACE** — mid-value, brighter: the walkable **lip** + platform edges (reads as "I stand here").
- **2× NEON** — HDR bright (value >1): the stage **signature glow** — plants, props, particle motes, edge highlights.

**The one rule: 1 dark base hue + 1–2 neon hues per stage. Never more.**

## Tileset spec (match `configs/Terrain.cs`)

32×32 atlas cells:
- **Row 0, cols 0–3 → TOP** (walkable surface). 4 variants, placed randomly. **Tile seamless left↔right.** Put the
  surface line + a 1px neon edge in the top ~4–6px so the standing lip glows; body below transitions to fill.
- **Rows 1–2, cols 0–3 → FILL** (platform body / underground). 8 variants. **Tile seamless all directions.**
  Darkest base, sparse texture (cracks, a dim glint), quiet — it's in shadow below the lip.
- **Plants sheet** (separate): row 0 cols 0–1 = ground plants, (0,1) = mushroom. 32px decor stamped on surfaces.
- **Trees/props**: standalone PNGs, any size (tall multi-tile), placed behind/on platforms.

## Background + motion (where "animated / alive" lives — cheap, no collider changes)

- **Parallax: 2–3 layers**, each near-black with a few glowing shapes, drifting slowly (far slowest). Draw wider
  than the view (~1280×720+) or horizontally seamless so they scroll. Keep the far layer very dark/low-contrast.
- **Animated decor props** (looping sheets): a few glowing flora/props that pulse or sway.
- **Ambient particles**: drifting neon motes via the existing emitter system.

## HDR / glow

Draw NEON at HDR intensity — the brightest glow **cores near-white** (of the neon hue), surrounded by the neon
colour. Once a `WorldEnvironment` with **Glow** is added (post real tiles), those cores bloom into soft halos.
Don't enable glow on the current placeholder art — it comes once real neon tiles exist.

---

## Stage 1 — "Arcane Void" (LOCKED palette)

Mysterious near-black **violet** ruins floating in the dark; **violet + electric-blue** glow. Contrasts Khalid's
red strongly. (sRGB hex; push glow cores toward the near-white value for bloom.)

```
BASE     #0B0812   #140E20   #1F1633   #2E2150     near-black → dark violet mass (fill, bg, deep shadow)
SURFACE  #4A3A7A   #6E4CB0                         rune-lit walkable lip / platform edges
NEON     #B44CFF   #4C6EFF                         violet glow + electric-blue accents (plants/props/motes/edges)
GLOW CORE #EAD8FF                                  near-white violet — the brightest bloom centres
```

Per-asset treatment:
- **TOP tiles** — dark violet stone (`#1F1633`/`#2E2150`); top ~4–6px is a `#6E4CB0` lip with a 1px `#B44CFF`
  neon edge (glow core `#EAD8FF` at the brightest points). 4 variants = vary the rune marks / cracks.
- **FILL tiles** — darkest (`#140E20`/`#0B0812`), sparse `#1F1633` cracks, an occasional dim rune glint. Quiet.
- **Plants / mushroom** — arcane fungi / small crystal shards: dark stem `#2E2150`, glowing cap `#B44CFF` or
  `#4C6EFF` (HDR).
- **Trees → crystal spires / rune-trees** — tall props (~48–128px), dark violet trunk `#1F1633` with electric-blue
  `#4C6EFF` glowing veins + a few `#EAD8FF` glow points.
- **Background** — far: `#0B0812` with faint `#140E20` distant monoliths + dim violet star-motes; mid: `#1F1633`
  silhouetted arcane structures with occasional `#4C6EFF` rune-window glows. Ambient: drifting `#B44CFF` motes.

## Authoring levels — hand-painted layouts (the editor workflow)

Levels are **hand-painted layout scenes**, not code. Structure:
```
scenes/levels/stage1/stage1_v*.tscn         (the arena layouts; RunManager picks one at random per run)
assets/terrain/stage1/tilesetN.png          (tileset1, tileset2, … — modular 32px sheets, see assets/terrain/README.md)
assets/terrain/stage1/terrain_tileset.tres  (the generated TileSet — regen via tools/gen_terrain_tileset.gd)
```
Each layout is a `LevelLayout` scene (`scripts/run/LevelLayout.cs`) containing:
- a **`Terrain` TileMapLayer** you paint (its solid tiles carry collision — **paint = collision**),
- **`PlayerSpawn`** + **`Exit`** `Marker2D`s,
- optional launch-orb spots in the **`orb`** group, and hand-placed decor (Tree/plant instances).

**To author:** see [`docs/painting-levels.md`](painting-levels.md). Enemies spawn on exposed ground tiles around the
player automatically. Regenerate the TileSet (`tools/gen_terrain_tileset.gd`, editor closed) after adding or editing a
`tilesetN.png` — collision is traced from the art.

**Editor-clobber discipline:** the editor overwrites open `.tscn`/`.tres` on disk. Author with the editor, but
close/reload before a headless run, and don't hand-edit a scene the editor has open.

## Ambient particles on a prop (e.g. falling leaves)

Pattern: attach a `CpuParticles2D` child to the prop. See `RunManager.AddLeafFall` for a fully-commented example.
The dials that matter: **Amount** (how many at once), **Lifetime** (how long they fall), **Gravity** (low = floaty),
**Damping** (air resistance / drift), **Spread** (fan-out), **Direction/EmissionRectExtents** (where they're born),
**Texture** (swap the placeholder mote for a leaf sprite), and a **ColorRamp** (born-colour → faded-out over life).
`LocalCoords = false` lets them fall through world space instead of riding the prop. Same recipe works for embers,
dust motes, dripping water, floating spores — it's the cheap "make the world feel alive" tool.

## Code plan (for when we implement — NOT yet)

1. Make `Terrain` **stage-aware**: a `StageTheme` record (tileset, plants, trees, bg layers, palette) selected by a
   `StageId` enum — so "add a stage" is data entry. (Same enum/record discipline as the rest of the codebase.)
2. Add a `ParallaxBackground` with the stage's bg layers; retire the per-level flat tint in favour of the stage bg.
3. Add a `WorldEnvironment` + tuned **Glow** to `level.tscn` — after real neon tiles exist.
