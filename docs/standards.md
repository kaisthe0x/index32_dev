# Engineering standards

The rules this codebase is held to. Whoever writes code here — the owner or the coding agent — follows them, and
the coding agent also acts as the project's **QA engineer**: before anything is pushed it reviews the change against
this file and cites any breach by rule id (`T2`, `P1`, …). If a rule is wrong, change it here — don't work around it.

**What the rules protect.** One person maintains this game, with an AI assistant doing much of the typing. That only
stays workable if the code is *boring to navigate*: a thing has one obvious home, a number is defined once, a wrong
value fails at compile time instead of in play, and nothing is left lying around that isn't used. Every rule below
serves one of those four.

---

## S — Structure

- **S1. One type per file**, named after the type. (A small private helper type used by exactly one class may live
  with it.)
- **S2. Each kind of thing has one folder.** Folders are organisation only — the namespace is flat (`MyGame`).

  | Folder | Holds |
  |---|---|
  | `enums/<domain>/` | one enum per file |
  | `records/<domain>/` | data shapes (`record` / data class) and the interfaces that describe a seam |
  | `ids/` | `const string` id holders (`AttackIds`, `EnemyIds`, `BuffIds`, …) |
  | `configs/` | tuning and content — pure data, no behaviour |
  | `scripts/` | behaviour: nodes, run state, UI, combat components |
  | `helpers/` | stateless utilities |
  | `tools/` | build / QA tools, never loaded by the game |
  | `resources/` | **generated** — never hand-edited (regenerate with the tool that owns it) |

- **S3. A new subsystem gets its own class.** Do not add a feature by growing `Player`, `RunManager`, `Enemy` or
  `HUD` — they are already too large (see *Known debt*). The pattern to copy is the stall **ledgers**
  (`ShotLedger`, `PerkLedger`, `BoxLedger`): a plain class that owns one feature's state and rules, created and
  owned by `RunManager`, with a node (`Stall`, a menu) that only drives presentation. Wiring a new class into a big
  one is fine; putting its logic there is not.

## T — Typed data

The point is that a mistake is a compile error, not a bug found in play.

- **T1. A closed set you switch on is an `enum`** (PascalCase members). Never a string, an int code or a bool pair.
- **T2. An open-ended id that is also a table key / animation name is a `const string` in an `ids/` holder.**
  Reference `EnemyIds.Tarri`, never the literal `"tarri"`. (Not an enum: the id *is* the key, so an enum would need a
  string map at every boundary.)
- **T3. A data shape is a `record` (or a small sealed class), not a dictionary.** Named, typed fields; nullable
  means "unset".
- **T4. A seam between systems is an `interface`** (`ITunable` is the model) — not a duck-typed `HasMethod` / `Call`
  by name, and not reflection. Reflection is allowed in tests only.
- **T5. No magic values in logic.** A number, colour, path or action name used by behaviour is a named constant —
  in `configs/` if it is tuning or content, `private const` beside its only user if it is an implementation detail.
- **T6. An enum that needs a table key gets a `.Key()` helper**, defined once beside the enum.

## D — Data and behaviour stay apart

- **D1. `configs/` is data only.** A config class holds constants and tables and a doc comment explaining what each
  number means; at most a trivial lookup (`Get(id)`). The rules that read it live in a different class.
- **D2. A number is defined in exactly one place.** Combat numbers live in the action's hit data and are applied
  through the tuning seam — never baked into a `.tscn`, never repeated in a script.
- **D3. Tuning is findable.** A designer question ("how fast do kamikazes come?") must lead to one constant with a
  comment that states the resulting values (see `configs/Rounds.cs`).
- **D4. Content is added by adding data**, not by adding a branch. A new enemy is a kit + table rows; a new perk is a
  `PerkDef`. If adding content needs a new `if (id == …)` in shared code, the design is missing a seam — fix that.

## O — Object design

- **O1. One responsibility per class.** If a class's summary needs "and", it is probably two classes.
- **O2. Shared behaviour goes in a base class or component, once** (`Stall`, `Enemy`, `Passive`, `Combatant`,
  `StallMenu`). Copy-pasting a block into a second class is a defect; so is a base class with one subclass and no
  second user planned.
