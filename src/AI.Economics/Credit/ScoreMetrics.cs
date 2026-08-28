using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Credit;

/// <summary>Качество разделения и калибровки скоринговой модели.</summary>
public sealed record ScoreQuality : IInterpretable
{
    /// <summary>Площадь под ROC-кривой.</summary>
    public double Auc { get; init; }

    /// <summary>Коэффициент Джини: <c>2 * AUC - 1</c>.</summary>
    public double Gini => (2 * Auc) - 1;

    /// <summary>Статистика Колмогорова — Смирнова: максимальное расхождение накопленных долей.</summary>
    public double Ks { get; init; }

    /// <summary>Порог вероятности, на котором достигается максимум статистики.</summary>
    public double KsThreshold { get; init; }

    /// <summary>Оценка Бриера — средний квадрат ошибки вероятности.</summary>
    public double Brier { get; init; }

    /// <summary>
    /// Наклон калибровки: единица означает, что модель не переоценивает
    /// и не недооценивает различия в риске.
    /// </summary>
    public double CalibrationSlope { get; init; }

    /// <summary>Сдвиг калибровки: ноль означает верный средний уровень риска.</summary>
    public double CalibrationIntercept { get; init; }

    /// <summary>Средняя предсказанная вероятность дефолта.</summary>
    public double MeanPredicted { get; init; }

    /// <summary>Фактическая доля дефолтов.</summary>
    public double MeanObserved { get; init; }

    /// <summary>Доля ложноположительных для ROC-кривой.</summary>
    public Vector RocFalsePositive { get; init; } = new Vector(0);

    /// <summary>Доля истинноположительных для ROC-кривой.</summary>
    public Vector RocTruePositive { get; init; } = new Vector(0);

    /// <summary>Предсказанная вероятность по децилям.</summary>
    public Vector CalibrationPredicted { get; init; } = new Vector(0);

