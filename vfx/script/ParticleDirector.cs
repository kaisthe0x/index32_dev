using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// Spawns 2D particle effects at authored positions during authored animation frames, so VFX layer over the
/// drawn sprites. Driven by the Emitters config (character table keyed id → animation → [rows]); a row is
/// sustained (emit while a listed frame shows) or burst (a one-shot on frame entry). Also fires frame-synced
/// SFX (SfxCharacters.Frames) and injects the player's resolved hit tuning into an effect's Hitbox. C# port of
/// <c>vfx/script/particle_director.gd</c>. Reads <see cref="Emitters"/>/<see cref="SfxCharacters"/>/<see cref="VfxPalette"/>/
/// <see cref="Combat"/> directly — no GDScript bridges.
/// </summary>
public partial class ParticleDirector : Node2D
{
	/// <summary>How an effect was authored facing RIGHT, kept so it can be mirrored each time: a CPU emitter's
	/// direction and gravity, or any other node's rotation.</summary>
	private readonly record struct BasePose(Vector2 Direction, Vector2 Gravity, float Rotation);

	/// <summary>A sustained row's live effect: spawned once, parented to the director, switched on while one of its
	/// frames is showing.</summary>
	private sealed class Sustained(Node2D node, List<Node> emitters, string anim, List<int> frames, Vector2 pos,
		BasePose basePose, List<Hitbox> hitboxes)
	{
		public readonly Node2D Root = node;
		public readonly List<Node> Emitters = emitters;
		public readonly string Anim = anim;
		public readonly List<int> Frames = frames;
		public readonly Vector2 Pos = pos;
		public readonly BasePose BasePose = basePose;
		public readonly List<Hitbox> Hitboxes = hitboxes;
		public bool Active;
	}

	/// <summary>A burst row bound to its animation, with its frames converted to emitted indices.</summary>
	private sealed record Burst(string Anim, List<int> Frames, EmitterDef Def);

	private AnimatedSprite2D _sprite = null!;
	private readonly List<Sustained> _sustained = new();
	private readonly List<Burst> _bursts = new();
	private readonly Dictionary<string, Dictionary<int, string>> _sfxFrames = new(); // anim -> { emitted_frame -> cue_key }

	private Sfx _sfx = null!;

	/// <summary>Wire the director to a player sprite; watch frame/animation changes. Call once, then SetCharacter().</summary>
	public void Setup(AnimatedSprite2D sprite)
	{
		_sprite = sprite;
		_sfx = GetNode<Sfx>("/root/Sfx");
		_sprite.FrameChanged += Refresh;
		_sprite.AnimationChanged += Refresh;
	}

	/// <summary>Rebuild the emitter set for a character (on swap).</summary>
	public void SetCharacter(string id)
	{
		foreach (var entry in _sustained)
			if (IsInstanceValid(entry.Root))
				entry.Root.QueueFree();
		_sustained.Clear();
		_bursts.Clear();
		BuildSfxFrames(id);

		foreach (var (anim, rows) in Emitters.Character(id))
		{
			int start = SheetStart(anim);
			foreach (EmitterDef row in rows)
			{
				var frames = FramesFor(anim, row, start);
				if (row.Mode != EmitterMode.Sustained)
				{
					_bursts.Add(new Burst(anim, frames, row));
					continue;
				}
				var node = Spawn(row);
				if (node == null)
					continue;
				var emitters = EmittersOf(node);
				foreach (var em in emitters)
					ParticleNodes.SetEmitting(em, false);
				AddChild(node);
				var hitboxes = HitboxesOf(node);
				foreach (var hb in hitboxes)
					hb.Source = Attacker();
				_sustained.Add(new Sustained(node, emitters, anim, frames, row.Pos, Capture(node), hitboxes));
			}
		}
		Refresh();
	}

	private void BuildSfxFrames(string id)
	{
		_sfxFrames.Clear();
		if (!SfxCharacters.Frames.TryGetValue(id, out var byAnim))
			return;
		foreach (var (anim, frames) in byAnim)
		{
			int start = SheetStart(anim);
			var emap = new Dictionary<int, string>();
			foreach (var (sheetFrame, cue) in frames)
				emap[sheetFrame - start] = cue;
			_sfxFrames[anim] = emap;
		}
	}

