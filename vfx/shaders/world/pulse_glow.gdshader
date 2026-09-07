shader_type canvas_item;

// Collectible pulse-glow (fada_figs): breathes the sprite's brightness above 1.0 so it blooms (HDR glow threshold is
// 1.0), reading as a little energy mote and making it easy to spot. Alpha is untouched, so only the drawn pixels
// glow. Entirely GPU-driven off TIME — no per-frame CPU cost. Shared material assigned in FadaFig.cs.

uniform float pulse_speed = 3.5;
uniform float pulse_amount = 0.7;   // brightness swing above base
uniform float base_glow = 1.6;      // base multiplier (well above 1 -> a steady, visible bloom)

void fragment() {
	vec4 col = texture(TEXTURE, UV);
	float pulse = base_glow + pulse_amount * (0.5 + 0.5 * sin(TIME * pulse_speed));
	col.rgb *= pulse;
	COLOR = col;
}
