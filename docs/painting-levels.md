# Painting levels (TileMapLayer workflow)

Levels are **hand-painted layout scenes**. Paint = collision: the tiles you paint are the ground Khalid and the
enemies stand on. Tilesets and how to add them: [`assets/terrain/README.md`](../assets/terrain/README.md).

## What a level is

`scenes/levels/stage1/stage1_v1.tscn` (more variants: `stage1_v2.tscn`, … — any `stage1_v*.tscn` in the folder joins
the random pool automatically). Each is a **`LevelLayout`** scene (`scripts/run/LevelLayout.cs`) with:
- a **`Terrain` TileMapLayer** — its **Tile Set must be `assets/terrain/stage1/terrain_tileset.tres`** (the shared,
  generated one). Never "New TileSet" in the inspector: an embedded copy is invisible to the generator, so new
  tiles and collision fixes would never reach that level.
- a **`PlayerSpawn`** `Marker2D` (where Khalid drops in), and optional launch-orb spots in the **`orb`** group,
- decor sprites (trees, statues, bushes) — no collision.

Enemies spawn around the player on exposed ground tiles automatically — no spawn markers needed.

## Paint

1. Open the level scene, click the **`Terrain`** node — the **TileMap** panel opens at the bottom.
2. Pick a **source** (tileset1, tileset2, …) in the palette, click a tile, and **left-click / drag** in the viewport.
   **Right-click** erases. Tools: Paint, Line, **Rect** (fastest for ground), Bucket.
3. Tiles snap to the 32 px grid. **Save**, then play the arena (F5 from the colour screen, or F6 on `arena.tscn`).

## Designing for Khalid's movement

| Move | Reach |
|---|---|
| Single jump height | ~1.9 tiles (a 2-tile step is just out of reach) |
| Double jump height | ~3.8 tiles |
| Running single-jump distance | ~4.9 tiles |
| Dash | ~2.4 tiles |
| Camera view (normal zoom) | ~24 × 13.5 tiles |

- **Slopes are walkable** (up to 50°) — collision is traced from the art, so a painted 1:1 ramp just works, for the
  player and enemies alike (`Combat.ApplyFloorHandling`).
- Space ledges 1 tile for easy hops, 2–3 tiles where a double jump is intended; use **launch orbs** for bigger climbs.
- Keep horizontal gaps ≤ 4 tiles for a running jump.

## Editor-clobber discipline

The editor overwrites open `.tscn` / `.tres` files on disk. **Close it before regenerating the TileSet** or before
any headless tool edits a level, and reopen afterwards.
