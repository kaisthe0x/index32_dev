using Godot;

namespace MyGame;

/// <summary>
/// Measures how LOUD a sound effect is, so <see cref="Sfx"/> can bring every cue to one shared level automatically.
/// The measure is the sound's PEAK MOMENTARY LOUDNESS (LUFS): the ITU-R BS.1770 loudness (K-weighted — a filter that
/// hears like a human, lighter on deep bass and a touch hotter up top) of its loudest 400 ms window, stepped every
/// 100 ms. A sound shorter than 400 ms is measured over its whole length. That's the right yardstick for short game
/// SFX: how loud the hit/whoosh/pickup is when it lands, not averaged over a tail of silence.
///
/// <para>Needs the raw PCM: the WAV must be imported UNCOMPRESSED (<c>compress/mode=0</c> — the project default for
/// WAVs). A compressed (QOA/ADPCM) stream can't be measured; <see cref="Measure"/> returns null for it.</para>
/// </summary>
public static class SfxLoudness
{
    private const float WindowSeconds = 0.4f;
    private const float HopSeconds = 0.1f;

    /// <summary>The peak momentary loudness of <paramref name="wav"/> in LUFS; null if it can't be measured (not PCM,
    /// or pure silence).</summary>
    public static float? Measure(AudioStreamWav wav)
    {
        if (wav.Format != AudioStreamWav.FormatEnum.Format16Bits && wav.Format != AudioStreamWav.FormatEnum.Format8Bits)
            return null;
        float[][] channels = Decode(wav);
        int rate = wav.MixRate;
        foreach (float[] ch in channels)
            KWeight(ch, rate);

        int n = channels[0].Length;
        int window = Mathf.Min(n, Mathf.RoundToInt(WindowSeconds * rate));
        int hop = Mathf.Max(1, Mathf.RoundToInt(HopSeconds * rate));
        if (window == 0)
            return null;
        // Running sum of per-sample power (summed over channels, BS.1770 weights L = R = 1).
        var cumulative = new double[n + 1];
        for (int i = 0; i < n; i++)
        {
            double p = 0.0;
            foreach (float[] ch in channels)
                p += (double)ch[i] * ch[i];
            cumulative[i + 1] = cumulative[i] + p;
        }
        double peak = 0.0;
        for (int start = 0; start + window <= n; start += hop)
            peak = System.Math.Max(peak, (cumulative[start + window] - cumulative[start]) / window);
        if (peak <= 0.0)
            return null;
        return (float)(-0.691 + 10.0 * System.Math.Log10(peak));
    }

    /// <summary>The PCM as one float array per channel, in [-1, 1].</summary>
    private static float[][] Decode(AudioStreamWav wav)
    {
        byte[] data = wav.Data;
        int chCount = wav.Stereo ? 2 : 1;
        bool is16 = wav.Format == AudioStreamWav.FormatEnum.Format16Bits;
        int frames = data.Length / (chCount * (is16 ? 2 : 1));
        var outCh = new float[chCount][];
        for (int c = 0; c < chCount; c++)
            outCh[c] = new float[frames];
        for (int f = 0; f < frames; f++)
            for (int c = 0; c < chCount; c++)
            {
                int i = f * chCount + c;
                outCh[c][f] = is16
                    ? (short)(data[2 * i] | (data[2 * i + 1] << 8)) / 32768.0f
                    : (sbyte)data[i] / 128.0f; // Godot stores 8-bit PCM signed
            }
        return outCh;
    }

    /// <summary>Apply the BS.1770 K-weighting in place: a +4 dB high shelf at ~1.5 kHz (the head's effect), then a
    /// ~38 Hz high-pass (the ear's bass roll-off). Coefficients derived for any sample rate.</summary>
    private static void KWeight(float[] x, int rate)
    {
        // Stage 1 — high shelf: gain 4 dB, Q 1/√2, 1500 Hz.
        {
            double a = System.Math.Pow(10.0, 4.0 / 40.0);
            double w0 = 2.0 * System.Math.PI * 1500.0 / rate;
            double cos = System.Math.Cos(w0), alpha = System.Math.Sin(w0) / (2.0 * (1.0 / System.Math.Sqrt(2.0)));
            double sq = 2.0 * System.Math.Sqrt(a) * alpha;
            Biquad(x,
                a * ((a + 1) + (a - 1) * cos + sq), -2 * a * ((a - 1) + (a + 1) * cos), a * ((a + 1) + (a - 1) * cos - sq),
                (a + 1) - (a - 1) * cos + sq, 2 * ((a - 1) - (a + 1) * cos), (a + 1) - (a - 1) * cos - sq);
        }
        // Stage 2 — high pass: Q 0.5, 38 Hz.
        {
            double w0 = 2.0 * System.Math.PI * 38.0 / rate;
            double cos = System.Math.Cos(w0), alpha = System.Math.Sin(w0) / (2.0 * 0.5);
            Biquad(x, (1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
        }
    }

    private static void Biquad(float[] x, double b0, double b1, double b2, double a0, double a1, double a2)
    {
        b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double y = b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x[i];
            y2 = y1; y1 = y;
            x[i] = (float)y;
        }
    }
}
