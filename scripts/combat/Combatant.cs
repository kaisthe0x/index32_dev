using Godot;

namespace MyGame;

/// <summary>
/// Shared base for the game's damageable, sprite-driven bodies — the C# **enemy tree** (Enemy/Nasen/Ein)
/// during the migration; Player still uses the GDScript <c>class_name Combatant</c> until it ports (Phase 4b).
/// Holds the small pieces both would otherwise reimplement: feet-anchoring, the hit-flash, knockback→shove.
/// C# port of <c>scripts/combat/combatant.gd</c>.
///
/// NOT a <c>[GlobalClass]</c> — only C# subclasses (Enemy…) extend it, so it needs no Godot global name and
/// therefore doesn't clash with the GDScript <c>Combatant</c> that Player still extends. Called only from C#,
/// so it's fully idiomatic (PascalCase). At the end of the body-tree port it becomes THE Combatant.
/// </summary>
public partial class Combatant : CharacterBody2D
{
    /// <summary>Anchor a sprite so its feet sit on the node origin, horizontally centred, using idle frame 0.</summary>
    public static void AnchorToFeet(AnimatedSprite2D sprite)
    {
        var frame = sprite.SpriteFrames.GetFrameTexture("idle", 0);
        if (frame == null)
            return;
        sprite.Centered = false;
        sprite.Offset = new Vector2(-frame.GetWidth() / 2.0f, -frame.GetHeight());
    }

    /// <summary>
    /// Apply an incoming hit's knockback to this body and return how long to stagger (0 = none). The caller
    /// applies its own stun with the returned time and passes its facing (the shove dir when the source is level).
    /// </summary>
    public float ApplyKnockback(Hit hit, int facing)
    {
        float stagger = hit.Stun;
        // IsInstanceValid, not != null: the attacker may have been freed (a shot that outlived its firer).
        if (hit.Knockback > 0.0f && GodotObject.IsInstanceValid(hit.Source) && hit.Source is Node2D src)
        {
            int dir = Mathf.Sign(GlobalPosition.X - src.GlobalPosition.X);
            if (dir == 0)
                dir = -facing;
            Velocity = new Vector2(dir * hit.Knockback, -hit.Knockback * Combat.KnockbackPop);
            stagger = Mathf.Max(stagger, Combat.MinStagger);
        }
        return stagger;
    }

    /// <summary>Flash `sprite` red, fading back to white — the shared "took a hit" tell.</summary>
    public void Flash(AnimatedSprite2D sprite)
    {
        sprite.Modulate = Combat.HitFlash;
        CreateTween().TweenProperty(sprite, "modulate", Colors.White, Combat.HitFlashTime);
    }

    /// <summary>Colour hit-flash via the sprite MATERIAL's <c>flash</c> uniform: both the body-palette and enemy-glow
    /// shaders `mix` their output toward <c>flash_color</c> by <c>flash</c>. This works where a plain <c>modulate</c>
    /// wouldn't — those shaders overwrite <c>COLOR</c>, so a node modulate is ignored. Pulses flash to 1 then eases it
    /// back to 0 over <paramref name="time"/> (0 = untouched rendering). No-op if the sprite has no such ShaderMaterial.</summary>
    protected void FlashSprite(AnimatedSprite2D sprite, Color color, float time)
    {
        if (sprite.Material is not ShaderMaterial mat)
            return;
        mat.SetShaderParameter("flash_color", new Vector3(color.R, color.G, color.B));
        mat.SetShaderParameter("flash", 1.0f);
        CreateTween().TweenProperty(mat, "shader_parameter/flash", 0.0f, time);
    }

    private Tween? _reactTw;

    /// <summary>
    /// Punchy "took a hit" reaction: a white-hot HDR flash + a feet-anchored squash, both scaled by `damage`.
    /// Fires even at 0 knockback (a flurry like ora_ora), so every hit reads as an impact. Kills any in-flight
    /// reaction so rapid hits re-punch cleanly.
    /// </summary>
    public void HitReact(AnimatedSprite2D sprite, float damage)
    {
        if (GodotObject.IsInstanceValid(_reactTw))
            _reactTw!.Kill();
        float punch = Mathf.Clamp(damage / 40.0f, 0.14f, 0.5f); // ora_ora ~0.19 .. ground_breaker 0.5
        float dur = 0.18f + punch * 0.12f;
        sprite.Scale = new Vector2(1.0f + punch, 1.0f - punch); // squash: wider + shorter, pivots at the feet
        _reactTw = CreateTween();
        _reactTw.TweenProperty(sprite, "scale", Vector2.One, dur)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); // springy recover
        FlashSprite(sprite, Combat.DamageFlash, dur); // prominent colour flash via the material's `flash` uniform
    }

    /// <summary>The body height a victim-VFX scene is authored against; `fitH` scales the spawned effect from this.</summary>
    private const float VictimVfxRefH = 34.0f;

    /// <summary>
    /// Spawn a hit's custom VFX ON this body — the dynamic per-attack hurt reaction. Parents `scene` to us
    /// (it tracks our position) and frees it after `duration` with a fade (0 = a self-freeing one-shot). The
    /// effect's ROOT position is an offset from our FEET (sprites are feet-anchored). `fitH` (0 = none) scales
    /// it to the victim. `recolor` honours Khalid's power-colour picks (set only for HIS powers on an enemy).
    /// </summary>
    public void SpawnVictimVfx(PackedScene scene, float duration, float fitH = 0.0f, bool recolor = false)
    {
        if (scene == null)
            return;
        var fx = scene.Instantiate();
        if (recolor)
            VfxPalette.RecolorTree(fx);
        AddChild(fx);
        if (fitH > 0.0f && fx is Node2D n)
            n.Scale = Vector2.One * (fitH / VictimVfxRefH);
        if (duration <= 0.0f)
            return; // a self-freeing one-shot (a Strike / particle burst)
        // Otherwise WE own its lifetime: wait `duration`, fade, then free — all on ONE Tween BOUND to fx
        // (auto-killed if fx frees; the callback is fx.QueueFree, a method group). No capturing SceneTreeTimer
        // lambda, which could be GC'd before firing.
        var tw = fx.CreateTween();
        tw.TweenInterval(duration);
        if (fx is CanvasItem)
            tw.TweenProperty(fx, "modulate:a", 0.0, 0.4);
        tw.TweenCallback(Callable.From(fx.QueueFree));
    }
}
