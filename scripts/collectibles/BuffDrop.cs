using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// A collectible BUFF drop — a dying enemy drops one at a RAMPING chance (see <c>RunManager.BuffDropChance</c>). It
/// pops out of the corpse and settles like a Fada Fig (RigidBody bounce), glows in its rarity colour, and grants a
/// RANDOM buff the moment the player touches it. The buff (id + tier) is rolled once at spawn so the glow can show
/// its rarity up front. Built ENTIRELY in code (no scene) — a placeholder look (a tinted, pulsing soft dot) until
/// dedicated buff-drop art exists; mirrors <see cref="FadaFig"/>'s pop/settle/pickup feel.
///
/// <para>The pool is the GENERALLY-USEFUL buffs only (<see cref="Pool"/>): BuffCatalog factories whose
/// <see cref="Buff.AppliesTo"/> isn't gated to a SPECIFIC move — so a random drop is never inert for the player's
/// loadout (move-gated buffs stay offer-only). Tier is weighted low: mostly Common, sometimes Rare.</para>
/// </summary>
public partial class BuffDrop : RigidBody2D
{
    private const float PopUpMin = 120.0f, PopUpMax = 200.0f, PopSide = 85.0f;
    private const float LifeSeconds = 30.0f;   // despawn if never collected (avoids clutter)
    private const float RareChance = 0.30f;    // else Common

    private string _buffId;   // null only if the pool is somehow empty (checked before granting)
    private Tier _tier;
    private bool _collected;

    // One shared pulse-glow material for every buff drop + the general-buff pool, computed once.
    private static ShaderMaterial _glowMaterial;
    private static string[] _pool;

    public override void _Ready()
    {
        RollBuff();
        BuildBody();
        // Scatter pop: up + a little sideways, with a spin so it tumbles/rolls before settling.
        LinearVelocity = new Vector2((float)GD.RandRange(-PopSide, PopSide), -(float)GD.RandRange(PopUpMin, PopUpMax));
        AngularVelocity = (float)GD.RandRange(-8.0, 8.0);
        if (LifeSeconds > 0.0f)
            GetTree().CreateTimer(LifeSeconds).Timeout += () => { if (!_collected) QueueFree(); };
    }

    private void RollBuff()
    {
        string[] pool = Pool();
        _buffId = pool.Length > 0 ? pool[GD.Randi() % (uint)pool.Length] : null;
        _tier = GD.Randf() < RareChance ? Tier.Rare : Tier.Common;
    }

    private void BuildBody()
    {
        Mass = 0.2f;
        GravityScale = 1.5f;
        LinearDamp = 0.5f;
        AngularDamp = 3.0f;
        CollisionLayer = 0;
        CollisionMask = (uint)Combat.Layer.World;   // fall + settle on the terrain, like a Fada Fig (never pushes the player)
        PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0.5f, Bounce = 0.35f };

        _glowMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://vfx/shaders/world/pulse_glow.gdshader") };
        AddChild(new Sprite2D
        {
            Texture = GD.Load<Texture2D>("res://vfx/shared/textures/soft_dot.png"),
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
            Scale = new Vector2(0.6f, 0.6f),
            Modulate = Tiers.ColorOf(_tier),   // rarity colour (Common = white)
            Material = _glowMaterial,
        });
        AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 3.0f } });

        var pickup = new Area2D { CollisionLayer = 0, CollisionMask = (uint)Combat.Layer.PlayerBody };
        pickup.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 8.0f } });
        pickup.BodyEntered += OnBodyEntered;
        AddChild(pickup);
    }

    private void OnBodyEntered(Node body)
    {
        if (_collected || body is not Player p || _buffId == null)
            return;
        Buff buff = BuffCatalog.Make(_buffId, _tier);
        if (buff == null)
            return;
        _collected = true;
        p.add_passive(buff);
        FloatingText.Emit(FloatingTextType.Damage, p, new Vector2(0, -46), buff.Name, 0.0f, Tiers.ColorOf(_tier));
        GetNodeOrNull<Sfx>("/root/Sfx")?.play_at("fada_fig_collect", GlobalPosition);   // PLACEHOLDER sfx (no buff cue yet)
        QueueFree();
    }

    /// <summary>Generally-useful buff ids, computed once: every implemented buff whose <see cref="Buff.AppliesTo"/>
    /// isn't gated to a SPECIFIC move (only the "*"/"attack"/"special" keywords, or none) — so a random drop is always
    /// meaningful. The move-gated buffs (Overcharge/Instant Reset/Wider Pull) stay offer-only.</summary>
    private static string[] Pool()
    {
        if (_pool != null)
            return _pool;
        var ids = new List<string>();
        foreach (string id in BuffCatalog.FACTORIES.Keys)
        {
            if (BuffCatalog.Make(id, Tier.Common) is not Buff b)
                continue;
            bool general = true;
            foreach (string a in b.AppliesTo)
                if (a != "*" && a != "attack" && a != "special") { general = false; break; }
            if (general)
                ids.Add(id);
        }
        _pool = ids.ToArray();
        return _pool;
    }
}
