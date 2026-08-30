using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Economics.Forecasting;

/// <summary>Итог бэктеста одной модели.</summary>
public sealed record BacktestSummary
{
    /// <summary>Название модели.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Средняя абсолютная масштабированная ошибка по всем срезам.</summary>
    public double Mase { get; init; }

    /// <summary>Симметричная процентная ошибка.</summary>
    public double SMape { get; init; }

    /// <summary>Корень из среднего квадрата ошибки.</summary>
    public double Rmse { get; init; }

    /// <summary>Средняя абсолютная ошибка.</summary>
    public double Mae { get; init; }

    /// <summary>Потери пинболл для нижней границы интервала.</summary>
    public double PinballLower { get; init; } = double.NaN;

    /// <summary>Потери пинболл для верхней границы интервала.</summary>
    public double PinballUpper { get; init; } = double.NaN;

    /// <summary>Фактическое покрытие интервалов прогноза.</summary>
    public double Coverage { get; init; } = double.NaN;

    /// <summary>Число срезов, на которых оценивалась модель.</summary>
    public int Folds { get; init; }

    /// <summary>Ошибка по горизонту: как быстро точность падает с удалением.</summary>
    public Vector MaeByHorizon { get; init; } = new Vector(0);
}

/// <summary>Результат сравнения моделей на скользящем начале.</summary>
public sealed record BacktestResult : IInterpretable
{
    /// <summary>Итоги по моделям, по возрастанию MASE.</summary>
    public IReadOnlyList<BacktestSummary> Models { get; init; } = [];

    /// <summary>Итог наивного прогноза — базовый ориентир.</summary>
    public BacktestSummary Naive { get; init; } = new();

    /// <summary>Горизонт прогноза, на котором велась оценка.</summary>
    public int Horizon { get; init; }

    /// <summary>Число срезов.</summary>
    public int Folds { get; init; }

