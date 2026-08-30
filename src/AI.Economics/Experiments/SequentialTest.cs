using System;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;
using AI.Statistics;

namespace AI.Economics.Experiments;

/// <summary>Результат последовательного критерия со всегда допустимым p-значением.</summary>
public sealed record SequentialTestResult : IInterpretable
{
    /// <summary>Всегда допустимое p-значение по каждому шагу наблюдений.</summary>
    public Vector PValues { get; init; } = new Vector(0);

    /// <summary>Оценка эффекта по каждому шагу.</summary>
    public Vector EffectPath { get; init; } = new Vector(0);

    /// <summary>Нижняя граница доверительной последовательности.</summary>
    public Vector LowerBound { get; init; } = new Vector(0);

    /// <summary>Верхняя граница доверительной последовательности.</summary>
    public Vector UpperBound { get; init; } = new Vector(0);

    /// <summary>Итоговое всегда допустимое p-значение.</summary>
    public double FinalPValue { get; init; }

    /// <summary>Итоговая оценка эффекта.</summary>
    public double FinalEffect { get; init; }

    /// <summary>Номер наблюдения, на котором можно было остановиться; −1, если решения нет.</summary>
    public int StoppingPoint { get; init; } = -1;

    /// <summary>Сколько наблюдений сэкономлено по сравнению с полным горизонтом.</summary>
    public double ObservationsSaved { get; init; }

    /// <summary>Уровень значимости.</summary>
    public double Alpha { get; init; }

    /// <summary>Всего наблюдений в каждой группе.</summary>
    public int SampleSize { get; init; }

    /// <summary>p-значение обычного критерия с фиксированным горизонтом на всей выборке.</summary>
    public double FixedHorizonPValue { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool decided = StoppingPoint >= 0;
        bool disagreement = decided != (FixedHorizonPValue <= Alpha);

        return new InterpretationBuilder("Последовательный критерий (mSPRT)")
            .Summary(decided
                ? $"Различие обнаружено на {StoppingPoint}-м наблюдении из {SampleSize}: " +
                  $"эксперимент можно было остановить, сэкономив {Fmt.Pct(ObservationsSaved)} трафика. " +
                  $"Итоговый эффект {Fmt.Num(FinalEffect, 4)}, всегда допустимое p-значение " +
                  $"{Fmt.Num(FinalPValue, 4)}."
                : $"Различие не обнаружено за {SampleSize} наблюдений. Всегда допустимое " +
                  $"p-значение {Fmt.Num(FinalPValue, 4)} при пороге {Fmt.Num(Alpha, 3)}.")
            .Metric("Всегда допустимое p", FinalPValue, null,
                "смотреть на него можно в любой момент без раздувания ошибки",
                FinalPValue <= Alpha ? MetricQuality.Good : MetricQuality.Neutral, 4)
            .Metric("p фиксированного горизонта", FixedHorizonPValue, null,
                "обычный критерий на полной выборке — только для сравнения",
                MetricQuality.Unknown, 4)
            .Metric("Точка остановки", decided ? Fmt.Int(StoppingPoint) : "не достигнута",
                decided ? "наблюдений" : null, "когда можно было прекратить эксперимент",
                decided ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Экономия трафика", Fmt.Pct(ObservationsSaved), null,
                "доля наблюдений, которые не понадобились",
                ObservationsSaved > 0.2 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Эффект", FinalEffect, null,
                $"интервал [{Fmt.Num(LowerBound.Count > 0 ? LowerBound[LowerBound.Count - 1] : double.NaN, 4)}; " +
                $"{Fmt.Num(UpperBound.Count > 0 ? UpperBound[UpperBound.Count - 1] : double.NaN, 4)}]",
                MetricQuality.Neutral, 4)
            .Finding("Всегда допустимое p-значение не растёт от подглядывания: критерий строится " +
                     "как отношение правдоподобий со смесью априорных значений эффекта, и его " +
                     "уровень ошибки контролируется на любой траектории остановки.")
            .FindingIf(decided && ObservationsSaved > 0.3,
                $"Экономия {Fmt.Pct(ObservationsSaved)} трафика означает, что эксперимент " +
                "занял бы существенно меньше времени, чем планировалось по фиксированному горизонту.")
            .FindingIf(disagreement,
                "Последовательный и обычный критерии дали разные ответы. Это ожидаемо: " +
                "последовательный консервативнее, он платит мощностью за право смотреть " +
                "на данные в любой момент.")
            .WarningIf(!decided,
                "Отсутствие решения не означает отсутствия эффекта. Возможно, эффект есть, " +
                "но меньше того, который выборка способна обнаружить.")
            .WarningIf(SampleSize < 200,
                $"Наблюдений всего {SampleSize}: нормальное приближение, на котором построен " +
                "критерий, работает грубо.")
            .Warning("Порядок наблюдений имеет значение. Данные должны поступать в том порядке, " +
                     "в котором они собирались, иначе точка остановки не имеет смысла.")
            .Recommendation("Фиксируйте правило остановки до запуска: последовательный критерий " +
                            "защищает от подглядывания, но не от смены метрики по ходу дела.")
            .Build();
    }
}

