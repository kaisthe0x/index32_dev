# run — the roguelite loop

Everything that makes the game a *run* lives here: the arena, the round loop + spawner, the Ruh economy
plumbing, the buff menu, and the mystery box. One folder, driven by data you can tune in one place each. The design it
implements is [`docs/game-loop.md`](../../docs/game-loop.md) — **endless CoD-Zombies-style rounds**.

> **Levels/exits are RETIRED.** There is now ONE arena and endless numbered **rounds**; the run ends only on
> death — no stage exit, no next-level, no reward door. The old machinery (`Levels` data,
> `ExitGate`, `Rewards`/`Build`/`RewardsCatalog`, `RewardUI`) is **parked, not wired** — `RunManager` no
> longer references it. It stays parked (not part of the round design);
> `RewardUI` is the one still live, reused by the run-start `AttackSelect`.

`scenes/arena.tscn`'s root **is** `RunManager` — open it and press F6 to drop straight into a run (F5 starts at the `palette_preview` colour pickers, which then load `arena.tscn`).

## The pieces

| File | What it is |
|---|---|
| `RunManager.cs` (`RunManager`) | The brain + the arena root. Builds ONE arena and runs the **round loop** (a hidden quota per round, trickled in from a mixed roster — proximity-placed around the player, under a concurrent cap — then a breather + ROUND banner), **awards Ruh per damaging hit landed** (via `gain_ruh_on_hit`, skipping a special's own hits — not per kill), **drops Fada Figs on each kill**, **pops a free pick-1-of-3 MILD buff menu at escalating fada-fig milestones**, **spawns a mystery box** (spend figs for a stingy powerful-buff gamble — a win rarely offers a **special-swap** instead), and restarts the run on death. Owns the camera/death/spawn flair. |
| `enemies.gd` (`EnemyKits`) | **The enemy roster** — one named kit per type (combat tuning + which scene), plus a `Tier`. `RunManager.SpawnPool` draws from these. Edit here to change *who* the enemies are. |
| `MysteryBox.cs` (`MysteryBox`, in `scripts/things/`) | A code-built placeholder "?" crate. Stand next to it (a "E" prompt shows) and press **E** (the `interact` action, registered in code) to spend `Cost` fada_figs on a gamble: `DudChanceBase` of pulls give nothing, otherwise it fires `won` and RunManager opens the **same 3-choice menu** from the POWERFUL pool (`BuffCatalog.PowerfulIds`, above-rare tiers). Each win raises the dud chance further (per-run). Press E again to pull again. |
| `RewardUI.cs` (`RewardUI`) | The pick-a-card popup (pauses the game, emits `chosen(id)`) — `Open(cards, title)`. Now drives the milestone **buff menu**. |
| `configs/Rounds.cs` (`Rounds`) | **Round tuning** — quota curve, concurrent cap, spawn interval, breather, when "n LEFT" shows. Pure data. |
| *(parked — not wired)* | `levels.gd` (`Levels`, still read once for the arena `bg` tint), `Rewards.cs`/`Build.cs`/`configs/RewardsCatalog.cs` (build-aware reward offers), `ExitGate.cs`. Parked; `RunManager` no longer drives them. |

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

**Enemy spawning is ROUND-driven + PROXIMITY-based** (`TickRound`, tuning in `configs/Rounds.cs`). Round `r` has a
hidden **quota** `Q(r) = QuotaBase + QuotaLinear·r + QuotaQuad·r²` (6, 9, 13, … 49 at r10). While `Fighting`, one enemy
spawns every `SpawnInterval(r)` (shortens per round, floored at `IntervalMin`) as long as fewer than the concurrent cap
`C(r)` (`CapBase`, +1 every `CapGrowthRounds`, max `CapMax`) quota enemies are alive; once `Q(r)` have spawned,
spawning **stops**, and the round **clears** on the last kill (`OnEnemyDied` → `ClearRound`) → a `BreatherTime` pause
(`RoundPhase.Breather`) → `StartRound(r+1)` with a **ROUND n** banner. The roster is drawn uniformly from
`RunManager.SpawnPool` (the grunts + Ein + Nasen; Wardens are for the future Warden rounds). **Only non-optional enemies
are quota enemies** — the sleeper Nasen (`optional`) spawns on its own cap but never counts or blocks a clear.
**Per-type caps:** a kit with a `spawn_cap` (Nasen = 1) can't have more than that many alive at once — `PickSpawnKit`
only rolls kits under their cap (`LivingOfType` vs `EffectiveCap`), and that cap grows +1 every `KitCapGrowthRounds`.
Each enemy is placed by
`SpawnPosition(kit)` relative to the player: **flyers** (`air`) overhead within `FlyerHeight*`/`FlyerXSpread`
(headroom-checked so they don't spawn inside a ceiling); **stationary** (`movement == Stationary`, e.g. Nasen)
far off on a ground tile (`StationarySpawn*`); **grunts** near on a ground tile but within a fair band
(`GroundSpawnMin..Max`) — a **min distance so an enemy never spawns on top of the player**. Ground tiles come
from `LevelLayout.GroundSurfaces()` (exposed tops of the Terrain tilemap — a solid cell with an empty cell
above). The distance bands are tunable consts in `RunManager`; the round curves live in `Rounds`. (The old
`spawn_ground`/`spawn_air` layout markers are unused — delete them from layouts.)

**Anti-camp cull:** a player could camp somewhere the AI can't reach and stall the round. So `CullOffscreen` tracks each
living enemy's time OFF-SCREEN (`_offscreen`, using the camera's visible rect grown by `OffscreenMargin`); once one stays
off-camera for `OffscreenDespawnTime` (8s) it's **silently freed** (no death VFX/sfx/figs) — **not a kill**: its cap slot
is released AND it goes back into the round's unspawned quota (`_spawned--`), so a fresh one spawns near the player.

Related, but not in this folder:
- **Player HP is SLOT-based** (`scripts/Player.cs`): you have **3 blocks**, measured internally in **half-blocks**
  (`BaseMaxHealth` = 6). **Every hit costs a flat half-block regardless of its damage** (`take_damage` ignores the
  amount — so 6 hits kill), and there's no player damage number. Damage-*reduction* is therefore inert (Jnoon's
  mitigation is parked; the parked reward `Thick Hide` still sets `damage_taken_mult` but nothing reads it). Healing
  is in half-blocks: the **Nem surge restores one block**, and the **Bloodrush/Skim** buffs give a per-tier *chance*
  per hit to restore a half-block (`LifestealBuff`). The HUD shows 3 block cells (half-block resolution).