	/// <summary>The EMITTED animation frames <paramref name="row"/> plays on: every frame of <paramref name="anim"/>,
	/// or its sheet-relative frames shifted by the frames the sprite generator dropped from the start.</summary>
	private List<int> FramesFor(string anim, EmitterDef row, int start)
	{
		var frames = new List<int>();
		if (row.AllFrames)
		{
			var sf = _sprite.SpriteFrames;
			if (sf != null && sf.HasAnimation(anim))
				for (int e = 0; e < sf.GetFrameCount(anim); e++)
					frames.Add(e);
		}
		else
		{
			foreach (int sheetFrame in row.Frames)
				frames.Add(sheetFrame - start);
		}
		return frames;
	}

	private int SheetStart(string anim) => AnimMeta.SheetStart(_sprite.SpriteFrames, anim);

	/// <summary>Instantiate a row's effect, recoloured to the power picks and with the row's typed settings applied;
	/// null (with a warning) if the scene is nothing the director can drive.</summary>
	private Node2D? Spawn(EmitterDef row)
	{
		if (row.Scene.Instantiate() is not Node2D node)
			return null;
		if (EmittersOf(node).Count == 0 && node is not Projectile && node is not Strike && node is not LobProjectile)
		{
			GD.PushWarning($"ParticleDirector: {row.Scene.ResourcePath} has no CPU/GPUParticles2D and is not a Projectile/Strike");
			node.QueueFree();
			return null;
		}
		VfxPalette.RecolorTree(node); // honour power-colour picks (no-op without picks)
		row.Configure?.Invoke(node);
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
				ParticleNodes.SetOneShot(em, true);
				ParticleNodes.SetEmitting(em, true);
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
		SegmentData hit = atk.ActiveHit();
		if (hit == null)
			return;
		if (node is ITunable tn)
		{
			tn.ApplyTuning(hit, atk);
			return;
		}
		foreach (var hb in hitboxes)
		{
			if (hit.Damage.HasValue) hb.Damage = hit.Damage.Value;
			// Multiplier applied OVER the hitbox's own baked damage (the slam scales BOTH its boxes by plunge
			// height this way). Runs after `damage` so an explicit value can still be set first.
			if (hit.DamageScale.HasValue) hb.Damage *= hit.DamageScale.Value;
			if (hit.Knockback.HasValue) hb.Knockback = hit.Knockback.Value;
			if (hit.Stun.HasValue) hb.Stun = hit.Stun.Value;
			if (hit.Color.HasValue)
			{
				hb.StatusColor = hit.Color.Value;
				hb.StatusTime = hit.ColorTime ?? hit.Stun ?? 0.0f;
			}
		}
	}

	/// <summary>The node the player lives in — where a world-anchored burst is parented. Null outside the tree.</summary>
	private Node? World() => GetParent()?.GetParent();

	private float Mirror() => _sprite.FlipH ? -1.0f : 1.0f;

	private static BasePose Capture(Node2D node) =>
		node is CpuParticles2D cp ? new BasePose(cp.Direction, cp.Gravity, 0.0f) : new BasePose(default, default, node.Rotation);

	private static void Face(Node2D node, BasePose basePose, Vector2 pos, float m)
	{
		node.Position = new Vector2(pos.X * m, pos.Y);
		if (node is Projectile)
		{
			node.Scale = new Vector2(m, node.Scale.Y);
		}
		else if (node is CpuParticles2D cp)
		{
			cp.Direction = new Vector2(basePose.Direction.X * m, basePose.Direction.Y);
			cp.Gravity = new Vector2(basePose.Gravity.X * m, basePose.Gravity.Y);
		}
		else
		{
			node.Scale = new Vector2(m, node.Scale.Y);
			node.Rotation = basePose.Rotation * m;
		}
	}