/// <summary>Результат байесовского сравнения двух вариантов.</summary>
public sealed record BayesianAbResult : IInterpretable
{
    /// <summary>Вероятность того, что вариант B лучше варианта A.</summary>
    public double ProbabilityBetter { get; init; }

    /// <summary>Ожидаемые потери от выбора B, если на самом деле лучше A.</summary>
    public double ExpectedLossChoosingB { get; init; }

    /// <summary>Ожидаемые потери от выбора A, если на самом деле лучше B.</summary>
    public double ExpectedLossChoosingA { get; init; }

    /// <summary>Апостериорное среднее конверсии варианта A.</summary>
    public double PosteriorMeanA { get; init; }

    /// <summary>Апостериорное среднее конверсии варианта B.</summary>
    public double PosteriorMeanB { get; init; }

    /// <summary>Нижняя граница интервала для разности конверсий.</summary>
    public double CredibleLow { get; init; }

    /// <summary>Верхняя граница интервала для разности конверсий.</summary>
    public double CredibleHigh { get; init; }

    /// <summary>Порог ожидаемых потерь, ниже которого решение считается принятым.</summary>
    public double LossThreshold { get; init; }

    /// <summary>Наблюдений в варианте A.</summary>
    public int TrialsA { get; init; }

    /// <summary>Наблюдений в варианте B.</summary>
    public int TrialsB { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool decisive = ExpectedLossChoosingB < LossThreshold || ExpectedLossChoosingA < LossThreshold;
        string winner = ProbabilityBetter > 0.5 ? "B" : "A";
        double lift = PosteriorMeanA > 0 ? (PosteriorMeanB - PosteriorMeanA) / PosteriorMeanA : 0;

        return new InterpretationBuilder("Байесовское сравнение вариантов")
            .Summary($"Вероятность того, что B лучше A, равна {Fmt.Pct(ProbabilityBetter)}. " +
                     $"Ожидаемый прирост конверсии {Fmt.Pct(lift)}, интервал разности " +
                     $"[{Fmt.Pct(CredibleLow, 2)}; {Fmt.Pct(CredibleHigh, 2)}]. " +
                     (decisive
                         ? $"Ожидаемые потери от выбора «{winner}» ниже порога — решение можно принимать."
                         : "Ожидаемые потери выше порога — данных пока недостаточно."))
            .Metric("P(B лучше A)", Fmt.Pct(ProbabilityBetter), null,
                "апостериорная вероятность, а не p-значение",
                ProbabilityBetter is > 0.95 or < 0.05 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Ожидаемые потери от B", ExpectedLossChoosingB, null,
                "средняя цена ошибки, если выбрать B", MetricQuality.Neutral, 5)
            .Metric("Ожидаемые потери от A", ExpectedLossChoosingA, null,
                "средняя цена ошибки, если выбрать A", MetricQuality.Neutral, 5)
            .Metric("Конверсия A", Fmt.Pct(PosteriorMeanA, 2), null, "апостериорное среднее")
            .Metric("Конверсия B", Fmt.Pct(PosteriorMeanB, 2), null, "апостериорное среднее")
            .Metric("Наблюдений", TrialsA + TrialsB, null,
                $"{TrialsA} в A, {TrialsB} в B", MetricQuality.Unknown, 0)
            .Finding("Байесовский подход отвечает на тот вопрос, который задаёт бизнес: " +
                     "какова вероятность, что вариант лучше, и во что обойдётся ошибка. " +
                     "p-значение отвечает на другой — насколько данные необычны при отсутствии эффекта.")
            .FindingIf(decisive,
                $"Ожидаемые потери от выбора «{winner}» составляют " +
                $"{Fmt.Num(Math.Min(ExpectedLossChoosingA, ExpectedLossChoosingB), 5)} — " +
                "меньше порога безразличия. Дальнейший сбор данных не окупается.")
            .FindingIf(CredibleLow < 0 && CredibleHigh > 0,
                "Интервал разности накрывает ноль: направление эффекта не установлено.")
            .WarningIf(!decisive,
                "Ожидаемые потери выше порога: продолжайте эксперимент или снижайте требования " +
                "к точности, если цена ошибки для бизнеса мала.")
            .WarningIf(TrialsA < 100 || TrialsB < 100,
                "Наблюдений мало, и результат заметно зависит от априорного распределения. " +
                "Использовано равномерное Beta(1, 1).")
            .Warning("Байесовский критерий не защищает от подглядывания сам по себе: правило " +
                     "«остановиться, когда вероятность превысит 95 %» тоже завышает ошибку. " +
                     "Порогом должны быть ожидаемые потери, а не вероятность.")
            .Build();
    }
}