- **Ruh** is the other pool — the **surge meter**, in
  charges/blocks of `RUH_PER_BLOCK` (100), capped by `ruh_cap`. You **start a run with 3 charges**
  (`BASE_RUH_CAP` = 300 — `begin_run` sets it full) and **refill by landing HITS** (`RUH_PER_HIT` = 20,
  so ~5 hits = 1 charge) — **not kills** — and it **never decays**. API: `gain_ruh_on_hit` /
  `take_damage` (HP only) / `heal` / `begin_run`. **Specials are free** now; **surges spend Ruh** (each
  use costs its `SurgeSpec.cost`, 100 = one charge). Rewards raise `ruh_cap` (toward `MAX_RUH_CAP` = 500, 5 charges).
- **The Ruh orbs** are in the HUD gauge under the health stars — one orb per charge; each surge empties one.
- **Surges apply a timed effect + aura** (`Player._begin_surge(SurgeSpec)`, fired by `Player._try_surge`
  on the dedicated `surge` button) — **Aegis** = invuln, **Jnoon** = ×2 damage dealt / ×0.5 taken; both
  for `duration` (+ the Fortitude `special_invuln_bonus`). Effects run on the `_surge_left` timer and
  clear together in `_end_surge`. Each surge names its own aura scene (`SurgeSpec.aura`).
- **Enemies** emit `damaged` (→ RunManager awards Ruh via `gain_ruh_on_hit`, skipping a special's own
  hits) and `died` in `Enemy._die` (→ `OnEnemyDied`: frees a cap slot + rolls the drops; no longer banks Ruh).
- **Spawn puff**: `vfx/spawn/enemy_spawn.tscn` (fired at each spawn spot).

## The loop (endless arena)

1. `RunManager.BuildArena()` sets the `bg` (from `Levels` index 0 — the one surviving use), loads a random
   `stage1_v*.tscn` layout, places the player at its `PlayerSpawn`, and resets the round state to a short
   `FirstRoundDelay` breather before **round 1**.
2. **Rounds** (see *Enemy spawning* above): quota trickle under the cap → spawning stops → last kill clears →
   breather → next round. RunManager pushes `HUD.SetRound(round, left, countdown, best)` on every change — and
   once per whole second of a breather — so the HUD shows `ROUND n`, `n LEFT` once `Rounds.ShowLeftAt` or fewer
   remain, `NEXT ROUND IN n` during the breather, and `BEST n`.
3. **Hitting** an enemy → `damaged` → `gain_ruh_on_hit()` charges the surge meter (a special's own hits are
   skipped). Specials are **free**; a **surge** fires only when you have the Ruh → `_try_surge()` spends its `cost`.
4. **Killing** an enemy → `died` → `OnEnemyDied`: always drops **Fada Figs** (deferred — death fires mid
   physics-flush); a quota enemy also frees a cap slot, counts toward the round, and the last one clears it.
