using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Econometrics;

/// <summary>Наблюдение для разрывного дизайна.</summary>
/// <param name="Running">Значение переменной назначения.</param>
/// <param name="Outcome">Отклик.</param>
public sealed record RddObservation(double Running, double Outcome);

/// <summary>Результат оценивания разрывного дизайна.</summary>
public sealed record RddResult : IInterpretable
{
    /// <summary>Порог назначения.</summary>
    public double Cutoff { get; init; }

    /// <summary>Оценка скачка отклика на пороге.</summary>
    public double Effect { get; init; }

    /// <summary>Стандартная ошибка оценки.</summary>
    public double StandardError { get; init; }

    /// <summary>Использованная полоса пропускания.</summary>
    public double Bandwidth { get; init; }

    /// <summary>Предел отклика слева от порога.</summary>
    public double LeftLimit { get; init; }

    /// <summary>Предел отклика справа от порога.</summary>
    public double RightLimit { get; init; }

    /// <summary>Наблюдений слева от порога внутри полосы.</summary>
    public int LeftObservations { get; init; }

    /// <summary>Наблюдений справа от порога внутри полосы.</summary>
    public int RightObservations { get; init; }

    /// <summary>Оценки при других полосах пропускания.</summary>
    public IReadOnlyList<(double Bandwidth, double Effect, double StandardError)> Sensitivity { get; init; } = [];

    /// <summary>Оценки на ложных порогах: должны быть незначимы.</summary>
    public IReadOnlyList<(double Cutoff, double Effect, double StandardError)> Placebo { get; init; } = [];

    /// <summary>Статистика проверки непрерывности плотности переменной назначения.</summary>
    public double DensityStatistic { get; init; }

    /// <summary>Уровень значимости проверки плотности.</summary>
    public double DensityPValue { get; init; } = 1;

    /// <summary>Уровень значимости оценки эффекта.</summary>
    public double PValue =>
        StandardError > 0 ? Distributions.NormalPValue(Effect / StandardError) : 1;

    /// <summary>Нижняя граница 95-процентного интервала.</summary>
    public double ConfidenceLow => Effect - (1.96 * StandardError);

    /// <summary>Верхняя граница 95-процентного интервала.</summary>
    public double ConfidenceHigh => Effect + (1.96 * StandardError);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool significant = PValue < 0.05;
        bool densityJump = DensityPValue < 0.05;

        var placeboSignificant = Placebo
            .Where(p => p.StandardError > 0 && Math.Abs(p.Effect / p.StandardError) > 1.96)
            .ToList();

        double sensitivitySpread = Sensitivity.Count > 0
            ? Sensitivity.Max(s => s.Effect) - Sensitivity.Min(s => s.Effect)
            : 0;

        return new InterpretationBuilder("Разрывный дизайн")
            .Summary($"Скачок отклика на пороге {Fmt.Num(Cutoff, 3)} равен {Fmt.Num(Effect, 4)} " +
                     $"(ст. ошибка {Fmt.Num(StandardError, 4)}, p = {Fmt.Num(PValue, 4)}). " +
                     $"Полоса пропускания {Fmt.Num(Bandwidth, 3)}, в неё попало " +
                     $"{LeftObservations} наблюдений слева и {RightObservations} справа. " +
                     $"Проверка плотности: p = {Fmt.Num(DensityPValue, 4)}.")
            .Metric("Эффект", Effect, null,
                $"интервал [{Fmt.Num(ConfidenceLow, 4)}; {Fmt.Num(ConfidenceHigh, 4)}]",
                significant ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("Предел слева", LeftLimit, null, "экстраполяция локальной регрессии к порогу",
                MetricQuality.Neutral, 4)
            .Metric("Предел справа", RightLimit, null, "экстраполяция локальной регрессии к порогу",
                MetricQuality.Neutral, 4)
            .Metric("Полоса пропускания", Bandwidth, null,
                $"наблюдений слева {LeftObservations}, справа {RightObservations}",
                Math.Min(LeftObservations, RightObservations) >= 30
                    ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Чувствительность к полосе", sensitivitySpread, null,
                "размах оценки при изменении полосы вдвое",
                Math.Abs(sensitivitySpread) < Math.Abs(Effect) * 0.5
                    ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Непрерывность плотности", DensityStatistic, null,
                $"p = {Fmt.Num(DensityPValue, 4)}; проверка манипуляции переменной назначения",
                densityJump ? MetricQuality.Critical : MetricQuality.Good, 3)
            .Metric("Ложных порогов значимо", placeboSignificant.Count, null,
                $"из {Placebo.Count} проверенных",
                placeboSignificant.Count == 0 ? MetricQuality.Good : MetricQuality.Warning, 0)
            .Finding("Дизайн опирается на то, что вблизи порога попадание в группу воздействия " +
                     "фактически случайно. Поэтому оценка относится только к объектам около " +
                     "порога и не переносится на всю выборку.")
            .FindingIf(significant,
                $"Скачок статистически значим: {Fmt.Num(Effect, 4)} при p = {Fmt.Num(PValue, 4)}. " +
                $"Отклик меняется с {Fmt.Num(LeftLimit, 3)} до {Fmt.Num(RightLimit, 3)} " +
                "при переходе через порог.")
            .FindingIf(placeboSignificant.Count == 0 && Placebo.Count > 0,
                $"Ни на одном из {Placebo.Count} ложных порогов скачка не обнаружено — " +
                "разрыв специфичен для настоящего порога.")
            .WarningIf(densityJump,
                "Плотность переменной назначения разрывна на пороге. Это признак " +
                "манипуляции: объекты подстраивают своё положение относительно правила, " +
                "и случайность попадания в группу нарушается.")
            .WarningIf(placeboSignificant.Count > 0,
                $"На {placeboSignificant.Count} ложных порогах эффект тоже значим. " +
                "Скорее всего форма зависимости нелинейна, и локальная линейная " +
                "аппроксимация принимает изгиб за скачок.")
            .WarningIf(Math.Min(LeftObservations, RightObservations) < 30,
                $"В полосе всего {Math.Min(LeftObservations, RightObservations)} наблюдений " +
                "с одной из сторон. Оценка неустойчива, доверительный интервал занижен.")
            .Warning("Полоса пропускания подобрана эмпирическим правилом. Она балансирует " +
                     "смещение и дисперсию, но не оптимальна: результат обязан быть " +
                     "устойчивым к её изменению вдвое в обе стороны.")
            .Recommendation("Приводите график: точки, усреднённые по узким интервалам " +
                            "переменной назначения, и две подогнанные линии. Читатель " +
                            "должен видеть разрыв, а не только его оценку.")
            .Recommendation("Проверяйте плотность переменной назначения и эффекты на ложных " +
                            "порогах — без этих двух проверок дизайн не считается обоснованным.")
            .Build();
    }
}

