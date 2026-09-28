namespace MyGame;

/// <summary>
/// One Needle Point shot. <paramref name="Levels"/> holds the stat value at each upgrade level (index 0 = the base
/// level, grey; its length − 1 = the max level). <paramref name="Effect"/> is the player-facing line with <c>{0}</c> for
/// the formatted value (<see cref="Shot.FormatValue"/>). A bought shot lasts <paramref name="Rounds"/> rounds and costs
/// <paramref name="Price"/> Lira at level 0 (each level raises it — <see cref="NeedlePoint.PriceAt"/>).
/// </summary>
public sealed record ShotDef(string Id, string Name, string Effect, ShotStat Stat, float[] Levels, int Rounds, int Price)
{
    public int MaxLevel => Levels.Length - 1;
}
