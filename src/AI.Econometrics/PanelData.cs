using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Econometrics;

/// <summary>Способ учёта индивидуальных эффектов в панельных данных.</summary>
public enum PanelEstimator
{
    /// <summary>Объединённый МНК: индивидуальные эффекты игнорируются.</summary>
    Pooled,

    /// <summary>Фиксированные эффекты: внутригрупповое преобразование.</summary>
    FixedEffects,

    /// <summary>Двусторонние фиксированные эффекты: по объектам и по периодам.</summary>
    TwoWayFixedEffects,

    /// <summary>Случайные эффекты: обобщённый МНК с квазивнутригрупповым преобразованием.</summary>
    RandomEffects,

    /// <summary>Первые разности.</summary>
    FirstDifference,

    /// <summary>Межгрупповая регрессия на средних по объектам.</summary>
    BetweenEffects,
}

/// <summary>Панельный набор данных.</summary>
public sealed record PanelDataset
{
    /// <summary>Матрица регрессоров без свободного члена.</summary>
    public Matrix Regressors { get; init; } = new(1, 1);

    /// <summary>Вектор отклика.</summary>
    public Vector Response { get; init; } = new(0);

    /// <summary>Идентификаторы объектов по наблюдениям.</summary>
    public IReadOnlyList<int> Units { get; init; } = [];

    /// <summary>Идентификаторы периодов по наблюдениям.</summary>
    public IReadOnlyList<int> Periods { get; init; } = [];

    /// <summary>Названия регрессоров.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>Число наблюдений.</summary>
    public int Observations => Response.Count;

    /// <summary>Число объектов.</summary>
    public int UnitCount => Units.Distinct().Count();

    /// <summary>Число периодов.</summary>
    public int PeriodCount => Periods.Distinct().Count();

    /// <summary>Сбалансирована ли панель.</summary>
    public bool IsBalanced => UnitCount > 0 && Observations == UnitCount * PeriodCount;

    /// <summary>Проверяет согласованность размерностей.</summary>
    /// <exception cref="ArgumentException">Размерности не совпадают.</exception>
    public void Validate()
    {
        if (Regressors.Height != Observations)
            throw new ArgumentException("Число строк регрессоров должно совпадать с длиной отклика.");
        if (Units.Count != Observations || Periods.Count != Observations)
            throw new ArgumentException("Идентификаторы объектов и периодов должны быть заданы для каждого наблюдения.");
        if (Observations < Regressors.Width + 2)
            throw new ArgumentException("Наблюдений недостаточно для оценивания.");
    }
}

/// <summary>Результат оценивания панельной модели.</summary>
public sealed record PanelResult : IInterpretable
{
    /// <summary>Использованный оценщик.</summary>
    public PanelEstimator Estimator { get; init; }

    /// <summary>Оценки коэффициентов.</summary>
    public IReadOnlyList<Coefficient> Coefficients { get; init; } = [];

    /// <summary>Внутригрупповой коэффициент детерминации.</summary>
    public double RSquared { get; init; }

    /// <summary>Число объектов.</summary>
    public int Units { get; init; }

    /// <summary>Число периодов.</summary>
    public int Periods { get; init; }

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Стандартное отклонение индивидуальных эффектов.</summary>
    public double SigmaUnit { get; init; }

    /// <summary>Стандартное отклонение идиосинкратической ошибки.</summary>
    public double SigmaError { get; init; }

    /// <summary>Доля дисперсии, приходящаяся на индивидуальные эффекты.</summary>
    public double Rho =>
        (SigmaUnit * SigmaUnit) + (SigmaError * SigmaError) > 0
            ? SigmaUnit * SigmaUnit / ((SigmaUnit * SigmaUnit) + (SigmaError * SigmaError))
            : 0;

    /// <summary>Параметр квазивнутригруппового преобразования в модели случайных эффектов.</summary>
    public double Theta { get; init; }

