using Godot;

namespace MyGame;

/// <summary>
/// DEBUG: playtests the buff catalog without the Figmenter — grants every wired <see cref="BuffCatalog"/> buff in turn,
/// and takes the granted ones away again. Driven by <see cref="DebugTools"/> (B / N).
/// </summary>
public sealed class BuffCycler
{
    private readonly Player _player;
    private int _next = 0; // index of the next buff to grant, cycling through the catalog

    public BuffCycler(Player player)
    {
        _player = player;
    }

    /// <summary>Grant the next wired catalog buff, at the middle strength, cycling through the whole set.</summary>
    public void GrantNext()
    {
        var ids = new List<string>(BuffCatalog.Factories.Keys);
        if (ids.Count == 0)
            return;
        string id = ids[_next++ % ids.Count];
        var buff = BuffCatalog.Make(id, WineRarity.Hot);
        if (buff == null)
            return;
        _player.AddPassive(buff);
        GD.Print($"[DEBUG] granted buff: {id}");
    }

    /// <summary>Remove every buff he holds; the character ability and the Needle Point shots stay.</summary>
    public void Clear()
    {
        foreach (var p in _player.Passives.OfType<Buff>().ToList())
            _player.RemovePassive(p);
        GD.Print("[DEBUG] cleared granted buffs");
    }
}
