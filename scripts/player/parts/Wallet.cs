namespace MyGame;

/// <summary>
/// What the player has banked this run: <see cref="Lira"/> (the common currency — every kill pays it; the stalls
/// spend it) and <see cref="FadaFigs"/> (the rare one — the mystery box spends it). Emptied when a run begins.
/// The HUD's counters listen to <see cref="Changed"/>.
/// </summary>
public sealed class Wallet
{
    public int Lira { get; private set; }
    public int FadaFigs { get; private set; }

    /// <summary>Either balance changed.</summary>
    public event System.Action? Changed;

    /// <summary>Bank <paramref name="n"/> Lira (a coin reached the player).</summary>
    public void CollectLira(int n)
    {
        Lira += n;
        Changed?.Invoke();
    }

    /// <summary>Try to spend <paramref name="cost"/> Lira. True + deducts if affordable; else false.</summary>
    public bool SpendLira(int cost)
    {
        if (cost < 0 || Lira < cost)
            return false;
        Lira -= cost;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Bank <paramref name="n"/> Fada Fig(s) (the player touched one; the box refunded a spin).</summary>
    public void CollectFadaFigs(int n = 1)
    {
        FadaFigs += n;
        Changed?.Invoke();
    }

    /// <summary>Try to spend <paramref name="cost"/> Fada Figs. True + deducts if affordable; else false.</summary>
    public bool SpendFadaFigs(int cost)
    {
        if (cost <= 0 || FadaFigs < cost)
            return false;
        FadaFigs -= cost;
        Changed?.Invoke();
        return true;
    }

    /// <summary>A new run: nothing banked.</summary>
    public void Empty()
    {
        Lira = 0;
        FadaFigs = 0;
        Changed?.Invoke();
    }
}
