using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Pricing;

/// <summary>Оценка одного коэффициента conjoint-модели.</summary>
public sealed record PartWorth
{
    /// <summary>Имя атрибута и уровня.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Частная полезность.</summary>
    public double Utility { get; init; }

    /// <summary>Стандартная ошибка оценки.</summary>
    public double StandardError { get; init; }

    /// <summary>Двустороннее p-значение.</summary>
    public double PValue { get; init; }

    /// <summary>
    /// Готовность платить за уровень в деньгах. Определена, если в плане
    /// задан числовой ценовой атрибут.
    /// </summary>
    public double WillingnessToPay { get; init; } = double.NaN;
}

/// <summary>Результат оценки conjoint-модели.</summary>
public sealed record ConjointResult : IInterpretable
{
    /// <summary>Частные полезности по уровням атрибутов.</summary>
    public IReadOnlyList<PartWorth> PartWorths { get; init; } = [];

    /// <summary>Относительная важность атрибутов, доли суммируются в единицу.</summary>
    public IReadOnlyDictionary<string, double> AttributeImportance { get; init; } =
        new Dictionary<string, double>();

    /// <summary>Логарифм правдоподобия модели.</summary>
    public double LogLikelihood { get; init; }

    /// <summary>Логарифм правдоподобия модели без коэффициентов.</summary>
    public double NullLogLikelihood { get; init; }

    /// <summary>Псевдо-R2 Макфаддена. Значения 0,2–0,4 считаются хорошими.</summary>
    public double McFaddenR2 => NullLogLikelihood < 0 ? 1.0 - (LogLikelihood / NullLogLikelihood) : 0;

    /// <summary>Доля заданий, в которых модель угадала выбор респондента.</summary>
    public double HitRate { get; init; }

    /// <summary>Число заданий на выбор.</summary>
    public int Tasks { get; init; }

    /// <summary>Число респондентов.</summary>
    public int Respondents { get; init; }

    /// <summary>Коэффициент при цене; отрицателен у нормальной модели.</summary>
    public double PriceCoefficient { get; init; } = double.NaN;

    /// <summary>Оценивалась ли индивидуальная гетерогенность (иерархический байес).</summary>
    public bool IsHierarchical { get; init; }

    /// <summary>Разброс индивидуальных полезностей по популяции; пуст для агрегатной модели.</summary>
    public IReadOnlyList<double> HeterogeneityStdDev { get; init; } = [];

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var topAttribute = AttributeImportance.OrderByDescending(kv => kv.Value).FirstOrDefault();
        PartWorth? bestLevel = PartWorths.OrderByDescending(p => p.Utility).FirstOrDefault();
        bool priceWrongSign = !double.IsNaN(PriceCoefficient) && PriceCoefficient > 0;
        int insignificant = PartWorths.Count(p => p.PValue > 0.05);

        // Значение считается заранее: текст сообщения вычисляется раньше,
        // чем проверяется условие его добавления
        double maxHeterogeneity = HeterogeneityStdDev.Count > 0 ? HeterogeneityStdDev.Max() : 0;

