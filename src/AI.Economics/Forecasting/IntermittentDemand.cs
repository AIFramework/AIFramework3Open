using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Forecasting;

/// <summary>Метод прогнозирования прерывистого спроса.</summary>
public enum IntermittentMethod
{
    /// <summary>Кростон: раздельное сглаживание размера спроса и интервала между продажами.</summary>
    Croston,

    /// <summary>Синтетоса — Бойлана: Кростон с поправкой на систематическое смещение.</summary>
    SyntetosBoylan,

    /// <summary>Тэйл — Син — Бабай: сглаживание вероятности спроса, а не интервала.</summary>
    TeunterSyntetosBabai,
}

/// <summary>Результат прогноза прерывистого спроса.</summary>
public sealed record IntermittentForecast : IInterpretable
{
    /// <summary>Использованный метод.</summary>
    public IntermittentMethod Method { get; init; }

    /// <summary>Прогноз среднего спроса за период — величина постоянная по горизонту.</summary>
    public double DemandPerPeriod { get; init; }

    /// <summary>Прогноз на горизонт.</summary>
    public Vector PointForecast { get; init; } = new Vector(0);

    /// <summary>Средний размер спроса в периодах, когда он был.</summary>
    public double AverageDemandSize { get; init; }

    /// <summary>Средний интервал между периодами со спросом.</summary>
    public double AverageInterval { get; init; }

    /// <summary>Доля периодов с нулевым спросом.</summary>
    public double ZeroShare { get; init; }

    /// <summary>Квадрат коэффициента вариации размера спроса.</summary>
    public double SquaredCoefficientOfVariation { get; init; }

    /// <summary>
    /// Классификация ряда по Синтетосу — Бойлану — Кроустону: гладкий,
    /// прерывистый, неравномерный или комковатый.
    /// </summary>
    public string DemandPattern { get; init; } = string.Empty;

    /// <summary>Рекомендованный метод для этого типа ряда.</summary>
    public IntermittentMethod RecommendedMethod { get; init; }

    /// <summary>Страховой запас для заданного уровня сервиса.</summary>
    public double SafetyStock { get; init; }

    /// <summary>Уровень сервиса, для которого рассчитан запас.</summary>
    public double ServiceLevel { get; init; }

    /// <summary>Точка перезаказа: спрос за срок поставки плюс страховой запас.</summary>
    public double ReorderPoint { get; init; }

    /// <summary>Срок поставки в периодах.</summary>
    public double LeadTime { get; init; }

    /// <summary>Средняя абсолютная масштабированная ошибка на обучающей выборке.</summary>
    public double InSampleMase { get; init; }