    /// <summary>Наблюдённая доля дефолтов по децилям.</summary>
    public Vector CalibrationObserved { get; init; } = new Vector(0);

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Число дефолтов.</summary>
    public int Defaults { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool weak = Gini < 0.3;
        bool suspicious = Gini > 0.85;
        bool miscalibrated = Math.Abs(MeanPredicted - MeanObserved) > 0.2 * Math.Max(MeanObserved, 1e-9);
        bool slopeOff = Math.Abs(CalibrationSlope - 1) > 0.25;

        return new InterpretationBuilder("Качество скоринговой модели")
            .Summary($"Коэффициент Джини {Fmt.Num(Gini, 3)} при статистике Колмогорова — Смирнова " +
                     $"{Fmt.Num(Ks, 3)}. Средняя предсказанная вероятность дефолта " +
                     $"{Fmt.Pct(MeanPredicted, 2)} против фактических {Fmt.Pct(MeanObserved, 2)}: " +
                     (miscalibrated
                         ? "модель систематически смещена по уровню риска."
                         : "уровень риска предсказан верно."))
            .Metric("Джини", Gini, null,
                "розничный скоринг обычно даёт 0,4-0,6; ниже 0,3 модель слабая",
                suspicious ? MetricQuality.Critical
                    : weak ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("AUC", Auc, null, "площадь под ROC-кривой", MetricQuality.Neutral, 3)
            .Metric("Колмогоров — Смирнов", Ks, null,
                $"максимум расхождения достигается на пороге {Fmt.Pct(KsThreshold, 2)}",
                Ks > 0.3 ? MetricQuality.Good : MetricQuality.Warning, 3)
            .Metric("Оценка Бриера", Brier, null,
                "средний квадрат ошибки вероятности; меньше — лучше", MetricQuality.Neutral, 4)
            .Metric("Наклон калибровки", CalibrationSlope, null,
                "единица означает верно оценённый разброс риска",
                slopeOff ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("Средний прогноз", Fmt.Pct(MeanPredicted, 2), null,
                $"фактически {Fmt.Pct(MeanObserved, 2)}",
                miscalibrated ? MetricQuality.Critical : MetricQuality.Good)
            .Metric("Наблюдений", Observations, null,
                $"из них дефолтов {Defaults}", MetricQuality.Unknown, 0)
            .Finding("Разделение и калибровка — независимые свойства. Модель может идеально " +
                     "ранжировать заёмщиков и при этом систематически завышать вероятность " +
                     "дефолта: первое нужно для решения о выдаче, второе — для резервов и цены риска.")
            .FindingIf(!weak && !suspicious,
                $"Разделяющая способность приемлема: коэффициент Джини {Fmt.Num(Gini, 3)} " +
                "находится в рабочем диапазоне для кредитного скоринга.")
            .FindingIf(CalibrationSlope < 0.75,
                "Наклон калибровки заметно ниже единицы: модель переоценивает различия " +
                "между заёмщиками. Обычный признак переобучения.")
            .FindingIf(CalibrationSlope > 1.25,
                "Наклон выше единицы: модель недооценивает различия, её прогнозы слишком " +
                "сжаты к среднему.")
            .WarningIf(suspicious,
                $"Коэффициент Джини {Fmt.Num(Gini, 3)} неправдоподобно высок для кредитного " +
                "скоринга. Проверьте, не попал ли в признаки факт, известный только после дефолта.")
            .WarningIf(weak,
                "Модель слабо разделяет заёмщиков. При таком качестве отсечение по баллу " +
                "почти не улучшает портфель по сравнению со случайным отбором.")
            .WarningIf(miscalibrated,
                $"Средний прогноз расходится с фактом на " +
                $"{Fmt.Pct(Math.Abs(MeanPredicted - MeanObserved) / Math.Max(MeanObserved, 1e-9))}. " +
                "Резервы и цена риска, посчитанные по такой модели, будут неверны.")
            .WarningIf(Defaults < 50,
                $"Дефолтов всего {Defaults}. Метрики разделения на такой выборке " +
                "имеют широкий доверительный интервал.")
            .Warning("Все метрики посчитаны на той выборке, которая передана. Если это " +
                     "обучающая выборка, значения оптимистичны — проверяйте на отложенной " +
                     "и на более позднем периоде.")
            .Recommendation("Разделяйте проверку на разделяющую способность и на калибровку: " +
                            "первая падает медленно, вторая ломается при первом же изменении " +
                            "макроусловий.")
            .Build();
    }
}

/// <summary>Индекс стабильности популяции по интервалам.</summary>
public sealed record PsiResult : IInterpretable
{
    /// <summary>Название проверяемой величины.</summary>
    public string Variable { get; init; } = string.Empty;

    /// <summary>Значение индекса стабильности.</summary>
    public double Psi { get; init; }

    /// <summary>Границы интервалов, построенные по эталонной выборке.</summary>
    public Vector Boundaries { get; init; } = new Vector(0);

    /// <summary>Доли эталонной выборки по интервалам.</summary>
    public Vector ExpectedShares { get; init; } = new Vector(0);

    /// <summary>Доли текущей выборки по тем же интервалам.</summary>
    public Vector ActualShares { get; init; } = new Vector(0);

    /// <summary>Вклад каждого интервала в индекс.</summary>
    public Vector Contributions { get; init; } = new Vector(0);

    /// <summary>Наблюдений в эталонной выборке.</summary>
    public int ExpectedCount { get; init; }

    /// <summary>Наблюдений в текущей выборке.</summary>
    public int ActualCount { get; init; }

    /// <summary>Словесная оценка дрейфа по общепринятым порогам.</summary>
    public string Verdict => Psi switch
    {
        < 0.1 => "популяция стабильна",
        < 0.25 => "заметный сдвиг, нужен мониторинг",
        _ => "популяция изменилась, модель требует пересмотра",
    };

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int worst = 0;
        for (int i = 1; i < Contributions.Count; i++)
            if (Contributions[i] > Contributions[worst]) worst = i;

        return new InterpretationBuilder($"Стабильность популяции: {Variable}")
            .Summary($"Индекс стабильности {Fmt.Num(Psi, 4)} — {Verdict}. Наибольший вклад даёт " +
                     $"интервал {worst + 1}: доля изменилась с {Fmt.Pct(ExpectedShares[worst])} " +
                     $"до {Fmt.Pct(ActualShares[worst])}.")
            .Metric("Индекс стабильности", Psi, null,
                "до 0,1 — стабильно, до 0,25 — мониторинг, выше — пересмотр модели",
                Psi < 0.1 ? MetricQuality.Good : Psi < 0.25 ? MetricQuality.Warning : MetricQuality.Critical, 4)
            .Metric("Наибольший сдвиг", $"интервал {worst + 1}", null,
                $"{Fmt.Pct(ExpectedShares[worst])} против {Fmt.Pct(ActualShares[worst])}")
            .Metric("Эталонная выборка", ExpectedCount, "наблюдений", null, MetricQuality.Unknown, 0)
            .Metric("Текущая выборка", ActualCount, "наблюдений", null, MetricQuality.Unknown, 0)
            .Finding("Индекс измеряет сдвиг распределения, а не падение качества. Модель может " +
                     "работать при сдвинутой популяции и ломаться при стабильной — " +
                     "он лишь показывает, что входные данные перестали походить на обучающие.")
            .FindingIf(Psi >= 0.25,
                "Сдвиг велик настолько, что оценки вероятности дефолта перестают быть " +
                "применимыми: модель обучена на другой популяции.")
            .WarningIf(ActualCount < 200,
                $"Текущая выборка {ActualCount} наблюдений: индекс на малых выборках " +
                "завышается случайными колебаниями долей.")
            .Warning("Границы интервалов взяты из эталонной выборки. При сравнении разных " +
                     "периодов границы должны оставаться неизменными, иначе индекс " +
                     "измеряет не дрейф, а перестроение сетки.")
            .Recommendation("Считайте индекс отдельно по итоговому баллу и по каждому " +
                            "значимому признаку: так видно, какая именно переменная уехала.")
            .Build();
    }
}

/// <summary>
/// Метрики качества скоринговых моделей: разделение, калибровка, дрейф.
/// </summary>
/// <remarks>
/// Три группы метрик отвечают на три разных вопроса. Разделение (Джини,
/// Колмогоров — Смирнов) — насколько модель отличает будущих дефолтников
/// от исправных заёмщиков. Калибровка — верен ли предсказанный уровень риска
/// в абсолютном выражении. Индекс стабильности — не изменилась ли популяция
/// настолько, что модель перестала быть применимой.
/// </remarks>
public static class ScoreMetrics
{
    /// <summary>Оценивает разделяющую способность и калибровку модели.</summary>
    /// <param name="probabilities">Предсказанные вероятности дефолта.</param>
    /// <param name="defaults">Фактические дефолты.</param>
    /// <param name="calibrationBins">Число групп для кривой калибровки.</param>
    /// <returns>Полный набор метрик с разбором.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Длины не совпадают или нет обоих исходов.</exception>
    public static ScoreQuality Evaluate(
        Vector probabilities, IReadOnlyList<bool> defaults, int calibrationBins = 10)
    {
        ArgumentNullException.ThrowIfNull(probabilities);
        ArgumentNullException.ThrowIfNull(defaults);

        int n = probabilities.Count;
        if (n != defaults.Count)
            throw new ArgumentException("Длины прогнозов и исходов должны совпадать.", nameof(defaults));

        int bad = defaults.Count(d => d);
        int good = n - bad;
        if (bad == 0 || good == 0)
            throw new ArgumentException("В выборке должны быть и дефолты, и исправные заёмщики.", nameof(defaults));

        var ordered = Enumerable.Range(0, n)
            .Select(i => (Score: probabilities[i], Bad: defaults[i]))
            .OrderByDescending(p => p.Score)
            .ToList();

        // AUC по рангам: доля пар, в которых дефолтник получил больший балл
        double auc = RankAuc(probabilities, defaults);

        var fpr = new Vector(n + 1);
        var tpr = new Vector(n + 1);
        double ks = 0, ksThreshold = 0;
        int cumulativeBad = 0, cumulativeGood = 0;

        for (int i = 0; i < n; i++)
        {
            if (ordered[i].Bad) cumulativeBad++;
            else cumulativeGood++;

            double badRate = (double)cumulativeBad / bad;
            double goodRate = (double)cumulativeGood / good;

            tpr[i + 1] = badRate;
            fpr[i + 1] = goodRate;

            double separation = Math.Abs(badRate - goodRate);
            if (separation > ks) { ks = separation; ksThreshold = ordered[i].Score; }
        }

        double brier = 0;
        for (int i = 0; i < n; i++)
        {
            double error = probabilities[i] - (defaults[i] ? 1 : 0);
            brier += error * error;
        }
        brier /= n;

        (Vector predicted, Vector observed) = CalibrationCurve(ordered, calibrationBins);
        (double slope, double intercept) = CalibrationFit(probabilities, defaults);

        return new ScoreQuality
        {
            Auc = auc,
            Ks = ks,
            KsThreshold = ksThreshold,
            Brier = brier,
            CalibrationSlope = slope,
            CalibrationIntercept = intercept,
            MeanPredicted = probabilities.Average(),
            MeanObserved = (double)bad / n,
            RocFalsePositive = fpr,
            RocTruePositive = tpr,
            CalibrationPredicted = predicted,
            CalibrationObserved = observed,
            Observations = n,
            Defaults = bad,
        };
    }

