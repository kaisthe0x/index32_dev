namespace MyGame;

/// <summary>
/// Where the HUD's health + Ruh gauge sits — a player setting (pause menu → Settings), persisted by
/// <see cref="SaveData.GetGaugePlacement"/>. Closed set; the HUD switches on it live.
/// </summary>
public enum GaugePlacement
{
    /// <summary>Fixed on screen at bottom-centre, a little below where Khalid stands (the default).</summary>
    Screen,
    /// <summary>In the world, centred under Khalid's feet, moving with him.</summary>
    FollowKhalid,
}
