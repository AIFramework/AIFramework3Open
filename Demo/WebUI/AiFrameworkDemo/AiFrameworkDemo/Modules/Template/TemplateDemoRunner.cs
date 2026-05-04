using AI.DataStructs.Algebraic;
using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Template;

/// <summary>
/// Шаблон DemoRunner. Реализуйте каждый case для своего алгоритма.
///
/// Доступные утилиты (через using static DemoRunnerBase):
///   MakeView(settings)          — ChartView с темой
///   N(p, "key", default)        — числовой параметр
///   I(p, "key", default)        — числовой параметр как int
///   RenderPng(cv, settings)     — PNG data URL
///   Png(cv, settings)           — DemoResult с PNG + Plotly
///   ToArray(vector)             — Vector -> double[]
/// </summary>
public static class TemplateDemoRunner
{
    public static DemoResult Run(
        string algoKey,
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string>  tp,
        DemoSettings s)
    {
        var cv = MakeView(s);

        switch (algoKey)
        {
            case "algo_a":
            {
                int n     = I(p, "n", 100);
                double alpha = N(p, "alpha", 0.5);

                // TODO: реализовать алгоритм A
                cv.ChartName = $"Алгоритм A  —  n={n}, α={alpha:F2}";

                return Png(cv, s);
            }

            case "algo_b":
            {
                // TODO: реализовать алгоритм B
                cv.ChartName = "Алгоритм B";

                return Png(cv, s);
            }

            case "algo_c":
            {
                // TODO: реализовать алгоритм C — текстовый вывод
                string output = ">> algo_c.Run()\n=> результат выполнения";

                return new DemoResult { TextOutput = output };
            }

            default:
                return new DemoResult { Error = $"Неизвестный ключ алгоритма: {algoKey}" };
        }
    }
}