- **O3. Smallest visibility that works.** `private` by default; `protected` only for a real subclass hook; `public`
  only for what another class calls.
- **O4. No mutable static state**, except run-wide selections that deliberately survive a scene change
  (`PaletteConfig.picks`, `VfxPalette.picks`) — each documented as such.
- **O5. Methods do one thing and read top to bottom.** Deep nesting or a method too long to see at once means
  extract a named helper. Prefer early returns.
- **O6. Callers decide, callees act.** Pass the value in (`hunt(speedMult)`) instead of having a low-level class
  reach up into run-level config.

## C — Cleanliness

- **C1. No dead code.** No unused field, method, parameter, branch, flag, `using`, file or asset. Deleting a feature
  deletes everything that only existed for it — code, data, docs, art, input actions.
- **C2. No speculative code.** Nothing is built "for later". An extension point exists when its second user does.
- **C3. No temporary hacks.** No TODO stubs, commented-out code, or "for now" workarounds. If the right fix is too
  big for this change, say so and agree to defer it — don't half-do it.
- **C4. Placeholders are labelled.** A stand-in asset or value carries the word `PLACEHOLDER` in a comment at its
  definition, so they can all be found with one search.
- **C5. Debug and playtest aids are labelled and isolated** — marked `DEBUG` or `PLAYTEST`, in one place, removable
  by deleting that place. A playtest switch does not ship in a merged change.
- **C6. Comments say why, not what.** Every class and every non-obvious member has a doc comment giving its purpose
  and the reason for anything surprising. A comment that restates the code is noise; a stale comment is a defect.
- **C7. Match the file you are in** — its naming, comment density and idiom — unless this document says otherwise.

## P — Performance

The game runs up to a few dozen enemies at once, each with a state machine, plus particles. The per-frame path
(`_Process`, `_PhysicsProcess`, and anything they call) is where care is owed.

- **P1. No allocation on the per-frame path** in steady state: no `new` of lists, arrays, dictionaries, shapes,
  query parameters, lambdas or strings. Allocate once and reuse.
- **P2. No tree lookups per frame.** `GetNode`, `FindChildren`, `GetNodesInGroup` and `ResourceLoader` results are
  fetched once (in `_Ready`) and cached.
- **P3. No LINQ and no string building per frame.** Loops and cached values instead.
- **P4. No work that does nothing.** A per-frame method returns early when it has nothing to do; a system that is
  off does not tick.
- **P5. Scale with what's alive, not with the map.** Anything proportional to tiles or all nodes is computed once
  and cached (see `LevelLayout.BuildGround`).
- **P6. Physics queries are deliberate** — one ray or shape where it's needed, never a scan every frame.
- **P7. Loads are up-front.** `GD.Load` of a scene, shader or sound happens once and the result is kept; nothing
  loads from disk mid-fight.
- **P8. Event-driven beats polling.** React to a signal or a call instead of checking every frame.

## G — Godot

- **G1. Scenes are minimal.** A `.tscn` holds what must be seen or dragged in the editor (art, shapes, markers).
  Behaviour, numbers, collision layers and draw order are set in code from the shared tables (`Combat`, `WorldZ`,
  `UiLayers`) so they cannot drift.
- **G2. Draw order and collision layers are never literals.** Use `WorldZ.*` / `UiLayers.*` / `Combat.Layer`.
- **G3. Scene and `project.godot` edits happen with the editor closed** — the open editor overwrites them.
- **G4. Signals are connected in code and disconnected or freed with their owner.** No lambda captures a node that
  can outlive it.
- **G5. Nodes are freed by their owner** (`QueueFree`); nothing is left parented to a container that never clears.
- **G6. The public surface of older classes is `snake_case`** (a leftover of the GDScript port — see *Known debt*).
  Inside such a class, match it. A **new** class uses C# conventions (PascalCase members).

## V — Verification and records

- **V1. It builds:** `dotnet build mygamedev.csproj`, zero errors, and no new warnings in files the change touches.
- **V2. Behaviour is checked in the engine**, not assumed: a headless run of a real scene for logic, a windowed
  render when the question is what something looks like.
