# Changelog

One entry per push, newest first. Each entry lists its commits with **what** changed, **why**, what it **could
affect**, and how it was **tested** — written for whoever reads this in six months, the owner included.
History before 2026-10-04 is in `git log` and `docs/game-loop.md`.

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
