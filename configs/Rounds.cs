namespace MyGame;

/// <summary>
/// Tuning for the endless ROUND loop (see <c>docs/game-loop.md</c>) — PURE DATA; the reader is <see cref="RunManager"/>.
/// Round <c>r</c> (1-based) sends a hidden QUOTA of enemies, trickled in one per <c>SpawnInterval(r)</c> seconds while
/// fewer than the concurrent CAP are alive; once the quota has spawned, spawning stops, and the round clears when all
/// of them are dead — and round r+1 starts right then. There is no break between rounds.
///
/// <para>The curve starts SLOW (round 1: 6 enemies, 3 at a time, one every 1.5 s) and keeps climbing every round.</para>
/// </summary>
public static class Rounds
{
    // Quota Q(r) = QuotaBase + QuotaLinear·r + QuotaQuad·r² (rounded) — ~quadratic like CoD's count curve.
    // r1 = 6, r2 = 10, r3 = 15, r5 = 26, r10 = 63, r20 = 183.
    public const float QuotaBase = 3.0f;
    public const float QuotaLinear = 3.0f;
    public const float QuotaQuad = 0.3f;

    // Concurrent cap C(r) = CapBase + (r-1) / CapGrowthRounds, never above CapMax — one more alive at once every round:
    // r1 = 3, r5 = 7, r10 = 12, r20 = 22.
    public const int CapBase = 3;
    public const int CapGrowthRounds = 1;
    public const int CapMax = 32;

    // Spawn interval = max(IntervalMin, IntervalBase · IntervalDecay^(r-1)) seconds — refills come faster each round:
    // r1 = 1.5 s, r5 = 1.17 s, r10 = 0.86 s, r20 = 0.47 s.
    public const float IntervalBase = 1.5f;
    public const float IntervalDecay = 0.94f;
    public const float IntervalMin = 0.35f;

    // A kit's own `spawn_cap` (per-type concurrency) grows +1 every KitCapGrowthRounds rounds.
    public const int KitCapGrowthRounds = 5;

    public const int ShowLeftAt = 5;          // the HUD reveals "n LEFT" once this many (or fewer) quota enemies remain

    // --- where enemies come from ------------------------------------------------------------------------------------
    // Regular enemies spawn at the layout's EnemySpawns SPOTS and patrol there until they notice the player. One enemy
    // per spot, spread out: each new one takes the free spot farthest from the spots already held — never one closer
    // to the player than SpawnMinDistance, so nothing pops in on top of him. All spots held = wait for a free one.
    public const float SpawnMinDistance = 320.0f;
    // NEAR-PLAYER spawns join from NearSpawnFromRound: that share of grunts appears on the player's floor, behind him,
    // NearSpawnMin..NearSpawnMax px away, instead of at a spot (stationary kits like Nasen always use spots). Share =
    // min(NearShareMax, NearShareBase + NearShareStep·(r - NearSpawnFromRound)): r10 = 20%, r12 = 40%, r15+ = 70%.
    // From then on, all spots held also falls back to a near spawn instead of waiting.
    public const int NearSpawnFromRound = 10;
    public const float NearShareBase = 0.2f;
    public const float NearShareStep = 0.1f;
    public const float NearShareMax = 0.7f;
    public const float NearSpawnMin = 100.0f;
    public const float NearSpawnMax = 240.0f;
    // STRAGGLERS: once the round has fully spawned and only this many quota enemies are left, they all HUNT the player
    // (so a round can't stall on an enemy he can't find or reach).
    public const int StragglerCount = 3;
    public const float StragglerSpeedMult = 1.6f; // ...and chase this much faster than their normal move speed (walk anim too)

    // --- stand-still pressure: KAMIKAZES (Ein) ----------------------------------------------------------------------
    // From KamikazeFromRound, a player who stays within StillRadius px for StillTime seconds gets a kamikaze spawned
    // near him — KamikazeDistance px to one side, KamikazeHeight up (room to react) — then another every
    // KamikazeInterval(r) s while he stays put, at most KamikazeMax(r) alive. They drop nothing and aren't part of the
    // round. The clock pauses while he's in a channelled surge (Nem's sleep).
    // Interval = max(KamikazeIntervalMin, KamikazeIntervalBase · KamikazeIntervalDecay^(r-5)): r5 = 2 s, r10 = 1.55 s,
    // r20 = 0.93 s. Max alive = min(KamikazeMaxCap, KamikazeMaxBase + (r-5) / KamikazeMaxGrowthRounds): r5 = 5, r10 = 6,
    // r20 = 8.
    public const int KamikazeFromRound = 5;
    public const float StillTime = 2.0f;
    public const float StillRadius = 50.0f;
    public const float KamikazeIntervalBase = 2.0f;
    public const float KamikazeIntervalDecay = 0.95f;
    public const float KamikazeIntervalMin = 0.75f;
    public const int KamikazeMaxBase = 5;
    public const int KamikazeMaxGrowthRounds = 5;
    public const int KamikazeMaxCap = 8;
    public const float KamikazeDistance = 220.0f;
    public const float KamikazeHeight = 110.0f;

    // --- the EDGE enemy: VENTILATOR -----------------------------------------------------------------------------------
    // From VentilatorFromRound, a player who stays within EdgeZone px of the arena's left or right end for EdgeDwell s
    // gets a Ventilator on his floor, on the INLAND side, VentilatorSpawnMin..Max px away — its wind blows him outward.
    // At most VentilatorMax alive; after one dies the next waits VentilatorCooldown s. Not part of the round (optional),
    // but it drops Lira + figs like any enemy.
    public const int VentilatorFromRound = 3;
    public const float EdgeZone = 300.0f;
    public const float EdgeDwell = 1.0f;
    public const int VentilatorMax = 1;
    public const float VentilatorCooldown = 10.0f;
    public const float VentilatorSpawnMin = 140.0f;
    public const float VentilatorSpawnMax = 260.0f;
}
