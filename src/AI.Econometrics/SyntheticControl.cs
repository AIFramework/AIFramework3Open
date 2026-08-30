using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Econometrics;

/// <summary>Вклад одного донора в синтетический контроль.</summary>
/// <param name="Donor">Название донора.</param>
/// <param name="Weight">Вес в синтетическом контроле.</param>
public sealed record DonorWeight(string Donor, double Weight);

/// <summary>Результат построения синтетического контроля.</summary>
public sealed record SyntheticControlResult : IInterpretable
{
    /// <summary>Название объекта под воздействием.</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>Веса доноров по убыванию вклада.</summary>
    public IReadOnlyList<DonorWeight> Weights { get; init; } = [];

    /// <summary>Фактический ряд объекта.</summary>
    public Vector Actual { get; init; } = new(0);

    /// <summary>Ряд синтетического контроля.</summary>
    public Vector Synthetic { get; init; } = new(0);

    /// <summary>Разность фактического и синтетического рядов.</summary>
    public Vector Gap { get; init; } = new(0);

    /// <summary>Число периодов до вмешательства.</summary>
    public int PreTreatmentPeriods { get; init; }

    /// <summary>Средний эффект после вмешательства.</summary>
    public double AverageEffect { get; init; }

    /// <summary>Корень средней квадратичной ошибки до вмешательства.</summary>
    public double PreTreatmentRmspe { get; init; }

    /// <summary>Корень средней квадратичной ошибки после вмешательства.</summary>
    public double PostTreatmentRmspe { get; init; }

    /// <summary>Отношение ошибок после и до вмешательства.</summary>
    public double RmspeRatio => PreTreatmentRmspe > 0 ? PostTreatmentRmspe / PreTreatmentRmspe : 0;

    /// <summary>Отношения ошибок для плацебо-доноров.</summary>
    public IReadOnlyList<(string Donor, double Ratio)> Placebo { get; init; } = [];

    /// <summary>Уровень значимости по ранговому плацебо-тесту.</summary>
    public double PValue { get; init; } = 1;

    /// <summary>Число доноров с ненулевым весом.</summary>
    public int ActiveDonors => Weights.Count(w => w.Weight > 1e-4);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        DonorWeight? leader = Weights.FirstOrDefault();
        bool goodFit = PreTreatmentRmspe > 0 && PreTreatmentRmspe < Math.Abs(AverageEffect);
        bool significant = PValue < 0.1;

        var builder = new InterpretationBuilder($"Синтетический контроль: {Unit}")
            .Summary($"Синтетический двойник построен из {ActiveDonors} доноров по " +
                     $"{PreTreatmentPeriods} периодам до вмешательства. Средний эффект " +
                     $"{Fmt.Num(AverageEffect, 4)}. Ошибка подгонки до вмешательства " +
                     $"{Fmt.Num(PreTreatmentRmspe, 4)}, после — {Fmt.Num(PostTreatmentRmspe, 4)}, " +
                     $"отношение {Fmt.Num(RmspeRatio, 2)}. Плацебо-тест: p = {Fmt.Num(PValue, 3)}.")
            .Metric("Средний эффект", AverageEffect, null,
                "разность фактического ряда и синтетического после вмешательства",
                MetricQuality.Neutral, 4)
            .Metric("Ошибка до вмешательства", PreTreatmentRmspe, null,
                "качество подгонки синтетического двойника",
                goodFit ? MetricQuality.Good : MetricQuality.Warning, 4)
            .Metric("Отношение ошибок", RmspeRatio, null,
                "во сколько раз расхождение после вмешательства больше, чем до",
                RmspeRatio > 3 ? MetricQuality.Good : MetricQuality.Neutral, 2)
            .Metric("p-значение", PValue, null,
                $"ранг среди {Placebo.Count} плацебо-доноров",
                significant ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("Активных доноров", ActiveDonors, null,
                $"из {Weights.Count} в пуле",
                ActiveDonors is >= 2 and <= 8 ? MetricQuality.Good : MetricQuality.Warning, 0);

        foreach (DonorWeight weight in Weights.Where(w => w.Weight > 1e-4))
            builder.Metric($"Вес: {weight.Donor}", weight.Weight, null, "доля в синтетическом контроле",
                MetricQuality.Unknown, 3);

        return builder
            .FindingIf(leader is not null,
                $"Наибольший вклад в синтетический контроль даёт «{leader?.Donor}» " +
                $"с весом {Fmt.Pct(leader?.Weight ?? 0, 1)}. Состав весов — содержательный " +
                "результат: он показывает, кем на самом деле был похож объект до вмешательства.")
            .Finding("Метод не требует параллельных трендов: он подбирает такую комбинацию " +
                     "доноров, которая воспроизводит траекторию объекта до вмешательства. " +
                     "Качество этой подгонки и есть проверяемая часть дизайна.")
            .FindingIf(RmspeRatio > 3,
                $"Расхождение после вмешательства в {Fmt.Num(RmspeRatio, 1)} раза превышает " +
                "уровень до него. Это основной признак реального эффекта в данном методе.")
            .WarningIf(!goodFit,
                $"Ошибка подгонки до вмешательства {Fmt.Num(PreTreatmentRmspe, 4)} сопоставима " +
                "с оценкой эффекта. Синтетический двойник плохо повторяет объект, " +
                "и разность после вмешательства нельзя приписывать вмешательству.")
            .WarningIf(ActiveDonors <= 1,
                "Практически весь вес пришёлся на одного донора. Такой синтетический " +
                "контроль — это парное сравнение, и его устойчивость крайне низка.")
            .WarningIf(Placebo.Count < 10,
                $"В плацебо-тесте участвует {Placebo.Count} доноров. Минимальное достижимое " +
                "p-значение равно единице, делённой на их число, поэтому при малом пуле " +
                "значимость получить невозможно.")
            .Warning("Вывод о значимости здесь ранговый, а не асимптотический: он показывает, " +
                     "насколько необычен объект среди доноров, и не даёт доверительного " +
                     "интервала для эффекта.")
            .Recommendation("Приводите график двух траекторий и отдельно график разности: " +
                            "совпадение до вмешательства и расхождение после — единственное " +
                            "наглядное обоснование метода.")
            .Recommendation("Исключайте из пула доноров тех, кто мог испытать то же " +
                            "вмешательство или его последствия, иначе эффект окажется занижен.")
            .Build();
    }
}

