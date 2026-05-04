using AI.DataStructs.Algebraic;
using System;
using System.Collections;
using System.Text;
using System.Text.Json;

namespace AI.SignalLab.Modulation.Modulation;

public class ModulationBitsTools
{
    /// <summary>
    /// Перевод битов в модулирующий сигнал
    /// </summary>
    /// <param name="bools">Биты</param>
    /// <param name="bitDuration">Длительность бита</param>
    /// <param name="lowSignal">Нижний уровень</param>
    /// <param name="hSignal">Верхний уровень</param>
    /// <param name="sr">Частота дискретизации</param>
    public static Vector Bits2Signal(bool[] bools, double bitDuration = 3e-3, double lowSignal = 0, double hSignal = 1, double sr = 8e+3) 
    {
        int nSimb = (int)(bitDuration * sr); // Отсчетов на символ
        Vector bitsSignal = new Vector(nSimb*bools.Length);
        
        for (int i = 0; i < bools.Length; i++)
        {
            if (bools[i])
            {
                // Установка высокого уровня
                int end = (i + 1) * nSimb;
                for (int j = i*nSimb; j < end; j++)
                    bitsSignal[j] = hSignal;
            }
            else 
            {
                // Установка низкого уровня
                int end = (i + 1) * nSimb;
                for (int j = i * nSimb; j < end; j++)
                    bitsSignal[j] = lowSignal;
            }

        }

        return bitsSignal;
    }

    /// <summary>
    /// Перевод цифрового объекта в модулирующий сигнал
    /// </summary>
    /// <param name="bools">Биты</param>
    /// <param name="bitDuration">Длительность бита</param>
    /// <param name="lowSignal">Нижний уровень</param>
    /// <param name="hSignal">Верхний уровень</param>
    /// <param name="sr">Частота дискретизации</param>
    public static Vector Object2Signal(object obj, double bitDuration = 3e-3, double lowSignal = 0, double hSignal = 1, double sr = 8e+3)
    {
        BitArray bits = new BitArray(Obj2Bytes(obj));
        bool[] bools = new bool[bits.Length];
        
        for (int i = 0; i < bits.Length; i++)
            bools[i] = bits[i];

        return Bits2Signal(bools, bitDuration, lowSignal, hSignal, sr);
    }

    /// <summary>
    /// Конвертирование объекта в массив байт.
    /// Строки кодируются как UTF-8, остальные типы — через JSON (UTF-8).
    /// </summary>
    public static byte[] Obj2Bytes(object obj)
    {
        if (obj == null)
            return null;

        if (obj is string s)
            return Encoding.UTF8.GetBytes(s);

        return JsonSerializer.SerializeToUtf8Bytes(obj, obj.GetType());
    }

    /// <summary>
    /// Восстановление строки из массива байт (UTF-8).
    /// </summary>
    public static string BytesToString(byte[] data)
    {
        if (data == null)
            return null;
        return Encoding.UTF8.GetString(data);
    }
}