5. **Buffs come from two places:**
   - **Milestone menu (mild, free):** collecting fada_figs fires `Player.fada_collected`, which drives the HUD's
     **"next buff" progress bar** (`HUD.SetBuffProgress`, spanning `_prevMilestone`→`_nextMilestone`). When the run's
     LIFETIME total crosses `_nextMilestone` (5 → 15 → 35 → …, the gap grows by `MilestoneGapGrowth`), `BeginBuffMilestone`
     **freezes the game, flashes a "LEVEL UP" banner + `buff_levelup` cue**, then after `LevelUpDelay` opens a `RewardUI`
     of **3 mild buffs** (`BuffCatalog.MildIds` — general, NON-invuln; Common/Rare, easing toward Rare with
     `_milestoneIndex`). Picking grants it + plays `buff_select`. **No figs are spent** — the balance is left for the box.
   - **Mystery box (powerful, paid gamble):** one `MysteryBox` per arena; stand next to it + press **E** to spend `Cost`
     figs (`Player.spend_fada_figs`). Most pulls dud (`DudChanceBase`); a WIN fires the box's `won` signal →
     `RunManager.OpenPowerfulBuffMenu` opens the **same 3-choice `RewardUI`** from `BuffCatalog.PowerfulIds` (above-rare,
     incl. invuln). Each win raises the dud chance. On a win, one of the three cards is **rarely** a **special-swap**
     instead of a buff (`RollBoxSpecial`, `SpecialOfferChance`, drawn from `BoxSpecialIds` — currently just **Zahluq**);
     picking it **replaces your equipped special** (`OnBuffChosen` → `Player.equip`, keyed by `_menuSpecialId`) rather
     than adding a passive. (Both menus share `ShowBuffMenu` / `OnBuffChosen`.)
6. **Death** (HP hits 0 — the 6th hit) → `SaveData.ReportRun(_round)` records the round reached (new best →
   `rounds_record`), then the whole run restarts via `Player.begin_run` (buffs cleared, a full 3 blocks of HP / a
   full 3-charge Ruh meter) + a fresh `BuildArena()`; the run-start `AttackSelect` re-opens.

## Tuning cheatsheet

- **Change the round curve** → `configs/Rounds.cs`: `Quota*` (enemies per round), `Cap*` (concurrent alive),
  `Interval*` (seconds between spawns), `BreatherTime`, `FirstRoundDelay`, `ShowLeftAt`. `RunManager.SpawnPool` is the
  roster drawn from. Anti-camp: `OffscreenDespawnTime` (how long off-screen before an enemy is silently culled) /
  `OffscreenMargin`.
- **Cap a specific enemy type** → add `{ "spawn_cap", N }` to its kit in `EnemyKits` (e.g. Nasen = 1). The cap grows
  +1 every `Rounds.KitCapGrowthRounds` rounds. Kits with no `spawn_cap` are unlimited.
- **Change the buff-menu cadence** → `RunManager` `FirstMilestone` / `MilestoneGapBase` / `MilestoneGapGrowth`
  (the milestone curve) + `BuffMenuChoices`. `LevelUpDelay` = the "LEVEL UP" banner hold before the menu opens (both
  banners share `RunManager.ShowBanner`; `RoundBannerHold` = how long "ROUND n" stays). Mild
  pool = `BuffCatalog.MildIds()`; tier skew = `RollMildTier`. Buff sfx: `buff_levelup` / `buff_select` in `SfxWorld`
  (PLACEHOLDER cues — repoint to real files when ready). The HUD "next buff" bar is `HUD.SetBuffProgress`.
- **Change the mystery box** → `MysteryBox` consts: `Cost` (figs per pull), `DudChanceBase` (~0.97), `DudChanceGrowth`
  (+per win), `DudChanceCap`. Powerful pool = `BuffCatalog.PowerfulIds()`; tier weights = `RollPowerfulTier`.
- **Change the box special-swap** → `RunManager` `SpecialOfferChance` (chance a win offers a special instead of a 3rd
  buff) + `BoxSpecialIds` (which specials are box-only; currently `SpecialIds.Zahluq`). `RollBoxSpecial` skips a special
  you already have equipped.
- **Move a buff between pools** → the invuln family is box-only via `BuffCatalog.IsInvuln`; move-gated buffs are
  excluded from both by the `General()` filter.
- **Change an enemy's stats** → its kit in `enemies.gd` (combat).
- **Change the Ruh / surge economy** → `Player.RUH_PER_HIT` (fill rate per hit), `RUH_PER_BLOCK`
  (charge size), `BASE_RUH_CAP` (starting charges), and the Aegis surge's `cost` / `duration` in
  `configs/actions_khalid.gd` (`SURGES`) for its Ruh price + invuln window. (Specials are free — no cost knob.)

## Known template gaps (deliberate, for later)

- The arena reuses one platform style; "different look" is just the `bg` tint so far.
- The mystery box is placeholder art (a "?" crate) and reuses the Fada-Fig pickup sfx; the buff menu reuses the
  reward-card popup. No dedicated art/sfx yet.
- The rest of the round design is still to build, in order (`docs/game-loop.md` § Build order): stragglers hunting
  the player → enemy **ranks** (tier-coloured, heavier hits) → **round drops** (Max Health / Max Ruh) → **Warden
  rounds** (every 10th). The parked `Rewards`/`ExitGate` code is not part of it.
- No win screen / meta-progression yet.
