using Godot;

namespace MyGame;

/// <summary>
/// The low-health screen effect: a full-screen shader grade that fades in once health drops under
/// <see cref="Threshold"/> (about one block left), stronger the lower it goes, and beats like a heart. Its own
/// <see cref="CanvasLayer"/> under the HUD (<see cref="UiLayers.LowHealth"/>). The <see cref="HUD"/> feeds it the
/// health ratio and ticks it.
/// </summary>
public partial class LowHealthVignette : CanvasLayer
{
    private const string ShaderPath = "res://vfx/shaders/low_health.gdshader";
    private const float Threshold = 0.34f;   // health ratio under which the effect is on (~1 block left)
    private const float MinLevel = 0.35f;    // its strength right at the threshold (1.0 at zero health)
    private const float Fade = 3.5f;         // level change per second
    private const float BeatHz = 1.15f;
    private const float PulseBase = 0.72f;
    private const float PulsePunch = 0.6f;

    private readonly ShaderMaterial _material = new() { Shader = GD.Load<Shader>(ShaderPath) };
    private float _level = 0.0f;
    private float _target = 0.0f;
    private float _time = 0.0f;

    /// <summary>Whether health is low enough for the effect right now (it may still be fading in or out).</summary>
    public bool Active => _target > 0.0f;

    public override void _Ready()
    {
        Layer = UiLayers.LowHealth;
        Visible = false;
        var rect = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Material = _material };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _material.SetShaderParameter("intensity", 0.0);
        AddChild(rect);
    }

    /// <summary>Health changed: <paramref name="ratio"/> is current / maximum.</summary>
    public void SetHealthRatio(float ratio)
    {
        if (ratio >= Threshold)
            _target = 0.0f;
        else
        {
            float t = Mathf.Clamp((Threshold - ratio) / Threshold, 0.0f, 1.0f);
            _target = Mathf.Lerp(MinLevel, 1.0f, t);
        }
    }

    /// <summary>Switch the effect off at once (no player to show it for).</summary>
    public void Clear()
    {
        _target = 0.0f;
        _level = 0.0f;
        Visible = false;
    }

    /// <summary>One frame: fade toward the target and beat.</summary>
    public void Tick(float delta)
    {
        _time += delta;
        _level = Mathf.MoveToward(_level, _target, Fade * delta);
        bool on = _level > 0.001f;
        Visible = on;
        if (on)
        {
            float mult = PulseBase + PulsePunch * Heartbeat(_time);
            _material.SetShaderParameter("intensity", Mathf.Clamp(_level * mult, 0.0f, 1.0f));
        }
    }

    /// <summary>Heartbeat envelope 0..1: a sharp "lub" thump plus a softer "dub", so the pulse punches.</summary>
    private static float Heartbeat(float t)
    {
        float ph = Mathf.PosMod(t * BeatHz, 1.0f);
        float lub = Mathf.Exp(-Mathf.Pow(ph / 0.055f, 2.0f));
        float dub = 0.6f * Mathf.Exp(-Mathf.Pow((ph - 0.17f) / 0.07f, 2.0f));
        return Mathf.Min(lub + dub, 1.0f);
    }
}
