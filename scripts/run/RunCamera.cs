using Godot;

namespace MyGame;

/// <summary>
/// The run's camera work, around the arena scene's <c>Camera2D</c>: the follow spring during play, the eased drift
/// used while Khalid spawns or dies, and the zoom moves between the three framings. Owned by <see cref="RunManager"/>.
/// If the scene has no camera every call does nothing.
///
/// <para>Follow is a CRITICALLY DAMPED SPRING (SmoothDamp) toward Khalid — it has velocity, so after a sudden jump (a
/// blink dash) it accelerates smoothly, glides and decelerates with no overshoot, and a stop eases out instead of
/// "lagging into place". <see cref="SmoothTime"/> ≈ time to catch up; lower = tighter (trails a running Khalid by
/// ~13 px at 0.055 s). Fast VERTICAL motion (launch orbs) tightens toward <see cref="SmoothTimeFast"/> so he never
/// leaves the frame.</para>
/// </summary>
public sealed class RunCamera
{
    private const float SmoothTime = 0.055f;
    private const float SmoothTimeFast = 0.015f;
    private const float TightenStart = 600.0f;   // vertical speed (px/s) where the follow starts tightening
    private const float TightenFull = 1200.0f;   // ... and where it is fully tight
    private const float EaseWeight = 0.12f;      // per-tick lerp of the spawn / death drift
    private static readonly Vector2 FollowOffset = new(0, -30);  // play: look a little above his feet
    private static readonly Vector2 EaseOffset = new(0, -18);    // spawn / death: closer on his body
    private static readonly Vector2 ZoomPlay = new(.5f, .5f);
    private static readonly Vector2 ZoomDeathLevel = new(3.0f, 3.0f);
    private static readonly Vector2 ZoomSpawnLevel = new(2, 2);

    private readonly Camera2D? _camera;
    private Vector2 _velocity = Vector2.Zero; // the follow spring's velocity (zeroed whenever the camera is eased instead)
    private Tween? _zoomTween;

    public RunCamera(Camera2D? camera) => _camera = camera;

    /// <summary>The camera node — the death overlay rides on it so it always covers the screen. Null if there is none.</summary>
    public Camera2D? Node => _camera;

    /// <summary>Jump straight to framing a player standing at <paramref name="playerPos"/> (a new arena).</summary>
    public void SnapTo(Vector2 playerPos)
    {
        if (_camera != null)
            Nodes.PlaceAt(_camera, playerPos + FollowOffset);
    }

    /// <summary>One tick of the follow spring during play.</summary>
    public void Follow(Player player, float delta)
    {
        if (_camera == null)
            return;
        Vector2 target = player.GlobalPosition + FollowOffset;
        float vy = Mathf.Abs(player.Velocity.Y);
        float t = Mathf.Clamp((vy - TightenStart) / (TightenFull - TightenStart), 0.0f, 1.0f);
        float smoothTime = Mathf.Lerp(SmoothTime, SmoothTimeFast, t);
        _camera.GlobalPosition = SmoothDamp(_camera.GlobalPosition, target, ref _velocity, smoothTime, delta);
    }

    /// <summary>One tick of the slow drift onto Khalid used while he spawns or dies (the spring is at rest meanwhile).</summary>
    public void EaseOnto(Player player)
    {
        _velocity = Vector2.Zero;
        if (_camera != null)
            _camera.GlobalPosition = _camera.GlobalPosition.Lerp(player.GlobalPosition + EaseOffset, EaseWeight);
    }

    public void ZoomToPlay() => ZoomTo(ZoomPlay, 0.4f);
    public void ZoomToSpawn() => ZoomTo(ZoomSpawnLevel, 0.35f);
    public void ZoomToDeath() => ZoomTo(ZoomDeathLevel, 0.45f);

    private void ZoomTo(Vector2 zoom, float duration)
    {
        if (_camera == null)
            return;
        if (_zoomTween != null && _zoomTween.IsValid())
            _zoomTween.Kill();
        _zoomTween = _camera.CreateTween();
        _zoomTween.TweenProperty(_camera, "zoom", zoom, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Critically damped spring step (Game Programming Gems 4 §1.10 — the classic SmoothDamp): moves
    /// <paramref name="from"/> toward <paramref name="to"/> with continuous velocity, no overshoot.</summary>
    private static Vector2 SmoothDamp(Vector2 from, Vector2 to, ref Vector2 vel, float smoothTime, float delta)
    {
        float omega = 2.0f / Mathf.Max(smoothTime, 0.0001f);
        float x = omega * delta;
        float exp = 1.0f / (1.0f + x + 0.48f * x * x + 0.235f * x * x * x);
        Vector2 change = from - to;
        Vector2 temp = (vel + omega * change) * delta;
        vel = (vel - omega * temp) * exp;
        return to + (change + temp) * exp;
    }
}
