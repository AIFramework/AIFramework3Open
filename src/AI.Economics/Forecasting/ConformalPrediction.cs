using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Forecasting;

/// <summary>Интервальный прогноз, откалиброванный по фактическим ошибкам.</summary>
public sealed record ConformalForecast : IInterpretable
{
    /// <summary>Точечный прогноз.</summary>
    public Vector PointForecast { get; init; } = new Vector(0);

    /// <summary>Нижняя граница интервала.</summary>
    public Vector Lower { get; init; } = new Vector(0);

    /// <summary>Верхняя граница интервала.</summary>
    public Vector Upper { get; init; } = new Vector(0);

    /// <summary>Заявленный уровень покрытия.</summary>
    public double ConfidenceLevel { get; init; }

    /// <summary>Границы прогноза по запрошенным квантилям: уровень — ряд значений.</summary>
    public IReadOnlyDictionary<double, Vector> Quantiles { get; init; } = new Dictionary<double, Vector>();

    /// <summary>Ширина интервала по горизонту.</summary>
    public Vector Width { get; init; } = new Vector(0);

    /// <summary>Фактическое покрытие на калибровочной выборке.</summary>
    public double CalibrationCoverage { get; init; }

    /// <summary>Покрытие исходных модельных интервалов на той же выборке.</summary>
    public double ModelCoverage { get; init; } = double.NaN;

    /// <summary>Число наблюдений в калибровочной выборке.</summary>
    public int CalibrationSize { get; init; }

    /// <summary>Растёт ли ширина интервала с горизонтом.</summary>
    public bool HorizonAware { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double averageWidth = Width.Count > 0 ? Width.Average() : double.NaN;
        double level = PointForecast.Count > 0 ? Math.Abs(PointForecast.Average()) : 1;
        double relativeWidth = level > 1e-9 ? averageWidth / level : double.NaN;
        bool modelMiscalibrated = !double.IsNaN(ModelCoverage)
                                  && Math.Abs(ModelCoverage - ConfidenceLevel) > 0.1;

        return new InterpretationBuilder("Конформный интервальный прогноз")
            .Summary($"Интервалы откалиброваны по {CalibrationSize} фактическим ошибкам и дают " +
                     $"покрытие {Fmt.Pct(CalibrationCoverage)} при заявленном " +
                     $"{Fmt.Pct(ConfidenceLevel, 0)}. Средняя ширина интервала — " +
                     $"{Fmt.Num(averageWidth)}, это {Fmt.Pct(relativeWidth)} прогнозного уровня." +
                     (modelMiscalibrated
                         ? $" Исходные интервалы модели покрывали лишь {Fmt.Pct(ModelCoverage)}."
                         : string.Empty))
            .Metric("Покрытие", Fmt.Pct(CalibrationCoverage), null,
                "доля фактов внутри интервала на калибровке",
                Math.Abs(CalibrationCoverage - ConfidenceLevel) < 0.05
                    ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Покрытие модели", double.IsNaN(ModelCoverage) ? "не оценено" : Fmt.Pct(ModelCoverage),
                null, "что давали интервалы до калибровки",
                modelMiscalibrated ? MetricQuality.Critical : MetricQuality.Neutral)
            .Metric("Средняя ширина", averageWidth, null, "в единицах ряда")
            .Metric("Относительная ширина", Fmt.Pct(relativeWidth), null,
                "к среднему уровню прогноза",
                relativeWidth > 0.6 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Калибровочная выборка", CalibrationSize, "наблюдений",
                "чем больше, тем точнее покрытие",
                CalibrationSize >= 50 ? MetricQuality.Good : MetricQuality.Warning, 0)
            .Finding("Конформный метод не делает предположений о распределении ошибки. " +
                     "Покрытие гарантировано при единственном условии — обменности " +
                     "калибровочных и будущих ошибок.")
            .FindingIf(modelMiscalibrated,
                $"Интервалы исходной модели были откалиброваны неверно: они покрывали " +
                $"{Fmt.Pct(ModelCoverage)} вместо {Fmt.Pct(ConfidenceLevel, 0)}. " +
                "Планирование запаса по ним дало бы не тот уровень сервиса.")
            .FindingIf(HorizonAware,
                "Ширина интервала растёт с горизонтом: калибровка выполнена отдельно " +
                "для каждого шага прогноза.")
            .FindingIf(Quantiles.Count > 0,
                $"Помимо интервала рассчитаны квантили: " +
                $"{string.Join(", ", Quantiles.Keys.OrderBy(q => q).Select(q => Fmt.Pct(q, 0)))}. " +
                "Именно они нужны для решений вида «сколько закупить, чтобы хватило " +
                "с вероятностью 95 %».")
            .WarningIf(CalibrationSize < 30,
                $"Калибровочных наблюдений всего {CalibrationSize}. Эмпирический квантиль " +
                "по такой выборке нестабилен, фактическое покрытие будет плавать.")
            .Warning("Обменность нарушается при структурном сдвиге: если после калибровки " +
                     "изменились цены, конкуренты или сезонный профиль, гарантия покрытия " +
                     "перестаёт действовать.")
            .Recommendation("Для планирования используйте не точечный прогноз, а нижний квантиль: " +
                            "он отвечает на вопрос «сколько будет как минимум с заданной вероятностью».")
            .Recommendation("Перекалибровывайте интервалы регулярно на свежих ошибках — " +
                            "это дешевле, чем переобучать саму модель.")
            .Build();
    }
}

