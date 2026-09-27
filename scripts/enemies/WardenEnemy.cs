using Godot;

namespace MyGame;

/// <summary>
/// Warden archetype: the elite of the Warden rounds (every 10th round; see <c>docs/game-loop.md</c>). Bigger and
/// tankier than a grunt, and a RELENTLESS TELEPORTING pursuer — if the player stays beyond
/// <see cref="teleport_range"/> for <see cref="teleport_delay"/>, he telegraphs then warps in near them (landing
/// just outside his lunge range, so a dash can dodge). His attack is a LUNGE (the base <c>close_lunge</c> impulse
/// on a <c>close_type = lunge</c> attack). He plays a cinematic SPAWN on creation (invulnerable during it), and on
/// death his animation plays and the CORPSE PERSISTS in the world (the base would free it). Warden deaths use their
/// own SFX. Kroj is the first Warden (an <see cref="EnemyKits"/> entry pointing at this via <c>scene</c>).
/// Subclass of <see cref="Enemy"/>, like <c>DiverEnemy</c>/<c>SleeperEnemy</c>.
/// </summary>
[GlobalClass]
public partial class WardenEnemy : Enemy
{
    [ExportGroup("Warden")]
    [Export] public float teleport_range { get; set; } = 360.0f;       // player must be beyond this to trigger a warp
    [Export] public float teleport_delay { get; set; } = 1.6f;         // ...for this long, before the warp begins
    [Export] public float telegraph_time { get; set; } = 0.45f;        // fair-warp: a beat where the player can dash away
    [Export] public float teleport_land_offset { get; set; } = 96.0f;  // lands this far from the player (outside lunge range)

    protected override string FramesPath => "res://resources/wardens/{0}.tres";

    private bool _spawning;
    private float _farTime;
    private bool _warping;
    private float _warpLeft;
    private Node? _telegraph;

    public override void _Ready()
    {
        base._Ready();
        if (Sprite.SpriteFrames.HasAnimation("spawn"))
        {
            _spawning = true;
            Hurt.SetDeferred(Area2D.PropertyName.Monitorable, false); // invulnerable during the cinematic entrance
            SetState(EState.Patrol);  // a non-Idle state so KeepIdleLive can't clobber the spawn anim
            Play("spawn");
            if (MakeVfx("spawn") is Node2D burst)
                AddChild(burst);
            SfxPlayAt("kroj.spawn", GlobalPosition);
        }
    }

    /// <summary>Warden death = the base death (state + kroj.death SFX + death anim) PLUS a burst; the corpse then
    /// persists (see <see cref="OnAnimFinished"/>).</summary>
    protected override void Die()
    {
        base.Die();
        if (MakeVfx("death_burst") is Node2D b)
        {
            GetParent().AddChild(b);   // into the level so it outlives us
            b.GlobalPosition = GlobalPosition;
        }
    }

    protected override void Act(float delta)
    {
        if (_spawning)
        {
            Velocity = new Vector2(0.0f, Velocity.Y);
            return;
        }
        if (TeleportPursuit(delta))
            return;  // warping this frame — hold, don't run normal AI
        base.Act(delta);
    }

    /// <summary>Relentless warp. Once the player is beyond <see cref="teleport_range"/> for
    /// <see cref="teleport_delay"/>, telegraph (a beat to dash) then blink in near them, just outside lunge range.
    /// Returns true while warping (the caller must not run the normal chase/attack AI).</summary>
    private bool TeleportPursuit(float delta)
    {
        var player = Player();
        if (player == null)
        {
            _farTime = 0.0f;
            CancelWarp();
            return false;
        }
        if (_warping)
        {
            _warpLeft -= delta;
            Velocity = new Vector2(0.0f, Velocity.Y);
            Face(Mathf.Sign(player.GlobalPosition.X - GlobalPosition.X));
            if (_warpLeft <= 0.0f)
                DoWarp(player);
            return true;
        }
        if (GlobalPosition.DistanceTo(player.GlobalPosition) > teleport_range)
        {
            _farTime += delta;
            if (_farTime >= teleport_delay)
                BeginWarp();
        }
        else
        {
            _farTime = 0.0f;
        }
        return false;
    }

    private void BeginWarp()
    {
        _warping = true;
        _warpLeft = telegraph_time;
        _telegraph = MakeVfx("warp");   // danger telegraph (placeholder) — worn on the warden while he charges the warp
        if (_telegraph is Node2D n)
            AddChild(n);
        SfxPlayAt("kroj.warp", GlobalPosition);
    }

    private void DoWarp(Node2D player)
    {
        // Land on the near side of the player, just outside lunge range, so a well-timed dash dodges the follow-up.
        int side = GlobalPosition.X <= player.GlobalPosition.X ? -1 : 1;
        GlobalPosition = new Vector2(player.GlobalPosition.X + side * teleport_land_offset, player.GlobalPosition.Y);
        Face(-side);
        var arrive = MakeVfx("warp");   // arrival burst (placeholder), left in the world
        if (arrive is Node2D a)
        {
            GetParent().AddChild(a);
            a.GlobalPosition = GlobalPosition;
        }
        SfxPlayAt("kroj.warp", GlobalPosition);
        CancelWarp();
        _farTime = 0.0f;
    }

    private void CancelWarp()
    {
        _warping = false;
        _warpLeft = 0.0f;
        if (_telegraph is Node2D t && IsInstanceValid(t))
            t.QueueFree();
        _telegraph = null;
    }

    protected override void OnAnimFinished()
    {
        if (State == EState.Dead)
        {
            Sprite.Pause();   // corpse PERSISTS on the last death frame (base would QueueFree here)
            return;
        }
        if (_spawning)
        {
            _spawning = false;
            Hurt.SetDeferred(Area2D.PropertyName.Monitorable, true);
            SetState(EState.Idle);   // entrance done — hand to normal chase/lunge AI
            return;
        }
        base.OnAnimFinished();
    }

    /// <summary>Wardens die louder than grunts — their own death cue.</summary>
    protected override string DeathSfxKey() => "kroj.death";
}
