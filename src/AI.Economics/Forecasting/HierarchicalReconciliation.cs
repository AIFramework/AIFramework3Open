using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Forecasting;

/// <summary>Способ согласования иерархических прогнозов.</summary>
public enum ReconciliationMethod
{
    /// <summary>Снизу вверх: агрегаты пересчитываются из нижнего уровня.</summary>
    BottomUp,

    /// <summary>Сверху вниз: верхний прогноз разносится по историческим долям.</summary>
    TopDown,

    /// <summary>Метод наименьших квадратов без учёта различий дисперсий уровней.</summary>
    OrdinaryLeastSquares,

    /// <summary>
    /// MinT с диагональной оценкой ковариации: уровни взвешиваются обратно
    /// дисперсии их ошибок.
    /// </summary>
    MinTraceDiagonal,
}

/// <summary>Узел иерархии прогнозов.</summary>
/// <param name="Name">Название узла.</param>
/// <param name="Level">Уровень: 0 — вершина иерархии.</param>
/// <param name="IsBottom">Является ли узел листом.</param>
public sealed record HierarchyNode(string Name, int Level, bool IsBottom);

/// <summary>Результат согласования прогнозов по иерархии.</summary>
public sealed record ReconciliationResult : IInterpretable
{
    /// <summary>Использованный метод.</summary>
    public ReconciliationMethod Method { get; init; }

    /// <summary>Узлы иерархии в порядке строк матрицы.</summary>
    public IReadOnlyList<HierarchyNode> Nodes { get; init; } = [];

    /// <summary>Исходные независимые прогнозы по узлам.</summary>
    public Matrix BaseForecasts { get; init; } = new Matrix(0, 0);

    /// <summary>Согласованные прогнозы той же формы.</summary>
    public Matrix ReconciledForecasts { get; init; } = new Matrix(0, 0);

    /// <summary>Максимальное расхождение суммы частей с целым до согласования.</summary>
    public double MaxIncoherenceBefore { get; init; }

    /// <summary>Максимальное расхождение после согласования.</summary>
    public double MaxIncoherenceAfter { get; init; }

    /// <summary>Средний относительный сдвиг прогнозов при согласовании.</summary>
    public double AverageAdjustment { get; init; }

    /// <summary>Наибольший относительный сдвиг и узел, где он произошёл.</summary>
    public string LargestAdjustmentNode { get; init; } = string.Empty;

