using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// The character's canonical BODY palette — 6 MATERIALS × (5 shades + 1 rim), LIGHT→DARK. C# port of
/// <c>configs/palette_config.gd</c>. These hex values are the EXACT colours baked into Khalid's repaletted sheets
/// (source of truth = repalette.py PALETTE); the 36 colours map 1:1 to the sprite's pixels so the palette-LUT shader
/// remaps them live. A picker recolours a material from ONE base colour; <see cref="Derive"/> keeps that material's
/// light→dark VALUE ramp and adopts the base's hue+saturation, so one swatch drives all 5 shades + rim.
/// </summary>
public static class PaletteConfig
{
    public static readonly string[] Materials = { "hair", "skin", "jacket", "trim", "pants", "metal" };
    public const int ShadesPer = 6;  // 5 shades + 1 rim, light → dark

    /// <summary>Default per-material HDR glow push (sprite_palette.gdshader `glow`), index-aligned to MATERIALS.</summary>
    public static readonly float[] MaterialGlow = { 3.2f, 0.0f, 0.0f, 0.8f, 0.0f, 0.4f };  // hair, skin, jacket, trim, pants, metal

    /// <summary>The default glow array (marshals to a PackedFloat32Array uniform), ready for SetShaderParameter("glow", …).</summary>
    public static float[] GlowFloats() => (float[])MaterialGlow.Clone();

    public const string BodyShader = "res://vfx/shaders/sprite_palette.gdshader";
    public const string PortraitShader = "res://vfx/shaders/portrait_recolor.gdshader";

    /// <summary>Portrait colour uniform → the body material whose pick drives it.</summary>
    public static readonly Dictionary<string, string> PortraitMap = new()
        { { "hair_col", "hair" }, { "coat_col", "jacket" }, { "trim_col", "trim" }, { "skin_col", "skin" } };

    // Effect params for the material-aware LUT — the ONE source of truth shared by the preview and the in-game player.
    public const float Vibrancy = 0.4f;
    public const float FlowSpeed = 1.1f;
    public const float FlowAmount = 0.6f;
    public const float FlowFreq = 8.0f;
    public const int FlowShift = 2;
    /// <summary>The Ruh orb's bright core colour (red family, HDR); the hair-absorb flare uses this, recoloured by the power picks.</summary>
    public static readonly Color RuhCore = new(1.9f, 0.45f, 0.5f);

    /// <summary>The player's chosen BODY picks {material → Color}, set once at run start; empty == default palette.</summary>
    private static Dictionary<string, Color> _picks = new();

    public static void SetPicks(IReadOnlyDictionary<string, Color> picks) => _picks = new Dictionary<string, Color>(picks);

    /// <summary>The run's chosen PRIMARY (hair) colour, or the default red when unpicked — Khalid's damage number tints to this.</summary>
    public static Color HairColor() =>
        _picks.TryGetValue("hair", out Color hair) ? hair : new Color(DefaultShades["hair"][0]);

    /// <summary>Build a ready-to-use body ShaderMaterial: the LUT recolour (from `bodyPicks`; null = the run's picks)
    /// plus every effect param. Used by BOTH the preview and Player, so they always match.</summary>
    public static ShaderMaterial MakeMaterial(IReadOnlyDictionary<string, Color>? bodyPicks = null)
    {
        bodyPicks ??= _picks;
        var m = new ShaderMaterial { Shader = GD.Load<Shader>(BodyShader) };
        m.SetShaderParameter("src", ToLinearVec3(DefaultFlat()));
        m.SetShaderParameter("dst", ToLinearVec3(BuildTargets(bodyPicks)));
        m.SetShaderParameter("glow", GlowFloats());
        m.SetShaderParameter("vibrancy", Vibrancy);
        m.SetShaderParameter("flow_speed", FlowSpeed);
        m.SetShaderParameter("flow_amount", FlowAmount);
        m.SetShaderParameter("flow_freq", FlowFreq);
        m.SetShaderParameter("flow_shift", FlowShift);
        // The Ruh-absorb flare follows the RECOLOURED Ruh: swap RuhCore's hue to the Power-1 pick (keeping its HDR
        // magnitude). Already a linear working-space HDR value, so fed straight through (no srgb_to_linear).
        Color flare = VfxPalette.Recolor(RuhCore);
        m.SetShaderParameter("hair_surge_color", new Vector3(flare.R, flare.G, flare.B));
        return m;
    }

