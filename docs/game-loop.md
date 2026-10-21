# Game loop — endless ROUNDS (the "CoD Zombies" loop)

**STATUS: rounds design AGREED 2026-09-24; the economy (§ Economy) AGREED 2026-09-28; being built in steps (see
[§ Build order](#build-order)).** This is the source of truth for the run structure. Numbers are starting points —
we tune live in engine (the house rule: verify by *running* Godot, never assume).

Companion docs: **`buff-catalog.md`** (every buff, sorted into Needle Point / the lottery stall / Figmenter),
`rewards-design.md` (raw buff wishlist), `art-direction.md` (stage-as-art-unit), `game-design.md` (old-loop
reference).

> **Design history — retired, do not reintroduce:**
> - **Fissure / Seal / Warden-charge** (the previous frozen design): three Fissures you Sealed for 3 Ruh to release
>   a Warden whose strength grew with time. **Retired** in favour of rounds — no Fissures, no Seal, no charge bar,
>   no `Seal` buff category, no "Ruh triangle".
> - **Main boss + Greed** (the design before that). If you see `Greed`, `greedWeight`, or a "main boss", it's stale.
> - **Reward doors / 5 levels per stage / exit gates** (the original loop).

---

## The premise in one paragraph

Khalid fights in one **flat** arena (one level floor, no platforms — the owner's decision of 2026-10-12, so that
every enemy can always reach him) through **endless, numbered rounds**. Each round has a **hidden kill quota**: enemies
trickle in (never more than a concurrent cap alive at once) until the quota has spawned, then spawning stops;
the round **clears** when every one of them is dead — and the next, bigger round starts **at once** (a **ROUND n**
banner, no break: the player never gets a breather; changed 2026-10-03). As rounds climb, enemies come **faster and in greater numbers**, and increasingly arrive at a
higher **rank** — recoloured to the rank's colour — hitting harder and soaking more. There is no end: the run is a
score chase, **highest round reached** is the record, and **permadeath** ends it.

---

## A round

1. **Quota** `Q(r)` — how many enemies round `r` sends. Hidden from the player (like CoD). Starting shape:
   `Q(r) = a + b·r + c·r²` (roughly quadratic, like CoD's count curve).
2. **Trickle** — spawn one at a time on an interval, up to the concurrent cap `C(r)` (grows slowly with `r`).
   Each one appears at a **spawn spot** the layout places (`EnemySpawns` markers) — **one enemy per spot**, the free
   one nearest the player that is still at least `SpawnMinDistance` 320 px from him — and **hunts** him from that
   moment (changed 2026-10-11; until then it patrolled its spot and the player went to find it, which played as a
   walk from one enemy to the next). On the flat stage that is all there is to it. (The code also covers levels with height —
   a hunter walks off a ledge that has ground below and waits under a platform it cannot climb, the crowd spread a
   little, `HuntWaitSpread` 40 px — but grunts do not jump or teleport, by decision.) **From round 10**, a growing share of grunts (20%, +10%/round, max 70%) instead spawns **right behind the
   player** on his floor (100–240 px), as the pressure ramps up. From the grunt roster; per-kit `spawn_cap`s still apply. The curve **starts slow**
   (round 1: 6 enemies, up to 6 alive at once, one every 1.5 s) and climbs every round (`configs/Rounds.cs`).
3. **Spawning stops** once `Q(r)` enemies have been spawned.
4. **Clear** — the round ends when all `Q(r)` are dead (kills == quota **and** none left alive).
5. **Next round, immediately** — the **ROUND n+1** banner and round `n+1` begins. **No break, no countdown**
   (changed 2026-10-03; the earlier ~8 s breather is gone). The stalls are open all the time, so shopping happens
   between fights, wherever the player makes room for it.

**What counts:** only **quota** enemies. Optional enemies (the stationary sleeper Nasen, the kamikazes below) are
**not** in the quota and don't block a clear.

**Stragglers** (CoD's "last zombie"): once the whole quota has spawned and only `StragglerCount` (3) or fewer
remain, they hunt **1.6× faster** (`StragglerSpeedMult`) — so the end of a round doesn't drag.

**Stand-still pressure (kamikazes):** from round `KamikazeFromRound` (5), a player who stays within `StillRadius`
(50 px) of one spot for `StillTime` (2 s) gets a **kamikaze** (Ein) `KamikazeDistance` (220 px) to one side and up
to `KamikazeHeight` (110 px) above — far enough to react — then another every 2 s while he stays put (5
alive at most) — both ramp with the round (interval ×0.95/round to 0.75 s; +1 max every 5 rounds to 8). Kamikazes
**drop nothing** and aren't in the quota; moving resets the clock. **Nem** pauses the clock while he sleeps (one
already diving still comes).

**The edge enemy (Ventilator):** from round 3, a player who lingers within 300 px of either end of the arena (1 s)
gets a **Ventilator** spawned inland of him on his floor. Its attack is a **wind gust** that does **no damage** but
flings him **outward — off the edge** unless he dodges, air-jumps back (within ~0.3 s) or dashes back. One at a time,
10 s cooldown after it dies. Not in the quota, but drops Lira + figs like any enemy. Tuning in `configs/Rounds.cs`.

---

## Scaling — how rounds get harder

- **Count + pressure:** `Q(r)` grows ~quadratically; the concurrent cap `C(r)` and the spawn rate grow slowly, so
  later rounds are both longer and *stickier* (the arena refills faster after each kill).
- **Roster:** the grunt roster can widen by round (tougher kits later) — data, not code.
- **Ranks** (below) carry the per-enemy stat growth.

### Enemy ranks — "tier colours" on enemies

Each spawned enemy rolls a **rank**. Early rounds are all base rank; a new rank enters the mix every few rounds and
the mix shifts upward over time — a round is a **blend**, so a high-rank enemy stands out as the threat.

- **Colour language:** Common (no colour) · Rare (blue) · Hot (orange) · Sensational (purple) · Epic (red) — the
  old buff-tier ladder. It's its own type (`EnemyRank`) — distinct from the advisory `EnemyTier` (Chip/Mid/Strong
  kit strength). *(Buffs no longer use tiers — Needle Point shots have their own level colours, § Economy;
  whether ranks adopt that ladder is an open question.)*
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
(Aegis: 5 s invulnerability, 1 charge). The Max Ruh drop is the other way to refill it. *(The rainchecked "healing
point" is dropped. There is no bought heal at all since 2026-10-16: health comes back from "when X, heal" triggers —
the lottery's heal items and the Figmenter's lifesteal, capped per round — and the Nem surge, § Economy.)*

---

## Wardens — later (noted, not in the first build)

**Every 10th round is a Warden round**: a Warden (Kroj is the first — `WardenEnemy`, already built: relentless
teleporting pursuer with a fair telegraphed warp, cinematic spawn) arrives alongside a thinner grunt quota; the
round clears when both are dead. **For now: grunts only.** Kroj stays out of the spawn pool until this lands.

---

## Economy — Lira, Fada Figs, and the three stalls

**AGREED 2026-09-28.** Replaces the old fig economy (figs on every kill + the free milestone buff menu — both
**removed**). Every number here is a **placeholder** to tune in play.

### Two currencies — both reset every run

| | **Lira** (common) | **Fada Figs** (rare) |
|---|---|---|
| Drops | **every kill**, in today's fig amounts (Chip 1 · Mid 2 · Strong 3) | a **per-enemy chance** per kill: **15 %** default (10 % until 2026-10-11), **Kebus 25 %** (the hardest grunt); tougher ranks / Wardens tuned later. A hit drops **1** fig |
| Pickup | flies to Khalid like Ruh (the magnet pickup); its own pickup sfx *(placeholder until the real sound lands)*; art `index32_art/art/things/lira.png` | as today |
| Spent on | **Needle Point** shots + **lottery** pulls | the **Figmenter** (+ future altars) |
| HUD | a counter (replaces the fig milestone ring) | a counter |

Neither carries over between runs — carrying currency out would let a player hoard power into the next run.

**The two currencies never compete:** Lira only buys at the stalls, figs only gamble at the Figmenter. The real choice is
inside Lira: **grow a permanent stat** (Needle Point) or **gamble on a boost right now** (the lottery stall).

### The three stalls differ in KIND, not just duration

The rule that sorts every buff (`buff-catalog.md`):

| Stall | Sells | Examples | Feel |
|---|---|---|---|
| **Needle Point** | **numbers** on Khalid's body (a "shot") | +dash, +air jump, run speed, jump height, attack damage | "I'm stronger this round" |
| **The lottery stall** | a **random boost, now** (an "item"), lasting a number of kills or uses | Ruh charges, specials off cooldown, fig and Lira bonuses, a few heal triggers | "let's see what I get" |
| **Figmenter** | **new mechanics** that define the build | Zahluq, invuln after a dash, a vortex on the dash, chain hits | "my run plays differently now" |

A stat boost changes a number Khalid already has; a lottery item is a short random boost you can't choose; the
box's pull is that **its mechanics can't be bought anywhere else**. (A second dash = Needle Point; a dash that
leaves a vortex = the Figmenter.) A flat "+20 % damage forever" is a number → it's a Needle Point shot, not a Figmenter buff.

**All three stalls are open all the time** (changed 2026-10-03 — there are no breaks any more; between 2026-09-28 and
then they were break-only). What you buy is **active at once**.

### Needle Point — the stat shots

**Permanent since 2026-09-29** (re-buying the same stats every break was a chore; permanent power also fits an endless
run that keeps getting harder).

- **Stock:** the **whole catalog, always** (7 shots). No rotation.
- **Each purchase = one RANK, for the rest of the run:** rank I (grey) → II (green) → III (blue) → IV (purple) → V
  (gold), up to that shot's own max (the dash / air-jump shots stop at III). Then the button reads **MAXED**.
- **Every rank costs more than the last**, ×1.6 per rank, from a **per-shot rank-I price** (2026-10-03 — priced by
  worth: mobility cheap, damage an investment): Jump Height / Run Speed **15**, Extra Jump **25**, Extra Dash / Reach /
  Slam Damage **30**, Attack Damage **60** (→ 96 / 154 / 246 / 393, ~950 to max). For scale, the round curve pays
  ~140 Lira by the end of round 5, ~560 by round 10. Placeholders.
- **Figs don't touch Needle Point any more** — buying with Lira *is* the upgrade; figs are the Figmenter's currency.
- **Pauses the game** (it's a menu).
- **Watch:** a strong player can max all of Needle Point late in a run — after that, Lira only goes to the lottery. The
  price curve delays it; if late rounds feel Lira-rich, add sinks.

### The lottery stall — pay, spin, take what lands

*(Replaced Dekken, the perk shop, on 2026-10-16 — the owner's design over five councils that day. "LotteryStall" is a
working name; candidates the owner was given: Longshot, The Drop, Fluke, Moonshot, Maybeam.)*

- **Pay first, then spin.** **E** pays at once and the orb in the beam spins ~**1 s** and lands on a random item. A
  pull can't be declined — the tension is the money already spent (Risk of Rain's chests, the CoD box). Declining for
  free would make the best play "stand in the beam until Heal comes round".
- **Live, no pause, no menu.** Rounds never stop, so a pull is taken under fire — the only stall where shopping is
  itself a risk.
- **Instant, and nothing is carried.** The item takes effect when the orb lands; there are no vials, carry slots, Q or
  Tab any more (deleted 2026-10-16). "When to pull" is the decision.
- **Price:** **20** Lira, **+10** for each further pull in a round, **4 pulls** a round at most; both reset when a round
  starts. The prompt shows the next price and the pulls left.
- **The wheel only holds what would help right now** — an item gated to a special you don't have, a second Second
  Wind, or one that would do nothing (Ruh Flask at full Ruh) is left out of that spin.
- **Items last KILLS or USES, never "this round"** — with no break between rounds, a timed item pulled late would be
  wasted.
- **A duplicate lasts longer, never stronger** — two Bounties don't pay ×4 (more Lira → more pulls → more Bounties
  would run away).
- **No bought heal.** Health comes back from **heal triggers**, which are at most **1 in 5** of any spin, and ALL
  triggered healing (these + the Figmenter's lifesteal) shares a cap of **2 blocks a round** — a heal the cap stops
  shows **CAP**. Second Wind, the emergency one, is not capped.

| Item | Lasts | Weight | Stacking | Heals | Note |
|---|---|---|---|---|---|
| **Ruh Flask** | instant | 4 | happens again | | +2 Ruh charges; off the wheel at full Ruh |
| **Cooldown Flush** | 3 uses | 4 | +3 uses | | your next specials skip their cooldown |
| **Fig Frenzy** | 25 kills | 4 | +25 kills | | +15 % fig odds; loose figs within 400 px fly to you |
| **Bounty** | 25 kills | 4 | +25 kills | | ×2 Lira per kill |
| **Wider Pull** | 25 kills | 4 | +25 kills | | Come Closer pulls 2 more; only with Come Closer (kills, not casts: the pull reads the bonus after the cast) |
| **Crimson Vortex** | 25 kills | 4 | +25 kills | | every dash leaves a vortex where it started; it spins 1.3 s and hits each enemy in it once (18) |
| **Leech Draught** | 30 kills | 1 | +30 kills | ✓ | 10 % of landed hits heal half a block |
| **Tithe** | 8 kills | 1 | +8 kills | ✓ | every 4th kill heals half a block |
| **Second Wind** | once | 1 | one at a time | ✓ | at your last half-block, heal a block |

All numbers are placeholders (`configs/Lottery.cs`). **Last Call** (heal at round end per kills) was cut: with
kill-measured items it overlapped Tithe. *Later, not now:* more items (8–10) including rare "jackpot" faces, item
icons for the orb, pairs of items with a named bonus, stacks that upgrade, the wine biasing the wheel.

### Figmenter — fig wine, the permanent build

*(Redesigned by the owner 2026-10-11; built the same day. It replaced the spin → offer "mystery box".)*

- **It makes wine.** **4 figs** start a brew (at the 15 % fig rate the first brew starts around round 3). The
  brew takes **2 rounds**; the tank visibly fills a step each round. (8 figs, 10 % and 5 rounds until 2026-10-11:
  the first mechanic arrived around round 9–10, after most runs' build should already exist.)
- **The wine holds one build-defining buff**, **permanent** for the run once drunk — never one you already hold. The
  bottle is **labelled**: you know what it is when it is ready. Very rarely it is a **special-swap** instead.
- **Drink it or carry it.** You collect the wine into your **one wine slot** and can drink it at any moment. Carried,
  it **ages**: +1 rarity every **2 rounds** (Common → Rare → Hot → Sensational → Epic), and the buff comes out that
  much stronger (each buff has five values). So the choice is power now, or more power later — at least 2 rounds after
  paying either way, and with only one slot the next ready wine waits in the machine until you drink.
- **Real time** — nothing pauses.
- **Planned, not built:** a between-runs upgrade system (XP spent in the main menu) that shortens the brew.
- **It stays put.** The Figmenter stands where the layout places it, like the other two stalls. (It relocated on a
  "teddy bear" roll until 2026-10-11; the owner removed that.)
- **Anti-overpower:** fig scarcity and the 2-round brew pace it (first wine ~round 5); Figmenter buffs are **mechanics, not
  multipliers**; no duplicates. In reserve if it's still too strong: the cost rises per brew, or some Figmenter buffs
  carry a drawback (RoR lunar-style).

### Placement + interaction

- Each layout places the **lottery stall** and **Needle Point** (easy to reach, near spawn) and the **Figmenter**
  (scenes in `scenes/things/`); none of them moves. One **interact** key + an on-screen prompt for all three.
- HUD: the Lira + fig counters; the owned shots and the held lottery items in the top-right list (an item with its
  kills / uses left).
- **Round 1** starts at 0 Lira — the first purchase comes after round 1 (intentional).

### Later ideas — noted, not in this build

- ~~**The Vial**~~ — built 2026-10-04 as Dekken's drink-now-or-keep vials; **removed 2026-10-16** with Dekken (the
  lottery's items take effect at once).
- **Build-gated places (noted 2026-10-05).** Until the flat arena (2026-10-12) one stall worked this way:
  Dekken sat on a platform you could only reach with enough **air jumps / jump height** from Needle Point. Extend that into a general idea — parts of
  the map, and what's on them, are locked behind what your **build** can do, each by a different stat:
  - **Mobility — jumps:** high or far platforms (what Dekken's spot was).
  - **Mobility — dashes:** a run of **traps / hazards** you can only cross with enough **dash charges**.
  - **Both:** routes that need buffed jumps **and** dashes together.
  - **Attack power:** something that only opens to enough **damage** (a barrier or door with a health bar, a guard
    that shrugs off weak hits).
  - …and so on for other stats (run speed against a timer, slam damage against a breakable floor).
  The point is that Needle Point ranks stop being only numbers — each one is also a **key**, and different builds open
  different parts of the map. Open: what waits in each place (see the far-platform rewards discussion — permanent
  unlocks were the suggestion), and whether a gate should ever lock something a run *needs* (a stall) or only extras.
- **Shop relocation** (the CoD teddy bear, but for the stalls).
- **HP-cost altars** (Risk of Rain blood shrine): pay health for power.
- **Per-purchase price growth** for shots (see Prices).
- ~~**The timed break**~~ (a shrinking break with "hold E to start the next round") — **superseded 2026-10-03**: the
  owner chose no breaks at all.

---

## Meta-progression — permanent upgrades from the main menu (planned, not built)

Things a player unlocks **once, for all future runs** — kept separate from the in-run economy (Lira and figs
always reset). Added only if the loop needs more variety; **nothing we build now should design these out**
(shot / lottery-item durations are per-item data precisely so this can raise them later). How they're earned (runs played,
best round, or a persistent meta-currency) is undecided.

- **Longer items / deeper shots:** certain lottery items last **more kills**, or Needle Point shots get a higher max
  rank / a cheaper rank I — permanently.
- **The lottery stall upgrades through experience** (the owner's idea of 2026-10-04 for Dekken's vials): the more you
  play, the more experience points you earn, spent on the stall for all future runs (what an upgrade does to a random,
  short-lived item — more pulls, a cheaper first pull, longer items — is undecided).
- **Sigils** — pre-run run-rule modifiers with tradeoffs (the long-standing idea in the glossary).

---

## HUD

- **ROUND n** (replaces the old WAVES line), and **"n LEFT"** once only a few quota enemies remain (~5).
- The record becomes **highest round reached** (persisted by `SaveData`).
- The enemy count is otherwise hidden, like CoD.

---

## Loadouts — attack + special + surge chosen together (agreed 2026-10-01, not built)

A run starts by picking ONE of five fixed **loadouts** (replacing the attack-only picker); a player can't mix pieces
freely — swapping one mid-run takes a special item found during the run (today: the Figmenter's special-swap). The
five use every attack, every regular special and every surge exactly once:

| Loadout | Attack | Special | Surge | Plays as |
|---|---|---|---|---|
| **Brawler** | Ora Ora | Come Closer | Jnoon | pull a crowd in, double damage, punch through it |
| **Reaper** | Twin Reaper | Redere Frisbee | Asra | mark with the DoT, knock enemies off, kite at double speed |
| **Lancer** | Spear | Redere Shield | Wara | block + parry, punish with the thrust combo; Wara turns the unblocked hit into a stun |
| **Gunner** | Cherry Shots | Bakshen | Aegis | ranged by default; anything that gets close eats a Bakshen (Aegis covers its wind-up) |
| **Warden** | Rope Dart | Ground Breaker | Nem | area control — the AoE stun is what makes Nem's 5 s heal safe |

**Rare specials** (box special-swaps, not in any loadout): **Zahluq** and **Frenemy** (once reworked — below).
Bakshen leaves the Figmenter pool, since it starts in Gunner. Open: the default loadout (Brawler suggested), and whether
items can also swap an attack or a surge.

**Frenemy needs a rework** (noted 2026-10-01): charming one slow grunt for 8 s on a 12 s cooldown adds very little.
It stays in the code as-is until the owner picks a replacement. Candidates: **Puppet Bomb** (the charmed enemy sprints
into the nearest group and explodes), **Discord** (everyone in range fights each other for a few seconds), **Hex**
(+50 % damage taken), **Fear** (enemies in range flee).

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

The existing `Buff : Passive` + `Trigger` + `ModifyTuning` foundation fits. Every buff, sorted by stall:
**`buff-catalog.md`** (its Seal category is retired).

- **Sources (§ Economy):** Needle Point shots (numbers, permanent, levelled), lottery items (random, paid first,
  lasting kills or uses), Figmenter buffs (mechanics, permanent, five strengths by the wine's rarity). A buff belongs to **one** pool.
- **Levels replace tiers:** only Needle Point shots scale, by **level** (grey → green → blue → purple → gold → …);
  the catalog's five per-tier values become the level values. Figmenter buffs use one value.
- **Persistence:** **timed-seconds** (a proc's short in-combat effect) | **N kills / uses** (lottery items) |
  **instant** (Ruh Flask) | **the whole run** (Needle Point shots, Figmenter buffs).
- **Stacking:** different buffs stack freely — **no cap on active buff count**. A second copy of a lottery item **adds** its
  kills / uses (longer, never stronger); the Figmenter never gives a duplicate. Shot + Figmenter effects on the same stat **add** (e.g. +20 % from a shot
  and +10 % from a Figmenter buff = +30 %).
- **Each buff record carries:** its pool · persistence + duration · the level values (shots) or its single value
  · a trigger.
- **Categories:** Dash · Jump · Slam · Attack (general + per-attack) · Special (per-special) · Surge.
- **Triggers — keep the system dynamic/extensible.** Have today: OnDash / OnGroundJump / OnAirJump /
  OnSlamTrigger / OnSlamLand / OnHitDealt / OnHurt. Reserved until the Player emits them: OnPerfectDodge / OnMiss /
  OnAttackAnimationEnd / OnAttackTrigger. Natural additions for this loop: OnRoundStart / OnRoundClear.
- **Many buffs are new mechanics, not numbers** (traps, weaken, frisbee bounces, perfect-dodge detection) — each
  custom effect is its own implementation pass.
- **Forward-compat for Sigils:** keep room for run-rule modifiers (future pre-run items with tradeoffs); don't build
  them yet.

---

## Parked ideas

Ideas cut from the game or set aside, kept so they aren't lost: where each lives, why it is out, and what would bring
it back. An idea that comes back is rebuilt as real game code (`docs/standards.md`, rule C8).

| Idea | Where it lives | Why it is out | What would bring it back |
|---|---|---|---|
| **Camping meteor** — stand still 5 s and a meteor falls where you stand (it hits like an attack) | `experiments/meteor/` — an EXPERIMENT, compiled only when switched on in `mygamedev.csproj`, never in a release build | A council ruled it out (2026-10-13): standing still already draws kamikazes from round 5, lingering at the ends a Ventilator. The owner kept it (2026-10-19) to try out and film | A playtest showing camping still pays despite the kamikazes — or as a run modifier the player chooses (see the Sigils note above) |
| **Tag-out** — a round enemy stuck 3 s under a player it can't reach swaps for a Mazab or a Kebus | A git stash in `index32_dev` (`tagout non-projectile enemies for projectile ones`) | Built during the roof-camping question; not merged | The Needle Point roof playtest: if standing on the roof still beats the round |
| **Painted platform arena** | `scenes/levels/stage1/painted_platforms.tscn` (the owner's — don't delete unasked) | The owner chose the flat arena (2026-10-12) | A layout with platforms coming back into the design |

---

## Build order

1. **Rounds** — the quota/cap/stop/clear state machine in `RunManager` (no breather since 2026-10-03), the ROUND banner, HUD `ROUND n` + `n LEFT`, record = highest round.
2. **Stragglers** — last-few enemies hunt the player. ✅ (2026-10-03, with spawn spots + stand-still kamikazes)
3. **Ranks** — `EnemyRank`, the rank mix by round, per-rank stats (health / damage taken / half-blocks per hit),
   the accent-recolour shader.
4. **Round drops** — the pickup class family: Max Health (rare) + Max Ruh.
5. **Warden rounds** — every 10th round, Kroj + a thinner quota.

**The economy (§ Economy)** lands in its own playable steps — each leaves the game working:

- **E1. Lira + fig drops** *(built 2026-09-28)* — the Lira pickup (magnet, placeholder sfx, HUD counter), per-kit fig chance, and
  **removing** the fig milestone menu + the fig ring.
- **E2. Needle Point** *(built 2026-09-28; made permanent 2026-09-29)* — the shot catalog, bought rank by rank with
  Lira at a rising price, permanent for the run (always open since 2026-10-03).
- **E3. Dekken** *(built 2026-09-28)* — 5 rotating perks, per-perk durations, whole-run perks leaving the pool.
  **Replaced 2026-10-16 by the LOTTERY STALL** (§ The lottery stall): pay first, a random item at once, no vials.
- **E4. Figmenter rework** *(built 2026-10-04)* — real-time spin → offer → take/decline; the mechanics pool
  (never a duplicate), the rare special-swap; Fast Travel goes to it. **Replaced 2026-10-11 by FIG WINE** (§ Figmenter):
  a brew of a few rounds (5 at first, 2 since 2026-10-11), a carried bottle that ages through five rarities, the buff as strong as the wine. (The teddy bear — refund + relocation between
  easy/hard spots — was built with it and removed 2026-10-11: the Figmenter stays put.) `Figmenter` + `FigmenterLedger` +
  `configs/FigmenterRules.cs`; tiers (`Tier`, `Family`) and the paused 3-card `RewardUI` are gone.
- **E5. Layout + HUD** — *partly built 2026-09-30:* the stalls are scenes placed in the layout (`scenes/things/`,
  found by `LevelLayout.Placed<T>()`) and share one interact prompt. Left: the HUD's active-buff icons. (The pointer to the Figmenter is dropped: it no longer moves.)
- **L1. Loadouts** — the run-start loadout picker (§ Loadouts) replacing the attack-only picker.

---

## Impact on existing code

- **`RunManager`'s steady trickle** (`SpawnWave` / `_waveCount` / fixed `MaxAlive`) → the round state machine;
  `SaveData`'s waves record → a **rounds** record.
- **Retired with Fissure/Seal:** the Seal buff ids (`BuffIds` Seal block) and any Fissure/Seal wording.
- **Deleted (older loops):** 5-levels-as-data (`Levels.cs`), the exit gate (`ExitGate.cs`), the level-template tool.
  The reward doors (`Rewards`, `RewardsCatalog`, `Build`, `DoorType`, `RewardIds`, and their Leech / Parry Mend /
  Reaper Edge passives) are **deleted** too. `RewardUI` lives on as the buff-card menu.
- **Kept:** the arena + tileset/terrain authoring, the enemies (`EnemyKits`, `WardenEnemy`), the typed
  enums/records/ids foundation, combat components, art direction.
- **Reworked by the economy:** fig-on-every-kill → Lira (+ a per-kit fig chance); the **fig milestone buff menu**
  (`RunManager` `FirstMilestone`/`MilestoneGap*`, the HUD `FigRing`) → **removed**; `Figmenter` (its dud chance,
  3-card powerful menu, fixed spot) → the § Economy box; `BuffCatalog`'s `Tier`/`MIN_TIER`/`MildIds`/`PowerfulIds`
  → the three pools + shot levels.

---

## Naming glossary

- **Round** — one numbered wave of the endless loop: a hidden quota, a trickle, a clear — then straight into the next.
- **Quota** — the round's hidden enemy count `Q(r)`. **Cap** — max quota enemies alive at once `C(r)`.
- **Rank** — an enemy's per-spawn strength level (Common → Epic), shown by its recoloured accent.
- **Round drop** — a floating power-up (Max Health, Max Ruh) that lasts the round.
- **Warden** — the elite of the Warden rounds (every 10th; later). Kroj is the first.
- **Ruh** — the meter (surge).
- **Lira** — the common currency (every kill): buys shots and lottery pulls. **Fada Figs** — the rare currency (a
  per-enemy chance): starts a brew at the Figmenter.
- **Needle Point** — the stat stall; sells **shots** (permanent numbers, bought rank by rank).
- **The lottery stall** — pay first, the orb spins and lands on a random item that takes effect at once (working name;
  replaced **Dekken**, the perk shop, 2026-10-16).
- **Figmenter** — spend figs for a permanent build-defining buff.
- **Rank** — a shot's level, one per purchase (grey → green → blue → purple → gold).
- **Redere Shield** — default Special: a frontal damage-block. **Aegis** — default Surge (5 s invuln, 1 charge).
- **Sigil** — future pre-run run-rule modifier.

---

## Open questions

- `Q(r)`, `C(r)`, spawn interval — live-tuned.
- Rank mix curve (when each rank enters, how fast the mix shifts) and per-rank stat multipliers — live-tuned.
- Round-drop odds + the minimum distance from the player.
- Every § Economy placeholder (prices, price growth, durations, max ranks, Figmenter cost,
  the lottery's prices, pulls, item counts and the heal cap) — live-tuned.
- Whether enemy rank colours adopt the shot-level ladder (grey/green/blue/purple/gold) for one "strength" colour
  language.
- How meta-progression unlocks are earned (§ Meta-progression) — undecided, and only if the loop needs it.
