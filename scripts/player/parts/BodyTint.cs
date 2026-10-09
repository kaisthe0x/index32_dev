using Godot;

namespace MyGame;

/// <summary>
/// The shader on the player's body, and the hair's flare when he absorbs Ruh. Khalid wears the palette-LUT material
/// (<see cref="PaletteConfig.MakeMaterial"/> — the run's body colours, glow, hair flow), whose <c>hair_surge</c>
/// uniform flares the hair. Any other character wears its own <c>resources/&lt;character&gt;_tint.tres</c>, if it
/// has one; a tint shader with the three hair colours flares by blending them toward gold.
/// </summary>
public sealed class BodyTint
{
    private const float FlareRefractory = 0.2f;   // partial absorbs closer together than this share one flare
    private static readonly Color FlareBase = new(2.6f, 1.7f, 0.5f);
    private static readonly Color FlareAccentA = new(2.3f, 1.0f, 0.35f);
    private static readonly Color FlareAccentB = new(1.9f, 0.6f, 0.25f);

    private readonly Node _owner;   // makes the flare's tween
    private ShaderMaterial? _material;
    private bool _isLut;
    private (Color Base, Color AccentA, Color AccentB)? _hair;   // a tint shader's own hair colours (non-LUT body only)
    private Tween? _flare;
    private float _refractoryLeft;

    public BodyTint(Node owner) => _owner = owner;

    /// <summary>Dress <paramref name="sprite"/> for <paramref name="character"/>.</summary>
    public void Apply(AnimatedSprite2D sprite, string character)
    {
        _hair = null;
        if (character == "khalid")
        {
            _material = PaletteConfig.MakeMaterial();
            _isLut = true;
            sprite.Material = _material;
            return;
        }
        string path = $"res://resources/{character}_tint.tres";
        var material = ResourceLoader.Exists(path) ? GD.Load<Material>(path) : null;
        _isLut = false;
        if (material is not ShaderMaterial shader)
        {
            _material = null;
            sprite.Material = material;
            return;
        }
        _material = (ShaderMaterial)shader.Duplicate();
        sprite.Material = _material;
        Variant br = _material.GetShaderParameter("base_red");
        Variant aa = _material.GetShaderParameter("accent_a");
        Variant ab = _material.GetShaderParameter("accent_b");
        if (br.VariantType == Variant.Type.Color && aa.VariantType == Variant.Type.Color && ab.VariantType == Variant.Type.Color)
            _hair = (br.AsColor(), aa.AsColor(), ab.AsColor());
    }

    public void Tick(float delta) => _refractoryLeft = Mathf.Max(_refractoryLeft - delta, 0.0f);

    /// <summary>A Ruh orb reached him: flare the hair — fully for a <paramref name="completedCharge"/>, lightly
    /// otherwise. False if a light flare was skipped because one just played (the caller skips its sound too).</summary>
    public bool FlareOnRuh(bool completedCharge)
    {
        if (!completedCharge && _refractoryLeft > 0.0f)
            return false;
        _refractoryLeft = FlareRefractory;
        Flare(completedCharge ? 1.0f : 0.6f, completedCharge ? 0.6f : 0.35f);
        return true;
    }

    private void Flare(float strength, float duration)
    {
        if (_material == null || (!_isLut && _hair == null))
            return;
        if (_flare != null && _flare.IsValid())
            _flare.Kill();
        _flare = _owner.CreateTween();
        _flare.TweenMethod(Callable.From<float>(SetFlare), 0.0f, strength, duration * 0.35f).SetEase(Tween.EaseType.Out);
        _flare.TweenMethod(Callable.From<float>(SetFlare), strength, 0.0f, duration * 0.65f).SetEase(Tween.EaseType.In);
    }

    private void SetFlare(float f)
    {
        if (_material == null)
            return;
        if (_isLut)
        {
            _material.SetShaderParameter("hair_surge", f);
            return;
        }
        if (_hair is not var (baseCol, accentA, accentB))
            return;
        _material.SetShaderParameter("base_red", baseCol.Lerp(FlareBase, f));
        _material.SetShaderParameter("accent_a", accentA.Lerp(FlareAccentA, f));
        _material.SetShaderParameter("accent_b", accentB.Lerp(FlareAccentB, f));
    }
}
