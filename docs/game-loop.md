# Game loop — the Fissure / Seal / Warden pivot

**STATUS: design AGREED, NOT yet implemented.** This is the source of truth for the new run structure.
Implementing it **obsoletes / repurposes** a chunk of the current run-flow code — see [§ Impact on existing
code](#impact-on-existing-code). Numbers here are starting points to prototype, not final — we tune live in
engine (the house rule: verify by *running* Godot, never assume).

This supersedes the old premise (5 levels/stage, clear enemy batches, exit → random reward door). Companion
docs: **`buff-catalog.md`** (the tiered buff list built from this spec), `rewards-design.md` (the raw buff
wishlist), `art-direction.md` (stage-as-art-unit), `game-design.md` (old-loop reference).

> **Design history:** an earlier version of this loop ended each stage with a **main boss** whose power was set
> by a hidden **Greed** meter (damage-buffs banked). Both the boss and Greed are **retired** — the three
> **Wardens** are now the climax, and the tension is a *visible* race against their growth. If you see `Greed`,
> `greedWeight`, or a "main boss" anywhere, it's stale.

---

## The premise in one paragraph

Khalid drops into a **stage arena** (he spawns at the **centre**, which is also the heal spot). Grunts
proximity-spawn around him at a thin base rate. Three **Fissures** sit in the arena; behind each, a **Warden**
is *charging* — a single, shared **Warden-strength** (health + damage) that **grows from t=0 and is capped**.
**Sealing a Fissure** (a 2 s channel costing a flat **3 Ruh**) makes its Warden **burst out** at *whatever
strength has accumulated so far* — its strength then **locks** — and each Seal **brakes** the growth for the
Fissures still charging *and* **ramps the grunt swarm**. Wardens are **relentless teleporting pursuers** you
can't out-run. Kills pay **Fada Figs** and **Ruh**; the **Chest** is open from the start (spend Fada Figs → pick 1 of 5
permanent buffs). **Kill all three Wardens → next stage. Three stages, permadeath.** Greed is gone — the
tension is the Warden-charge bar, and it's on screen.

---

## The strategic axis — *when* you seal each Fissure

There are no scripted beats; there's one continuous fight and one decision you make three times: **seal this
Fissure now, or let it charge?**

- **Seal early** → the Warden bursts out **weak**, and the brake keeps the others weaker too — **but** your
  build is still thin (little time to farm Fada Figs / buy Chest buffs) and every Seal **thickens the swarm**, so a
  rush ends in a **swarm-heavy** fight against three feeble Wardens.
- **Seal late** → you've banked buffs and Fada Figs and the swarm is still thin — **but** the Warden bursts out a
  **titan** (capped), so a patient run ends in a **warden-heavy** fight against monsters you're built to face.

Both are viable and *feel different* (a horde fight vs. a titan fight). The charge is **capped**, so waiting
tops out at "very hard," not "impossible." Sequential play (seal 1 → kill it → seal 2 → …) faces Wardens one at
a time, but each later one charged longer, so it's stronger; rush-sealing faces three weak Wardens at once.

---

## The Ruh triangle (the spine of the whole design)

**Ruh is the single scarce resource and the most valuable thing in the game.** One meter, three sinks:

| Sink | Cost | Role |
|---|---|---|
| **Seal** a Fissure | flat **3 Ruh** | progress (spawn + brake) |
| **Surge** (Aegis) | 1 charge / 100 | survive (5 s invulnerability) |
| **Heal** at the centre spot | drains Ruh, gradual | sustain |

**Source:** landing hits (as today — a special's own hits grant no Ruh). Wardens grant Ruh on hit too, so the
meter doesn't flatline once the swarm thins. **Ruh cap is buffable** — and because a Seal costs a *fixed* 3
(not "all current Ruh"), a bigger cap is pure upside: seal *and* keep Ruh for a surge/heal. Ruh-cap rewards are
**rare and capped** (e.g. ≤ +2 cap/stage) so they don't snowball.

Every moment is a Ruh budget: *do I seal (progress), surge (survive), or heal (sustain)?* That contention is
**deliberate** — it's the core decision of the game. (A teleporting Warden warping onto your centre heal-spot is
exactly why healing is never safe.)

