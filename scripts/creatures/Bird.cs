using Godot;

namespace MyGame;

/// <summary>
/// A bird perched in the arena — scenery (tuning in <see cref="Birds"/>). It is placed off screen by
/// <see cref="BirdFlock"/>; the moment the camera first sees it, it settles onto its perch (frames 0 to
/// <see cref="Birds.PerchFrame"/>) and holds there. When Khalid or an enemy comes within
/// <see cref="Birds.ScareRadius"/> it plays the rest of the strip while flying up and away from whoever scared it,
/// then frees itself (the art breaks the bird apart on its last frames).
///
/// <para>It only reacts: the camera, the scare sensor and the sprite's frames drive it through signals, and it ticks
/// only while flying.</para>
/// </summary>
public partial class Bird : Node2D
{
    private const string Animation = "bird";
    private const string FleeCue = "bird_flee";

    private enum Stage { Unseen, Settling, Perched, Fleeing }

    private readonly SpriteFrames _frames;
    private AnimatedSprite2D _sprite = null!;
    private Area2D _sensor = null!;
    private Sfx _sfx = null!;
    private Stage _stage = Stage.Unseen;
    private int _fleeSide = 1;      // +1 flies right, -1 left (away from whoever scared it)
    private float _fleeTime = 0.0f; // seconds since take-off (ramps the flight speed up)

    /// <summary>A bird drawn from <paramref name="frames"/> (<see cref="BuildFrames"/> — one shared by the whole flock).</summary>
    public Bird(SpriteFrames frames) => _frames = frames;

    /// <summary>The bird strip cut into one animation, in sheet order. Built once per arena by <see cref="BirdFlock"/>.</summary>
    public static SpriteFrames BuildFrames()
    {
        var sheet = GD.Load<Texture2D>(Birds.SheetPath);
        var frames = new SpriteFrames();
        frames.AddAnimation(Animation);
        frames.SetAnimationSpeed(Animation, Birds.Fps);
        frames.SetAnimationLoopMode(Animation, SpriteFrames.LoopMode.None);
        for (int i = 0; i < sheet.GetWidth() / Birds.FrameSize; i++)
        {
            var region = new Rect2(i * Birds.FrameSize, 0, Birds.FrameSize, Birds.FrameSize);
            frames.AddFrame(Animation, new AtlasTexture { Atlas = sheet, Region = region });
        }
        return frames;
    }

    public override void _Ready()
    {
        ZIndex = WorldZ.Wildlife;
        SetProcess(false); // it only moves while fleeing
        _sfx = GetNode<Sfx>("/root/Sfx");

        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = _frames,
            Animation = Animation,
            Offset = new Vector2(0.0f, -Birds.FrameSize / 2.0f), // feet (the frame's bottom edge) on the perch
            FlipH = GD.Randf() < 0.5f,                           // the art faces right; half of them look left
        };
        _sprite.FrameChanged += OnFrameChanged;
        _sprite.AnimationFinished += QueueFree;
        AddChild(_sprite);

        var seen = new VisibleOnScreenNotifier2D
        {
            Rect = new Rect2(-Birds.FrameSize / 2.0f, -Birds.FrameSize, Birds.FrameSize, Birds.FrameSize),
        };
        seen.ScreenEntered += OnSeen;
        AddChild(seen);

        // Off until it is perched: turning it on then also reports anyone ALREADY standing in range.
        _sensor = new Area2D
        {
            CollisionLayer = 0,
            CollisionMask = (uint)(Combat.Layer.PlayerBody | Combat.Layer.EnemyBody),
            Monitoring = false,
            Monitorable = false,
        };
        _sensor.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = Birds.ScareRadius } });
        _sensor.BodyEntered += OnScared;
        AddChild(_sensor);
    }

    public override void _Process(double delta)
    {
        _fleeTime += (float)delta;
        float speed = Mathf.Min(_fleeTime / Birds.FleeRampTime, 1.0f);
        Position += new Vector2(Birds.FleeVelocity.X * _fleeSide, Birds.FleeVelocity.Y) * speed * (float)delta;
    }

    /// <summary>The camera sees it for the first time: settle onto the perch.</summary>
    private void OnSeen()
    {
        if (_stage != Stage.Unseen)
            return;
        _stage = Stage.Settling;
        _sprite.Play(Animation);
    }

    private void OnFrameChanged()
    {
        if (_stage == Stage.Settling && _sprite.Frame == Birds.PerchFrame)
        {
            _stage = Stage.Perched;
            _sprite.Pause();
            _sensor.SetDeferred(Area2D.PropertyName.Monitoring, true);
        }
        else if (_stage == Stage.Fleeing && _sprite.Frame == Birds.FleeSoundFrame)
            _sfx.PlayAt(FleeCue, GlobalPosition);
    }

    /// <summary>Khalid or an enemy came close: play the rest of the strip, flying away from <paramref name="body"/>.</summary>
    private void OnScared(Node2D body)
    {
        if (_stage != Stage.Perched)
            return;
        _stage = Stage.Fleeing;
        _sensor.SetDeferred(Area2D.PropertyName.Monitoring, false);
        _fleeSide = GlobalPosition.X >= body.GlobalPosition.X ? 1 : -1;
        _sprite.FlipH = _fleeSide < 0;
        _sprite.Play(); // resumes from the perch frame
        SetProcess(true);
    }
}
