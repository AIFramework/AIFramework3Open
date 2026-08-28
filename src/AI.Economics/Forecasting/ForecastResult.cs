using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;

namespace AI.Economics.Forecasting;

/// <summary>Прогноз ряда с интервалами и диагностикой посадки.</summary>
public sealed record ForecastResult : IInterpretable
{
    /// <summary>Название модели.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Точечный прогноз на горизонт.</summary>
    public Vector PointForecast { get; init; } = new Vector(0);

    /// <summary>Нижняя граница интервала прогноза.</summary>
    public Vector Lower { get; init; } = new Vector(0);

    /// <summary>Верхняя граница интервала прогноза.</summary>
    public Vector Upper { get; init; } = new Vector(0);

    /// <summary>Уровень доверия интервалов.</summary>
    public double ConfidenceLevel { get; init; } = 0.9;

    /// <summary>Модельные значения на обучающей выборке.</summary>
    public Vector Fitted { get; init; } = new Vector(0);

    /// <summary>Остатки модели.</summary>
    public Vector Residuals { get; init; } = new Vector(0);

    /// <summary>Оценённые параметры модели.</summary>
    public IReadOnlyDictionary<string, double> Parameters { get; init; } = new Dictionary<string, double>();

    /// <summary>Оценка стандартного отклонения ошибки на один шаг.</summary>
    public double Sigma { get; init; }

    /// <summary>Информационный критерий Акаике; <c>NaN</c>, если неприменим.</summary>
    public double Aic { get; init; } = double.NaN;

    /// <summary>Средняя абсолютная масштабированная ошибка на обучающей выборке.</summary>
    public double InSampleMase { get; init; } = double.NaN;

    /// <summary>Длина сезонного цикла, использованная моделью; 1 — сезонности нет.</summary>
    public int SeasonalPeriod { get; init; } = 1;

    /// <summary>Горизонт прогноза.</summary>
    public int Horizon => PointForecast.Count;

    /// <summary>Автокорреляция остатков первого порядка — проверка на недоописанную структуру.</summary>
    public double ResidualAutocorrelation { get; init; } = double.NaN;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double first = PointForecast.Count > 0 ? PointForecast[0] : double.NaN;
        double last = PointForecast.Count > 0 ? PointForecast[PointForecast.Count - 1] : double.NaN;
        double lastLower = Lower.Count > 0 ? Lower[Lower.Count - 1] : double.NaN;
        double lastUpper = Upper.Count > 0 ? Upper[Upper.Count - 1] : double.NaN;

        double relativeWidth = Math.Abs(last) > 1e-9 ? (lastUpper - lastLower) / Math.Abs(last) : double.NaN;
        bool autocorrelated = !double.IsNaN(ResidualAutocorrelation) && Math.Abs(ResidualAutocorrelation) > 0.3;
        bool beatsNaive = !double.IsNaN(InSampleMase) && InSampleMase < 1;

