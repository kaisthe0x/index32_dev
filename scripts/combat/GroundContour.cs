using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// The terrain SURFACE under a ground AoE, sampled as a contour — a contiguous run of <see cref="Sample"/>
/// (world point + surface normal) that follows the ground — plus the operations that reshape an AoE's particle
/// emission and hitbox to hug it. This is what makes a slam / shockwave curve with a slope instead of firing flat.
///
/// <para>Built by walking downward physics rays across a width (so it's robust to ANY <see cref="Combat.Layer.World"/>
/// collision — tiles, slopes, gaps — not just axis-aligned boxes), stepping outward from the impact and stopping at
/// the first gap on each side, giving ONE contiguous band centred on the hit (a shockwave doesn't leap a pit).</para>
///
/// <para><see cref="ParticleDirector"/> builds one contour per ground AoE (emitters flagged conform_to_ground) and
/// applies it to each Rectangle emitter (<see cref="ConformEmitter"/>) and each box hitbox (<see cref="ConformHitbox"/>).</para>
/// </summary>
public sealed class GroundContour
{
    /// <summary>One surface sample: a WORLD-space point on the ground and the surface normal there (points out of the ground).</summary>
    public readonly record struct Sample(Vector2 Point, Vector2 Normal);

    private readonly List<Sample> _samples; // ordered left -> right, contiguous (no gaps)

    private GroundContour(List<Sample> samples) => _samples = samples;

    /// <summary>Fewer than 2 samples can't form a band — nothing to conform to.</summary>
    public bool IsEmpty => _samples.Count < 2;

    // Sampling defaults: step across the footprint this finely, searching this far up/down for the surface.
    private const float DefaultStep = 8.0f;
    private const float DefaultReach = 48.0f;

    /// <summary>
    /// Reshape a whole ground AoE — its Rectangle particle emitters + box hitboxes — to hug the terrain, so it
    /// curves with slopes instead of firing flat. One contour is sampled at the widest footprint and applied to
    /// every part. The single entrypoint both spawn paths use (Khalid's <see cref="ParticleDirector"/> bursts and
    /// an enemy's <c>SpawnAttack</c>). Returns false when there's no ground under the impact — the caller then
    /// discards the effect (an AoE over a pit shouldn't emit or hit). A null space (can't sample) leaves it as authored.
    /// </summary>
    public static bool Conform(Node2D node, PhysicsDirectSpaceState2D space)
    {
        if (node == null || space == null)
            return true;
        var emitters = Gather<CpuParticles2D>(node, "CPUParticles2D"); // engine class name is CPU-caps
        var hitboxes = Gather<Hitbox>(node, "Area2D");

        float halfWidth = 0.0f;
        foreach (var cp in emitters)
            if (cp.EmissionShape == CpuParticles2D.EmissionShapeEnum.Rectangle)
                halfWidth = Mathf.Max(halfWidth, cp.EmissionRectExtents.X * Mathf.Abs(cp.GlobalScale.X));
        foreach (var hb in hitboxes)
            foreach (var csN in hb.FindChildren("*", "CollisionShape2D", true, false))
                if (csN is CollisionShape2D cs && cs.Shape is RectangleShape2D rect)
                    halfWidth = Mathf.Max(halfWidth, rect.Size.X * 0.5f * Mathf.Abs(cs.GlobalScale.X));
        if (halfWidth <= 0.0f)
            return true; // nothing rectangular to conform (e.g. a point burst) — fire as-is

        var contour = Build(space, node.GlobalPosition, halfWidth, DefaultStep, DefaultReach);
        if (contour == null || contour.IsEmpty)
            return false;
        foreach (var cp in emitters)
            contour.ConformEmitter(cp);
        foreach (var hb in hitboxes)
            contour.ConformHitbox(hb);
        return true;
    }

    /// <summary>Root-inclusive gather of nodes of type <typeparamref name="T"/> (matched by its Godot base class).</summary>
    private static List<T> Gather<T>(Node root, string godotClass) where T : Node
    {
        var found = new List<T>();
        if (root is T self)
            found.Add(self);
        foreach (var n in root.FindChildren("*", godotClass, true, false))
            if (n is T t)
                found.Add(t);
        return found;
    }

    /// <summary>
    /// Walk the surface across <c>[center.X ± halfWidth]</c> at <paramref name="step"/> px spacing, outward from the
    /// impact column, stopping at the first gap each side. Each ray searches ±<paramref name="reach"/> around the
    /// previous sample's height, so it tracks curves. Returns null if there's no ground under the impact itself.
    /// </summary>
    public static GroundContour Build(PhysicsDirectSpaceState2D space, Vector2 center, float halfWidth, float step, float reach)
    {
        if (space == null || step <= 0.0f)
            return null;
        if (!Probe(space, center.X, center.Y, reach, out Sample seed))
            return null;

        var left = new List<Sample>();   // strictly-left samples, near -> far
        var right = new List<Sample>();  // strictly-right samples, near -> far
        WalkOut(space, center.X, seed.Point.Y, -step, halfWidth, reach, left);
        WalkOut(space, center.X, seed.Point.Y, +step, halfWidth, reach, right);
        left.Reverse();                  // -> far..near so the whole run reads left -> right

        var all = new List<Sample>(left.Count + 1 + right.Count);
        all.AddRange(left);
        all.Add(seed);
        all.AddRange(right);
        return new GroundContour(all);
    }