/// <summary>
/// Конформное предсказание: интервалы с гарантированным покрытием, полученные
/// из фактических ошибок модели.
/// </summary>
/// <remarks>
/// <para>
/// Интервалы, которые выдают ARIMA и экспоненциальное сглаживание, опираются
/// на предположение о нормальности ошибок и правильности самой модели.
/// На практике оба предположения нарушаются, и заявленный 90-процентный
/// интервал покрывает 70 % фактов. Планирование запаса по таким интервалам
/// даёт не тот уровень сервиса, на который рассчитывали.
/// </para>
/// <para>
/// Конформный метод берёт ошибки модели на отложенной части истории и строит
/// интервал как эмпирический квантиль их абсолютных величин. Единственное
/// требование — обменность калибровочных и будущих ошибок; никаких
/// предположений о виде распределения не нужно.
/// </para>
/// <para>
/// Калибровка выполняется отдельно для каждого шага горизонта: ошибка
/// прогноза на месяц вперёд и на полгода вперёд имеет разный масштаб,
/// и общий квантиль был бы слишком широк для ближнего горизонта и слишком
/// узок для дальнего.
/// </para>
/// </remarks>
public static class ConformalPrediction
{
    /// <summary>
    /// Калибрует интервалы по ошибкам модели на скользящем начале.
    /// </summary>
    /// <param name="series">Полный исторический ряд.</param>
    /// <param name="forecaster">Функция «история и горизонт — прогноз».</param>
    /// <param name="horizon">Горизонт прогноза.</param>
    /// <param name="confidenceLevel">Требуемый уровень покрытия.</param>
    /// <param name="calibrationFolds">Число срезов калибровки.</param>
    /// <param name="quantiles">Дополнительные квантили прогноза.</param>
    /// <returns>Откалиброванный интервальный прогноз на будущее.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    /// <exception cref="ArgumentException">Ряд слишком короткий.</exception>
    public static ConformalForecast Calibrate(
        Vector series,
        Func<Vector, int, ForecastResult> forecaster,
        int horizon = 6,
        double confidenceLevel = 0.9,
        int calibrationFolds = 20,
        IReadOnlyList<double>? quantiles = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(forecaster);

        int n = series.Count;
        if (n < horizon + 15)
            throw new ArgumentException("Ряд слишком короткий для калибровки.", nameof(series));

        int minimumTrain = Math.Max(n - calibrationFolds - horizon, 10);
        var errorsByStep = new List<double>[horizon];
        var signedByStep = new List<double>[horizon];
        for (int h = 0; h < horizon; h++)
        {
            errorsByStep[h] = [];
            signedByStep[h] = [];
        }

        int inModelInterval = 0, totalPoints = 0;

        for (int trainEnd = minimumTrain; trainEnd + horizon <= n; trainEnd++)
        {
            var train = new Vector(trainEnd);
            for (int t = 0; t < trainEnd; t++) train[t] = series[t];

            ForecastResult forecast;
            try
            {
                forecast = forecaster(train, horizon);
            }
            catch (ArgumentException)
            {
                continue;
            }

            for (int h = 0; h < horizon; h++)
            {
                double actual = series[trainEnd + h];
                double predicted = forecast.PointForecast[h];

                errorsByStep[h].Add(Math.Abs(actual - predicted));
                signedByStep[h].Add(actual - predicted);

                if (forecast.Lower.Count > h && forecast.Upper.Count > h)
                {
                    totalPoints++;
                    if (actual >= forecast.Lower[h] && actual <= forecast.Upper[h]) inModelInterval++;
                }
            }
        }

        int calibrationSize = errorsByStep[0].Count;
        if (calibrationSize < 5)
            throw new ArgumentException("Не удалось собрать калибровочную выборку.", nameof(series));

        ForecastResult final = forecaster(series, horizon);

        var lower = new Vector(horizon);
        var upper = new Vector(horizon);
        var width = new Vector(horizon);
        var quantileSeries = new Dictionary<double, Vector>();

        foreach (double q in quantiles ?? [])
            quantileSeries[q] = new Vector(horizon);

        double alpha = 1 - confidenceLevel;
        int covered = 0, checkedPoints = 0;

        for (int h = 0; h < horizon; h++)
        {
            double[] absolute = [.. errorsByStep[h].OrderBy(v => v)];
            double[] signed = [.. signedByStep[h].OrderBy(v => v)];

            // Поправка на конечность выборки: квантиль берётся с запасом,
            // иначе покрытие систематически ниже заявленного
            double level = Math.Min(1.0, (1 - alpha) * (1 + (1.0 / absolute.Length)));
            double radius = EconMath.Quantile(absolute, level);

            lower[h] = final.PointForecast[h] - radius;
            upper[h] = final.PointForecast[h] + radius;
            width[h] = 2 * radius;

            foreach (double q in quantileSeries.Keys.ToList())
                quantileSeries[q][h] = final.PointForecast[h] + EconMath.Quantile(signed, q);

            foreach (double error in errorsByStep[h])
            {
                checkedPoints++;
                if (error <= radius) covered++;
            }
        }

        return new ConformalForecast
        {
            PointForecast = final.PointForecast,
            Lower = lower,
            Upper = upper,
            ConfidenceLevel = confidenceLevel,
            Quantiles = quantileSeries,
            Width = width,
            CalibrationCoverage = checkedPoints > 0 ? (double)covered / checkedPoints : double.NaN,
            ModelCoverage = totalPoints > 0 ? (double)inModelInterval / totalPoints : double.NaN,
            CalibrationSize = calibrationSize,
            HorizonAware = true,
        };
    }
}
