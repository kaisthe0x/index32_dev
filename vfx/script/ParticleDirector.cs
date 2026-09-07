using Godot;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;
using GArr = Godot.Collections.Array;

namespace MyGame;

/// <summary>
/// Spawns 2D particle effects at authored positions during authored animation frames, so VFX layer over the
/// drawn sprites. Driven by the Emitters config (character table keyed id → animation → [rows]); a row is
/// sustained (emit while a listed frame shows) or burst (a one-shot on frame entry). Also fires frame-synced
/// SFX (SfxCharacters.FRAMES) and injects the player's resolved hit tuning into an effect's Hitbox. C# port of
/// <c>vfx/script/particle_director.gd</c>. Reads <see cref="Emitters"/>/<see cref="SfxCharacters"/>/<see cref="VfxPalette"/>/
/// <see cref="Combat"/> directly — no GDScript bridges.
/// </summary>
public partial class ParticleDirector : Node2D
{
	private sealed class Sustained
	{
		public Node2D node; public List<Node> emitters; public string anim; public List<int> frames;
		public Vector2 pos; public GDict baseCap; public List<Hitbox> hitboxes; public bool active;
	}

	private sealed class Burst
	{
		public string anim; public List<int> frames; public Vector2 pos; public PackedScene scene;
		public string node; public GDict set; public GDict boost; public bool conform_to_ground; public bool follow;
	}

	private AnimatedSprite2D _sprite;
	private readonly List<Sustained> _sustained = new();
	private readonly List<Burst> _bursts = new();
	private GDict _sfxFrames = new(); // anim -> { emitted_frame -> cue_key }

	private GDict _sfxCharsFrames;
	private Sfx _sfx;

	/// <summary>Wire the director to a player sprite; watch frame/animation changes. Call once, then set_character().</summary>
	public void setup(AnimatedSprite2D sprite)
	{
		_sprite = sprite;
		_sfxCharsFrames = SfxCharacters.FRAMES;
		_sfx = GetNode<Sfx>("/root/Sfx");
		_sprite.FrameChanged += Refresh;
		_sprite.AnimationChanged += Refresh;
	}

	/// <summary>Rebuild the emitter set for a character (on swap).</summary>
	public void set_character(string id)
	{
		foreach (var entry in _sustained)
			if (IsInstanceValid(entry.node))
				entry.node.QueueFree();
		_sustained.Clear();
		_bursts.Clear();
		BuildSfxFrames(id);

		var byAnim = Emitters.Character(id);
		foreach (var animK in byAnim.Keys)
		{
			if (byAnim[animK].VariantType != Variant.Type.Array)
				continue;
			string anim = animK.AsString();
			int start = SheetStart(anim);
			foreach (Variant rowV in byAnim[animK].As<GArr>())
			{
				var row = rowV.As<GDict>();
				var frames = FramesFor(anim, row.ContainsKey("frames") ? row["frames"] : new GArr(), start);
				Vector2 pos = row["pos"].As<Vector2>();
				var scene = row["scene"].As<PackedScene>();
				var boost = row.ContainsKey("boost") ? row["boost"].As<GDict>() : new GDict();
				string mode = row.ContainsKey("mode") ? row["mode"].AsString() : "burst";
				if (mode == "sustained")
				{
					var node = Spawn(scene, row.ContainsKey("node") ? row["node"].AsString() : "");
					if (node == null)
						continue;
					ApplyOverrides(node, row.ContainsKey("set") ? row["set"].As<GDict>() : new GDict());
					var emitters = EmittersOf(node);
					foreach (var em in emitters)
					{
						Boost((Node2D)em, boost);
						SetEmitting(em, false);
					}
					AddChild(node);
					var hitboxes = HitboxesOf(node);
					foreach (var hb in hitboxes)
						hb.source = Attacker();
					_sustained.Add(new Sustained
					{
						node = node,
						emitters = emitters,
						anim = anim,
						frames = frames,
						pos = pos,
						baseCap = Capture(node),
						hitboxes = hitboxes,
						active = false,
					});
				}
				else
				{
					_bursts.Add(new Burst
					{
						anim = anim,
						frames = frames,
						pos = pos,
						scene = scene,
						node = row.ContainsKey("node") ? row["node"].AsString() : "",
						set = row.ContainsKey("set") ? row["set"].As<GDict>() : new GDict(),
						boost = boost,
						conform_to_ground = row.ContainsKey("conform_to_ground") && row["conform_to_ground"].AsBool(),
						follow = row.ContainsKey("follow") && row["follow"].AsBool(),
					});
				}
			}
		}
		Refresh();
	}