    /// <summary>Величина наибольшего относительного сдвига.</summary>
    public double LargestAdjustment { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        string methodName = Method switch
        {
            ReconciliationMethod.BottomUp => "снизу вверх",
            ReconciliationMethod.TopDown => "сверху вниз",
            ReconciliationMethod.OrdinaryLeastSquares => "МНК-согласование",
            _ => "MinT с диагональной ковариацией",
        };

        return new InterpretationBuilder("Согласование иерархических прогнозов")
            .Summary($"Метод: {methodName}. До согласования сумма частей расходилась с целым " +
                     $"на {Fmt.Num(MaxIncoherenceBefore)}, после согласования расхождение " +
                     $"{Fmt.Num(MaxIncoherenceAfter, 6)}. Средний сдвиг прогнозов — " +
                     $"{Fmt.Pct(AverageAdjustment)}, наибольший у узла «{LargestAdjustmentNode}» " +
                     $"({Fmt.Pct(LargestAdjustment)}).")
            .Metric("Расхождение до", MaxIncoherenceBefore, null,
                "на сколько сумма нижнего уровня не сходилась с верхним",
                MaxIncoherenceBefore > 0 ? MetricQuality.Critical : MetricQuality.Good)
            .Metric("Расхождение после", MaxIncoherenceAfter, null,
                "должно быть машинным нулём", MetricQuality.Good, 6)
            .Metric("Средний сдвиг", Fmt.Pct(AverageAdjustment), null,
                "насколько согласование изменило исходные прогнозы",
                AverageAdjustment > 0.15 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Наибольший сдвиг", Fmt.Pct(LargestAdjustment), null,
                $"узел «{LargestAdjustmentNode}»",
                LargestAdjustment > 0.3 ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Узлов", Nodes.Count, null,
                $"{Nodes.Count(nd => nd.IsBottom)} на нижнем уровне", MetricQuality.Unknown, 0)
            .Finding("Независимо построенные прогнозы разных уровней никогда не сходятся: " +
                     "сумма прогнозов по товарам не равна прогнозу по категории. " +
                     "Планирование по несогласованным числам приводит к тому, что закупка " +
                     "и финансовый план живут по разным цифрам.")
            .FindingIf(Method == ReconciliationMethod.MinTraceDiagonal,
                "MinT взвешивает уровни обратно дисперсии их ошибок, поэтому более надёжные " +
                "уровни меньше сдвигаются. На практике он точнее и «снизу вверх», и «сверху вниз».")
            .FindingIf(Method == ReconciliationMethod.BottomUp,
                "Согласование снизу вверх сохраняет прогнозы нижнего уровня без изменений. " +
                "Это надёжно, но игнорирует то, что агрегаты обычно прогнозируются точнее: " +
                "на них меньше шума.")
            .FindingIf(Method == ReconciliationMethod.TopDown,
                "Согласование сверху вниз разносит агрегат по историческим долям и потому " +
                "не умеет реагировать на изменение структуры спроса внутри категории.")
            .WarningIf(LargestAdjustment > 0.3,
                $"Прогноз узла «{LargestAdjustmentNode}» сдвинулся на {Fmt.Pct(LargestAdjustment)}. " +
                "Такой сдвиг означает, что исходные прогнозы уровней сильно противоречили друг другу — " +
                "проверьте, не строились ли они на разных данных.")
            .Warning("Согласование не улучшает прогноз само по себе, если исходные прогнозы " +
                     "систематически смещены: оно распределяет смещение, а не убирает его.")
            .Recommendation("Стройте базовые прогнозы на каждом уровне независимо и лучшим " +
                            "доступным методом — согласование работает тем лучше, чем точнее вход.")
            .Build();
    }
}