    /// <summary>Наблюдений в ряде.</summary>
    public int Observations { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool wrongMethod = Method != RecommendedMethod;
        bool veryIntermittent = ZeroShare > 0.7;

        return new InterpretationBuilder("Прогноз прерывистого спроса")
            .Summary($"Ряд классифицирован как «{DemandPattern}»: {Fmt.Pct(ZeroShare)} периодов " +
                     $"без продаж, средний размер заказа {Fmt.Num(AverageDemandSize)} при интервале " +
                     $"{Fmt.Num(AverageInterval)} периодов. Прогноз среднего спроса — " +
                     $"{Fmt.Num(DemandPerPeriod)} за период; точка перезаказа " +
                     $"{Fmt.Num(ReorderPoint)} при уровне сервиса {Fmt.Pct(ServiceLevel, 0)}.")
            .Metric("Спрос за период", DemandPerPeriod, null,
                "постоянная величина: метод не прогнозирует, когда именно будет заказ")
            .Metric("Доля нулей", Fmt.Pct(ZeroShare), null, "периодов без продаж",
                veryIntermittent ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Средний интервал", AverageInterval, "периодов", "между заказами")
            .Metric("Размер заказа", AverageDemandSize, null, "когда спрос есть")
            .Metric("CV2 размера", SquaredCoefficientOfVariation, null,
                "порог 0,49 отделяет ровный спрос от неравномерного")
            .Metric("Точка перезаказа", ReorderPoint, null,
                $"спрос за {Fmt.Num(LeadTime, 1)} периодов поставки плюс страховой запас " +
                $"{Fmt.Num(SafetyStock)}", MetricQuality.Good)
            .Metric("MASE", InSampleMase, null, "меньше 1 — точнее наивного прогноза",
                InSampleMase < 1 ? MetricQuality.Good : MetricQuality.Warning)
            .Finding("Классические методы на таком ряде дают систематическую ошибку: они " +
                     "усредняют нули с продажами и предсказывают дробный спрос каждый период, " +
                     "тогда как физически спрос приходит редко и крупно.")
            .FindingIf(wrongMethod,
                $"Для ряда типа «{DemandPattern}» уместнее метод {Name(RecommendedMethod)}, " +
                $"а использован {Name(Method)}.")
            .FindingIf(Method == IntermittentMethod.Croston,
                "Метод Кростона даёт смещённую вверх оценку: отношение средних не равно среднему " +
                "отношений. Поправка Синтетоса — Бойлана убирает это смещение.")
            .FindingIf(Method == IntermittentMethod.TeunterSyntetosBabai,
                "Метод TSB обновляет вероятность спроса каждый период, поэтому реагирует " +
                "на прекращение продаж. Кростон в такой ситуации держит старый прогноз бесконечно.")
            .WarningIf(veryIntermittent,
                $"Спрос очень редкий ({Fmt.Pct(ZeroShare)} нулей). Любая точечная оценка здесь " +
                "малоинформативна: планировать надо от точки перезаказа и уровня сервиса, " +
                "а не от прогноза за период.")
            .WarningIf(Observations < 24,
                $"Наблюдений всего {Observations}: параметры сглаживания оценены грубо.")
            .Warning("Прогноз даёт средний спрос, а не момент следующего заказа. Для планирования " +
                     "запаса используйте точку перезаказа, для планирования производства — " +
                     "агрегированный спрос по группе позиций.")
            .Recommendation("Оценивайте такие ряды по уровню сервиса и издержкам хранения, " +
                            "а не по MAPE: процентная ошибка на нулевых периодах не определена.")
            .Build();
    }

    private static string Name(IntermittentMethod method) => method switch
    {
        IntermittentMethod.Croston => "Кростона",
        IntermittentMethod.SyntetosBoylan => "Синтетоса — Бойлана",
        IntermittentMethod.TeunterSyntetosBabai => "TSB",
        _ => method.ToString(),
    };
}

/// <summary>
/// Прогнозирование прерывистого спроса: запчасти, B2B-заказы, длинный хвост
/// ассортимента.
/// </summary>
/// <remarks>
/// <para>
/// На рядах, где продажи случаются раз в несколько периодов, классические
/// методы ломаются: экспоненциальное сглаживание усредняет нули с продажами
/// и выдаёт дробный спрос каждый период, а процентные метрики ошибки
/// не определены на нулях.
/// </para>
/// <para>
/// Кростон разделяет задачу: отдельно сглаживается размер спроса в тех
/// периодах, когда он был, и отдельно интервал между такими периодами.
/// Прогноз — их отношение. Синтетос и Бойлан заметили, что отношение
/// сглаженных величин смещено вверх, и предложили поправку. Метод TSB
/// вместо интервала сглаживает вероятность спроса и потому единственный
/// из трёх реагирует на то, что позиция перестала продаваться.
/// </para>
/// </remarks>
public static class IntermittentDemand
{
    /// <summary>Строит прогноз прерывистого спроса.</summary>
    /// <param name="series">Ряд спроса по периодам, с нулями.</param>
    /// <param name="method">Метод прогнозирования.</param>
    /// <param name="horizon">Горизонт прогноза.</param>
    /// <param name="alpha">Параметр сглаживания размера спроса.</param>
    /// <param name="beta">
    /// Параметр сглаживания интервала или вероятности; <c>NaN</c> — использовать
    /// то же значение, что и для размера.
    /// </param>
    /// <param name="leadTime">Срок поставки в периодах.</param>
    /// <param name="serviceLevel">Целевой уровень сервиса.</param>
    /// <returns>Прогноз, классификация ряда и параметры запаса.</returns>
    /// <exception cref="ArgumentNullException">Ряд не задан.</exception>
    /// <exception cref="ArgumentException">Мало наблюдений или нет ни одной продажи.</exception>
    public static IntermittentForecast Fit(
        Vector series, IntermittentMethod method = IntermittentMethod.SyntetosBoylan,
        int horizon = 12, double alpha = 0.1, double beta = double.NaN,
        double leadTime = 4, double serviceLevel = 0.95)
    {
        ArgumentNullException.ThrowIfNull(series);

        double[] y = [.. series];
        int n = y.Length;
        if (n < 8) throw new ArgumentException("Нужно минимум восемь наблюдений.", nameof(series));
        if (y.All(v => v <= 0)) throw new ArgumentException("В ряде нет ни одной продажи.", nameof(series));

        if (double.IsNaN(beta)) beta = alpha;

        double[] sizes = [.. y.Where(v => v > 0)];
        double zeroShare = y.Count(v => v <= 0) / (double)n;
        double averageSize = sizes.Average();
        double sizeVariance = sizes.Length > 1
            ? sizes.Sum(v => (v - averageSize) * (v - averageSize)) / (sizes.Length - 1)
            : 0;
        double cv2 = averageSize > 1e-9 ? sizeVariance / (averageSize * averageSize) : 0;

        double averageInterval = sizes.Length > 0 ? (double)n / sizes.Length : n;

        (double demandPerPeriod, double[] fitted) = Smooth(y, method, alpha, beta);

        string pattern = Classify(averageInterval, cv2);
        IntermittentMethod recommended = Recommend(averageInterval, cv2);

        // Дисперсия спроса за срок поставки: сумма дисперсии размера
        // и вклада случайности самого факта заказа
        double probability = 1.0 / Math.Max(averageInterval, 1);
        double leadDemand = demandPerPeriod * leadTime;
        double leadVariance = leadTime * ((probability * sizeVariance)
                                        + (probability * (1 - probability) * averageSize * averageSize));
        double z = EconMath.NormalInv(serviceLevel);
        double safety = z * Math.Sqrt(Math.Max(leadVariance, 0));

        var forecast = new Vector(horizon);
        for (int h = 0; h < horizon; h++) forecast[h] = demandPerPeriod;

        return new IntermittentForecast
        {
            Method = method,
            DemandPerPeriod = demandPerPeriod,
            PointForecast = forecast,
            AverageDemandSize = averageSize,
            AverageInterval = averageInterval,
            ZeroShare = zeroShare,
            SquaredCoefficientOfVariation = cv2,
            DemandPattern = pattern,
            RecommendedMethod = recommended,
            SafetyStock = safety,
            ServiceLevel = serviceLevel,
            ReorderPoint = leadDemand + safety,
            LeadTime = leadTime,
            InSampleMase = Mase(y, fitted),
            Observations = n,
        };
    }