	private void BuildSfxFrames(string id)
	{
		_sfxFrames = new GDict();
		var byAnim = _sfxCharsFrames.ContainsKey(id) ? _sfxCharsFrames[id].As<GDict>() : new GDict();
		foreach (var animK in byAnim.Keys)
		{
			string anim = animK.AsString();
			int start = SheetStart(anim);
			var emap = new GDict();
			var frames = byAnim[animK].As<GDict>();
			foreach (var fK in frames.Keys)
				emap[fK.As<int>() - start] = frames[fK];
			_sfxFrames[anim] = emap;
		}
	}

	private List<int> FramesFor(string anim, Variant raw, int start)
	{
		var outL = new List<int>();
		if (raw.VariantType == Variant.Type.String && raw.AsString() == "all")
		{
			var sf = _sprite.SpriteFrames;
			if (sf != null && sf.HasAnimation(anim))
				for (int e = 0; e < sf.GetFrameCount(anim); e++)
					outL.Add(e);
		}
		else
		{
			foreach (Variant f in raw.As<GArr>())
				outL.Add(f.As<int>() - start);
		}
		return outL;
	}

	private int SheetStart(string anim)
	{
		var sf = _sprite.SpriteFrames;
		if (sf != null && sf.HasMeta("sheet_start"))
			return sf.GetMeta("sheet_start").As<GDict>() is { } m && m.ContainsKey(anim) ? m[anim].As<int>() : 0;
		return 0;
	}

	/// <summary>Instantiate an effect from its preloaded scene; optionally lift one named child of a palette scene.</summary>
	private Node2D Spawn(PackedScene scene, string nodeName = "")
	{
		if (scene == null)
			return null;
		var root = scene.Instantiate();
		var node = root as Node2D;
		if (nodeName != "")
		{
			var child = root.GetNodeOrNull<Node2D>(nodeName);
			if (child == null)
			{
				GD.PushWarning($"ParticleDirector: palette {scene.ResourcePath} has no child '{nodeName}'");
				root.QueueFree();
				return null;
			}
			root.RemoveChild(child);
			child.Owner = null;
			root.QueueFree();
			node = child;
		}
		if (EmittersOf(node).Count == 0 && node is not Projectile && node is not Strike)
		{
			GD.PushWarning($"ParticleDirector: {scene.ResourcePath} has no CPU/GPUParticles2D and is not a Projectile/Strike");
			node.QueueFree();
			return null;
		}
		VfxPalette.RecolorTree(node); // honour power-colour picks (no-op without picks)
		return node;
	}

	/// <summary>Pull any "Trail" child onto the DIRECTOR so it FOLLOWS the player (a dash trail), mirrored + brief.</summary>
	private void SpawnFollowers(Node2D root, float m)
	{
		foreach (var childN in root.GetChildren())
		{
			if (childN.Name != "Trail" || childN is not Node2D f)
				continue;
			Vector2 authored = f.Position;
			root.RemoveChild(f);
			f.Owner = null;
			AddChild(f);
			Face(f, Capture(f), authored, m);
			var ems = EmittersOf(f);
			foreach (var em in ems)
			{
				SetOneShot(em, true);
				SetEmitting(em, true);
			}
			FreeWhenDone(f, ems);
		}
	}