/// <summary>
/// Согласование прогнозов по иерархии: товар — категория — регион — компания.
/// </summary>
/// <remarks>
/// <para>
/// Задача возникает всегда, когда прогнозы нужны на нескольких уровнях
/// сразу. Закупка планирует по товарам, коммерческий директор — по
/// категориям, финансовый план — по компании. Построенные независимо,
/// эти прогнозы не сходятся, и организация начинает жить по трём разным
/// цифрам.
/// </para>
/// <para>
/// Формально: пусть <c>S</c> — матрица суммирования, отображающая нижний
/// уровень во все узлы иерархии. Согласованный прогноз имеет вид
/// <c>S G y</c>, где <c>G</c> зависит от метода. Для «снизу вверх»
/// <c>G</c> просто выбирает нижний уровень, для МНК
/// <c>G = (S'S)^-1 S'</c>, для MinT <c>G = (S'W^-1 S)^-1 S'W^-1</c>
/// с оценкой <c>W</c> по дисперсиям ошибок узлов.
/// </para>
/// </remarks>
public static class HierarchicalReconciliation
{
    /// <summary>Согласовывает прогнозы по заданной структуре иерархии.</summary>
    /// <param name="nodes">Узлы в порядке строк матриц.</param>
    /// <param name="summing">
    /// Матрица суммирования: строки — все узлы, столбцы — нижний уровень.
    /// Элемент равен единице, если лист входит в узел.
    /// </param>
    /// <param name="baseForecasts">Прогнозы: строки — узлы, столбцы — горизонт.</param>
    /// <param name="method">Способ согласования.</param>
    /// <param name="errorVariances">
    /// Дисперсии ошибок узлов для MinT; при <c>null</c> берутся единичные.
    /// </param>
    /// <returns>Согласованные прогнозы и диагностика сдвигов.</returns>
    /// <exception cref="ArgumentNullException">Аргументы не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности не согласованы.</exception>
    public static ReconciliationResult Reconcile(
        IReadOnlyList<HierarchyNode> nodes,
        Matrix summing,
        Matrix baseForecasts,
        ReconciliationMethod method = ReconciliationMethod.MinTraceDiagonal,
        Vector? errorVariances = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(summing);
        ArgumentNullException.ThrowIfNull(baseForecasts);

        int total = summing.Height;
        int bottom = summing.Width;
        int horizon = baseForecasts.Width;

        if (baseForecasts.Height != total)
            throw new ArgumentException("Число строк прогнозов должно совпадать с числом узлов.",
                nameof(baseForecasts));
        if (nodes.Count != total)
            throw new ArgumentException("Число узлов должно совпадать с числом строк матрицы.", nameof(nodes));

        double[,] g = BuildG(summing, method, errorVariances, total, bottom, baseForecasts);

        var reconciled = new Matrix(total, horizon);
        double sumAdjustment = 0;
        int counted = 0;
        double largest = 0;
        string largestNode = nodes.Count > 0 ? nodes[0].Name : string.Empty;

        for (int h = 0; h < horizon; h++)
        {
            var bottomValues = new double[bottom];
            for (int b = 0; b < bottom; b++)
            {
                double sum = 0;
                for (int i = 0; i < total; i++) sum += g[b, i] * baseForecasts[i, h];
                bottomValues[b] = sum;
            }

            for (int i = 0; i < total; i++)
            {
                double sum = 0;
                for (int b = 0; b < bottom; b++) sum += summing[i, b] * bottomValues[b];
                reconciled[i, h] = sum;

                double original = baseForecasts[i, h];
                if (Math.Abs(original) > 1e-9)
                {
                    double adjustment = Math.Abs((sum - original) / original);
                    sumAdjustment += adjustment;
                    counted++;
                    if (adjustment > largest)
                    {
                        largest = adjustment;
                        largestNode = nodes[i].Name;
                    }
                }
            }
        }

        return new ReconciliationResult
        {
            Method = method,
            Nodes = nodes,
            BaseForecasts = baseForecasts,
            ReconciledForecasts = reconciled,
            MaxIncoherenceBefore = Incoherence(summing, baseForecasts, nodes),
            MaxIncoherenceAfter = Incoherence(summing, reconciled, nodes),
            AverageAdjustment = counted > 0 ? sumAdjustment / counted : 0,
            LargestAdjustment = largest,
            LargestAdjustmentNode = largestNode,
        };
    }

    /// <summary>
    /// Строит матрицу суммирования для двухуровневой иерархии
    /// «итого — группы — листья».
    /// </summary>
    /// <param name="groupSizes">Число листьев в каждой группе.</param>
    /// <returns>Матрица суммирования и список узлов.</returns>
    /// <exception cref="ArgumentNullException">Размеры групп не заданы.</exception>
    public static (Matrix Summing, IReadOnlyList<HierarchyNode> Nodes) BuildTwoLevel(
        IReadOnlyList<int> groupSizes)
    {
        ArgumentNullException.ThrowIfNull(groupSizes);

        int bottom = groupSizes.Sum();
        int total = 1 + groupSizes.Count + bottom;

        var summing = new Matrix(total, bottom);
        var nodes = new List<HierarchyNode>(total) { new("Итого", 0, false) };

        for (int b = 0; b < bottom; b++) summing[0, b] = 1;

        int row = 1;
        int offset = 0;
        for (int g = 0; g < groupSizes.Count; g++)
        {
            nodes.Add(new HierarchyNode($"Группа {g + 1}", 1, false));
            for (int b = 0; b < groupSizes[g]; b++) summing[row, offset + b] = 1;
            offset += groupSizes[g];
            row++;
        }

        offset = 0;
        for (int g = 0; g < groupSizes.Count; g++)
        {
            for (int b = 0; b < groupSizes[g]; b++)
            {
                nodes.Add(new HierarchyNode($"Г{g + 1}-{b + 1}", 2, true));
                summing[row, offset + b] = 1;
                row++;
            }
            offset += groupSizes[g];
        }

        return (summing, nodes);
    }

