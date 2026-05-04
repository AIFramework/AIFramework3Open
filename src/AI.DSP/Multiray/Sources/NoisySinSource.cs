using AI.DataStructs.Algebraic;
using AI.Statistics;
using System;
using System.Collections.Generic;

namespace AI.DSP.Multiray.Sources;

/// <summary>
/// Источник синусоидального сигнала с белым гауссовым шумом
/// </summary>
public class NoisySinSource : Source
{
    /// <summary>
    /// Частота синусоиды (Гц)
    /// </summary>
    public double F0 = 1200;
    /// <summary>
    /// Отношение сигнал/шум (SNR) в разах (не в дБ)
    /// </summary>
    public double SNR = 5.0;

    /// <summary>
    /// Конструктор по умолчанию
    /// </summary>
    public NoisySinSource() { }

    /// <summary>
    /// Конструктор с частотой дискретизации и координатами
    /// </summary>
    public NoisySinSource(double sr, params double[] coords) : base(sr, coords) { }

    /// <summary>
    /// Конструктор с частотой дискретизации, SNR и координатами
    /// </summary>
    public NoisySinSource(double sr, double snr, params double[] coords) : base(sr, coords)
    {
        SNR = snr;
    }

    /// <inheritdoc/>
    public override Vector GetSignal(double dist, double speed, IEnumerable<Source> sources = null)
    {
        BuildCarrier(F0, dist, speed, out Vector t, out double attenuation, out double phaseShift);

        Vector cleanSignal = attenuation * t.Transform(x =>
            Math.Sin(2 * Math.PI * F0 * x - phaseShift)
        );

        if (cleanSignal.Count == 0) return cleanSignal;

        double signalRMS = 0;
        for (int i = 0; i < cleanSignal.Count; i++)
            signalRMS += cleanSignal[i] * cleanSignal[i];
        signalRMS = Math.Sqrt(signalRMS / cleanSignal.Count);

        Vector noise = Statistic.RandNorm(cleanSignal.Count);

        double noiseRMS = 0;
        for (int i = 0; i < noise.Count; i++)
            noiseRMS += noise[i] * noise[i];
        noiseRMS = Math.Sqrt(noiseRMS / noise.Count);

        if (noiseRMS < 1e-30 || SNR < 1e-30)
            return cleanSignal;

        double noiseScale = signalRMS / (SNR * noiseRMS);
        return cleanSignal + noise * noiseScale;
    }
}

/// <summary>
/// Источник белого шума
/// </summary>
public class NoiseSource : Source
{
    /// <summary>
    /// Конструктор по умолчанию
    /// </summary>
    public NoiseSource() { }

    /// <summary>
    /// Конструктор с частотой дискретизации и координатами
    /// </summary>
    public NoiseSource(double sr, params double[] coords) : base(sr, coords) { }

    /// <inheritdoc/>
    public override Vector GetSignal(double dist, double speed, IEnumerable<Source> sources = null)
    {
        Vector t = Vector.Time0(SR, T);
        return Statistic.RandNorm(t.Count);
    }
}