	private List<Node> EmittersOf(Node root)
	{
		var o = new List<Node>();
		if (root.IsClass("CPUParticles2D"))
			o.Add(root);
		foreach (var n in root.FindChildren("*", "CPUParticles2D", true, false))
			o.Add(n);
		if (root.IsClass("GPUParticles2D"))
			o.Add(root);
		foreach (var n in root.FindChildren("*", "GPUParticles2D", true, false))
			o.Add(n);
		return o;
	}

	private List<Hitbox> HitboxesOf(Node root)
	{
		var o = new List<Hitbox>();
		if (root is Hitbox hbRoot)
			o.Add(hbRoot);
		foreach (var a in root.FindChildren("*", "Area2D", true, false))
			if (a is Hitbox hb)
				o.Add(hb);
		return o;
	}

	private Node Attacker() => GetParent();

	private void InjectTuning(Node2D node, List<Hitbox> hitboxes)
	{
		// The director is a child of the player, so the attacker IS the player (its resolved tuning feeds the hits).
		if (Attacker() is not Player atk)
			return;
		SegmentData hit = atk.active_hit();
		if (hit == null)
			return;
		if (node is ITunable tn)
		{
			tn.apply_tuning(hit, atk);
			return;
		}
		foreach (var hb in hitboxes)
		{
			if (hit.Damage.HasValue) hb.damage = hit.Damage.Value;
			// Multiplier applied OVER the hitbox's own baked damage (the slam scales BOTH its boxes by plunge
			// height this way). Runs after `damage` so an explicit value can still be set first.
			if (hit.DamageScale.HasValue) hb.damage *= hit.DamageScale.Value;
			if (hit.Knockback.HasValue) hb.knockback = hit.Knockback.Value;
			if (hit.Stun.HasValue) hb.stun = hit.Stun.Value;
			if (hit.Color.HasValue)
			{
				hb.status_color = hit.Color.Value;
				hb.status_time = hit.ColorTime ?? hit.Stun ?? 0.0f;
			}
		}
	}

	private Node World()
	{
		var p = GetParent();
		return p?.GetParent();
	}

	private float Mirror() => _sprite.FlipH ? -1.0f : 1.0f;

	private void Boost(Node2D node, GDict boost)
	{
		if (boost.Count == 0)
			return;
		float BF(string k, float d) => boost.ContainsKey(k) ? boost[k].As<float>() : d;
		node.Set("amount", Mathf.Max(1, Mathf.RoundToInt(node.Get("amount").As<int>() * BF("amount", 1.0f))));
		node.Set("lifetime", node.Get("lifetime").As<double>() * BF("lifetime", 1.0f));
		if (boost.ContainsKey("explosiveness"))
			node.Set("explosiveness", (double)BF("explosiveness", 0.0f));
		if (node is CpuParticles2D)
		{
			ScaleMinMaxPair(node, "initial_velocity_min", "initial_velocity_max", BF("speed", 1.0f));
			ScaleMinMaxPair(node, "scale_amount_min", "scale_amount_max", BF("scale", 1.0f));
		}
		else if (boost.ContainsKey("speed") || boost.ContainsKey("scale"))
		{
			GD.PushWarning("ParticleDirector: 'speed'/'scale' boost needs a CPUParticles2D");
		}
	}

	private void ApplyOverrides(Node2D node, GDict overrides)
	{
		foreach (var keyV in overrides.Keys)
		{
			string key = keyV.AsString();
			int idx = key.LastIndexOf(':');
			string prop = idx >= 0 ? key.Substring(idx + 1) : key;
			string path = idx >= 0 ? key.Substring(0, idx) : "";
			Node target = path != "" ? node.GetNodeOrNull(path) : node;
			if (target == null)
			{
				GD.PushWarning($"ParticleDirector: override '{key}' -- no such child");
				continue;
			}
			Variant value = overrides[keyV];
			if (value.VariantType == Variant.Type.String && value.AsString().StartsWith("res://"))
				value = GD.Load<Resource>(value.AsString());
			target.Set(prop, value);
		}
	}