    private static double[,] BuildG(
        Matrix summing, ReconciliationMethod method, Vector? errorVariances,
        int total, int bottom, Matrix baseForecasts)
    {
        var g = new double[bottom, total];

        if (method == ReconciliationMethod.BottomUp)
        {
            // Листья идентифицируются по строкам с единственной единицей
            for (int b = 0; b < bottom; b++)
            {
                for (int i = 0; i < total; i++)
                {
                    double rowSum = 0;
                    for (int j = 0; j < bottom; j++) rowSum += summing[i, j];
                    if (rowSum == 1 && summing[i, b] == 1) { g[b, i] = 1; break; }
                }
            }
            return g;
        }

        if (method == ReconciliationMethod.TopDown)
        {
            // Доли берутся из самих базовых прогнозов нижнего уровня
            var proportions = new double[bottom];
            double sum = 0;

            for (int b = 0; b < bottom; b++)
            {
                for (int i = 0; i < total; i++)
                {
                    double rowSum = 0;
                    for (int j = 0; j < bottom; j++) rowSum += summing[i, j];
                    if (rowSum == 1 && summing[i, b] == 1)
                    {
                        double value = 0;
                        for (int h = 0; h < baseForecasts.Width; h++) value += baseForecasts[i, h];
                        proportions[b] = Math.Max(value, 0);
                        sum += proportions[b];
                        break;
                    }
                }
            }

            for (int b = 0; b < bottom; b++)
                g[b, 0] = sum > 1e-9 ? proportions[b] / sum : 1.0 / bottom;

            return g;
        }

        var weights = new double[total];
        for (int i = 0; i < total; i++)
        {
            weights[i] = method == ReconciliationMethod.MinTraceDiagonal && errorVariances is not null
                ? Math.Max(errorVariances[i], 1e-9)
                : 1.0;
        }

        // G = (S' W^-1 S)^-1 S' W^-1
        var sws = new double[bottom, bottom];
        for (int a = 0; a < bottom; a++)
            for (int b = 0; b < bottom; b++)
                for (int i = 0; i < total; i++)
                    sws[a, b] += summing[i, a] * summing[i, b] / weights[i];

        double[,]? inverse = EconMath.Inverse(sws);
        if (inverse is null) return BuildG(summing, ReconciliationMethod.BottomUp, null, total, bottom, baseForecasts);

        for (int b = 0; b < bottom; b++)
            for (int i = 0; i < total; i++)
            {
                double value = 0;
                for (int a = 0; a < bottom; a++) value += inverse[b, a] * summing[i, a] / weights[i];
                g[b, i] = value;
            }

        return g;
    }

    /// <summary>Максимальное нарушение равенства «сумма частей равна целому».</summary>
    private static double Incoherence(Matrix summing, Matrix forecasts, IReadOnlyList<HierarchyNode> nodes)
    {
        int total = summing.Height;
        int bottom = summing.Width;
        double worst = 0;

        var bottomRows = new List<int>();
        for (int i = 0; i < total; i++)
        {
            double rowSum = 0;
            for (int j = 0; j < bottom; j++) rowSum += summing[i, j];
            if (rowSum == 1) bottomRows.Add(i);
        }

        for (int h = 0; h < forecasts.Width; h++)
        {
            for (int i = 0; i < total; i++)
            {
                if (nodes[i].IsBottom) continue;

                double aggregated = 0;
                foreach (int leaf in bottomRows)
                {
                    for (int b = 0; b < bottom; b++)
                        if (summing[leaf, b] == 1 && summing[i, b] == 1) { aggregated += forecasts[leaf, h]; break; }
                }

                worst = Math.Max(worst, Math.Abs(aggregated - forecasts[i, h]));
            }
        }

        return worst;
    }
}
