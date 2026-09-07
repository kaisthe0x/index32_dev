using Godot;

namespace MyGame;

/// <summary>
/// A hand-placed decorative bush. Every bush shares ONE atlas (assets/terrain/stage1/bushes.png) and just shows a
/// different 32px frame of it via <see cref="Variant"/>, so any number of bushes batch into ~one draw call. Purely
/// visual — no collision, no per-frame cost (the frame is set once). <c>[Tool]</c> so the picked variant previews
/// live in the editor. Drop <c>scenes/props/bush.tscn</c> into a layout, set Variant in the Inspector, drag anywhere.
/// </summary>
[Tool]
[GlobalClass]
public partial class Bush : Sprite2D
{
	private const int FrameSize = 32;  // bushes.png is 128x32 -> four 32px frames in a row
	private const int Variants = 4;

	private int _variant;

	/// <summary>Which bush (0..3) of the shared atlas strip to show.</summary>
	[Export(PropertyHint.Range, "0,3,1")]
	public int Variant
	{
		get => _variant;
		set
		{
			_variant = Mathf.Clamp(value, 0, Variants - 1);
			ApplyFrame();
		}
	}

	public override void _Ready() => ApplyFrame();

	private void ApplyFrame()
	{
		if (Texture == null)
			return;
		RegionEnabled = true;
		RegionRect = new Rect2(_variant * FrameSize, 0, FrameSize, FrameSize);
	}
}
