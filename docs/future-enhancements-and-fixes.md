# Future enhancements & fixes

A running parking lot for ideas and fixes that are **not being built yet**. Drop new ideas here
the moment they come up; promote one into a real task when it's time. Newest at the top is fine.

---

## Game modes: Normal vs. Hard ("Attrition")

Two run modes. Names are placeholders — want something more evocative than "Normal / Hard".

### Hard mode — passive health drain ("Attrition")

The player is **actively, slowly dying**: HP ticks down on its own as time passes. **Killing an
enemy restores a small chunk of HP.** This forces aggression — you can't camp; you have to keep
killing to stay alive.

- The drain **stops when only one enemy (or none) remains** in the level. So the player can leave
  the last enemy alive and take a breather / reposition without bleeding out.
- Intended as the **Hard** mode only; Normal mode has no drain.

**Rough implementation shape (for when we build it):**
- A run-level `mode` flag (Normal / Hard) chosen at run start.
- In Hard mode, drain HP at `drain_rate` HP/sec while `alive_enemy_count > 1`; pause otherwise.
- Heal `kill_heal` HP on each enemy death (hook the existing `Enemy.Died` / RunManager kill path).
- Tune `drain_rate` vs `kill_heal` so a competent player nets positive while fighting, negative
  while idling. Surface the drain in the HUD (e.g. a subtle red vignette pulse or a downward HP
  tick) so it's readable.
- Consider: does the special-meter / reward economy differ by mode? (Probably Hard just adds the
  drain on top of the same loop.)

---

<!-- Add future ideas below this line. -->

## Box buffs that can't be built yet

Catalogued in `ids/BuffIds.cs` and `docs/buff-catalog.md`, left out of `BuffCatalog.Factories` because the hook each
one needs doesn't exist. (Moved here from comments in the code, 2026-10-09.)

- **Slam Feast** — needs a count of enemies killed by the slam. The slam's damage strike is spawned by the
  `ParticleDirector` on the slam animation's frames 3/4, which is *after* `OnSlamLand` is dispatched, so nothing has
  been killed yet at the hook. Needs a slam-kill tally.
- **Backstab** — needs the victim's position against the player's facing at the moment of contact. Damage is baked
  into the `Hitbox` when it is activated and applied in `Hitbox.OnAreaEntered` with the amount already fixed; there
  is no per-victim tuning hook before contact. Needs an on-contact tuning seam.
- **Perfect-Dodge Haste / Fury / Aegis** — a dash dodge can't be detected. Dash i-frames work by making the player's
  hurtbox non-monitorable during the dash, so an incoming hit never reaches `OnHurt` and there is no "avoided by
  dashing" event. Needs a real perfect-dodge window that doesn't weaken the dodge.
- **Instant Reset (Zahluq)** — built but parked (`BuffCatalog.Parked`): special-box whiffs don't emit `OnMiss`
  (`Hitbox.Deactivate` only emits it for non-special hits), so it would never fire.

## Sound: beyond the compressor and limiter

`Sfx.InstallBusEffects` puts a compressor (-18 dB, 4:1 — built 2026-10-12, so a horde's stacked sounds don't drown the
music) and then a hard peak ceiling on the SFX bus. Both treat the summed symptom. Ways to improve it:

1. **Music at one loudness.** The three stage-1 tracks measure -21.6, -23.5 and -24.1 LUFS (integrated), so the music
   itself moves 2.5 dB between tracks. Render them from the source project at one integrated loudness (the middle
   one, about -23.5 LUFS, keeps today's level); a per-track gain table in code would break the "drop a file in the
   folder" rule of `music/README.md`.
2. A per-cue **concurrency cap**: skip or duck a cue that is already playing N copies, or fired within a few
   milliseconds. This tackles the cause (the same sound stacking), not the summed symptom.
3. Expose the ceiling, or the limiter itself, in the options menu.

## Animation: smoother motion, maybe the Dead Cells way

Noted 2026-10-11; the owner wants to explore this later. Nothing is built.

**The wish.** Khalid's animations should feel smoother, above all the RUN (it is on screen most of the time), and
the change from one movement to the next should not pop. Two ideas the owner raised: more frames per animation, and a
3D base under the pixel art "the way Dead Cells does it".

**How Dead Cells does it** (the lead artist's own write-up — "Art Design Deep Dive: Using a 3D pipeline for 2D
animation in Dead Cells", Game Developer): a rough 3D model and skeleton; animation built pose-to-pose on KEY frames,
with easing frames added before or after a key and "never in-between"; each frame rendered tiny with no
anti-aliasing, together with a normal map for a toon shader; effects layered on top carry the motion. The 3D model
buys fast retakes (retime or redo an animation in minutes), not smoothness. It needs a skilled modeller and animator.

**What was tried, and what to learn from it.**
- A blocky 3D Khalid scripted in Blender with a procedural run cycle, rendered at game size and shaded in his
  palette. REJECTED by the owner: it changed his shading and silhouette, read as slower, and looked lifeless. Evenly
  blended motion from code is the opposite of the pose-to-pose approach above. Do not repeat this; any future attempt
  starts from the owner's own frames and keeps them untouched.
- A look at the run's own frames: its ten frames are six distinct drawings (6 and 9 repeat 2, 8 repeats 4, 10
  repeats 5), and the strides in the loop are 4, 3 and 2 frames long, so the bounce is uneven. Playing frames 1–8
  only (two even strides, at 12 fps to keep the pace) was shown to the owner, who saw no major difference. Not applied.

**Where to pick it up.**
- Every change of animation is a hard cut (`Sprite.Play` of the next one from its first frame). Bridge frames between
  the pairs that pop most would follow the Dead Cells rule (ease into and out of the keys); the game would need a
  small "from A to B, play these first" rule.
- Tools found, none tested: **GhostFrames** (an Aseprite extension that estimates in-between frames from two frames
  and can insert them as a guide to redraw over); **Smack Studio** (Steam, Early Access: pixel art mapped onto a 3D
  skeleton you pose, then exported as sprites — the nearest thing to the Dead Cells method without 3D skills; its
  ideal character height is 50–150 px and Khalid is about 35); a proper low-poly model and animator for the full
  Dead Cells pipeline.
- A change of frame count in an attack also means updating the tables keyed to frame numbers (`HIT_FRAMES` in
  `tools/gen_spriteframes.py`, the particle and sound frame keys). The run has none of those.

## Built, not wired up yet

Code that exists and works but has no caller today. It is kept on purpose (owner's decision, 2026-10-09) for the
feature named beside it; if that feature is dropped, delete the code with it (rule `C1`).

- **Volume and mute controls** — `Sfx` / `Music` `SetVolume`, `GetVolume`, `SetMuted`; `Music.Pause` / `Resume`;
  `AudioBus.SetVolumeDb`, `IsMuted`, `GetEffect`, `SetEffectEnabled`. For: volume sliders and mute toggles in the
  pause menu.
- **A dash that leaves an effect** — `Player.SetDashEffect` (and through it `Player.HoldAnimation`, reached only by
  the crimson-vortex dash effect). For: a box buff or perk that turns a dash effect on.
- **A character's intrinsic ability** — `Player.CharacterAbilityFor` (returns nothing: Khalid has none) and the
  `CharacterAbility` base. For: a second character, or giving Khalid one.
- **More than one character** — `CharacterConfig.Ids`, `FramesPath`, `PortraitPath`, `AbilityPath`; the non-Khalid
  body tint path in `Player.ApplyCharacter` with `vfx/shaders/sprite_tint.gdshader`. For: the parked characters
  coming back.
- **Hue for a texture-baked effect** — `VfxPalette.HueFor`. For: recolouring an effect drawn as a texture rather
  than particles.
- **Small leftovers of the same kind** — `StrikeTypes.From` (parse a strike-type key), `StatusDef.Label` (a status's
  display name), `SurgeSpec.DamageTakenMult` (a surge that changes damage taken), `PaletteConfig.ShadesPer`,
  `Enemy.CurrentAttackType`.
- **Planned content** — `EnemyKits.Kroj` and `WardenEnemy` (Warden rounds); the 22 `BuffIds` with no factory yet
  (see "Box buffs that can't be built yet" above and `docs/buff-catalog.md`).