        var builder = new InterpretationBuilder($"Прогноз: {Model}")
            .Summary($"Горизонт {Horizon} периодов. Первое значение {Fmt.Num(first)}, последнее " +
                     $"{Fmt.Num(last)} с интервалом {Fmt.Pct(ConfidenceLevel, 0)} " +
                     $"[{Fmt.Num(lastLower)}; {Fmt.Num(lastUpper)}]. " +
                     (beatsNaive
                         ? $"Модель точнее наивного прогноза в {Fmt.Num(1 / Math.Max(InSampleMase, 1e-9))} раза."
                         : "Модель не превосходит наивный прогноз на обучающей выборке."))
            .Metric("Первое значение", first, null, "прогноз на ближайший период")
            .Metric("Последнее значение", last, null,
                $"интервал [{Fmt.Num(lastLower)}; {Fmt.Num(lastUpper)}]")
            .Metric("MASE на обучении", InSampleMase, null,
                "меньше 1 — точнее наивного прогноза",
                beatsNaive ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Сигма", Sigma, null, "стандартное отклонение ошибки на один шаг")
            .Metric("Ширина интервала", double.IsNaN(relativeWidth) ? "не определена" : Fmt.Pct(relativeWidth),
                null, "относительно прогнозного значения на конце горизонта",
                relativeWidth > 1 ? MetricQuality.Warning : MetricQuality.Neutral);

        if (!double.IsNaN(Aic))
            builder.Metric("AIC", Aic, null, "для сравнения моделей на одних данных", MetricQuality.Unknown, 1);

        if (!double.IsNaN(ResidualAutocorrelation))
        {
            builder.Metric("Автокорреляция остатков", ResidualAutocorrelation, null,
                "близкая к нулю означает, что структура ряда описана",
                autocorrelated ? MetricQuality.Warning : MetricQuality.Good);
        }

        foreach ((string name, double value) in Parameters)
            builder.Metric(name, value, null, null, MetricQuality.Unknown, 4);

        builder
            .FindingIf(SeasonalPeriod > 1,
                $"Модель учитывает сезонность с периодом {SeasonalPeriod}.")
            .FindingIf(Math.Abs(last - first) > Math.Abs(first) * 0.1 && Math.Abs(first) > 1e-9,
                last > first
                    ? $"Прогноз растёт: с {Fmt.Num(first)} до {Fmt.Num(last)} за горизонт."
                    : $"Прогноз снижается: с {Fmt.Num(first)} до {Fmt.Num(last)} за горизонт.")
            .FindingIf(!double.IsNaN(relativeWidth) && relativeWidth > 0.5,
                "Интервал широк. Для планирования используйте не точечный прогноз, а его нижнюю " +
                "границу: она отвечает на вопрос «сколько будет с вероятностью 90 %, не меньше».")
            .WarningIf(autocorrelated,
                $"Остатки автокоррелированы ({Fmt.Num(ResidualAutocorrelation)}): в ряде осталась " +
                "неописанная структура, интервалы прогноза занижены.")
            .WarningIf(!beatsNaive && !double.IsNaN(InSampleMase),
                "Модель не лучше наивного прогноза. Прежде чем усложнять, проверьте, " +
                "не является ли ряд случайным блужданием.")
            .Warning("Интервалы рассчитаны в предположении, что структура ряда не изменится. " +
                     "Они не покрывают риск структурного сдвига — смены цены, конкурента, регулирования.")
            .Recommendation("Проверьте модель бэктестом со скользящим началом: посадка " +
                            "на обучающей выборке систематически оптимистична.");

        return builder.Build();
    }
}

/// <summary>
/// Метрики качества прогноза.
/// </summary>
/// <remarks>
/// Ключевая метрика — MASE. В отличие от MAPE она определена при нулевых
/// значениях ряда, симметрична и сравнима между рядами разного масштаба.
/// Её единица имеет прямой смысл: значение 1 означает «точно как наивный
/// прогноз», значение 0,7 — «на 30 % точнее».
/// </remarks>
public static class ForecastMetrics
{
    /// <summary>Средняя абсолютная ошибка.</summary>
    /// <param name="actual">Факт.</param>
    /// <param name="forecast">Прогноз.</param>
    public static double Mae(Vector actual, Vector forecast)
    {
        Check(actual, forecast);
        double sum = 0;
        for (int i = 0; i < actual.Count; i++) sum += Math.Abs(actual[i] - forecast[i]);
        return sum / actual.Count;
    }

    /// <summary>Корень из среднего квадрата ошибки.</summary>
    /// <param name="actual">Факт.</param>
    /// <param name="forecast">Прогноз.</param>
    public static double Rmse(Vector actual, Vector forecast)
    {
        Check(actual, forecast);
        double sum = 0;
        for (int i = 0; i < actual.Count; i++)
        {
            double d = actual[i] - forecast[i];
            sum += d * d;
        }
        return Math.Sqrt(sum / actual.Count);
    }

    /// <summary>Симметричная средняя абсолютная процентная ошибка.</summary>
    /// <param name="actual">Факт.</param>
    /// <param name="forecast">Прогноз.</param>
    public static double SMape(Vector actual, Vector forecast)
    {
        Check(actual, forecast);
        double sum = 0;
        int counted = 0;

        for (int i = 0; i < actual.Count; i++)
        {
            double denominator = (Math.Abs(actual[i]) + Math.Abs(forecast[i])) / 2;
            if (denominator < 1e-12) continue;
            sum += Math.Abs(actual[i] - forecast[i]) / denominator;
            counted++;
        }

        return counted > 0 ? sum / counted : double.NaN;
    }

