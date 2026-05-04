using AI.DataStructs.Algebraic;
using System;

namespace AI.SignalLab.Modulation.Modulation.DigitalModulations;

/// <summary>
/// Амплитудная манипуляция (ASK / OOK).
/// Каждый бит модулирует амплитуду косинусоидальной несущей:
///   s(t) = A(bit) * cos(2π·f0·t)
/// </summary>
public class ASK
{
    /// <summary>Несущая частота, Гц</summary>
    public double CarrierFrequency { get; }

    /// <summary>Частота дискретизации, Гц</summary>
    public double SampleRate { get; }

    /// <summary>Амплитуда для логической «1»</summary>
    public double HighAmplitude { get; }

    /// <summary>Амплитуда для логического «0»</summary>
    public double LowAmplitude { get; }

    /// <summary>Длительность одного бита, с</summary>
    public double BitDuration { get; }

    public ASK(double carrierFrequency, double sampleRate,
               double bitDuration = 1e-3,
               double highAmplitude = 1.0,
               double lowAmplitude = 0.0)
    {
        CarrierFrequency = carrierFrequency;
        SampleRate = sampleRate;
        BitDuration = bitDuration;
        HighAmplitude = highAmplitude;
        LowAmplitude = lowAmplitude;
    }

    /// <summary>
    /// Модуляция потока бит в ASK-сигнал.
    /// </summary>
    public Vector Modulate(bool[] bits)
    {
        int samplesPerBit = (int)(BitDuration * SampleRate);
        Vector output = new Vector(bits.Length * samplesPerBit);
        double dt = 1.0 / SampleRate;

        for (int b = 0; b < bits.Length; b++)
        {
            double amplitude = bits[b] ? HighAmplitude : LowAmplitude;
            int start = b * samplesPerBit;
            for (int s = 0; s < samplesPerBit; s++)
            {
                double t = (start + s) * dt;
                output[start + s] = amplitude * Math.Cos(2 * Math.PI * CarrierFrequency * t);
            }
        }

        return output;
    }

    /// <summary>
    /// Демодуляция ASK сигнала (огибающая через синхронное детектирование + порог).
    /// </summary>
    public bool[] Demodulate(Vector signal)
    {
        int samplesPerBit = (int)(BitDuration * SampleRate);
        int numBits = signal.Count / samplesPerBit;
        bool[] bits = new bool[numBits];
        double dt = 1.0 / SampleRate;
        double threshold = (HighAmplitude + LowAmplitude) / 2.0;

        for (int b = 0; b < numBits; b++)
        {
            int start = b * samplesPerBit;
            double sum = 0;

            for (int s = 0; s < samplesPerBit; s++)
            {
                double t = (start + s) * dt;
                // Умножение на опорный косинус и усреднение — интегрирующий детектор
                sum += signal[start + s] * Math.Cos(2 * Math.PI * CarrierFrequency * t);
            }

            double envelope = 2.0 * sum / samplesPerBit;
            bits[b] = envelope >= threshold;
        }

        return bits;
    }
}
