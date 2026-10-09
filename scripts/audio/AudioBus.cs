using Godot;

namespace MyGame;

/// <summary>
/// Runtime control of the audio buses (Master / SFX / Music) — volume, mute, and effects. The visual
/// counterpart is the editor's Audio panel. C# port of <c>scripts/audio/audio_bus.gd</c>. Used only by the C#
/// <see cref="Sfx"/>/<see cref="Music"/> services (a pure static class). Bus names: "Master", "SFX", "Music".
/// </summary>
public static class AudioBus
{
    /// <summary>Set a bus volume from a friendly 0..1 slider value.</summary>
    public static void SetVolumeLinear(StringName bus, float v)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i != -1)
            AudioServer.SetBusVolumeDb(i, Mathf.LinearToDb(Mathf.Clamp(v, 0.0f, 1.0f)));
    }

    /// <summary>The bus volume as a 0..1 value. 1.0 if the bus doesn't exist.</summary>
    public static float GetVolumeLinear(StringName bus)
    {
        int i = AudioServer.GetBusIndex(bus);
        return i == -1 ? 1.0f : Mathf.Clamp(Mathf.DbToLinear(AudioServer.GetBusVolumeDb(i)), 0.0f, 1.0f);
    }

    public static void SetVolumeDb(StringName bus, float db)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i != -1)
            AudioServer.SetBusVolumeDb(i, db);
    }

    public static void SetMuted(StringName bus, bool on)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i != -1)
            AudioServer.SetBusMute(i, on);
    }

    public static bool IsMuted(StringName bus)
    {
        int i = AudioServer.GetBusIndex(bus);
        return i != -1 && AudioServer.IsBusMute(i);
    }

    /// <summary>Add an effect to a bus at runtime and RETURN it (or null if the bus is missing).</summary>
    public static AudioEffect? AddEffect(StringName bus, AudioEffect effect)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i == -1)
            return null;
        AudioServer.AddBusEffect(i, effect);
        return effect;
    }

    public static AudioEffect? GetEffect(StringName bus, int idx = 0)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i == -1 || idx < 0 || idx >= AudioServer.GetBusEffectCount(i))
            return null;
        return AudioServer.GetBusEffect(i, idx);
    }

    public static void SetEffectEnabled(StringName bus, int idx, bool on)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i != -1 && idx >= 0 && idx < AudioServer.GetBusEffectCount(i))
            AudioServer.SetBusEffectEnabled(i, idx, on);
    }
}
