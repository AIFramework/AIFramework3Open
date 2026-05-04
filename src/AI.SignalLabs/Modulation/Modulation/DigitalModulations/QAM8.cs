using System;
using System.Numerics;

namespace AI.SignalLab.Modulation.Modulation.DigitalModulations;

/// <summary>
/// 8-уровневая квадратурная амплитудная модуляция (8-QAM).
/// Использует 3 бита на символ.
/// Созвездие: 8 точек, нормированных к единичной средней мощности.
/// </summary>
public class QAM8 : BaseIQModulation
{
    public override int BitsPerSymbol => 3;

    public override Complex[] Constellation { get; } = new Complex[]
    {
        new Complex( 1,  1) / Math.Sqrt(2),   // 000
        new Complex(-1,  1) / Math.Sqrt(2),   // 001
        new Complex(-1, -1) / Math.Sqrt(2),   // 010
        new Complex( 1, -1) / Math.Sqrt(2),   // 011
        new Complex( 3,  1) / Math.Sqrt(10),  // 100
        new Complex(-3,  1) / Math.Sqrt(10),  // 101
        new Complex(-3, -1) / Math.Sqrt(10),  // 110
        new Complex( 3, -1) / Math.Sqrt(10)   // 111
    };
}