    /// <summary>
    /// Средняя абсолютная масштабированная ошибка: ошибка модели, делённая
    /// на ошибку наивного прогноза на обучающей выборке.
    /// </summary>
    /// <param name="actual">Факт на тестовой выборке.</param>
    /// <param name="forecast">Прогноз на тестовой выборке.</param>
    /// <param name="trainingSeries">Обучающая выборка для масштабирования.</param>
    /// <param name="seasonalPeriod">Период сезонности; 1 — обычный наивный прогноз.</param>
    /// <returns>Значение MASE; меньше единицы — модель лучше наивной.</returns>
    public static double Mase(Vector actual, Vector forecast, Vector trainingSeries, int seasonalPeriod = 1)
    {
        Check(actual, forecast);
        ArgumentNullException.ThrowIfNull(trainingSeries);

        int lag = Math.Max(seasonalPeriod, 1);
        if (trainingSeries.Count <= lag) return double.NaN;

        double scale = 0;
        for (int i = lag; i < trainingSeries.Count; i++)
            scale += Math.Abs(trainingSeries[i] - trainingSeries[i - lag]);
        scale /= trainingSeries.Count - lag;

        return scale > 1e-12 ? Mae(actual, forecast) / scale : double.NaN;
    }

    /// <summary>
    /// Пинболл-функция потерь для квантильного прогноза.
    /// </summary>
    /// <param name="actual">Факт.</param>
    /// <param name="quantileForecast">Прогноз заданного квантиля.</param>
    /// <param name="quantile">Уровень квантиля.</param>
    /// <returns>Средние потери; чем меньше, тем лучше калибровка.</returns>
    /// <remarks>
    /// Именно эта функция, а не MAE, оценивает интервальный прогноз: она
    /// штрафует занижение и завышение асимметрично, в соответствии с тем,
    /// какой квантиль обещан.
    /// </remarks>
    public static double PinballLoss(Vector actual, Vector quantileForecast, double quantile)
    {
        Check(actual, quantileForecast);

        double sum = 0;
        for (int i = 0; i < actual.Count; i++)
        {
            double d = actual[i] - quantileForecast[i];
            sum += d >= 0 ? quantile * d : (quantile - 1) * d;
        }

        return sum / actual.Count;
    }

    /// <summary>Доля фактических значений, попавших в интервал прогноза.</summary>
    /// <param name="actual">Факт.</param>
    /// <param name="lower">Нижняя граница.</param>
    /// <param name="upper">Верхняя граница.</param>
    public static double Coverage(Vector actual, Vector lower, Vector upper)
    {
        Check(actual, lower);
        Check(actual, upper);

        int inside = 0;
        for (int i = 0; i < actual.Count; i++)
            if (actual[i] >= lower[i] && actual[i] <= upper[i]) inside++;

        return (double)inside / actual.Count;
    }

    /// <summary>Наивный прогноз: повтор последнего значения либо значения год назад.</summary>
    /// <param name="series">Исторический ряд.</param>
    /// <param name="horizon">Горизонт.</param>
    /// <param name="seasonalPeriod">Период сезонности; 1 — обычный наивный прогноз.</param>
    public static Vector Naive(Vector series, int horizon, int seasonalPeriod = 1)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.Count == 0) throw new ArgumentException("Пустой ряд.", nameof(series));

        var forecast = new Vector(horizon);
        int lag = Math.Max(seasonalPeriod, 1);

        for (int h = 0; h < horizon; h++)
        {
            int index = series.Count - lag + (h % lag);
            forecast[h] = index >= 0 ? series[index] : series[series.Count - 1];
        }

        return forecast;
    }

    private static void Check(Vector a, Vector b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Count != b.Count || a.Count == 0)
            throw new ArgumentException("Ряды должны быть непустыми и одинаковой длины.", nameof(b));
    }
}
