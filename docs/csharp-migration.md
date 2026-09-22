# GDScript → C# migration

Tracking doc for porting mygamedev from GDScript to C#. **The game systems (everything shipped) become C#;
throwaway test/QA harnesses stay GDScript.** Motivation: a typed attack hierarchy (`Strike` as a base, with
`Melee`/`Blast`/`Aoe`/`TimedAoe` as real types), a typed reward/item system, and compile-time bug-catching
as the project scales.

Branch: `feature/csharp-migration`.

## Toolchain (verified)

- **`godot-mono` 4.7.2.stable.mono** (the .NET build; the plain GDScript `godot` is gone from PATH). Patch-
  newer than the old 4.7.1 build — fine within 4.7.x; the game boots clean under it.
- **.NET SDK + runtime 10** only. So the C# project targets **`net10.0`** (matching the sole installed
  runtime — avoids net8 roll-forward issues).
- Build: `dotnet build mygamedev.csproj` (fast) or `godot-mono --headless --build-solutions` (also registers
  `[GlobalClass]` types). NuGet is reachable, so `Godot.NET.Sdk/4.7.2` restores.
- **Run/verify** with `godot-mono` (NOT `godot`): `godot-mono --headless res://scenes/<scene>.tscn`,
  `godot-mono --headless --quit-after 150` for a boot smoke test. Same real-scene discipline as before.

## Scaffolding (done)

- `mygamedev.csproj` — `Godot.NET.Sdk/4.7.2`, `net10.0`, `Nullable=enable`, `ImplicitUsings=enable`,
  `RootNamespace=MyGame`, `EnableDynamicLoading=true`.
- `mygamedev.sln` (classic format; `dotnet new sln --format sln`).
- `project.godot` → `[dotnet] project/assembly_name="mygamedev"`.

## Interop findings — THE constraint that governs order (verified empirically)

| Boundary crossing | Works? |
|---|---|
| GDScript instantiates a C# `[GlobalClass]` (`X.new()`) and calls its **instance methods** | ✅ |
| GDScript **reads/writes a C# instance property or field** by dot-access | ✅ (see naming) |
| C# **signals** (`[Signal]`) → GDScript `connect` | ✅ |
| GDScript **`var x := csharpObj.Member`** (type inference on a C# member read) | ❌ **parse error** — needs an explicit type: `var x: float = csharpObj.Member` |
| GDScript reads a C# **`static` / `const`** member | ❌ **parse error** |
| GDScript **`extends` a C# class** (and the reverse, C# extends GDScript) | ❌ **not supported** — "Could not resolve script" |

**THE structural constraint (verified):** an **`extends` chain must be a single language.** GDScript can *use* a
C# class (`new`, `is`, typed var, call methods, connect signals) but cannot **inherit** from one — and C#
can't inherit from GDScript. **Consequence:** a base class with GDScript subclasses can only move to C# when
its whole subtree moves with it. Here that ties **`Combatant` + `Player` + `Enemy` + `Nasen` + `Ein`** into
**one atomic phase** — they can't be ported one at a time. (Cross-language calls between *unrelated* classes
are fine via instances/signals/dynamic `Call`; only `extends` is language-locked.)

More boundary facts (all verified):
- **Names are exact and case-sensitive.** GDScript addresses a C# member by its literal C# name — `hit.Amount`,
  never `hit.amount`. `[Export]` is **not** required for GDScript access (plain public props/fields work).
- **`:=` inference fails on ANY C# member** (property, field, or computed — not an alias quirk). So a GDScript
  line reading a ported member must annotate the type. There are few of these; the parse error names each.
- **snake_case alias pattern for data objects that cross into GDScript** (e.g. `Hit`): give the C# class
  idiomatic `PascalCase` properties **plus** transition-only `snake_case` alias properties that forward
  (`public float amount { get => Amount; set => Amount = value; }`). Then the still-GDScript consumers keep
  their `hit.amount` reads/writes UNCHANGED — only their rare `:=` reads need an explicit type. Delete the
  alias block once no `.gd` touches that object. The Godot source generator accepts `Amount`/`amount` together.
- **GDScript does NOT honour C# default parameters.** A C# `void activate(float d = 0)` makes GDScript demand
  the arg (`activate()` → "Too few arguments"). Expose a **parameterless** method for the common call + a
  separately-named method for the variant (`ActivateTimed(float)`). No overloading by arity for GDScript.
- **C# lambda on a Godot signal can be GC'd before it fires** — a capturing `() => …` connected to
  `SceneTreeTimer.Timeout` (or any signal) may be collected, so the callback silently never runs. Use a
  **method group** (`timer.Timeout += ArmHitbox;`) — it keeps `this` alive. (This bit the TimedAoe telegraph
  and would have bitten DoT-tick / multi-hit; all converted to method groups.)
- **C#→GDScript callbacks are dynamic `Call`.** When ported C# must call a still-GDScript method on a body it
  holds as `Node` (lunge/armor/`hold_animation`), use `node.HasMethod("x")` + `node.Call("x", args…)`. It's
  the stringly-typed direction; unavoidable until the body is C# too.
- **Cpu/GpuParticles2D share property *names* but not a C# base** — no common type exposes `Emitting`/
  `Lifetime`/`Restart`; use a small typed `switch` per kind. `Lifetime` is `double`; `LifetimeRandomness` is
  CPU-only. Also `Timer` is ambiguous under ImplicitUsings — qualify `Godot.Timer`.
