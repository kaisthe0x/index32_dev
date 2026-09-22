shader_type canvas_item;

// ENEMY accent glow. The enemies are repaletted to a dark BODY + a bright ACCENT (eyes / orb / iris).
// This makes just the accent pixels bloom -- pushed past 1.0 so the WorldEnvironment bloom catches them --
// so they read as "alive, glowing eyes in the dark" without lighting up the whole body like Khalid.
//
// An accent pixel = SATURATED and BRIGHT (the body is dark / low-value, so it never crosses the gates).
// With viewport HDR 2D on, TEXTURE samples in LINEAR, which is what we push. Deliberately subtle: `glow`
// defaults low (a fraction of Khalid's hair glow).
//
// TWEAK on the material (resources/enemy_glow.tres) or here:
//   glow     -- how hard the accent blooms (0 = off). Raise for punchier eyes.
//   sat_min  -- how saturated a pixel must be to count as an accent (raise if the body starts glowing).
//   val_min  -- how bright it must be (raise to glow only the very brightest highlight).
//   soft     -- width of the gate's falloff (keeps it from hard-edging).

uniform float glow : hint_range(0.0, 6.0) = 1.3;
uniform float sat_min : hint_range(0.0, 1.0) = 0.40;
uniform float val_min : hint_range(0.0, 1.0) = 0.30;
uniform float soft : hint_range(0.01, 0.5) = 0.10;

// Hit-flash: blend the enemy toward flash_color by `flash`. flash = 0 (default) is a pure no-op; code pulses
// it -> 1 on a hit and eases it back. (linear HDR values)
uniform vec3 flash_color = vec3(1.0);
uniform float flash : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float mx = max(tex.r, max(tex.g, tex.b));
	float mn = min(tex.r, min(tex.g, tex.b));
	float sat = mx > 0.0 ? (mx - mn) / mx : 0.0;
	// Accent gate: saturated AND bright. Body pixels (dark, low-sat) fail one gate and stay untouched.
	float accent = smoothstep(sat_min, sat_min + soft, sat) * smoothstep(val_min, val_min + soft, mx);
	tex.rgb *= 1.0 + glow * accent; // lift the accent past 1.0 -> bloom
	tex.rgb = mix(tex.rgb, flash_color, flash); // hit-flash (no-op at flash = 0)
	COLOR = vec4(tex.rgb, tex.a);
}
