# Game loop — endless ROUNDS (the "CoD Zombies" loop)

**STATUS: design AGREED 2026-09-24; being built in steps (see [§ Build order](#build-order)).** This is the source
of truth for the run structure. Numbers are starting points — we tune live in engine (the house rule: verify by
*running* Godot, never assume).

Companion docs: **`buff-catalog.md`** (tiered buffs), `rewards-design.md` (raw buff wishlist), `art-direction.md`
(stage-as-art-unit), `game-design.md` (old-loop reference).

> **Design history — retired, do not reintroduce:**
> - **Fissure / Seal / Warden-charge** (the previous frozen design): three Fissures you Sealed for 3 Ruh to release
>   a Warden whose strength grew with time. **Retired** in favour of rounds — no Fissures, no Seal, no charge bar,
>   no `Seal` buff category, no "Ruh triangle".
> - **Main boss + Greed** (the design before that). If you see `Greed`, `greedWeight`, or a "main boss", it's stale.
> - **Reward doors / 5 levels per stage / exit gates** (the original loop).

---

## The premise in one paragraph

Khalid fights in one arena through **endless, numbered rounds**. Each round has a **hidden kill quota**: enemies
trickle in (never more than a concurrent cap alive at once) until the quota has spawned, then spawning stops;
the round **clears** when every one of them is dead. A short **breather**, a **ROUND n** banner, and the next,
bigger round begins. As rounds climb, enemies come **faster and in greater numbers**, and increasingly arrive at a
higher **rank** — recoloured to the rank's colour — hitting harder and soaking more. There is no end: the run is a
score chase, **highest round reached** is the record, and **permadeath** ends it.

---

## A round

1. **Quota** `Q(r)` — how many enemies round `r` sends. Hidden from the player (like CoD). Starting shape:
   `Q(r) = a + b·r + c·r²` (roughly quadratic, like CoD's count curve).
2. **Trickle** — spawn one at a time on an interval, up to the concurrent cap `C(r)` (grows slowly with `r`).
   Spawning around the player, from the grunt roster; per-kit `spawn_cap`s still apply.
3. **Spawning stops** once `Q(r)` enemies have been spawned.
4. **Clear** — the round ends when all `Q(r)` are dead (kills == quota **and** none left alive).
5. **Breather** (~8 s) — no spawns; the **ROUND n+1** banner; then round `n+1` starts. (Natural moment for the
   mystery box / the fig milestone menu — both stay as they are.)

**What counts:** only **quota** enemies. Optional enemies (the stationary sleeper Nasen) are **not** in the quota
and don't block a clear. An enemy **despawned** by the anti-camp rule (off-screen too long) is **not a kill** —
it goes back into the round's unspawned pool so a fresh one spawns near the player.

**Stragglers** (CoD's "last zombie"): once only a few quota enemies remain, they **hunt** the player (no idle
patrol, no give-up leash), and the anti-camp despawn/respawn keeps a stuck one from stalling the round.

---

## Scaling — how rounds get harder

- **Count + pressure:** `Q(r)` grows ~quadratically; the concurrent cap `C(r)` and the spawn rate grow slowly, so
  later rounds are both longer and *stickier* (the arena refills faster after each kill).
- **Roster:** the grunt roster can widen by round (tougher kits later) — data, not code.
- **Ranks** (below) carry the per-enemy stat growth.

### Enemy ranks — "tier colours" on enemies

Each spawned enemy rolls a **rank**. Early rounds are all base rank; a new rank enters the mix every few rounds and
the mix shifts upward over time — a round is a **blend**, so a high-rank enemy stands out as the threat.

- **Colour language = the buff tiers:** Common (no colour) · Rare (blue) · Hot (orange) · Sensational (purple) ·
  Epic (red). One colour ladder for "how strong is this thing" across the whole game. It's its own type
  (`EnemyRank`), sharing the colours — distinct from the advisory `EnemyTier` (Chip/Mid/Strong kit strength).
- **Recolour at RUNTIME, not baked:** enemies are a dark body + ONE neon accent (art direction), so a shader swaps
  the accent's hue to the rank colour (higher ranks may glow brighter). Works for every current and future enemy
  with no per-rank sheets (baking would be enemies × ranks sheet sets, every art change repeated).
- **Stats by rank:** more health and/or less damage taken, and — the scary part — **heavier hits**:
  - base ranks hit for **½ block** (today's rule: every hit costs a flat half-block),
  - higher ranks hit for **1 block**, then **1½**, and at the very top **2 blocks** — only reached when a player is
    *very* deep and heavily buffed.

---

## Round drops — power-ups (CoD's Max Ammo)

A new pickup family: a **floating icon** dropped at random during a round. It **lasts the whole round** (despawns
at the clear) and the player collects it by touching it, **whenever they choose**.

- **Never dropped right in front of the player** — it appears **away** from them (a minimum distance), so it isn't
  grabbed by accident when they'd rather save it.
- **Max Health** — full heal. **Rare.**
- **Max Ruh** — fills the Ruh meter. More common than Max Health.
- More drop types can join the family later (it's its own class hierarchy, data-driven odds).

---

## Ruh

**Surge only (for now).** Ruh fills by landing hits (a special's own hits grant none) and is spent on the Surge
(Aegis: 5 s invulnerability, 1 charge). A **healing point** is wanted eventually — rainchecked. The Max Ruh drop is
the other way to refill it.

---

## Wardens — later (noted, not in the first build)

**Every 10th round is a Warden round**: a Warden (Kroj is the first — `WardenEnemy`, already built: relentless
teleporting pursuer with a fair telegraphed warp, cinematic spawn) arrives alongside a thinner grunt quota; the
round clears when both are dead. **For now: grunts only.** Kroj stays out of the spawn pool until this lands.

---

## Currencies & rewards (as built today — unchanged by this pivot)

- **Fada Figs** — dropped on every kill. The **spendable balance** feeds the **mystery box** (a stingy
  powerful-buff gamble); the **lifetime total** crosses escalating milestones that pop a **free pick-1-of-3 buff
  menu**.
- **Buffs** — permanent for the run; see § Buff system + `buff-catalog.md`.
- *(The previously planned "Chest" and grunt temp-buff drops are open — not part of this build.)*

---

## HUD

- **ROUND n** (replaces the old WAVES line), and **"n LEFT"** once only a few quota enemies remain (~5).
- The record becomes **highest round reached** (persisted by `SaveData`).
- The enemy count is otherwise hidden, like CoD.

---

## Failure

Death ends the run (permadeath). The only goal is to get further than last time.

---

## Tuning laws (carve these in stone)

1. **The player must feel like a god vs. the swarm.** Clear-throughput must outpace swarm escalation, or it's a
   treadmill. Buff power has to scale faster than `Q(r)` / `C(r)` — until very deep rounds, where the top ranks
   (1½–2 block hits) are what finally ends a run.
2. **The rank curve is the real difficulty engine** (CoD's is zombie health: roughly linear early, then ~×1.1 per
   round). Count alone doesn't kill a strong player; harder-hitting, tankier ranks do.
3. **A reliable AoE / crowd-clear tool must be reachable early**, or big rounds simply win.
4. **Enemies must be cheap + pooled** (no per-frame pathfinding) to hit high concurrent caps; caps are tuned live.

---

## Buff system — rules for the build

The existing `Buff : Passive` + `Tier` + `Trigger` + `ModifyTuning` foundation fits. Full tiered list:
**`buff-catalog.md`** (its Seal category is retired).

- **Tiers:** Common (no colour) · Rare (blue) · Hot (orange) · Sensational (purple) · Epic (red). Tier scales
  magnitude *and* adds effects (e.g. 12→20→30→50→75%; frisbee gains bounces).
- **Persistence:** **timed-seconds** (short in-combat effects) | **permanent** (the rest of the run).
- **Stacking:** different families → unlimited stack; same family → higher tier **replaces** lower. **No cap on
  active buff count** — stacking is part of the fantasy.
- **Each buff record carries:** a family id (stacking key) · stacks-vs-replaces rule · persistence · tier +
  tier-scaling · a trigger.
- **Categories:** Dash · Jump · Slam · Attack (general + per-attack) · Special (per-special) · Surge.
- **Triggers — keep the system dynamic/extensible.** Have today: OnDash / OnGroundJump / OnAirJump /
  OnSlamTrigger / OnSlamLand / OnHitDealt / OnHurt. Reserved until the Player emits them: OnPerfectDodge / OnMiss /
  OnAttackAnimationEnd / OnAttackTrigger. Natural additions for this loop: OnRoundStart / OnRoundClear.
- **Many buffs are new mechanics, not numbers** (traps, weaken, frisbee bounces, perfect-dodge detection) — each
  custom effect is its own implementation pass.
- **Forward-compat for Sigils:** keep room for run-rule modifiers (future pre-run items with tradeoffs); don't build
  them yet.

---

## Build order

1. **Rounds** — the quota/cap/stop/clear/breather state machine in `RunManager`, anti-camp despawn returning to the
   pool, the ROUND banner, HUD `ROUND n` + `n LEFT`, record = highest round.
2. **Stragglers** — last-few enemies hunt the player.
3. **Ranks** — `EnemyRank`, the rank mix by round, per-rank stats (health / damage taken / half-blocks per hit),
   the accent-recolour shader.
4. **Round drops** — the pickup class family: Max Health (rare) + Max Ruh.
5. **Warden rounds** — every 10th round, Kroj + a thinner quota.

---

## Impact on existing code

- **`RunManager`'s steady trickle** (`SpawnWave` / `_waveCount` / fixed `MaxAlive`) → the round state machine;
  `SaveData`'s waves record → a **rounds** record.
- **Retired with Fissure/Seal:** the Seal buff ids (`BuffIds` Seal block) and any Fissure/Seal wording.
- **Already obsolete (older loops):** reward doors (`DoorType`, `ExitGate`, `RewardUI` door flavour),
  5-levels-as-data (`Levels.cs`) + exit-gate assumptions. `RewardUI` itself lives on as the buff-card menu.
- **Kept:** the arena + tileset/terrain authoring, the enemies (`EnemyKits`, `WardenEnemy`), the typed
  enums/records/ids foundation, combat components, art direction, figs + mystery box + milestone buff menu.

---

## Naming glossary

- **Round** — one numbered wave of the endless loop: a hidden quota, a trickle, a clear, a breather.
- **Quota** — the round's hidden enemy count `Q(r)`. **Cap** — max quota enemies alive at once `C(r)`.
- **Rank** — an enemy's per-spawn strength level (Common → Epic), shown by its recoloured accent.
- **Round drop** — a floating power-up (Max Health, Max Ruh) that lasts the round.
- **Warden** — the elite of the Warden rounds (every 10th; later). Kroj is the first.
- **Ruh** — the meter (surge). **Fada Figs** — currency (mystery box) + buff-menu milestones.
- **Redere Shield** — default Special: a frontal damage-block. **Aegis** — default Surge (5 s invuln, 1 charge).
- **Sigil** — future pre-run run-rule modifier.

---

## Open questions

- `Q(r)`, `C(r)`, spawn interval, breather length — live-tuned.
- Rank mix curve (when each rank enters, how fast the mix shifts) and per-rank stat multipliers — live-tuned.
- Round-drop odds + the minimum distance from the player.
- The healing point (rainchecked) — where, cost, and whether it's breather-only.
- Between-run meta-progression vs. pure permadeath restart — undecided.