        var builder = new InterpretationBuilder(
                IsHierarchical ? "Conjoint: иерархическая байесовская модель" : "Conjoint: агрегатная модель MNL")
            .Summary($"Выбор объясняется в первую очередь атрибутом «{topAttribute.Key}» " +
                     $"({Fmt.Pct(topAttribute.Value)} важности). Модель угадывает выбор респондента " +
                     $"в {Fmt.Pct(HitRate)} заданий при {Fmt.Pct(1.0 / Math.Max(AverageAlternatives(), 2))} " +
                     $"у случайного угадывания.")
            .Metric("Псевдо-R2 Макфаддена", McFaddenR2, null,
                "0,2-0,4 соответствует хорошей посадке",
                McFaddenR2 >= 0.2 ? MetricQuality.Good : McFaddenR2 >= 0.1 ? MetricQuality.Warning : MetricQuality.Critical)
            .Metric("Доля угаданных выборов", Fmt.Pct(HitRate), null, "доля заданий, где модель права",
                HitRate > 0.5 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Заданий", Tasks, null, null, MetricQuality.Unknown, 0)
            .Metric("Респондентов", Respondents, null, null, MetricQuality.Unknown, 0);

        if (!double.IsNaN(PriceCoefficient))
        {
            builder.Metric("Коэффициент при цене", PriceCoefficient, null,
                "полезность на единицу цены, должен быть отрицательным",
                priceWrongSign ? MetricQuality.Critical : MetricQuality.Good, 6);
        }

        foreach ((string attribute, double importance) in AttributeImportance.OrderByDescending(kv => kv.Value))
            builder.Metric($"Важность: {attribute}", Fmt.Pct(importance), null, null, MetricQuality.Unknown);

        builder
            .FindingIf(bestLevel is not null && !double.IsNaN(bestLevel.WillingnessToPay),
                $"Наибольшую ценность даёт «{bestLevel?.Name}»: аудитория готова доплатить за него " +
                $"{Fmt.Money(bestLevel?.WillingnessToPay ?? 0)}.")
            .FindingIf(topAttribute.Value > 0.5,
                $"Атрибут «{topAttribute.Key}» определяет выбор более чем наполовину. Остальные " +
                "свойства товара в этой категории почти не влияют на решение.")
            .FindingIf(IsHierarchical && maxHeterogeneity > 0,
                $"Разброс индивидуальных предпочтений заметен: стандартное отклонение полезностей " +
                $"по популяции достигает {Fmt.Num(maxHeterogeneity)}. Единое предложение " +
                "для всей аудитории оставляет деньги на столе.")
            .WarningIf(priceWrongSign,
                "Коэффициент при цене положителен: модель утверждает, что дороже — привлекательнее. " +
                "Обычно это признак ошибки в кодировании цены или слишком узкого её диапазона.")
            .WarningIf(McFaddenR2 < 0.1,
                $"Псевдо-R2 всего {Fmt.Num(McFaddenR2)}: выбор респондентов почти не объясняется " +
                "атрибутами. Проверьте, не был ли дизайн слишком сложным для восприятия.")
            .WarningIf(insignificant > 0,
                $"{insignificant} уровней незначимы: их полезность неотличима от базового уровня.")
            .WarningIf(!IsHierarchical,
                "Агрегатная модель приписывает всем респондентам одинаковые предпочтения. " +
                "На неоднородной аудитории она смещает симуляцию долей — сравните с байесовской версией.")
            .Warning("Conjoint измеряет заявленный выбор в отсутствие бюджетного ограничения. " +
                     "Абсолютные доли завышены, относительные сравнения надёжнее.")
            .RecommendationIf(!IsHierarchical,
                "Оцените иерархическую байесовскую версию: она даёт индивидуальные полезности " +
                "и позволяет сегментировать аудиторию по предпочтениям.")
            .Recommendation("Проверьте выводы симулятором долей на реальных конфигурациях товара " +
                            "и конкурентов, а не на средних полезностях.");

        return builder.Build();
    }

    private double AverageAlternatives() => 3;
}

/// <summary>
/// Агрегатная модель дискретного выбора (мультиномиальный логит) для
/// conjoint-исследований.
/// </summary>
/// <remarks>
/// <para>
/// Вероятность выбора карточки пропорциональна экспоненте её полезности:
/// </para>
/// <code>
/// P(j) = exp(beta' x_j) / sum_k exp(beta' x_k)
/// </code>
/// <para>
/// Логарифм правдоподобия вогнут, градиент и гессиан выписываются в явном
/// виде, поэтому метод Ньютона сходится за несколько итераций и не требует
/// подбора начального приближения.
/// </para>
/// </remarks>
public sealed class MultinomialLogit
{
    private double[] _beta = [];
    private double[] _standardErrors = [];

    /// <summary>Оценённые полезности.</summary>
    public Vector Coefficients => new(_beta);

    /// <summary>План исследования, использованный при обучении.</summary>
    public ConjointDesign? Design { get; private set; }