/// <summary>
/// Синтетический контроль: построение взвешенной комбинации доноров,
/// воспроизводящей объект до вмешательства.
/// </summary>
/// <remarks>
/// <para>
/// Когда объект под воздействием один — регион, город, компания — обычная
/// разность разностей опирается на произвольный выбор контрольной группы.
/// Синтетический контроль выбирает веса доноров так, чтобы взвешенная
/// комбинация повторяла траекторию объекта до вмешательства:
/// </para>
/// <code>
/// min_w sum_{t &lt; T0} ( y_t - sum_j w_j y_jt )^2,
/// w_j &gt;= 0,  sum_j w_j = 1
/// </code>
/// <para>
/// Ограничения неотрицательности и суммы к единице принципиальны: они не дают
/// экстраполировать за пределы наблюдаемого множества доноров и делают веса
/// интерпретируемыми.
/// </para>
/// <para>
/// Значимость оценивается плацебо-тестом: та же процедура применяется к каждому
/// донору, как если бы вмешательство коснулось его. Если отношение ошибок после
/// и до вмешательства у настоящего объекта выделяется на фоне доноров, эффект
/// считается реальным. Ранг в этом распределении и даёт p-значение.
/// </para>
/// </remarks>
public static class SyntheticControl
{
    /// <summary>Строит синтетический контроль и проводит плацебо-тест.</summary>
    /// <param name="treated">Ряд объекта под воздействием.</param>
    /// <param name="donors">Ряды доноров: строка — период, столбец — донор.</param>
    /// <param name="donorNames">Названия доноров.</param>
    /// <param name="treatmentPeriod">Номер периода начала вмешательства.</param>
    /// <param name="unitName">Название объекта под воздействием.</param>
    /// <returns>Веса доноров, траектории и результат плацебо-теста.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы или периодов до вмешательства мало.</exception>
    public static SyntheticControlResult Build(
        Vector treated, Matrix donors, IReadOnlyList<string>? donorNames,
        int treatmentPeriod, string unitName = "объект")
    {
        ArgumentNullException.ThrowIfNull(treated);
        ArgumentNullException.ThrowIfNull(donors);

        int periods = treated.Count;
        if (donors.Height != periods)
            throw new ArgumentException("Ряды доноров должны совпадать по длине с рядом объекта.", nameof(donors));
        if (treatmentPeriod < 3 || treatmentPeriod >= periods)
            throw new ArgumentException("Нужно минимум три периода до вмешательства.", nameof(treatmentPeriod));
        if (donors.Width < 2)
            throw new ArgumentException("Нужно минимум два донора.", nameof(donors));

        double[] weights = Optimize(treated, donors, treatmentPeriod);

        var synthetic = new Vector(periods);
        var gap = new Vector(periods);

        for (int t = 0; t < periods; t++)
        {
            double value = 0;
            for (int j = 0; j < donors.Width; j++) value += weights[j] * donors[t, j];

            synthetic[t] = value;
            gap[t] = treated[t] - value;
        }

        double preRmspe = Rmspe(gap, 0, treatmentPeriod);
        double postRmspe = Rmspe(gap, treatmentPeriod, periods);

        double averageEffect = 0;
        for (int t = treatmentPeriod; t < periods; t++) averageEffect += gap[t];
        averageEffect /= periods - treatmentPeriod;

        var names = new List<string>(donors.Width);
        for (int j = 0; j < donors.Width; j++)
            names.Add(donorNames is not null && j < donorNames.Count ? donorNames[j] : $"донор {j + 1}");

        var ranked = names
            .Select((name, index) => new DonorWeight(name, weights[index]))
            .OrderByDescending(w => w.Weight)
            .ToList();

        var placebo = Placebo(treated, donors, names, treatmentPeriod);
        double ratio = preRmspe > 0 ? postRmspe / preRmspe : 0;

        int betterOrEqual = placebo.Count(p => p.Ratio >= ratio) + 1;
        double pValue = (double)betterOrEqual / (placebo.Count + 1);

        return new SyntheticControlResult
        {
            Unit = unitName,
            Weights = ranked,
            Actual = treated,
            Synthetic = synthetic,
            Gap = gap,
            PreTreatmentPeriods = treatmentPeriod,
            AverageEffect = averageEffect,
            PreTreatmentRmspe = preRmspe,
            PostTreatmentRmspe = postRmspe,
            Placebo = placebo,
            PValue = pValue,
        };
    }