    /// <summary>Остатки преобразованной регрессии.</summary>
    public Vector Residuals { get; init; } = new(0);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        Coefficient? strongest = Coefficients
            .Where(c => c.Name != "const")
            .OrderByDescending(c => Math.Abs(c.TStatistic))
            .FirstOrDefault();

        bool effectsMatter = Rho > 0.2;

        var builder = new InterpretationBuilder($"Панельная регрессия: {EstimatorName()}")
            .Summary($"{Observations} наблюдений по {Units} объектам за {Periods} периодов. " +
                     $"Внутригрупповой R² {Fmt.Num(RSquared, 3)}. На индивидуальные эффекты " +
                     $"приходится {Fmt.Pct(Rho, 1)} необъяснённой дисперсии.")
            .Metric("R²", RSquared, null, "после преобразования данных", MetricQuality.Neutral, 4)
            .Metric("Объектов", Units, null, $"периодов {Periods}", MetricQuality.Neutral, 0)
            .Metric("Доля дисперсии эффектов", Rho, null,
                effectsMatter ? "индивидуальные различия существенны" : "объекты близки между собой",
                MetricQuality.Neutral, 3)
            .Metric("Разброс эффектов", SigmaUnit, null,
                $"идиосинкратическая ошибка {Fmt.Num(SigmaError, 4)}", MetricQuality.Neutral, 4);

        if (Estimator == PanelEstimator.RandomEffects)
        {
            builder.Metric("Тета", Theta, null,
                "доля группового среднего, вычитаемая при преобразовании", MetricQuality.Neutral, 3);
        }

        foreach (Coefficient coefficient in Coefficients)
        {
            builder.Metric(coefficient.Name, coefficient.Estimate, null,
                $"ст. ошибка {Fmt.Num(coefficient.StandardError, 4)}, p = {Fmt.Num(coefficient.PValue, 4)} " +
                coefficient.Stars,
                coefficient.IsSignificant ? MetricQuality.Good : MetricQuality.Neutral, 4);
        }

        return builder
            .FindingIf(strongest is not null,
                $"Наибольшая по значимости связь — «{strongest?.Name}»: коэффициент " +
                $"{Fmt.Num(strongest?.Estimate ?? 0, 4)} при t = {Fmt.Num(strongest?.TStatistic ?? 0, 2)}.")
            .FindingIf(Estimator is PanelEstimator.FixedEffects or PanelEstimator.TwoWayFixedEffects,
                "Фиксированные эффекты убирают всё, что постоянно во времени внутри объекта. " +
                "Это снимает смещение от ненаблюдаемых постоянных различий, но и лишает " +
                "возможности оценить эффект любой неизменной характеристики.")
            .FindingIf(Estimator == PanelEstimator.RandomEffects,
                $"Случайные эффекты используют и межгрупповую, и внутригрупповую вариацию " +
                $"(тета = {Fmt.Num(Theta, 3)}). Оценки эффективнее, но состоятельны только " +
                "если эффекты не коррелированы с регрессорами.")
            .FindingIf(effectsMatter,
                $"Индивидуальные эффекты объясняют {Fmt.Pct(Rho, 1)} остаточной дисперсии. " +
                "Объединённый МНК на таких данных даст заниженные стандартные ошибки.")
            .WarningIf(Periods < 3,
                $"Периодов всего {Periods}. Панельные методы требуют вариации во времени; " +
                "при двух периодах фиксированные эффекты вырождаются в первые разности.")
            .WarningIf(Estimator == PanelEstimator.RandomEffects,
                "Случайные эффекты состоятельны только при экзогенности индивидуальных " +
                "эффектов. Это проверяется тестом Хаусмана, и без него выбор оценщика " +
                "остаётся необоснованным.")
            .Warning("Стандартные ошибки следует кластеризовать по объектам: наблюдения " +
                     "одного объекта коррелированы во времени, и без кластеризации " +
                     "значимость систематически завышается.")
            .Recommendation("Оцените обе модели и сравните тестом Хаусмана: это стандартный " +
                            "порядок выбора между фиксированными и случайными эффектами.")
            .Recommendation("Если интересен эффект постоянной характеристики, фиксированные " +
                            "эффекты его не дадут — нужен либо межгрупповой оценщик, " +
                            "либо иерархическая модель.")
            .Build();
    }

    /// <summary>Читаемое название оценщика.</summary>
    private string EstimatorName() => Estimator switch
    {
        PanelEstimator.Pooled => "объединённый МНК",
        PanelEstimator.FixedEffects => "фиксированные эффекты",
        PanelEstimator.TwoWayFixedEffects => "двусторонние фиксированные эффекты",
        PanelEstimator.RandomEffects => "случайные эффекты",
        PanelEstimator.FirstDifference => "первые разности",
        _ => "межгрупповая регрессия",
    };
}

