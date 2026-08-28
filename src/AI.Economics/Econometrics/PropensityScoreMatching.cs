using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Econometrics;

/// <summary>Баланс одной ковариаты до и после сопоставления.</summary>
/// <param name="Variable">Название ковариаты.</param>
/// <param name="BeforeTreated">Среднее в группе воздействия до сопоставления.</param>
/// <param name="BeforeControl">Среднее в контроле до сопоставления.</param>
/// <param name="AfterControl">Среднее в сопоставленном контроле.</param>
/// <param name="StandardizedBefore">Стандартизованная разность до сопоставления.</param>
/// <param name="StandardizedAfter">Стандартизованная разность после сопоставления.</param>
public sealed record BalanceCheck(
    string Variable, double BeforeTreated, double BeforeControl, double AfterControl,
    double StandardizedBefore, double StandardizedAfter)
{
    /// <summary>Сбалансирована ли ковариата после сопоставления.</summary>
    public bool IsBalanced => Math.Abs(StandardizedAfter) < 0.1;

    /// <summary>На сколько улучшился баланс.</summary>
    public double Improvement => Math.Abs(StandardizedBefore) - Math.Abs(StandardizedAfter);
}

/// <summary>Результат сопоставления по склонности к воздействию.</summary>
public sealed record MatchingResult : IInterpretable
{
    /// <summary>Средний эффект воздействия на подвергшихся ему.</summary>
    public double AverageTreatmentEffectOnTreated { get; init; }

    /// <summary>Стандартная ошибка оценки.</summary>
    public double StandardError { get; init; }

    /// <summary>Наивная разность средних без сопоставления.</summary>
    public double NaiveDifference { get; init; }

    /// <summary>Баланс ковариат до и после сопоставления.</summary>
    public IReadOnlyList<BalanceCheck> Balance { get; init; } = [];

    /// <summary>Число объектов под воздействием.</summary>
    public int Treated { get; init; }

    /// <summary>Число сопоставленных объектов под воздействием.</summary>
    public int Matched { get; init; }

    /// <summary>Число контрольных объектов.</summary>
    public int Controls { get; init; }

    /// <summary>Использованный радиус сопоставления в единицах логита склонности.</summary>
    public double Caliper { get; init; }

    /// <summary>Доля объектов под воздействием, попавших в область общей поддержки.</summary>
    public double CommonSupport => Treated > 0 ? (double)Matched / Treated : 0;

    /// <summary>Модель склонности.</summary>
    public LimitedDependentResult? PropensityModel { get; init; }

    /// <summary>Уровень значимости оценки эффекта.</summary>
    public double PValue =>
        StandardError > 0
            ? Distributions.NormalPValue(AverageTreatmentEffectOnTreated / StandardError)
            : 1;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var unbalanced = Balance.Where(b => !b.IsBalanced).ToList();
        BalanceCheck? worst = Balance.OrderByDescending(b => Math.Abs(b.StandardizedAfter)).FirstOrDefault();
        bool significant = PValue < 0.05;
        double selectionBias = NaiveDifference - AverageTreatmentEffectOnTreated;

        var builder = new InterpretationBuilder("Сопоставление по склонности")
            .Summary($"Эффект на подвергшихся воздействию {Fmt.Num(AverageTreatmentEffectOnTreated, 4)} " +
                     $"(ст. ошибка {Fmt.Num(StandardError, 4)}, p = {Fmt.Num(PValue, 4)}). " +
                     $"Наивная разность средних {Fmt.Num(NaiveDifference, 4)} — смещение отбора " +
                     $"{Fmt.Num(selectionBias, 4)}. Сопоставлено {Matched} из {Treated} объектов " +
                     $"({Fmt.Pct(CommonSupport, 0)} общей поддержки), несбалансированных " +
                     $"ковариат {unbalanced.Count} из {Balance.Count}.")
            .Metric("Эффект", AverageTreatmentEffectOnTreated, null,
                $"ст. ошибка {Fmt.Num(StandardError, 4)}, p = {Fmt.Num(PValue, 4)}",
                significant ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Наивная разность", NaiveDifference, null,
                "разность средних без учёта различий в характеристиках", MetricQuality.Neutral, 4)
            .Metric("Смещение отбора", selectionBias, null,
                "сколько наивной оценки объясняется различием групп",
                Math.Abs(selectionBias) > Math.Abs(AverageTreatmentEffectOnTreated)
                    ? MetricQuality.Warning : MetricQuality.Neutral, 4)
            .Metric("Общая поддержка", CommonSupport, null,
                $"{Matched} из {Treated} объектов нашли пару",
                CommonSupport > 0.9 ? MetricQuality.Good
                    : CommonSupport > 0.7 ? MetricQuality.Warning : MetricQuality.Critical, 3)
            .Metric("Несбалансированных ковариат", unbalanced.Count, null,
                $"из {Balance.Count}; порог стандартизованной разности 0,1",
                unbalanced.Count == 0 ? MetricQuality.Good : MetricQuality.Warning, 0);