	private GDict Capture(Node2D node)
	{
		if (node is CpuParticles2D cp)
			return new GDict { { "dir", cp.Direction }, { "grav", cp.Gravity } };
		return new GDict { { "rot", node.Rotation } };
	}

	private void Face(Node2D node, GDict baseCap, Vector2 pos, float m)
	{
		node.Position = new Vector2(pos.X * m, pos.Y);
		if (node is Projectile)
		{
			node.Scale = new Vector2(m, node.Scale.Y);
		}
		else if (node is CpuParticles2D cp)
		{
			Vector2 dir = baseCap["dir"].As<Vector2>();
			Vector2 grav = baseCap["grav"].As<Vector2>();
			cp.Direction = new Vector2(dir.X * m, dir.Y);
			cp.Gravity = new Vector2(grav.X * m, grav.Y);
		}
		else
		{
			node.Scale = new Vector2(m, node.Scale.Y);
			node.Rotation = (baseCap.ContainsKey("rot") ? baseCap["rot"].As<float>() : node.Rotation) * m;
		}
	}

	private void Refresh()
	{
		string anim = _sprite.Animation;
		int frame = _sprite.Frame;
		float m = Mirror();

		foreach (var entry in _sustained)
		{
			if (!IsInstanceValid(entry.node))
				continue;
			bool on = entry.anim == anim && entry.frames.Contains(frame);
			Face(entry.node, entry.baseCap, entry.pos, m);
			foreach (var em in entry.emitters)
				SetEmitting(em, on);
			if (on != entry.active)
			{
				if (on)
					InjectTuning(entry.node, entry.hitboxes);
				foreach (var hb in entry.hitboxes)
				{
					if (on)
						hb.activate();
					else
						hb.deactivate();
				}
				entry.active = on;
			}
		}

		foreach (var b in _bursts)
			if (b.anim == anim && b.frames.Contains(frame))
				FireBurst(b, m);

		var emapV = _sfxFrames.ContainsKey(anim) ? _sfxFrames[anim].As<GDict>() : new GDict();
		string cue = emapV.ContainsKey(frame) ? emapV[frame].AsString() : "";
		if (cue != "")
			_sfx.play_at(cue, GlobalPosition, 0.0f, 1.0f);
	}

	/// <summary>Fire the burst emitters configured under `anim` now, as a code-driven one-shot (an event, not a frame).</summary>
	public void fire_effect(string anim, float tilt = 0.0f)
	{
		float m = Mirror();
		foreach (var b in _bursts)
			if (b.anim == anim)
				FireBurst(b, m, tilt);
	}

	private void FireBurst(Burst b, float m, float tilt = 0.0f)
	{
		var node = Spawn(b.scene, b.node);
		if (node == null)
			return;
		ApplyOverrides(node, b.set);
		if (node is LobProjectile lob)
		{
			LaunchLob(lob, b, m);
			return;
		}
		SpawnFollowers(node, m);
		var emitters = EmittersOf(node);
		if (emitters.Count == 0 && HitboxesOf(node).Count == 0 && node is not Projectile && node is not Strike)
		{
			node.QueueFree();
			return;
		}
		Face(node, Capture(node), b.pos, m);
		if (!Mathf.IsZeroApprox(tilt))
			node.Rotation += tilt;
		foreach (var em in emitters)
			Boost((Node2D)em, b.boost);
		float emitDur = node is BlastStrike bs ? bs.emit_duration : 0.0f;
		Vector2 target = GlobalPosition + new Vector2(b.pos.X * m, b.pos.Y);
		var world = World();
		if (b.follow || world == null)
			AddChild(node);
		else
			world.AddChild(node);
		PlaceAt(node, target);
		var hitboxes = HitboxesOf(node);
		if (b.conform_to_ground &&
			!GroundContour.Conform(node, IsInsideTree() ? GetWorld2D().DirectSpaceState : null))
		{
			node.QueueFree(); // AoE landed over a pit / no ground — don't emit or hit
			return;
		}
		foreach (var em in emitters)
		{
			SetOneShot(em, emitDur <= 0.0f);
			SetEmitting(em, true);
			if (emitDur > 0.0f)
			{
				var emCap = em;
				GetTree().CreateTimer(emitDur).Timeout += () =>
				{
					if (IsInstanceValid(emCap))
						SetEmitting(emCap, false);
				};
			}
		}
		InjectTuning(node, hitboxes);
		foreach (var hb in hitboxes)
		{
			hb.source = Attacker();
			hb.activate();
		}
		if (node is not Projectile && node is not Strike)
			FreeWhenDone(node, emitters);
	}

