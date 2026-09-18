using AI.DataStructs.Algebraic;
using AI.Economics.Forecasting;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Выбор модели прогноза и её проверка на истории.
/// </summary>
/// <remarks>
/// Прогноз считался одной моделью — экспоненциальным сглаживанием, — и выбора у автора скрипта
/// не было: на ряде с длинной памятью она проигрывает ARIMA, на коротком ряде выигрывает метод
/// тета. Теперь модель выбирается параметром, а <c>auto</c> пробует все и берёт лучшую по
/// ошибке на обучающей выборке.
/// <para>
/// Качество не обещается, а меряется: <c>econ.backtest</c> прогоняет модель по скользящему
/// началу и ставит рядом наивный прогноз. Модель хуже наивной — это видно числом, а не
/// догадкой, и MASE показывает это прямо: единица означает «точно как наивный».
/// </para>
/// </remarks>
public static partial class EconModule
{
    /// <summary>Модели, которые перебирает <c>auto</c>; порядок — от самой частой.</summary>
    private static readonly string[] s_models = ["ets", "arima", "theta"];

    /// <summary>
    /// Проверка прогноза на истории скользящим началом.
    /// </summary>
    /// <remarks>
    /// Ключевая метрика — MASE, а не MAPE: MAPE не определена при нулях в ряду (сезон без
    /// продаж — обычное дело) и несимметрична к недо- и перепрогнозу. Симметричная процентная
    /// ошибка тоже посчитана — <c>smape</c>, — но решение принимается по MASE.
    /// </remarks>
    [ScriptFn("backtest", "Проверяет прогноз на истории: MASE, sMAPE, RMSE и наивный ориентир",
        Example = "econ.backtest(выручка, horizon: 3, season: 12)")]
    public static ScriptRecord Backtest(
        [ScriptParam("исторический ряд")] Vector series,
        [ScriptParam("горизонт на каждом срезе")] int horizon = 6,
        [ScriptParam("длина сезона; 1 — без сезонности")] int season = 1,
        [ScriptParam("модель: \"auto\", \"ets\", \"arima\", \"theta\"")] string kind = "auto",
        [ScriptParam("сколько срезов истории проверить")] int folds = 5)
    {
        Require(horizon > 0, "econ.backtest: горизонт должен быть больше нуля");
        Require(folds > 0, "econ.backtest: срезов — хотя бы один");
        Require(series.Count >= 8, "econ.backtest: ряд короче восьми наблюдений, проверять нечего");

        int cycle = Math.Max(1, season);
        var models = new List<(string Name, Func<Vector, int, ForecastResult> Forecaster)>();

        foreach (string name in Models(kind, "econ.backtest"))
        {
            string model = name;

            models.Add((model, (history, steps) => Single(history, steps, cycle, model, 0.9)));
        }

        BacktestResult result;

        try
        {
            result = ForecastBacktest.Run(series, models, horizon, folds, 0, cycle);
        }
        catch (ArgumentException exception)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"econ.backtest: {exception.Message}",
                "нужен ряд подлиннее, меньший горизонт либо меньше срезов (folds)");
        }

        BacktestSummary best = result.Models.Count > 0 ? result.Models[0] : result.Naive;

        return Record(
            ("model", ScriptValue.Str(best.Model)),
            ("mase", ScriptValue.Num(best.Mase)),
            ("smape", ScriptValue.Num(best.SMape)),
            ("rmse", ScriptValue.Num(best.Rmse)),
            ("mae", ScriptValue.Num(best.Mae)),
            ("coverage", ScriptValue.Num(best.Coverage)),
            ("folds", ScriptValue.Num(result.Folds)),
            ("naive_mase", ScriptValue.Num(result.Naive.Mase)),
            ("beats_naive", ScriptValue.Bool(best.Mase < result.Naive.Mase)));
    }

    /// <summary>Считает прогноз выбранной моделью; <c>auto</c> берёт лучшую по обучающей выборке.</summary>
    /// <remarks>
    /// Вместе с прогнозом возвращается имя семейства («ets», «arima», «theta»), а не только
    /// подпись библиотеки вида «ARIMA(2,1,1)»: скрипту нужно сравнивать выбор с тем, что он
    /// просил, а подпись меняется от ряда к ряду.
    /// </remarks>
    private static (ForecastResult Result, string Kind) Predict(
        Vector series, int horizon, int season, string kind, double confidence, string what)
    {
        ForecastResult? best = null;
        string chosen = string.Empty;
        string? failure = null;

        foreach (string name in Models(kind, what))
        {
            try
            {
                ForecastResult candidate = Single(series, horizon, Math.Max(1, season), name, confidence);

                if (best == null || Better(candidate, best))
                {
                    best = candidate;
                    chosen = name;
                }
            }
            catch (ArgumentException exception)
            {
                // Ряда не хватило одной модели — это не отказ, пока есть другие: ради этого
                // «auto» и существует. Причина запоминается на случай, если не справится никто.
                failure = exception.Message;
            }
        }

        return best == null
            ? throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{what}: {failure ?? "ни одна модель не справилась с этим рядом"}",
                "нужно больше истории: моделям нужен хотя бы десяток наблюдений сверх сезонного цикла")
            : (best, chosen);
    }

    private static ForecastResult Single(Vector series, int horizon, int season, string kind, double confidence) => kind switch
    {
        "arima" => Arima.AutoFit(series, horizon, season, 2, confidence),
        "theta" => ThetaMethod.Fit(series, horizon, season, confidence),
        _ => ExponentialSmoothing.AutoFit(series, horizon, season, confidence),
    };

    private static IReadOnlyList<string> Models(string kind, string what) => kind switch
    {
        "auto" => s_models,
        "ets" or "arima" or "theta" => [kind],
        _ => throw new ScriptError(
            DiagnosticCodes.UnknownArgument,
            $"{what}: неизвестная модель «{kind}»",
            "известны: \"auto\", \"ets\", \"arima\", \"theta\""),
    };

    /// <summary>Лучше ли кандидат: сперва по ошибке на обучающей выборке, затем по AIC.</summary>
    private static bool Better(ForecastResult candidate, ForecastResult best)
    {
        if (!double.IsNaN(candidate.InSampleMase) && !double.IsNaN(best.InSampleMase))
            return candidate.InSampleMase < best.InSampleMase;

        if (!double.IsNaN(candidate.Aic) && !double.IsNaN(best.Aic)) return candidate.Aic < best.Aic;

        return !double.IsNaN(candidate.InSampleMase);
    }
}
