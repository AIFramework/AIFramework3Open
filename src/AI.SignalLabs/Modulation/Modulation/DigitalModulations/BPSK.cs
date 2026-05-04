using System.Numerics;

namespace AI.SignalLab.Modulation.Modulation.DigitalModulations;

/// <summary>
/// Двоичная фазовая манипуляция (BPSK).
/// Использует 1 бит на символ.
/// Созвездие: { -1, 1 }
/// </summary>
public class BPSK : BaseIQModulation
{
    public override int BitsPerSymbol => 1;

    public override Complex[] Constellation { get; } = new Complex[]
    {
        new Complex(-1, 0), // бит 0
        new Complex( 1, 0)  // бит 1
    };
}
