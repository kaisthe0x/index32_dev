using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>
    /// The equipped special being cast. Specials cost nothing but each has its own cooldown (<see cref="Cooldown"/>).
    /// A special tagged <c>held</c> (Redere Shield) stays up on its last frame for as long as the button is held, and
    /// its cooldown only starts on release; one tagged <c>shield</c> blocks hits from the front, and PARRIES them
    /// during its first <see cref="Player.ParryWindow"/> seconds.
    /// </summary>
    private sealed class SpecialState : PlayerState
    {
        public SpecialState(Player player) : base(player) { }

        /// <summary>Seconds until the special can be cast again.</summary>
        public float Cooldown;
        /// <summary>Seconds of the parry window still open.</summary>
        public float ParryLeft;

        public override StringName Animation => P._currentSpecial != null ? P._currentSpecial.Animation : "idle";

        /// <summary>A "held" special is up right now — its cooldown waits until it's released.</summary>
        public bool Holding => P._current == this && P._currentSpecial != null && P._currentSpecial.HasTag("held");

        /// <summary>He is behind a shield special right now.</summary>
        public bool Shielding => P._current == this && P._currentSpecial != null && P._currentSpecial.HasTag("shield");

        /// <summary>Cast the equipped special, if it is off cooldown.</summary>
        public void Start()
        {
            if (Cooldown > 0.0f || P._currentSpecial is not { } special)
                return;
            Cooldown = special.Cooldown; // every special has its own cooldown
            bool isShield = special.HasTag("shield");
            foreach (var p in P._passives)
                p.OnSpecialCast(P, special);
            if (isShield)
                ParryLeft = P.ParryWindow;
            P._attack.OnSpecialCast();
            P._activeHit = P.ResolveTuning(special, 0);
            P._activeHit.FromSpecial = true;
            P.Enter(this);
            if (P.HasAnim(special.Animation))
            {
                Sprite.Play(special.Animation);
                Sprite.SetFrameAndProgress(0, 0.0f);
            }
        }

        /// <summary>The frame the special's hit comes out on: its first authored hit frame, or the animation's middle.</summary>
        public int StrikeFrame()
        {
            if (P._currentSpecial is not { } special)
                return 0;
            var hits = AnimMeta.HitFrames(Sprite.SpriteFrames, special.Animation);
            if (hits.Count > 0)
                return hits[0];
            return Sprite.SpriteFrames.GetFrameCount(special.Animation) / 2;
        }

        public override void Tick(float delta)
        {
            // A special that carries Lunge (e.g. Zahluq) dashes through: hold vertical and let the lunge impulse ride
            // instead of friction-damping it (mirrors the attack). Super-armor is honoured by the player, for any state.
            if (P._activeHit.Lunge.HasValue)
            {
                SetVelY(0.0f);
            }
            else
            {
                Brake(delta);
                if (!OnFloor)
                    AddVelY(P._gravity * delta);
            }
            if (P._currentSpecial != null && P._currentSpecial.HasTag("held"))
            {
                int last = Sprite.SpriteFrames.GetFrameCount(P._currentSpecial.Animation) - 1;
                if (Sprite.Frame >= last)
                {
                    if (Input.IsActionPressed("special"))
                    {
                        if (Sprite.IsPlaying())
                            Sprite.Pause();
                    }
                    else
                    {
                        P._activeHit = new SegmentData();
                        P.EnterFree(FreeMode.Idle);
                    }
                }
            }
        }
    }
}
