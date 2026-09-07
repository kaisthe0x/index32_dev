# run — the roguelite loop

Everything that makes the game a *run* lives here: the continuous arena, the spawner, the Ruh economy
plumbing, and the buff drops. One folder, driven by data you can tune in one place each. The premise it
implements is in [`docs/game-design.md`](../../docs/game-design.md) (moving toward the Fissure/Seal/Warden
pivot in [`docs/game-loop.md`](../../docs/game-loop.md)).

> **Levels/exits are RETIRED.** There is now ONE endless arena: enemies trickle in at a steady rate and
> restart only on death — no stage exit, no next-level, no reward door. The old machinery (`Levels` data,
> `ExitGate`, `Rewards`/`Build`/`RewardsCatalog`, `RewardUI`) is **parked, not wired** — `RunManager` no
> longer references it. It stays for the pivot's own reward phase (chest / warden drops) to build on;
> `RewardUI` is the one still live, reused by the run-start `AttackSelect`.

`scenes/arena.tscn`'s root **is** `RunManager` — open it and press F6 to drop straight into a run (F5 starts at the `palette_preview` colour pickers, which then load `arena.tscn`).

## The pieces

| File | What it is |
|---|---|
| `RunManager.cs` (`RunManager`) | The brain + the arena root. Builds ONE continuous arena, **trickles enemies in at a steady rate** from a mixed roster (proximity-placed around the player, capped by a concurrent-alive limit), **awards Ruh per damaging hit landed** (via `gain_ruh_on_hit`, skipping a special's own hits — not per kill), **drops Fada Figs + a random buff (ramping chance) on each kill**, and restarts the run on death. Owns the camera/death/spawn flair. |
| `enemies.gd` (`EnemyKits`) | **The enemy roster** — one named kit per type (combat tuning + which scene), plus a `Tier`. `RunManager.SpawnPool` draws from these. Edit here to change *who* the enemies are. |
| `BuffDrop.cs` (`BuffDrop`, in `scripts/collectibles/`) | A collectible **buff drop** — code-built (no scene), pops/settles like a Fada Fig, glows in its rarity colour, and grants a RANDOM generally-useful buff on touch (id+tier rolled at spawn, tier weighted low). Pool = `BuffCatalog` factories not gated to a specific move. |
| `RewardUI.cs` (`RewardUI`) | The pick-a-reward popup (pauses the game, emits `chosen(id)`). Still live: reused by the run-start `AttackSelect`. |
| *(parked — not wired)* | `levels.gd` (`Levels`, still read once for the arena `bg` tint), `Rewards.cs`/`Build.cs`/`configs/RewardsCatalog.cs` (build-aware reward offers), `ExitGate.cs`. Kept for the pivot's reward phase; `RunManager` no longer drives them. |

**Hand-painted stage layouts** are the active approach: `RunManager` loads a random
`scenes/levels/stage1/stage1_v*.tscn` (a `LevelLayout`, discovered by the `stage1_v` glob in
`StageLayoutPaths`) and reads its `PlayerSpawn` / `Exit` markers (+ optional `orb` group). Terrain
is a **`TileMapLayer` with per-tile collision**: `tools/gen_terrain_tileset.gd` reads the terrain sheet
(`assets/terrain/stage1/tileset1.png`) and builds `terrain_tileset.tres` — every non-empty 32px cell
becomes a paintable tile, and every **≥85%-opaque (solid) cell gets a full-box collider** on the World
physics layer; decor cells (5–85% opaque) are paintable but pass-through. So paint = collision. This
requires a **genuinely modular sheet** (distinct reusable tiles: surface / fill / edges / corners /
platforms / decor) — a single mural does *not* work (its cells aren't reusable and its opaque interior
would all turn solid). Re-run the generator after editing the sheet, then paint the level in-editor.
Slopes / one-way platforms: paint the tiles, then hand-tweak those colliders in the TileSet editor (the
generator only bakes full boxes). See [`docs/painting-levels.md`](../../docs/painting-levels.md).

**Enemy spawning is CONTINUOUS + PROXIMITY-based.** A `_PhysicsProcess` accumulator fires `SpawnWave()` every
`SpawnInterval` (steady, no ramp for now — retuned when seals arrive), dropping `EnemiesPerWave` enemies picked
uniformly from `RunManager.SpawnPool` (a mixed roster: the grunts + Ein + Nasen; Wardens are elite/pivot-only).
Spawning pauses while `_alive` (living non-optional enemies) is at the `MaxAlive` cap. Each enemy is placed by
`SpawnPosition(kit)` relative to the player: **flyers** (`air`) overhead within `FlyerHeight*`/`FlyerXSpread`
(headroom-checked so they don't spawn inside a ceiling); **stationary** (`movement == Stationary`, e.g. Nasen)
far off on a ground tile (`StationarySpawn*`); **grunts** near on a ground tile but within a fair band
(`GroundSpawnMin..Max`) — a **min distance so an enemy never spawns on top of the player**. Ground tiles come
from `LevelLayout.GroundSurfaces()` (exposed tops of the Terrain tilemap — a solid cell with an empty cell
above). The interval/count/cap + distance bands are tunable consts in `RunManager`. (The old
`spawn_ground`/`spawn_air` layout markers are unused — delete them from layouts.)

Related, but not in this folder:
- **Player HP + Ruh** live on the `Player` (`scripts/Player.cs`) as **two independent pools**:
  `health` (damage hits this only; heals ONLY via rewards) and `ruh` — the **surge meter**, in
  charges/blocks of `RUH_PER_BLOCK` (100), capped by `ruh_cap`. You **start a run with 3 charges**
  (`BASE_RUH_CAP` = 300 — `begin_run` sets it full) and **refill by landing HITS** (`RUH_PER_HIT` = 20,
  so ~5 hits = 1 charge) — **not kills** — and it **never decays**. API: `gain_ruh_on_hit` /
  `take_damage` (HP only) / `heal` / `begin_run`. **Specials are free** now; **surges spend Ruh** (each
  use costs its `SurgeSpec.cost`, 100 = one charge). Rewards raise `ruh_cap` (toward `MAX_RUH_CAP` = 500, 5 charges).
- **The Ruh block meter** is built in `scripts/hud.gd` next to the HP bar (crimson cells) — one cell
  per charge; each surge empties one.
- **Surges apply a timed effect + aura** (`Player._begin_surge(SurgeSpec)`, fired by `Player._try_surge`
  on the dedicated `surge` button) — **Aegis** = invuln, **Jnoon** = ×2 damage dealt / ×0.5 taken; both
  for `duration` (+ the Fortitude `special_invuln_bonus`). Effects run on the `_surge_left` timer and
  clear together in `_end_surge`. Each surge names its own aura scene (`SurgeSpec.aura`).
- **Enemies** emit `damaged` (→ RunManager awards Ruh via `gain_ruh_on_hit`, skipping a special's own
  hits) and `died` in `Enemy._die` (→ `OnEnemyDied`: frees a cap slot + rolls the drops; no longer banks Ruh).
- **Spawn puff**: `vfx/spawn/enemy_spawn.tscn` (fired at each spawn spot).

## The loop (endless arena)

1. `RunManager.BuildArena()` sets the `bg` (from `Levels` index 0 — the one surviving use), loads a random
   `stage1_v*.tscn` layout, places the player at its `PlayerSpawn`, and seeds one spawn wave.
2. Every `SpawnInterval` seconds **`SpawnWave()`** trickles in `EnemiesPerWave` random enemies (proximity-placed),
   unless already at the `MaxAlive` cap. Each tick bumps `_waveCount`.
3. **Hitting** an enemy → `damaged` → `gain_ruh_on_hit()` charges the surge meter (a special's own hits are
   skipped). Specials are **free**; a **surge** fires only when you have the Ruh → `_try_surge()` spends its `cost`.
4. **Killing** an enemy → `died` → `OnEnemyDied`: `_alive--` (frees a cap slot), always drops **Fada Figs**, and
   with probability `BuffDropChance()` (ramps `BuffDropBase` → `BuffDropCap` by `_waveCount`) drops a **`BuffDrop`**
   (a random generally-useful buff, tier weighted low). Both spawn deferred (death fires mid physics-flush).
5. **Death** (HP hits 0) → the whole run restarts via `Player.begin_run` (buffs cleared, 100 HP / a full 3-charge
   Ruh meter) + a fresh `BuildArena()`; the run-start `AttackSelect` re-opens.

## Tuning cheatsheet

- **Change the spawn pressure** → `RunManager` consts: `SpawnInterval` (seconds between waves), `EnemiesPerWave`,
  `MaxAlive` (concurrent cap). `SpawnPool` is the roster drawn from.
- **Change the buff-drop rate** → `BuffDropBase` / `BuffDropStep` (+per wave) / `BuffDropCap`. Which buffs can drop
  = `BuffDrop.Pool()` (implemented `BuffCatalog` factories not gated to a specific move); rarity mix = `RareChance`.
- **Change an enemy's stats** → its kit in `enemies.gd` (combat).
- **Change the Ruh / surge economy** → `Player.RUH_PER_HIT` (fill rate per hit), `RUH_PER_BLOCK`
  (charge size), `BASE_RUH_CAP` (starting charges), and the Aegis surge's `cost` / `duration` in
  `configs/actions_khalid.gd` (`SURGES`) for its Ruh price + invuln window. (Specials are free — no cost knob.)

## Known template gaps (deliberate, for later)

- The arena reuses one platform style; "different look" is just the `bg` tint so far.
- Buff drops grant a RANDOM buff (no player choice) and reuse the Fada-Fig pickup sfx — placeholder look/feel.
- The steady spawn rate + ramping drops are the interim loop; **seals** (per `docs/game-loop.md`) will drive
  the rate and the reward economy, at which point the parked `Rewards`/`ExitGate` code gets reworked or removed.
- No win screen / meta-progression yet.