	private void Refresh()
	{
		string anim = _sprite.Animation;
		int frame = _sprite.Frame;
		float m = Mirror();

		foreach (var entry in _sustained)
		{
			if (!IsInstanceValid(entry.Root))
				continue;
			bool on = entry.Anim == anim && entry.Frames.Contains(frame);
			Face(entry.Root, entry.BasePose, entry.Pos, m);
			foreach (var em in entry.Emitters)
				ParticleNodes.SetEmitting(em, on);
			if (on != entry.Active)
			{
				if (on)
					InjectTuning(entry.Root, entry.Hitboxes);
				foreach (var hb in entry.Hitboxes)
				{
					if (on)
						hb.Activate();
					else
						hb.Deactivate();
				}
				entry.Active = on;
			}
		}

		foreach (var b in _bursts)
			if (b.Anim == anim && b.Frames.Contains(frame))
				FireBurst(b, m);

		if (_sfxFrames.TryGetValue(anim, out var emap) && emap.TryGetValue(frame, out string? cue))
			_sfx.PlayAt(cue, GlobalPosition, 0.0f, 1.0f);
	}

	/// <summary>Fire the burst emitters configured under `anim` now, as a code-driven one-shot (an event, not a frame).</summary>
	public void FireEffect(string anim, float tilt = 0.0f)
	{
		float m = Mirror();
		foreach (var b in _bursts)
			if (b.Anim == anim)
				FireBurst(b, m, tilt);
	}

	private void FireBurst(Burst b, float m, float tilt = 0.0f)
	{
		EmitterDef row = b.Def;
		var node = Spawn(row);
		if (node == null)
			return;
		if (node is LobProjectile lob)
		{
			LaunchLob(lob, row.Pos, m);
			return;
		}
		SpawnFollowers(node, m);
		var emitters = EmittersOf(node);
		if (emitters.Count == 0 && HitboxesOf(node).Count == 0 && node is not Projectile && node is not Strike)
		{
			node.QueueFree();
			return;
		}
		Face(node, Capture(node), row.Pos, m);
		if (!Mathf.IsZeroApprox(tilt))
			node.Rotation += tilt;
		float emitDur = node is BlastStrike bs ? bs.EmitDuration : 0.0f;
		Vector2 target = GlobalPosition + new Vector2(row.Pos.X * m, row.Pos.Y);
		var world = World();
		if (row.Follow || world == null)
			AddChild(node);
		else
			world.AddChild(node);
		Nodes.PlaceAt(node, target);
		var hitboxes = HitboxesOf(node);
		if (row.ConformToGround &&
			!GroundContour.Conform(node, IsInsideTree() ? GetWorld2D().DirectSpaceState : null))
		{
			node.QueueFree(); // AoE landed over a pit / no ground — don't emit or hit
			return;
		}
		foreach (var em in emitters)
		{
			ParticleNodes.SetOneShot(em, emitDur <= 0.0f);
			ParticleNodes.SetEmitting(em, true);
			if (emitDur > 0.0f)
			{
				var emCap = em;
				GetTree().CreateTimer(emitDur).Timeout += () =>
				{
					if (IsInstanceValid(emCap))
						ParticleNodes.SetEmitting(emCap, false);
				};
			}
		}
		InjectTuning(node, hitboxes);
		foreach (var hb in hitboxes)
		{
			hb.Source = Attacker();
			hb.Activate();
		}
		if (node is not Projectile && node is not Strike)
			FreeWhenDone(node, emitters);
	}

	private void LaunchLob(LobProjectile lob, Vector2 pos, float m)
	{
		var atk = Attacker();
		lob.Source = atk;
		if (atk is Player p && p.ActiveHit() is SegmentData hit)
		{
			if (hit.Damage.HasValue) lob.ExplosionDamage = hit.Damage.Value;
			if (hit.Knockback.HasValue) lob.ExplosionKnockback = hit.Knockback.Value;
			if (hit.Stun.HasValue) lob.ExplosionStun = hit.Stun.Value;
		}
		Vector2 muzzle = GlobalPosition + new Vector2(pos.X * m, pos.Y);
		lob.Target = NearestEnemyPos(muzzle, m);
		var world = World();
		if (world != null)
			world.AddChild(lob);
		else
			AddChild(lob);
		Nodes.PlaceAt(lob, muzzle);
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
		System.Action handler = () =>
		{
			left[0]--;
			if (left[0] <= 0 && IsInstanceValid(root))
				root.QueueFree();
		};
		foreach (var em in emitters)
			ParticleNodes.OnFinished(em, handler);
	}

	public override void _Process(double delta)
	{
		if (_sustained.Count == 0)
			return;
		float m = Mirror();
		foreach (var entry in _sustained)
			if (IsInstanceValid(entry.Root))
				Face(entry.Root, entry.BasePose, entry.Pos, m);
	}
}
