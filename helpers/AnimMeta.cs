using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// Readers for the per-animation metadata the sprite generator writes into each SpriteFrames (see
/// tools/gen_spriteframes.py). Both the player combo/strike logic and enemy attack timing read the same maps.
/// The metadata is stored on the resource as engine dictionaries; it is parsed ONCE per SpriteFrames into typed
/// tables, so the per-frame readers allocate nothing.
/// </summary>
public static class AnimMeta
{
    private sealed class Parsed
    {
        public readonly Dictionary<StringName, int[]> HitFrames = new();
        public readonly Dictionary<StringName, int[]> EveryFrame = new();   // filled on demand by HitFramesOrAll
        public readonly Dictionary<StringName, int> SheetStart = new();
        public readonly Dictionary<StringName, int> LoopFrom = new();
        public readonly Dictionary<StringName, int> LoopTo = new();
    }

    private static readonly ConditionalWeakTable<SpriteFrames, Parsed> Cache = new();
    private static readonly int[] NoFrames = System.Array.Empty<int>();

    /// <summary>The authored hit frames (EMITTED indices) for `anim`, or empty if none.</summary>
    public static IReadOnlyList<int> HitFrames(SpriteFrames? frames, StringName anim) =>
        frames != null && Of(frames).HitFrames.TryGetValue(anim, out var hits) ? hits : NoFrames;

    /// <summary>The authored hit frames for `anim`, or EVERY frame of it when none are authored (a combo with no
    /// hit frames treats each frame as its own segment).</summary>
    public static IReadOnlyList<int> HitFramesOrAll(SpriteFrames frames, StringName anim)
    {
        var parsed = Of(frames);
        if (parsed.HitFrames.TryGetValue(anim, out var hits) && hits.Length > 0)
            return hits;
        if (!parsed.EveryFrame.TryGetValue(anim, out var all))
        {
            all = new int[frames.GetFrameCount(anim)];
            for (int i = 0; i < all.Length; i++)
                all[i] = i;
            parsed.EveryFrame[anim] = all;
        }
        return all;
    }

    /// <summary>How many leading sheet frames were dropped for `anim` (the idle-reference frame 0), or 0.</summary>
    public static int SheetStart(SpriteFrames? frames, StringName anim) =>
        frames != null && Of(frames).SheetStart.TryGetValue(anim, out int start) ? start : 0;

    /// <summary>The frame (EMITTED index) a loop of `anim` restarts at, or -1 if unset.</summary>
    public static int LoopFrom(SpriteFrames? frames, StringName anim) =>
        frames != null && Of(frames).LoopFrom.TryGetValue(anim, out int from) ? from : -1;

    /// <summary>The last frame (EMITTED index, inclusive) of `anim`'s loop, or -1 if unset.</summary>
    public static int LoopTo(SpriteFrames? frames, StringName anim) =>
        frames != null && Of(frames).LoopTo.TryGetValue(anim, out int to) ? to : -1;

    private static Parsed Of(SpriteFrames frames) => Cache.GetValue(frames, Parse);

    private static Parsed Parse(SpriteFrames frames)
    {
        var parsed = new Parsed();
        foreach (var (anim, hits) in Meta(frames, "hit_frames"))
            parsed.HitFrames[anim.AsStringName()] = hits.AsInt32Array();
        ReadInts(frames, "sheet_start", parsed.SheetStart);
        ReadInts(frames, "loop_from", parsed.LoopFrom);
        ReadInts(frames, "loop_to", parsed.LoopTo);
        return parsed;
    }

    private static void ReadInts(SpriteFrames frames, string key, Dictionary<StringName, int> into)
    {
        foreach (var (anim, value) in Meta(frames, key))
            into[anim.AsStringName()] = value.AsInt32();
    }

    private static GDict Meta(SpriteFrames frames, string key) =>
        frames.HasMeta(key) ? frames.GetMeta(key).As<GDict>() : new GDict();
}
