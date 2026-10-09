using Godot;

namespace MyGame;

/// <summary>
/// The timed self-buff a SURGE applies — the "surge" component of a SURGE Action. Triggered on the `surge`
/// button, gated by RUH (each use spends `cost`). The defaults below are a surge that does nothing; each surge
/// Action sets only what it uses (see <see cref="ActionsKhalid"/>).
/// </summary>
public partial class SurgeSpec : RefCounted
{
    public float cost = 100.0f;
    public float duration = 5.0f;
    public bool invuln = false;
    public float damage_mult = 1.0f;
    public float damage_taken_mult = 1.0f;
    public float speed_mult = 1.0f;
    public bool channel = false;       // a movement-locking sleep/heal channel (Nem)
    public float heal_frac = 0.0f;
    public string trigger = "cast";    // "cast" (immediate) or "hit" (armed reactive — Wara)
    public float stun_radius = 0.0f;
    public float stun_time = 0.0f;
    public string aura = "";           // orbit aura VFX scene shown while active
    public string burst = "";          // Wara: the AoE burst played once WHEN triggered
}
