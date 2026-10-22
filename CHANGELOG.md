# Changelog

One entry per push, newest first. Each entry lists its commits with **what** changed, **why**, what it **could
affect**, and how it was **tested** — written for whoever reads this in six months, the owner included.
History before 2026-10-04 is in `git log` and `docs/game-loop.md`.

---

## 2026-10-22 — `new-shit` — Khalid's animations get a light shading pass

### Khalid: subtle shading on every animation

- **What:** all 29 of Khalid's sheets (`sprites/characters/khalid/*_frames.png`) are shaded instead of one colour per
  part: each part's top and left edges take its ramp's lightest shade (slot 0), its bottom and right edges and
  anything sitting under another part take the next darker shade (slot 2), and parts too thin to shade stay flat
  (slot 1). The owner picked this "subtle" strength from a flat / subtle / stronger comparison.
- **How:** in the art repo (`index32_art` 974f827) every Khalid master gained an `Export_Shading` layer above
  `Export_Character`, holding only the shaded pixels — so the owner can repaint, hide or delete it frame by frame
  without touching the drawing; the hooks export it merged with the character. Only fully opaque pixels in a part's
  flat colour were touched; semi-transparent fade pixels (dash, death, spawn) are unchanged. The sheets were copied
  here unchanged. README § the palette ("Khalid is FLAT…") describes the layer.
- **Could affect:** the colour picker and the palette shader (`sprite_palette.gdshader`) — all three shades are slots
  of the existing 36-colour `PaletteConfig.DefaultShades`, so they recolour like the flat colour did; the hair flow
  and glow work per shade as before. The rope dart's red arcs are drawn in hair red, so they are shaded as hair too.
  No code or frame layout changed.