	private void LaunchLob(LobProjectile lob, Burst b, float m)
	{
		var atk = Attacker();
		lob.source = atk;
		if (atk is Player p && p.active_hit() is SegmentData hit)
		{
			if (hit.Damage.HasValue) lob.explosion_damage = hit.Damage.Value;
			if (hit.Knockback.HasValue) lob.explosion_knockback = hit.Knockback.Value;
			if (hit.Stun.HasValue) lob.explosion_stun = hit.Stun.Value;
		}
		Vector2 muzzle = GlobalPosition + new Vector2(b.pos.X * m, b.pos.Y);
		lob.target = NearestEnemyPos(muzzle, m);
		var world = World();
		if (world != null)
			world.AddChild(lob);
		else
			AddChild(lob);
		PlaceAt(lob, muzzle);
	}

	private Vector2 NearestEnemyPos(Vector2 from, float m)
	{
		Vector2 best = new(float.PositiveInfinity, float.PositiveInfinity);
		float bestD = float.PositiveInfinity;
		foreach (Node e in GetTree().GetNodesInGroup("enemies"))
			if (e is Node2D e2)
			{
				float d = from.DistanceSquaredTo(e2.GlobalPosition);
				if (d < bestD)
				{
					bestD = d;
					best = e2.GlobalPosition;
				}
			}
		return float.IsFinite(best.X) ? best : from + new Vector2(120.0f * m, 20.0f);
	}

	private void FreeWhenDone(Node root, List<Node> emitters)
	{
		int[] left = { emitters.Count };
		// CPU/GPUParticles2D expose `finished` with DIFFERENT C# delegate types, so connect by name.
		Callable handler = Callable.From(() =>
		{
			left[0]--;
			if (left[0] <= 0 && IsInstanceValid(root))
				root.QueueFree();
		});
		foreach (var em in emitters)
			em.Connect("finished", handler);
	}

	public override void _Process(double delta)
	{
		if (_sustained.Count == 0)
			return;
		float m = Mirror();
		foreach (var entry in _sustained)
			if (IsInstanceValid(entry.node))
				Face(entry.node, entry.baseCap, entry.pos, m);
	}

	// --- small helpers (inlined from Nodes/MathUtil) --------------------------
	private static void SetEmitting(Node em, bool on)
	{
		if (em is CpuParticles2D cp) cp.Emitting = on;
		else if (em is GpuParticles2D gp) gp.Emitting = on;
	}

	private static void SetOneShot(Node em, bool on)
	{
		if (em is CpuParticles2D cp) cp.OneShot = on;
		else if (em is GpuParticles2D gp) gp.OneShot = on;
	}

	private static void PlaceAt(Node2D node, Vector2 pos)
	{
		node.GlobalPosition = pos;
		node.ResetPhysicsInterpolation();
	}

	private static void ScaleMinMaxPair(Node2D obj, string minProp, string maxProp, float f)
	{
		if (Mathf.IsEqualApprox(f, 1.0f))
			return;
		float lo = obj.Get(minProp).As<float>() * f;
		float hi = obj.Get(maxProp).As<float>() * f;
		if (f >= 1.0f) { obj.Set(maxProp, hi); obj.Set(minProp, lo); }
		else { obj.Set(minProp, lo); obj.Set(maxProp, hi); }
	}
}