---

## Wardens — the climax

- **One per Fissure**, spawned by **Sealing** it. Before that it doesn't exist as an enemy — it's a **charge
  value** ticking up behind the Fissure.
- **Charge model:** a **single shared Warden-strength** (HP + damage) grows from stage start at a base rate,
  **capped**. Each **Seal brakes the rate** for the Fissures still charging. When you Seal, that Warden **spawns
  at the current value and locks** — a spawned Warden does **not** keep growing; it just hunts you.
- **Entrance:** a **dramatic spawn animation** (vfx/sfx) — the Warden *bursts* from its Fissure. These entrances
  are the set-piece the retired boss used to be.
- **Relentless teleporting pursuit:** Wardens follow you across the **whole map** and **teleport** to stay on
  you — you **cannot kite them** (this is the shelved grunt-teleport idea, now the Wardens' signature; grunts
  stay non-teleporting and escapable). The warp must be **fair** (below).
- **Fair warp (required):** a Warden may **not** blink onto you and instantly hit. It needs a **danger
  indicator** (a tell above the player / at the landing spot) with a real reaction window, so the counter is
  **dash to dodge** (i-frames). With multiple Wardens out you can only dodge **one warp at a time** — that's the
  tax for rushing all three. *(Future: fancier warp variants; for now, just make it fair.)*
- **Placeholders:** Wardens don't exist yet → build with **beefed-up grunts** (tankier/faster/more-Ruh-on-hit,
  a stand-in charge value + teleport) and drop real Wardens in later as data + art (`art/wardens/`).

**Progression = kill all three Wardens → next stage.** The grunt swarm is **ambient** (Ruh/Fada Figs farm + pressure);
it does **not** gate the stage.

---

## Currencies & rewards

| Thing | What | From | Carries across stages? |
|---|---|---|---|
| **Ruh** | the meter (seal + surge + heal) | landing hits (+ Wardens) | meter resets per stage |
| **Fada Figs** | currency for the Chest | every kill (100%, small) | **yes** (for now) |
| **Buffs** | run power | 3 sources below | depends on source |