    /// <summary>A portrait ShaderMaterial whose hue uniforms follow `bodyPicks` (an unpicked family stays -1).</summary>
    public static ShaderMaterial MakePortraitMaterial(IReadOnlyDictionary<string, Color> bodyPicks)
    {
        var m = new ShaderMaterial { Shader = GD.Load<Shader>(PortraitShader) };
        ApplyPortraitHues(m, bodyPicks);
        return m;
    }

    /// <summary>Set an existing portrait material's colour uniforms from `bodyPicks` (live update on a pick change).</summary>
    public static void ApplyPortraitHues(ShaderMaterial m, IReadOnlyDictionary<string, Color> bodyPicks)
    {
        foreach (var (uni, mat) in PortraitMap)
        {
            if (bodyPicks.TryGetValue(mat, out Color c))
            {
                m.SetShaderParameter(uni, new Color(c.R, c.G, c.B, 1.0f));
            }
            else
            {
                m.SetShaderParameter(uni, new Color(0.0f, 0.0f, 0.0f, -1.0f));
            }
        }
    }

    /// <summary>material → [5 shades + rim], hex, LIGHT → DARK. From repalette.py PALETTE (keep in sync).</summary>
    public static readonly Dictionary<string, string[]> DefaultShades = new()
    {
        ["hair"] = new[] { "#941E1E", "#811A1A", "#721717", "#651414", "#531111", "#330A0A" },
        ["skin"] = new[] { "#0DA29B", "#0B8B84", "#086863", "#064946", "#021F1E", "#021312" },
        ["jacket"] = new[] { "#52382B", "#4B3328", "#37261D", "#271B15", "#160F0C", "#0E0907" },
        ["trim"] = new[] { "#EBE123", "#D1C81F", "#A7A019", "#797412", "#3B3809", "#242305" },
        ["pants"] = new[] { "#34432F", "#2F3D2B", "#293525", "#1D251A", "#161C14", "#0E120C" },
        ["metal"] = new[] { "#8E969E", "#60656A", "#43474B", "#2A2C2E", "#141516", "#0D0D0E" },
    };

    /// <summary>The default 36 colours flattened in MATERIALS order — the shader's `src` array.</summary>
    public static List<Color> DefaultFlat()
    {
        var outL = new List<Color>();
        foreach (var m in Materials)
            foreach (var hex in DefaultShades[m])
                outL.Add(new Color(hex));
        return outL;
    }

    /// <summary>Recolour one material from a single `base`, ANCHORED BY VALUE: the pick lands on the shade whose
    /// lightness is nearest, and every other shade shifts by the same delta (light→dark spacing preserved). Adopts
    /// base's hue+saturation. Returns the 6 shades (5 + rim).</summary>
    public static List<Color> Derive(string material, Color baseCol)
    {
        string[] shades = DefaultShades[material];
        float anchorV = new Color(shades[0]).V;
        float bestDiff = 999.0f;
        foreach (var hex in shades)
        {
            float v = new Color(hex).V;
            if (Mathf.Abs(v - baseCol.V) < bestDiff)
            {
                bestDiff = Mathf.Abs(v - baseCol.V);
                anchorV = v;
            }
        }
        float delta = baseCol.V - anchorV;
        var outL = new List<Color>();
        foreach (var hex in shades)
        {
            var d = new Color(hex);
            outL.Add(Color.FromHsv(baseCol.H, baseCol.S, Mathf.Clamp(d.V + delta, 0.0f, 1.0f), d.A));
        }
        return outL;
    }

    /// <summary>A full 36-colour target list from a {material → base Color} pick set (missing materials keep default).</summary>
    public static List<Color> BuildTargets(IReadOnlyDictionary<string, Color> bodyPicks)
    {
        var outL = new List<Color>();
        foreach (var m in Materials)
        {
            if (bodyPicks.TryGetValue(m, out Color pick))
                outL.AddRange(Derive(m, pick));
            else
                foreach (var hex in DefaultShades[m])
                    outL.Add(new Color(hex));
        }
        return outL;
    }

    /// <summary>Colours → a Vector3[] in LINEAR space (marshals to a PackedVector3Array uniform; HDR 2D samples linear,
    /// so src/dst must be linear for the per-pixel match to be exact).</summary>
    public static Vector3[] ToLinearVec3(List<Color> colors)
    {
        var outA = new Vector3[colors.Count];
        for (int i = 0; i < colors.Count; i++)
        {
            Color l = colors[i].SrgbToLinear();
            outA[i] = new Vector3(l.R, l.G, l.B);
        }
        return outA;
    }
}