- **A C# property `foo` reserves the accessor name `set_foo`/`get_foo`** — so a C# **method** named `set_foo`
  collides with a `foo` property (`CS0082`). The GDScript `player.gd` had both a `character` property AND a
  `set_character()` method; in C# only one can exist. Drop the method and route callers through the property
  (`_player.character = id` runs the same setter) — GDScript writing a C# property triggers its setter.
- **`GodotObject.Call("method", …)` on a C# target needs the EXACT arity** — it does NOT fill the C# method's
  default parameters. A C# `play(string, float = 0, float = 1)` called as `_sfx.Call("play", "jump")` fails with
  *"Nonexistent function 'play'"* (no 1-arg overload). Once a bridged autoload/helper is itself C#, stop calling
  it by string and call it **typed** (`GetNode<Sfx>("/root/Sfx").play("jump")` — the C# compiler fills the
  defaults). This is also the phase-7 cleanup, so do it when the target ports. (GDScript direct calls to a C#
  method hit the same "can't fill C# defaults" wall — pass every arg explicitly there.)
- **Exposing a C# `const` to GDScript:** GDScript can't read a C# static/const, but a GDScript consumer that
  needs the value (HUD reading `RUH_PER_BLOCK` to size the Ruh meter) can read a C# **instance property**
  mirroring it (`public float RUH_PER_BLOCK => 100f;`). Class-qualified reads (`Player.RUH_PER_BLOCK`) still
  fail and must switch to the instance (`player.RUH_PER_BLOCK`).
- **STALE `GD.Load<GDScript>("…gd")` BRIDGES in C# after the target migrated** — a latent-until-triggered crash:
  `RewardUI.cs` still did `GD.Load<GDScript>("res://configs/loadout.gd").Call("tier_color", …)` after `loadout.gd`
  became `Loadout.cs`, so opening a reward door threw `Error loading resource: loadout.gd` → `NullReferenceException`
  (only on the final levels, when a loadout-swap card with a `tier` was offered — that's the only card that calls
  `TierColor`). Fix = call the C# static directly (`Loadout.TierColor(tier)`). **Sweep after every cluster:**
  `grep -rhoE '"res://[^"]+\.gd"' **/*.cs` and existence-check each — a migrated `.gd` that any C# still loads is a
  runtime bomb the compiler can't catch. (After this fix the only live `.gd` bridges are the colour cluster.)
- **Setting `Area2D.Monitorable` inside a hit signal** — `Player.Die()` runs from the hurtbox's `take_hit` → OnHurt
  chain, i.e. during physics-query flush, so `_hurtbox.Monitorable = false` was blocked ("Function blocked during
  in/out signal"). Fix = `SetDeferred(Area2D.PropertyName.Monitorable, false)`. Applies to any monitorable/monitoring
  toggle reachable from a body/area-entered handler.
- **GDScript `--script` TOOLS that referenced a now-C# config** (`tools/verify_frames.gd`, `tools/capture_shots.gd`
  read `CharacterConfig.IDS`/`.FRAMES_PATH`): these stay GDScript (build/QA tools), so they hit the same wall —
  GDScript can't read a C# static class, and there's no instance to bridge through in a `SceneTree`-root tool.
  Fix = **inline a local mirror `const`** in the tool (`const CHARACTER_IDS := ["khalid"]`) with a "mirror of
  <Class>.cs" comment. Caught late (only surfaced when the editor reloaded and re-parsed the tool scripts) — a
  reminder to re-parse the WHOLE project after migrating any `class_name`, not just the game scenes.