    /// <summary>Подбирает веса доноров проекционным градиентным спуском на симплексе.</summary>
    private static double[] Optimize(Vector treated, Matrix donors, int treatmentPeriod)
    {
        int m = donors.Width;
        var weights = new double[m];
        for (int j = 0; j < m; j++) weights[j] = 1.0 / m;

        double scale = 0;
        for (int t = 0; t < treatmentPeriod; t++)
            for (int j = 0; j < m; j++) scale += donors[t, j] * donors[t, j];

        double step = scale > 0 ? 1.0 / scale : 1e-3;

        for (int iteration = 0; iteration < 5000; iteration++)
        {
            var gradient = new double[m];

            for (int t = 0; t < treatmentPeriod; t++)
            {
                double prediction = 0;
                for (int j = 0; j < m; j++) prediction += weights[j] * donors[t, j];

                double residual = prediction - treated[t];
                for (int j = 0; j < m; j++) gradient[j] += 2 * residual * donors[t, j];
            }

            double shift = 0;
            for (int j = 0; j < m; j++)
            {
                double updated = weights[j] - (step * gradient[j]);
                shift += Math.Abs(updated - weights[j]);
                weights[j] = updated;
            }

            ProjectToSimplex(weights);
            if (shift < 1e-12) break;
        }

        return weights;
    }

    /// <summary>Проекция вектора на симплекс: неотрицательность и сумма к единице.</summary>
    private static void ProjectToSimplex(double[] weights)
    {
        int m = weights.Length;
        double[] sorted = [.. weights.OrderByDescending(v => v)];

        double cumulative = 0, theta = 0;
        for (int j = 0; j < m; j++)
        {
            cumulative += sorted[j];
            double candidate = (cumulative - 1) / (j + 1);
            if (j + 1 == m || sorted[j + 1] <= candidate) { theta = candidate; break; }
        }

        for (int j = 0; j < m; j++) weights[j] = Math.Max(0, weights[j] - theta);
    }

    /// <summary>Плацебо-тест: та же процедура для каждого донора.</summary>
    private static IReadOnlyList<(string Donor, double Ratio)> Placebo(
        Vector treated, Matrix donors, IReadOnlyList<string> names, int treatmentPeriod)
    {
        int periods = treated.Count, m = donors.Width;
        if (m < 3) return [];

        var results = new List<(string, double)>(m);

        for (int target = 0; target < m; target++)
        {
            var fake = new Vector(periods);
            var pool = new Matrix(periods, m - 1);

            for (int t = 0; t < periods; t++)
            {
                fake[t] = donors[t, target];

                int column = 0;
                for (int j = 0; j < m; j++)
                {
                    if (j == target) continue;
                    pool[t, column++] = donors[t, j];
                }
            }

            double[] weights = Optimize(fake, pool, treatmentPeriod);
            var gap = new Vector(periods);

            for (int t = 0; t < periods; t++)
            {
                double value = 0;
                for (int j = 0; j < m - 1; j++) value += weights[j] * pool[t, j];
                gap[t] = fake[t] - value;
            }

            double pre = Rmspe(gap, 0, treatmentPeriod);
            double post = Rmspe(gap, treatmentPeriod, periods);

            results.Add((names[target], pre > 0 ? post / pre : 0));
        }

        return results;
    }

    /// <summary>Корень средней квадратичной ошибки на отрезке периодов.</summary>
    private static double Rmspe(Vector gap, int from, int to)
    {
        if (to <= from) return 0;

        double sum = 0;
        for (int t = from; t < to; t++) sum += gap[t] * gap[t];

        return Math.Sqrt(sum / (to - from));
    }
}
