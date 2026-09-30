namespace MyGame;

/// <summary>
/// One Needle Point shot. Each purchase raises it one RANK, for the rest of the run: <paramref name="Levels"/> holds the
/// stat value at each rank (index 0 = rank I, the first purchase; its length = the max rank). <paramref name="Effect"/>
/// is the player-facing line with <c>{0}</c> for the formatted value (<see cref="Shot.FormatValue"/>). Rank I costs
/// <paramref name="Price"/> Lira; every rank after costs more (<see cref="NeedlePoint.PriceOfNext"/>).
/// </summary>
public sealed record ShotDef(string Id, string Name, string Effect, ShotStat Stat, float[] Levels, int Price)
{
    public int MaxRank => Levels.Length;
}
