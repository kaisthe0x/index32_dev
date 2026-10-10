using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Keeps the arena stocked with <see cref="Bird"/>s (tuning in <see cref="Birds"/>): up to <see cref="Birds.Count"/>
/// at once, each put on a random walkable tile the camera can't currently see — so a bird is always found already
/// there, never seen popping in — and replaced a while after it flies off. Scenery only: it reports nothing and nothing
/// depends on it. <see cref="RunManager"/> ticks this during normal play and builds a new one for each arena.
/// </summary>
public sealed class BirdFlock
{
    private readonly Node2D _content;       // the arena's content node: birds are added here and freed with the arena
    private readonly LevelLayout? _layout;
    private readonly ArenaGround _ground;
    private readonly SpriteFrames _frames = Bird.BuildFrames();
    private readonly List<Bird> _birds = new();
    private float _nextIn = 0.0f;           // seconds until the next bird may arrive (set when one leaves)

    public BirdFlock(Node2D content, LevelLayout? layout, ArenaGround ground)
    {
        _content = content;
        _layout = layout;
        _ground = ground;
    }

    /// <summary>One tick: while the arena is short of birds, count down and then try to place one.</summary>
    public void Tick(float delta)
    {
        if (_birds.Count >= Birds.Count)
            return;
        _nextIn -= delta;
        if (_nextIn > 0.0f || PickPerch() is not Vector2 perch)
            return;
        var bird = new Bird(_frames);
        bird.TreeExited += () => OnBirdGone(bird);
        _birds.Add(bird);
        _content.AddChild(bird);
        Nodes.PlaceAt(bird, perch);
    }

    private void OnBirdGone(Bird bird)
    {
        _birds.Remove(bird);
        _nextIn = (float)GD.RandRange(Birds.RespawnMin, Birds.RespawnMax);
    }

    /// <summary>A random tile top that is out of the camera's view, clear of solid props and not beside another bird —
    /// as the exact point on its surface (a ramp tile's surface is below its top). Null if
    /// <see cref="Birds.SpawnTries"/> random tiles all fail.</summary>
    private Vector2? PickPerch()
    {
        var tops = _layout?.Tops();
        if (tops == null || tops.Count == 0)
            return null;
        Rect2 view = CameraView().Grow(Birds.OffscreenMargin);
        for (int i = 0; i < Birds.SpawnTries; i++)
        {
            Vector2 top = tops[(int)(GD.Randi() % (uint)tops.Count)];
            if (!view.HasPoint(top) && FarFromBirds(top) && _ground.SpotIsClear(top))
                return _ground.GroundBelow(top);
        }
        return null;
    }

    /// <summary>The part of the world the camera is showing.</summary>
    private Rect2 CameraView()
    {
        Viewport viewport = _content.GetViewport();
        return viewport.GetCanvasTransform().AffineInverse() * viewport.GetVisibleRect();
    }

    private bool FarFromBirds(Vector2 point)
    {
        foreach (Bird bird in _birds)
            if (bird.GlobalPosition.DistanceTo(point) < Birds.MinSpacing)
                return false;
        return true;
    }
}
