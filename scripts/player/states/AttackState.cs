using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>
    /// The equipped attack in progress. Two kinds: a COMBO plays the attack's animation one segment at a time — each
    /// press (or holding the button) plays up to the next hit frame, pauses there for a recovery beat, and chains on;
    /// a FLURRY loops the animation for as long as the button is held. A special pressed during an attack is
    /// buffered and cast at the next break. The hits themselves are spawned by the <see cref="ParticleDirector"/> on
    /// the animation's frames, armed with <see cref="Player._activeHit"/>.
    /// </summary>
    private sealed class AttackState : PlayerState
    {
        public AttackState(Player player) : base(player) { }

        private int _step;              // the combo segment that plays next (0 = the first)
        private int _segEnd;            // the frame the current segment stops on
        private bool _segmentPlaying;
        private bool _flurry;
        private float _recoveryLeft;
        private bool _bufferedSpecial;
        /// <summary>Seconds left in which the next press continues the combo instead of restarting it.</summary>
        public float ComboWindow;

        public override StringName Animation => P._currentAttack != null ? P._currentAttack.Animation : "idle";

        public override void Enter() => SetVelX(0.0f);

        /// <summary>Forget the combo entirely (the character was re-applied).</summary>
        public void Reset()
        {
            _step = 0;
            ComboWindow = 0.0f;
            _segmentPlaying = false;
            _bufferedSpecial = false;
            _flurry = false;
        }

        /// <summary>The swing in progress ended by ANY route (release, special, surge, hurt, …). Done centrally, on
        /// every state change, because a stale flurry flag makes <see cref="Advance"/> swallow every later press.</summary>
        public void StopSwing()
        {
            _flurry = false;
            _segmentPlaying = false;
        }

        /// <summary>A special is starting: the combo starts over and nothing stays buffered.</summary>
        public void OnSpecialCast()
        {
            _step = 0;
            ComboWindow = 0.0f;
            _segmentPlaying = false;
            _bufferedSpecial = false;
        }

        public void DropBufferedSpecial() => _bufferedSpecial = false;

        /// <summary>Start the attack, or its next combo segment (a flurry already going just keeps going).</summary>
        public void Advance()
        {
            if (P._currentAttack is not { } attack)
                return;
            if (attack.IsFlurry)
            {
                if (!_flurry)
                    StartFlurry(attack);
                return;
            }
            var hits = Hits(attack);
            if (hits.Count == 0)
                return;
            _bufferedSpecial = false;

            if (ComboWindow <= 0.0f || _step >= hits.Count)
                _step = 0;
            int segStart = _step == 0 ? 0 : hits[_step - 1] + 1;
            _segEnd = hits[_step];
            _step += 1;
            P._activeHit = P.ResolveTuning(attack, _step - 1);

            ComboWindow = P.ComboResetTime;
            _segmentPlaying = true;
            P.Enter(this);
            Sprite.SpeedScale = 1.0f;
            Sprite.Play(attack.Animation);
            Sprite.SetFrameAndProgress(segStart, 0.0f);
        }

        private void StartFlurry(Action attack)
        {
            _bufferedSpecial = false;
            _flurry = true;
            P._activeHit = P.ResolveTuning(attack, 0);
            P.Enter(this);
            Sprite.SpeedScale = 1.0f;
            Sprite.Play(attack.Animation);
        }

        /// <summary>The frames that end each combo segment: the authored hit frames, or every frame when none are authored.</summary>
        private IReadOnlyList<int> Hits(Action attack) => AnimMeta.HitFramesOrAll(Sprite.SpriteFrames, attack.Animation);

        public override void Tick(float delta)
        {
            bool lunging = P._activeHit.Lunge.HasValue && _recoveryLeft > 0.0f;
            if (lunging)
            {
                SetVelY(0.0f);
            }
            else
            {
                Brake(delta);
                if (!OnFloor)
                    AddVelY(P._gravity * delta);
            }

            if (Input.IsActionJustPressed("special") && P._special.Cooldown <= 0.0f)
                _bufferedSpecial = true; // only a READY special — one on cooldown would stall the attack until it recharged

            if (_flurry)
            {
                if (_bufferedSpecial)
                    P._special.Start();
                else if (!Input.IsActionPressed("attack"))
                {
                    P.NotifyAttackAnimEnd();
                    P.EnterFree(FreeMode.Idle);
                }
                return;
            }

            if (_segmentPlaying)
            {
                if (Sprite.Frame >= _segEnd)
                {
                    Sprite.SetFrameAndProgress(_segEnd, 0.0f);
                    Sprite.Pause();
                    _segmentPlaying = false;
                    _recoveryLeft = Mathf.Max(P.AttackRecovery, P._activeHit.Hold ?? 0.0f);
                    ComboWindow = P.ComboResetTime;
                    if (_bufferedSpecial)
                        P._special.Start();
                }
                return;
            }

            if (_bufferedSpecial)
            {
                P._special.Start();
                return;
            }
            // A press chains the next hit; HOLDING chains it too — except after the combo's last hit, which keeps its
            // recovery beat before holding loops the combo back to its first hit (via idle).
            if (Input.IsActionJustPressed("attack") || (AttackHeld() && P._currentAttack is { } held && _step < Hits(held).Count))
            {
                Advance();
                return;
            }
            ComboWindow = Mathf.Max(ComboWindow - delta, 0.0f);
            _recoveryLeft -= delta;
            if (_recoveryLeft <= 0.0f)
            {
                if (P._activeHit.Lunge.HasValue)
                    SetVelX(0.0f);
                P.NotifyAttackAnimEnd();
                P.EnterFree(FreeMode.Idle);
            }
        }
    }
}
