using System;
using System.Numerics;

namespace AI.SignalLab.Modulation.Modulation.DigitalModulations;

/// <summary>
/// 16-уровневая квадратурная амплитудная модуляция (16-QAM).
/// Использует 4 бита на символ.
/// Созвездие: сетка 4x4, нормированная к единичной средней мощности.
/// </summary>
public class QAM16 : BaseIQModulation
{
    public override int BitsPerSymbol => 4;

    public override Complex[] Constellation { get; }

    public QAM16()
    {
        Constellation = new Complex[16];
        for (int i = 0; i < 16; i++)
        {
            int iBits = i & 3;
            int qBits = (i >> 2) & 3;
            double iVal = iBits == 0 ? -3 : iBits == 1 ? -1 : iBits == 3 ? 1 : 3;
            double qVal = qBits == 0 ? -3 : qBits == 1 ? -1 : qBits == 3 ? 1 : 3;
            Constellation[i] = new Complex(iVal, qVal) / Math.Sqrt(10);
        }
    }
}
