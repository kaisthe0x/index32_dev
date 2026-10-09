using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>
    /// One state of the player's state machine: what he does each physics tick while in it, what happens on entering
    /// it, the animation it shows, and the data only it needs. <see cref="Player"/> owns the states (one instance of
    /// each, for his whole life), which one is current, and everything they share — his body, stats, loadout,
    /// passives. The states are nested in Player so they can work on that shared data directly; each lives in its own
    /// file under <c>scripts/player/states/</c>.
    ///
    /// <para>Two ways to change state: <see cref="Player.Enter"/> runs the new state's <see cref="Enter"/> (its
    /// one-off side effects) and resets the sprite; <see cref="Player.SetFree"/> only relabels free movement
    /// (idle / run / jump / fall) with no side effect.</para>
    /// </summary>
    private abstract class PlayerState
    {
        protected readonly Player P;

        protected PlayerState(Player player) => P = player;

        /// <summary>The animation this state shows.</summary>
        public abstract StringName Animation { get; }

        /// <summary>The state was just entered.</summary>
        public virtual void Enter() { }

        /// <summary>One physics tick in this state.</summary>
        public abstract void Tick(float delta);

        protected AnimatedSprite2D Sprite => P._sprite;
        protected bool OnFloor => P.IsOnFloor();
        protected Vector2 Velocity { get => P.Velocity; set => P.Velocity = value; }
        protected void SetVelX(float x) => P.SetVelX(x);
        protected void SetVelY(float y) => P.SetVelY(y);
        protected void AddVelY(float dy) => P.AddVelY(dy);

        /// <summary>The attack button is down — every attack keeps going while it's held (flurries loop, combos chain).</summary>
        protected static bool AttackHeld() => Input.IsActionPressed("attack");

        /// <summary>Slow his horizontal speed toward a stop with ground friction.</summary>
        protected void Brake(float delta) => SetVelX(Mathf.MoveToward(Velocity.X, 0.0f, P._friction * delta));
    }
}
