using AI.DataStructs.Algebraic;
using System;

namespace AI.DSP.MusicUtils;

/// <summary>
/// Эхо и реверберация
/// </summary>
[Serializable]
public class EchoReverb
{
    /// <summary>
    /// Частота дискретизации проекта
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Эхо и реверберация
    /// </summary>
    /// <param name="sr">Частота дискретизации проекта</param>
    public EchoReverb(int sr)
    {
        SampleRate = sr;
    }

    /// <summary>
    /// Прямое эхо: y[n] = x[n] + volume * x[n - delay]
    /// </summary>
    /// <param name="data">Сигнал (изменяется на месте)</param>
    /// <param name="timeDelay">Задержка эхо в секундах</param>
    /// <param name="volume">Громкость эхо (0..1)</param>
    public void Echo(Vector data, double timeDelay = 0.05, double volume = 0.3)
    {
        int steps = (int)(SampleRate * timeDelay);
        if (steps <= 0) return;

        int len = data.Count;

        for (int i = steps; i < len; i++)
            data[i] += volume * data[i - steps];
    }

    /// <summary>
    /// Обратное эхо: y[n - delay] += volume * x[n]
    /// </summary>
    /// <param name="data">Сигнал (изменяется на месте)</param>
    /// <param name="timeDelay">Задержка эхо в секундах</param>
    /// <param name="volume">Громкость эхо (0..1)</param>
    public void EchoReverse(Vector data, double timeDelay = 0.05, double volume = 0.3)
    {
        int steps = (int)(SampleRate * timeDelay);
        if (steps <= 0) return;

        int len = data.Count;

        for (int i = steps; i < len; i++)
            data[i - steps] += volume * data[i];
    }
}
