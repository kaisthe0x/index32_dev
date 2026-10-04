# run — the roguelite loop

Everything that makes the game a *run* lives here: the arena, the round loop + spawner, the Ruh economy
plumbing, the buff menu, and the mystery box. One folder, driven by data you can tune in one place each. The design it
implements is [`docs/game-loop.md`](../../docs/game-loop.md) — **endless CoD-Zombies-style rounds**.

> **Levels/exits are gone.** There is ONE arena and endless numbered **rounds**; the run ends only on death — no
> stage exit, no next level, no reward doors — that code is deleted.

`scenes/arena.tscn`'s root **is** `RunManager` — open it and press F6 to drop straight into a run (F5 starts at the `palette_preview` colour pickers, which then load `arena.tscn`).

## The pieces

| File | What it is |
|---|---|
| `RunManager.cs` (`RunManager`) | The brain + the arena root. Builds ONE arena and runs the **round loop** (a hidden quota per round, trickled in from a mixed roster at the layout's spawn spots, under a concurrent cap; the last few hunt the player, and from round 5 standing still draws kamikazes — then straight into the next round with a ROUND banner — no break), **awards Ruh per damaging hit landed** (via `gain_ruh_on_hit`, skipping a special's own hits — not per kill), **pays Lira on every kill** (coins that fly to the player) **plus a per-kit chance of one Fada Fig**, **spawns a mystery box** (spend figs for a stingy powerful-buff gamble — a win rarely offers a **special-swap** instead) **and the stalls** (Needle Point, Dekken — placed in the layout), ticks the run's `PerkLedger` at each round clear, and restarts the run on death. Owns the camera/death/spawn flair. |
| `enemies.gd` (`EnemyKits`) | **The enemy roster** — one named kit per type (combat tuning + which scene), plus a `Tier`. `RunManager.SpawnPool` draws from these. Edit here to change *who* the enemies are. |
| `ShotLedger.cs` (`ShotLedger`) | **Needle Point's rules** for one run: the ranks owned of each shot (as `Shot` passives). BUY (break only) raises a shot one rank, permanently, at a rising price. Data in `configs/NeedlePoint.cs`; see the main README § Needle Point shots. |
| `PerkLedger.cs` (`PerkLedger`) | **Dekken's rules** for one run: the round's stock (5 random perks, rerolled every round), active/owned perks (as `Perk` passives), BUY (break only) + `OnRoundClear`. Data in `configs/Dekken.cs`; see the main README § Dekken perks. |
| `DekkenStall.cs` (`DekkenStall`, in `scripts/things/`) | The perk shop (placeholder amber stall), placed in the layout. Press **E** at it to open the `DekkenMenu` (`scripts/ui/`). |
| `NeedlePointStall.cs` (`NeedlePointStall`, in `scripts/things/`) | The stat stall — a booth you walk up into (ramped dais collision). Press **E** on its top platform to open the `NeedlePointMenu` (`scripts/ui/`) over the run's ledger. |
| `Stall.cs` (`Stall`, in `scripts/things/`) | The shared stand-at-it-and-press-**E** base for the stall SCENES (`scenes/things/`): reads the scene's `Visual`, `Interact` (Area2D — where to stand) and `Prompt` (Marker2D) nodes; owns the prompt and the key (`interact`, registered in code). The mystery box, Needle Point and Dekken extend it. |
| `MysteryBox.cs` (`MysteryBox`, in `scripts/things/`) | A code-built placeholder "?" crate (a `Stall`). Press **E** at it to spend `Cost` fada_figs on a gamble: `DudChanceBase` of pulls give nothing, otherwise it fires `won` and RunManager opens the **same 3-choice menu** from the POWERFUL pool (`BuffCatalog.PowerfulIds`, above-rare tiers). Each win raises the dud chance further (per-run). Press E again to pull again. |
| `RewardUI.cs` (`RewardUI`) | The pick-a-card popup (pauses the game, emits `chosen(id)`) — `Open(cards, title)`. Now drives the mystery box's **buff menu**. |
| `configs/Rounds.cs` (`Rounds`) | **Round tuning** — quota curve, concurrent cap, spawn interval, spawn-spot distance, stragglers, stand-still kamikazes, when "n LEFT" shows. Pure data. |

