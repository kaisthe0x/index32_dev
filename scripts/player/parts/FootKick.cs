using Godot;

namespace MyGame;

/// <summary>
/// The dirt and grass a running character kicks up (<see cref="Footfalls"/>). <see cref="Player"/> calls
/// <see cref="Kick"/> on each push-off frame of its run; the ground under the feet decides what comes up — the tile
/// the floor contact touched, looked up by its tileset image in <see cref="Footfalls.ByTileset"/> (stone has no row,
/// so it sheds nothing). The particles are left in the world (they don't follow the runner) and drawn behind him.
/// A small pool of bursts lets a new kick start while the last one is still falling.
/// </summary>
public sealed partial class FootKick : Node2D
{
    private const int Pool = 3;

    private readonly CpuParticles2D[] _dirt = new CpuParticles2D[Pool];
    private readonly CpuParticles2D[] _grass = new CpuParticles2D[Pool];
    private int _next;

    public override void _Ready()
    {
        var speck = PixelTexture(1, 1);
        var blade = PixelTexture(1, 3);
        for (int i = 0; i < Pool; i++)
        {
            _dirt[i] = Burst(Footfalls.DirtPerKick, Footfalls.DirtLife, speck, alignToMotion: false);
            _dirt[i].ScaleAmountMin = Footfalls.DirtSizeMin;
            _dirt[i].ScaleAmountMax = Footfalls.DirtSizeMax;
            _grass[i] = Burst(Footfalls.GrassPerKick, Footfalls.GrassLife, blade, alignToMotion: true);
        }
    }

    /// <summary>A foot pushed off: spray whatever the ground under <paramref name="body"/> sheds, behind the heel of
    /// a runner facing <paramref name="facing"/> (+1 right, -1 left).</summary>
    public void Kick(CharacterBody2D body, int facing)
    {
        if (SurfaceUnder(body) is not { } style)
            return;
        var at = body.GlobalPosition + new Vector2(Footfalls.HeelOffset.X * facing, Footfalls.HeelOffset.Y);
        var dir = new Vector2(Footfalls.Throw.X * facing, Footfalls.Throw.Y).Normalized();
        Fire(_dirt[_next], style.Dirt, at, dir);
        Fire(_grass[_next], style.Grass, at, dir);
        _next = (_next + 1) % Pool;
    }

    /// <summary>The style of the tile the body's floor contact touched, or null (no tile, or a tileset that sheds
    /// nothing).</summary>
    private static FootfallStyle? SurfaceUnder(CharacterBody2D body)
    {
        for (int i = 0; i < body.GetSlideCollisionCount(); i++)
        {
            var hit = body.GetSlideCollision(i);
            if (hit.GetNormal().Dot(body.UpDirection) < 0.5f || hit.GetCollider() is not TileMapLayer layer)
                continue;
            // The tile at the contact point, one pixel into the floor. (Not GetCoordsForBodyRid: the layer merges its
            // tiles' collision into chunks, so the body's "tile" is just one representative of the chunk.)
            var cell = layer.LocalToMap(layer.ToLocal(hit.GetPosition() - hit.GetNormal()));
            int source = layer.GetCellSourceId(cell);
            if (source < 0 || layer.TileSet?.GetSource(source) is not TileSetAtlasSource atlas || atlas.Texture == null)
                continue;
            return Footfalls.ByTileset.GetValueOrDefault(atlas.Texture.ResourcePath);
        }
        return null;
    }

    private static void Fire(CpuParticles2D burst, Color[] colours, Vector2 at, Vector2 dir)
    {
        burst.ColorInitialRamp = Palette(colours);
        burst.GlobalPosition = at;
        burst.Direction = dir;
        burst.Restart();
    }

    private CpuParticles2D Burst(int amount, float life, Texture2D texture, bool alignToMotion)
    {
        var p = new CpuParticles2D
        {
            Emitting = false,
            OneShot = true,
            Explosiveness = 0.85f,
            Amount = amount,
            Lifetime = life,
            LocalCoords = false,
            Texture = texture,
            Spread = Footfalls.Spread,
            InitialVelocityMin = Footfalls.SpeedMin,
            InitialVelocityMax = Footfalls.SpeedMax,
            Gravity = Footfalls.Gravity,
            ColorRamp = FadeAtEnd(),
        };
        p.SetParticleFlag(CpuParticles2D.ParticleFlags.AlignYToVelocity, alignToMotion); // a blade points the way it flies
        AddChild(p);
        return p;
    }

    /// <summary>A random pick per particle from exactly these colours: a gradient with hard steps between them.</summary>
    private static Gradient Palette(Color[] colours)
    {
        var g = new Gradient { InterpolationMode = Gradient.InterpolationModeEnum.Constant, Offsets = [], Colors = [] };
        for (int i = 0; i < colours.Length; i++)
            g.AddPoint((float)i / colours.Length, colours[i]);
        return g;
    }

    /// <summary>Full colour for most of the life, then gone over the last fifth (as the piece lands).</summary>
    private static Gradient FadeAtEnd()
    {
        var g = new Gradient { Offsets = [], Colors = [] };
        g.AddPoint(0.0f, Colors.White);
        g.AddPoint(0.8f, Colors.White);
        g.AddPoint(1.0f, new Color(1, 1, 1, 0));
        return g;
    }

    private static ImageTexture PixelTexture(int w, int h)
    {
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        img.Fill(Colors.White);
        return ImageTexture.CreateFromImage(img);
    }
}
