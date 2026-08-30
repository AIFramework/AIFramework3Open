using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;
using AI.Statistics;

namespace AI.Economics.Econometrics;

/// <summary>Группа объектов, упорядоченная по предсказанному эффекту.</summary>
/// <param name="Group">Номер группы от лучшей к худшей.</param>
/// <param name="PredictedEffect">Средний предсказанный эффект в группе.</param>
/// <param name="ActualEffect">Фактическая разность средних в группе.</param>
/// <param name="Size">Число объектов.</param>
public sealed record EffectGroup(int Group, double PredictedEffect, double ActualEffect, int Size);

/// <summary>Результат оценивания неоднородности эффекта причинным лесом.</summary>
public sealed record CausalForestResult : IInterpretable
{
    /// <summary>Предсказанный индивидуальный эффект по наблюдениям.</summary>
    public Vector Effects { get; init; } = new(0);

    /// <summary>Средний эффект по выборке.</summary>
    public double AverageEffect { get; init; }

    /// <summary>Разброс индивидуальных эффектов.</summary>
    public double EffectSpread { get; init; }

    /// <summary>Группы по величине предсказанного эффекта.</summary>
    public IReadOnlyList<EffectGroup> Groups { get; init; } = [];

    /// <summary>Важность признаков как доля разбиений по ним.</summary>
    public IReadOnlyList<(string Variable, double Importance)> Importance { get; init; } = [];

    /// <summary>Число деревьев.</summary>
    public int Trees { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Коэффициент калибровки: наклон регрессии фактического эффекта на предсказанный.</summary>
    public double CalibrationSlope { get; init; }

    /// <summary>Есть ли содержательная неоднородность эффекта.</summary>
    public bool HasHeterogeneity => Groups.Count > 1 &&
        Math.Abs(Groups[0].ActualEffect - Groups[^1].ActualEffect) > 1e-9;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        EffectGroup? best = Groups.FirstOrDefault();
        EffectGroup? worst = Groups.LastOrDefault();
        (string Variable, double Importance) leader = Importance.FirstOrDefault();

        double gain = best is not null && worst is not null
            ? best.ActualEffect - worst.ActualEffect
            : 0;

        bool calibrated = CalibrationSlope is > 0.5 and < 1.5;

        var builder = new InterpretationBuilder("Причинный лес: неоднородность эффекта")
            .Summary($"Обучено {Trees} деревьев на {Observations} наблюдениях. Средний эффект " +
                     $"{Fmt.Num(AverageEffect, 4)}, разброс индивидуальных оценок " +
                     $"{Fmt.Num(EffectSpread, 4)}. Разница между лучшей и худшей группой " +
                     $"{Fmt.Num(gain, 4)}. Наклон калибровки {Fmt.Num(CalibrationSlope, 2)}.")
            .Metric("Средний эффект", AverageEffect, null, "по всей выборке", MetricQuality.Neutral, 4)
            .Metric("Разброс эффектов", EffectSpread, null,
                "стандартное отклонение индивидуальных оценок",
                EffectSpread > Math.Abs(AverageEffect) * 0.3 ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Выигрыш от таргетирования", gain, null,
                "разница фактического эффекта между лучшей и худшей группой",
                Math.Abs(gain) > Math.Abs(AverageEffect) ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Наклон калибровки", CalibrationSlope, null,
                calibrated ? "предсказания эффекта откалиброваны" : "предсказания смещены по масштабу",
                calibrated ? MetricQuality.Good : MetricQuality.Warning, 3);

        foreach (EffectGroup group in Groups)
        {
            builder.Metric($"Группа {group.Group}", group.ActualEffect, null,
                $"предсказано {Fmt.Num(group.PredictedEffect, 4)}, объектов {group.Size}",
                MetricQuality.Unknown, 4);
        }

        foreach ((string variable, double importance) in Importance)
        {
            builder.Metric($"Важность: {variable}", importance, null,
                "доля разбиений по признаку", MetricQuality.Unknown, 3);
        }

        return builder
            .FindingIf(leader.Variable is not null,
                $"Сильнее всего эффект различается по признаку «{leader.Variable}»: " +
                $"на него приходится {Fmt.Pct(leader.Importance, 0)} разбиений. Это и есть " +
                "переменная, по которой стоит сегментировать воздействие.")
            .FindingIf(best is not null && worst is not null,
                $"В лучшей группе фактический эффект {Fmt.Num(best?.ActualEffect ?? 0, 4)}, " +
                $"в худшей {Fmt.Num(worst?.ActualEffect ?? 0, 4)}. Направляя воздействие " +
                "только на верхнюю группу, можно получить больший результат при меньшем охвате.")
            .FindingIf(calibrated,
                "Наклон калибровки близок к единице: порядок предсказанных эффектов " +
                "воспроизводится на данных, не участвовавших в построении разбиений.")
            .Finding("Лес обучается честно: разбиения строятся на одной половине подвыборки, " +
                     "а эффекты в листьях оцениваются на другой. Без этого разделения " +
                     "неоднородность находится даже там, где её нет.")
            .WarningIf(!calibrated,
                $"Наклон калибровки {Fmt.Num(CalibrationSlope, 2)} далёк от единицы. " +
                "Ранжирование объектов может быть полезным, но абсолютные значения " +
                "индивидуальных эффектов использовать нельзя.")
            .WarningIf(!HasHeterogeneity,
                "Различий между группами не обнаружено. Скорее всего эффект однороден, " +
                "и достаточно средней оценки — сегментация ничего не добавит.")
            .Warning("Метод оценивает неоднородность условно на наблюдаемых признаках " +
                     "и предполагает случайное назначение воздействия. На данных наблюдений " +
                     "он унаследует всё смещение отбора, которое было в исходной выборке.")
            .Recommendation("Проверяйте выигрыш от таргетирования на отложенной выборке: " +
                            "это единственная честная оценка практической пользы модели.")
            .Recommendation("Не интерпретируйте важность признаков как причинность: " +
                            "она показывает, где эффект различается, а не почему.")
            .Build();
    }
}

