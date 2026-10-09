using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>The spawn animation at the start of a run: he cannot act or be hit until it has played.</summary>
    private sealed class SpawnState : PlayerState
    {
        public SpawnState(Player player) : base(player) { }

        public override StringName Animation => "spawn";

        public override void Enter() => SetVelX(0.0f);

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
