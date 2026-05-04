using AI;
using AI.Charts;
using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using AI.Statistics;
using System.Text;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.Ai;

public static partial class AiDemoRunner
{
    #region Ряды и обработка сигналов — случаи

    private static void RunSeriesCase(
        string key, IReadOnlyDictionary<string, double> p,
        ChartView cv, ref string? textOut)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        switch (key)
        {
            case "diff_integr":
            {
                int n = Math.Max(50, (int)N("n", 200));
                double xMax = Math.Max(1, N("xMax", 2 * Math.PI));
                double step = xMax / (n - 1);
                var x = Vector.Seq(0, step, xMax);
                var sin  = x.Transform(Math.Sin);
                var cosT = x.Transform(Math.Cos);
                double fd = 1.0 / step;
                var dSin = Functions.Diff(sin, fd);
                var iCos = Functions.Integral(cosT, fd);
                cv.ChartName = "Численные дифференцирование и интегрирование";
                cv.LabelX = "x"; cv.LabelY = "значение";
                cv.AddPlot(x, sin,  "sin(x)",                      Palette[0], width: 2);
                cv.AddPlot(x, cosT, "cos(x) (истина производной)", Palette[2], width: 2);
                cv.AddPlot(x, dSin, "Diff(sin(x))",                Palette[1], width: 3);
                cv.AddPlot(x, iCos, "∫cos(x)dx",                   Palette[3], width: 3);
                double errD = (dSin - cosT).Transform(v => v * v).Sum() / n;
                double errI = (iCos - sin).Transform(v => v * v).Sum() / n;
                textOut =
                    $"Шаг сетки h = {step:F5}\n" +
                    $"MSE(Diff(sin) − cos) = {errD:F6}\n" +
                    $"MSE(∫cos − sin)      = {errI:F6}\n" +
                    "Погрешность схемы первого порядка ∝ h.";
                break;
            }
            case "moving_stats":
            {
                int n = Math.Max(64, (int)N("n", 512));
                int window = Math.Clamp((int)N("window", 21), 4, Math.Max(4, n - 4));
                double noise = Math.Max(0, N("noise", 0.25));
                var rng = new Random((int)N("seed", 42));
                var t = new Vector(n);
                for (int i = 0; i < n; i++) t[i] = 0.02 * i;
                var clean = t.Transform(ti => Math.Sin(2 * Math.PI * 0.4 * ti) + 0.5 * Math.Sin(2 * Math.PI * 1.3 * ti));
                var noisy = new Vector(n);
                for (int i = 0; i < n; i++) noisy[i] = clean[i] + RandomEngine.NextGaussian(rng) * noise;
                var mAvg = Functions.WindowFuncDouble(noisy, v => v.Mean(), window);
                var mStd = Functions.WindowFuncDouble(noisy, v => Math.Sqrt(Statistic.CalcVariance(v)), window);
                var tWin = new Vector(mAvg.Count);
                for (int i = 0; i < mAvg.Count; i++) tWin[i] = t[i + window / 2];
                cv.ChartName = $"Скользящие статистики, окно {window}";
                cv.LabelX = "t"; cv.LabelY = "значение";
                cv.AddPlot(t, noisy,  "Шум",            WithAlpha(Palette[0], 120), width: 1);
                cv.AddPlot(t, clean,  "Истинный сигнал", Palette[2], width: 2);
                cv.AddPlot(tWin, mAvg, "Скольз. среднее", Palette[1], width: 3);
                cv.AddPlot(tWin, mStd, "Скольз. СКО",     Palette[3], width: 2);
                break;
            }
            case "form_factors":
            {
                int n = Math.Max(64, (int)N("n", 256));
                int kind = Math.Clamp((int)N("kind", 0), 0, 3);
                var t = new Vector(n);
                for (int i = 0; i < n; i++) t[i] = 2 * Math.PI * i / (n - 1);
                var sig = new Vector(n);
                string name;
                switch (kind)
                {
                    case 1:
                        name = "Треугольный";
                        for (int i = 0; i < n; i++) { double x = t[i] / Math.PI; double f = x - Math.Floor(x + 0.5); sig[i] = 4 * Math.Abs(f) - 1; }
                        break;
                    case 2:
                        name = "Прямоугольный";
                        for (int i = 0; i < n; i++) sig[i] = Math.Sin(t[i]) >= 0 ? 1 : -1;
                        break;
                    case 3:
                        name = "Импульс (sparse)";
                        for (int i = 0; i < n; i++) sig[i] = 0;
                        for (int k = 8; k < n; k += Math.Max(2, n / 8)) sig[k] = 3;
                        break;
                    default:
                        name = "Синус";
                        for (int i = 0; i < n; i++) sig[i] = Math.Sin(t[i]);
                        break;
                }
                double crest = FormStatistics.CrestFactor(sig);
                double shape = FormStatistics.ShapeFactor(sig);
                double imp   = FormStatistics.ImpulseFactor(sig);
                double rms   = Statistic.RMS(sig);
                double absMax = Math.Max(Math.Abs(sig.Min()), Math.Abs(sig.Max()));
                cv.ChartName = $"{name}:  crest={crest:F3}   shape={shape:F3}   impulse={imp:F3}";
                cv.LabelX = "t"; cv.LabelY = "значение";
                cv.AddPlot(t, sig, name, Palette[0], width: 2);
                var sb = new StringBuilder();
                sb.AppendLine("> Форм-факторы сигнала");
                sb.AppendLine();
                sb.AppendLine($"  Сигнал:         {name}");
                sb.AppendLine($"  Длина:          {n}");
                sb.AppendLine();
                sb.AppendLine($"  max|x|:          {absMax:F4}");
                sb.AppendLine($"  RMS:             {rms:F4}");
                sb.AppendLine();
                sb.AppendLine("  +--------------------+------------+");
                sb.AppendLine("  | Фактор             |   Значение |");
                sb.AppendLine("  |--------------------+------------|");
                sb.AppendLine($"  | Пик-фактор         | {crest,10:F4} |  max|x| / RMS");
                sb.AppendLine($"  | Форм-фактор        | {shape,10:F4} |  RMS / mean|x|");
                sb.AppendLine($"  | Импульс-фактор     | {imp,10:F4} |  max|x| / mean|x|");
                sb.AppendLine("  +--------------------+------------+");
                sb.AppendLine();
                sb.AppendLine("  Для sin(x):     crest ≈ √2,  shape ≈ π/(2√2) ≈ 1.11");
                sb.AppendLine("  Для прямоуг.:   crest = 1,   shape = 1");
                sb.AppendLine("  Для импульса:   crest велик (разреженная энергия)");
                textOut = sb.ToString();
                break;
            }
        }
    }

    #endregion
}