/// <summary>
/// Причинный лес: оценка индивидуального эффекта воздействия и его
/// неоднородности по признакам.
/// </summary>
/// <remarks>
/// <para>
/// Средний эффект отвечает на вопрос «работает ли воздействие в целом».
/// Практический вопрос обычно другой: на кого его направлять. Причинный лес
/// строит деревья, разбиения которых максимизируют различие эффекта между
/// ветвями, а не однородность отклика:
/// </para>
/// <code>
/// критерий разбиения = n_L * n_R / n * ( tau_L - tau_R )^2
/// tau_leaf = mean(y | D=1, leaf) - mean(y | D=0, leaf)
/// </code>
/// <para>
/// Ключевое требование — честность. Наблюдения подвыборки делятся пополам:
/// первая половина определяет структуру разбиений, вторая оценивает эффекты в
/// листьях. Без разделения дерево находит неоднородность даже в данных, где её
/// нет: оно подстраивает границы под шум и затем измеряет этот же шум.
/// </para>
/// <para>
/// Качество проверяется калибровкой: объекты сортируются по предсказанному
/// эффекту, делятся на группы, и в каждой считается фактическая разность
/// средних. Наклон регрессии фактического эффекта на предсказанный должен быть
/// близок к единице.
/// </para>
/// </remarks>
public static class CausalForest
{
    /// <summary>Обучает причинный лес и оценивает неоднородность эффекта.</summary>
    /// <param name="features">Матрица признаков.</param>
    /// <param name="treatment">Признак воздействия: единица или ноль.</param>
    /// <param name="outcome">Отклик.</param>
    /// <param name="names">Названия признаков.</param>
    /// <param name="trees">Число деревьев.</param>
    /// <param name="minLeaf">Минимальное число наблюдений каждой группы в листе.</param>
    /// <param name="maxDepth">Максимальная глубина дерева.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Индивидуальные эффекты, группы и важность признаков.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы или одна из групп пуста.</exception>
    public static CausalForestResult Fit(
        Matrix features, Vector treatment, Vector outcome,
        IReadOnlyList<string>? names = null,
        int trees = 200, int minLeaf = 10, int maxDepth = 4, int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(treatment);
        ArgumentNullException.ThrowIfNull(outcome);

        int n = treatment.Count;
        if (features.Height != n || outcome.Count != n)
            throw new ArgumentException("Размерности данных должны совпадать.", nameof(outcome));
        if (n < 4 * minLeaf)
            throw new ArgumentException("Наблюдений недостаточно для честного разбиения.", nameof(features));

        int treatedCount = Enumerable.Range(0, n).Count(i => treatment[i] > 0.5);
        if (treatedCount == 0 || treatedCount == n)
            throw new ArgumentException("Нужны обе группы: с воздействием и без.", nameof(treatment));

        Random rng = RandomEngine.Create(seed);
        int featureCount = features.Width;

        var totals = new double[n];
        var counts = new int[n];
        var splitCounts = new double[featureCount];

        for (int b = 0; b < trees; b++)
        {
            var indices = Enumerable.Range(0, n).OrderBy(_ => rng.Next()).ToList();
            int half = indices.Count / 2;

            var structure = indices.Take(half).ToList();
            var honest = indices.Skip(half).ToList();

            var node = new TreeNode(structure, honest);
            Grow(node, features, treatment, outcome, minLeaf, maxDepth, 0, rng, splitCounts);

            for (int i = 0; i < n; i++)
            {
                double? effect = Predict(node, features, i);
                if (effect is null) continue;

                totals[i] += effect.Value;
                counts[i]++;
            }
        }

        var effects = new Vector(n);
        for (int i = 0; i < n; i++) effects[i] = counts[i] > 0 ? totals[i] / counts[i] : 0;

        double average = effects.Average();
        double spread = Math.Sqrt(effects.Sum(e => (e - average) * (e - average)) / Math.Max(1, n - 1));

        IReadOnlyList<EffectGroup> groups = BuildGroups(effects, treatment, outcome, 5);

        double totalSplits = splitCounts.Sum();
        var importance = new List<(string, double)>(featureCount);

        for (int j = 0; j < featureCount; j++)
        {
            importance.Add((
                names is not null && j < names.Count ? names[j] : $"x{j + 1}",
                totalSplits > 0 ? splitCounts[j] / totalSplits : 0));
        }

        return new CausalForestResult
        {
            Effects = effects,
            AverageEffect = average,
            EffectSpread = spread,
            Groups = groups,
            Importance = [.. importance.OrderByDescending(i => i.Item2)],
            Trees = trees,
            Observations = n,
            CalibrationSlope = CalibrationSlope(groups),
        };
    }

