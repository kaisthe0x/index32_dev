# Changelog

One entry per push, newest first. Each entry lists its commits with **what** changed, **why**, what it **could
affect**, and how it was **tested** — written for whoever reads this in six months, the owner included.
History before 2026-10-04 is in `git log` and `docs/game-loop.md`.

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