/// <summary>
/// Разрывный дизайн: оценка эффекта по скачку отклика на пороге правила
/// назначения.
/// </summary>
/// <remarks>
/// <para>
/// Когда попадание в программу определяется формальным порогом — балл выше
/// проходного, выручка выше лимита, возраст старше границы, — объекты чуть выше
/// и чуть ниже порога почти одинаковы во всём, кроме факта участия. Скачок
/// отклика на пороге и есть оценка эффекта:
/// </para>
/// <code>
/// tau = lim_{x-&gt;c+} E[Y | X = x] - lim_{x-&gt;c-} E[Y | X = x]
/// </code>
/// <para>
/// Пределы оцениваются локальной линейной регрессией с треугольным ядром:
/// наблюдения ближе к порогу получают больший вес. Ядро и линейная (а не
/// постоянная) аппроксимация вместе снижают смещение на границе.
/// </para>
/// <para>
/// Дизайн держится на двух проверяемых следствиях. Плотность переменной
/// назначения должна быть непрерывна на пороге — разрыв означает манипуляцию.
/// Эффект на ложных порогах должен отсутствовать — иначе разрывом объявляется
/// обычный изгиб зависимости.
/// </para>
/// </remarks>
public static class RegressionDiscontinuity
{
    /// <summary>Оценивает эффект в резком разрывном дизайне.</summary>
    /// <param name="observations">Наблюдения с переменной назначения и откликом.</param>
    /// <param name="cutoff">Порог назначения.</param>
    /// <param name="bandwidth">Полоса пропускания; при нуле подбирается эмпирическим правилом.</param>
    /// <returns>Оценка скачка, проверки устойчивости и плотности.</returns>
    /// <exception cref="ArgumentNullException">Наблюдения не заданы.</exception>
    /// <exception cref="ArgumentException">Недостаточно наблюдений по одну из сторон порога.</exception>
    public static RddResult Estimate(
        IReadOnlyList<RddObservation> observations, double cutoff = 0, double bandwidth = 0)
    {
        ArgumentNullException.ThrowIfNull(observations);

        if (observations.Count < 20)
            throw new ArgumentException("Наблюдений недостаточно для локальной регрессии.", nameof(observations));

        double h = bandwidth > 0 ? bandwidth : RuleOfThumbBandwidth(observations, cutoff);

        (double effect, double error, double left, double right, int leftCount, int rightCount) =
            LocalLinear(observations, cutoff, h);

        if (leftCount < 5 || rightCount < 5)
            throw new ArgumentException(
                "По одну из сторон порога слишком мало наблюдений.", nameof(observations));

        var sensitivity = new List<(double, double, double)>();
        foreach (double factor in new[] { 0.5, 0.75, 1.5, 2.0 })
        {
            (double e, double se, _, _, int l, int r) = LocalLinear(observations, cutoff, h * factor);
            if (l >= 5 && r >= 5) sensitivity.Add((h * factor, e, se));
        }

        var placebo = new List<(double, double, double)>();
        double[] sortedRunning = [.. observations.Select(o => o.Running).OrderBy(v => v)];

        foreach (double quantile in new[] { 0.25, 0.75 })
        {
            double fake = EconMath.Quantile(sortedRunning, quantile);
            if (Math.Abs(fake - cutoff) < h) continue;

            var side = observations
                .Where(o => (fake < cutoff && o.Running < cutoff) || (fake > cutoff && o.Running > cutoff))
                .ToList();

            if (side.Count < 20) continue;

            (double e, double se, _, _, int l, int r) = LocalLinear(side, fake, h);
            if (l >= 5 && r >= 5) placebo.Add((fake, e, se));
        }

        (double densityStatistic, double densityP) = DensityContinuity(observations, cutoff, h);

        return new RddResult
        {
            Cutoff = cutoff,
            Effect = effect,
            StandardError = error,
            Bandwidth = h,
            LeftLimit = left,
            RightLimit = right,
            LeftObservations = leftCount,
            RightObservations = rightCount,
            Sensitivity = sensitivity,
            Placebo = placebo,
            DensityStatistic = densityStatistic,
            DensityPValue = densityP,
        };
    }

