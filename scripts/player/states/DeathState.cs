using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>
    /// Dead, playing the death animation. It starts FROZEN on its first frame — the run's death sequence holds the
    /// beat, then calls <see cref="Release"/> — and once it has played out he is hidden and <see cref="Finished"/>.
    /// (A fall out of the arena never enters this state: there is no animation, he just keeps falling.)
    /// </summary>
    private sealed class DeathState : PlayerState
    {
        public DeathState(Player player) : base(player) { }

        private bool _frozen;
        /// <summary>The death has played out (or there was nothing to play).</summary>
        public bool Finished;

        public override StringName Animation => "death";

        public override void Enter()
        {
            SetVelX(0.0f);
            _frozen = true;
            Sprite.Play("death");
            Sprite.SetFrameAndProgress(0, 0.0f);
            Sprite.Pause();
        }

        /// <summary>Let the held first frame go.</summary>
        public void Release()
        {
            if (!_frozen)
                return;
            _frozen = false;
            P._sprite?.Play();
        }

        public override void Tick(float delta)
        {
            Brake(delta);
            if (OnFloor)
                SetVelY(0.0f);
            else
                AddVelY(P._gravity * delta);
        }
    }
}