    /// <summary>Обучает модель методом максимального правдоподобия.</summary>
    /// <param name="tasks">Задания на выбор.</param>
    /// <param name="design">План исследования.</param>
    /// <param name="maxIterations">Максимум итераций Ньютона.</param>
    /// <returns>Оценки, важности атрибутов и качество модели.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    /// <exception cref="ArgumentException">Заданий нет.</exception>
    public ConjointResult Fit(IReadOnlyList<ChoiceTask> tasks, ConjointDesign design, int maxIterations = 50)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(design);
        if (tasks.Count == 0) throw new ArgumentException("Нужно хотя бы одно задание.", nameof(tasks));

        Design = design;
        int k = design.ParameterCount;
        _beta = new double[k];

        double[][][] encoded = Encode(tasks, design);

        double[,]? lastInverse = null;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            var gradient = new double[k];
            var information = new double[k, k];

            for (int t = 0; t < tasks.Count; t++)
            {
                double[] probabilities = Probabilities(encoded[t], _beta);
                double[][] rows = encoded[t];

                var mean = new double[k];
                for (int j = 0; j < rows.Length; j++)
                    for (int a = 0; a < k; a++) mean[a] += probabilities[j] * rows[j][a];

                for (int a = 0; a < k; a++)
                    gradient[a] += rows[tasks[t].ChosenIndex][a] - mean[a];

                for (int j = 0; j < rows.Length; j++)
                    for (int a = 0; a < k; a++)
                        for (int b = 0; b < k; b++)
                            information[a, b] += probabilities[j] * rows[j][a] * rows[j][b];

                for (int a = 0; a < k; a++)
                    for (int b = 0; b < k; b++) information[a, b] -= mean[a] * mean[b];
            }

            // Небольшая регуляризация: в дробном плане часть столбцов почти коллинеарна
            for (int a = 0; a < k; a++) information[a, a] += 1e-6;

            double[,]? inverse = EconMath.Inverse(information);
            if (inverse is null) break;
            lastInverse = inverse;

            double shift = 0;
            for (int a = 0; a < k; a++)
            {
                double step = 0;
                for (int b = 0; b < k; b++) step += inverse[a, b] * gradient[b];
                step = EconMath.Clamp(step, -2, 2);
                _beta[a] += step;
                shift += Math.Abs(step);
            }