/// <summary>Результат теста Хаусмана на выбор между оценщиками.</summary>
public sealed record HausmanResult : IInterpretable
{
    /// <summary>Статистика теста.</summary>
    public double Statistic { get; init; }

    /// <summary>Число степеней свободы.</summary>
    public int DegreesOfFreedom { get; init; }

    /// <summary>Уровень значимости.</summary>
    public double PValue { get; init; } = 1;

    /// <summary>Расхождения оценок по коэффициентам.</summary>
    public IReadOnlyList<(string Variable, double Fixed, double Random, double Difference)> Differences { get; init; } = [];

    /// <summary>Отвергается ли экзогенность индивидуальных эффектов.</summary>
    public bool PrefersFixedEffects => PValue < 0.05;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        var largest = Differences.OrderByDescending(d => Math.Abs(d.Difference)).FirstOrDefault();

        return new InterpretationBuilder("Тест Хаусмана")
            .Summary($"Статистика {Fmt.Num(Statistic, 3)} при {DegreesOfFreedom} степенях свободы, " +
                     $"p = {Fmt.Num(PValue, 4)}. " +
                     (PrefersFixedEffects
                         ? "Экзогенность индивидуальных эффектов отвергается: нужны фиксированные эффекты."
                         : "Экзогенность не отвергается: случайные эффекты допустимы и эффективнее."))
            .Metric("Статистика", Statistic, null, $"{DegreesOfFreedom} степеней свободы",
                MetricQuality.Neutral, 3)
            .Metric("p-значение", PValue, null,
                PrefersFixedEffects ? "случайные эффекты несостоятельны" : "случайные эффекты допустимы",
                PrefersFixedEffects ? MetricQuality.Warning : MetricQuality.Good, 4)
            .Metric("Наибольшее расхождение", largest.Difference, null,
                $"по коэффициенту «{largest.Variable}»", MetricQuality.Neutral, 4)
            .Finding("Тест сравнивает две оценки: состоятельную при любых предпосылках " +
                     "(фиксированные эффекты) и эффективную только при экзогенности " +
                     "эффектов (случайные). Систематическое расхождение означает, что " +
                     "вторая предпосылка нарушена.")
            .FindingIf(!PrefersFixedEffects,
                "Непринятие гипотезы не доказывает экзогенность: тест может не иметь " +
                "мощности при малом числе объектов или слабой вариации регрессоров.")
            .WarningIf(Statistic < 0,
                "Отрицательная статистика означает, что разность ковариационных матриц " +
                "не положительно определена. Обычно это следствие малой выборки; " +
                "результат теста в таком случае неинформативен.")
            .Warning("Классический тест Хаусмана предполагает эффективность оценки со " +
                     "случайными эффектами. При гетероскедастичности или кластеризации " +
                     "предпосылка нарушается, и корректнее использовать его робастную версию.")
            .Recommendation("Смотрите не только на p-значение, но и на величину расхождения " +
                            "коэффициентов: экономически незначимое расхождение может быть " +
                            "статистически значимым на большой панели.")
            .Build();
    }
}