**Hand-painted stage layouts** are the active approach: `RunManager` loads a random
`scenes/levels/stage1/stage1_v*.tscn` (a `LevelLayout`, discovered by the `stage1_v` glob in
`StageLayoutPaths`) and reads its `PlayerSpawn` marker, its `EnemySpawns` markers, the optional `orb` group, and the three REQUIRED stall
SCENES placed in it — `scenes/things/mystery_box.tscn`, `needle_point.tscn`, `dekken.tscn`, found by
`LevelLayout.Placed<T>()` (a missing one logs an error). Terrain
is a **`TileMapLayer` with per-tile collision**: `tools/gen_terrain_tileset.gd` builds the shared
`assets/terrain/stage1/terrain_tileset.tres` from every **`tilesetN.png`** in that folder (each its own atlas source,
id = N), with collision **traced from each tile's pixels** (full tiles = boxes; slopes/cut corners = traced polygons,
snapped flush to the cell edges). So paint = collision, and painted ramps are walkable. Author modular 32px sheets,
re-run the generator (editor closed), then paint in-editor. See [`assets/terrain/README.md`](../../assets/terrain/README.md)
+ [`docs/painting-levels.md`](../../docs/painting-levels.md).

**Enemy spawning is ROUND-driven, at the layout's SPAWN SPOTS** (`TickRound`, tuning in `configs/Rounds.cs`). Round `r` has a
hidden **quota** `Q(r) = QuotaBase + QuotaLinear·r + QuotaQuad·r²` (6, 10, 15, … 63 at r10 — a slow start). One enemy
spawns every `SpawnInterval(r)` (shortens per round, floored at `IntervalMin`) as long as fewer than the concurrent cap
`C(r)` (`CapBase`, +1 every `CapGrowthRounds`, max `CapMax`) quota enemies are alive; once `Q(r)` have spawned,
spawning **stops**, and the round **clears** on the last kill (`OnEnemyDied` → `ClearRound`) → `StartRound(r+1)` at
once — **no break, no countdown**; the HUD plays the **ROUND n** intro (big at screen centre, then it flies up into the round block). The roster is drawn uniformly from
`RunManager.SpawnPool` (the grunts + Nasen — Ein is the stand-still kamikaze below; Wardens are for the future Warden rounds). **Only non-optional enemies
are quota enemies** — the sleeper Nasen (`optional`, as are kamikazes) spawns on its own cap but never counts or blocks a clear.
**Per-type caps:** a kit with a `spawn_cap` (Nasen = 1) can't have more than that many alive at once — `PickSpawnKit`
only rolls kits under their cap (`LivingOfType` vs `EffectiveCap`), and that cap grows +1 every `KitCapGrowthRounds`.
Each enemy appears at a **spawn spot** — a `Marker2D` under the layout's `EnemySpawns` node (`LevelLayout.EnemySpawns`).
**One enemy per spot** (`_spotOf`, freed on death), spread over the map: `PickSpawnSpot` takes the free spot farthest
from the spots already held, among those at least `Rounds.SpawnMinDistance` (320 px) from the player (so nothing lands
on top of him); with every spot held the spawn waits (`SpawnOne` returns false, `TickRound` retries). `SpawnAt` puffs, spawns, wires `died`/`damaged`, tracks it in `_enemies`
and counts it toward the quota unless the kit is `optional`. The enemy **patrols** around its spot until the player
comes within its `aggro_range` (320 px), so the player has to go **find** enemies (the off-screen arrows help).
A layout with no markers logs an error and spawns nothing.

