using Godot;
using System.Collections.Generic;

namespace MyGame;

/// <summary>
/// One saved colour scheme from the picker: three sets of picks, each key → the chosen colour. <see cref="Body"/>
/// is keyed by <see cref="PaletteConfig.MATERIALS"/>, <see cref="Power"/> by the <see cref="VfxPalette"/> family,
/// <see cref="Ui"/> by <see cref="UiStyle.PickFrame"/> / <see cref="UiStyle.PickAccent"/>. A missing key means
/// "the default"; a scheme with no picks at all is an unused slot.
/// </summary>
public sealed record ColorScheme(
    IReadOnlyDictionary<string, Color> Body,
    IReadOnlyDictionary<string, Color> Power,
    IReadOnlyDictionary<string, Color> Ui)
{
    /// <summary>No picks: the built-in default look, and what an unused slot holds.</summary>
    public static readonly ColorScheme Empty =
        new(new Dictionary<string, Color>(), new Dictionary<string, Color>(), new Dictionary<string, Color>());

    public bool IsEmpty => Body.Count == 0 && Power.Count == 0 && Ui.Count == 0;
}