- **V3. Say what was not verified.** A claim of "works" names how it was checked; anything unchecked is stated.
- **V4. Docs move with the code**, in the same change: `README.md` (how it works), `docs/game-loop.md` (design),
  `scripts/run/README.md` (the run), and `CHANGELOG.md` (what and why).
- **V5. Nothing is pushed without a changelog entry** in `CHANGELOG.md`: per commit — what it does, why and how it
  was done that way, what it could affect, how it was tested. The entry is written before the commit, and is part
  of it.
- **V6. A QA pass comes before every push.** The whole pending change is read back against this document —
  every changed file in full, every caller and reader of what changed, leftovers the change made unused — and the
  build and engine checks are run. Breaches are fixed first; anything knowingly left is named in the changelog.

---

## Decisions already made — do not re-argue

A QA pass does not raise these as findings. They were chosen on purpose.

- **C#, flat namespace `MyGame`.** The project and assembly keep the names `MyGame` / `mygamedev` deliberately.
- **`const string` ids, not enums, for content ids** (T2).
- **Placeholder art, sound and text are acceptable while labelled** (C4). The owner draws the art; code does not wait.
- **The `DEBUG` keys** (buff grant / clear, damage, heal, respawn) stay during development.
- **Levels are hand-painted scenes**; enemy spawn spots, box spots and stalls are markers in the layout, not code.
- **`HUD`, `Sfx` and `Music` are autoloads.**
- **Design choices** — endless rounds with no breaks, the three stalls, the economy, the numbers — belong to the
  owner and `docs/game-loop.md`. A QA pass may question whether code *implements* the design, and may flag a balance
  or feel risk as a note; it does not overrule the design.
- **Warden / Kroj code is kept** though unused today — Warden rounds are planned.
- **`playground/`** (both repos) is parked work, outside these standards.

## Known debt — do not add to it

These predate the standards. A QA pass does not report them as new findings, but **does** report a change that makes
any of them worse, and says so when a change is a cheap chance to reduce one.

- **Oversized classes:** `Player.cs` (~2,270 lines), `Enemy.cs` (~1,240), `RunManager.cs` (~1,090), `HUD.cs` (~620).
  Direction: carve out subsystems as their own classes (S3) when they are next touched.
- **Dictionary-typed tables:** enemy kits (`EnemyKits`), emitter and sound tables use Godot dictionaries with string
  keys, applied by name (`enemy.Set(key, value)`). Direction: typed records (T3). New tables must be typed.
- **`snake_case` public members** on `Player`, `Enemy`, `Hitbox`, `Strike`, `Projectile` and `Sfx` — from the
  GDScript port. Direction: rename when a class is otherwise being reworked, all at once.
- **By-name access to engine particle nodes:** `ParticleDirector`, `VfxPalette` and the enemy walk trail set
  `emitting` / `amount` / `lifetime` / `texture` by property name, because `CPUParticles2D` and `GPUParticles2D`
  share those names but no typed base. Direction: one small typed wrapper, used by all three. (Calls between the
  game's *own* classes are all typed since 2026-10-09 — keep it that way: rule `T4`.)
- **Compiler warnings:** the project has nullable reference checking on, and the build prints about 410 warnings
  (mostly `CS8618` — a field not set in the constructor; worst in `Player`, `HUD`, `RunManager`, `ParticleDirector`).
  With that many, a new one goes unseen. Direction: bring it to zero, then make warnings fail the build. Until then
  rule `V1` is checked by comparing the count before and after a change.
- **Per-call allocations in spawn-time code:** `RunManager.SpotIsClear` and the point / ray queries build their
  shape and parameters on each call. Not per-frame today; must not move onto the per-frame path as-is.
- **No automated tests.** Behaviour is checked by throwaway headless scenes that are deleted afterwards. Direction:
  keep the valuable ones as a permanent suite.
- **Stale docs:** `docs/rewards-design.md`, `docs/game-design.md` and parts of `docs/csharp-migration.md` describe
  retired designs; they are kept as history and marked as such.
