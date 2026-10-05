using Godot;

namespace MyGame;

/// <summary>
/// Grant an invulnerability window when its bound trigger fires — the catalog's immunity buffs
/// (Dash Immunity, Jump Immunity, Slam Immunity, Hit Guard). One generic class, ROUTED by <see cref="Buff.Trigger"/>:
/// the granted instance's Trigger picks which hook actually grants. Uses <c>Player.grant_invuln</c>. Built by
/// <see cref="BuffCatalog"/>. (Perfect-Dodge / anim-end immunity land once those triggers emit.)
/// </summary>
public partial class InvulnBuff : Buff
{
    private readonly float _secs;  // the window, seconds

    public InvulnBuff(string id, Trigger trig, float secs)
    {
        Id = id;
        Trigger = trig;
        _secs = secs;
    }

    private void Grant(Player p) => p.grant_invuln(_secs);

    public override void OnDash(Player p) { if (Trigger == Trigger.OnDash) Grant(p); }
    public override void OnGroundJump(Player p) { if (Trigger == Trigger.OnGroundJump) Grant(p); }
    public override void OnSlamLand(Player p, float fallDistance, float fallSpeed) { if (Trigger == Trigger.OnSlamLand) Grant(p); }
    public override void OnHitDealt(Player p, float amount, Node target) { if (Trigger == Trigger.OnHitDealt) Grant(p); }
    public override void OnAnimEnd(Player p) { if (Trigger == Trigger.OnAnimEnd) Grant(p); }  // Follow-through
}