- **Tested:** every new sheet compared with the previous one: same alpha everywhere, changes only on opaque palette
  pixels (about 45% of Khalid's pixels). `--import`, `verify_frames.gd` (0 failures, uniform 128×80 canvas),
  `dotnet build` 0 warnings, a headless arena boot (the one exit-time "resources still in use" line is there without
  this change too), and a windowed render of the colour-picker screen with a saved scheme: the shading survives the
  recolour. **Not verified:** a full playthrough look at every move in motion.

---

## 2026-10-20 — `new-shit` — Khalid dances when left alone; experiments/ (the camping meteor, dev-only); the stage-1 lady; dirt and grass kicked up when running

### Footfalls: the ground sheds what it is made of

- **What:** running on the grass floor, each push-off sprays soil specks and grass blades up and back from Khalid's
  heel, in the floor tiles' own colours (soil `363548` / `4a5566` / `5a705e`, moss `6f8a66` / `86a071` / `9db582`);
  they fly up to about 25 px — clear of the ground line, where same-coloured grass reads against the backdrop — and
  fall away behind him. The stone and brick bridge (`tileset2`) sheds nothing.
- **How:** `configs/Footfalls.cs` (data: which tileset sheds which colours, keyed by its image; the push-off frames
  per character and animation — Khalid's run: sheet frames 2, 6, 9; the burst numbers), `records/vfx/FootfallStyle.cs`,
  `scripts/player/parts/FootKick.cs` (a pool of three one-shot `CpuParticles2D` bursts per kind, left in the world,
  drawn behind the sprite; each particle picks exactly one palette colour through a hard-stepped gradient; blades
  point along their flight). `Player.OnFrameChanged` fires it on a push-off frame while he runs on the floor. The
  surface is the tile at the floor contact point, looked up in the `TileMapLayer` — not `GetCoordsForBodyRid`, which
  returns one representative tile of the layer's merged collision chunk (it reported a bridge tile under the grass).
- **Could affect:** the run's look (a new layer of particles); nothing else reads these.
- **Tested:** build 0 warnings; reimport and headless boot clean (the exit-time line is the stage music, as before);
  a throwaway scene through the real arena in recording mode (deleted): running on the grass floor, up to 6 bursts in
  the air at once; teleported onto the bridge and running its length, 0. A 1280×720 render of the run showed the
  spray behind him (tuned up once: the first pass was too faint — 1-px specks that stayed under the ground line).
  **Not verified:** other surfaces (only stage 1 exists); landing or slam dust (not asked for).
- **Tuned by the owner** after the first build: 13 soil specks and 5 blades per kick, blades living 0.4 s, a 45°
  spread (the constants in `configs/Footfalls.cs`).

### Khalid dances after 8 s of calm

- **What:** standing idle on the ground for 8 s with no input and no damage, Khalid dances: the owner's new
  14-frame `dance_1` (`sprites/characters/khalid/khalid_dance_1_frames.png`, from
  `index32_art/art/characters/khalid/khalid_dance_1.aseprite`). It starts on the idle pose, plays frames 0–2 once,
  then loops 3 to the last frame; any button, key, real stick push or a hit stops it, and 8 more calm seconds start it
  again.
- **How:** a `Dance` free mode in `FreeState` with a `Calm` timer that counts only while he stands idle on the
  ground (`DanceAfter` 8 s); `Player._Input` (any press, or a stick past `CalmStickDeadzone` 0.5) and `TakeDamage`
  reset it through `ResetCalm`. The loop is the generator's existing `loop_from` (3) with `keep_first` (frame 0
  plays), and `FRAME_DURATIONS` copies the master's own frame timings (0.1–0.2 s) — `tools/gen_spriteframes.py`;
  `resources/characters/khalid.tres` regenerated.
- **Could affect:** idle (the dance only replaces it after 8 s of calm); `khalid.tres` is rewritten in the
  generator's layout — loaded in Godot and compared animation by animation against the committed one, every existing
  animation, region, margin, duration and metadata is identical; the only change is `dance_1`.
- **Tested:** build 0 warnings; reimport and headless boot clean; a throwaway headless scene through the real arena
  in recording mode (deleted) passed 5/5: the dance starts at 8.00 s, plays 0,1,2 then 3..13 back to 3, a key press
  stops it, it starts again after another 8 s, a hit stops it. A 1280×720 render of every frame showed it on the
  ground and lined up with idle.

### DEBUG: the camping meteor (M), for a devlog

- **What:** with M on, staying within 50 px (`Rounds.StillRadius`) for 5 s drops a meteor where Khalid stands, and
  another every 5 s he stays. A ground glow and a tightening shadow warn for 1.1 s; a burning rock falls at an angle
  trailing fire, smoke and sparks; the impact is a soft white flash, a flattened shockwave along the ground, an
  upward fireball, square rock debris under gravity, dust rolling out both ways, embers, a scorch mark that fades
  over 5 s, a camera shake and a blast (Ein's kamikaze sound). It hits like an enemy attack — a hostile `Hitbox` for
  one instant (the usual half-block, knocked back from the crater).
- **Why debug only:** a council ruled a camping meteor out of the game (2026-10-13); the owner wanted it for a
  devlog, easy to delete. Everything is in `scripts/debug/MeteorStrike.cs`, built in code (no scene, no asset);
  `DebugTools` adds it and binds M. Removing it = that file and the M lines in `DebugTools`.
- **Tested:** a throwaway headless scene (deleted) in recording mode: one meteor cost one half-block and knocked
  him 69 px; renders of the warning, fall, impact and scorch (the flash, dust and fireball were reworked after the
  first render — a hard disc, dust spilling under the floor and a fireball half underground).

### index32_art: the stage-1 lady and Khalid's dance flattened

- **What:** `art/stages/stage1/lady.aseprite` (the owner's new stage decor: a woman lying on a ledge holding up a
  cup, 5 frames) repaletted and flattened to one colour per part — new profile `tools/repalette/profiles/lady.py`
  (five parts by hue, ramps measured from the owner's own colours) and a flat look in `flat_looks.py` (her ramps'
  second-lightest shades, shared with Khalid's through `_second_lightest`); her content layer renamed `Layer 3` →
  `Thing` so the tools see it; `lady.png` exported (not in the game yet). `khalid_dance_1.aseprite` frames 2–9 were
  flattened through Khalid's profile and look; the owner then redrew the dance to 14 frames, all flat. (index32_art `281140a`.)
- **Tested:** each master's export matched its dry run pixel for pixel; the lady's `Background` / `Platform` layers
  and every frame duration unchanged.

### The lottery stall moved (the owner's edit)

- **What:** in `scenes/levels/stage1/stage1_v2.tscn` the lottery stall stands at x 138 instead of 761 — the owner
  moved it in the editor (2026-10-19). Nothing else in the scene changed (a one-line diff).
- **Could affect:** where players find the stall in the flat arena; the stalls' spacing (Needle Point 1027,
  Figmenter 1290 unchanged).
- **Tested:** headless boot of the arena clean.

### experiments/: dev-only ideas, never in the final game (the meteor moves there)

- **What:** a home for gameplay ideas kept to try out and film. `experiments/<name>/` is compiled only when named in
  `EnabledExperiments` in `mygamedev.csproj` (empty by default) and never in a release export (`ExportRelease`),
  whatever that list says. A compiled-in experiment implements `IExperiment` (`records/experiments/`), is found by
  `Experiments.AttachAll` (`scripts/run/`, called once by `RunManager`) and gets an `ExperimentHost` — the player and
  the arena root, nothing more. The camping meteor moved from `scripts/debug/MeteorStrike.cs` to
  `experiments/meteor/` and is off; while compiled in it is simply on (the M key is gone). New: rule `C8` and the
  all-on QA build in `V6` (`docs/standards.md`), `experiments/README.md`, `docs/game-loop.md` § Parked ideas (the
  meteor, the tag-out stash, the painted arena — why each is out and what would bring it back).
- **Why / how:** the owner wanted ideas like the meteor kept and switchable, on the condition that they are "purely
  for our dev and debug process" and never land in the final build. A council (2026-10-19) cut the first proposal to
  this: no Python tool, no `.gdignore` toggling, no round-event hooks, no moving the other parked material. Off =
  not compiled: an MSBuild `Compile Remove` of `experiments/**` and a target that re-includes the named folders
  (skipped for `ExportRelease`). Discovery is by reflection on purpose — a list in game code would stop compiling
  the moment an experiment is switched off. No scene may reference an experiment (Godot would fail at runtime on a
  compiled-out class).
- **Could affect:** the build (one more target; the default compiles exactly what it did before, minus the meteor);
  `RunManager` startup (one reflection pass over the assembly's types, once).
- **Tested:** build 0 warnings off, with `meteor`, with `all`; the compile counts 198/199/199 files and a release
  build with `all` at 198; the compiled assemblies searched — the meteor class is absent from the default and the
  release build, present with `meteor`. A throwaway headless scene through the real arena (recording mode, standing
  still 7 s; deleted): off — no experiment, no meteor, health 6 → 6; on — the meteor attached itself, fell and cost a
  half-block (6 → 5). Reimport and headless boot clean with it off. **Not verified:** an actual release export (no
  export preset exists yet); `scripts/debug/` still ships in every build — the same release gate should strip it
  when the first export preset is made.

## 2026-10-17 — `new-shit` — Dekken replaced by the lottery stall; triggered healing capped; Crimson Vortex; the art repo reorganised

### The lottery stall: pay first, spin, take what lands

- **What:** Dekken (the perk shop with drink-or-keep vials) is gone; the **lottery stall** stands in its place
  (`LotteryStall` — a working name). E pays at once (20 Lira, +10 per further pull that round, 4 pulls a round; reset
  when a round starts), the orb in the owner's dome-and-beam machine spins 1 s and lands on a random item that takes
  effect immediately. Live: no menu, no pause. Nothing is carried — the vials, the Q / Tab keys and the HUD's vial row
  are deleted. The items (all PLACEHOLDER numbers, `configs/Lottery.cs`): Ruh Flask (+2 charges), Cooldown Flush (next
  3 specials skip their cooldown), Fig Frenzy (+15 % figs and a fig magnet, 25 kills), Bounty (×2 Lira, 25 kills),
  Wider Pull (Come Closer +2 targets, 25 kills, only with Come Closer), and the heal items Leech Draught (10 % of
  landed hits heal half a block, 30 kills), Tithe (every 4th of 8 kills heals), Second Wind (at the last half-block,
  heal a block, once).
- **Added:** `configs/Lottery.cs`; `ids/LotteryIds.cs`; `enums/lottery/LotteryMeasure.cs`;
  `records/lottery/LotteryItemDef.cs`; `scripts/run/LotteryLedger.cs` (the rules: pulls, price, the roll, the held
  items); `scripts/things/LotteryStall.cs` + `scenes/things/lottery_stall.tscn`; `scripts/abilities/lottery/`
  (`LotteryEffect` and one class per item); `scripts/player/parts/HealBudget.cs`; `assets/things/lottery_stall.png` (the stage-1 version of the owner's
  dome-and-beam drawing, `index32_art/art/stages/stage1/lottery_stall.aseprite` — its `Thing` + `Hands` layers in the
  stage's dusk slate, without the `Items` layer; the base in greys is `art/things/lottery_stall.aseprite`). While the
  orb spins, the items' names flicker at it — PLACEHOLDER until item icons exist.
- **Removed:** `configs/Dekken.cs`, `ids/PerkIds.cs`, `enums/perks/`, `records/perks/`, `scripts/abilities/Perk.cs`,
  `scripts/run/PerkLedger.cs`, `scripts/run/VialControls.cs`, `scripts/things/DekkenStall.cs`,
  `scripts/ui/DekkenMenu.cs`, `scripts/ui/VialRow.cs`, `vfx/shaders/vial_recolor.gdshader`, `assets/things/dekken.png`,
  `scenes/things/dekken.tscn`; and what only the perks used — the Shield (`Player.HitShields` and its block in
  `OnHurt`), Prepared (`Player.SurgeFree`, `SurgeState.FireFree`), Fast Travel (`RunManager.FastTravelToFigmenter`),
  the `Passive.OnRoundStart` hook and `Trigger.OnRoundStart`, `HUD.SetVials`. In `index32_art`, the old Dekken art is deleted on the
  owner's word (`art/things/Dekken.aseprite` + `dekken.png`, `art/stages/stage1/Dekken.aseprite` + `.png`); the new
  drawing lives on as `lottery_stall.aseprite`.
- **Changed:** `scripts/run/RunManager.cs` (builds the `LotteryLedger`, gives it each kill and the round start, Lira ×
  `LiraMultiplier`); `scripts/Player.cs` (`HealTriggered` + the `HealBudget`; `NotifyRoundStart` now refills it);
  `scripts/abilities/LifestealBuff.cs` (heals through `HealTriggered`); `scripts/things/Stall.cs` (`SetPrompt`, wider
  prompt); `scripts/ui/BuffList.cs` (held items with KILLS / USES / READY left); `scripts/HUD.cs`;
  `scenes/levels/stage1/stage1_v2.tscn` and `painted_platforms.tscn` (the stall swapped, same spot, x 761); comments in
  `BuffCatalog`, `SfxWorld`, `FadaFig`, `StallMenu`, `WineSlot`, `WorldZ`, `SurgeState`, `Passive`; docs —
  `README.md`, `scripts/run/README.md`, `docs/game-loop.md` (§ The lottery stall, with the item table),
  `docs/buff-catalog.md`, `docs/standards.md` (examples), `docs/painting-levels.md`,
  `docs/future-enhancements-and-fixes.md`.
- **Why / how:** the owner hated Dekken's look and wanted a slot-machine stall (Risk of Rain's chests, the CoD box);
  six councils the same day shaped the rules. Pay-first because declining for free makes "wait for Heal" the only
  play; kills / uses instead of "this round" because rounds never pause; a duplicate extends instead of multiplying
  so Bounty can't run away; the wheel only rolls items that would help now (`LotteryEffect.IsUseful`, the special
  gate, one primed Second Wind), with the heal items' weight scaled to at most a quarter of the rest (≤ 1 in 5 per
  spin). **No bought heal** (the owner's rule): triggered healing — the lottery's heal items AND the Figmenter's
  Bloodrush / Skim — now shares a cap of 2 blocks a round (`HealBudget`, refilled at round start and run start); a
  capped heal shows **CAP** over Khalid; Second Wind is not capped (an emergency heal that could be capped fails when
  it matters). Built on the existing seams: items are `Passive`s (the `ledgers` pattern, rule `S3`), content is data +
  a class per item (`D4`). **Deviations from the councils:** Wider Pull lasts kills, not casts (Come Closer reads the
  bonus when its field spawns, after the cast, so a cast-counted one would end before its last pull); Ruh Flask is
  left off the wheel at full Ruh instead of spilling into "free specials" (specials don't cost Ruh); Last Call was
  cut (it overlapped Tithe).
- **Could affect:** the economy (no heal for sale — health now comes back only from triggers and the Nem surge until
  the planned wine-drink heal is built); Bloodrush / Skim heal less on a long round (the cap); anything that read the
  removed members; the HUD top-left (the vial row is gone); a debug arena rebuild (key 0) leaves held lottery items on
  Khalid without a ledger to count them down — the same as the old perks; a death clears them.
- **Tested:** build 0 warnings; reimport and headless boot clean (the exit-time line is the stage music stream, as
  before); a throwaway headless scene through the real arena passed 29 of 29: the prompt, pay-first pricing 20 → 30 →
  40 → 50, the spin lock, the 4-pull limit and NO PULLS LEFT, the round reset, Bounty ×2 extending to 50 kills and
  ending, Fig Frenzy on/off, Ruh Flask instant and off the wheel at full Ruh, Wider Pull off without Come Closer, heals
  at 20 % of 4000 rolls, the 2-block cap and its refill, Tithe every 4th kill, Second Wind one-at-a-time and firing on
  a hit to the last half-block, Cooldown Flush clearing the cooldown 3 times, the buff list line. A real kill with
  Bounty paid Matat's 3 Lira as 6. A 1280×720 render showed the stall in the arena with its prompt, the spinning orb
  and the landed name (the prompt was then moved above the dome); a second render after the owner asked for the
  stage colours and no `Items` layer showed the recoloured machine and the names flickering in the beam. **Not tested:** by a player — the price, the
  counts, the heal cap and the 1-in-5 share are all first numbers; Leech Draught only by code (random).

### Crimson Vortex on the lottery wheel; the player-side animation hold removed

- **What:** a ninth item, **Crimson Vortex** (25 kills, weight 4 — about 1 pull in 6): while it lasts every dash
  drops the crimson vortex (`vfx/character/khalid/dash/crimson_vortex/`), a spinning red field at the dash's start
  that hits each enemy in it once for 18; when it runs out the dash goes back to the plain one. Added
  `scripts/abilities/lottery/CrimsonVortexEffect.cs`, `LotteryIds.CrimsonVortex` and its `Lottery.Items` row
  (`Value` = the damage).
- **Changed:** a dash effect now carries its own hit tuning — `Player.SetDashEffect(effect, hit)`, with the
  `DashEffect` / `DashHit` getters so an item can put back what it replaced; `DashState` fires the effect with a
  copy of that tuning instead of an empty one. The vortex's `Damage = 18.0` is gone from its `.tscn` (rule `D2`: a
  combat number lives in the data and goes through the tuning seam).
- **Removed:** `Player.HoldAnimation` with its `_holdLeft` / `_channel` state and the hold-cancel on a hit or death,
  `IStrikeWielder.HoldAnimation`, `BlastStrike.OnTuningApplied` and the `Strike.OnTuningApplied` hook it was the
  only user of. The hold froze Khalid's sprite for the vortex's 1.3 s emit on every dash, and a hit cancelled the
  vortex — wrong for an effect left behind. Without the vortex holding him there was no other caller (the enemy
  blasts channel through `Enemy`, which isn't an `IStrikeWielder`), so it was dead code (`C1`).
- **Why:** the owner is recording a demo of the stall and wanted a showy item; the vortex was finished art with no
  way to reach it.
- **Could affect:** dashing (every dash now passes `DashHit` to its effect — empty for the default dash, so the
  same as before); the enemy blasts (untouched: they never used the removed hook); the vortex damage doesn't scale
  with the Needle Point damage shots (its tuning isn't resolved through the stat multipliers — a first choice).
- **Tested:** build 0 warnings; reimport and headless boot clean; a throwaway headless scene through the real arena
  (deleted): the vortex is on the wheel (≈ 17 % of 2000 rolls without Come Closer), applying it swaps the dash
  effect, a dash drops the vortex, Khalid's sprite keeps playing, an enemy inside it went 60 → 42, and after 25
  kills the dash is the default one again with empty tuning. A 1280×720 render showed the vortex left behind at the
  dash start. **Not tested:** by a player — the 18 and the 25 kills are first numbers.


### Zahluq and Bakshen named as the specials they are

- **What:** both were attacks once and are Figmenter-only specials now, but every asset still said `attack_`. Renamed
  in both repos: the sprite sheets (`sprites/characters/khalid/khalid_special_zahluq|bakshen_frames.png`) and their
  animations in `resources/characters/khalid.tres`, the effect scenes (`vfx/character/khalid/special/zahluq|bakshen/
  special_*.tscn`), the sounds (`sfx/character/special/zahluq|bakshen/`), Bakshen's icon (`assets/icons/specials/`),
  the keys in `EmittersCharacters` / `SfxCharacters` (their rows moved into the specials groups) and
  `tools/gen_spriteframes.py`; in `index32_art` the masters, GIFs and frame sheets
  (`art/characters/khalid/khalid_special_*`) and the icon (`art/icons/specials/bakshen`).
- **Removed:** `Action.AnimationOverride` — it existed only to point these two specials at their `attack_`
  animations; a special's animation is `special_<id>` again with no exception.
- **Also:** `project.godot` no longer enables the GDScript formatter addon, whose folder was deleted on 2026-08-24
  (every import warned about it).
- **Could affect:** anything that loaded these assets by their old paths (all callers found and changed; the moved
  files kept their uids); the editor's recent-files list.
- **Tested:** build 0 warnings; reimport clean (the addon warning gone); headless boot clean (the exit-time lines are
  the stage music, as before); a throwaway headless scene through the real arena (deleted) equipped and cast each:
  both played their `special_` animation (Zahluq 4 frames, Bakshen 3), spawned their moved effect scene, resolved
  their sound files and killed the enemy in front.

### index32_art cleaned up and reorganised

- **Deleted:** three Aseprite `.bak` backups (now git-ignored), the orphan `kroj_attack_frames.png` (its master was
  renamed to `kroj_attack_lunge` in August), `art/things/InfinityEye` and Khalid's unused `air_punch_texture_1`
  (both on the owner's word), the empty `effect_sheets/` + `textures/` under Khalid.
- **Moved:** the parked characters (Feyke, Katalyst, Lenbondosen, Wayna) to `art/parked/` — all 212 files kept, the
  owner wants them for later; the promo art (`SteamAssets`, Khalid's `BigDesigns`) to a top-level `promo/`, outside
  `art/` so the export hooks never touch it; the status icons to `art/status/`; `export-character-layer.lua` to
  `tools/`.
- **Renamed** every folder and file to snake_case, matching the game's ids (`art/enemies/matat/`,
  `art/wardens/kroj/`, `khalid.png`, `figmenter`, `lottery_stall`, `needle_point`, `man_choking_statue`,
  `wine_bottle`, `skeleton_chillin`, `tree1`; the misspellings fixed: `planet_with_orbiting_moon`,
  `hero_pic_square1`). The parked characters keep their names. The repalette profiles (`subjects`), the hooks, the
  art README (the layout + the naming rule), the repalette README, this README and the workspace skill follow.
- **Could affect:** only the art repo — the game keeps its own copies of the art. The owner's uncommitted Figmenter
  edits moved with the file (`art/stages/stage1/figmenter.aseprite`), still uncommitted.
- **Tested:** both stall profiles dry-run under their new names; the hook export through the moved Lua script
  matched the committed `matat_idle` sheet pixel for pixel; Khalid's item README regenerates with the new portrait.

## 2026-10-16 — `new-shit` — The enemies and Kroj in flat colours

### Every enemy flat, no outlines, to match Khalid

- **What:** all 36 enemy sheets and Kroj's 5 now have no outline. Kebus, Ein, Ventilator and Kroj use one colour per
  part (Khalid's look); Baghel, Mazab, Matat, Tarri, Breski and Nasen use two tones per part — a base and a darker
  tone kept inside the silhouette — because each is drawn in one colour family and one colour turned them into solid
  silhouettes. Baghel and Breski take a lighter pair than the rest.
- **Changed:** `sprites/enemies/*/*_frames.png` (36), `sprites/wardens/kroj/*_frames.png` (5), from `index32_art`
  `124b736`; `README.md` (Enemy sprites).
- **Why / how:** the owner wanted the enemies to match the flat Khalid; he saw trials of one colour per part for all
  ten, and two tones for the six that went solid, before choosing. Built properly this time: the art repo's new
  `tools/repalette/flatten_to_aseprite.py` applies each subject's look, which is data in
  `tools/repalette/flat_looks.py` (which colours each part takes over, and the colour or base + dark pair they become).
  It reproduces the flat Khalid exactly when run on him. Same sheet sizes and frame counts, so the enemy SpriteFrames
  in `resources/` did not change.
- **Could affect:** how every enemy reads. **Known, not fixed yet:** the enemy glow (`vfx/shaders/enemy_glow.gdshader`)
  blooms any colour bright and saturated enough, and several flat base colours now cover most of a body — Mazab's,
  Matat's and Breski's bodies, Tarri's yellow, Ventilator's red, Kebus's skin. In play Mazab reads as glowing red
  linework instead of a flat body. Next: tune the shader's thresholds (agreed with the owner).
- **Tested:** each sheet matches the approved dry run; build 0 warnings; the frames check is clean; the arena boots
  headless with only the old exit-time line; 1280×720 renders of the arena show the enemies in place.

## 2026-10-15 — `new-shit` — Khalid's flat art cleaned up; the floor's decoration row removed

### Khalid: the owner's cleanup of every animation

- **What:** the owner cleaned stray pixels out of all 28 flat Khalid animations and reworked Swing (still unused in
  the game). 13–456 visible pixels changed per sheet.
- **Changed:** `sprites/characters/khalid/*_frames.png` (28 sheets, from `index32_art` `9ec0fb2` (Bakshen) and
  `e485714` (the rest)).
- **Why / how:** the flatten left stray pixels where shades had sat beside each other; the owner fixed them by hand in
  the `.aseprite` masters. Same sheet sizes and frame counts, so `resources/characters/khalid.tres` did not change.
- **Could affect:** Khalid's look only.
- **Tested:** the frames check is clean, build 0 warnings, the arena boots headless with only the old exit-time line.
  Every sheet is in the 6 flat colours except two stray spots left in the art: Dash frame 3 has 8 pixels of
  `#821C1C` (one step off the hair `#811A1A`) around x 79–82, y 55, and Fall frame 3 one semi-transparent `#4C5550`
  pixel at x 64, y 75. Both still draw (the palette shader takes the nearest colour); they are the owner's to fix.

### The stage: the decoration row along the floor removed

- **What:** the owner removed the row of 98 tileset3 tiles (rocks and plants) along the top of the floor, x -800 to
  2848, in `scenes/levels/stage1/stage1_v2.tscn`.
- **Could affect:** looks only — tileset3 is decoration with no collision (`DECOR_SHEETS` in
  `tools/gen_terrain_tileset.gd`), so movement, spawning and the stalls are unchanged.
- **Tested:** the arena boots headless. Not looked at in a window by me.

## 2026-10-14 — `new-shit` — Khalid in flat colours

### Every Khalid animation: one colour per part, no outlines

- **What:** all 28 of Khalid's sprite sheets now use one colour per material — hair `#811A1A`, skin `#0B8B84`,
  jacket `#4B3328`, trim `#D1C81F`, pants `#2F3D2B`, metal `#60656A` (each ramp's second-lightest shade) — and no
  outline: every darker shade of a material, its rim included, became that one colour.
- **Changed:** `sprites/characters/khalid/*_frames.png` (28 sheets, copied from `index32_art` commit `d7719ec`, where
  the `.aseprite` masters were flattened and re-exported); `README.md` (Palette cleanup).
- **Why / how:** the owner wants a basic flat look ahead of redrawing every animation; he saw a trial on Bakshen, then
  on all 28, before it was applied. The masters were flattened by exact colour mapping (every sheet was already on
  the 36-colour palette), written back through the art repo's own export and import scripts. Same sheet sizes and
  frame counts, so `resources/characters/khalid.tres` did not change.
- **Could affect:** how Khalid reads — where a part overlaps itself (an arm over the coat, two legs) the edge is gone,
  most visible in run, slam and Ground Breaker; the red effects drawn in the hair colour (rope dart, twin reaper,
  spear, death, spawn, Redere Shield) are solid red shapes now. The palette shader still works (the 6 colours are
  slots of its 36): the hair flow pulses the whole flat red toward a darker red, and a colour picked in the
  pre-game picker shows at the shade `PaletteConfig.Derive` puts in slot 1, which can be a little lighter or darker
  than the pick.
- **Tested:** each master's export matches the approved trial pixel for pixel (28 of 28); the frames check
  (`tools/verify_frames.gd`) is clean; build 0 warnings; the arena boots headless with only the old exit-time line.
  **Not tested:** how it looks in play — a windowed screenshot came out too small to judge; the owner checks it with
  F5.

## 2026-10-13 — `new-shit` — Recording mode, and every debug tool in `scripts/debug/`

### Recording mode (H), and the debug tools in one folder

- **What:** a new DEBUG key, **H**, toggles RECORDING mode — just Khalid and the arena, for capturing footage of the
  stage: the HUD is hidden, the round does not run (no enemies, no pressure spawns) and the stalls show no prompt and
  don't open. Each toggle rebuilds the arena, so going in clears the enemies and coming out starts a fresh round 1.
  At the same time every debug tool moved into a new folder, `scripts/debug/`: the developer keys (`DebugTools` —
  0 / Z / X / B / N / H), the buff grant / clear (`BuffCycler`, out of `Player`), recording mode (`RecordingMode`) and
  the compile-time print (`BuildLog`, out of `helpers/`).
- **Changed:** new `scripts/debug/{DebugTools,BuffCycler,RecordingMode}.cs` + `README.md`; `helpers/BuildLog.cs` →
  `scripts/debug/BuildLog.cs`; `scripts/run/RunManager.cs` (the key handler is gone — it adds a `DebugTools` and holds
  the round while `Recording` is on, both lines marked `DEBUG`); `scripts/Player.cs` (`DebugGrantNextBuff` /
  `DebugClearBuffs` gone, a read-only `Passives` list added); `scripts/run/LevelLayout.cs` (`StallGroup` public, so
  recording mode finds the stalls without repeating the string); `mygamedev.csproj` (a comment's path);
  `README.md`, `helpers/README.md`, `docs/standards.md` (rule `C5` and the DEBUG-keys decision name the folder).
- **Why / how:** the owner wanted the debugging tools kept apart from the game. Rule `C5` already asked for debug aids
  "in one place, removable by deleting that place", and they were spread over three files. Now the game code knows
  the folder in two places (`RunManager`'s `_debug` lines, the `HUD`'s `BuildLog` call); deleting the folder and
  those lines removes it all. `Player.cs` is ~30 lines shorter (known debt: its size).
- **Could affect:** the dev keys only. The buff clear now goes through `Player.RemovePassive` per buff (the same
  teardown, the HUD refreshed per buff instead of once). The game itself does not change with recording mode off.
- **Tested:** build 0 warnings; the arena boots headless clean (only the old exit-time resource line). A throwaway
  headless scene drove the keys: B twice granted 2 buffs, N cleared them, H hid the HUD, removed the enemies and
  turned off all three stalls' interact areas, H again brought the HUD and round 1 back — 5 of 5 passed.
  **Not tested:** 0 / Z / X (moved unchanged); recording in a window.
- **Not done:** recording mode survives an arena rebuild it did not start — a death (falling through the bridge gap)
  or the 0 key — and the new arena's stalls open again. Toggle H twice to get it back.

## 2026-10-12 — `new-shit` — Music up, sound effects down, bombs and figs land on brick platforms

### The music is louder, and a horde no longer drowns it

- **What:** the music's level is -5 dB (-12 → -8 → -5, the owner's changes by ear). Every sound effect is 8 dB quieter
  (`Sfx.TargetLoudness` -26 → -34 LUFS): the owner found single cues too loud against the music, not only crowds.
  The SFX bus also has a compressor ahead of its limiter: above -26 dB the summed sound effects rise 1 dB for
  every 4, attack 5 ms, release 200 ms.
- **Changed:** `scripts/audio/Music.cs` (`DefaultVolumeDb`); `scripts/audio/Sfx.cs` (`InstallLimiter` →
  `InstallBusEffects`, four compressor constants).
- **Why / how:** every cue is normalized to one loudness, but sounds SUM — twelve at once are about 11 dB louder
  than one — so in late rounds the fight covered the music. The owner wanted the music clear WITHOUT turning it up
  further. A compressor acts on the summed signal, so it needs no per-cue rules and leaves a quiet moment alone.
  It was already the first item of "Sound: beyond the limiter" in `docs/future-enhancements-and-fixes.md`.
- **Could affect:** how every busy fight sounds; a single loud hit in a crowd has less punch than before.
- **Tested:** build 0 warnings; the arena boots with both effects on the bus. **Not tested: by ear.** The four
  numbers are a starting point; nobody has listened to a late round with them.
- **Not done:** the three stage-1 tracks differ by 2.5 dB (-21.6 / -23.5 / -24.1 LUFS). Noted in
  `docs/future-enhancements-and-fixes.md`: render them at one loudness from the source project.

### The stage: a brick bridge over a gap, and the stalls further apart

- **What (the owner's edit of `stage1_v2.tscn`, in the editor):** a 13-tile gap in the floor left of the stalls
  (x -64 to 352) is bridged with the one-way `tileset2` bricks, level with the floor and with the void underneath;
  the stalls stand further apart (Dekken 761, Needle Point 1027, Figmenter 1290), because standing at the right
  one of three close stalls was confusing. The spawn spot that then sat under Dekken (x 760) was moved to x 606.
- **Could affect:** the bridge is the first one-way ground on the flat stage — that is what exposed the two bugs
  below. Dropping down through a one-way tile there would drop the player into the void: NOT checked.
- **Tested:** played by the owner. The moved spawn spot was not re-run.

### Docs: two notes for later

- **What:** `docs/future-enhancements-and-fixes.md` gained "Animation: smoother motion, maybe the Dead Cells way"
  (what was tried on 2026-10-11 and rejected, and where to pick it up — commit `895babb`) and "Animation: Khalid
  dances when he is left alone" (the owner's idea: after 10 s out of danger with no input he dances; just an idle
  animation, no special cases). Its sound section now records that the compressor is built and that the three music
  tracks differ by 2.5 dB.
- **Could affect:** nothing — notes only.

### Mazab's bomb and dropped figs land on the brick platforms

- **What:** a lobbed bomb and a dropped Fada Fig now land on one-way (jump-through) tiles as they do on solid
  ground. Both fell through them.
- **Changed:** `LobProjectile.SurfaceBetween` casts against `Combat.GroundMask` (solid + one-way) instead of solid
  terrain only; `FadaFig._Ready` sets its collision mask to `Combat.GroundMask` (it had the engine default, solid only).
- **Why:** the owner bridged a gap in the flat stage with the `tileset2` bricks, which are one-way; the bomb's
  landing ray ignored that layer. It still only lands while falling, so a rising bomb passes up through a platform.
- **Tested:** build 0 warnings. **Not tested in the engine** — reported and to be confirmed by the owner's play.

---

## 2026-10-12 — `new-shit` — The arena is flat, and the slam has a cooldown

### One level floor: every enemy can always reach the player

- **What:** the stage's layout is now `scenes/levels/stage1/stage1_v2.tscn` — one level floor end to end, no ledges
  or platforms. The twelve spawn spots sit along it either side of the stalls (none between x 760 and 1500), and the
  three stalls stand together mid-arena on the floor (Dekken 960, Needle Point 1122, Figmenter 1290). The painted
  arena with its ledges and platforms is renamed `painted_platforms.tscn`, which takes it out of the layout pool
  (only `stage1_v*` files are picked); it is otherwise unchanged. `RunManager.StageLayoutPaths` returns the layouts
  in name order.
- **Why / how:** once enemies hunted the player (the entry below), the upper platforms were a place they could not
  follow: they gathered underneath, and most of the stage stood empty. The owner played the flat layout against the
  painted one on the same build and chose flat — "much more fun and it makes the combat way more interactive". The
  flat scene was produced by a script that rewrote the `Terrain` tile data of a copy of the painted scene (the floor's
  mossy top continued where the ledges stood; the big tree lowered to the floor), because the Godot editor was open
  and the painted scene must not be edited while it is (rule `G3`).
- **Could affect:** everything that assumed height. Extra Jump and Jump Height (Needle Point) and the wines that key
  off jumping now matter only for dodging and for the slam, whose damage grows with the height fallen. Dekken no
  longer needs bought jumps to reach. Nothing in the code depends on platforms; `Pursuit`'s ledge logic is simply
  never exercised on this layout.
- **Known and left for the owner:** `painted_platforms.tscn` is an unused file (rule `C1`) — kept because it is the
  owner's painted level and may become another stage; delete it if not. The docs still describe platforms in places
  (`docs/painting-levels.md`, the build-gated places idea in `docs/game-loop.md`).
- **Tested:** build 0 warnings; a scripted headless run of the real arena, 6 of 6 for the layout (no terrain above
  the floor; twelve spots and three stalls on the floor; the floor is level where the ledges were; six hunters reach
  the player); the owner played it. A first version put a spawn spot under Needle Point and the enemy spawned inside
  the stall — the spots were moved clear of the stalls; that fix was confirmed by the owner's play, not by a
  re-run of the check. After the rename, a clean headless boot of the arena.

### The slam has a cooldown

- **What:** after a slam lands, the next cannot start for `Locomotion.SlamCooldown` (1.5 s). Before, it had none and
  could be chained without pause.
- **Changed:** `configs/Locomotion.cs`, `SlamState` (`Cooldown`, checked in `CanStart`, set on impact), `Player`
  (reads the knob, counts it down beside the special's).
- **Why:** so that a slam is a moment and not the whole fight, in either layout. (The slam's damage already grows
  with the height fallen — 1× under 120 px up to 2.5× at 700 px — so jump height already feeds it.)
- **Could affect:** every slam build; Slam Quake / Wrath / Spring / Immunity fire at most once per 1.5 s.
- **Tested:** in the same headless run, 3 of 3: a slam starts from the air; landing starts the cooldown and blocks the
  next; it is ready again once the cooldown has run. **Not built:** anything on the HUD showing the slam is ready,
  and a refund on kills — both were suggested; left until the owner has felt the plain cooldown.

---

## 2026-10-11 — `new-shit` — Enemies hunt the player; the first fig wine comes sooner

### Every round enemy hunts from the moment it spawns

- **What:** a round enemy no longer patrols its spawn spot until the player comes within 320 px. `RunManager` makes
  it a hunter as it spawns (`Enemy.Hunt`), and it comes for him wherever he is. A hunter steps off a ledge when there
  is ground below to land on (never into the void); it still cannot climb, so when the player is on a platform above
  it, it walks to the spot under him and waits there, each hunter up to 40 px to one side so the crowd spreads out.
  When he is below it, it walks toward him and off the first ledge on the way; standing right over him, it keeps
  going the way it faces to the end of its floor. Enemies now spawn at the free spot NEAREST the player (still at
  least 320 px away) instead of the one farthest from the other enemies. Round 1 allows 6 alive at once (was 3);
  the cap still grows by one a round (10 at round 5, 15 at round 10). The last three still speed up to 1.6×.
- **New / changed:** new `scripts/enemies/parts/Pursuit.cs` (the hunting state and the two cases above);
  `EdgeSensor.LandingBelow` (the existing ledge ray cast again 640 px down); `Enemy.Hunt(speedMult, waitSpread)`
  and the pursuit branch in `Enemy.Act` (the `_hunting` / `_huntSpeedMult` fields moved into `Pursuit`);
  `RunManager.OnEnemySpawned` calls `Hunt`; `EnemySpawner.PickSpawnSpot`; `configs/Rounds.cs`: `CapBase` 3 → 6, new
  `HuntSpeedMult` (1.0) and `HuntWaitSpread` (40).
- **Why / how:** the owner found playing a chore — "I am hunting them instead of them hunting me". Spots were spread
  over a 3,600 px arena and grunts walk 34–46 px/s, so hunting alone would have left him waiting up to a minute for
  an enemy to arrive; that is why the spawn-spot choice changed with it. Jumping and teleporting grunts were
  considered and rejected (jump links or navigation per platform, an animation per enemy, and it removes the crowd
  under a platform, which is the slam's best target). The hunting logic is its own part so `Enemy.cs` does not grow
  (rule `S3`).
- **Could affect:** every fight — more enemies reach the player sooner, from round 1. Enemies leave platforms, so
  they can end up crowded on the main floor. A player with a ranged attack can hit a waiting crowd from a platform
  at no risk before the kamikazes start in round 5: NOT addressed here, to be judged in play. Spots near the player
  stay "held" by enemies that have already walked away from them, so in rounds 8–9 (cap 13–14, 12 spots) a spawn
  can wait for a kill. The optional enemies (Nasen, the kamikaze, the Ventilator) are unchanged.
- **Tested:** build with 0 warnings. A scripted headless run of the real arena, 10 of 10: six hunters in round 1; all
  six closed in on a player standing at his spawn, including the ones spawned on platforms, which came down to his
  floor; with the player held on the highest platform all six waited under him within the 40 px band, spread over
  52 px, and none fell into the void; moved to the far floor, all six followed. The saved run-loop check
  (`handoff_files/RunCheck.cs`) passes 8 of 8. NOT tested: a real played run; how the crowd looks; rounds past 2; a
  hunter on a floor whose both ends drop only into the void (it would pace there).

### The first fig wine comes around round 5, not round 9–10

- **What:** a brew costs **4 figs** (was 8) and takes **2 cleared rounds** (was 5); an enemy drops a fig **15 %** of
  the time (was 10 %; Kebus stays 25 %).
- **Changed:** `configs/FigmenterRules.cs` (`Cost`, `BrewRounds`), `Enemy.FigChance`'s default.
- **Why:** the owner's second complaint — the buff system was hard to get at. The Figmenter is the only source of
  build-defining mechanics, and the first one arrived after most of a run.
- **Could affect:** build pace and power — roughly three wines by round 10 instead of one. Ageing (+1 rarity per 2
  carried rounds) is unchanged, so a wine now reaches Epic in four times its brew time.
- **Tested:** the saved wine check (`handoff_files/WineCheck.cs`) run against the new numbers: the flow behaves
  (refused with "NEED 4", 50 → 46 figs, "2 ROUNDS", ready, collect, age, drink), but 5 of its 16 assertions are
  written for 8 figs and 5 rounds and report FAIL — the check needs rewriting to read the constants. Not done.

---

## 2026-10-11 — `new-shit` — The mystery box is now the Figmenter, and it makes fig wine

### Figs brew fig wine: a 5-round wait, a bottle you carry, a buff as strong as the wine is old

- **What:** the spin → offer flow is replaced. At the empty Figmenter **E** pays 8 figs and a brew starts; it takes
  **5 cleared rounds**, the tank filling one frame of the owner's animation per round. The finished wine is labelled
  with its buff; **E** collects it into the player's **one wine slot** (HUD, under the vials). **F** (D-pad up)
  drinks it at any moment. A carried wine climbs one rarity every **2 rounds** — Common, Rare, Hot, Sensational, Epic
  — and the buff is granted at that strength. A ready wine waits in the machine (without ageing) while the slot is
  taken; the machine can brew again as soon as it is emptied.
- **New / changed:** `FigmenterLedger` rewritten (brew state, the carried wine, `OnRoundClear`, a `Changed` event);
  `Figmenter` rewritten to only show the ledger (no per-frame work); new `WineControls` (the key + the HUD push),
  `WineSlot` (HUD), `Wine` (was `FigmenterRoll`), `WineKind` (was `FigmenterOutcome`), `WineRarity`, `BrewState`;
  `FigmenterRules`: `BrewRounds`, `AgeRounds`, rarity colours (`SpinTime` / `OfferTime` gone); cues
  `figmenter_spin` / `figmenter_result` → `figmenter_brew` / `figmenter_ready`. **`BuffCatalog` has five values per
  buff again** (`Func<WineRarity, Buff>`), restored from the tables that commit `2b1000c` collapsed to their middle
  value; `Buff.Rarity` records which was granted and the buff list shows it in the rarity's colour.
  `assets/things/figmenter.png` is now a 6-frame strip with the owner's moss. **Its own sounds:** `figmenter_brew`,
  `figmenter_ready`, `wine_collect`, `wine_drink` (`sfx/things/figmenter/`) — synthesized PLACEHOLDERS that replace
  the reused fig-pickup sound; nobody has listened to them yet.
- **Why / how:** the owner's design — wine takes time and gains value with age. What a wine holds is rolled when the
  brew is PAID for, so figs are never taken for nothing, and shown when it is ready, so drink-now versus carry is an
  informed choice (both decided with the owner). The fill frame is picked from the brew's fraction, not its round
  count, so a later change to the brew time needs no new art. Ageing counts only carried rounds, so holding the
  bottle is the cost.
- **Could affect:** the pace of builds — the first Figmenter buff now arrives about 5 rounds later than before
  (around round 9–10), and buffs drunk young are WEAKER than before (Common is the first of five values; the old
  single value was the third). The debug buff key grants the middle (Hot) strength, as before. A new key, F.
- **Tested:** a scripted headless run, 16 of 16: refused without figs; 8 figs start a brew; E while brewing does
  nothing; frame 3 and "2 ROUNDS" after three rounds; ready, full and labelled after five; a ready wine waits;
  collected as a Common; a second brew while carrying; Hot after five carried rounds; the second wine differs from
  the first; no collecting with the slot taken; drinking grants the buff at Hot and empties the slot; an empty slot
  does nothing; the second wine collects; rarity caps at Epic. The same run windowed, with screenshots of the tank
  half full, the ready label and the HUD slot. The check is saved as `Repos/handoff_files/WineCheck.cs`. **Not
  tested:** a real run of five rounds (the check calls the round-clear hook directly); a wine that is a
  special-swap (6 %, did not come up); the HUD slot's look at full window size; the owner has not played it.

### The wine slot is a bottle

*(Added after the first commit of this entry.)*

- **What:** the HUD's wine slot was a text box; it is now Khalid's one wine bottle — the owner's drawing, simplified
  to 6 colours, shown EMPTY while he carries no wine and FULL (the Figmenter's exact oranges) while he does, with the
  wine's name and rarity beside it. `scripts/ui/WineSlot.cs` rewritten; new `assets/things/wine_bottle.png` (two
  64×64 frames). No rule changed: he could already carry exactly one wine.
- **Why / how:** the owner's design — one bottle he fills at the Figmenter and drinks when he likes. The bottle is
  drawn at half size with the same filtering as the currency icons beside it.
- **Could affect:** only the HUD's top-left corner.
- **Tested:** build 0 warnings; `WineCheck` 16 of 16 in a windowed run, with a screenshot of the corner showing the
  empty bottle and then the full one with its label. **Not tested:** how it reads at full window size (the test
  window was about 630 px wide); the owner has not seen it in play.

### The stall is renamed and drawn, and it stays where it is put

- **What:** the mystery box is called the **Figmenter** everywhere, and its placeholder (a purple square with a "?")
  is replaced by the owner's drawing — a steel vat with a window and a control box — in stage-1 colours
  (`assets/things/figmenter.png`, 96×96). It stands on the ground where the layout places it, like Needle Point and
  Dekken. **The teddy bear is gone:** a spin can no longer refund the figs and send the Figmenter to another spot, so
  the machine never refunds or moves.
- **Renamed:** `MysteryBox` → `Figmenter`, `BoxLedger` → `FigmenterLedger`, `BoxRules` → `FigmenterRules`,
  `BoxRoll` → `FigmenterRoll`, `BoxOutcome` → `FigmenterOutcome`; the folders `enums/box` and `records/box` →
  `…/figmenter`; `scenes/things/mystery_box.tscn` → `figmenter.tscn`; the layout's `MysteryBox` node → `Figmenter`;
  the sound cues `box_spin` / `box_result` → `figmenter_*`; `RunManager._box` / `FastTravelToBox`; comments and docs.
  Older changelog entries keep the old name — they describe what was true then.
- **Removed with the relocation** (rule `C1`): `FigmenterOutcome.Teddy`, `FigmenterLedger.Refund`,
  `FigmenterRules.TeddyChance` / `HardSpotChance`, the cue `box_teddy`, `LevelLayout.BoxSpots`, the layout's
  `BoxSpots` node and its six markers, the Figmenter's `Setup`, beam, fade and `Leaving` phase, and
  `Stall.PlayerArrived` (its only user was the beam). The Figmenter now gets its ledger like the other stalls
  (`figmenter.Ledger = …`).
- **Why / how:** the owner chose the design and the name, wants it to be a machine that makes fig wine, and wants it
  fixed in place like the other stalls. The NAME, the LOOK and the fixed place were done now; the fig-wine mechanic
  is a later design step. Files were moved with `git mv` together with their `.uid` files, so the scene's script
  reference is unchanged. (A hover with rocket fire was built in between and dropped at the owner's request, with
  its effect scene — nothing of it remains.)
- **Could affect:** Dekken's Fast Travel still goes to the Figmenter, which is now always at the same place. A layout must place
  `scenes/things/figmenter.tscn`; it no longer needs spot markers (`stage1_v1.tscn` was updated — it stands at the
  old `Start` spot). The interact area is 80 px wide (was 64) and the prompt sits at the top of the art. E5 loses
  its "pointer to the box" item: there is nothing to point at once it cannot move.
- **Tested:** `dotnet build` 0 warnings; headless arena boot clean; the saved checks pass (`ECheck`: a vial, Nem, a combo; `WineCheck`: above); a WINDOWED screenshot shows the new art standing on the ground beside Khalid, with no
  children under its art (no emitters). The Godot editor was closed while the scenes were edited. **Not tested:** the
  owner has not played it.

---

## 2026-10-10 — `new-shit` — Birds: scenery that flies off when something comes close

### Birds perch around the arena and flee from Khalid

- **What:** up to four small birds sit on random tiles. A bird is placed where the camera can't see it; the first
  time it comes on screen it plays frames 0–3 of its strip and holds; when Khalid comes within 80 px it
  plays frames 4–23 while flying up and away from them, with a flutter sound on frame 4, and is gone. Another arrives
  6–14 s later, again off screen. New: `configs/Birds.cs` (all numbers), `scripts/creatures/Bird.cs` (one bird),
  `scripts/run/BirdFlock.cs` (how many, where), `sprites/creatures/bird/bird.png`, `sfx/creatures/bird/flee.wav`,
  the cue `bird_flee` in `configs/SfxWorld.cs`, `WorldZ.Wildlife`. Changed: `RunManager` builds and ticks the flock;
  `LevelLayout.Tops()` (every exposed tile top); `ArenaGround.SpotIsClear` and `GroundBelow` are now public.
- **Why / how:** the owner wants the arena to feel alive. The bird only reacts to signals (camera visibility, a
  body entering its sensor, the sprite's frame) and ticks only while flying, so a perched bird costs nothing per
  frame. The flock is its own class owned by `RunManager`, like `PressureSpawns` (rule `S3`). The strip is cut into
  frames in code, so nothing hand-written lands in `resources/`. The art was recoloured from a ~1,150-colour
  purple-to-green gradient to four blues in `index32_art` (repalette profile `bird`).
- **Could affect:** nothing in combat — the sensor only listens (`CollisionLayer` 0) and birds are not enemies, so
  rounds, quotas and drops don't see them. `SpotIsClear` / `GroundBelow` kept their behaviour; only their
  visibility changed. Up to four extra sprites and four idle `Area2D`s per arena.
- **Tested:** `dotnet build` 0 warnings; headless arena boot clean; a WINDOWED scripted run (throwaway, deleted):
  4 birds placed off screen, the watched one played frames 0→3 on first being seen and held, took off when Khalid
  was put 50 px away, flew away from him over frames 4–23 (2.1 s) and freed itself, and the flock was back to 4
  after 10.7 s. Screenshots checked by eye. The owner then replaced the sound file, set its mix
  offset from −8 dB to 0 (so the row was removed — unlisted is 0) and approved the result. The owner then asked that ONLY Khalid scare
  birds: the sensor's mask dropped `EnemyBody`. **Not tested:** that an enemy walking past a perched bird leaves it
  there (it follows from the mask; not observed in a run).

---

## 2026-10-09 — `new-shit` — `Player` redesign: one class per state

The second half of the redesign the owner approved. `Player.cs` was 2,213 lines: one class holding fourteen states'
logic and every state's data in one list of ~110 fields. Nothing is meant to play differently.

### What he is doing is a state class; `Player` is what the states share

- **What:** ten state classes in the new `scripts/player/states/`, each with the data only it needs, an `Enter()`
  (its one-off entry effects) and a `Tick()`:

  | Class | Lines | What it is |
  |---|---|---|
  | `FreeState` | 198 | Idle / run / jump / fall (one state — `FreeMode` only picks the animation). Every action starts here. |
  | `SurgeState` | 219 | All of surges: firing, the effect while it lasts, the aura, Nem's sleep, Wara's armed counter. |
  | `AttackState` | 171 | Combos (segment by segment) and flurries (looped while held); the buffered special. |
  | `LaunchState` | 127 | Launch orbs: the orb in range, the pull, the fling, the lock-out. |
  | `DashState` | 112 | The dash / blink and its recovery; the buffered attack. |
  | `SpecialState` | 96 | Casting the special; its cooldown; held specials; the parry window. |
  | `LandState` | 88 | The landing animation and everything that cancels it. |
  | `SlamState` | 84 | The drop and the impact. |
  | `DeathState`, `SpawnState` | 49, 25 | The two animations he cannot act in. |
  | `PlayerState` | 46 | The base: `Animation`, `Enter()`, `Tick()`, and the helpers the states share. |

  Two plain classes in `scripts/player/parts/`: **`Wallet`** (Lira and Fada Figs, with a `Changed` event the HUD
  listens to — callers now write `player.Wallet.SpendLira(…)`) and **`BodyTint`** (the body shader and the hair's
  flare on absorbing Ruh).
  `Player.cs` is 1,180 lines: health and Ruh, the loadout, passives and the tuning seam, taking damage and dying,
  the per-tick pipeline, and the three ways to change state.
- **Why:** the owner's redesign decision. Before, the dash's four timers sat in the same list as the combo's seven
  flags and the surge's thirteen fields, and any of them could be touched from anywhere. Now a state's data is
  private to it; what another state may do to it is a named method (`_attack.StopSwing()`,
  `_launch.Release(lockOut: true)`, `_surge.End()`).
- **How:**
  - The state classes are **nested in `Player`**, which is a `partial` class spread over those files. Nested classes
    can use the player's private members, so nothing had to be made public for them — the shared data is still the
    player's own, but each state's data is no longer everybody's.
  - The fourteen-value `State` enum is gone. `Enter(state)` replaced the old `Enter` and its `switch` (each case
    moved into that state's `Enter()`); `SetFree(mode)` replaced the plain `_state = …` assignments, which
    deliberately skipped those entry effects; `EnterFree(mode)` is the first with a free-movement look.
  - Idle, run, jump, fall and hurt were five enum values that all ran the same code. They are one state.
  - The code inside each state is the old method body, moved by hand and reading the player through `P`.
- **Removed as dead:** `_blinkPhaseWalls` (a flag nothing ever set — the blink always stops at walls), six one-line
  wrappers (`Anim`, `HasTag`, `CooldownOf`, `IsFlurry`, `HasSlam`, `IsShielding`), and `HUD.SetLira` /
  `SetFadaFigs` (the HUD reads the wallet itself now).
- **Could affect:** everything Khalid does.
- **Tested:**
  - **A recorder, run before and after** (`handoff_files/PlayerTrace.cs`). It plays a fixed 52-part script of inputs
    at a fixed frame rate — running, jumping, double jump, ground / air / held dash, a dash with a buffered attack,
    the slam, each of the 5 attacks three ways (one press, held, repeated presses), an attack in the air, an attack
    with a buffered special, each of the 7 specials, the shield held and released, a parry and a block, each of the
    5 surges, a surge with no Ruh, Wara triggered by an enemy's hit, Nem interrupted, a flinch, a knockback, a hit
    mid-attack, a hit during dash i-frames, a gust recovered by air jump and by dash, a launch orb, the wallet,
    death and the restart — and writes one line per physics tick: animation and frame, position, velocity, health,
    Ruh, facing, whether he can be hit, whether he is visible, plus every strike / projectile he produces with its
    values and every sound. Three runs on the old code agreed on all 10,700 tick lines.
    **The redesigned player produces the same file: 0 differing lines**, same attack nodes, same sounds.
  - The other 42 headless checks pass (seam 10, surge / combo / box / vial 6, run loop 8, HUD 14, status 4), with
    the checks' own peeks at private fields updated for the new classes.
  - Applying a character to a player outside the scene tree (the editor's case) logs no error.
  - Build 0 warnings; clean boot of the colour screen and the arena.
  - **Not tested:** real play — the recorder proves the same inputs give the same result, tick for tick, on flat
    ground; it does not cover platforms (drop-through), slopes, or a real fight's timing. The hair flare and the
    HUD's counter pop were not seen.
- **Found, not changed:** jumping at the exact moment of landing (from the land animation) makes no jump sound and
  does not tell passives a ground jump happened, unlike every other ground jump. It was like that before; the
  recorder would flag the fix as a difference, so it is left for the owner to confirm.

---

## 2026-10-09 — `new-shit` — `Enemy` redesign, step 2: its self-contained parts

### Attack sounds, status display, edge sensing and the magnet pull are classes

- **What:** four pieces of `Enemy` that each had their own state and a narrow job moved to the new
  `scripts/enemies/parts/`:

  | Class | Job |
  |---|---|
  | `AttackSounds` | An attack's start cue and its per-frame cues; a held channel's sounds on stoppable players. |
  | `StatusDisplay` | The pips beside the health bar, the halo overhead, the coloured flash over the sprite. |
  | `EdgeSensor` | The two rays that tell a walking enemy there is floor ahead. |
  | `MagnetPull` | Being dragged to a point by Come Closer, and stunned on arrival. |

  `Enemy.cs` is now 864 lines (1,225 before the redesign). What is left is what an enemy *is*: its stats, its
  body, the loop that decides between patrolling, chasing and attacking, taking damage, and dying.
- **Why:** the owner's redesign decision; rule `S3`. Each of these could be read, and changed, without knowing the
  enemy's state machine.
- **How:** code moved as it was. `MagnetPull.Step` returns what the enemy should do (nothing, "you have arrived",
  or a velocity) instead of setting the enemy's fields, so the enemy still owns its own movement and stun.
  Subclasses reach the sounds through `Sounds` instead of three forwarding methods.
- **Left in `Enemy` on purpose:** stun, charm and the reap damage-over-time. They change what the enemy does each
  tick (its state, its target, its health), so they are the state machine, not a part beside it. Hit-stop likewise
  — thirty lines that freeze and shake the enemy's own sprite.
- **Could affect:** enemy attack sounds (including a blast's sounds stopping when it is staggered); status pips,
  halo and flash; enemies stopping at ledges and following slopes; Come Closer.
- **Tested:** the attack recorder again matches the original baseline line for line (104 lines — it includes
  every sound file each enemy plays). The 10 seam checks (Come Closer among them), the 8 run-loop checks and the 4
  status checks pass. Build 0 warnings. **Not tested:** an enemy at a real ledge (the checks run on flat ground and
  the stage's slopes only by chance); a blast interrupted mid-sound.

---

## 2026-10-09 — `new-shit` — `Enemy` redesign, step 1: attacks are objects

The owner approved redesigning `Enemy` and `Player` rather than only moving code. This is the first step for
`Enemy`. Nothing is meant to play differently.

### An enemy's attacks are objects its kit gives it

- **What:** `Enemy` carried 29 flat attack settings (`CloseDamage`, `FarRange`, `FarMode`, `LobArcTime`, …) that
  every enemy had whether it used them or not, and one block of code that branched on them. Now an enemy has two
  optional slots, `Close` and `Far`, each holding an **attack object** from the new `scripts/enemies/attacks/`:

  | Class | What it is |
  |---|---|
  | `EnemyAttack` | The base: type, range, damage, knockback, stun; `Begin()` and `OnFrame(frame)`. |
  | `MeleeAttack` | Lands on the animation's hit frames — slash, shockwave, held blast, lunge, gust. Takes its range from its effect scene's reach. |
  | `RangedAttack` | Base for an attack that releases one thing on its fire frame, from the muzzle. |
  | `ShotAttack` | A projectile: aimed, straight ahead, or a ground wave (`ShotPath`, was `FarMode`). |
  | `LobAttack` | An arcing bomb that lands, dwells and bursts. |

  A kit now reads `e.Far = new ShotAttack { Speed = 200, AimCap = 45 };` or
  `e.Close = new MeleeAttack(StrikeType.Blast) { Range = 140, Damage = 16, Knockback = 120, Stun = 0.3f };`.
  `Enemy` decides only **when** to start an attack; the attack does the rest. `Enemy.cs` went from 1,225 to 1,015
  lines and lost 29 exports and seven methods.
- **Why:** the owner's decision (quality and bloat). A setting that belongs to one kind of attack now lives with
  that attack, a kit can only set what its attacks have, and a new kind of attack is a new class instead of another
  branch in `Enemy`. Enemy ranks will build on this.
- **How:**
  - The two states `Close` / `Far` became one, `Attack`, with the attack in progress remembered. The enemy's
    "start when lined up, in range and off cooldown" logic is unchanged.
  - A melee-only enemy used the far attack's default range (300) as the distance at which it walks in on a
    lined-up target. That number is now its own setting, `Enemy.EngageRange`, with the same default.
  - The Sleeper and the Diver never used the base attack code — they only borrowed its animation name. Each now
    names its own animation (`attack_aoe`, `attack_kamikaze`); their kits no longer set an attack type.
    The Sleeper's rage always hugs the ground (its only kit had the flag on).
  - What an attack needs from its enemy is public on `Enemy`: `Facing`, `Target()`, `SpawnAttack`, `EffectScene` /
    `EffectPos` / `Effect` (were `VfxScene` / `VfxPos`), `HitFramesOf`, `BeginHitstop`, `Lunge`.
- **Removed as dead:** `FarHitboxExtents` and `FarHitboxOffset` were set by two kits and read by nothing. The bare
  melee hitbox's size, position and lifetime were settable per kit, but every kit that set them has an effect scene,
  which is used instead — they are now constants beside the one case that needs them (Kebus's jab).
  `Enemy.CurrentAttackType` had no caller.
- **Could affect:** every enemy's attack — when it starts, what it spawns, its numbers, its sounds, hit-stop and
  lunge; the Sleeper's rage and the kamikaze's dive.
- **Tested:**
  - **A recorder, run before and after.** For all 10 kits, each spawned far from and next to a standing player, it
    records everything the enemy produces: each strike, projectile, lob and hitbox with every exported value after
    tuning (damage, knockback, stun, gust, speed, range, life, flags, shape sizes) and each sound file played.
    Two runs on the old code gave the same 104 lines; the redesigned code gives **the same 104 lines**.
  - The 10 seam checks, the 8 run-loop checks and the 4 status checks pass. Build 0 warnings.
  - **Not tested:** how it looks and feels in play; an enemy attacking while charmed; the Warden outside the
    recorder's two scenarios.

---

## 2026-10-09 — `new-shit` — unused `Player` members removed; the rest written down

### Nine unused members leave `Player`

- **What:** deleted `BaseRunSpeed`, `JumpVelocity`, `DashSpeed`, `Gravity`, `SlamSpeed`, `MaxAirJumps` (read-only views
  for a HUD debug panel that no longer exists), `GetState`, `CurrentAttack` and `CurrentSpecial`. Nothing called any
  of them. With `GetState` gone, the `Player.State` enum is `private` — nothing outside the player reads it.
- **Why:** rule `C1`; the owner picked these from the list in part 4.
- **The other unused members stay**, by the owner's decision, as groundwork for named features (volume sliders, a
  dash effect, a character ability, more characters, …). Rule `C3` keeps TODO notes out of the code, so they are
  listed in `docs/future-enhancements-and-fixes.md` under "Built, not wired up yet", each with the feature it waits
  for.
- **Could affect:** nothing — the compiler confirms no caller existed.
- **Tested:** build 0 warnings; the 10 seam checks and the 6 surge / combo / box / vial checks pass.

---

## 2026-10-09 — `new-shit` — enemy status display stops allocating every tick

### `Enemy.RefreshStatusIcons` compares flags instead of building a list and a string

- **What:** every living enemy, every physics tick, built a new `List<StatusType>` and joined it into a string just
  to see whether its status pips (reap / stun / charm) had changed. It now packs the three conditions into an `int`,
  compares that, and builds the list only when the set really changes.
- **Why:** rule `P1` (no allocation on the per-frame path). With 12 enemies alive that was about 1,400 short-lived
  objects a second for the garbage collector. Found while reading `Enemy` for step F.
- **Could affect:** the status pips beside an enemy's health bar and the halo over its head.
- **Tested:** a 4-check headless scene: no pips at rest; Stun after a stun; Reap, Stun, Charm in that order when all
  three apply; none once they run out. The 10 seam checks pass. Build 0 warnings.

### Step F stops here for `Enemy` and `Player` — and why

`RunManager` and `HUD` were split because they were several jobs sharing a file. `Enemy` (1,226 lines) and `Player`
(2,219) were read with the same intent and left alone, deliberately:

- Each is **one state machine** whose states share about forty fields. No block of methods can leave with a narrow
  interface. Moving `Enemy`'s attack code (445 lines) into its own class would mean handing that class some thirty
  of `Enemy`'s fields and exports — two classes that cannot be understood apart, which is worse than one.
- What can leave cleanly is small: about 200 lines of `Enemy` in four pieces, about 450 of `Player` in four. That
  takes them to ~1,000 and ~1,750 — still oversized, for real risk in the code every fight runs through.
- Making them properly small is a **design change**: an enemy's attacks as behaviour objects its kit picks; the
  player's states as state objects. That changes how kits and moves are authored, so it is the owner's decision,
  and it is best done with the feature that needs it (enemy ranks; the loadout picker).

Recorded under Known debt in `docs/standards.md`. Nothing in `Player` was changed.

---

## 2026-10-09 — `new-shit` — cleanup, part 7 (splitting the big classes: `HUD`)

Step F, second class. `HUD.cs` was 624 lines holding every widget's fields, constants and logic in one class. It is
now 179 lines: the screen layout, the binding to the player, and the `Set…` methods the run calls. One commit.
Nothing is meant to look or behave differently.

### The HUD's parts are their own classes

- **What:** new classes in `scripts/ui/`:

  | Class | Lines | Job |
  |---|---|---|
  | `HudGauge` | 240 | Health stars, Ruh orbs, the special bar; the Screen / FollowKhalid placement; dim-at-rest, bright on change. |
  | `RoundBanner` | 106 | ROUND n / n LEFT / BEST n, and the flying round-start intro. |
  | `BuffList` | 85 | The top-right list of shots, perks and box buffs. |
  | `LowHealthVignette` | 81 | The low-health screen effect and its heartbeat. |
  | `VialRow` | 36 | The carried-vial slots. |

  `UiStyle.HudLabel(style)` replaces the HUD's private label maker, now that three classes need it.
- **Why:** rule `S3`. The round intro's timing constants sat between the gauge's pixel scale and the low-health
  fade; a change to one widget meant reading past all the others.
- **How:** method bodies moved as they were. The widgets that are controls (`RoundBanner`, `BuffList`, `VialRow`) set
  their own anchors in their constructors — the same values the HUD used to set from outside. `HudGauge` is a plain
  node that owns both of the gauge's homes (the screen box and the world-following layer). The gauge stays bright at
  low health by being told so each frame (`LowHealthVignette.Active`) instead of reading the vignette's field.
  The pause menu's "gauge placement" setting now calls `HudGauge.ApplyPlacement` directly.
- **Removed:** five "is the HUD built yet?" null checks on the `Set…` methods. The HUD is an autoload, built before
  any scene can call it.
- **Could affect:** everything on the HUD — the gauge and its two placements, its brightness, the round block and
  intro, the counters, the vial slots, the buff list, the low-health effect — and when the HUD shows and hides.
- **Tested:**
  - A new 14-check headless scene, written and passed on the HUD **before** the split, then on the split HUD with
    identical output. It reads only what the HUD shows (node types, label texts, visibility): 3 stars / 3 orbs / the
    special bar; the round, n LEFT and BEST texts through `SetRound`, and n LEFT hiding at 0; both counters; one
    framed slot per carry slot with the vial names; a granted buff's name and description, and the list emptying;
    the low-health layer on at 1 HP and off after healing; FollowKhalid putting the pips on the world layer with one
    follower on the player, and Screen putting them back and removing it.
  - Screenshots from a windowed run before and after, compared region by region (counters, round block, gauge):
    the same to the eye; the few differing pixels are the background behind them (the window came up one pixel
    wider the second time).
  - Build 0 warnings; clean boots.
  - **Not tested:** the round intro's flight (the check waits for it to land), the heartbeat's look, the pause menu
    switching the placement by hand.

---

## 2026-10-09 — `new-shit` — cleanup, part 6 (splitting the big classes: `RunManager`)

Step F, first class. `RunManager` was 1,087 lines doing eight jobs. It is now 421 lines that own the order of
things and the round loop; seven parts are their own classes. One commit. Nothing is meant to play differently.

### `RunManager` is the round loop; its parts are classes

- **What:** new classes in `scripts/run/`, each with one job:

  | Class | Lines | Job |
  |---|---|---|
  | `EnemySpawner` | 215 | Who spawns and where: the roster, per-type caps, spawn-spot choice, near-player spawns, building an enemy from its kit, the living list. Events `Spawned` / `Died` / `Damaged`. |
  | `PressureSpawns` | 124 | The stand-still kamikazes and the edge Ventilator, each with its clock. |
  | `ArenaGround` | 125 | Where things can stand: floor tile under a point, clear of props, headroom, inside a wall. |
  | `DeathSequence` | 132 | What plays between dying and the restart (cinematic, or the fall wait). A child node. |
  | `RunCamera` | 91 | Follow spring, spawn / death drift, the three zoom levels. |
  | `ArenaBackdrop` | 65 | The background image and tint. A `CanvasLayer`. |
  | `VialControls` | 82 | Drink / cycle keys for carried vials and the HUD's vial row. A child node. |

  `RunManager` keeps: building the arena, the round counters and curve, the stragglers' hunt, drops and Ruh orbs,
  the attack picker, the restart, and the `DEBUG` keys.
- **Why:** rule `S3` and the "oversized classes" item under Known debt. A change to how the camera follows used to
  mean opening the same file as the spawn rules and the death fade, with all their fields in one list.
- **How:**
  - The method bodies were moved as they were (lifted by a script, not retyped); what changed is how the pieces
    reach each other. The spawner no longer bumps the round's counters itself: it raises `Spawned` / `Died` /
    `Damaged`, and `RunManager` and `PressureSpawns` listen.
  - The spawner, the ground and the pressure spawns are **rebuilt with each arena**, so their state resets by
    construction — `BuildArena` used to zero eleven fields by hand.
  - `ArenaGround.HeadroomAbove` takes its height limit as a parameter instead of reading the kamikaze's constant, so
    the ground knows nothing about kamikazes.
  - **Shared helper:** `PlaceAt` (set a position + reset interpolation) existed as four identical private copies
    (`RunManager`, `Enemy`, `LobProjectile`, `ParticleDirector`). It is now `helpers/Nodes.cs`, used by all (rule `O2`).
  - **Dead code removed:** the arena scene still carried a legacy `Floor` body that `RunManager.BuildFloor` switched
    off on every start ("the painted layout is the terrain now"). The node is deleted from `scenes/arena.tscn`, and
    `BuildFloor` with it. Also gone: about twenty `_player == null` / `_player?.` checks in `RunManager` — the player is required
    and fetched once.
  - The backdrop now unsubscribes from the window's resize signal when it leaves the tree (it never did).
  - `helpers/README.md` described four GDScript files that no longer exist; rewritten for what is there.
- **Could affect:** the whole run loop — spawning and its caps, where enemies appear, rounds clearing, the two
  pressure spawns, drops, the camera in play / spawn / death, the death cinematic and fall death, the restart, the
  background, vial keys, Fast Travel.
- **Tested:**
  - A new 8-check headless scene of the run loop, written and passed on the code **before** the split, then on the
    split code with the same results: round 1 starts and enemies spawn at separate spots; the camera stays on the
    player; killing the quota starts the next round; standing still draws a kamikaze after 2.0 s at the same offset;
    standing at the arena's end draws a Ventilator on the inland side after 1.0 s; kills pay Lira; death restarts
    the run; the new run spawns enemies.
  - The earlier 15 checks, plus a new one — a kept Heal vial is drunk with the vial key (3 → 5 half-hearts, pocket
    empty). All 24 pass.
  - Two screenshots from a windowed run: in play (background image, tint, arena, HUD all drawn) and 1.3 s into the
    death cinematic (zoomed on Khalid, world faded to black behind him).
  - Build 0 warnings; clean boot of the colour screen and the arena.
  - **Not tested:** the fall death (walking off the arena); Fast Travel; the feel of the camera (the follow maths
    is unchanged, the check only confirms it tracks); a window resize.
- **Left as is:** `RunManager` still builds the glow environment and holds the drop code (~50 lines). The drop code
  is the next thing to carve when drops grow (round drops are planned).

---

## 2026-10-09 — `new-shit` — cleanup, part 5 (no more SCREAMING_CASE)

### Constants, tables and `Player.State` have C# names

- **What:** the last GDScript-style names — 88 `SCREAMING_CASE` constants, static tables and enum members — are
  `PascalCase`: `SfxCharacters.CUES` → `Cues`, `EmittersCharacters.TABLE` → `Table`, `EnemyKits.KEBUS` → `Kebus`,
  `ActionsKhalid.ATTACKS` → `Attacks`, `SaveData.MAX_SCHEMES` → `MaxSchemes`, `Player.State.ATTACK` →
  `State.Attack`. Two got a clearer name instead of a literal one: `SaveData.PATH` → `SavePath`,
  `PaletteConfig.DEFAULT` → `DefaultShades`.
- **Why:** rule `G6` (one naming style), and rule `T1` already said enum members are PascalCase. The owner asked for
  it after part 4.
- **How:** the same compiler-driven rename as part 4. None of these names is exported or stored in a scene, so no
  scene file changed. Comments and the backticked names in the docs follow. `G6` now covers constants; the
  `SCREAMING_CASE` item is gone from Known debt.
- **Could affect:** nothing at run time — an enum member's name is not stored anywhere (scenes and the save file
  hold numbers and strings, and the settings saved by name use `GaugePlacement`, which was already PascalCase).
- **Tested:** build 0 warnings; the 15 headless checks pass; the colour screen and the arena boot clean.
- **Left as is:** words in capitals inside comments that are emphasis, not names ("the SURGE meter", "by ANY route").

---

## 2026-10-09 — `new-shit` — fixes from the owner's first run after the cleanup

The owner pressed F5 and reported two things: an engine error in the Output panel, and no compile-time line.

### Player no longer looks up the HUD from outside the scene tree

- **What:** `Player` reached the HUD autoload with `GetNodeOrNull<HUD>("/root/HUD")` in seven places. They now go
  through one private property, `Hud`, which returns null while the player is not in the scene tree.
- **Why:** the editor printed `ERROR: Can't use get_node() with absolute paths from outside the active scene tree.`
  `Player` is a `[Tool]` script, so it also runs inside the editor. When the editor applies the `Character`
  property to a player that is not in the tree (a scene in a background tab; a script reload after a build),
  `ApplyCharacter` → `SeedPassives` → `ClearPassives` → `RefreshBuffHud` asked for `/root/HUD` from there.
- **Is it from the cleanup?** The code path is the same in the commit before the cleanup began (`744ec1f`), so the
  fault is old. What made it show now is not established — most likely the editor reloading every script after the
  rename. It was harmless either way: the lookup returned null and the game was unaffected.
- **Could affect:** the buff list, Lira and fig counters on the HUD (all seven sites). In the running game the
  player is always in the tree, so nothing changes there.
- **Tested:** reproduced first — a headless scene that instantiates `player.tscn` without adding it to the tree and
  sets `Character` printed the exact error; after the fix it prints nothing. The 15 headless checks pass (they cover
  the fig counter through the mystery box).

### The compile time shows where the owner looks

- **What:** (1) the game prints `Last C# compile: <when>  <configuration>  <seconds> s  <files> files` as its first
  line in the Output panel when started from the editor — new `helpers/BuildLog.cs`, called from `HUD._Ready`,
  labelled `DEBUG`, inactive in an exported game. (2) `build_times.log` now gets a line only when the compiler
  actually ran; builds with nothing to recompile used to add "0.03 s" lines.
- **Why:** the build did log on F5 — the line was in `build_times.log` — but the editor shows build output only in
  its MSBuild panel, not in Output, so the owner never saw it. And with the "0 s" lines, "the last line of the log"
  was usually meaningless.
- **How:** the build target compares the assembly's modified time with the moment the compile step started; if the
  assembly was not rewritten, the compiler did not run and nothing is logged.
- **Could affect:** nothing in the game.
- **Tested:** a first build, a no-change build (no line), a one-file-touched build and a clean build wrote three
  lines; `ExportDebug` and `ExportRelease` build clean; a headless run printed the line. **Not tested:** seeing it
  in the editor's Output panel on F5 — that is the owner's check.

---

## 2026-10-09 — `new-shit` — cleanup, part 4 (C# names everywhere)

Step G. The classes ported from GDScript kept their `snake_case` public names (`player.take_damage()`,
`enemy.max_health`, `_sfx.play_at()`), because GDScript callers and scene files addressed them by name. Every
caller has been C# for a long time. One commit. Nothing is meant to play differently.

### Every member has a C# name

- **What:** 328 members renamed to `PascalCase` across 29 classes — methods, properties, public fields, and the
  seven signals. The big ones: `Player` (81), `Enemy` (67), `Locomotion` (23), `Projectile` (21), `Hitbox` (19),
  `LobProjectile` (16), `SurgeSpec` (13), `OrbitAura` (13), `Sfx` (10), `DiverEnemy` (10). Examples:
  `take_damage` → `TakeDamage`, `max_health` → `MaxHealth`, `is_dead()` → `IsDead()`, `play_at` → `PlayAt`,
  `make_loop_2d` → `MakeLoop2D`, signal `health_changed` → `HealthChanged`, `died` → `Died`.
  No lowercase-named member is left in the game's code.
- **Why:** rule `G6` (now rewritten: C# naming everywhere) and the last "leftover of the GDScript port" item under
  Known debt. Two naming styles in one codebase meant every call site made you remember which style that class used.
- **How:**
  - **The rename was done by the compiler's own rename engine** (a throwaway Roslyn tool, not kept in the repo), not
    by search-and-replace. It renames a symbol and every reference to that symbol, and nothing that merely has the
    same spelling — which matters for names like `health`, `speed`, `source`, `damage`, `count` and `spawn`.
  - **Scene files.** An `[Export]` name is the key a `.tscn` stores. 27 of the renamed exports are set in scenes: 88
    property lines in 35 scene files were renamed with them. The rewrite is scoped to nodes whose script owns that
    export — `lifetime` and `gravity` are also properties of the engine's particle nodes (162 such lines), and those
    were left alone.
  - **Four name clashes, settled by hand.** `Player` had public snake_case aliases in front of private methods of the
    target name: `has_anim` / `fire_effect` (no outside caller — the alias is gone), `refresh_buff_hud` (the private
    `RefreshBuffHud` is now public), and the property `run_speed` beside the method `RunSpeed()` — the property is
    now `BaseRunSpeed`. In `ParticleDirector` the private field `node` became `Root`, since `Node` is an engine type.
  - **Strings.** The only member addressed by a string was `Box.CallDeferred("deactivate")`; it now uses the
    generated `Hitbox.MethodName.Deactivate`, which the compiler checks. Every other string handed to the engine
    names an engine property (`modulate:a`, `scale`, `zoom`…).
  - **Comments and docs** were updated to the new names: 63 comment lines, and the backticked names in `README.md`
    (about 200 lines), `scripts/run/README.md`, `vfx/README.md` and four smaller docs. The class comments that
    explained why the names were snake_case are rewritten.
  - **Rules:** `G6` rewritten; new `G7` — an `[Export]` name is a file format, and how to rename one safely.
- **Could affect:** everything, in principle — it touches 100+ files — but only in two ways. (1) A reference the
  compiler cannot see: there is none left that names a renamed member (checked, see below). (2) A scene value that
  no longer finds its property: Godot drops it silently and the property takes its default.
- **Tested:**
  - Build: 0 warnings, 0 errors.
  - **Scene values: identical.** Before the rename, a headless tool instantiated all 47 scenes and wrote down every
    script-exported property of every node — 1,005 values. Run again after the rename and compared with the names
    mapped: 1,005 of 1,005 present, 0 different.
  - The 10-check and 5-check headless scenes (ported to the new names): all 15 pass.
  - Clean headless boot of the colour screen and the arena; the owner's save backed up and restored, identical.
  - Every string literal passed to `TweenProperty` / `CallDeferred` / `Call` / `Connect` listed and checked: all are
    engine properties.
  - The four GDScript tools under `tools/` and `vfx/script/` reference none of the renamed names.
  - **Not tested:** real play. The scene-value check is the strong one here; what it cannot see is a scene value that
    happened to equal the default both before and after.
- **Not done, on purpose:**
  - `SCREAMING_CASE` names — the static tables and constants (`CUES`, `TABLE`, `KEBUS`, `MATERIALS`, `MAX_SCHEMES`,
    … about 60) and `Player.State`'s members (`State.ATTACK`). Now listed under Known debt; the same tool can do
    them in one pass.
  - `SurgeSpec.Trigger` is still a string (`"cast"` / `"hit"`) where rule `T1` wants an enum.
  - Prose in the docs that names a member without backticks, and the historical docs (`docs/csharp-migration.md`,
    `docs/game-design.md`, `docs/rewards-design.md`), were not touched.
- **Found, not changed — unused members, for the owner to decide** (the rename tool can list what nothing
  references):
  - `Player`: `BaseRunSpeed`, `JumpVelocity`, `DashSpeed`, `Gravity`, `SlamSpeed`, `MaxAirJumps` (read-only views
    "for the HUD debug stats panel", which no longer exists), `GetState`, `CurrentAttack`, `CurrentSpecial`,
    `SetDashEffect` (the unwired crimson-vortex hook noted in part 1).
  - Audio controls: `Sfx` / `Music` `SetVolume`, `GetVolume`, `SetMuted`; `Music.Pause` / `Resume`;
    `AudioBus.SetVolumeDb`, `IsMuted`, `GetEffect`, `SetEffectEnabled`.
  - `CharacterConfig.IDS`, `FramesPath`, `PortraitPath`, `AbilityPath`; `PaletteConfig.SHADES_PER`;
    `VfxPalette.HueFor`; `StrikeTypes.From`; `StatusDef.Label`; `SurgeSpec.DamageTakenMult`;
    `Enemy.CurrentAttackType`.
  - Kept on purpose: `EnemyKits.KROJ` (Warden rounds are planned) and 22 `BuffIds` for buffs not built yet.

---

## 2026-10-09 — `new-shit` — compile-time log

### Every build logs how long the C# compile took

- **What:** two small build targets in `mygamedev.csproj` time the compile step and (1) print
  `Compile time: 1.40 s (144 files, Debug)` in the build output, (2) append a line to `build_times.log` in the repo
  root — date and time, configuration, seconds, number of source files. The log is git-ignored: timings depend on
  the machine.
- **Why:** the owner asked for data on compile time while the cleanup goes on.
- **How:** the targets hook `CoreCompile` (the compiler run itself), not the whole build, so the number is not
  blurred by restore, asset copying or the build's fixed start-up cost. They run for every build, including the ones
  the Godot editor starts. A build with nothing to recompile logs about 0 s.
- **What the numbers say so far:** a full build of the code as it was before the cleanup (`744ec1f`, 133 files,
  15,192 lines) took 2.02 s; today's code (141 files, 15,169 lines) takes 1.90 s — the average of four clean
  builds each, whole build, same machine. The difference is within noise. **The cleanup is not a compile-time
  win and was never going to be:** the game is ~15,000 lines and compiles in under a second and a half. What the
  cleanup buys is errors caught at compile time instead of in play. Compile time will matter if the code grows
  10× or a slow source generator is added; the log is there to show it if that happens.
- **Could affect:** nothing in the game. If the repo folder is read-only the build fails at the log write.
- **Tested:** a clean build, a no-change build (0.03 s) and a one-file-touched build (1.32 s) each wrote one line.

---

## 2026-10-09 — `new-shit` — cleanup, part 3 (zero compiler warnings)

Step E. The project has had nullable reference checking switched on since the C# port, and the build printed 196
warnings that nobody could read through. It now prints none, and a new warning fails the build. One commit.
Nothing is meant to play differently.

### The build is warning-free, and stays that way

- **What:** all 196 warnings are fixed — none is silenced — and `mygamedev.csproj` now sets
  `TreatWarningsAsErrors`. Almost all were about `null`: a field, parameter or return value declared as "never
  null" that could be. Each was settled one of two ways (now rule `T7` in `docs/standards.md`):
  - **It can really be absent → the type says so (`T?`) and every use handles it.** Examples: the player's equipped
    attack / special / surge (absent in the editor and for a character with no sprite frames), the launch orb in
    range, the mystery box's current roll, the HUD's bound player, the run's camera and background image,
    `Actions.GetAction`, `BuffCatalog.Make`, `BoxLedger.Spin`, `LevelLayout.Placed`, the sound service's
    `make_loop` / `make_oneshot`, the `source` of the enemy `damaged` signal.
  - **It is always set before anything reads it → declared non-null with `= null!`.** 67 fields: nodes built or
    fetched in `_Ready` (HUD rows and labels, the player's sprite and hurtbox, menu widgets) and values handed over by
    the one `Setup` / `Open` call. Each was checked to be assigned unconditionally on that path.
- **Why:** rule `V1`. With ~200 warnings on every build, a new one — a real "this can be null here" — was invisible.
  At zero, the compiler does that review on every build for free, and the rule can be enforced by the build itself
  instead of by comparing counts.
- **How — the places where code changed shape, not just a type:**
  - **Surges** (`Player`): `CanSurge()` plus two unguarded reads of `_currentSurge.Surge` became one
    `ReadySurge()` that returns the surge and its spec together, or nothing. `FireSurge` / `BeginSurge` take the
    action as a parameter. A channelled surge (Nem) remembers its sleep time in `_surgeSleepTime` when it begins,
    instead of re-reading the equipped surge mid-channel.
  - **Attacks and specials** (`Player`): `StartSpecial`, `AdvanceCombo`, `StartFlurry` and `SpecialStrikeFrame`
    bind the equipped action once at the top and stop if there is none. Before, a missing action would have thrown
    a null error at the first use; for Khalid there is always one, so this path is not reachable in play today.
  - **Mystery box**: the roll is passed to `Reveal(roll)` and bound once in `Interact`, instead of read from a
    nullable field at each step.
  - **Enemy `damaged` signal**: emitted through the generated typed method with `Node? source` (a
    damage-over-time tick whose attacker is gone has no source). This removed a `null!` that was lying to the compiler.
  - **`ParticleDirector`**: a sustained effect's record takes its fields in its constructor (all read-only except
    `active`), so it cannot exist half-filled.
  - **Colour screen**: `SwatchRow` took three optional arguments for two different jobs; it is now `PickerRow`
    (makes and stores a picker) on top of `SwatchRow` (lays a row out).
  - **`RunManager`**: `SpawnEnemy` cannot return null, so the "spawn failed" branches in `SpawnAt` / `SpawnOne` were
    dead and are gone. The player is fetched with `GetNode` (a missing Player now logs an engine error at start
    instead of failing later).
  - **Leftover of the GDScript port removed**: `Player.RUH_PER_BLOCK`, an instance property that existed only
    because GDScript cannot read a C# constant. `Player.RuhPerBlock` is now a public constant; HUD and RunManager
    read it.
  - **One deprecation**: `SpriteFrames.SetAnimationLoop` → `SetAnimationLoopMode(…, Linear)` in `OverheadStatus`.
- **Could affect:** firing a surge (button, and the Prepared perk's free one), Nem's sleep and heal, Wara; starting
  a special, a combo or a flurry; the special's strike frame (the cooldown refund when hit during a windup); the
  mystery box's spin / offer / take / teddy bear; Ruh orbs and damage numbers (they hang off the `damaged`
  signal); the health-and-Ruh gauge following Khalid; the looping status icon over an enemy; the colour screen's
  picker rows.
- **Tested:**
  - Build: 0 warnings, 0 errors in all three configurations (`Debug`, `ExportDebug`, `ExportRelease`).
  - The 10-check headless scene from parts 1 and 2 — all pass.
  - A second headless scene for what this step touched — 5 checks, all pass: Nem falls asleep for its full
    duration (4.98 s left on the sleep frame) and heals one star, then ends; a non-flurry combo (Spear) starts and
    advances; the mystery box takes 8 figs and spins; the offer is taken and the buff joins the player.
  - Clean headless boot of the colour screen and the arena. The owner's `save.cfg` was backed up before and
    restored after each run.
  - **Not tested:** the teddy-bear branch of the box (the roll did not land on it); the gauge following Khalid and
    the overhead status icon (nothing draws headless); the colour screen's rows by eye.
- **Left as is — for the owner to decide:**
  - Several `if (_sprite != null)` / `if (_player != null)` checks guard things that are now declared never-null.
    They are harmless and were left, because `Player` is a `[Tool]` script that also runs inside the editor and
    removing them safely means walking each editor path.
  - Unused audio controls: `Sfx` / `Music` `set_volume`, `get_volume`, `set_muted`, and `AudioBus.SetVolumeDb`,
    `IsMuted`, `GetEffect`, `SetEffectEnabled` have no caller. They look like the groundwork for volume sliders in
    the pause menu. Keep if sliders are coming; delete under rule `C1` if not.
  - `Player.CharacterAbilityFor` always returns null (no character has an intrinsic ability). Same question.

---

## 2026-10-09 — `new-shit` — cleanup, part 2 (typed content tables)

Step D of bringing the code up to `docs/standards.md`: the game's content tables and the data passed around with
them were Godot dictionaries with string keys — a leftover of the GDScript port. They are now typed. One commit.
Two things play differently, both listed under "Behaviour changes" below; nothing else is meant to.

### Content tables and their readers are typed

- **What:**
  - **Enemy kits** (`scripts/run/EnemyKits.cs`). A kit is an `EnemyKit` record (`records/enemies/EnemyKit.cs`): id,
    display name, tier, movement, scene, optional `SpawnCap` / `LiraDrop`, and a `Tune` function that sets the
    enemy's stats on the typed instance (`e.attack_range = 150f`). `EnemyKit.Of<SleeperEnemy>(…)` gives a kit for a
    subclass a typed `Tune`. `RunManager` holds `EnemyKit[]` and no longer applies stats with `enemy.Set(name, value)`.
    `Enemy.far_mode` is the new `FarMode` enum (`Aimed`, `Forward`, `GroundWave`, `Lob`) instead of a string.
  - **Sound tables** (`configs/SfxCharacters.cs`, `SfxEnemies.cs`, `SfxWorld.cs`): `CUES` is
    `Dictionary<string, string>`, `VOLUMES` `Dictionary<string, float>`, `PITCH` `Dictionary<string, Vector2>`, and
    the frame-synced `FRAMES` a typed nested dictionary. `Sfx` and `Enemy` read them without `Variant` casts.
  - **Emitter tables** (`vfx/config/EmittersCharacters.cs`, `EmittersEnemies.cs`): a row is an `EmitterDef` record
    (`records/vfx/EmitterDef.cs`) — scene, position, `Mode` (the new `EmitterMode` enum), `Frames`, `AllFrames`,
    `ConformToGround`, `Follow`, and a typed `Configure` hook. `ParticleDirector` and `Enemy` read the record.
  - **Animation metadata** (`helpers/AnimMeta.cs`): hit frames, sheet start and loop bounds are parsed once per
    `SpriteFrames` into typed tables and cached. `HitFrames` returns `IReadOnlyList<int>`; `LoopBound(…, "loop_from")`
    became `LoopFrom` / `LoopTo`; `HitFramesOrAll` is the player's "every frame is a combo step" fallback.
  - **Colour picks and saved schemes**: picks are `Dictionary<string, Color>`; a saved scheme is a `ColorScheme`
    record (`records/ui/ColorScheme.cs`). `SaveData` converts to and from the engine's dictionaries only inside its
    read / write helpers; `PaletteConfig`, `VfxPalette` and the colour screen never see one. `SaveData.ColorSchemes()`
    and `SchemeUsed(i)` became `Scheme(i)` and `Scheme(i).IsEmpty`.
  - **Small tables**: `EnemyMarkers.COLORS`, `Icons.PATHS`, the player's hurt cue list, the non-Khalid hair tint
    (a tuple instead of a 3-key dictionary).
  - **Particle nodes**: new `helpers/ParticleNodes.cs` (`SetEmitting`, `SetOneShot`, `OnFinished`) is the one typed
    place that tells a `CPUParticles2D` from a `GPUParticles2D`; `ParticleDirector` and the enemy walk trail use it
    instead of `Set("emitting", …)`. `VfxPalette` sets a gradient texture's gradient through the typed property.
- **Why:** rule `T3` (a data shape is a record, not a dictionary) and `T1` (a closed set is an enum). With string
  keys a typo in a kit (`"atack_range"`) or a wrong value type compiled and failed silently in play; now it does not
  compile. It also removes boxing and `Variant` conversion from code that runs per frame (`AnimMeta` was read every
  frame while an attack was held and built a new array each time — rule `P1`).
- **How:** `T3` now also says where an engine dictionary is still correct: only where the engine hands one over or
  demands one (a `ConfigFile` value, resource metadata, an engine property), converted on the spot. Three such
  places remain: `SaveData`, `AnimMeta`, and the font-variation setting in `UiStyle`. The save file's format on disk
  is unchanged.
- **Behaviour changes:**
  - **Health-bar names are right for every enemy.** The old kits for Kebus, Baghel and Mazab never set a display
    name, so all three showed the default "Kebus". The record makes the name a required field.
  - **A stronger or reskinned effect is now always its own scene.** The emitter-row keys `boost` (rescale particle
    count / speed / size), `node` (fire one named child of a scene) and `set` (override a property by path) are
    gone, with the director code behind them. No row used `boost` or `node`; the only `set`-style use (Cherry Shots'
    homing last shot) is the typed `Configure` hook. `vfx/README.md` is updated.
- **Removed as dead code** (rule `C1`, no caller anywhere): `Locomotion.Make` and `SurgeSpec.Make` (built those
  objects from a dictionary; the actions build them directly), `Loadout.Options` / `SwapChoices` and
  `Player.loadout_choices` (the old swap-reward offer), `LoadoutCategories.All` / `Parse`, the kits' unused `air`
  key, `Player`'s private copy of `SheetStart`, and nine unused `using` aliases. The loadout picker (L1) will need
  an options list again; it should be written typed then, against the screen that uses it.
- **Could affect:** every enemy's stats and attacks (all ten kits were rewritten by hand — the risk is a value
  copied wrong); which enemies spawn and their per-type caps; Lira per kill; all sound cues, their mix and pitch;
  every particle effect on Khalid and the enemies, and the frame it fires on; combo steps, the special's strike
  frame, looping animations (swing, Nasen's rage); the colour screen, saved schemes and the Dekken vial / UI colours
  that follow them.
- **Tested:**
  - The 10-check headless scene from part 1, run on the finished code — all pass (enemy attack lands, stun sweep,
    Zahluq lunge, blast hold, Come Closer, targeting, kill + Ruh orb, launch orb, Sleeper + Diver, Wara).
  - Saved schemes: read the owner's real `save.cfg` (three used slots, written by the old code) — every pick came
    back; wrote a scheme to slot 5 and read it back in a fresh process; record and settings untouched. The real
    save was backed up first and restored after, byte-identical.
  - `AnimMeta` against the raw resource values for Khalid, Breski and Nasen (hit frames, sheet start, loop from /
    to, a missing animation, a null resource).
  - Kit values compared line by line against the previous file.
  - Clean headless boot of the colour screen (which recolours a sample effect with the saved scheme) and the arena.
  - Build: 0 errors, 196 warnings. The "410" quoted in part 1 was that same list counted twice — the compiler prints
    each warning in the build and again in the summary. The true count before this work was about 205; `standards.md`
    is corrected.
  - **Not tested:** how any effect or sound looks and sounds in real play (nothing draws or plays headless) — a
    wrong particle position or a missing cue would show there first; the colour screen's buttons by hand.
- **Left as is:** `VfxPalette.RecolorNode` still reads and sets `texture` by property name (any node type may carry
  a gradient texture) — the one by-name engine access left, listed under Known debt.

---

## 2026-10-09 — `new-shit` — cleanup, part 1 (typed calls, leftovers)

First step of bringing the existing code up to `docs/standards.md` (the plan and its order are in the workspace
handoff). No behaviour is meant to change in either commit.

### Calls between the game's own classes are typed

- **What:** every `HasMethod("…")` / `Call("…")` / `Get("…")` / `Set("…")` between the game's own classes is now a
  normal typed call. Two small interfaces carry the seams that cross class families:
  - `IStrikeWielder` (`ApplyLunge`, `SetArmor`, `HoldAnimation`) — what a strike asks of the body that threw it.
    `Player` implements it; `Strike` and `BlastStrike` use it instead of calling the player by method name.
  - `ISidedAttack` (`hostile`, `friendly_fire`, `source`) — what `Enemy.SpawnAttack` sets on the `Strike` or
    `Projectile` it spawns, instead of setting three properties by name.
  The rest are direct: the player's stun sweep and Come Closer's pull call `Enemy` methods; the launch-orb code holds
  `LaunchOrb`, not `Node2D`; the Ruh orb is instantiated as `RuhOrb`; enemies find the player as `Player`; the
  off-screen arrows read `Enemy.enemy_id`; the surge aura recolour checks for `OrbitAura`.
- **Why:** rule `T4`. A by-name call is a string the compiler cannot check — rename the method and it fails silently
  in play. These were left from when half the game was GDScript and one side could not see the other's types.
- **How:** `Player`'s three wielder methods were renamed to PascalCase as they moved behind the new interface. The
  Wara surge had its own copy of the stun loop; it now calls `stun_nearby`, the same sweep Slam Quake uses (`O2`).
- **Could affect:** everything these seams carry — enemy attacks landing on the player, stuns, Zahluq's lunge and
  super-armor, the animation hold during a blast, Come Closer, launch orbs, Ruh orbs, charmed-enemy targeting.
- **Tested:** a headless scene run on this commit and on the previous one, 10 checks, all passing: an enemy's attack
  hurts the player; the stun sweep hits near and misses far; Zahluq lunges; a blast holds the animation; Come Closer
  pulls; a charmed enemy targets an enemy and a normal one targets the player; a hit kills and a Ruh orb launches;
  a dash by a launch orb captures the player; the Sleeper and Diver enemies load, take a hit and die; Wara negates a
  hit and stuns. Build: 410 warnings before and after. **Not tested:** the off-screen arrows (nothing draws
  headless) and the feel of any of it in real play.
- **Left as is:** particle nodes are still driven by property name (`emitting`, `amount`…), because the engine's two
  particle classes share no typed base — now listed under Known debt. Enemy kits still apply by name (the kits step).
- **Found, not changed:** `Player.HoldAnimation` is only reached through the "crimson vortex" dash effect, and
  nothing in the game turns that effect on (`set_dash_effect` has no caller). It looks like a planned box buff's
  hook; the owner should say whether it stays.

### Leftovers removed

- **What:** deletes the snake_case alias block on `Hit` (13 duplicate properties marked "delete once no GDScript
  uses a Hit") and switches its last users (`Enemy`, `SleeperEnemy`, `DiverEnemy`) to the real names; deletes two
  dead `Player` fields (`attack_projectile_bonus`, `special_radius_mult`) that were only ever reset; moves the
  "why this buff can't be built yet" notes out of `BuffCatalog` and the sound-improvement list out of `Sfx` into
  `docs/future-enhancements-and-fixes.md`; relabels the icon table's note as `PLACEHOLDER`.
- **Why:** rules `C1` (no dead code) and `C3` (no TODO stubs in code); `C4` for the label. The GDScript port
  finished long ago, so the aliases had no reader left.
- **Could affect:** enemy damage, stun, charm and damage-over-time read the hit through the renamed properties.
- **Tested:** covered by the same 10 checks. **A mistake caught by them:** the first rename pass emptied
  `SleeperEnemy.cs` and `DiverEnemy.cs` (the script opened each file for writing before reading it). The build still
  passed, because an empty file compiles; the headless run failed loudly when a Sleeper tried to spawn. Both files
  were restored from git and the rename redone. This is the argument for the permanent test suite that comes next.
- **Left as is:** `vfx/shaders/sprite_tint.gdshader` is unused by Khalid but belongs to the code path that tints a
  non-Khalid character; removing it is a decision about whether other characters return.

---

## 2026-10-09 — `new-shit`

### Standards rulebook and this changelog

- **What:** adds `docs/standards.md` — the engineering rules, each with an id (structure, typed data, data vs
  behaviour, object design, cleanliness, performance, Godot, verification), plus "Decisions already made" and
  "Known debt" — and this changelog. The README points at both.
- **Why:** the standards lived only in the coding agent's workspace skill and memory, outside the repo. Written
  down here they are one rulebook the change can be checked against, and the debt that falls short of it is listed
  honestly so it can be paid down on purpose.
- **How:** rules `V5` and `V6` make the process part of the standard — a QA pass against the rulebook before every
  push, and no push without a changelog entry.
- **Could affect:** nothing in the game.
- **Not kept:** a separate QA-reviewer agent and a GitHub workflow to run it on pull requests were drafted and then
  dropped before ever being committed — the coding agent does the QA pass itself instead.

### Dekken's vials wear Khalid's hair colour

- **What:** `vfx/shaders/vial_recolor.gdshader`, applied to the machine's art by `DekkenStall.TintVials` with the
  run's hair colour (`PaletteConfig.HairColor()`). Only the vials change; steel, amber lines and moss are untouched.
- **Why:** the owner picked green hair and the vials stayed red; they are meant to read as the same substance.
- **How:** the vials are the art's only red ramp, so the shader keys on red dominance and repaints those pixels the
  tint scaled by their painted brightness. The key is measured in linear light (the project renders 2D in HDR) —
  a first version keyed in sRGB terms recoloured the amber lines too.
- **Could affect:** any future Dekken art must keep its vials in that red and use that red nowhere else.
- **Tested:** windowed render of three machines (default, green, blue hair) — vials follow, amber lines do not.
  **Not tested:** in a real run with a saved colour scheme.
- **QA pass:** the first version had the shader parameter and node names as bare string literals and did the work
  inline in `_Ready` (rules `T5`, `O5`); both are now named constants and a `TintVials` method.

### Choking-man statue: new art

- **What:** `assets/terrain/stage1/man_choking_statue.png` replaced with the redrawn 128×128 version; its node in
  `stage1_v1.tscn` goes from scale 0.7 to 1.4.
- **Why:** the owner changed the statue's detail. The new canvas is half the old one, so the scale doubles to keep
  the same height on screen (about 179 px).
- **Could affect:** only that prop. At 1.4 the pixels are slightly uneven, as they were at 0.7.
- **Tested:** the arena boots clean. **Not tested:** how it looks in game.

### Notes for later

- `docs/game-loop.md` records the "build-gated places" idea (areas locked behind jumps, dashes, both, or attack
  power) and Dekken upgrades through experience points. No code.

---

## 2026-10-04 — `new-shit`

### `81f1343` Cleanup: remove unused files, dead player fields and stale input actions

- **What:** deletes `scenes/tile_paint.tscn`, `resources/khalid_tint.tres`, `tools/gen_effect_frames.gd` and a stray
  `node_modules/.package-lock.json`; removes `Player.impervious_until_hit` and `damage_taken_mult`; moves the
  `Trigger` enum to `enums/abilities/Trigger.cs`; removes the `prev_character` / `next_character` input actions.
- **Why:** nothing loaded or read any of them. The two input actions sat on Q, E and the bumpers — the buttons the
  vials and stalls now use.
- **Could affect:** nothing in play. `vfx/shaders/sprite_tint.gdshader` is now unused (kept for a future character's
  `<char>_tint.tres`); `Player.attack_projectile_bonus` and `special_radius_mult` are also dead and still present.
- **Tested:** build, reimport and arena boot.

### `dfaef63` Dekken: vending machine art, moved to the high-left platform

- **What:** the placeholder stall becomes the mossy vending machine (`assets/things/dekken.png`); the stand-here
  area and prompt are resized; in `stage1_v1` Dekken moves to the top-left platform and two hidden nodes are shown.
- **Why:** first real art for Dekken, repaletted from about 8,100 colours to 15.
- **Could affect:** Dekken is now the hardest stall to reach — buying a heal takes a climb.
- **Tested:** arena boot. **Not tested:** its look in game.

### `c134505` Dekken: perks are vials you can drink now or keep for later

- **What:** each perk has two buttons — DRINK (at the machine) or KEEP (carry it; Q drinks, Tab switches). Two
  carry slots, never two of a kind; timed perks start their rounds when drunk; a drink that would do nothing stays
  in the pocket; whole-run perks are drink-only. `PerkLedger` holds the carried vials; the HUD shows two slots.
- **Why:** a heal that fires on purchase is a poor buy, and Fast Travel was nearly useless when it triggered on
  payment. Carrying makes both worth buying and sets Dekken apart from Needle Point.
- **Could affect:** survivability — a pocketed heal is a second chance. The cap of two is the brake.
- **Tested:** headless run of every rule (duplicate, full pockets, drink-only, blocked drink, cycle, drink).
  **Not tested:** the wider menu row and the HUD slots on screen.

### `2b1000c` Mystery box: real-time spin, single-version buffs, teddy bear relocation

- **What:** the box no longer pauses the game. E spends 8 figs → names spin 2.5 s → the result is offered 8 s (E
  takes it; leaving it declines, figs spent). Buffs have one version and never repeat; 6 % of spins are a
  special-swap; 1 in 8 is the teddy bear (figs back, the box moves to another spot under a beam). Adds `BoxRules`,
  `BoxLedger`, `BoxOutcome`, `BoxRoll` and `BoxSpots/Easy|Hard` markers; removes `RewardUI`, the dud chance, and the
  `Tier` / `Family` system.
- **Why:** step E4 of the economy plan. The box is the build-variety system; real time fits the no-breaks rule.
- **Could affect:** every box buff now uses its old "Hot" value; `Overcharge` keeps working on any special after
  Bakshen is swapped away (existing behaviour, left as is).
- **Tested:** headless run of 16 spins — 12 distinct buffs, no repeats, 2 special-swaps, 2 relocations, an expired
  offer. **Not tested:** whether the two hard box spots are reachable in play.
