using AI.DataStructs.WithComplexElements;
using System;
using System.Numerics;

namespace AI.SignalLab.Modulation.Modulation.DigitalModulations;

/// <summary>
/// Базовый класс для цифровых IQ-модуляций (BPSK, QPSK, QAM и др.).
/// Осуществляет маппинг битов в точки созвездия и демаппинг (Minimum Distance Decoding).
/// </summary>
public abstract class BaseIQModulation
{
    /// <summary>
    /// Количество бит на один символ (1 для BPSK, 2 для QPSK, 3 для 8-QAM и т.д.)
    /// </summary>
    public abstract int BitsPerSymbol { get; }

    /// <summary>
    /// Идеальные точки созвездия (нормированные по мощности)
    /// </summary>
    public abstract Complex[] Constellation { get; }

    /// <summary>
    /// Преобразует битовый поток в последовательность IQ-символов.
    /// Если длина битового потока не кратна BitsPerSymbol, в конец добавляются нули.
    /// </summary>
    public Complex[] MapBitsToSymbols(bool[] bits)
    {
        int pad = (BitsPerSymbol - (bits.Length % BitsPerSymbol)) % BitsPerSymbol;
        bool[] paddedBits = bits;
        
        if (pad > 0)
        {
            paddedBits = new bool[bits.Length + pad];
            Array.Copy(bits, paddedBits, bits.Length);
        }

        int numSymbols = paddedBits.Length / BitsPerSymbol;
        Complex[] mappedSymbols = new Complex[numSymbols];
        
        for (int i = 0; i < numSymbols; i++)
        {
            int index = 0;
            for (int b = 0; b < BitsPerSymbol; b++)
            {
                if (paddedBits[i * BitsPerSymbol + b]) 
                    index |= (1 << b);
            }
            mappedSymbols[i] = Constellation[index];
        }
        
        return mappedSymbols;
    }

    /// <summary>
    /// Декодирует принятые IQ-символы обратно в битовый поток.
    /// Использует метод максимального правдоподобия (поиск ближайшей точки созвездия).
    /// </summary>
    /// <param name="iqSymbols">Принятые символы с шумом/искажениями</param>
    /// <param name="expectedBits">Ожидаемое количество бит (для отсечения нулей-заполнителей)</param>
    public bool[] DemapSymbolsToBits(ComplexVector iqSymbols, int expectedBits)
    {
        int numSymbols = Math.Min(iqSymbols.Count, (expectedBits + BitsPerSymbol - 1) / BitsPerSymbol);
        bool[] decodedBits = new bool[numSymbols * BitsPerSymbol];

        for (int i = 0; i < numSymbols; i++)
        {
            double minDist = double.MaxValue;
            int bestIndex = 0;

            for (int c = 0; c < Constellation.Length; c++)
            {
                double dist = (iqSymbols[i] - Constellation[c]).Magnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    bestIndex = c;
                }
            }

            for (int b = 0; b < BitsPerSymbol; b++)
            {
                decodedBits[i * BitsPerSymbol + b] = ((bestIndex >> b) & 1) == 1;
            }
        }

        bool[] exactBits = new bool[expectedBits];
        Array.Copy(decodedBits, exactBits, expectedBits);
        return exactBits;
    }
}
