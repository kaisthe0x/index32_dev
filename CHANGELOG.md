# Changelog

One entry per push, newest first. Each entry lists its commits with **what** changed, **why**, what it **could
affect**, and how it was **tested** — written for whoever reads this in six months, the owner included.
History before 2026-10-04 is in `git log` and `docs/game-loop.md`.

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