    private static void WalkOut(PhysicsDirectSpaceState2D space, float centerX, float prevY, float dx,
        float halfWidth, float reach, List<Sample> outList)
    {
        for (float x = centerX + dx; Mathf.Abs(x - centerX) <= halfWidth + 0.01f; x += dx)
        {
            if (!Probe(space, x, prevY, reach, out Sample s))
                break; // gap / ledge -> the band ends on this side
            outList.Add(s);
            prevY = s.Point.Y;
        }
    }

    private static bool Probe(PhysicsDirectSpaceState2D space, float x, float aroundY, float reach, out Sample sample)
    {
        if (GroundProbe.TryAt(space, x, aroundY, reach, out Vector2 point, out Vector2 normal))
        {
            sample = new Sample(point, normal);
            return true;
        }
        sample = default;
        return false;
    }

    /// <summary>Samples within <paramref name="halfWidth"/> of <paramref name="centerX"/> (world x).</summary>
    private List<Sample> Within(float centerX, float halfWidth)
    {
        var o = new List<Sample>();
        foreach (var s in _samples)
            if (Mathf.Abs(s.Point.X - centerX) <= halfWidth + 0.01f)
                o.Add(s);
        return o;
    }

    /// <summary>
    /// Re-emit <paramref name="cp"/> along the contour instead of from its flat rectangle: emission points hug the
    /// ground and normals point out of the surface, so the burst erupts perpendicular to the slope all along it.
    /// No-op for non-rectangle emitters; silences the emitter if no ground falls under its footprint.
    /// </summary>
    public void ConformEmitter(CpuParticles2D cp)
    {
        if (cp.EmissionShape != CpuParticles2D.EmissionShapeEnum.Rectangle)
            return;
        float halfW = cp.EmissionRectExtents.X * Mathf.Max(Mathf.Abs(cp.GlobalScale.X), 0.001f);
        var run = Within(cp.GlobalPosition.X, halfW);
        if (run.Count < 2)
        {
            cp.EmissionShape = CpuParticles2D.EmissionShapeEnum.Point; // footprint barely on ground -> burst at the impact
            return;
        }
        var points = new Vector2[run.Count];
        var normals = new Vector2[run.Count];
        for (int i = 0; i < run.Count; i++)
        {
            points[i] = cp.ToLocal(run[i].Point);
            // Transform the normal as a DIRECTION (through the emitter's rotation/scale), then re-normalise.
            normals[i] = (cp.ToLocal(run[i].Point + run[i].Normal) - points[i]).Normalized();
        }
        cp.EmissionShape = CpuParticles2D.EmissionShapeEnum.DirectedPoints;
        cp.EmissionPoints = points;
        cp.EmissionNormals = normals;
    }

    /// <summary>
    /// Replace each flat rectangle shape under <paramref name="hb"/> with a polygon BAND that follows the contour
    /// within that rect's width, as tall as the rect (centred on the surface), so the hit fairly covers bodies
    /// standing on the slope. A shape whose footprint has no ground is dropped (that stretch simply doesn't hit).
    /// </summary>
    public void ConformHitbox(Hitbox hb)
    {
        foreach (var node in new List<Node>(hb.FindChildren("*", "CollisionShape2D", true, false)))
        {
            if (node is not CollisionShape2D cs || cs.Shape is not RectangleShape2D rect)
                continue;
            float halfW = rect.Size.X * 0.5f * Mathf.Max(Mathf.Abs(cs.GlobalScale.X), 0.001f);
            float halfH = rect.Size.Y * 0.5f * Mathf.Max(Mathf.Abs(cs.GlobalScale.Y), 0.001f);
            var run = Within(cs.GlobalPosition.X, halfW);
            if (run.Count < 2)
            {
                cs.QueueFree(); // no ground here -> no hit along this stretch
                continue;
            }
            var band = new Vector2[run.Count * 2];
            for (int i = 0; i < run.Count; i++)
                band[i] = hb.ToLocal(run[i].Point + new Vector2(0.0f, -halfH));               // top edge, left -> right
            for (int i = 0; i < run.Count; i++)
                band[run.Count + i] = hb.ToLocal(run[run.Count - 1 - i].Point + new Vector2(0.0f, halfH)); // bottom, right -> left
            var poly = new CollisionPolygon2D { Polygon = band };
            hb.AddChild(poly);
            cs.QueueFree();
        }
    }
}
