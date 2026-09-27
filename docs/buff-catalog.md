# Buff catalog — tiered, from `rewards-design.md`

**STATUS: proposed tiers for red-pen.** This turns the `rewards-design.md` wishlist into a structured, tiered
catalog + a new **Seal** category, ready to become code against the buff-system spec in `game-loop.md`
(§ Buff system). **Tiers and numbers are my proposal — adjust freely.**

> **Note (2026-09-24):** the **Fissure/Seal loop is retired** (see `game-loop.md` — the game is now endless rounds),
> so the **Seal category below is retired** too: kept only as a record, not to be built. Warden-related notes
> still apply to the future Warden rounds.
>
> **Note:** an earlier draft carried a `greedWeight` per buff (to scale a hidden boss). **Greed and the boss are
> retired** — Wardens grow on a pure time + Seal-brake curve, nothing buff-side feeds them — so there is **no
> greed column** here. If you see one anywhere, it's stale.

---

## How to read this

- **Tier** — Common (no colour) · Rare (blue) · Hot (orange) · Sensational (purple) · Epic (red). A buff is a
  **family** that scales across tiers:
  - **Scalable** buffs list five values `C / R / H / S / E` (e.g. damage `12 / 20 / 30 / 50 / 75 %`).
  - **Threshold** buffs exist only from a **min tier up** (e.g. *invuln-while-sealing* is **Epic-only**); higher
    tiers add a rider.
- **Kind** — **Passive** (a modifier that holds as long as the buff is owned) vs **Proc** (fires a
  set-duration effect when its trigger hits). *Persistence* (temp-this-stage vs permanent) is **source-driven**
  (grunt drop = stage-temp; Warden kill / Chest = permanent), not a property of the buff.
- **NEW** — effect needs a mechanic that doesn't exist yet (traps, weaken debuff, dash-hitbox, bounce, air-wall,
  perfect-dodge/miss detection). Framework + data first; each custom effect is its own pass.