    /// <summary>Длина сезонного цикла.</summary>
    public int SeasonalPeriod { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        BacktestSummary? best = Models.FirstOrDefault();
        bool beatsNaive = best is not null && best.Mase < 1;
        bool coverageOff = best is not null && !double.IsNaN(best.Coverage)
                           && Math.Abs(best.Coverage - 0.9) > 0.12;

        var builder = new InterpretationBuilder("Бэктест со скользящим началом")
            .Summary($"Лучшая модель — «{best?.Model}» с MASE {Fmt.Num(best?.Mase ?? double.NaN)} " +
                     $"на {Folds} срезах и горизонте {Horizon}. " +
                     (beatsNaive
                         ? $"Она точнее наивного прогноза на {Fmt.Pct(1 - (best?.Mase ?? 1))}."
                         : "Ни одна модель не превзошла наивный прогноз — это главный вывод."))
            .Metric("Лучшая модель", best?.Model ?? "нет", null,
                "по MASE на отложенных срезах", MetricQuality.Neutral)
            .Metric("MASE", best?.Mase ?? double.NaN, null,
                "меньше 1 — точнее наивного прогноза",
                beatsNaive ? MetricQuality.Good : MetricQuality.Critical)
            .Metric("sMAPE", Fmt.Pct((best?.SMape ?? double.NaN) / 1.0, 1), null,
                "симметричная процентная ошибка")
            .Metric("Покрытие интервалов", double.IsNaN(best?.Coverage ?? double.NaN)
                    ? "не оценено" : Fmt.Pct(best?.Coverage ?? 0), null,
                "доля фактов, попавших в интервал; должна совпадать с заявленной",
                coverageOff ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Срезов", Folds, null, "число последовательных проверок", MetricQuality.Unknown, 0);

        foreach (BacktestSummary model in Models)
            builder.Metric($"MASE: {model.Model}", model.Mase, null,
                $"sMAPE {Fmt.Pct(model.SMape)}",
                model.Mase < 1 ? MetricQuality.Good : MetricQuality.Warning);

        builder
            .Finding("Скользящее начало оценивает модель так, как она будет использоваться: " +
                     "обучение только на прошлом, проверка на будущем. Ошибка на обучающей " +
                     "выборке систематически оптимистична и для выбора модели не годится.")
            .FindingIf(best is not null && best.MaeByHorizon.Count > 1,
                $"Точность падает с горизонтом: ошибка на первом шаге " +
                $"{Fmt.Num(best?.MaeByHorizon[0] ?? 0)}, на последнем " +
                $"{Fmt.Num(best?.MaeByHorizon[(best?.MaeByHorizon.Count ?? 1) - 1] ?? 0)}. " +
                "Планируйте разную детальность решений на разной глубине.")
            .FindingIf(Models.Count > 1 && Models.Last().Mase > (Models.First().Mase * 1.5),
                $"Разброс между моделями значим: худшая ошибается в " +
                $"{Fmt.Num(Models.Last().Mase / Math.Max(Models.First().Mase, 1e-9))} раза чаще лучшей. " +
                "Выбор модели здесь не формальность.")
            .WarningIf(!beatsNaive,
                "Наивный прогноз не побеждён. Это либо признак случайного блуждания, либо " +
                "того, что моделям не хватает данных. Внедрять сложную модель в таком случае — " +
                "оплачивать сопровождение без выигрыша в точности.")
            .WarningIf(coverageOff,
                $"Покрытие интервалов {Fmt.Pct(best?.Coverage ?? 0)} вместо заявленного. " +
                "Интервалы откалиброваны неверно: планирование запаса по ним даст " +
                "не тот уровень сервиса, на который рассчитывали.")
            .WarningIf(Folds < 5,
                $"Срезов всего {Folds}: различие между моделями может оказаться случайным.")
            .Warning("Результат привязан к конкретному участку истории. Структурный сдвиг " +
                     "обесценивает и лучшую по бэктесту модель — перепроверяйте выбор регулярно.")
            .Recommendation("Сравнивайте модели по MASE, а не по MAPE: последняя не определена " +
                            "на нулях и штрафует занижение сильнее завышения.")
            .RecommendationIf(coverageOff,
                "Откалибруйте интервалы конформным предсказанием: оно даёт заявленное " +
                "покрытие без предположений о распределении ошибки.");

        return builder.Build();
    }
}

/// <summary>
/// Бэктест прогнозных моделей со скользящим началом.
/// </summary>
/// <remarks>
/// <para>
/// Единственный честный способ сравнить прогнозные модели: обучить на
/// прошлом, проверить на будущем, повторить, сдвигая границу. Ошибка
/// на обучающей выборке в такой задаче бесполезна — модель с достаточным
/// числом параметров опишет любой ряд идеально и провалится на первом же
/// новом наблюдении.
/// </para>
/// <para>
/// Наивный прогноз включён в сравнение обязательно. Если модель его
/// не превосходит, то никакого преимущества у неё нет — это самый частый
/// и самый неприятный результат бэктеста.
/// </para>
/// </remarks>
public static class ForecastBacktest
{
    /// <summary>Проверяет набор моделей на скользящем начале.</summary>
    /// <param name="series">Полный исторический ряд.</param>
    /// <param name="models">
    /// Модели: имя и функция «история и горизонт — прогноз».
    /// </param>
    /// <param name="horizon">Горизонт прогноза на каждом срезе.</param>
    /// <param name="folds">Число срезов.</param>
    /// <param name="minimumTrain">Минимальная длина обучающей выборки.</param>
    /// <param name="seasonalPeriod">Период сезонности для наивного прогноза и MASE.</param>
    /// <returns>Метрики по моделям и наивному прогнозу.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    /// <exception cref="ArgumentException">Ряд слишком короткий для такого числа срезов.</exception>
    public static BacktestResult Run(
        Vector series,
        IReadOnlyList<(string Name, Func<Vector, int, ForecastResult> Forecaster)> models,
        int horizon = 6, int folds = 5, int minimumTrain = 0, int seasonalPeriod = 1)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(models);

        int n = series.Count;
        if (minimumTrain <= 0) minimumTrain = Math.Max(n - (folds * horizon), Math.Max(2 * seasonalPeriod, 10));

        if (minimumTrain + horizon > n)
            throw new ArgumentException(
                "Ряд слишком короткий для заданных горизонта и числа срезов.", nameof(series));

        int available = (n - minimumTrain - horizon) / Math.Max(horizon, 1) + 1;
        folds = Math.Max(1, Math.Min(folds, available));