        foreach (BalanceCheck check in Balance)
        {
            builder.Metric($"Баланс: {check.Variable}", check.StandardizedAfter, null,
                $"было {Fmt.Num(check.StandardizedBefore, 3)}, стало {Fmt.Num(check.StandardizedAfter, 3)}",
                check.IsBalanced ? MetricQuality.Good : MetricQuality.Warning, 3);
        }

        return builder
            .Finding("Сопоставление уравнивает группы по наблюдаемым характеристикам. " +
                     "Разность наивной оценки и оценки после сопоставления показывает, " +
                     "какую часть различия создавал отбор, а не воздействие.")
            .FindingIf(unbalanced.Count == 0,
                "Все ковариаты сбалансированы: стандартизованные разности внутри 0,1. " +
                "Это необходимое условие корректности сопоставления.")
            .FindingIf(worst is not null && !worst.IsBalanced,
                $"Хуже всего сбалансирована ковариата «{worst?.Variable}»: " +
                $"стандартизованная разность {Fmt.Num(worst?.StandardizedAfter ?? 0, 3)}. " +
                "Остаточный дисбаланс переносится прямо в оценку эффекта.")
            .WarningIf(CommonSupport < 0.9,
                $"Пару нашли только {Fmt.Pct(CommonSupport, 0)} объектов под воздействием. " +
                "Оценка относится к подвыборке, для которой нашлись похожие контрольные — " +
                "это уже не тот же эффект, что для всей группы.")
            .WarningIf(unbalanced.Count > 0,
                $"После сопоставления {unbalanced.Count} ковариат остались несбалансированными. " +
                "Уменьшите радиус сопоставления или добавьте взаимодействия в модель склонности.")
            .Warning("Метод устраняет смещение только по наблюдаемым характеристикам. " +
                     "Если отбор идёт по признаку, которого нет в данных, сопоставление " +
                     "не помогает — и никакая проверка баланса этого не покажет.")
            .Recommendation("Всегда приводите таблицу баланса. Без неё оценка эффекта " +
                            "по сопоставлению не поддаётся проверке.")
            .Recommendation("Сравните результат с оценкой по регрессии на тех же ковариатах: " +
                            "заметное расхождение указывает на нелинейность связи, " +
                            "которую регрессия не улавливает.")
            .Build();
    }
}

