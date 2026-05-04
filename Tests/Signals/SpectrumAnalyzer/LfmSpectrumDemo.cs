using AI.DataStructs.Algebraic;
using AI.DSP.DSPCore;

namespace SpectrumAnalyzer
{
    /// <summary>
    /// Демо: линейно-частотно-модулированный сигнал (ЛЧМ) для проверки спектра Уэлча.
    /// </summary>
    internal static class LfmSpectrumDemo
    {
        /// <summary>Разнос частоты по времени (Гц/с и т.п. в модели).</summary>
        public const double FrequencySweep = 500;

        /// <summary>Начальная частота (Гц).</summary>
        public const double StartFrequencyHz = 500;

        /// <summary>Частота дискретизации (Гц).</summary>
        public const int SampleRate = 4096;

        /// <summary>Длительность записи (с).</summary>
        public const double DurationSeconds = 10;

        /// <summary>Размер блока БПФ для метода Уэлча.</summary>
        public const int FftBlockSize = 4096;

        /// <summary>Сгенерировать ЛЧМ по модели <see cref="Signal.LFM"/>.</summary>
        public static Vector BuildSignal()
        {
            return Signal.LFM(FrequencySweep, StartFrequencyHz, SampleRate, DurationSeconds);
        }
    }
}
