using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.DSP.Multiray.Sources;

/// <summary>
/// Источник синусоидального сигнала
/// </summary>
public class SinSource : Source
{
    /// <summary>
    /// Частота синусоиды (Гц)
    /// </summary>
    public double F0 = 300;

    /// <summary>
    /// Конструктор по умолчанию
    /// </summary>
    public SinSource() { }

    /// <summary>
    /// Конструктор с частотой дискретизации и координатами
    /// </summary>
    public SinSource(double sr, params double[] coords) : base(sr, coords) { }

    /// <inheritdoc/>
    public override Vector GetSignal(double dist, double speed, IEnumerable<Source> sources = null)
    {
        BuildCarrier(F0, dist, speed, out Vector t, out double attenuation, out double phaseShift);
        return attenuation * t.Transform(x => Math.Sin(2 * Math.PI * F0 * x - phaseShift));
    }
}
