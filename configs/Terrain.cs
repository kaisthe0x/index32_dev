using Godot;

namespace MyGame;

/// <summary>
/// The stage BACKDROP: a full-screen background image under a colour tint. Data + helpers; RunManager places it. C# port of <c>configs/terrain.gd</c>. (The old procedural tileset /
/// ground-plant / tree "skin" it used to carry is retired — stages are hand-painted layouts now.)
/// </summary>
public static class Terrain
{
	// Full-screen background image under the BackgroundTint layer. RunManager shows the SINGLE image (no tiling)
	// scaled to BackgroundZoom of the viewport, centred, over a dark backing sampled from the image's own edge.
	// 1.0 = fills the screen (the original look); LOWER = zoomed out a little (the starfield sits in a bit more
	// space). Raising above 1.0 zooms in (the edges crop).
	public const string BackgroundTexturePath = "res://assets/terrain/stage1/bg1.png";
	public const float BackgroundZoom = 1.0f;  // bg1 is 640x360 → fills at 1.0 (no border) while still reading zoomed-out
	// A dark colour laid over the image at BackgroundTintAlpha to sink it behind the play layer (fully opaque when
	// there's no image — then it IS the backdrop).
	public static readonly Color BackgroundTint = new(0.06f, 0.10f, 0.13f);
	public const float BackgroundTintAlpha = 0.4f;

	private static Texture2D Load(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

	public static Texture2D BackgroundTexture() => Load(BackgroundTexturePath);
}
