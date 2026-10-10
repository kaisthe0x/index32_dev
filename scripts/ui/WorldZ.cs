namespace MyGame;

/// <summary>
/// The draw order (z_index) of everything in the WORLD — ONE table, so layers can't silently cover each other (the
/// world twin of <see cref="UiLayers"/>, which orders the screens). Higher draws on top. Always use these for anything
/// placed in the arena instead of a literal z.
///
/// <para>Effects PARENTED to a body (a surge's orbit, a swing's trail, a status icon) keep small RELATIVE offsets — about
/// −2 … +2 — meaning "just behind / in front of me". So the gap between <see cref="Terrain"/> and <see cref="Actors"/>
/// must stay wider than that: an orbit's back half (Actors − 1) still draws over the statue, the stalls and the ground.</para>
/// </summary>
public static class WorldZ
{
    public const int Scenery = -30;       // the layout's backdrop props (its "Aesthetic" node: statue, trees, skeleton)
    public const int Stalls = -25;        // things you use — the mystery box, Needle Point, Dekken, launch orbs: in front of
                                          // the scenery, BEHIND the tiles (so rocks, plants and ground sit over their base)
    public const int Decor = -20;         // the layout's Decor tile layer (rocks, plants)
    public const int Terrain = -10;       // the layout's Terrain tile layer (ground, platforms)
    public const int Wildlife = -2;       // birds perched on the ground: over the tiles, under drops and actors
    public const int Drops = -1;          // pickups resting on the ground (fada figs)
    public const int Actors = 0;          // Khalid and the enemies (the default — nothing sets it)
    public const int SpawnFx = 4;         // an enemy's spawn flash
    public const int FlyingPickups = 5;   // Lira coins + Ruh souls on their way to Khalid
    public const int Impacts = 50;        // a projectile's hit effect, over the body it struck
    public const int DeathOverlay = 400;  // the death cinematic's dimming overlay …
    public const int DeathPlayer = 500;   // … with Khalid drawn above it
}
