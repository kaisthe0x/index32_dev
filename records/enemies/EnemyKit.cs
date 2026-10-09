namespace MyGame;

/// <summary>
/// One enemy TYPE's spawn spec (the roster is <see cref="EnemyKits"/>): which scene to build, what it is for the
/// spawner, and its tuning.
/// <list type="bullet">
/// <item><paramref name="Id"/> — an <see cref="EnemyIds"/> id; also the key into the sprite / emitter / sound tables.</item>
/// <item><paramref name="DisplayName"/> — the name on its health bar.</item>
/// <item><paramref name="Tier"/> — its strength class; sets the Lira it drops unless <see cref="LiraDrop"/> says otherwise.</item>
/// <item><paramref name="Movement"/> — how it gets about; the spawner places a stationary one only at a spawn spot.</item>
/// <item><paramref name="Tune"/> — sets the enemy's tuning properties (health, speeds, ranges, attack numbers…) on the
///   freshly built node, before it enters the tree. Typed: a wrong name or type is a compile error. Anything it
///   doesn't set keeps the default declared on <see cref="Enemy"/>.</item>
/// </list>
/// </summary>
public sealed record EnemyKit(string Id, string DisplayName, EnemyTier Tier, EnemyMovement Movement, System.Action<Enemy> Tune)
{
    /// <summary>The scene every plain ground enemy is built from.</summary>
    public const string DefaultScene = "res://scenes/enemy.tscn";

    /// <summary>The scene to instance — a behaviour subclass's scene for a sleeper, diver or warden.</summary>
    public string Scene { get; init; } = DefaultScene;

    /// <summary>At most this many alive at once (the spawner grows it over the run); null = no per-type cap.</summary>
    public int? SpawnCap { get; init; }

    /// <summary>Lira dropped on death; null = the default for its <see cref="Tier"/>.</summary>
    public int? LiraDrop { get; init; }

    /// <summary>A kit for a behaviour subclass <typeparamref name="T"/> built from <paramref name="scene"/>, whose
    /// <paramref name="tune"/> can set that subclass's own properties.</summary>
    public static EnemyKit Of<T>(string id, string displayName, EnemyTier tier, EnemyMovement movement, string scene,
        System.Action<T> tune) where T : Enemy =>
        new(id, displayName, tier, movement, e => tune((T)e)) { Scene = scene };
}