    /// <summary>Узел причинного дерева.</summary>
    private sealed class TreeNode(List<int> structure, List<int> honest)
    {
        public List<int> Structure { get; } = structure;

        public List<int> Honest { get; } = honest;

        public int Feature { get; set; } = -1;

        public double Threshold { get; set; }

        public TreeNode? Left { get; set; }

        public TreeNode? Right { get; set; }

        public double? Effect { get; set; }
    }

    /// <summary>Рекурсивно выращивает причинное дерево.</summary>
    private static void Grow(
        TreeNode node, Matrix features, Vector treatment, Vector outcome,
        int minLeaf, int maxDepth, int depth, Random rng, double[] splitCounts)
    {
        node.Effect = LeafEffect(node.Honest, treatment, outcome, minLeaf);

        if (depth >= maxDepth || node.Structure.Count < 4 * minLeaf) return;

        int featureCount = features.Width;
        var candidates = Enumerable.Range(0, featureCount)
            .OrderBy(_ => rng.Next())
            .Take(Math.Max(1, (int)Math.Ceiling(Math.Sqrt(featureCount))))
            .ToList();

        double bestScore = 0;
        int bestFeature = -1;
        double bestThreshold = 0;

        foreach (int feature in candidates)
        {
            double[] values = [.. node.Structure.Select(i => features[i, feature]).Distinct().OrderBy(v => v)];
            if (values.Length < 4) continue;

            foreach (double quantile in new[] { 0.25, 0.5, 0.75 })
            {
                double threshold = EconMath.Quantile(values, quantile);

                var left = node.Structure.Where(i => features[i, feature] <= threshold).ToList();
                var right = node.Structure.Where(i => features[i, feature] > threshold).ToList();

                double? leftEffect = LeafEffect(left, treatment, outcome, minLeaf);
                double? rightEffect = LeafEffect(right, treatment, outcome, minLeaf);

                if (leftEffect is null || rightEffect is null) continue;

                double difference = leftEffect.Value - rightEffect.Value;
                double score = (double)left.Count * right.Count / node.Structure.Count * difference * difference;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestFeature = feature;
                    bestThreshold = threshold;
                }
            }
        }

