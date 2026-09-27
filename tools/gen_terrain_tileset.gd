extends SceneTree

## Dev tool: build the stage's TileSet from its terrain sheets, with collision baked in, so levels are
## hand-painted in the editor (paint = collision). Re-run after adding / editing a sheet — with the editor CLOSED
## (an open editor writes its stale copy of the .tres back):
##   godot-mono --headless --script tools/gen_terrain_tileset.gd
##
## Sheets are discovered by name: every `tilesetN.png` in DIR (tileset1, tileset2, …) becomes its own atlas
## source with source id = N — so adding tileset3 never renumbers tileset1/2 and painted levels stay valid.
## A sheet can be any size; it's sliced into TILE x TILE cells from its top-left (no margins, no spacing).
##
## Collision is TRACED from each cell's own pixels: a fully opaque cell gets a box; a partial one (a slope, a cut
## corner, a thin ledge) gets polygons traced around its opaque pixels, so the physics matches the art; points near a
## cell edge snap onto it (EDGE_SNAP) so tiles join seamlessly. Empty cells are skipped. All collision is on physics
## layer 0 (collision_layer = World).

const DIR := "res://assets/terrain/stage1/"
const OUT := DIR + "terrain_tileset.tres"
const TILE := 32
const WORLD_LAYER := 1        # Combat.Layer.World = 1 << 0
const ALPHA_CUTOFF := 0.5     # a pixel counts as solid at >= this alpha
const TRACE_EPSILON := 1.0    # polygon simplification (px) when tracing partial cells
const EDGE_SNAP := 2.0        # traced points this close (px) to a cell edge snap ONTO it, so neighbouring tiles meet
                              # flush — no 1px lip where art stops a pixel short of the edge (a lip snags bodies)

func _init() -> void:
	var sheets := _sheets()
	if sheets.is_empty():
		push_error("gen_terrain_tileset: no tilesetN.png in " + DIR); quit(); return

	var ts := TileSet.new()
	ts.tile_size = Vector2i(TILE, TILE)
	ts.add_physics_layer()
	ts.set_physics_layer_collision_layer(0, WORLD_LAYER)
	ts.set_physics_layer_collision_mask(0, 0) # terrain is static; it detects nothing

	for n in sheets:
		var path: String = DIR + "tileset%d.png" % n
		var tex: Texture2D = load(path)
		var img := tex.get_image()
		if img.get_width() % TILE != 0 or img.get_height() % TILE != 0:
			push_warning("gen_terrain_tileset: %s is %dx%d — not a multiple of %d; the remainder is ignored" %
				[path, img.get_width(), img.get_height(), TILE])
		var src := TileSetAtlasSource.new()
		src.texture = tex
		src.texture_region_size = Vector2i(TILE, TILE)
		ts.add_source(src, n)
		var tiles := 0; var traced := 0
		for cy in img.get_height() / TILE:
			for cx in img.get_width() / TILE:
				var cell := img.get_region(Rect2i(cx * TILE, cy * TILE, TILE, TILE))
				var bm := BitMap.new()
				bm.create_from_image_alpha(cell, ALPHA_CUTOFF)
				var solid := bm.get_true_bit_count()
				if solid == 0:
					continue # empty cell — not a tile
				var coord := Vector2i(cx, cy)
				src.create_tile(coord)
				tiles += 1
				var polys: Array[PackedVector2Array] = []
				if solid == TILE * TILE:
					polys.append(PackedVector2Array([Vector2(0, 0), Vector2(TILE, 0), Vector2(TILE, TILE), Vector2(0, TILE)]))
				else:
					polys = bm.opaque_to_polygons(Rect2i(0, 0, TILE, TILE), TRACE_EPSILON)
					traced += 1
				var td := src.get_tile_data(coord, 0)
				var added := 0
				for poly in polys:
					var pts := _snap_to_edges(poly)
					if pts.size() < 3:
						continue # collapsed to a sliver along an edge — nothing to collide with
					td.add_collision_polygon(0)
					td.set_collision_polygon_points(0, added, pts)
					added += 1
		print("gen_terrain_tileset: tileset%d -> source %d: %d tiles (%d with traced collision)" % [n, n, tiles, traced])

	var err := ResourceSaver.save(ts, OUT)
	print("gen_terrain_tileset: %s -> %s" % ["OK" if err == OK else "ERR %d" % err, OUT])
	quit()

## A traced polygon in tile-centred coords (tile polygons are centred on the cell), with every point within EDGE_SNAP
## of a cell edge moved onto it, and the resulting consecutive duplicates dropped.
func _snap_to_edges(poly: PackedVector2Array) -> PackedVector2Array:
	var out := PackedVector2Array()
	for p in poly:
		var q := Vector2(_snap(p.x), _snap(p.y)) - Vector2(TILE, TILE) / 2.0
		if out.is_empty() or not out[out.size() - 1].is_equal_approx(q):
			out.append(q)
	if out.size() > 1 and out[0].is_equal_approx(out[out.size() - 1]):
		out.remove_at(out.size() - 1)
	return out

func _snap(v: float) -> float:
	if v <= EDGE_SNAP: return 0.0
	if v >= TILE - EDGE_SNAP: return float(TILE)
	return v

## The N of every `tilesetN.png` in DIR, ascending.
func _sheets() -> Array[int]:
	var ns: Array[int] = []
	for f in DirAccess.get_files_at(DIR):
		var name := f.trim_suffix(".import")
		if name == f and name.begins_with("tileset") and name.ends_with(".png"):
			var n := name.trim_prefix("tileset").trim_suffix(".png")
			if n.is_valid_int():
				ns.append(n.to_int())
	ns.sort()
	return ns
