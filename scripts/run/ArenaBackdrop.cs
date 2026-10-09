using Godot;

namespace MyGame;

/// <summary>
/// What is drawn behind the arena: the stage's single background image (<see cref="Terrain.BackgroundTexture"/>),
/// centred and scaled to the window, over a flat fill in the image's own edge colour, under a dark tint that sinks it
/// behind the play layer. With no image the tint alone is the backdrop. Added once by <see cref="RunManager"/>.
/// </summary>
public partial class ArenaBackdrop : CanvasLayer
{
    private static readonly Color FallbackFill = new(0.05f, 0.05f, 0.06f);

    private Sprite2D? _sky;
    private Vector2 _imageSize;

    public override void _Ready()
    {
        Layer = UiLayers.Background;
        Color tint = Terrain.BackgroundTint;
        var image = Terrain.BackgroundTexture();
        if (image != null)
        {
            tint.A = Terrain.BackgroundTintAlpha;
            _imageSize = image.GetSize();
            // Dark backing in the image's OWN edge tone, so zooming the single (non-tiled) image out never shows a
            // hard cut or the void — the starfield just sits in a bit more of its own space.
            Color fill = FallbackFill;
            Image pixels = image.GetImage();
            if (pixels != null)
            {
                if (pixels.IsCompressed())
                    pixels.Decompress();
                fill = pixels.GetPixel(0, 0);
            }
            var back = new ColorRect { Color = fill, MouseFilter = Control.MouseFilterEnum.Ignore };
            back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(back);
            // The SINGLE starfield (no tiling), centred + scaled by BackgroundZoom in Layout (1.0 = fills).
            _sky = new Sprite2D { Texture = image, TextureFilter = CanvasItem.TextureFilterEnum.Nearest };
            AddChild(_sky);
        }
        Layout();
        GetViewport().SizeChanged += Layout;
        var tintRect = new ColorRect { Color = tint, MouseFilter = Control.MouseFilterEnum.Ignore };
        tintRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(tintRect);
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= Layout;

    /// <summary>Centre + scale the single bg image (BackgroundZoom of the viewport) for the current resolution. Re-run
    /// on viewport resize.</summary>
    private void Layout()
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float zoom = Terrain.BackgroundZoom;
        Vector2 skySize = vp * zoom;        // the image's on-screen rect (zoom 1.0 = fills)
        if (_sky != null && IsInstanceValid(_sky) && _imageSize.X > 0)
        {
            _sky.Position = vp / 2;
            _sky.Scale = skySize / _imageSize;
        }
    }
}
