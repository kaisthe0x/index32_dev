# Art direction — world / tiles / stages

The look is a **muted world, bright actors** (the Risk of Rain approach): the environment is low-saturation
and a little darker than its backdrop, with variety from several muted hue families rather than brightness. The
*vivid* colour comes from the actors — Khalid (recolourable), combat effects (HDR red/gold/teal), and enemy
accents. Keeping the environment muted + disciplined is what stops the screen turning to chaos and makes the
actors (and, later, the glow bloom) pop.

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

## The palette recipe (every stage)

Modelled on Risk of Rain: the environment is **low-saturation everywhere** and gets its variety from **several
muted hue families**, not from brightness; everything saturated belongs to the actors.

- **Backdrop carries the stage hue** — one hue, muted (saturation ≈ 0.35–0.45) and mid-dark. Never a fully
  saturated sky: next to one, no terrain colour reads right.
- **Terrain + props = 3–4 muted hue families on shared value ramps** — e.g. a deep body hue, a slate mid-tone, a
  moss/lichen top, a pale bone highlight. Each ramp is **hue-shifted** (shadows cooler, highlights warmer), so a
  single prop spans several families from shadow to tip.
- **Terrain is darker than the backdrop**; the walkable **lip is the lightest terrain band** (reads as "I stand here").
- **One small accent** may echo the backdrop hue (fungus specks, vines) to tie the layers together.

## Tileset spec (match `configs/Terrain.cs`)

32×32 atlas cells:
- **Row 0, cols 0–3 → TOP** (walkable surface). 4 variants, placed randomly. **Tile seamless left↔right.** Put the
  surface line + a 1px neon edge in the top ~4–6px so the standing lip glows; body below transitions to fill.
- **Rows 1–2, cols 0–3 → FILL** (platform body / underground). 8 variants. **Tile seamless all directions.**
  Darkest base, sparse texture (cracks, a dim glint), quiet — it's in shadow below the lip.
- **Decor sheets** (separate, no collision — e.g. `tileset3`: rocks + plants): 32px pieces drawn sitting on the cell's
  bottom edge, painted on the layout's `Decor` layer in the cell above the ground.
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

## Stage 1 palette

A muted plum sky over indigo stone, slate edges and mossy tops. Every stage1 asset (masters in
`index32_art/art/stages/stage1/`, copies in `assets/terrain/stage1/`) is drawn from these ramps:

```
BACKDROP  #422933 … #6c4555                          muted plum (bg1 — hue 335°, saturation ≈ 0.4)
BODY      #221e30   #363548                          deep indigo fill → slate-indigo border (tileset1)
TOP       #4a5566 → #5a705e → #6f8a66 → #86a071 → #9db582   slate fading up into the moss lip (tileset1)
BRICKS    #20222f   #363c50   #474f66   #56607a      muted slate-blue platforms (tileset2)
PROPS     #100d18 → #1f1c2e → #2e2c42 → #4b5166 → #647a63 → #7f9670 → #bcbf9c
          one hue-shifted ramp by value: indigo shadow → slate → moss → bone (statue, trees, skeleton)
```

## Authoring levels — hand-painted layouts (the editor workflow)

Levels are **hand-painted layout scenes**, not code. Structure:
```
scenes/levels/stage1/stage1_v*.tscn         (the arena layouts; RunManager picks one at random per run)
assets/terrain/stage1/tilesetN.png          (tileset1, tileset2, … — modular 32px sheets, see assets/terrain/README.md)
assets/terrain/stage1/terrain_tileset.tres  (the generated TileSet — regen via tools/gen_terrain_tileset.gd)
```
Each layout is a `LevelLayout` scene (`scripts/run/LevelLayout.cs`) containing:
- a **`Terrain` TileMapLayer** you paint (its solid tiles carry collision — **paint = collision**),
- a **`PlayerSpawn`** `Marker2D`,
- a **`Decor`** TileMapLayer for rocks and plants (no collision),
- optional launch-orb spots in the **`orb`** group, and hand-placed decor sprites (statue, trees).

**To author:** see [`docs/painting-levels.md`](painting-levels.md). Enemies spawn automatically on the floor the
player is standing on. Regenerate the TileSet (`tools/gen_terrain_tileset.gd`, editor closed) after adding or editing a
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
