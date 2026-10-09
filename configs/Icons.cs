using Godot;
using System.Collections.Generic;
using GDict = Godot.Collections.Dictionary;

namespace MyGame;

/// <summary>
/// Central ICON registry for things without their own icon field — buffs + status pips. UI asks HERE, so when real
/// art lands you swap a PATH and nothing else changes. Keys are namespaced ("buff:&lt;id&gt;", "status:&lt;id&gt;");
/// textures load lazily + cache. A buff with no entry shows the fallback. C# port of <c>configs/icons.gd</c>.
/// PLACEHOLDER: every path is a stand-in (reused pngs) until the real icons are drawn.
/// </summary>
public static class Icons
{
    private const string Fallback = "res://vfx/shared/textures/soft_dot.png";

    private static readonly GDict PATHS = new()
    {
        // enemy STATUS icons
        { "status:reap", "res://vfx/shared/textures/skull_texture.png" },
        { "status:stun", "res://vfx/shared/textures/z_texture.png" },
        { "status:slow", "res://vfx/shared/textures/forward_arrow_texture.png" },
        { "status:charm", "res://vfx/shared/textures/pixel_ember.png" },
    };

    private static readonly Dictionary<string, Texture2D> Cache = new();

    /// <summary>The texture for a namespaced key ("buff:momentum", "status:stun", …), cached. Unknown = FALLBACK.</summary>
    public static Texture2D Texture(string key)
    {
        string path = PATHS.ContainsKey(key) ? PATHS[key].AsString() : Fallback;
        return LoadCached(ResourceLoader.Exists(path) ? path : Fallback);
    }

    /// <summary>The texture at an explicit res:// PATH (e.g. an Action's embedded icon), cached. Empty/missing = FALLBACK.</summary>
    public static Texture2D LoadPath(string path)
    {
        if (path == "" || !ResourceLoader.Exists(path))
            path = Fallback;
        return LoadCached(path);
    }

    private static Texture2D LoadCached(string path)
    {
        if (Cache.TryGetValue(path, out var tex))
            return tex;
        tex = GD.Load<Texture2D>(path);
        Cache[path] = tex;
        return tex;
    }

    public static Texture2D Status(StatusType status) => Texture($"status:{status.Key()}");
}
