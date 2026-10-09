using Godot;

namespace MyGame;

/// <summary>
/// The timed self-buff a SURGE applies — the "surge" component of a SURGE Action. Triggered on the `surge`
/// button, gated by RUH (each use spends `cost`). The defaults below are a surge that does nothing; each surge
/// Action sets only what it uses (see <see cref="ActionsKhalid"/>).
/// </summary>
public partial class SurgeSpec : RefCounted
{
    public float Cost = 100.0f;
    public float Duration = 5.0f;
    public bool Invuln = false;
    public float DamageMult = 1.0f;
    public float DamageTakenMult = 1.0f;
    public float SpeedMult = 1.0f;
    public bool Channel = false;       // a movement-locking sleep/heal channel (Nem)
    public float HealFrac = 0.0f;
    public string Trigger = "cast";    // "cast" (immediate) or "hit" (armed reactive — Wara)
    public float StunRadius = 0.0f;
    public float StunTime = 0.0f;
    public string Aura = "";           // orbit aura VFX scene shown while active
    public string Burst = "";          // Wara: the AoE burst played once WHEN triggered
}
