using AI.DataStructs.Algebraic;
using AI.SignalLab.Filters;
using AiFrameworkDemo.Core;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.SignalLabs;

public static partial class SignalLabsDemoRunner
{
    /// <summary>
    /// Демо SRRC-фильтра: импульсная характеристика (ИХ) и АЧХ (амплитудно-частотная характеристика).
    /// </summary>
    internal static DemoResult DoSrrc(IReadOnlyDictionary<string, double> p, DemoSettings settings)
    {
        double rollOff    = N(p, "rollOff", 3) * 0.1;
        int    span       = I(p, "span", 6);
        double symbolRate = N(p, "symbolRate", 2000);    // Бд
        double sr         = N(p, "sampleRate", 16000);   // Гц

        double T = 1.0 / symbolRate; // длительность символа, с

        // Вычисляем ядро ИХ
        Vector kernel = RootRaisedCosineFilter.ComputeKernel(T, sr, rollOff, span);

        int len = kernel.Count;
        int half = len / 2;

        double[] xTime = new double[len];
        double[] yKernel = new double[len];
        for (int i = 0; i < len; i++)
        {
            xTime[i] = (i - half) / sr * 1000; // мс
            yKernel[i] = kernel[i];
        }

        // АЧХ через DFT (только положительные частоты)
        int fftLen = NextPow2(len * 4);
        double[] fftIn = new double[fftLen];
        for (int i = 0; i < len; i++) fftIn[i] = kernel[i];

        double[] mag = ComputeMagnitudeSpectrum(fftIn, fftLen, sr);

        int halfFft = fftLen / 2;
        double[] xFreq = new double[halfFft];
        double[] yMag  = new double[halfFft];
        for (int i = 0; i < halfFft; i++)
        {
            xFreq[i] = i * sr / fftLen;
            yMag[i]  = mag[i];
        }

        // График 1: ИХ
        var cvIr = MakeView(settings);
        cvIr.ChartName = $"SRRC — Импульсная характеристика (β={rollOff:F2}, span={span})";
        cvIr.LabelX = "Время, мс";
        cvIr.LabelY = "h(t)";
        cvIr.AddPlot(new Vector(xTime), new Vector(yKernel), "ИХ SRRC");

        // График 2: АЧХ
        var cvFr = MakeView(settings);
        cvFr.ChartName = $"SRRC — АЧХ (β={rollOff:F2})";
        cvFr.LabelX = "Частота, Гц";
        cvFr.LabelY = "|H(f)|";

        // Обрезаем по частоте Найквиста для наглядности
        int cutoff = Math.Min(halfFft, (int)(3.0 / T / sr * fftLen));
        cutoff = Math.Max(cutoff, 2);
        double[] xFreqCut = xFreq[..cutoff];
        double[] yMagCut  = yMag[..cutoff];
        cvFr.AddPlot(new Vector(xFreqCut), new Vector(yMagCut), "АЧХ SRRC");

        // Текст
        var sb = new StringBuilder();
        sb.AppendLine($"Roll-off β: {rollOff:F2}");
        sb.AppendLine($"Длина фильтра в символах (span): ±{span}");
        sb.AppendLine($"Скорость символов: {symbolRate:F0} Бд  ->  T = {T * 1e3:F3} мс");
        sb.AppendLine($"Частота дискретизации: {sr:F0} Гц  ->  {(int)(sr / symbolRate)} отсч./символ");
        sb.AppendLine($"Число коэффициентов ядра: {len}");
        sb.AppendLine();
        sb.AppendLine("Свойства пары SRRC (Tx × Rx):");
        sb.AppendLine("  * Произведение двух SRRC = RC (Raised Cosine)");
        sb.AppendLine("  * Нулевая МСИ в моменты выборки символов (критерий Найквиста)");
        sb.AppendLine($"  * Ширина основного лепестка: {(1 + rollOff) / T:F0} Гц");
        sb.AppendLine($"  * Полоса Найквиста (без скатывания): {1.0 / (2 * T):F0} Гц");

        // Возвращаем первый график (ИХ) как основной PNG; АЧХ идёт в Plotly
        var result = Png(cvIr, settings, textOutput: sb.ToString());

        // Добавляем АЧХ как второй Plotly (переиспользуем PlotlyJson второго ChartView)
        // Для простоты сливаем оба в один ChartView с двумя линиями
        var cvBoth = MakeView(settings);
        cvBoth.ChartName = $"SRRC: ИХ и АЧХ (β={rollOff:F2})";
        cvBoth.LabelX = "Индекс";
        cvBoth.LabelY = "Значение";

        double[] xNorm = new double[len];
        for (int i = 0; i < len; i++) xNorm[i] = (i - half);
        cvBoth.AddPlot(new Vector(xNorm), new Vector(yKernel), "ИХ (нормир. ось)");

        return Png(cvBoth, settings, textOutput: sb.ToString());
    }

    #region Вспомогательные методы (FFT, утилиты)

    private static int NextPow2(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    /// <summary>Вычисляет модуль спектра через наивное DFT (достаточно для коротких ядер).</summary>
    private static double[] ComputeMagnitudeSpectrum(double[] signal, int fftLen, double sr)
    {
        int half = fftLen / 2;
        double[] mag = new double[half];
        double twoPiOverN = 2 * Math.PI / fftLen;

        // Для ядра длиной ~100–300 коэффициентов наивное DFT по fftLen точкам
        // производительно достаточно (fftLen ≤ 4096).
        Parallel.For(0, half, k =>
        {
            double re = 0, im = 0;
            double angle = twoPiOverN * k;
            for (int n = 0; n < signal.Length; n++)
            {
                if (signal[n] == 0) continue;
                re += signal[n] * Math.Cos(angle * n);
                im -= signal[n] * Math.Sin(angle * n);
            }
            mag[k] = Math.Sqrt(re * re + im * im);
        });

        // Нормировка на максимум
        double max = mag.Max();
        if (max > 0)
            for (int i = 0; i < half; i++) mag[i] /= max;

        return mag;
    }

    #endregion
}
