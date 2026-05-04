using System;
using System.Numerics;

namespace AI.SignalLab.Modulation.Modulation.DigitalModulations;

/// <summary>
/// Квадратурная фазовая манипуляция (QPSK).
/// Использует 2 бита на символ.
/// Созвездие: 4 точки на окружности, нормированные к единичной мощности (1/√2).
/// </summary>
public class QPSK : BaseIQModulation
{
    public override int BitsPerSymbol => 2;

    public override Complex[] Constellation { get; } = new Complex[]
    {
        new Complex(-1, -1) / Math.Sqrt(2), // 00
        new Complex( 1, -1) / Math.Sqrt(2), // 01
        new Complex(-1,  1) / Math.Sqrt(2), // 10
        new Complex( 1,  1) / Math.Sqrt(2)  // 11
    };
}