/// <summary>
/// Оценивание панельных данных: фиксированные и случайные эффекты, первые
/// разности, межгрупповая регрессия и тест Хаусмана.
/// </summary>
/// <remarks>
/// <para>
/// Панельные данные позволяют убрать ненаблюдаемые различия между объектами,
/// постоянные во времени. Внутригрупповое преобразование вычитает средние по
/// объекту:
/// </para>
/// <code>
/// y_it - mean_i(y) = (x_it - mean_i(x))' beta + (e_it - mean_i(e))
/// </code>
/// <para>
/// Индивидуальный эффект исчезает, а вместе с ним и смещение от корреляции
/// этого эффекта с регрессорами. Цена — потеря всей межгрупповой вариации:
/// эффект характеристики, постоянной во времени, оценить невозможно.
/// </para>
/// <para>
/// Случайные эффекты сохраняют часть межгрупповой вариации, вычитая долю
/// группового среднего:
/// </para>
/// <code>
/// theta = 1 - sqrt(sigma_e^2 / (T * sigma_u^2 + sigma_e^2))
/// </code>
/// <para>
/// Оценка эффективнее, но состоятельна лишь при экзогенности эффектов.
/// Выбор между двумя подходами делается тестом Хаусмана.
/// </para>
/// </remarks>
public static class PanelData
{
    /// <summary>Оценивает панельную модель выбранным способом.</summary>
    /// <param name="dataset">Панельные данные.</param>
    /// <param name="estimator">Способ учёта эффектов.</param>
    /// <param name="clusterByUnit">Кластеризовать ли стандартные ошибки по объектам.</param>
    /// <returns>Коэффициенты и разложение дисперсии.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы.</exception>
    public static PanelResult Fit(
        PanelDataset dataset,
        PanelEstimator estimator = PanelEstimator.FixedEffects,
        bool clusterByUnit = true)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        dataset.Validate();