    /// <summary>
    /// Индекс стабильности популяции между эталонной и текущей выборками.
    /// </summary>
    /// <param name="expected">Эталонная выборка, обычно обучающая.</param>
    /// <param name="actual">Текущая выборка.</param>
    /// <param name="bins">Число интервалов, построенных по эталонной выборке.</param>
    /// <param name="variable">Название величины для отчёта.</param>
    /// <returns>Индекс и разбивка по интервалам.</returns>
    /// <exception cref="ArgumentNullException">Выборки не заданы.</exception>
    /// <exception cref="ArgumentException">Выборки слишком малы.</exception>
    public static PsiResult PopulationStability(
        Vector expected, Vector actual, int bins = 10, string variable = "балл")
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        if (expected.Count < 20 || actual.Count < 20)
            throw new ArgumentException("Нужно минимум 20 наблюдений в каждой выборке.", nameof(expected));

        if (bins < 2) bins = 2;

        double[] sortedExpected = [.. expected.OrderBy(v => v)];
        var boundaries = new Vector(bins - 1);
        for (int b = 1; b < bins; b++)
            boundaries[b - 1] = EconMath.Quantile(sortedExpected, (double)b / bins);

        var expectedShares = new Vector(bins);
        var actualShares = new Vector(bins);
        var contributions = new Vector(bins);

