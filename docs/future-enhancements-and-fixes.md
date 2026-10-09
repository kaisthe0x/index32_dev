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
- Heal `kill_heal` HP on each enemy death (hook the existing `Enemy.died` / RunManager kill path).
- Tune `drain_rate` vs `kill_heal` so a competent player nets positive while fighting, negative
  while idling. Surface the drain in the HUD (e.g. a subtle red vignette pulse or a downward HP
  tick) so it's readable.
- Consider: does the special-meter / reward economy differ by mode? (Probably Hard just adds the
  drain on top of the same loop.)

---

<!-- Add future ideas below this line. -->

## Box buffs that can't be built yet

Catalogued in `ids/BuffIds.cs` and `docs/buff-catalog.md`, left out of `BuffCatalog.FACTORIES` because the hook each
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
  (`Hitbox.deactivate` only emits it for non-special hits), so it would never fire.

## Sound: beyond the limiter

`Sfx.InstallLimiter` puts a hard peak ceiling on the SFX bus so stacked cues can't spike. It is a safety net, not a
mix. Ways to improve it:

1. A gentle **compressor** before the limiter (`AudioEffectCompressor`, about -18 dB threshold, about 4:1), so loud
   moments duck smoothly instead of being clamped.
2. A per-cue **concurrency cap**: skip or duck a cue that is already playing N copies, or fired within a few
   milliseconds. This tackles the cause (the same sound stacking), not the summed symptom.
3. Expose the ceiling, or the limiter itself, in the options menu.
