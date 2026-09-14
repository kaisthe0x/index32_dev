shader_type canvas_item;

// MATERIAL-AWARE PALETTE-SWAP recolour. The sprite is baked to exactly N known colours (PaletteConfig:
// 6 materials x 6 shades, light->dark). For each pixel we find the nearest `src` slot and output the
// matching `dst` slot -- so a colour picker that rewrites `dst` recolours the WHOLE character live, every
// pixel, no per-part masks and no hue-key gaps (the reason the old tint shader only caught the bright
// hair). Because we know each pixel's slot, we also know its MATERIAL (slot / 6) and SHADE (slot % 6),
// so the living-hair FLOW and the HDR GLOW are layered back on per material -- same effects as before,
// now riding an exact recolour.
//
// Colour space: with viewport HDR 2D on, TEXTURE samples in LINEAR, so `src`/`dst` are fed in LINEAR too
// (PaletteConfig.to_linear_vec3). Match + output happen in one space; alpha passes through untouched.

const int N = 36;
const int SHADES = 6;    // shades per material (5 + rim), light -> dark
const int HAIR = 0;      // hair is MATERIALS[0] -- the one material with animated flow

uniform vec3 src[N];   // baked palette, linear (set once from PaletteConfig.default_flat)
uniform vec3 dst[N];   // target palette, linear (rewritten by the picker; == src -> default look)

// Effects (index-aligned to materials for glow; hair-only for flow):
uniform float glow[SHADES];              // per-material HDR push (>0 lifts past 1.0 -> WorldEnvironment bloom)
uniform float vibrancy = 0.0;            // saturation punch (0 = untouched)
uniform float flow_speed = 1.1;          // how fast the hair accents move
uniform float flow_amount : hint_range(0.0, 1.0) = 0.6;  // how strongly darker accents bleed through
uniform float flow_freq = 8.0;           // spatial frequency of the flow waves
uniform int flow_shift = 2;              // how many shades darker the flowing accent pulls toward
uniform float hair_surge : hint_range(0.0, 1.0) = 0.0;  // Ruh-absorb flare: 0 = rest, 1 = full flare
uniform vec3 hair_surge_color = vec3(2.6, 1.7, 0.5);    // the flare target (linear HDR gold)

// Hit-flash: blend the whole body toward flash_color by `flash`. flash = 0 (default) is a pure no-op, so
// normal rendering is untouched; code pulses flash -> 1 on a hit and eases it back. (linear HDR values)
uniform vec3 flash_color = vec3(1.0);
uniform float flash : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	int best = 0;
	float bestd = 1e20;
	for (int i = 0; i < N; i++) {
		vec3 e = tex.rgb - src[i];
		float d = dot(e, e);  // squared distance; nearest slot (exact for baked palette pixels)
		if (d < bestd) {
			bestd = d;
			best = i;
		}
	}
	int mat = best / SHADES;
	int shade = best - mat * SHADES;
	vec3 col = dst[best];

	// Hair flow: pull the pixel toward a DARKER hair shade on a moving wave, so dark accents ripple
	// through the bright base -- the living-hair look, reconstructed from the palette instead of a
	// separate accent colour. Only the hair material animates.
	if (mat == HAIR) {
		int acc = HAIR * SHADES + min(shade + flow_shift, SHADES - 1);
		float wave = 0.5 + 0.5 * sin(TIME * flow_speed + UV.y * flow_freq);
		col = mix(col, dst[acc], wave * flow_amount);
		// Ruh-absorb flare: the hair surges toward the HDR gold absorb colour and eases back (driven by
		// player.gd's _set_hair_mix). Scaled by the shade's brightness so darker hair flares less.
		col = mix(col, hair_surge_color * max(shade == 0 ? 1.0 : 0.7 - 0.1 * float(shade), 0.2), hair_surge);
	}

	// Per-material HDR glow: lift the material past 1.0 so its bright shades clear the bloom threshold.
	col *= (1.0 + glow[mat]);

	// Vibrancy: push saturation away from luma.
	if (vibrancy != 0.0) {
		float luma = dot(col, vec3(0.299, 0.587, 0.114));
		col = mix(vec3(luma), col, 1.0 + vibrancy);
	}

	col = mix(col, flash_color, flash); // hit-flash (no-op at flash = 0)
	COLOR = vec4(col, tex.a);
}