- **NODE ported from a `.gd` that scenes reference:** keep the **exact public surface names** the scenes/`.gd`
  callers use (snake_case fields/methods/signals — forced anyway because `.tscn` `[Export]` values serialize
  by name and GDScript calls by exact name). Internals stay idiomatic. **Reuse the uid**: put the old
  `<name>.gd.uid` value in a new `<Name>.cs.uid`, AND rewrite each scene's `ext_resource` `path=` to the
  `.cs` (keep the uid) — both resolve to the C# script, no other scene edits. snake_case C# `[Export]`
  properties load the existing `.tscn`-authored values correctly (verified: `damage = 18.0` → `18.0`).
  Final PascalCase polish is deferred to the cleanup phase (all callers C# by then → safe rename).

**Consequence:** a shared *static* config (e.g. `Combat`'s layer bits, the `const`-table configs) **cannot be
flipped to C# while any GDScript still reads it.** Two tactics:

1. **Duplication bridge** (for small, stable static data like collision layers): keep the GDScript copy AND add
   a C# copy; both live until the last GDScript consumer is ported, then delete the `.gd`. Drift risk is low
   for constants.
2. **Flip cohesive clusters together** so a static config and all its consumers become C# in one phase.

Nodes/Resources are the interop-friendly unit: ported C# nodes talk to un-ported GDScript via **instances +
signals**, never via statics.

## Conventions

- Namespace `MyGame`; `PascalCase` members; nullable reference types on.
- C# files mirror their GDScript origin folder during transition (e.g. `configs/Combat.cs` beside
  `configs/combat.gd`) so paired files stay together; the `.gd` is deleted when nothing references it.
- A C# type GDScript must use is `[GlobalClass]` + extends a Godot type. Pure C#-only helpers are plain
  classes (e.g. `Combat` is a `static class`, not a Godot type).

## Verification discipline

Port a unit → `dotnet build` (0 warnings, nullable clean) → run the relevant real scene under `godot-mono`
and confirm **behavioural parity** with the pre-port build → then move on. Keep the GDScript scene-test
harnesses (GDScript, per the carve-out).

## Known minor issues (from playtest, deferred — not blocking)

- **Enemy idle looks slightly buggy (esp. Mazab).** The idle "settle then ping-pong" bounce (GDScript, not yet
  ported) reads a touch off on Mazab's frames. Pre-existing tuning, not a migration regression. Revisit its
  `idle_loop_from/to` when the enemy tree lands.
- **Baghel's attack trail + Kebus's projectile render WHITE** (playtest). Almost certainly a `Projectile.cs`
  regression — those are the two enemy projectile scenes re-scripted in Phase 2. Suspect the particle-colour
  path: `SampleVisualColor()` (baghel's ground trail — likely returning the white fallback because the source
  emitter's colour is on `color`, not `ColorRamp`, or the C# read differs from GDScript), and/or the main
  visual's colour on kebus. Contained fix; deferred per user. Check the baghel/kebus `.tscn` emitter colour
  source vs the C# read.

## Phases

- [x] **0 · Toolchain** — godot-mono 4.7.2 + dotnet 10 verified; branch cut.
- [x] **1 · Scaffold + prove pipeline** — csproj/sln/`[dotnet]`; interop probe confirmed instance-calls +
  signals cross and statics don't. Probe removed.
- [x] **2 · Combat core + typed attack hierarchy** (every standalone combat component; `Combatant` deferred to
  Phase 4 with the body tree) — done: `Combat.cs`, `Hit.cs`, `Hitbox.cs`+`Hurtbox.cs`,
  and **the typed Strike hierarchy** (the marquee goal): `Strike` (base) + `MeleeStrike` / `AoeStrike` /
  `BlastStrike` / `TimedAoeStrike`. **Design:** the base owns all shared machinery; **`BlastStrike` carries the
  real channel behaviour** (`emit_duration` + hold-the-caster + interrupt) and Enemy/Player now detect a
  channel with `is BlastStrike`; `TimedAoeStrike` adds a telegraph delay (unused by scenes yet, unit-tested);
  Melee/Aoe are the box shapes (honest — this game's attack differences are shape/params + the channel). 25
  attack scenes re-scripted to their type (18 Melee / 5 Aoe / 2 Blast). Verified: each scene instantiates as
  the right type, `apply_tuning` works through the base, Tarri's channel is detected + cancels, Breski's combo
  lands 4 hits, TimedAoe box off-during/on-after telegraph, clean boot.
  ⚠ **Regression caught in playtest + fixed:** moving `emit_duration` off the base `Strike` onto `BlastStrike`
  broke `ParticleDirector._fire_burst` (`particle_director.gd:433`), which read `node.emit_duration if node
  is Strike` — a `MeleeStrike` IS a `Strike` but has no `emit_duration`, so every player melee (bakshen)
  crashed. Now guards on `is BlastStrike`. **Lesson:** when narrowing a base member to a subclass, grep
  EVERY `is <Base>` / member-access site — not just the obvious owners (I'd fixed enemy/player but missed the
  director). Headless scene tests don't exercise input-driven player attacks; playtesting found it.
  Also done: **`Projectile.cs`** (5 scenes re-scripted; verified — kebus straight shot lands, the frisbee homes
  then ricochets across 2 enemies) and **`LobProjectile.cs`** (code-built, no scenes; verified — mazab throws,
  arcs, dwells, explodes, AoE damages). NEXT: `Combatant` (⚠ base of Player/Enemy — porting it makes GDScript
  `extends` a C# class; verify that interop first) and `MagnetField`.
  Two more interop notes: **C#→GDScript autoload call** is `GetNodeOrNull("/root/Sfx")?.Call("play_at", …)`
  (the autoloads are still GDScript). And the payoff shows: a ported C# class calls another ported class
  **typed** — LobProjectile invokes `AoeStrike.apply_tuning(...)` directly instead of the old
  `has_method`/dynamic dance.
  Also done: **`MagnetField.cs`** (1 scene rewired; verified — magnetizes a GDScript enemy via dynamic
  `Call`). **Every standalone combat component is now C#.** `Combatant` is NOT standalone (base of the body
  tree) → it moves in Phase 4.

- [ ] **3 · Configs → typed data** — the `const`-table configs become typed C# as their consumers port. Many
  are consumed by the body tree, so they largely land with Phase 4.
- [~] **4 · The BODY TREE** — split into two atomic sub-phases, decoupled so the game stays runnable between
  them (Player keeps the GDScript `Combatant`; the enemy tree gets a **C# `Combatant`**).
  **Key enabler (verified):** a **non-`[GlobalClass]` C# class can share a name with a GDScript global class**
  — only C# subclasses extend the C# `Combatant`, so it needs no global registration and doesn't clash with
  the GDScript `class_name Combatant`. No rename needed.
  - [x] **Combatant.cs** — ported (idiomatic PascalCase; called only from the C# enemy tree). Verified:
    methods work, `SpawnVictimVfx(recolor:true)` bridges to the GDScript `VfxPalette.recolor_tree` via
    `GD.Load<GDScript>(…).Call(…)`, and it coexists with the GDScript Combatant (clean boot).
  - [x] **4a · Enemy tree — DONE & verified.** `Enemy.cs` (1225 lines) + **`SleeperEnemy.cs`** (was Nasen) +
    **`DiverEnemy.cs`** (was Ein), all C# extending C# `Combatant`. **Reframed by ARCHETYPE** (user's call):
    Nasen/Ein are now behaviour-type classes, and "Nasen"/"Ein" became **kits** (`EnemyKits.NASEN`/`.EIN`
    point at generic `sleeper_enemy.tscn`/`diver_enemy.tscn` + carry the identity/tuning) — a new sleeper or
    diver is now just a kit, no new script. `enemy.tscn` rewired to `Enemy.cs` (uid reuse); the three `.gd`
    files + `nasen.tscn`/`ein.tscn` deleted. Compiled clean on the FIRST pass; clean boot; parity verified —
    base Enemy (Breski melee combo 4 / Kebus projectile 5 / Mazab lob 2), SleeperEnemy (Nasen rage 13),
    DiverEnemy (Ein dive+self-destruct). The bridges held: UI helpers via dynamic Call/Get, config readers via
    `GD.Load<GDScript>().Call`, Sfx via `/root/Sfx`, Shapes/Nodes inlined. GDScript consumers (RunManager
    `var e: Enemy`, player `hit.source is Enemy`) work unchanged against the C# `Enemy`.
    (Bridge strategy used, as planned: combat deps typed C#; UI helpers held as `Node2D` + dynamic Call/Get;
    config readers `Emitters`/`SfxEnemies`/`AnimMeta` bridged via `GD.Load<GDScript>().Call`; Sfx via
    `/root/Sfx`; Shapes/Nodes inlined. This transitional dynamic-call cruft is removed when Player/HUD/configs
    port in 4b/5/6.) ⚠ Input-driven player-attack paths still need a **playtest** (headless can't drive input).
  - [x] **4b · Player — DONE & compile/boot-verified.** `Player.cs` (~1450 lines) extends the C# `Combatant`;
    the GDScript `player.gd` + `combatant.gd` deleted → the body tree is now ALL C#. `player.tscn` rewired to
    `Player.cs` (uid reuse `c7150pixjqvhw`). The whole passive/buff stack ported WITH it (forced atomic — see
    the extends-chain rule): `Passive.cs` / `Buff.cs` / `CharacterAbility.cs` + `Leech.cs` / `ParryMend.cs` /
    `ReaperEdge.cs`, all `[GlobalClass]` so the still-GDScript `Rewards` service can `Leech.new()` them.
    Bridges held: config objects (Action/Locomotion/SurgeSpec) carried as `GodotObject` via `.Get/.Call`;
    Actions/Loadout/AnimMeta/PaletteConfig/VfxPalette/FloatingText via `GD.Load<GDScript>().Call`; Sfx via
    `/root/Sfx`; ParticleDirector/StatusOverlay/FloatingHealthBar instantiated by GDScript-`.New()` and driven
    by dynamic Call/Get. GDScript touch-ups for the 3 static reads (`Player.State.SPAWN` → `is_spawning()`,
    `Player.RUH_PER_BLOCK` → an instance accessor) + the `:=` fix in `hud.gd`; `Rewards._make_passive` now
    `.new()`s the C# passives by name. **Verified:** clean boot of `level.tscn` (200 frames, no errors) + a
    throwaway QA scene exercising the reward path end-to-end (stat buff onto a C# field; a C# `Leech` granted
    by GDScript with its `OnHitDealt` hook firing + healing; `ReaperEdge` buff granted; `Build.of` querying the
    C# player; the level-duration tick) → **PASS**. ⚠ Input-driven movement/combat/surge FEEL still needs a
    **playtest** (headless can't drive input).
  - **Reward/buff FOUNDATION laid (per docs/rewards-design.md, the user's direction).** `Buff` now carries the
    doc's model: `Tier` (Common→Epic rarity + badge colour, `RewardTypes.cs`), `DurationLevels` (null =
    permanent / N = expires after N level advances, ticked by `Player.advance_level()`), the existing
    `AppliesTo` scope + `Family` replace-in-place, and a `Trigger` enum (the doc's growing hook vocabulary).
    Passive hooks extended with the doc's wired movement/attack moments (`OnDash`/`OnGroundJump`/`OnAirJump`/
    `OnSlamTrigger`/`OnSlamLand`); the harder triggers (OnMiss/OnPerfectDodge/level-timer) are reserved in
    `Trigger` until the player learns to emit them. This is the scalable base future buffs drop into as data.
- [x] **C#→GDScript static bridge** pattern (recorded + used widely in Phase 5): call a GDScript `class_name`
  static from C# via `GD.Load<GDScript>("res://…​.gd").Call("method", args…)`; read a GDScript `const` via
  `.GetScriptConstantMap()["NAME"]` (Terrain cells, SfxCharacters.CUES). A GDScript **static var** can't be
  written from C#, so add a tiny static **setter** func and bridge-call it (`SaveData.set_current_cleared`).
- [x] **5 · Run system — DONE & verified.** The forced static-call cluster ported to C#:
  - **5a · Reward system** — `Reward.cs` / `RewardsCatalog.cs` / `Build.cs` / `Rewards.cs` (was
    `configs/reward.gd`+`rewards_catalog.gd`, `scripts/run/build.gd`+`rewards.gd`). `Rewards` is `[GlobalClass]`
    with **instance** methods so the (then-still-GDScript) RunManager called it via `Rewards.new()`; its
    `_make_passive` is now direct C# (`new Leech()`). Verified by a throwaway QA (offer_for roulette + build
    gating + special-swap cards + apply stat/passive/per-move effects + records) → **PASS**.
  - **5b · RunManager** — `RunManager.cs` (~620-line port; `level.tscn` root rewired, uid `pl8q0jlcf73e`
    reused; `run_manager.gd` deleted). Talks to the C# body tree + `Rewards` directly; BRIDGES the still-GDScript
    layer it depends on: **Levels** (get_level/count via Call), **Terrain** (funcs via Call + consts via the
    constant map), **Music/Sfx** (`/root/*`), **SaveData** (report_run + the new `set_current_cleared`),
    **FloatingText/VfxPalette** (static Call), and **ExitGate/RewardUI/AttackSelect/LaunchOrb** (GDScript `.New()`
    + signal connect). Enemy `died`/`damaged` bound via per-enemy capturing lambdas on `Connect` (safe: the enemy
    holds the connection). Verified: clean boot (builds level 0 — terrain/platforms/orbs/gate/enemy-kits/camera/
    glow/floor/attack-picker) + a programmatic flow QA driving clear → gate-touch → reward → **level advance** →
    **PASS**. ⚠ The death cinematic + camera feel + input-driven clear are headless-verified only by construction;
    confirm by **playtest**.
  - Still GDScript (bridged; move in phase 6/7): `Levels`/`EnemyKits` (data), `ExitGate`/`RewardUI`/
    `AttackSelect` (UI nodes), Terrain + the autoloads/helpers.
- [~] **6 · Autoloads + UI — IN PROGRESS.** Done so far: the **combat presentation helpers** cluster (the
  cleanly-migratable leaves) — `StatusOverlay.cs`, `FloatingText.cs`, `FloatingHealthBar.cs` (were
  `scripts/combat/status_overlay.gd` / `floating_text.gd` / `health_bar.gd`). All C#-only consumers now, so
  Player/Enemy/RunManager hold them TYPED (removed the `NewGd`/`Call/Get`/`_floatingText.Call("emit")` bridges);
  `FloatingText` reads its GDScript type presets via the constant map. The one blocker — `hud.gd` called the
  `FloatingHealthBar.color_for_ratio` **static** — was resolved by inlining the colour bands in `hud.gd` (GDScript
  can't call a C# static; keep the two copies in sync). Verified: build clean + clean boot (bars/overlays built at
  construction) + a QA that `take_damage` spawns a `FloatingText`. Then the **run UI** cluster — `ExitGate.cs`,
  `RewardUI.cs`, `AttackSelect.cs` (were `scripts/run/exit_gate.gd`/`reward_ui.gd`/`attack_select.gd`). C#-only
  consumers (RunManager + `RewardUI` uses `AttackSelect.CardBody`), so RunManager holds them TYPED (removed the
  `.New()`/`Call("open"/"setup"/"reflect")`/`Connect` bridges; signals are now `gate.touched += …` / `ui.chosen += …`).
  They bridge Icons/Loadout/Actions (config statics) + read the GDScript Action via `.Get`. Verified: build clean +
  clean boot + the phase-5 flow QA re-run finding the UI by C# type (`is ExitGate`/`is RewardUI`/`is AttackSelect`),
  driving clear → touch → reward → advance → **PASS**. ⚠ The button/keyboard INPUT + visual layout need a **playtest**.
  Then **overhead_status/status_icons** (`StatusIcons.cs`/`OverheadStatus.cs`, Enemy holds them typed; bridge
  StatusTypes/Icons; verified by a stun-status QA). Then the **audio autoloads** — `AudioBus.cs` (static),
  `Sfx.cs`/`Music.cs` (the two `Node` autoloads; `project.godot` repointed to the `.cs`). Snake public surface so
  GDScript `Sfx.play(...)` still resolves; the ~26 C# `_sfx.Call("play",…)` bridges converted to **typed** calls
  (`GetNode<Sfx>("/root/Sfx").play(…)`) because `Call` can't fill C# defaults (see interop note) — so the audio
  bridge cleanup is DONE. Two GDScript `Sfx.play_at(k,p)` callers now pass all args. Verified: build clean + boot +
  the run-flow QA (Music crossfade level→base_rest, Sfx across the loop) fire with no errors. ⚠ **Sound itself
  (crossfade feel, volumes, pooling, pinned output device) needs a PLAYTEST** — headless has no audio out.
  Then the **HUD** — `HUD.cs` (~420-line port; `hud.tscn` root repointed) + `OffscreenMarkers.cs` (its only
  consumer). No caller churn (nothing calls HUD — it binds to the Player + reads its C# signals). Now that HUD is
  C# it calls `FloatingHealthBar.ColorForRatio` DIRECTLY (dropped the mirrored colour-band duplication); bridges
  SaveData (added a `get_current_cleared` getter beside the setter) / PaletteConfig / Loadout / EnemyMarkers.
  Verified: build clean + clean boot (HUD builds, binds, seeds from signals; OffscreenMarkers draws). ⚠ The
  visual LAYOUT (portrait/bars/markers) is a faithful port but worth a glance in a **playtest**.
  **Remaining phase 6:** `things/launch_orb.gd` (small) + `ui/palette_preview.gd` (the standalone pre-game colour
  picker — its own screen, no game-loop bridge value; optional / can ride with phase 7).
- [~] **7 · Config data → C# + cleanup — IN PROGRESS** (user chose "everything → C#"). All game logic/UI/audio
  is already C#; this phase ports the remaining ~30 `.gd` (config DATA tables + the VFX driver + the pre-game
  colour picker) and, as each config ports, converts its C# consumers from `GD.Load<GDScript>().Call`/constant-map
  BRIDGES to direct C# static calls (that's the bridge cleanup). Clusters migrate atomically with their consumers.
  - [x] **Leaf data:** `StatusTypes` / `EnemyMarkers` / `FloatingTextTypes` / `Icons` → C# (consumers
    StatusIcons/OverheadStatus/OffscreenMarkers/FloatingText/ExitGate/RewardUI/AttackSelect updated to direct calls).
  - [x] **Terrain** → `Terrain.cs` (RunManager updated; the `_terr*` constant-map bridge deleted). Data as C#
    `static readonly` arrays/consts; funcs as static methods. Boot-verified.
  - [x] **Actions cluster** (the largest) — `Action`/`StrikeSpec`/`Locomotion`/`SurgeSpec`/`ActionsKhalid`/`Actions`/
    `Loadout`/`CharacterConfig` → C#. Data records are `: RefCounted` with SNAKE public members, so the consumers'
    `.Get("animation")`/`.Call("segment", seg)` access on an Action works UNCHANGED (property/method exposure
    confirmed at runtime); only the accessor calls (`Actions.get_action`, `Loadout.tier_label`) switched to direct
    C# statics in Player/Build/Rewards/AttackSelect/HUD. Verified: build clean + boot + flow QA (equip a non-default
    attack, clear, reward, advance) PASS.
  - [x] **VFX driver — `ParticleDirector.cs`** (588-line `particle_director.gd` → C#, the biggest single file).
    Player now creates it typed (`new ParticleDirector()`) + calls `setup`/`set_character`/`fire_effect` directly
    (the old 1-arg `.Call("fire_effect", dash)` bridge was an arity trap — C# won't fill the `tilt` default via
    `Call`). Config readers still GDScript, **bridged**: `Emitters.character(id)` via `GD.Load<GDScript>.Call`,
    `SfxCharacters.FRAMES` via `GetScriptConstantMap`, `VfxPalette.recolor_tree` via `.Call`; `Combat.Layer.World`
    used **directly** (C#). Inlined `Nodes.find_all` (→ `FindChildren`) + `MathUtil.clip_band`/`scale_min_max_pair`;
    CPU/GPUParticles2D `finished` connected by **name** (`Connect("finished", …)`) since the two have different C#
    signal-delegate types. **Deleted** the now-orphaned GDScript `configs/combat.gd` dup + `helpers/math_util.gd` +
    `helpers/nodes.gd` (particle_director was their last consumer; no `.gd`/`.cs`/`.tscn` references remain).
    Verified: build 0 errors + clean boot (which runs `set_character` = every sustained row instantiated via the
    config bridges) + direct-`fire_effect` QA firing `double_jump`/`attack_ora_ora`/`special_ground_breaker` (the
    `clip_to_ground` branch) — fx-node count climbed each burst, zero SCRIPT ERROR. (Headless input can't drive the
    attack — documented gotcha — so the QA calls `fire_effect` on the director node directly.)
  - [x] **Emitters\* config** — `Emitters.cs` + `EmittersCharacters.cs` + `EmittersEnemies.cs` (3 `.gd` → C#
    `static` classes). Data ported as `static readonly GDict TABLE` built via `GD.Load<PackedScene>` (was
    `preload`); rows stay Godot Dictionaries so consumers' `.As<GDict>()`/`.As<GArr>()`/`row["pos"].As<Vector2>()`
    reads are **unchanged**. **Dropped both bridges**: ParticleDirector's `_emittersScript.Call("character", id)` →
    `Emitters.Character(id)`; Enemy's `EmittersScript.Call("enemy_effect", …)` → `Emitters.EnemyEffect(…)`. Reused
    the old `.gd` uids for the new `.cs.uid`. Verified: build 0 errors + clean boot + fire_effect QA (spear/twin_reaper/
    frenemy/slam bursts all spawned, base 5→21) + 6 enemies resolved their `EnemyEffect` emitters, zero errors.
  - [x] **Sfx cue configs** — `SfxCharacters.cs` + `SfxEnemies.cs` + `SfxWorld.cs` (3 `.gd` → C# `static` classes;
    `CUES`/`FRAMES` as `static readonly GDict`, int frame-keys preserved). **De-bridged 4 sites**: Sfx.cs's
    3-path `GetScriptConstantMap()["CUES"]` merge loop → 3 direct `_cues.Merge(Sfx*.CUES)`; Enemy's
    `SfxEnemiesScript.Call("frames_for", …)` → `SfxEnemies.FramesFor(…)` (bridge field deleted); ParticleDirector's
    `sfx_characters.gd…["FRAMES"]` → `SfxCharacters.FRAMES`; RunManager's `_sfxCharsC` map field → `SfxCharacters.CUES`
    directly (field deleted). Reused the old `.gd` uids. Verified: build 0 errors + clean boot + QA (played one cue
    per config through the live Sfx autoload — zero "cue … not found" warnings ⇒ all 3 CUES tables merged with
    correct paths; 240 frames of 6-enemy combat exercised the FRAMES tables via Enemy/ParticleDirector, zero errors).
  - [x] **Pre-game colour cluster (atomic)** — `VfxPalette.cs` + `PaletteConfig.cs` + `SaveData.cs` +
    `scripts/ui/PalettePreview.cs` (4 `.gd` → C#, migrated together because `palette_preview` reads all three as
    statics and GDScript can't read C# statics). PalettePreview is the **main scene** (`project.godot` main_scene);
    its `.tscn` ext_resource path rewired to the `.cs` (uid kept). Statics (`VfxPalette.picks`/`PaletteConfig.picks`/
    SaveData's schemes) persist across the picker→run `ChangeSceneToFile` exactly as GDScript statics did. **De-bridged
    every C# consumer** (Player, HUD, RunManager, Combatant, LaunchOrb, ParticleDirector — the last one now has ZERO
    bridges) from `GD.Load<GDScript>(…).Call(…)` to direct `VfxPalette.RecolorTree`/`PaletteConfig.MakeMaterial`/
    `SaveData.ReportRun` etc. **Gotcha:** Godot .NET has no `PackedFloat32Array`/`PackedVector3Array` C# types — the
    bindings marshal them as `float[]`/`Vector3[]` (a `SetShaderParameter` of a `float[]` becomes a packed uniform).
    Reused the old `.gd` uids. Verified: build 0 errors + clean boot of the picker (main scene) + interactive QA
    (10 pickers built; body/power colour_changed → PaletteConfig/VfxPalette SetPicks + RecolorTree; slot-select + Save
    round-tripped to `user://save.cfg` — `active` re-read from disk; **Start → ChangeSceneToFile → level loaded with a
    live player**), zero errors. **QA caveat noted:** driving Save wrote to the real `user://save.cfg` — avoid QA that
    mutates real user data (point it at a temp path / back up first).
  - [x] **`helpers/shapes.gd`** — DELETED, not ported: it was already **dead** (both C# consumers, Enemy + LobProjectile,
    had inlined `MakeBox`; the only remaining `Shapes.make_box` references were comments). Confirmed no code/`.tscn`
    reference before deleting. Build + boot clean after.
  - [x] **MIGRATION COMPLETE.** Every gameplay/runtime script is C#. The ONLY `.gd` left in the project are intentional
    build/QA tools run via `--script` (they stay GDScript, and mirror any C# config they need via a local `const` —
    see the tools finding above): `vfx/script/build_particles.gd`, `tools/{capture_shots,verify_frames,gen_effect_frames}.gd`.
    No C# `GD.Load<GDScript>` config/logic bridges remain (only the Sfx/Music/HUD **autoload** `GetNode` lookups, which
    are typed C#→C# now). Optional follow-up: a final snake→PascalCase polish of the public surfaces that were kept
    snake_case only for the migration (now every caller is C#).

## Post-migration — strong-typing the data layer (kill the stringly-typed GDicts)

The migration left a lot of config as `Godot.Collections.Dictionary` with hardcoded string keys (a scaffold so
GDScript consumers' `.Get("x")` kept working). With no GDScript consumers left, that's pure debt. Initiative:
replace it with **enums** (closed taxonomies), **records/classes** (data shapes), and globally-importable
**`const string` ids** — organised into dedicated top-level folders with domain subfolders (flat `namespace MyGame`):
`enums/<domain>/`, `records/<domain>/`, `ids/`. (Data TABLES stay in `configs/`; node scripts stay in `scripts/`.)

Naming: **enum members are PascalCase** (`ActionStyle.Flurry`, not `FLURRY` — the .NET convention; called out and
agreed). **IDs are `const string`, not enums** — the id doubles as the runtime loadout string AND the config key
(animation `attack_<id>`, Emitters/Sfx tables), and a single generic `Action` type can't hold a category-specific
enum id, so an enum would force `enum↔snake` conversion maps at every boundary while `const string` IS the key.

- [x] **Stage 1 — the Action / attack / tuning / buff cluster.** New: `enums/actions/{ActionCategory,ActionStyle}.cs`,
  `enums/combat/StrikeType.cs` (+ `StrikeTypes.Key()`/`.From()` for the enemy emitter/sfx key boundary),
  `ids/{AttackIds,SpecialIds,SurgeIds,MovementIds}.cs`, `records/actions/Action.cs` (record; `Animation` derived from
  Category+Id; `with`-injected Id/Category by the accessor), `records/combat/{HitData,SegmentData}.cs` +
  `records/combat/ITunable.cs`. `SegmentData` is the typed tuning (nullable fields = the old "key present?" semantics;
  `Clone()` for the resolve seam; mutated by buffs). Rewrote `ActionsKhalid` (typed catalog: `new Action { … Hit =
  new HitData(StrikeType.Melee, new SegmentData { Damage = 15, … }) }`) + `Actions` accessor. Threaded `Action`/
  `SegmentData` through the WHOLE tuning seam: `Player` (`_currentAttack`/`_activeHit`/`ResolveTuning`/`active_hit`
  typed, wrappers thinned, movement/surge read typed fields), `Passive.ModifyTuning`/`OnSpecialCast` + `Buff.AppliesTo`
  (→ `List<string>`)/`ReaperEdge`/`ParryMend`, `Strike`/`Projectile` (`apply_tuning(SegmentData)` + `ITunable`; dropped
  the dead GDict `GetF`/`source`-key paths), `LobProjectile`, `Enemy`+subclasses (`SpawnAttack(SegmentData)`, typed
  `apply_tuning` via ITunable), `ParticleDirector` (`InjectTuning`/lob via typed `Player.active_hit()`), and the UI
  (`HUD`/`AttackSelect`/`Build`/`Loadout`/`Rewards`). Deleted `configs/Action.cs` + `configs/StrikeSpec.cs`.
  **Removed `tier` from attacks** (attacks have no tier — only buffs/rewards do): dropped it from `Action`, the swap
  cards (`Loadout.Options`/`Rewards`), and the attack-select + move-line badges. Catalog/buff rewards that declare
  their own string tier keep it (`RewardsCatalog`/`Reward`/`RewardUI`/`Loadout.TierColor` untouched). Verified: build
  0 errors + clean boot (picker + `level.tscn`) + runtime QA (6 enemies via kits, `apply_hit` kills an enemy, player
  `take_damage`→death path all clean). Player-attack damage numbers pending a playtest (headless can't drive attack input).
- [x] **Stage 2 — UI presentation config (statuses + floating text).** `enums/combat/StatusType.cs`
  (Reap/Stun/Slow/Charm) + `records/ui/{StatusDef,OverheadHalo}.cs`; rewrote `StatusTypes` (DEFS/OVERHEAD →
  `Dictionary<StatusType,…>`, ORDER → `StatusType[]`, `ColorOf(StatusType)`, `Key(this StatusType)` for the
  `Icons` <c>status:</c> key). `enums/ui/FloatingTextType.cs` (Damage/DamageSpecial/PlayerDamage) +
  `records/ui/FloatingTextStyle.cs` (fixed-or-ramp size/colour with defaults); rewrote `FloatingTextTypes` →
  `Dictionary<FloatingTextType,FloatingTextStyle>`. Threaded through consumers: `Enemy.RefreshStatusIcons` builds a
  `List<StatusType>`; `StatusIcons`/`OverheadStatus` take it typed; `FloatingText.Emit` takes `FloatingTextType` +
  a typed `Color? colorOverride` (killed the per-call `GDict overrides` merge — only the player's hair colour used it);
  callers in `Player`/`RunManager`. Verified: build 0 errors + clean `level.tscn` boot + runtime QA (non-lethal stun
  → StatusType pip/halo, lethal hit → death-clear, player `take_damage` → floating text — all clean).
- [x] **Stage 3 — the enemy roster.** `ids/EnemyIds.cs` (const strings: the id IS the runtime `enemy_id` + the
  Emitters/Sfx/EnemyMarkers table key) + `enums/enemies/EnemyTier.cs` (Chip/Mid/Strong; advisory — RunManager skips
  it). Rewrote `EnemyKits` to author ids via `EnemyIds`, tier via `EnemyTier`, and `attack_type` via
  `StrikeType.<X>.Key()` — kept the kit as a by-name override BAG (its keys mirror Enemy's `[Export]` names, applied
  via `enemy.Set`; a fixed record would fight that). `EnemyMarkers` keyed by `EnemyIds`. Verified: build 0 errors +
  clean `level.tscn` boot (kit values unchanged — pure authoring change). NOTE: the EmittersEnemies/SfxEnemies table
  KEYS are still string literals (they mirror the same ids); switching them to `EnemyIds` is optional low-value churn.
- [x] **Stage 4 — door types + Icons accessors.** `enums/rewards/DoorType.cs` (Health/Athletic/Attack/Special) +
  `DoorTypes.All`/`.Key()`. Threaded through the reward flow: `RunManager._doorType`, `ExitGate.door_type`/`Setup`/
  `Info` (dropped the `Known()` validator — the enum is always valid), `Rewards.offer_for(DoorType)`,
  `RewardsCatalog.POOLS` → `Dictionary<DoorType,GArr>`, `Reward.Make(DoorType)`/`.Door`, `RewardUI.Open(DoorType)`.
  Added typed `Icons.Door(DoorType)` + `Icons.Status(StatusType)` (StatusIcons uses the latter). Also swapped the
  reward catalog's cross-domain move refs to consts (`AttackIds.TwinReaper`/`.RopeDart`, `SpecialIds.RedereShield`).
  **Already-typed (no work):** `Combat.Layer` enum + helpers; `RewardTypes` `Tier`/`Trigger` enums. Verified: build
  0 errors + reward-flow QA (clear → gate `touched` → `offer_for(DoorType)` → `POOLS[DoorType]` → RewardUI shown, clean).
- [x] **Stage 5 — reward + passive ID consts.** `ids/PassiveIds.cs` (ties a passive's ctor `Id`, the
  `Rewards.MakePassive` dispatch, and the catalog `passive` field) + `ids/RewardIds.cs` (the catalog reward keys).
  Threaded through the passive ctors, `MakePassive` (const-pattern switch), and `RewardsCatalog`. Pure const-value
  swaps — build 0 errors + clean boot, no behaviour change.
- [x] **Stage 6 — reward-tier unification + LoadoutCategory.** (a) **Retired the string reward-tier**
  (typical/elite/broken) — unified onto the `RewardTypes.Tier` enum (Common…Epic): `Reward.RewardTier` is now `Tier?`,
  the catalog's one tiered reward (`rope_dart`) is `(int)Tier.Epic`, `RewardUI` badges via `Tiers.ColorOf`/
  `Label`, and `Loadout.TierColor`/`TierLabel` + their dicts are deleted (dead). (b) **`enums/actions/LoadoutCategory.cs`**
  (Attack/Special/Surge/Run/Jump/Dash/Slam) + `LoadoutCategories.All`/`.Movement`/`.Key()`/`.Kind()`/`.Parse()`.
  Threaded through Player (`_loadout` → `Dictionary<LoadoutCategory,string>`, `equip`/`loadout_id`/`LoadoutGet`/
  `ApplyLoadout`/`ApplyMovement` all typed), `Loadout` (Options/DefaultId/SwapChoices), `Build`
  (preserved the surge-not-pluralised quirk), `Rewards` (swap-card `Parse` at the `swap:<cat>:<id>` seam + the Equip
  dict), `RunManager`. Verified: build 0 errors + QA (`equip(LoadoutCategory.Attack,"spear")` from GDScript marshals,
  reward flow with the enum shows the UI, zero errors).
- **Data-typing initiative: COMPLETE across all shipped domains.** Only truly cosmetic tidy-ups remain and are
  optional: moving `Locomotion`/`SurgeSpec` into `records/` (they work as typed classes), and the
  EmittersEnemies/SfxEnemies **table keys** → `EnemyIds` (they already mirror the same ids).