### Three buff sources (choose vs. random is the key axis)
- **Grunt drops** — **Common–Rare**, **temp** (wiped at stage end), **random**, tiered chance (low tiers more
  common), **constant** rate (they're disposable, no need to throttle).
- **Warden kills** — **≥ Rare**, **random**, can be permanent; may also drop a Fada Fig lump / health / a Ruh-cap
  bump. This is the relocated prize — you **earn** it by beating the elite, not by opening its cage. **A rung
  below Chest quality** so the Chest stays the build destination.
- **Chest** — **Hot–Epic**, **permanent**, **you choose 1 of 5**. The build-crafting tool. **Open from the
  start** for a modest Fada Figs price; **cost rises each use**; **resets each stage**; after **3 uses**, a **10%
  close chance, +10% per subsequent use**. The rising cost + lock are the **anti-turtle throttle** — they're
  what stops "farm forever, buy everything, then stomp a capped Warden."

---

## The arena & enemies

- **Centre spawn = centre heal spot** — the *eye of the storm*: grunts proximity-spawn around you and a
  teleporting Warden will warp right onto you, so the one place you retreat to heal is the most dangerous.
  Healing is a **gradual, interruptible channel** — a hit mid-heal can leave you **net-negative**; healing is a
  commitment, not a free reset.
- **Grunts spawn around the player** (Ein/Nasen keep their distant/random spawns). Fissures are **not** grunt
  spawn points — they're Seal objectives / Warden cages.
- **Grunts don't teleport.** Jump to a far platform and they can't follow — they patrol until you re-enter
  detection. To stop "parking the whole roster far away," a grunt that **loses you past a leash despawns
  elegantly** and is **replaced near you** — kiting-to-breathe works, permanent-parking doesn't. **Wardens are
  the exception** — they teleport and never let go.
- **Swarm cap + escalation:** starts ~**20 & thin**, ramps to ~**60 & high** across a stage (**3 steps, one per
  Seal**; resets per stage). The cap is a hard ceiling (spawns wait when full) — "faster spawns" means "re-fills
  to the cap faster after a kill," i.e. the swarm gets **stickier**, not infinite. So a rush-sealer maxes the
  swarm early; a patient player keeps it thin.

---

## Progression & failure

- **3 stages.** Each = a new art skin (see `art-direction.md`) **and** harder: tankier/nastier grunts, worse
  Wardens (higher base + charge rate + cap, weirder abilities), a higher swarm ramp. Keep it a **per-stage
  multiplier table** so "add a stage" stays data entry.
- **Carry across stages:** Chest (permanent) buffs + Fada Figs. **Don't carry:** grunt-dropped (temp) buffs — wiped
  at stage end.
- **Failure = the DPS race.** No hard timer: if the Wardens you spawned outscale your damage, you can't kill
  them before they grind you down. **Permadeath** — death ends the run.
- **The end:** clear Stage 3 = run complete. (Win screen / loop / endless = open.)

---

## Tuning laws (carve these in stone)

1. **Rubber-band SUB-linear — the player must feel like a god vs. the swarm.** Clear-throughput must **outpace**
   swarm escalation — swarm ~3× (20→60), player power ~**5–8×** — or it's a treadmill and the "erase 60 enemies"
   fantasy never lands.
2. **The Warden cap vs. player power is the new key curve** (it replaced the Greed→boss curve). Set the cap
   **high enough, and reachable slowly enough, that "wait it out" is scary — not free.** If a capped Warden is
   trivially out-DPS'd by a full build, the race evaporates and turtling wins. Prototype this early.
3. **Ruh-per-hit vs. sink costs is THE first number to prototype.** The whole spine lives or dies here.
4. **A reliable AoE / crowd-clear tool must be reachable early**, or the ramped swarm simply wins.
5. **Cap + enemy AI are tuned live in engine** — 60 is a guess; enemies must be **cheap + pooled** (no
   per-frame pathfinding) to hit that count.

---

## Buff system — rules for the build

The existing `Buff : Passive` + `Tier` + `Trigger` + `ModifyTuning` foundation fits; this is the spec to build
the catalog against. Full tiered list: **`buff-catalog.md`**.

- **Tiers** (from `rewards-design.md`): Common (no colour) · Rare (blue) · Hot (orange) · Sensational (purple)
  · Epic (red). Tier scales magnitude *and* adds effects (e.g. 12→20→30→50→75%; frisbee gains bounces).
- **Persistence** (replaces the retired "duration in levels"): **timed-seconds** (short in-combat effects) |
  **stage** (temp drops, wiped at stage end) | **permanent** (Chest + some Warden-kill rewards).
- **Stacking:** different families → **unlimited stack**; same family **permanent** → higher tier **replaces**
  lower; a **temp + a permanent** of the same family **both apply while the temp lives** (then only the
  permanent remains); temps wiped at stage end. **No cap on active buff count** — stacking is part of the fantasy.
- **Each buff record carries:** a **family id** (stacking key) · a **stacks-vs-replaces** rule · **persistence**
  · **tier + tier-scaling** · a **trigger**. *(No `greedWeight` — Greed is retired; nothing buff-side scales the
  Wardens, whose growth is pure time + Seal-brakes.)*
- **Categories:** Dash · Jump · Slam · Attack (general + per-attack) · Special (per-special) · Surge · **+ a NEW
  `Seal` category** (not in `rewards-design.md` — brainstorm separately): seal faster / instant, seal for less
  Ruh, **invuln-while-sealing**, AoE-stun on seal, auto-effects on Warden kill, etc.
  - **Base Seal = a 2 s vulnerable channel**, so buffs that erase that risk are top-tier: **instant-seal** and
    **invuln-while-sealing** are **Epic** prizes; cheaper-Ruh / faster-but-not-instant / on-seal bonuses sit lower.
  - **Dash + perfect-dodge buffs are extra-valuable now** — the dash is the counter to Warden warps, so
    dash-invuln / perfect-dodge families shine against the climax.
- **Triggers — keep the system DYNAMIC/extensible** (many trigger types incoming). Have today:
  OnDash / OnGroundJump / OnAirJump / OnSlamTrigger / OnSlamLand / OnHitDealt / OnHurt. Reserved until the
  Player emits them: OnPerfectDodge / OnMiss / OnAttackAnimationEnd / OnAttackTrigger / first-N-seconds-of-stage.
  New for this loop: OnSeal / OnWardenKill / (others as needed). The catalog can be authored now; reserved-
  trigger buffs stay inert until the emit points exist.
- **Many buffs are new *mechanics*, not numbers** (traps: stun/weaken/DoT/burst; a 25%-weaken debuff; frisbee
  bounce-count; projectile-on-punch; an "air wall"; perfect-dodge detection). The framework + data + tiers come
  first; each custom effect is its own implementation pass.
- **Forward-compat for Sigils:** build the system extensible enough to later accept **run-rule modifiers**
  (not stat buffs) — items that change offer counts, costs, or impose tradeoffs (e.g. *"pick 2 from the Chest
  but lose a random buff"*). Leave the door open; don't build them yet.

---

## Impact on existing code

**Obsoleted / repurposed** (built for the old loop):
- **Reward doors** — `DoorType`, `ExitGate`, `RewardUI` (door flavour), the Health/Athletic/Attack/Special
  door offer. Replaced by grunt drops + Warden-kill rewards + the Chest.
- **5-levels-as-data** (`Levels.cs`) + the painted-level loader's **5-level / exit-gate** assumptions
  (`RunManager.BuildLevel`, `StageLayoutPaths` over `stage1_v*`). Replaced by one stage arena.
- **Scattered ground/air wave spawns** — grunts now proximity-spawn around the player.

**Repurposed / kept:**
- `LevelLayout` → a **`StageLayout`**: centre **PlayerSpawn = HealSpot** + **3 Fissure markers**, no exit gate.
- `scenes/levels/stage1/` → the **stage arena**; `stage1_v1` (v-variants reserved for *future* visual variety
  of the same stage — way post-launch).
- **Kept wholesale:** the tileset/terrain authoring, the enemies (`EnemyKits`), the typed enums/records/ids
  foundation, the combat components, the art direction. Wardens **don't exist yet** → build the loop with
  **beefed-up grunts as placeholders**, drop real ones in later as data + art.

---

## Naming glossary

- **Fissure** — an arena object you Seal (3 per stage); a Warden charges behind it.
- **Seal** — the flat-3-Ruh, 2 s action that closes a Fissure: **spawns** its Warden at the current charge,
  **brakes** the remaining charge, **ramps** the swarm.
- **Warden** — the elite that bursts out on Seal (one per Fissure); a capped, locked-at-spawn, **teleporting**
  relentless pursuer. The stage's climax; killing all three advances.
- **Ruh** — the meter (seal + surge + heal). **Fada Figs** — currency (Chest).
- **Redere Shield** — default Special: a **frontal damage-block** (back/flanks stay exposed). **Aegis** —
  default Surge (5 s invuln, 1 Ruh charge).
- **Sigil** — *future* pre-run item (a run-rule modifier with tradeoffs), chosen alongside the attack.
- *(Retired: **Boss**, **Greed**, `greedWeight` — do not reintroduce.)*

---

## Open questions (not blocking the buff build)

- **Warden charge curve + cap** — base rate, per-Seal brake amount, cap value: all live-tuned (law #2 is the one
  to prototype). "Don't overthink the exact numbers; set one and tweak."
- **Warp danger-indicator** — reaction-window length, and whether Aegis/Redere also counter a warp (not just
  dash). Tuned in engine.
- Between-run **meta-progression** (unlock attacks/specials/cosmetics) vs. pure permadeath restart — undecided.
- The **Seal buff category** contents + full **tier assignments** — drafted in `buff-catalog.md`, awaiting red-pen.
