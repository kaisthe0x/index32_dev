# configs — tuning data, one file per domain

Pure **data** pulled out of the scripts that use it: constant lookup tables,
rosters, collision-layer bits, feel/balance numbers. The goal is that when you
want to retune the game you open `configs/`, not hunt through behavior code.

Each file is a `class_name X` holding only `const`s (or pure static accessors),
referenced globally as `X.MEMBER` (e.g. `Combat.L_WORLD`, `ActionsKhalid.ATTACKS`).
Nothing here runs — no `_process`, no state — so it's safe to read from anywhere and
cheap to change. **Config data and the code that reads it stay in separate files**:
the catalog tables (`actions_<char>.gd`, `sfx_*.gd`) are pure data; the accessors
(`actions.gd`, the `Sfx` service) are the readers.

> The roguelite **run** data (the enemy roster/kits) is *not* here — it lives with the run
> logic in [`scripts/run/`](../scripts/run/README.md) (`EnemyKits`). This folder is the
> player/combat tuning tables (+ `Rounds`, `BuffCatalog`, `Terrain`).

## What's here

| File | Class | Holds |
|---|---|---|
| `combat.gd` | `Combat` | Collision-layer bitmask table (`L_*`, mirrors project.godot) + shared hit-reaction feel (`KNOCKBACK_POP`, `MIN_STAGGER`, `STRIKE_ACTIVE`, `HIT_FLASH`, `HIT_FLASH_TIME`) |
| `action.gd` | `Action` | One thing a character **performs** (attack/special): typed identity (`id`/`name`/`icon`), `category` + cadence `style` enums, `tier`, `animation`, `cooldown`, and — if it deals damage — its `hit` (a `StrikeSpec`). The typed successor to the old `Move`; built from a catalog by `Actions`. Presentation is NOT here — it hangs off `animation` (particles + sounds). |
| `strike_spec.gd` | `StrikeSpec` | The **"Strike" component** of an Action: delivery `type` enum (MELEE/PROJECTILE/AOE/BLAST/…, descriptive) + `segments` (per-combo-hit tuning dicts — damage/knockback/stun/extents + effect tags). The SINGLE source of an attack's hit numbers; `null` on an Action = no hitbox. |
| `locomotion.gd` | `Locomotion` | The **"Locomotion" component** of a movement Action (run/jump/dash/slam): every movement/physics knob (run speed/accel/friction, jump velocity/air-jumps/gravity/land, dash speed/time/blink, slam speed/timing/drop-scaling) as one typed spec. Holds the shared BASELINE; each character's `MOVEMENTS` catalog overlays only its deviations. This is where movement lives now — **nothing** movement-related is a Player `@export` anymore. |
| `actions_khalid.gd` | `ActionsKhalid` | Khalid's action catalog — PURE DATA: `ATTACKS` + `SPECIALS` + `MOVEMENTS` (run/jump/dash/slam, each a `move`/Locomotion row) tables (each row an `Action.make` dict, icon embedded) + `DEFAULT_ATTACK`/`DEFAULT_SPECIAL`/`DEFAULT_MOVEMENTS`. **Edit the defaults here to change what Khalid starts with.** One `actions_<char>.gd` per character. |
| `actions.gd` | `Actions` | The accessor over the per-character catalogs (`get_action(character, kind, id)`, `ids(...)`) — the reader half, kept separate from the data. `kind` is one of `attacks`/`specials`/`run`/`jump`/`dash`/`slam`; returns `null` for an empty pool. |
| `loadout.gd` | `Loadout` | The swappable **loadout + tier** layer: per category (attack/special/run/jump/dash/slam) the options a character has, each with a **tier** (`typical`/`elite`/`broken`). ALL six categories now come from the `Actions` catalog (each action's `tier` + `name` + `icon`) — a movement variant is added exactly like an attack variant, a new catalog row. |
| `character_config.gd` | `CharacterConfig` | The player roster `IDS` + the per-id resource path templates (`FRAMES_PATH`, `PORTRAIT_PATH`, `ABILITY_PATH`). Identity only — movement stats moved to the `Locomotion` baseline + each character's `MOVEMENTS` catalog. Ships **Khalid only** (others parked in `playground/`). |
| `terrain.gd` | `Terrain` | The level **art skin**: the 32px tileset sheet + which atlas cells are surface vs fill tiles, ground-plant + tree props, and the background image. `RunManager` stamps these as sprites over the colliders (`_paint_surface`) — see [`scripts/run/`](../scripts/run/README.md). Drop art in `assets/terrain/`. |

## What deliberately stays OUT of here

**`@export` fields on nodes** — `Player`'s health/ruh/juice/lahm + attack pacing, `Enemy`'s
per-enemy stats, `FloatingHealthBar`'s styling. Godot
surfaces those in the **inspector per node**, which is how you tune them; relocating
them into a const file would lose that. The rule of thumb: **a constant table or
roster → here; an `@export` you tune per-node in the inspector → stays on the node.**
(Movement is the exception that proved worth spreading out: it left the inspector for the typed
`Locomotion` config so run/jump/dash/slam become swappable catalog Actions like everything else.)

## Adding / changing config

- **Retune an existing value** — edit the const here; every reader picks it up (it's
  referenced by class, not copied).
- **Add a new table** — new `configs/<name>.gd` with `class_name <Name> extends
  RefCounted` and your `const`s; reference it as `<Name>.MEMBER`. Pick a class name
  that doesn't collide with an existing one (Player, Enemy, Combat, Actions, …).
- **`@export_enum` caveat** — the character list is duplicated as a literal in
  `Player.character`'s `@export_enum(...)` because that hint needs literal strings
  (it can't reference `CharacterConfig.IDS`). Keep the two in sync by hand.
