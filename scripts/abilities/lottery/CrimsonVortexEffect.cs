namespace MyGame;

/// <summary>Crimson Vortex: while it lasts, every dash drops the crimson vortex — a spinning red field that hits
/// each enemy caught in it once, for the item's <c>Value</c> (<c>vfx/character/khalid/dash/crimson_vortex/</c>; its
/// size is authored in that scene). A short taste of the dash mechanics the Figmenter holds. It swaps the dash's effect
/// and puts the one he had back when it ends.</summary>
public partial class CrimsonVortexEffect : LotteryEffect
{
    private const string VortexDash = "dash_crimson_vortex"; // its key in EmittersCharacters
    private string _previous = "";
    private SegmentData _previousHit = new();

    public CrimsonVortexEffect(LotteryItemDef def) : base(def) { }

    public override void Setup(Player player)
    {
        _previous = player.DashEffect;
        _previousHit = player.DashHit;
        player.SetDashEffect(VortexDash, new SegmentData { Damage = Def.Value });
    }

    public override void Teardown(Player player) => player.SetDashEffect(_previous, _previousHit);
}