**Near-player spawns** (from `Rounds.NearSpawnFromRound`, 10): `NearShare(r)` of the grunts — 20% at r10, +10% a round,
max 70%; never a stationary kit — spawn on the player's floor, BEHIND him (opposite `Player.facing`),
`NearSpawnMin..NearSpawnMax` (100–240 px) away: `NearPlayerSpot` → `PickGroundSurface`. That only uses the floor the
player stands on: `LevelLayout` splits the Terrain's exposed tops into connected **floor regions** (tops join where
their surfaces meet — side by side, or along a ramp, `LevelLayout.Linked`; a block step or a sawtooth splits them),
`SpawnSurfacesNear` returns the region under him (`GroundBelow`, a ray down, so a jump doesn't change it) limited to
flat runs of `MinSpawnFloorTiles` (3)+, and `SpotIsClear` drops tiles under something solid (Needle Point's dais).
From round 10, all spots held also falls back to a near spawn instead of waiting.

**Stragglers:** once the quota has fully spawned and ≤ `Rounds.StragglerCount` (3) remain, `UpdateStragglers` calls
`Enemy.hunt(Rounds.StragglerSpeedMult)` on each — they chase the player anywhere, no leash, **1.6× faster** (walk
animation sped up to match) — so a round never stalls on one he can't find.

**Stand-still kamikazes** (`TickPressure`): from `Rounds.KamikazeFromRound` (5), a player who stays within
`StillRadius` (50 px) for `StillTime` (2 s) gets an **Ein** (`EnemyKits.EIN` — `optional`, no Lira, no figs; NOT in
`SpawnPool`) `KamikazeDistance` (220 px) to a random side (`KamikazeSpot` — the other side if that one is inside a
wall) and up to `KamikazeHeight` (110 px) above, under any ceiling (`HeadroomAbove`); then another every
`KamikazeInterval(r)` (2 s at r5, ×0.95 a round, min 0.75 s) while he stays put, at most `KamikazeMax(r)` alive (5 at
r5, +1 every 5 rounds, max 8). Moving away resets the clock; Nem's sleep pauses it (`Player.is_channeling_surge`) —
kamikazes already diving still come.

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
  `take_damage` (HP only) / `heal` / `begin_run`. **Specials cost no Ruh** (each has its own cooldown); **surges spend Ruh** (each
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

1. `RunManager.BuildArena()` sets the `bg` tint (`Terrain.BackgroundTint`), loads a random
   `stage1_v*.tscn` layout, places the player at its `PlayerSpawn`, and resets the round state; **round 1** starts on the
   first tick of play (after the attack pick + spawn). The stage
   music (`Music.play_stage`) starts as the arena loads (the colour-scheme screen before it is silent).
2. **Rounds** (see *Enemy spawning* above): quota trickle under the cap → spawning stops → last kill clears → the
   next round starts at once. RunManager pushes `HUD.SetRound(round, left, best)` on every change, so the HUD shows
   `ROUND n`, `n LEFT` once `Rounds.ShowLeftAt` or fewer remain, and `BEST n`. **Round sound** (`SfxWorld`,
   `sfx/world/round/round_start.wav`): `round_start` plays as each round begins, with the ROUND n label.
3. **Hitting** an enemy → `damaged` → `gain_ruh_on_hit()` charges the surge meter (a special's own hits are
   skipped). Specials cost no Ruh (cooldown-gated); a **surge** fires only when you have the Ruh → `_try_surge()` spends its `cost`.
4. **Killing** an enemy → `died` → `OnEnemyDied` → `SpawnDrops` (deferred — death fires mid physics-flush):
   `Enemy.lira_drop` **Lira** coins that fly to the player and bank on arrival, and — at `Enemy.fig_chance` (10 %
   default, Kebus 25 %) — **one Fada Fig** that settles until touched. Nothing drops if the enemy fell off the map. A
   quota enemy also frees a cap slot, counts toward the round, and the last one clears it.
5. **Buffs come from the stalls:**
   - **Needle Point (Lira, permanent stats), always open:** each purchase raises a shot one rank for the rest of the
     run, each rank dearer than the last — `ShotLedger`.
   - **Dekken (Lira, utility perks), always open:** 5 random perks per round — a heal, a teleport to the box, fig odds,
     a fig magnet, a shield, a free surge, Wider Pull — `PerkLedger`, ticked + restocked at `ClearRound`.
     `StartRound` fires `Player.notify_round_start` (round-scoped perks re-arm).
   - **Mystery box (powerful, paid gamble):** one `MysteryBox` per arena; stand next to it + press **E** to spend `Cost`
     figs (`Player.spend_fada_figs`). Some pulls dud (`DudChanceBase`, 20 %); a WIN fires the box's `won` signal →
     `RunManager.OpenBoxMenu` opens a **3-choice `RewardUI`** from `BuffCatalog.PowerfulIds` (above-rare tiers). Each win raises the dud chance. On a win, one of the three cards is **rarely** a **special-swap**
     instead of a buff (`RollBoxSpecial`, `SpecialOfferChance`, drawn from `BoxSpecialIds` — **Zahluq** and **Bakshen**);
     picking it **replaces your equipped special** (`OnBuffChosen` → `Player.equip`, keyed by `_menuSpecialId`) rather
     than adding a passive. Picking plays `buff_select`.
6. **Death** (HP hits 0 — the 6th hit) → `SaveData.ReportRun(_round)` records the round reached (new best →
   `rounds_record`), then the whole run restarts via `Player.begin_run` (buffs cleared, a full 3 blocks of HP / a
   full 3-charge Ruh meter) + a fresh `BuildArena()`; the run-start `AttackSelect` re-opens.

## Tuning cheatsheet

- **Change the round curve** → `configs/Rounds.cs`: `Quota*` (enemies per round), `Cap*` (concurrent alive),
  `Interval*` (seconds between spawns), `ShowLeftAt`. `RunManager.SpawnPool` is the
  roster drawn from. Anti-camp: `OffscreenDespawnTime` (how long off-screen before an enemy is silently culled) /
  `OffscreenMargin`.
- **Cap a specific enemy type** → add `{ "spawn_cap", N }` to its kit in `EnemyKits` (e.g. Nasen = 1). The cap grows
  +1 every `Rounds.KitCapGrowthRounds` rounds. Kits with no `spawn_cap` are unlimited.
- **Change the drops** → Lira per kill: `RunManager.LiraForTier` (by advisory tier) or a kit's `lira_drop`; fig
  odds: a kit's `fig_chance` (default `Enemy.fig_chance` = 0.1). Pickup cues `lira_collect` / `fada_fig_collect` in
  `SfxWorld` (PLACEHOLDERS). The ROUND n intro's timing is `IntroFadeIn` / `IntroHold` / `IntroFly` in `HUD.cs`.
- **Change the mystery box** → `MysteryBox` consts: `Cost` (figs per pull, 8 — figs are rare), `DudChanceBase`,
  `DudChanceGrowth` (+per win), `DudChanceCap`. Cards per win: `RunManager.BuffMenuChoices`. Powerful pool = `BuffCatalog.PowerfulIds()`; tier weights = `RollPowerfulTier`.
- **Change the box special-swap** → `RunManager` `SpecialOfferChance` (chance a win offers a special instead of a 3rd
  buff) + `BoxSpecialIds` (which specials are box-only; currently `SpecialIds.Zahluq` + `SpecialIds.Bakshen`). `RollBoxSpecial` skips a special
  you already have equipped.
- **Change the shots** → `configs/NeedlePoint.cs`: each shot's per-rank values and rank-I price in `SHOTS`, plus
  `PriceGrowth` and `RANK_COLORS`. A new shot = a `ShotIds` id + a `ShotStat` + its
  case in `Shot.Apply`.
- **Change the perks** → `configs/Dekken.cs`: `StockSize` and each perk's duration, rounds, price and `Value` in
  `PERKS`. A new perk = a `PerkIds` id + its entry + its effect (`Perk` for lasting ones, `PerkLedger.Buy` for one-use).
- **Which buffs the box offers** → `BuffCatalog.PowerfulIds()` (every general buff; move-gated buffs are excluded by
  the `General()` filter).
- **Change an enemy's stats** → its kit in `enemies.gd` (combat).
- **Change the Ruh / surge economy** → `Player.RUH_PER_HIT` (fill rate per hit), `RUH_PER_BLOCK`
  (charge size), `BASE_RUH_CAP` (starting charges), and the Aegis surge's `cost` / `duration` in
  `configs/actions_khalid.gd` (`SURGES`) for its Ruh price + invuln window. (Specials cost no Ruh — their knob is each special's `Cooldown` in `ActionsKhalid.SPECIALS`.)

## Known template gaps (deliberate, for later)

- The arena reuses one platform style; "different look" is just the `bg` tint so far.
- The mystery box is placeholder art (a "?" crate) and reuses the Fada-Fig pickup sfx; the buff menu reuses the
  reward-card popup. No dedicated art/sfx yet.
- The rest of the round design is still to build, in order (`docs/game-loop.md` § Build order): stragglers hunting
  the player → enemy **ranks** (tier-coloured, heavier hits) → **round drops** (Max Health / Max Ruh) → **Warden
  rounds** (every 10th).
- No win screen / meta-progression yet.