/// <summary>
/// Последовательные критерии: остановка эксперимента в любой момент без
/// раздувания ошибки первого рода.
/// </summary>
/// <remarks>
/// <para>
/// Проблема, которую они решают: при фиксированном горизонте подглядывание
/// в промежуточные результаты с остановкой при достижении значимости
/// повышает ошибку первого рода с 5 % до 20–30 %. Половина «побед»
/// в таких экспериментах — артефакт правила остановки.
/// </para>
/// <para>
/// Смешанное отношение правдоподобий (mSPRT) строит статистику, у которой
/// уровень ошибки контролируется одновременно на всех моментах остановки.
/// Эффект под альтернативой не фиксируется, а интегрируется по нормальному
/// априорному распределению с параметром <c>tau</c>: чем он больше, тем
/// критерий чувствительнее к крупным эффектам и слабее к мелким.
/// </para>
/// </remarks>
public static class SequentialTest
{
    /// <summary>Выполняет последовательный критерий для двух рядов наблюдений.</summary>
    /// <param name="control">Наблюдения контрольной группы в порядке поступления.</param>
    /// <param name="treatment">Наблюдения группы воздействия в порядке поступления.</param>
    /// <param name="tau">
    /// Масштаб априорного распределения эффекта. Разумное значение —
    /// ожидаемый эффект, который имеет смысл обнаруживать.
    /// </param>
    /// <param name="alpha">Уровень значимости.</param>
    /// <returns>Траектория p-значений, доверительная последовательность и точка остановки.</returns>
    /// <exception cref="ArgumentNullException">Ряды не заданы.</exception>
    /// <exception cref="ArgumentException">Наблюдений слишком мало.</exception>
    public static SequentialTestResult Run(Vector control, Vector treatment, double tau = 0.05, double alpha = 0.05)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(treatment);

        int n = Math.Min(control.Count, treatment.Count);
        if (n < 20) throw new ArgumentException("Нужно минимум 20 наблюдений в каждой группе.", nameof(control));

        var pValues = new Vector(n);
        var effects = new Vector(n);
        var lower = new Vector(n);
        var upper = new Vector(n);

        double sumControl = 0, sumTreatment = 0;
        double sumSqControl = 0, sumSqTreatment = 0;
        double best = 1.0;
        int stopping = -1;

        for (int i = 0; i < n; i++)
        {
            sumControl += control[i];
            sumTreatment += treatment[i];
            sumSqControl += control[i] * control[i];
            sumSqTreatment += treatment[i] * treatment[i];

            int k = i + 1;
            double meanControl = sumControl / k;
            double meanTreatment = sumTreatment / k;
            double effect = meanTreatment - meanControl;
            effects[i] = effect;

            if (k < 2)
            {
                pValues[i] = 1;
                lower[i] = double.NaN;
                upper[i] = double.NaN;
                continue;
            }

            double varControl = Math.Max((sumSqControl / k) - (meanControl * meanControl), 1e-12);
            double varTreatment = Math.Max((sumSqTreatment / k) - (meanTreatment * meanTreatment), 1e-12);
            double sigma2 = varControl + varTreatment;

            // Смешанное отношение правдоподобий: априорное распределение
            // эффекта нормальное с дисперсией tau^2
            double denominator = sigma2 + (k * tau * tau);
            double likelihoodRatio = Math.Sqrt(sigma2 / denominator)
                                   * Math.Exp(k * k * tau * tau * effect * effect / (2 * sigma2 * denominator));

            double p = Math.Min(1.0, 1.0 / Math.Max(likelihoodRatio, 1e-300));
            best = Math.Min(best, p);
            pValues[i] = best;

            // Доверительная последовательность: множество эффектов, которые
            // не были бы отвергнуты на этом шаге
            double halfWidth = Math.Sqrt(2 * sigma2 * denominator
                * Math.Log(Math.Sqrt(denominator / sigma2) / alpha) / (k * k * tau * tau));
            lower[i] = effect - halfWidth;
            upper[i] = effect + halfWidth;

            if (stopping < 0 && best <= alpha) stopping = k;
        }