            if (shift < 1e-9) break;
        }

        _standardErrors = new double[k];
        for (int a = 0; a < k; a++)
            _standardErrors[a] = lastInverse is null ? double.NaN : Math.Sqrt(Math.Max(lastInverse[a, a], 0));

        return BuildResult(tasks, design, encoded, _beta, _standardErrors, isHierarchical: false, []);
    }

    /// <summary>
    /// Симулятор долей: предсказанные доли выбора для набора карточек.
    /// </summary>
    /// <param name="profiles">Конфигурации товаров, включая конкурентов.</param>
    /// <returns>Доли, суммирующиеся в единицу.</returns>
    /// <exception cref="InvalidOperationException">Модель не обучена.</exception>
    public Vector SimulateShares(IReadOnlyList<ConjointProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (Design is null) throw new InvalidOperationException("Сначала обучите модель.");

        double[][] rows = [.. profiles.Select(Design.Encode)];
        double[] shares = Probabilities(rows, _beta);
        return new Vector(shares);
    }

    /// <summary>Кодирует все задания в матрицы признаков.</summary>
    internal static double[][][] Encode(IReadOnlyList<ChoiceTask> tasks, ConjointDesign design)
    {
        var encoded = new double[tasks.Count][][];
        for (int t = 0; t < tasks.Count; t++)
        {
            encoded[t] = new double[tasks[t].Alternatives.Count][];
            for (int j = 0; j < tasks[t].Alternatives.Count; j++)
                encoded[t][j] = design.Encode(tasks[t].Alternatives[j]);
        }
        return encoded;
    }

    /// <summary>Вероятности выбора альтернатив со сдвигом на максимум.</summary>
    internal static double[] Probabilities(double[][] rows, double[] beta)
    {
        int j = rows.Length;
        var utilities = new double[j];
        double max = double.NegativeInfinity;

        for (int i = 0; i < j; i++)
        {
            double u = 0;
            for (int a = 0; a < beta.Length; a++) u += beta[a] * rows[i][a];
            utilities[i] = u;
            if (u > max) max = u;
        }

        double sum = 0;
        for (int i = 0; i < j; i++)
        {
            utilities[i] = Math.Exp(utilities[i] - max);
            sum += utilities[i];
        }

        for (int i = 0; i < j; i++) utilities[i] /= sum;
        return utilities;
    }

    /// <summary>Логарифм правдоподобия набора заданий при заданных полезностях.</summary>
    internal static double LogLikelihood(double[][][] encoded, IReadOnlyList<ChoiceTask> tasks, double[] beta)
    {
        double ll = 0;
        for (int t = 0; t < tasks.Count; t++)
        {
            double[] p = Probabilities(encoded[t], beta);
            ll += Math.Log(Math.Max(p[tasks[t].ChosenIndex], 1e-300));
        }
        return ll;
    }

    /// <summary>Собирает результат: полезности, важности, качество посадки.</summary>
    internal static ConjointResult BuildResult(
        IReadOnlyList<ChoiceTask> tasks, ConjointDesign design, double[][][] encoded,
        double[] beta, double[] standardErrors, bool isHierarchical, IReadOnlyList<double> heterogeneity)
    {
        int k = design.ParameterCount;
        int priceColumn = design.PriceColumn;
        double priceCoefficient = priceColumn >= 0 ? beta[priceColumn] : double.NaN;

        var partWorths = new List<PartWorth>(k);
        for (int a = 0; a < k; a++)
        {
            double se = standardErrors.Length > a ? standardErrors[a] : double.NaN;
            double z = se > 0 ? beta[a] / se : double.NaN;

            partWorths.Add(new PartWorth
            {
                Name = design.ParameterNames[a],
                Utility = beta[a],
                StandardError = se,
                PValue = double.IsNaN(z) ? double.NaN : 2.0 * (1.0 - EconMath.NormalCdf(Math.Abs(z))),
                WillingnessToPay = priceColumn >= 0 && a != priceColumn && Math.Abs(priceCoefficient) > 1e-12
                    ? -beta[a] / priceCoefficient
                    : double.NaN,
            });
        }

        // Важность атрибута — размах его полезностей относительно суммы размахов
        var ranges = new Dictionary<string, double>();
        int column = 0;
        foreach (ConjointAttribute attribute in design.Attributes)
        {
            if (attribute.ColumnCount == 0) continue;

            double range;
            if (attribute.IsNumeric)
            {
                double low = attribute.NumericValues!.Min();
                double high = attribute.NumericValues!.Max();
                range = Math.Abs(beta[column] * (high - low));
            }
            else
            {
                double min = 0, max = 0;
                for (int level = 0; level < attribute.ColumnCount; level++)
                {
                    double u = beta[column + level];
                    if (u < min) min = u;
                    if (u > max) max = u;
                }
                range = max - min;
            }

            ranges[attribute.Name] = range;
            column += attribute.ColumnCount;
        }

        double total = ranges.Values.Sum();
        var importance = ranges.ToDictionary(
            kv => kv.Key, kv => total > 0 ? kv.Value / total : 0);

        int hits = 0;
        for (int t = 0; t < tasks.Count; t++)
        {
            double[] p = Probabilities(encoded[t], beta);
            int best = 0;
            for (int j = 1; j < p.Length; j++) if (p[j] > p[best]) best = j;
            if (best == tasks[t].ChosenIndex) hits++;
        }

        double nullLl = tasks.Sum(t => Math.Log(1.0 / Math.Max(t.Alternatives.Count, 1)));

        return new ConjointResult
        {
            PartWorths = partWorths,
            AttributeImportance = importance,
            LogLikelihood = LogLikelihood(encoded, tasks, beta),
            NullLogLikelihood = nullLl,
            HitRate = tasks.Count > 0 ? (double)hits / tasks.Count : 0,
            Tasks = tasks.Count,
            Respondents = tasks.Select(t => t.Respondent).Distinct().Count(),
            PriceCoefficient = priceCoefficient,
            IsHierarchical = isHierarchical,
            HeterogeneityStdDev = heterogeneity,
        };
    }
}
