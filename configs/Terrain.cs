using Godot;

namespace MyGame;

/// <summary>
/// The stage BACKDROP: a full-screen background image (+ an optional animated element) behind the per-level colour
/// tint. Data + helpers; RunManager places it. C# port of <c>configs/terrain.gd</c>. (The old procedural tileset /
/// ground-plant / tree "skin" it used to carry is retired — stages are hand-painted layouts now.)
/// </summary>
public static class Terrain
{
	// Full-screen background image behind the per-level colour tint. RunManager shows the SINGLE image (no tiling)
	// scaled to BackgroundZoom of the viewport, centred, over a dark backing sampled from the image's own edge.
	// 1.0 = fills the screen (the original look); LOWER = zoomed out a little (the starfield sits in a bit more
	// space). Raising above 1.0 zooms in (the edges crop).
	public const string BackgroundTexturePath = "res://assets/terrain/stage1/bg1.png";
	public const float BackgroundZoom = 1.0f;  // bg1 is 640x360 → fills at 1.0 (no border) while still reading zoomed-out
	public const float BackgroundTintAlpha = 0.4f;

	// Optional ANIMATED background element (an orbiting planet), drawn over the bg, scaled with the bg's zoom.
	public const string BackgroundAnimPath = "res://assets/terrain/stage1/planet_moon.png";
	public const int BackgroundAnimFrameSize = 48;   // square frame side (sheet is a horizontal strip)
	public const int BackgroundAnimFrameCount = 10;  // 480 / 48
	public const float BackgroundAnimFps = 6.0f;
	public static readonly Vector2 BackgroundAnimRatio = new(0.7f, 0.18f);  // its centre as a fraction of the viewport
	public const float BackgroundAnimScale = 2.0f;   // extra multiplier on top of BackgroundZoom

	private static Texture2D Load(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

	public static Texture2D BackgroundTexture() => Load(BackgroundTexturePath);

	/// <summary>SpriteFrames for the animated background element (one looping "orbit" clip), or null if absent.</summary>
	public static SpriteFrames BackgroundAnimFrames()
	{
		var tex = Load(BackgroundAnimPath);
		if (tex == null)
			return null;
		var sf = new SpriteFrames();
		sf.RemoveAnimation("default");
		sf.AddAnimation("orbit");
		sf.SetAnimationLoop("orbit", true);
		sf.SetAnimationSpeed("orbit", BackgroundAnimFps);
		for (int i = 0; i < BackgroundAnimFrameCount; i++)
			sf.AddFrame("orbit", new AtlasTexture
			{
				Atlas = tex,
				Region = new Rect2(i * BackgroundAnimFrameSize, 0, BackgroundAnimFrameSize, BackgroundAnimFrameSize),
			});
		return sf;
	}
}