    /// <summary>Локальная линейная регрессия по обе стороны порога.</summary>
    private static (double Effect, double Error, double Left, double Right, int LeftCount, int RightCount)
        LocalLinear(IReadOnlyList<RddObservation> observations, double cutoff, double bandwidth)
    {
        var inside = observations.Where(o => Math.Abs(o.Running - cutoff) <= bandwidth).ToList();

        int leftCount = inside.Count(o => o.Running < cutoff);
        int rightCount = inside.Count - leftCount;

        if (leftCount < 3 || rightCount < 3) return (0, 0, 0, 0, leftCount, rightCount);

        int n = inside.Count;
        var design = new double[n, 4];
        var response = new double[n];
        var weights = new double[n];

        for (int i = 0; i < n; i++)
        {
            double distance = inside[i].Running - cutoff;
            double side = distance >= 0 ? 1 : 0;

            design[i, 0] = 1;
            design[i, 1] = side;
            design[i, 2] = distance;
            design[i, 3] = side * distance;

            response[i] = inside[i].Outcome;

            // Треугольное ядро: вес линейно убывает к краю полосы
            weights[i] = Math.Max(0, 1 - (Math.Abs(distance) / bandwidth));
        }

        var options = new RegressionOptions
        {
            AddIntercept = false,
            Variance = RobustVariance.Hc1,
            Weights = weights,
        };

        RegressionResult fit = LinearRegression.FitDesign(
            design, response, ["const", "скачок", "наклон", "изменение наклона"], options, "RDD");

        double intercept = fit.Coefficients[0].Estimate;
        double jump = fit.Coefficients[1].Estimate;

        return (jump, fit.Coefficients[1].StandardError, intercept, intercept + jump, leftCount, rightCount);
    }

    /// <summary>Эмпирическое правило выбора полосы пропускания.</summary>
    /// <remarks>
    /// Правило Сильвермана по разбросу переменной назначения, скорректированное
    /// на число наблюдений. Оно не оптимально по среднеквадратичной ошибке, но
    /// даёт разумную отправную точку, а устойчивость проверяется отдельно.
    /// </remarks>
    private static double RuleOfThumbBandwidth(IReadOnlyList<RddObservation> observations, double cutoff)
    {
        double[] distances = [.. observations.Select(o => Math.Abs(o.Running - cutoff)).OrderBy(v => v)];
        double median = EconMath.Quantile(distances, 0.5);
        double spread = EconMath.Quantile(distances, 0.75) - EconMath.Quantile(distances, 0.25);

        double scale = Math.Max(Math.Max(median, spread), 1e-9);
        return scale * Math.Pow(observations.Count, -0.2) * 2.5;
    }

    /// <summary>Проверка непрерывности плотности переменной назначения на пороге.</summary>
    private static (double Statistic, double PValue) DensityContinuity(
        IReadOnlyList<RddObservation> observations, double cutoff, double bandwidth)
    {
        int left = observations.Count(o => o.Running < cutoff && o.Running >= cutoff - bandwidth);
        int right = observations.Count(o => o.Running >= cutoff && o.Running <= cutoff + bandwidth);
        int total = left + right;

        if (total < 20) return (0, 1);

        // Биномиальный тест на равенство долей слева и справа от порога
        double expected = total / 2.0;
        double statistic = (right - expected) / Math.Sqrt(total * 0.25);

        return (statistic, Distributions.NormalPValue(statistic));
    }
}
