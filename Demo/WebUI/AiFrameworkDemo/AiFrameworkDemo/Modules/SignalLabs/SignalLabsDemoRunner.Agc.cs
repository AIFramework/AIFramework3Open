using AI.DataStructs.Algebraic;
using AI.SignalLab.AGC;
using AI.SignalLab.AGC.CustomAGC;
using AiFrameworkDemo.Core;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.SignalLabs;

public static partial class SignalLabsDemoRunner
{
    /// <summary>
    /// Демо АРУ: синусоида с резкими скачками амплитуды -> алгоритм АРУ -> график до/после.
    /// </summary>
    internal static DemoResult DoAgc(IReadOnlyDictionary<string, double> p, DemoSettings settings)
    {
        int agcType     = I(p, "agcType", 0);
        double threshold = N(p, "tresholdAgc", 4);
        double freq      = N(p, "signalFreq", 1000);
        int sr           = (int)N(p, "sampleRate", 44100);
        int totalSamples = (int)(N(p, "duration", 2000) / 1000.0 * sr);

        // Ограничиваем длину — иначе PNG становится огромным
        totalSamples = Math.Min(totalSamples, 4 * sr);

        // Генерируем тестовый сигнал с тремя зонами амплитуды: тихо / средне / громко
        Vector original = GenerateAmplitudeJumpSignal(sr, totalSamples, freq);

        // Выбираем алгоритм АРУ
        IAGC agc = agcType switch
        {
            1 => new LogAGC        { TresholdAGC = threshold },
            2 => new MinCombineAGC { TresholdAGC = threshold },
            _ => new DirectAGC     { TresholdAGC = threshold },
        };

        // Пропускаем сигнал через АРУ (потоковый режим — без batch-обёртки)
        Vector processed = new Vector(totalSamples);
        for (int i = 0; i < totalSamples; i++)
            processed[i] = agc.Calculate(original[i]);

        // Строим отображаемый фрагмент (не более 2000 точек для читаемости графика)
        int displaySamples = Math.Min(totalSamples, 2000);
        int step = Math.Max(1, totalSamples / displaySamples);
        int points = totalSamples / step;

        double[] xs = new double[points];
        double[] ys1 = new double[points];
        double[] ys2 = new double[points];

        for (int i = 0; i < points; i++)
        {
            xs[i]  = i * step / (double)sr * 1000; // мс
            ys1[i] = original[i * step];
            ys2[i] = processed[i * step];
        }

        var cv = MakeView(settings);
        cv.ChartName = AgcTitle(agcType);
        cv.AddPlot(new Vector(xs), new Vector(ys1), "Исходный сигнал");
        cv.AddPlot(new Vector(xs), new Vector(ys2), "После АРУ");
        cv.LabelX = "Время, мс";
        cv.LabelY = "Амплитуда";

        // Статистика
        var sb = new StringBuilder();
        sb.AppendLine($"Тип АРУ: {AgcTitle(agcType)}");
        sb.AppendLine($"Длит. сигнала: {totalSamples / (double)sr * 1000:F0} мс  |  Частота дискр.: {sr} Гц");
        sb.AppendLine($"Порог АРУ (TresholdAGC): {threshold:F1}");
        sb.AppendLine();
        sb.AppendLine($"Исходный сигнал — max: {original.Max():F3}  min: {original.Min():F3}");
        sb.AppendLine($"После АРУ       — max: {processed.Max():F3}  min: {processed.Min():F3}");

        return Png(cv, settings, textOutput: sb.ToString());
    }

    private static string AgcTitle(int type) => type switch
    {
        1 => "LogAGC — Логарифмическая АРУ",
        2 => "MinCombineAGC — Комбинированная АРУ",
        _ => "DirectAGC — Прямая АРУ",
    };

    /// <summary>
    /// Синусоида с тремя зонами амплитуды: 0.1 -> 2.0 -> 10.0 -> 0.1.
    /// Имитирует резкие скачки уровня речевого / радио-сигнала.
    /// </summary>
    private static Vector GenerateAmplitudeJumpSignal(int sr, int totalSamples, double freq)
    {
        double dt = 1.0 / sr;
        double total = totalSamples * dt;

        Vector v = new Vector(totalSamples);
        for (int i = 0; i < totalSamples; i++)
        {
            double t = i * dt;
            double a = t < total * 0.25 ? 0.1
                     : t < total * 0.50 ? 2.0
                     : t < total * 0.75 ? 10.0
                     : 0.1;
            v[i] = a * Math.Sin(2 * Math.PI * freq * t);
        }
        return v;
    }
}