        if (bestFeature < 0) return;

        var structureLeft = node.Structure.Where(i => features[i, bestFeature] <= bestThreshold).ToList();
        var structureRight = node.Structure.Where(i => features[i, bestFeature] > bestThreshold).ToList();
        var honestLeft = node.Honest.Where(i => features[i, bestFeature] <= bestThreshold).ToList();
        var honestRight = node.Honest.Where(i => features[i, bestFeature] > bestThreshold).ToList();

        if (LeafEffect(honestLeft, treatment, outcome, minLeaf) is null ||
            LeafEffect(honestRight, treatment, outcome, minLeaf) is null)
            return;

        node.Feature = bestFeature;
        node.Threshold = bestThreshold;
        splitCounts[bestFeature]++;

        node.Left = new TreeNode(structureLeft, honestLeft);
        node.Right = new TreeNode(structureRight, honestRight);

        Grow(node.Left, features, treatment, outcome, minLeaf, maxDepth, depth + 1, rng, splitCounts);
        Grow(node.Right, features, treatment, outcome, minLeaf, maxDepth, depth + 1, rng, splitCounts);
    }

    /// <summary>Эффект в листе по честной подвыборке.</summary>
    private static double? LeafEffect(
        IReadOnlyList<int> indices, Vector treatment, Vector outcome, int minLeaf)
    {
        double treatedSum = 0, controlSum = 0;
        int treated = 0, controls = 0;

        foreach (int i in indices)
        {
            if (treatment[i] > 0.5) { treatedSum += outcome[i]; treated++; }
            else { controlSum += outcome[i]; controls++; }
        }

        if (treated < minLeaf || controls < minLeaf) return null;

        return (treatedSum / treated) - (controlSum / controls);
    }

    /// <summary>Предсказание эффекта для наблюдения.</summary>
    private static double? Predict(TreeNode node, Matrix features, int index)
    {
        TreeNode current = node;

        while (current.Feature >= 0)
        {
            TreeNode? next = features[index, current.Feature] <= current.Threshold
                ? current.Left
                : current.Right;

            if (next?.Effect is null) break;
            current = next;
        }

        return current.Effect;
    }

    /// <summary>Разбивает выборку на группы по предсказанному эффекту.</summary>
    private static IReadOnlyList<EffectGroup> BuildGroups(
        Vector effects, Vector treatment, Vector outcome, int groups)
    {
        int n = effects.Count;
        var order = Enumerable.Range(0, n).OrderByDescending(i => effects[i]).ToList();
        var result = new List<EffectGroup>(groups);

        for (int g = 0; g < groups; g++)
        {
            int from = g * n / groups;
            int to = (g + 1) * n / groups;
            if (to <= from) continue;

            var slice = order.GetRange(from, to - from);

            var treated = slice.Where(i => treatment[i] > 0.5).ToList();
            var controls = slice.Where(i => treatment[i] <= 0.5).ToList();

            double actual = treated.Count > 0 && controls.Count > 0
                ? treated.Average(i => outcome[i]) - controls.Average(i => outcome[i])
                : 0;

            result.Add(new EffectGroup(g + 1, slice.Average(i => effects[i]), actual, slice.Count));
        }

        return result;
    }

    /// <summary>Наклон регрессии фактического эффекта на предсказанный.</summary>
    private static double CalibrationSlope(IReadOnlyList<EffectGroup> groups)
    {
        if (groups.Count < 2) return 0;

        double meanPredicted = groups.Average(g => g.PredictedEffect);
        double meanActual = groups.Average(g => g.ActualEffect);

        double covariance = 0, variance = 0;
        foreach (EffectGroup group in groups)
        {
            double deviation = group.PredictedEffect - meanPredicted;
            covariance += deviation * (group.ActualEffect - meanActual);
            variance += deviation * deviation;
        }

        return variance > 1e-18 ? covariance / variance : 0;
    }
}