    /// <summary>Сравнивает все три метода на одном ряде.</summary>
    /// <param name="series">Ряд спроса.</param>
    /// <param name="horizon">Горизонт прогноза.</param>
    /// <param name="alpha">Параметр сглаживания.</param>
    /// <param name="leadTime">Срок поставки.</param>
    /// <param name="serviceLevel">Уровень сервиса.</param>
    /// <returns>Результаты в порядке возрастания ошибки.</returns>
    public static IReadOnlyList<IntermittentForecast> CompareAll(
        Vector series, int horizon = 12, double alpha = 0.1, double leadTime = 4, double serviceLevel = 0.95)
    {
        IntermittentMethod[] methods =
        [
            IntermittentMethod.Croston,
            IntermittentMethod.SyntetosBoylan,
            IntermittentMethod.TeunterSyntetosBabai,
        ];

        return [.. methods
            .Select(m => Fit(series, m, horizon, alpha, double.NaN, leadTime, serviceLevel))
            .OrderBy(r => r.InSampleMase)];
    }

    /// <summary>Рекуррентное сглаживание по выбранному методу.</summary>
    private static (double Forecast, double[] Fitted) Smooth(
        double[] y, IntermittentMethod method, double alpha, double beta)
    {
        int n = y.Length;
        var fitted = new double[n];

        int firstDemand = Array.FindIndex(y, v => v > 0);
        double size = y[firstDemand];
        double interval = Math.Max(firstDemand + 1, 1);
        double probability = 1.0 / interval;
        int sinceLast = 0;

        for (int t = 0; t < n; t++)
        {
            fitted[t] = method switch
            {
                IntermittentMethod.Croston => size / Math.Max(interval, 1e-9),
                IntermittentMethod.SyntetosBoylan => (1 - (beta / 2)) * size / Math.Max(interval, 1e-9),
                _ => probability * size,
            };

            if (method == IntermittentMethod.TeunterSyntetosBabai)
            {
                // Вероятность обновляется каждый период, в том числе нулевой:
                // именно это делает метод чувствительным к прекращению продаж
                probability = (beta * (y[t] > 0 ? 1 : 0)) + ((1 - beta) * probability);
                if (y[t] > 0) size = (alpha * y[t]) + ((1 - alpha) * size);
                continue;
            }

            sinceLast++;
            if (y[t] > 0)
            {
                size = (alpha * y[t]) + ((1 - alpha) * size);
                interval = (beta * sinceLast) + ((1 - beta) * interval);
                sinceLast = 0;
            }
        }

        double forecast = method switch
        {
            IntermittentMethod.Croston => size / Math.Max(interval, 1e-9),
            IntermittentMethod.SyntetosBoylan => (1 - (beta / 2)) * size / Math.Max(interval, 1e-9),
            _ => probability * size,
        };

        return (forecast, fitted);
    }

    /// <summary>
    /// Классификация Синтетоса — Бойлана — Кроустона по среднему интервалу
    /// и вариации размера спроса.
    /// </summary>
    private static string Classify(double interval, double cv2) => (interval < 1.32, cv2 < 0.49) switch
    {
        (true, true) => "гладкий",
        (true, false) => "неравномерный",
        (false, true) => "прерывистый",
        (false, false) => "комковатый",
    };

    private static IntermittentMethod Recommend(double interval, double cv2)
    {
        if (interval < 1.32 && cv2 < 0.49) return IntermittentMethod.Croston;
        if (interval > 2.5) return IntermittentMethod.TeunterSyntetosBabai;
        return IntermittentMethod.SyntetosBoylan;
    }

    private static double Mase(double[] y, double[] fitted)
    {
        if (y.Length < 2) return double.NaN;

        double scale = 0;
        for (int t = 1; t < y.Length; t++) scale += Math.Abs(y[t] - y[t - 1]);
        scale /= y.Length - 1;

        double mae = 0;
        for (int t = 1; t < y.Length; t++) mae += Math.Abs(y[t] - fitted[t]);
        mae /= y.Length - 1;

        return scale > 1e-12 ? mae / scale : double.NaN;
    }
}
