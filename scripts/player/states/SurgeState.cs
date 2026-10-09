using Godot;

namespace MyGame;

public partial class Player
{
    /// <summary>
    /// SURGES — the powers that spend Ruh. This holds the whole of it: firing one (the surge button, or free via the
    /// Prepared perk), the effect while it lasts — invulnerability (Aegis), a damage multiplier (Jnoon), a speed
    /// multiplier (Asra), an armed counter that waits for an enemy's hit (Wara) — its aura, and its end. As a STATE it
    /// is the cast animation, and for a CHANNELLED surge (Nem) the sleep that follows: he lies down on the animation's
    /// second-to-last frame for the surge's duration, healing a block, and any hit wakes him.
    /// </summary>
    private sealed class SurgeState : PlayerState
    {
        private const float HealHalfBlocks = 2.0f;   // a channelled surge restores one block
        private const float AuraFade = 0.3f;
        private const float BurstLife = 1.5f;

        public SurgeState(Player player) : base(player) { }

        private float _left;            // seconds of a timed surge (or of the sleep) still to go
        private bool _invulnerable;
        private bool _asleep;
        private float _healTarget, _healRate;
        private int _sleepFrame;
        private float _sleepTime;
        private SurgeSpec? _armed;      // the spec waiting for an enemy's hit (Wara)
        private Node2D? _aura;

        public float DamageMult { get; private set; } = 1.0f;
        public float SpeedMult { get; private set; } = 1.0f;
        /// <summary>A channelled surge is going (cast or asleep).</summary>
        public bool Channelling { get; private set; }
        /// <summary>An armed surge is waiting for an enemy to hit him.</summary>
        public bool Armed => _armed != null;
        /// <summary>He cannot be hurt right now because of a surge.</summary>
        public bool Invulnerable => _invulnerable && _left > 0.0f;

        public override StringName Animation => P._currentSurge != null ? P._currentSurge.Animation : "idle";

        public override void Enter()
        {
            SetVelX(0.0f);
            if (P._currentSurge != null && P.HasAnim(P._currentSurge.Animation))
            {
                Sprite.Play(P._currentSurge.Animation);
                Sprite.SetFrameAndProgress(0, 0.0f);
            }
            else
            {
                P.SetFree(FreeMode.Idle);
            }
        }

        /// <summary>The surge button: fire the equipped surge if one is ready and he has the Ruh for it.</summary>
        public void TryFireFromInput()
        {
            if (!Input.IsActionJustPressed("surge") || Ready() is not var (surge, spec) || P.Ruh < spec.Cost)
                return;
            P.Ruh -= spec.Cost;
            Fire(surge, spec);
        }

        /// <summary>Fire the equipped surge WITHOUT spending Ruh. No-op if one is already going.</summary>
        public void FireFree()
        {
            if (Ready() is var (surge, spec))
                Fire(surge, spec);
        }

        /// <summary>The equipped surge and its spec, if one can fire now (alive, none channelling or armed); else null.</summary>
        private (Action Surge, SurgeSpec Spec)? Ready() =>
            !P._dead && !Channelling && !Armed && P._currentSurge is { Surge: { } spec } surge ? (surge, spec) : null;

        private void Fire(Action surge, SurgeSpec s)
        {
            Begin(surge, s);
            P.Flash(Sprite);
            P._sfx.Play(surge.Animation.ToString());
            if (P._current != P._spawn && P.HasAnim(surge.Animation))
                P.Enter(this);
        }

        private void Begin(Action surge, SurgeSpec s)
        {
            End();
            _invulnerable = s.Invuln;
            DamageMult = s.DamageMult;
            SpeedMult = s.SpeedMult;
            Channelling = s.Channel;
            if (s.Trigger == "hit")
            {
                _armed = s;
                _left = 0.0f;
            }
            else if (Channelling)
            {
                _asleep = false;
                _left = 0.0f;
                // Slot health: a healing surge (Nem) restores ONE block over its channel.
                _healTarget = Mathf.Min(P.Health + HealHalfBlocks, P.MaxHealth);
                _healRate = (_healTarget - P.Health) / Mathf.Max(s.Duration, 0.01f);
                var anim = surge.Animation;
                int fcount = (Sprite.SpriteFrames != null && Sprite.SpriteFrames.HasAnimation(anim))
                    ? Sprite.SpriteFrames.GetFrameCount(anim) : 0;
                _sleepFrame = Mathf.Max(fcount - 2, 0);
                _sleepTime = s.Duration;
            }
            else
            {
                _left = s.Duration + P.SpecialInvulnBonus;
            }
            string aura = s.Aura;
            if (aura != "" && ResourceLoader.Exists(aura))
            {
                var scene = GD.Load<PackedScene>(aura);
                _aura = scene?.Instantiate() as Node2D;
                if (_aura != null)
                {
                    if (_aura is OrbitAura orbit)
                        orbit.MoonColor = VfxPalette.Recolor(orbit.MoonColor);
                    VfxPalette.RecolorTree(_aura);
                    P.AddChild(_aura);
                }
            }
        }

        /// <summary>Every tick, whatever the state: a timed surge runs out.</summary>
        public void Update(float delta)
        {
            if (_left <= 0.0f)
                return;
            _left -= delta;
            if (_left <= 0.0f)
                End();
        }

        /// <summary>End whatever surge is going: its effects, its channel, its aura.</summary>
        public void End()
        {
            _left = 0.0f;
            _invulnerable = false;
            DamageMult = 1.0f;
            SpeedMult = 1.0f;
            _armed = null;
            if (Channelling)
            {
                Channelling = false;
                _asleep = false;
                P._sprite?.Play();
            }
            if (IsInstanceValid(_aura))
            {
                var aura = _aura!;
                var tw = aura.CreateTween();
                tw.TweenProperty(aura, "modulate:a", 0.0, AuraFade);
                tw.TweenCallback(Callable.From(aura.QueueFree));
            }
            _aura = null;
        }

        /// <summary>An enemy hit him while a surge was armed (Wara): the hit is negated, everything nearby is stunned.</summary>
        public void TriggerArmed()
        {
            var s = _armed;
            if (s == null)
            {
                End();
                return;
            }
            P.StunNearby(s.StunRadius, s.StunTime);
            string burst = s.Burst;
            if (burst != "" && ResourceLoader.Exists(burst))
            {
                var scene = GD.Load<PackedScene>(burst);
                var b = scene?.Instantiate() as Node2D;
                if (b != null)
                {
                    VfxPalette.RecolorTree(b);
                    P.AddChild(b);
                    P.GetTree().CreateTimer(BurstLife).Timeout += b.QueueFree;
                }
            }
            P._sfx.Play("surge_wara_trigger");
            P.Flash(Sprite);
            End();
        }

        public override void Tick(float delta)
        {
            Brake(delta);
            if (!OnFloor)
                AddVelY(P._gravity * delta);
            if (!Channelling)
                return;
            if (!_asleep)
            {
                if (Sprite.Frame >= _sleepFrame)
                {
                    _asleep = true;
                    Sprite.SetFrameAndProgress(_sleepFrame, 0.0f);
                    Sprite.Pause();
                    _left = _sleepTime;
                }
            }
            else
            {
                P.Health = Mathf.Min(P.Health + _healRate * delta, _healTarget);
                _left -= delta;
                if (_left <= 0.0f)
                {
                    End();
                    P.EnterFree(FreeMode.Idle);
                }
            }
        }
    }
}
