shader_type canvas_item;

// Recolour ONLY the vials on a sprite -- Dekken's vending machine. The vials are painted in one red ramp and are the
// only red in the art, so a pixel is a vial when its red clearly dominates (g and b both under `key` x r). Those
// pixels are repainted `tint` (Khalid's hair colour, set by DekkenStall) scaled by how bright the painted red is --
// the vial's shading survives, only its colour changes -- and lifted by `glow` so the bright ones bloom a little.
// Every other pixel (steel, amber lines, moss) passes through untouched.
//
// The numbers are in LINEAR light (the project renders 2D in HDR): there the vial ramp has g/r = b/r ~ 0.03-0.07
// while the amber glow lines sit at g/r ~ 0.45, so `key` 0.2 splits them with room to spare; the darkest vial shade
// is r ~ 0.045, hence the low floor.

uniform vec4 tint : source_color = vec4(1.0, 0.2, 0.2, 1.0); // the vials' colour at full brightness
uniform float glow : hint_range(0.0, 4.0) = 1.3;             // lift on the vials (above 1 blooms via HDR)
uniform float key : hint_range(0.0, 1.0) = 0.2;              // g, b must be under this fraction of r to count as red

void fragment() {
	vec4 t = texture(TEXTURE, UV);
	bool vial = t.a > 0.0 && t.r > 0.02 && t.g < t.r * key && t.b < t.r * key;
	COLOR = vial ? vec4(tint.rgb * t.r * glow, t.a) : t;
}
