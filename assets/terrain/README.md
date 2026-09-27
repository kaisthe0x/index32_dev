# assets/terrain — stage tilesets + stage art

Each stage has a folder (`stage1/`, …) holding its **tilesets** (what levels are painted with) and its standalone
art (background, props).

## Tilesets — `tilesetN.png`

A stage can have any number of tilesets, named by increment: **`tileset1.png`, `tileset2.png`, `tileset3.png`, …**
Each is a different design for the same stage, and each becomes its own palette when painting.

**Authoring rules** (masters live in `index32_art/art/stages/<stage>/tilesetN.aseprite`):
- **32 × 32 px tiles** on a grid starting at the top-left — **no padding, no spacing, no extrusion, no trimming**.
- The sheet can be **any size** that's a multiple of 32 (96 × 96, 128 × 64, 320 × 160 …) — as big as the design needs.
- **One reusable piece per cell**, nothing crossing a cell edge. Draw each unique piece once (a middle surface tile
  is painted many times in Godot — it only needs to exist once in the sheet). Empty cells are fine (skipped).
- Export with *File → Export As…* at 100% (not *Export Sprite Sheet* — that's for animation frames).

**Collision is automatic and follows the art.** The generator traces each tile's collision from its own pixels:
a full tile gets a box, a slope becomes a real walkable ramp, a cut corner collides where it looks solid. So draw
the solid part of a tile as solid (opaque) and the air as transparent — that IS the physics.

## Per-tileset physics

Each tileset can collide differently — set in the table at the top of `tools/gen_terrain_tileset.gd`:

| Setting | Effect |
|---|---|
| *(default)* **solid** | traced collision on the **World** layer — walls, floors, ramps |
| `ONE_WAY_SHEETS` | **jump-through** on the **Platform** layer: land on top, jump up through from below, **drop down through with S / Down** |
| `CELL_OVERRIDES` | one cell's physics against its sheet's: `SOLID`, `ONE_WAY`, or `NONE` (paintable decoration, no collision) |

Current stage1 sets: **tileset1** = solid ground (block, fill, slope) · **tileset2** = floating brick platforms,
jump-through; its support pole (cell 1,1) is overridden to **solid** so it blocks like a wall.

## Adding or updating a tileset

1. Export the PNG into this stage folder as the next number (e.g. `stage1/tileset2.png`), or overwrite an existing one.
2. **Close the Godot editor** (an open editor writes its stale copy of the TileSet back over the new one).
3. Rebuild: `godot-mono --headless --import` then `godot-mono --headless --script tools/gen_terrain_tileset.gd`.
   It rebuilds `stage1/terrain_tileset.tres` with every `tilesetN.png` as **atlas source N** — adding tileset3
   never renumbers tileset1/2, so painted levels stay valid.
4. Reopen the editor and paint (see [`docs/painting-levels.md`](../../docs/painting-levels.md)).

⚠️ **Changing an existing sheet's layout** (moving/removing tiles) breaks cells already painted with the old
positions. Adding tiles in empty cells, or adding a new `tilesetN`, is always safe.

## Other stage art

`bg1.png` (backdrop), and props (`big_tree.png`, `tree1.png`, `man_choking_statue.png`, `skeleton_chillin.png`) are placed as sprites in
the layout scenes — no collision; they're decoration.