/// <summary>
/// Сопоставление по склонности к воздействию: оценка эффекта при неслучайном
/// отборе в программу.
/// </summary>
/// <remarks>
/// <para>
/// Когда участие в программе выбирают сами объекты, различие средних смешивает
/// эффект программы с различием участников и неучастников. Сопоставление
/// строит для каждого участника похожего неучастника по вероятности участия:
/// </para>
/// <code>
/// p(x) = Pr(D = 1 | X = x)
/// ATT = mean_{i: D=1} ( y_i - y_{m(i)} )
/// </code>
/// <para>
/// Ключевой результат Розенбаума и Рубина: если отбор определяется наблюдаемыми
/// характеристиками, достаточно уравнять объекты по одномерной склонности, а не
/// по всему вектору характеристик.
/// </para>
/// <para>
/// Сопоставление ведётся по логиту склонности с радиусом: пара считается
/// допустимой, только если расстояние меньше доли стандартного отклонения
/// логита. Качество проверяется балансом ковариат — стандартизованная разность
/// после сопоставления должна быть по модулю меньше 0,1.
/// </para>
/// </remarks>
public static class PropensityScoreMatching
{
    /// <summary>Оценивает эффект воздействия сопоставлением по склонности.</summary>
    /// <param name="covariates">Матрица характеристик объектов.</param>
    /// <param name="treatment">Признак воздействия: единица или ноль.</param>
    /// <param name="outcome">Отклик.</param>
    /// <param name="names">Названия ковариат.</param>
    /// <param name="caliperFactor">Радиус сопоставления в долях стандартного отклонения логита склонности.</param>
    /// <param name="neighbours">Число ближайших соседей для усреднения.</param>
    /// <returns>Оценка эффекта, баланс ковариат и модель склонности.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы или одна из групп пуста.</exception>
    public static MatchingResult Estimate(
        Matrix covariates, Vector treatment, Vector outcome,
        IReadOnlyList<string>? names = null, double caliperFactor = 0.2, int neighbours = 1)
    {
        ArgumentNullException.ThrowIfNull(covariates);
        ArgumentNullException.ThrowIfNull(treatment);
        ArgumentNullException.ThrowIfNull(outcome);

        int n = treatment.Count;
        if (covariates.Height != n || outcome.Count != n)
            throw new ArgumentException("Размерности данных должны совпадать.", nameof(outcome));

        var treatedIndices = Enumerable.Range(0, n).Where(i => treatment[i] > 0.5).ToList();
        var controlIndices = Enumerable.Range(0, n).Where(i => treatment[i] <= 0.5).ToList();

        if (treatedIndices.Count == 0 || controlIndices.Count == 0)
            throw new ArgumentException("Нужны обе группы: с воздействием и без.", nameof(treatment));

        LimitedDependentResult propensity = LimitedDependent.Fit(
            covariates, treatment, LimitedDependentModel.Logit, names);

        var logit = new double[n];
        for (int i = 0; i < n; i++)
        {
            double p = Math.Clamp(propensity.Fitted[i], 1e-6, 1 - 1e-6);
            logit[i] = Math.Log(p / (1 - p));
        }

        double logitMean = logit.Average();
        double logitSd = Math.Sqrt(logit.Sum(v => (v - logitMean) * (v - logitMean)) / Math.Max(1, n - 1));
        double caliper = caliperFactor * logitSd;

        var matchedTreated = new List<int>();
        var matchedControls = new List<List<int>>();
        int k = Math.Max(1, neighbours);

        foreach (int i in treatedIndices)
        {
            var candidates = controlIndices
                .Select(j => (Index: j, Distance: Math.Abs(logit[j] - logit[i])))
                .Where(c => c.Distance <= caliper)
                .OrderBy(c => c.Distance)
                .Take(k)
                .Select(c => c.Index)
                .ToList();

            if (candidates.Count == 0) continue;

            matchedTreated.Add(i);
            matchedControls.Add(candidates);
        }

        if (matchedTreated.Count == 0)
            throw new ArgumentException(
                "Ни один объект не нашёл пару: увеличьте радиус сопоставления.", nameof(caliperFactor));

        var differences = new List<double>(matchedTreated.Count);
        for (int m = 0; m < matchedTreated.Count; m++)
        {
            double control = matchedControls[m].Average(j => outcome[j]);
            differences.Add(outcome[matchedTreated[m]] - control);
        }

        double att = differences.Average();
        double variance = differences.Count > 1
            ? differences.Sum(d => (d - att) * (d - att)) / (differences.Count - 1) / differences.Count
            : 0;

        double naive = treatedIndices.Average(i => outcome[i]) - controlIndices.Average(i => outcome[i]);

        return new MatchingResult
        {
            AverageTreatmentEffectOnTreated = att,
            StandardError = Math.Sqrt(Math.Max(variance, 0)),
            NaiveDifference = naive,
            Balance = BalanceReport(covariates, treatedIndices, controlIndices, matchedTreated, matchedControls, names),
            Treated = treatedIndices.Count,
            Matched = matchedTreated.Count,
            Controls = controlIndices.Count,
            Caliper = caliper,
            PropensityModel = propensity,
        };
    }

    /// <summary>Баланс ковариат до и после сопоставления.</summary>
    private static IReadOnlyList<BalanceCheck> BalanceReport(
        Matrix covariates, IReadOnlyList<int> treated, IReadOnlyList<int> controls,
        IReadOnlyList<int> matchedTreated, IReadOnlyList<List<int>> matchedControls,
        IReadOnlyList<string>? names)
    {
        var report = new List<BalanceCheck>(covariates.Width);

        for (int j = 0; j < covariates.Width; j++)
        {
            double treatedMean = treated.Average(i => covariates[i, j]);
            double controlMean = controls.Average(i => covariates[i, j]);

            double treatedVariance = treated.Count > 1
                ? treated.Sum(i => Math.Pow(covariates[i, j] - treatedMean, 2)) / (treated.Count - 1)
                : 0;
            double controlVariance = controls.Count > 1
                ? controls.Sum(i => Math.Pow(covariates[i, j] - controlMean, 2)) / (controls.Count - 1)
                : 0;

            double pooled = Math.Sqrt(Math.Max((treatedVariance + controlVariance) / 2, 1e-18));

            double matchedTreatedMean = matchedTreated.Average(i => covariates[i, j]);
            double matchedControlMean = matchedControls.Average(list => list.Average(i => covariates[i, j]));

            report.Add(new BalanceCheck(
                names is not null && j < names.Count ? names[j] : $"x{j + 1}",
                treatedMean, controlMean, matchedControlMean,
                (treatedMean - controlMean) / pooled,
                (matchedTreatedMean - matchedControlMean) / pooled));
        }

        return report;
    }
}
