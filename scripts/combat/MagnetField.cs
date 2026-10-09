using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// The Come Closer special's effect: on spawn, MAGNETIZE the <see cref="MaxTargets"/> nearest enemies IN FRONT of
/// Khalid (the way he faces) within <see cref="PullRange"/> toward him — each dragged in (Enemy.Magnetize) and
/// STUNNED on arrival. Enemies behind him are never pulled. Self-frees
/// after <see cref="Life"/>. The come_closer scene authors the exports.
/// </summary>
[GlobalClass]
public partial class MagnetField : Node2D
{
    [Export] public float PullRange { get; set; } = 260.0f;
    [Export] public float PullYBand { get; set; } = 48.0f;
    [Export] public int MaxTargets { get; set; } = 1;
    [Export] public float ArriveDist { get; set; } = 64.0f;
    [Export] public float PullSpeed { get; set; } = 340.0f;
    [Export] public float StunTime { get; set; } = 1.5f;
    [Export] public float Life { get; set; } = 1.6f;

    public override void _Ready()
    {
        // Measure the grab from KHALID, not `self`: the director add_child()s us (running this _Ready) and only
        // sets our world position afterwards, so our own global_position isn't final here.
        if (GetTree().GetFirstNodeInGroup("player") is Player khalid)
        {
            Vector2 origin = khalid.GlobalPosition;
            int facing = khalid.Facing;
            // Collect every in-range, same-level enemy IN FRONT (the facing side only), then grab the nearest
            // `MaxTargets` (closest-first).
            var inReach = new List<(Enemy Enemy, float Dist)>();
            foreach (var e in GetTree().GetNodesInGroup("enemies"))
            {
                if (e is not Enemy enemy)
                    continue;
                float dx = (enemy.GlobalPosition.X - origin.X) * facing; // distance ahead; negative = behind him
                if (dx >= 0.0f && dx <= PullRange && Mathf.Abs(enemy.GlobalPosition.Y - origin.Y) <= PullYBand)
                    inReach.Add((enemy, dx));
            }
            inReach.Sort((a, b) => a.Dist.CompareTo(b.Dist));
            // Wider Pull buff bumps the grab count via a run-scoped bonus on the player.
            int targets = MaxTargets + khalid.MagnetTargetBonus;
            int n = Mathf.Min(targets, inReach.Count);
            for (int i = 0; i < n; i++)
                inReach[i].Enemy.Magnetize(khalid, ArriveDist, PullSpeed, StunTime);
        }
        GetTree().CreateTimer(Life).Timeout += QueueFree;
    }
}