        double pooledVariance = (Variance(control, n) / n) + (Variance(treatment, n) / n);
        double fixedZ = pooledVariance > 0 ? effects[n - 1] / Math.Sqrt(pooledVariance) : 0;

        return new SequentialTestResult
        {
            PValues = pValues,
            EffectPath = effects,
            LowerBound = lower,
            UpperBound = upper,
            FinalPValue = best,
            FinalEffect = effects[n - 1],
            StoppingPoint = stopping,
            ObservationsSaved = stopping > 0 ? 1.0 - ((double)stopping / n) : 0,
            Alpha = alpha,
            SampleSize = n,
            FixedHorizonPValue = 2.0 * (1.0 - EconMath.NormalCdf(Math.Abs(fixedZ))),
        };
    }

    /// <summary>
    /// Байесовское сравнение двух вариантов по конверсии с равномерным
    /// априорным распределением.
    /// </summary>
    /// <param name="successesA">Число конверсий в варианте A.</param>
    /// <param name="trialsA">Число наблюдений в варианте A.</param>
    /// <param name="successesB">Число конверсий в варианте B.</param>
    /// <param name="trialsB">Число наблюдений в варианте B.</param>
    /// <param name="lossThreshold">Порог ожидаемых потерь для принятия решения.</param>
    /// <param name="draws">Число выборок из апостериорного распределения.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Вероятность превосходства и ожидаемые потери.</returns>
    /// <exception cref="ArgumentException">Некорректные счётчики.</exception>
    public static BayesianAbResult Bayesian(
        int successesA, int trialsA, int successesB, int trialsB,
        double lossThreshold = 0.001, int draws = 50_000, int seed = 42)
    {
        if (trialsA <= 0 || trialsB <= 0)
            throw new ArgumentException("Число наблюдений должно быть положительным.", nameof(trialsA));
        if (successesA < 0 || successesB < 0 || successesA > trialsA || successesB > trialsB)
            throw new ArgumentException("Число конверсий должно лежать между нулём и числом наблюдений.",
                nameof(successesA));

        Random rng = RandomEngine.Create(seed);

        double alphaA = successesA + 1, betaA = trialsA - successesA + 1;
        double alphaB = successesB + 1, betaB = trialsB - successesB + 1;

        int better = 0;
        double lossB = 0, lossA = 0;
        var differences = new double[draws];

        for (int i = 0; i < draws; i++)
        {
            double a = RandomEngine.NextBeta(rng, alphaA, betaA);
            double b = RandomEngine.NextBeta(rng, alphaB, betaB);

            differences[i] = b - a;
            if (b > a) better++;

            lossB += Math.Max(a - b, 0);
            lossA += Math.Max(b - a, 0);
        }

        Array.Sort(differences);

        return new BayesianAbResult
        {
            ProbabilityBetter = (double)better / draws,
            ExpectedLossChoosingB = lossB / draws,
            ExpectedLossChoosingA = lossA / draws,
            PosteriorMeanA = alphaA / (alphaA + betaA),
            PosteriorMeanB = alphaB / (alphaB + betaB),
            CredibleLow = EconMath.Quantile(differences, 0.025),
            CredibleHigh = EconMath.Quantile(differences, 0.975),
            LossThreshold = lossThreshold,
            TrialsA = trialsA,
            TrialsB = trialsB,
        };
    }

    private static double Variance(Vector values, int count)
    {
        double mean = 0;
        for (int i = 0; i < count; i++) mean += values[i];
        mean /= count;

        double sum = 0;
        for (int i = 0; i < count; i++) sum += (values[i] - mean) * (values[i] - mean);
        return count > 1 ? sum / (count - 1) : 0;
    }
}