        foreach (double value in expected) expectedShares[BinIndex(value, boundaries)]++;
        foreach (double value in actual) actualShares[BinIndex(value, boundaries)]++;

        double psi = 0;
        for (int b = 0; b < bins; b++)
        {
            // Сглаживание нужно, чтобы пустой интервал не давал бесконечность
            double e = Math.Max(expectedShares[b] / expected.Count, 1e-6);
            double a = Math.Max(actualShares[b] / actual.Count, 1e-6);

            contributions[b] = (a - e) * Math.Log(a / e);
            psi += contributions[b];

            expectedShares[b] = e;
            actualShares[b] = a;
        }

        return new PsiResult
        {
            Variable = variable,
            Psi = psi,
            Boundaries = boundaries,
            ExpectedShares = expectedShares,
            ActualShares = actualShares,
            Contributions = contributions,
            ExpectedCount = expected.Count,
            ActualCount = actual.Count,
        };
    }

    /// <summary>Площадь под ROC-кривой через статистику Манна — Уитни.</summary>
    private static double RankAuc(Vector scores, IReadOnlyList<bool> defaults)
    {
        int n = scores.Count;
        var ordered = Enumerable.Range(0, n)
            .OrderBy(i => scores[i])
            .ToArray();

        // Средние ранги для совпадающих значений: без этого AUC смещается
        // на моделях с дискретной шкалой баллов
        var ranks = new double[n];
        int index = 0;
        while (index < n)
        {
            int end = index;
            while (end + 1 < n && Math.Abs(scores[ordered[end + 1]] - scores[ordered[index]]) < 1e-12) end++;

            double averageRank = ((index + end) / 2.0) + 1;
            for (int k = index; k <= end; k++) ranks[ordered[k]] = averageRank;

            index = end + 1;
        }

        double badRankSum = 0;
        int bad = 0;
        for (int i = 0; i < n; i++)
        {
            if (!defaults[i]) continue;
            badRankSum += ranks[i];
            bad++;
        }

        int good = n - bad;
        return ((badRankSum - (bad * (bad + 1) / 2.0)) / ((double)bad * good));
    }

    private static (Vector Predicted, Vector Observed) CalibrationCurve(
        List<(double Score, bool Bad)> ordered, int bins)
    {
        if (bins < 2) bins = 2;
        int n = ordered.Count;

        var predicted = new Vector(bins);
        var observed = new Vector(bins);

        for (int b = 0; b < bins; b++)
        {
            int from = b * n / bins;
            int to = (b + 1) * n / bins;
            if (to <= from) continue;

            var slice = ordered.GetRange(from, to - from);
            predicted[b] = slice.Average(p => p.Score);
            observed[b] = slice.Count(p => p.Bad) / (double)slice.Count;
        }

        return (predicted, observed);
    }

    /// <summary>
    /// Калибровка по Коксу: логистическая регрессия факта дефолта
    /// на логит предсказанной вероятности.
    /// </summary>
    private static (double Slope, double Intercept) CalibrationFit(
        Vector probabilities, IReadOnlyList<bool> defaults)
    {
        int n = probabilities.Count;
        var x = new double[n, 2];
        var y = new double[n];

        for (int i = 0; i < n; i++)
        {
            double p = EconMath.Clamp(probabilities[i], 1e-6, 1 - 1e-6);
            x[i, 0] = 1.0;
            x[i, 1] = Math.Log(p / (1 - p));
            y[i] = defaults[i] ? 1 : 0;
        }

        var model = new LogisticRegression();
        model.Fit(x, y, ridge: 0, maxIterations: 40);

        return (model.Beta.Length > 1 ? model.Beta[1] : double.NaN,
                model.Beta.Length > 0 ? model.Beta[0] : double.NaN);
    }

    private static int BinIndex(double value, Vector boundaries)
    {
        for (int b = 0; b < boundaries.Count; b++)
            if (value < boundaries[b]) return b;

        return boundaries.Count;
    }
}