        return estimator switch
        {
            PanelEstimator.Pooled => FitPooled(dataset, clusterByUnit),
            PanelEstimator.FirstDifference => FitDifference(dataset, clusterByUnit),
            PanelEstimator.BetweenEffects => FitBetween(dataset),
            PanelEstimator.RandomEffects => FitRandom(dataset, clusterByUnit),
            PanelEstimator.TwoWayFixedEffects => FitWithin(dataset, clusterByUnit, twoWay: true),
            _ => FitWithin(dataset, clusterByUnit, twoWay: false),
        };
    }

    /// <summary>Сравнивает фиксированные и случайные эффекты тестом Хаусмана.</summary>
    /// <param name="fixedEffects">Оценка с фиксированными эффектами.</param>
    /// <param name="randomEffects">Оценка со случайными эффектами.</param>
    /// <returns>Статистика теста и разбор расхождений.</returns>
    /// <exception cref="ArgumentNullException">Оценки не заданы.</exception>
    public static HausmanResult Hausman(PanelResult fixedEffects, PanelResult randomEffects)
    {
        ArgumentNullException.ThrowIfNull(fixedEffects);
        ArgumentNullException.ThrowIfNull(randomEffects);

        double statistic = 0;
        int df = 0;
        var differences = new List<(string, double, double, double)>();

        foreach (Coefficient fe in fixedEffects.Coefficients)
        {
            if (fe.Name == "const") continue;

            Coefficient? re = randomEffects.Coefficients.FirstOrDefault(c => c.Name == fe.Name);
            if (re is null) continue;

            double difference = fe.Estimate - re.Estimate;
            differences.Add((fe.Name, fe.Estimate, re.Estimate, difference));

            double variance = (fe.StandardError * fe.StandardError)
                - (re.StandardError * re.StandardError);

            if (variance <= 1e-18) continue;

            statistic += difference * difference / variance;
            df++;
        }

        return new HausmanResult
        {
            Statistic = statistic,
            DegreesOfFreedom = df,
            PValue = df > 0 ? Distributions.ChiSquarePValue(statistic, df) : 1,
            Differences = differences,
        };
    }

    /// <summary>Объединённый МНК без учёта эффектов.</summary>
    private static PanelResult FitPooled(PanelDataset dataset, bool cluster)
    {
        RegressionResult fit = LinearRegression.Fit(
            dataset.Regressors, dataset.Response, dataset.Names, Options(dataset, cluster));

        (double sigmaUnit, double sigmaError) = VarianceComponents(dataset, fit.Residuals);

        return new PanelResult
        {
            Estimator = PanelEstimator.Pooled,
            Coefficients = fit.Coefficients,
            RSquared = fit.RSquared,
            Units = dataset.UnitCount,
            Periods = dataset.PeriodCount,
            Observations = dataset.Observations,
            SigmaUnit = sigmaUnit,
            SigmaError = sigmaError,
            Residuals = fit.Residuals,
        };
    }

    /// <summary>Внутригрупповое преобразование.</summary>
    private static PanelResult FitWithin(PanelDataset dataset, bool cluster, bool twoWay)
    {
        int n = dataset.Observations, k = dataset.Regressors.Width;

        var x = new Matrix(n, k);
        var y = new Vector(n);

        Demean(dataset.Regressors, dataset.Response, dataset.Units, x, y);

        if (twoWay)
        {
            var x2 = new Matrix(n, k);
            var y2 = new Vector(n);
            Demean(x, y, dataset.Periods, x2, y2);

            // Двойное вычитание убирает общее среднее дважды: возвращаем его
            double meanY = dataset.Response.Average();
            for (int i = 0; i < n; i++)
            {
                y2[i] += meanY;
                y[i] = y2[i];
                for (int j = 0; j < k; j++) x[i, j] = x2[i, j];
            }
        }

        var options = Options(dataset, cluster) with { AddIntercept = false };
        RegressionResult fit = LinearRegression.Fit(x, y, dataset.Names, options);

        (double sigmaUnit, double sigmaError) = VarianceComponents(dataset, fit.Residuals);

        return new PanelResult
        {
            Estimator = twoWay ? PanelEstimator.TwoWayFixedEffects : PanelEstimator.FixedEffects,
            Coefficients = fit.Coefficients,
            RSquared = fit.RSquared,
            Units = dataset.UnitCount,
            Periods = dataset.PeriodCount,
            Observations = n,
            SigmaUnit = sigmaUnit,
            SigmaError = sigmaError,
            Residuals = fit.Residuals,
        };
    }

    /// <summary>Модель случайных эффектов.</summary>
    private static PanelResult FitRandom(PanelDataset dataset, bool cluster)
    {
        PanelResult within = FitWithin(dataset, cluster, twoWay: false);
        PanelResult between = FitBetween(dataset);

        double sigmaError = within.SigmaError;
        double periods = Math.Max(1.0, (double)dataset.Observations / Math.Max(1, dataset.UnitCount));

        double sigmaUnitSquared = Math.Max(
            (between.SigmaError * between.SigmaError) - (sigmaError * sigmaError / periods), 0);

        double theta = sigmaError > 0
            ? 1 - Math.Sqrt(sigmaError * sigmaError / ((periods * sigmaUnitSquared) + (sigmaError * sigmaError)))
            : 0;

        int n = dataset.Observations, k = dataset.Regressors.Width;
        var x = new Matrix(n, k + 1);
        var y = new Vector(n);

        var unitMeansX = GroupMeans(dataset.Regressors, dataset.Units);
        var unitMeansY = GroupMeans(dataset.Response, dataset.Units);

        for (int i = 0; i < n; i++)
        {
            int unit = dataset.Units[i];
            x[i, 0] = 1 - theta;
            for (int j = 0; j < k; j++) x[i, j + 1] = dataset.Regressors[i, j] - (theta * unitMeansX[unit][j]);
            y[i] = dataset.Response[i] - (theta * unitMeansY[unit]);
        }

        var names = new List<string> { "const" };
        for (int j = 0; j < k; j++)
            names.Add(j < dataset.Names.Count ? dataset.Names[j] : $"x{j + 1}");

        var options = Options(dataset, cluster) with { AddIntercept = false };
        RegressionResult fit = LinearRegression.Fit(x, y, names, options);

        return new PanelResult
        {
            Estimator = PanelEstimator.RandomEffects,
            Coefficients = fit.Coefficients,
            RSquared = fit.RSquared,
            Units = dataset.UnitCount,
            Periods = dataset.PeriodCount,
            Observations = n,
            SigmaUnit = Math.Sqrt(sigmaUnitSquared),
            SigmaError = sigmaError,
            Theta = theta,
            Residuals = fit.Residuals,
        };
    }

    /// <summary>Регрессия первых разностей внутри объектов.</summary>
    private static PanelResult FitDifference(PanelDataset dataset, bool cluster)
    {
        var rows = Enumerable.Range(0, dataset.Observations)
            .OrderBy(i => dataset.Units[i])
            .ThenBy(i => dataset.Periods[i])
            .ToList();

        var xs = new List<double[]>();
        var ys = new List<double>();
        var units = new List<int>();

        for (int position = 1; position < rows.Count; position++)
        {
            int current = rows[position], previous = rows[position - 1];
            if (dataset.Units[current] != dataset.Units[previous]) continue;

            var row = new double[dataset.Regressors.Width];
            for (int j = 0; j < row.Length; j++)
                row[j] = dataset.Regressors[current, j] - dataset.Regressors[previous, j];

            xs.Add(row);
            ys.Add(dataset.Response[current] - dataset.Response[previous]);
            units.Add(dataset.Units[current]);
        }

        if (xs.Count <= dataset.Regressors.Width)
            throw new ArgumentException("После взятия разностей наблюдений не осталось.", nameof(dataset));

        var x = new Matrix(xs.Count, dataset.Regressors.Width);
        var y = new Vector(xs.Count);

        for (int i = 0; i < xs.Count; i++)
        {
            for (int j = 0; j < dataset.Regressors.Width; j++) x[i, j] = xs[i][j];
            y[i] = ys[i];
        }

        var options = new RegressionOptions
        {
            AddIntercept = false,
            Variance = cluster ? RobustVariance.Clustered : RobustVariance.Hc1,
            Clusters = cluster ? units : null,
        };

        RegressionResult fit = LinearRegression.Fit(x, y, dataset.Names, options);

        return new PanelResult
        {
            Estimator = PanelEstimator.FirstDifference,
            Coefficients = fit.Coefficients,
            RSquared = fit.RSquared,
            Units = dataset.UnitCount,
            Periods = dataset.PeriodCount,
            Observations = xs.Count,
            SigmaError = fit.Sigma,
            Residuals = fit.Residuals,
        };
    }

    /// <summary>Межгрупповая регрессия на средних по объектам.</summary>
    private static PanelResult FitBetween(PanelDataset dataset)
    {
        var unitMeansX = GroupMeans(dataset.Regressors, dataset.Units);
        var unitMeansY = GroupMeans(dataset.Response, dataset.Units);

        var keys = unitMeansY.Keys.OrderBy(u => u).ToList();
        var x = new Matrix(keys.Count, dataset.Regressors.Width);
        var y = new Vector(keys.Count);

        for (int i = 0; i < keys.Count; i++)
        {
            for (int j = 0; j < dataset.Regressors.Width; j++) x[i, j] = unitMeansX[keys[i]][j];
            y[i] = unitMeansY[keys[i]];
        }

        if (keys.Count <= dataset.Regressors.Width + 1)
            throw new ArgumentException("Объектов недостаточно для межгрупповой регрессии.", nameof(dataset));

        RegressionResult fit = LinearRegression.Fit(x, y, dataset.Names);

        return new PanelResult
        {
            Estimator = PanelEstimator.BetweenEffects,
            Coefficients = fit.Coefficients,
            RSquared = fit.RSquared,
            Units = keys.Count,
            Periods = dataset.PeriodCount,
            Observations = keys.Count,
            SigmaError = fit.Sigma,
            Residuals = fit.Residuals,
        };
    }

    /// <summary>Настройки регрессии с кластеризацией по объектам.</summary>
    private static RegressionOptions Options(PanelDataset dataset, bool cluster) => new()
    {
        Variance = cluster ? RobustVariance.Clustered : RobustVariance.Hc1,
        Clusters = cluster ? dataset.Units : null,
    };

    /// <summary>Вычитает групповые средние из регрессоров и отклика.</summary>
    private static void Demean(
        Matrix source, Vector response, IReadOnlyList<int> groups, Matrix targetX, Vector targetY)
    {
        var meansX = GroupMeans(source, groups);
        var meansY = GroupMeans(response, groups);

        for (int i = 0; i < response.Count; i++)
        {
            int group = groups[i];
            for (int j = 0; j < source.Width; j++) targetX[i, j] = source[i, j] - meansX[group][j];
            targetY[i] = response[i] - meansY[group];
        }
    }

    /// <summary>Средние регрессоров по группам.</summary>
    private static Dictionary<int, double[]> GroupMeans(Matrix source, IReadOnlyList<int> groups)
    {
        var sums = new Dictionary<int, double[]>();
        var counts = new Dictionary<int, int>();

        for (int i = 0; i < groups.Count; i++)
        {
            int group = groups[i];
            if (!sums.TryGetValue(group, out double[]? sum))
            {
                sum = new double[source.Width];
                sums[group] = sum;
                counts[group] = 0;
            }

            for (int j = 0; j < source.Width; j++) sum[j] += source[i, j];
            counts[group]++;
        }

        foreach (int group in sums.Keys.ToList())
            for (int j = 0; j < source.Width; j++) sums[group][j] /= counts[group];

        return sums;
    }

    /// <summary>Средние отклика по группам.</summary>
    private static Dictionary<int, double> GroupMeans(Vector response, IReadOnlyList<int> groups)
    {
        var sums = new Dictionary<int, double>();
        var counts = new Dictionary<int, int>();

        for (int i = 0; i < groups.Count; i++)
        {
            int group = groups[i];
            sums.TryAdd(group, 0);
            counts.TryAdd(group, 0);
            sums[group] += response[i];
            counts[group]++;
        }

        foreach (int group in sums.Keys.ToList()) sums[group] /= counts[group];
        return sums;
    }

    /// <summary>Разложение дисперсии остатков на межгрупповую и внутригрупповую части.</summary>
    private static (double SigmaUnit, double SigmaError) VarianceComponents(
        PanelDataset dataset, Vector residuals)
    {
        var means = new Dictionary<int, double>();
        var counts = new Dictionary<int, int>();

        for (int i = 0; i < residuals.Count; i++)
        {
            int unit = dataset.Units[i];
            means.TryAdd(unit, 0);
            counts.TryAdd(unit, 0);
            means[unit] += residuals[i];
            counts[unit]++;
        }

        foreach (int unit in means.Keys.ToList()) means[unit] /= counts[unit];

        double within = 0, between = 0;
        double overall = means.Values.Average();

        for (int i = 0; i < residuals.Count; i++)
        {
            double deviation = residuals[i] - means[dataset.Units[i]];
            within += deviation * deviation;
        }

        foreach (int unit in means.Keys)
        {
            double deviation = means[unit] - overall;
            between += deviation * deviation;
        }

        double sigmaError = Math.Sqrt(within / Math.Max(1, residuals.Count - means.Count));
        double sigmaUnit = Math.Sqrt(between / Math.Max(1, means.Count - 1));

        return (sigmaUnit, sigmaError);
    }
}
