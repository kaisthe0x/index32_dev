namespace MyGame;

/// <summary>
/// Tuning for the endless ROUND loop (see <c>docs/game-loop.md</c>) — PURE DATA; the reader is <see cref="RunManager"/>.
/// Round <c>r</c> (1-based) sends a hidden QUOTA of enemies, trickled in one per <c>SpawnInterval(r)</c> seconds while
/// fewer than the concurrent CAP are alive; once the quota has spawned, spawning stops, and the round clears when all
/// of them are dead. A BREATHER follows, then round r+1.
/// </summary>
public static class Rounds
{
    // Quota Q(r) = QuotaBase + QuotaLinear·r + QuotaQuad·r² (rounded) — ~quadratic like CoD's count curve.
    // r1 = 11, r5 = 32, r10 = 73, r20 = 208.
    public const float QuotaBase = 8.0f;
    public const float QuotaLinear = 3.0f;
    public const float QuotaQuad = 0.35f;

    // Concurrent cap C(r) = CapBase + (r-1) / CapGrowthRounds, never above CapMax — the arena gets "stickier" slowly.
    public const int CapBase = 6;
    public const int CapGrowthRounds = 2;
    public const int CapMax = 24;

    // Spawn interval = max(IntervalMin, IntervalBase · IntervalDecay^(r-1)) seconds — refills come faster each round.
    public const float IntervalBase = 0.9f;
    public const float IntervalDecay = 0.95f;
    public const float IntervalMin = 0.35f;

    // A kit's own `spawn_cap` (per-type concurrency) grows +1 every KitCapGrowthRounds rounds.
    public const int KitCapGrowthRounds = 5;

    public const float BreatherTime = 8.0f;   // seconds between a clear and the next round
    public const int ShowLeftAt = 5;          // the HUD reveals "n LEFT" once this many (or fewer) quota enemies remain
    public const int CountdownSfxFrom = 4;    // the breather counter plays round_countdown at each of this … 1 (SfxWorld)
}