        var accumulators = models.ToDictionary(m => m.Name, _ => new Accumulator(horizon));
        var naive = new Accumulator(horizon);

        for (int fold = 0; fold < folds; fold++)
        {
            int trainEnd = minimumTrain + (fold * horizon);
            if (trainEnd + horizon > n) break;

            var train = new Vector(trainEnd);
            for (int t = 0; t < trainEnd; t++) train[t] = series[t];

            var actual = new Vector(horizon);
            for (int h = 0; h < horizon; h++) actual[h] = series[trainEnd + h];

            foreach ((string name, var forecaster) in models)
            {
                try
                {
                    ForecastResult forecast = forecaster(train, horizon);
                    accumulators[name].Add(actual, forecast.PointForecast, forecast.Lower, forecast.Upper,
                        train, seasonalPeriod, forecast.ConfidenceLevel);
                }
                catch (ArgumentException)
                {
                    // Модель не смогла обучиться на этом срезе — срез пропускается
                }
            }

            Vector naiveForecast = ForecastMetrics.Naive(train, horizon, seasonalPeriod);
            naive.Add(actual, naiveForecast, null, null, train, seasonalPeriod, double.NaN);
        }

        var summaries = models
            .Select(m => accumulators[m.Name].Summarize(m.Name))
            .Where(s => s.Folds > 0)
            .OrderBy(s => s.Mase)
            .ToList();

        return new BacktestResult
        {
            Models = summaries,
            Naive = naive.Summarize("Наивный прогноз"),
            Horizon = horizon,
            Folds = folds,
            SeasonalPeriod = seasonalPeriod,
        };
    }

    /// <summary>Накопитель метрик по срезам.</summary>
    private sealed class Accumulator(int horizon)
    {
        private readonly List<double> _mase = [];
        private readonly List<double> _sMape = [];
        private readonly List<double> _rmse = [];
        private readonly List<double> _mae = [];
        private readonly List<double> _pinballLower = [];
        private readonly List<double> _pinballUpper = [];
        private readonly List<double> _coverage = [];
        private readonly double[] _maeByHorizon = new double[horizon];
        private int _folds;

        public void Add(Vector actual, Vector forecast, Vector? lower, Vector? upper,
            Vector train, int seasonalPeriod, double confidenceLevel)
        {
            _folds++;
            _mase.Add(ForecastMetrics.Mase(actual, forecast, train, seasonalPeriod));
            _sMape.Add(ForecastMetrics.SMape(actual, forecast));
            _rmse.Add(ForecastMetrics.Rmse(actual, forecast));
            _mae.Add(ForecastMetrics.Mae(actual, forecast));

            for (int h = 0; h < actual.Count && h < _maeByHorizon.Length; h++)
                _maeByHorizon[h] += Math.Abs(actual[h] - forecast[h]);

            if (lower is null || upper is null || double.IsNaN(confidenceLevel)) return;

            double tail = (1 - confidenceLevel) / 2;
            _pinballLower.Add(ForecastMetrics.PinballLoss(actual, lower, tail));
            _pinballUpper.Add(ForecastMetrics.PinballLoss(actual, upper, 1 - tail));
            _coverage.Add(ForecastMetrics.Coverage(actual, lower, upper));
        }

        public BacktestSummary Summarize(string model)
        {
            var byHorizon = new Vector(_maeByHorizon.Length);
            for (int h = 0; h < _maeByHorizon.Length; h++)
                byHorizon[h] = _folds > 0 ? _maeByHorizon[h] / _folds : double.NaN;

            return new BacktestSummary
            {
                Model = model,
                Mase = Average(_mase),
                SMape = Average(_sMape),
                Rmse = Average(_rmse),
                Mae = Average(_mae),
                PinballLower = Average(_pinballLower),
                PinballUpper = Average(_pinballUpper),
                Coverage = Average(_coverage),
                Folds = _folds,
                MaeByHorizon = byHorizon,
            };
        }

        private static double Average(List<double> values)
        {
            var valid = values.Where(v => !double.IsNaN(v)).ToList();
            return valid.Count > 0 ? valid.Average() : double.NaN;
        }
    }
}