- **⏳** — trigger is **reserved** (Player doesn't emit it yet): OnPerfectDodge, OnMiss, OnAttackAnimationEnd,
  OnAttackTrigger, first-N-seconds-of-stage. Authorable now, inert until the emit point lands.

---

## Dash  (trigger: OnDash unless noted)

| Buff | Effect & tier | Kind | Notes |
|---|---|---|---|
| **Dash Immunity** | immune after dash: `0.5 / 1 / 1.5 / 2 / 3` s | Proc | extends dash i-frames — great vs Warden warps |
| **Chain Dash** | free re-dash, no cooldown. R: +1 · H: +1, 2 s window · S: +2 · E: unlimited for 2 s | Passive | mobility; pairs nastily with Dash Damage |
| **Dash Damage** | pass-through deals `8 / 14 / 22 / 34 / 50` | Passive | **NEW** (dash hitbox) |
| **Dash Leech** | pass-through steals `3 / 5 / 8 / 12 / 18 %` of dmg as HP | Passive | **NEW**; needs Dash Damage; sustain |
| **Dash Stun** | pass-through stuns `1 / 1.5 / 2 / 3 / 4` s | Passive | **NEW**; control |
| **Dash Trap** | leave a trap on dash — see **Traps** below | Passive | **NEW** |
| **Perfect Dodge: Haste** ⏳ | on perfect-dodge: `+30/40/50/60/75 %` speed, `5` s | Proc | ⏳ OnPerfectDodge |
| **Perfect Dodge: Fury** ⏳ | on perfect-dodge: `+30/50/70/90/120 %` dmg, `7` s | Proc | ⏳; timed damage — huge vs a warping Warden |
| **Perfect Dodge: Aegis** ⏳ | on perfect-dodge: immune `1.5 / 2 / 2.5 / 3 / 4` s | Proc | ⏳; defense |

## Jump  (trigger: OnGroundJump unless noted)

| Buff | Effect & tier | Kind | Notes |
|---|---|---|---|
| **Jump Immunity** | immune after ground-jump `0.5 / 0.75 / 1 / 1.5 / 2` s | Proc | defense |
| **High Jump** | `+15 / 30 / 45 / 60 / 80 %` jump height | Passive | mobility |
| **Jump Trap** | leave a trap on ground-jump — see **Traps** | Passive | **NEW** |
| **Extra Air Jump** *(OnAirJump)* | R: +1 air jump · H: +1 · S: +2 · E: +3 | Passive | **threshold, min Rare**; mobility |
| **Peak Slam** *(OnAirJump)* | at jump peak a cue plays; slam within it = `+30/50/70/90/120 %` slam dmg | Passive | **NEW** (peak window + cue); conditional dmg |

## Slam  (trigger: OnSlamTrigger / OnSlamLand)

| Buff | Effect & tier | Kind | Notes |
|---|---|---|---|
| **Slam Volley** *(Trigger)* | fire `3 / 5 / 7 / 9 / 12` downward projectiles | Passive | **NEW**; real added DPS |
| **Slam Force** *(Trigger)* | `+20 / 35 / 50 / 70 / 100 %` slam damage | Passive | core slam scaler |
| **Slam Immunity** *(Land)* | immune `1 / 1.5 / 2 / 2.5 / 3` s | Proc | defense |
| **Slam Quake** *(Land)* | survivors stunned `1 / 1.5 / 2 / 3 / 4` s | Proc | control |
| **Slam Feast** *(Land)* | `+5 / 10 / 15 / 20 / 30 %` current HP per enemy killed | Proc | sustain |
| **Slam Spring** *(Land)* | next ground jump `+30/50/70/90/120 %` height | Proc | mobility |
| **Slam Wrath** *(Land)* | attack dmg `+30/50/70/90/120 %` for `1 / 1.5 / 2 / 2.5 / 3` s | Proc | timed damage |

## Attack — general  (apply to any chosen attack)

| Buff | Effect & tier | Kind | Notes |
|---|---|---|---|
| **Long Reach** ⏳ | hitbox reach `+25/50/75/100/150 %` | Passive | ⏳ OnAttackTrigger (or just passive) |
| **Opening Fury** ⏳ | first `5/7/9/12/15` s of a **stage**: attack dmg ×2 | Proc | ⏳ stage-timer; a fast-clear burst |
| **Momentum** *(OnHitDealt)* | each consecutive hit ×`1.15 / 1.25 / 1.4 / 1.6 / 2` (ramp, resets on miss) | Passive | biggest DPS lever |
| **Hit Guard** *(OnHitDealt)* | immune `0.1 / 0.25 / 0.4 / 0.6 / 1` s on hit | Proc | defense |
| **Follow-through** ⏳ | immune `0.5 / 1 / 1.5 / 2 / 3` s at attack-anim end | Proc | ⏳ OnAttackAnimationEnd |

## Attack — per-attack  (only offered if that attack is equipped)

| Attack | Buff | Effect & tier | Notes |
|---|---|---|---|
| **Zahluq** | Ledge Save ⏳ | on anim-end near a ledge, boost back on-stage. R→E: bigger window/height | **NEW**; ⏳ anim-end; utility |
| | Bloodrush | on-hit steal `3/5/8/12/18 %` HP | sustain |
| | Instant Reset ⏳ | on **miss**, cooldown resets fully | ⏳ OnMiss; more casts = more dmg |
| **Ora Ora** | Barrage | punches fire projectiles: `4/6/8/12/16` dmg each | **NEW**; big added DPS |
| | Skim | grab `1/2/3/4/6 %` of dmg dealt as HP | sustain |
| | Air Wall | on **miss**, spawn a blocking air wall (dur scales) | **NEW**; ⏳ OnMiss; defense |
| **Spear** | Finisher | final combo hit releases a spear projectile (`dmg` scales) | **NEW**; added dmg |
| | Backstab | hits from behind deal `1.5/2/2.5/3/4×` | conditional but huge |
| | Missfire | fire a spear per **missed** hit | **NEW**; ⏳ OnMiss |
| **Bakshen** | Overcharge | on-hit, cooldown `−0.5/1/1.5/2/full` s | more Bakshen = massive dmg |

## Surge  (all surges)

| Buff | Effect & tier | Kind | Notes |
|---|---|---|---|
| **Prepared** | auto-trigger your surge at **stage start** (higher tiers: + a second charge banked) | Passive | retag "level"→"stage"; Aegis = defense |

## Special — per-special

| Special | Buff | Effect & tier | Notes |
|---|---|---|---|
| **Come Closer** | Wider Pull | magnetize `+1 / +1 / +2 / +2 / +3` extra enemies | utility/control |

## Traps  (shared sub-system — used by Dash Trap & Jump Trap)

One **NEW** trap entity, four flavours; the buff picks a flavour, tier scales its numbers:

| Trap | Effect & tier | Notes |
|---|---|---|
| **Snare** (stun) | 3 s field, stun `1/1.5/2/3/4` s | control — can pin a chasing Warden |
| **Sap** (weaken) | 3 s field, enemies take `+15/25/35/50/70 %` dmg | a damage amp — strong if it sticks to a Warden |
| **Pyre** (DoT) | 5 s field, `4/7/11/16/24` dmg/tick | mostly vs grunts |
| **Mine** (burst) | one-shot, explodes on contact for `20/35/55/80/120` | mostly vs grunts |

---

## NEW — Seal category  (not in `rewards-design.md`; brainstormed)

The Fissure verb is central enough to deserve its own buff line. Base Seal = **3 Ruh, 2 s channel**, so buffs
that erase that risk top out at **Epic**.

| Buff | Effect & tier | Notes |
|---|---|---|
| **Swift Seal** | seal time `2 → 1.5 / 1 / 0.6 / 0.3 / instant` s | **Epic = instant** |
| **Ward Seal** | immune while sealing. **S:** immune · **E:** immune + AoE-stun on start | **threshold, min Sensational** — erases the 2 s risk |
| **Cheap Seal** | seal costs `3 → 3 / 3 / 2 / 2 / 1` Ruh | economy-huge (Ruh is the spine); **min Hot** |
| **Seal Nova** | on seal-complete, AoE knock-back/stun `1/1.5/2/3/4` s | **NEW**; buys space as the Warden bursts out |
| **Warden's Toll** | on Warden kill: refund `1/1/2/2/3` Ruh + small heal | **NEW trigger** OnWardenKill |
| **Free Seal** | after a Warden kill, your **next seal costs 0 Ruh** | **NEW**; rewards seal-then-clear play |
| **Remote Seal** | seal from a distance (range scales); **E:** any range | **NEW**; convenience, high-tier |
| **Seal Surge** | on seal, `+30/50/70/90/120 %` dmg **and** speed for 4 s | timed damage; rewards aggressive sealing |

---

## Ideas parked (captured from chat — NOT designed or tiered yet)

- **Infighting / "Discord"** — a chaos buff that makes enemy AoEs **trigger on ANY nearby body, not just the
  player** — so a Nasen sleeper rages at whatever's in its zone and the eruption catches other grunts, turning
  a swarm on itself. Half the mechanic already exists: Nasen's AoE **damage** is friendly-fire today
  (`friendly_fire` on `EnemyKits.NASEN` → `Combat.HurtMask`). This buff is the **trigger** half — today
  `SleeperEnemy` only wakes/rages on the *player* in its `rage_zone`; the buff would let it (and any similar
  detect-then-attack enemy) count enemies as valid triggers while owned. Tier/persistence TBD; leans
  control/chaos utility. Needs a per-owner "enemies-provoke-enemies" flag the enemy AI reads.

## Tier philosophy (the shape I tuned to)

- **Common / Rare** — one modest stat, often situational: immunity windows, reach, higher jump, minor traps,
  +1 air jump. Safe to drop constantly; wiped each stage.
- **Hot / Sensational** — strong multipliers and rule-benders: big damage %, extra projectiles, +2 air jumps,
  cheaper Seals, invuln-while-sealing.
- **Epic** — run-definers: instant Seal, unlimited chain-dash, on-hit ramp at full ×2, remove-a-cooldown,
  Backstab ×4. These are why the Chest exists.

**Where the DPS lives** (for balancing the Warden-cap-vs-power curve — see `game-loop.md` law #2): the big
damage levers are Momentum (on-hit ramp), Slam Volley/Force, Ora Ora Barrage, Bakshen Overcharge, Backstab,
Instant Reset, then the timed-damage procs (Slam Wrath, Perfect-Dodge Fury, Seal Surge) and Sap. Everything
else is mobility / defense / sustain / control — it keeps you *alive* to deal that damage, which against
relentless teleporting Wardens matters just as much.

---

## Record shape (sketch — matches the house style; finalize at build time)

Enums in `enums/buffs/`, ids in `ids/BuffIds.cs`, the table in `configs/BuffCatalog.cs`:

```csharp
public enum BuffCategory { Dash, Jump, Slam, AttackGeneral, AttackPerAttack, Special, Surge, Seal }
public enum BuffKind     { Passive, Proc }
public enum BuffTrigger  { OnDash, OnGroundJump, OnAirJump, OnSlamTrigger, OnSlamLand,
                           OnHitDealt, OnHurt, OnSeal, OnWardenKill,
                           /* reserved */ OnPerfectDodge, OnMiss, OnAttackAnimationEnd, OnAttackTrigger, OnStageStart }

// Five per-tier magnitudes; nullable so a threshold buff can leave low tiers "unset".
public record TierScale(float? Common, float? Rare, float? Hot, float? Sensational, float? Epic);

public record BuffDef(
    string       Id,             // BuffIds.* const string (the config key)
    BuffCategory Category,
    BuffTrigger  Trigger,
    string       Family,         // stacking key: same family + permanent → higher tier replaces
    BuffKind     Kind,
    TierScale    Scale,          // per-tier numbers (null = tier not offered)
    bool         NeedsNewMechanic,
    string       AttackId = null // set only for per-attack buffs (offer-gating)
);
```

`Passive` buffs feed the existing `ModifyTuning` seam; `Proc` buffs fire on their `Trigger`. The catalog is
pure data — the *effects* land incrementally (stat buffs first; NEW-flagged mechanics per their own pass).
