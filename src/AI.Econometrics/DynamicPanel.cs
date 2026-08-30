using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Econometrics.Numerics;

namespace AI.Econometrics;

/// <summary>Результат оценивания динамической панели.</summary>
public sealed record DynamicPanelResult : IInterpretable
{
    /// <summary>Оценки коэффициентов уравнения в разностях.</summary>
    public IReadOnlyList<Coefficient> Coefficients { get; init; } = [];

    /// <summary>Оценка коэффициента при лаге отклика.</summary>
    public double Persistence { get; init; }

    /// <summary>Оценка того же коэффициента объединённым МНК: верхняя граница.</summary>
    public double PooledPersistence { get; init; }

    /// <summary>Оценка с фиксированными эффектами: нижняя граница.</summary>
    public double WithinPersistence { get; init; }

    /// <summary>Статистика Саргана на валидность инструментов.</summary>
    public double SarganStatistic { get; init; }

    /// <summary>Уровень значимости теста Саргана.</summary>
    public double SarganPValue { get; init; } = 1;

    /// <summary>Статистика теста на автокорреляцию второго порядка в разностях.</summary>
    public double ArellanoBondAr2 { get; init; }

    /// <summary>Уровень значимости теста на автокорреляцию второго порядка.</summary>
    public double Ar2PValue { get; init; } = 1;

    /// <summary>Число использованных инструментов.</summary>
    public int Instruments { get; init; }

    /// <summary>Число объектов.</summary>
    public int Units { get; init; }

    /// <summary>Число уравнений в разностях.</summary>
    public int Observations { get; init; }

    /// <summary>Долгосрочный множитель по первому регрессору, если он есть.</summary>
    public double LongRunMultiplier =>
        Math.Abs(1 - Persistence) > 1e-9 && Coefficients.Count > 1
            ? Coefficients[1].Estimate / (1 - Persistence)
            : 0;

    /// <summary>Лежит ли оценка инерции между границами объединённого и внутригруппового оценщиков.</summary>
    public bool IsInBounds =>
        Persistence >= WithinPersistence - 0.02 && Persistence <= PooledPersistence + 0.02;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double halfLife = Persistence > 0 && Persistence < 1
            ? Math.Log(0.5) / Math.Log(Persistence)
            : double.NaN;

        return new InterpretationBuilder("Динамическая панель: Ареллано — Бонд")
            .Summary($"Оценено {Observations} уравнений в разностях по {Units} объектам с " +
                     $"{Instruments} инструментами. Коэффициент инерции {Fmt.Num(Persistence, 3)} " +
                     $"при границах {Fmt.Num(WithinPersistence, 3)} (фиксированные эффекты) и " +
                     $"{Fmt.Num(PooledPersistence, 3)} (объединённый МНК). " +
                     $"Сарган p = {Fmt.Num(SarganPValue, 4)}, AR(2) p = {Fmt.Num(Ar2PValue, 4)}.")
            .Metric("Инерция", Persistence, null,
                double.IsNaN(halfLife) ? "процесс не затухает" : $"период полураспада {Fmt.Num(halfLife, 1)}",
                Persistence < 1 ? MetricQuality.Good : MetricQuality.Critical, 4)
            .Metric("Граница снизу", WithinPersistence, null,
                "оценка с фиксированными эффектами смещена вниз", MetricQuality.Neutral, 4)
            .Metric("Граница сверху", PooledPersistence, null,
                "объединённый МНК смещён вверх", MetricQuality.Neutral, 4)
            .Metric("Сарган", SarganStatistic, null,
                $"p = {Fmt.Num(SarganPValue, 4)}; проверка валидности инструментов",
                SarganPValue < 0.05 ? MetricQuality.Warning : MetricQuality.Good, 3)
            .Metric("AR(2)", ArellanoBondAr2, null,
                $"p = {Fmt.Num(Ar2PValue, 4)}; автокорреляция второго порядка в разностях",
                Ar2PValue < 0.05 ? MetricQuality.Critical : MetricQuality.Good, 3)
            .Metric("Инструментов", Instruments, null,
                $"на {Units} объектов", Instruments > Units ? MetricQuality.Warning : MetricQuality.Good, 0)
            .Finding("Оценка с фиксированными эффектами в динамической модели смещена вниз " +
                     "(смещение Никелла), объединённый МНК — вверх. Корректная оценка обязана " +
                     "лежать между ними: это первая и самая простая проверка результата.")
            .FindingIf(IsInBounds,
                "Оценка попадает в интервал между двумя смещёнными границами — необходимое " +
                "условие адекватности выполнено.")
            .FindingIf(!double.IsNaN(halfLife),
                $"Отклонение от долгосрочного уровня уменьшается вдвое за " +
                $"{Fmt.Num(halfLife, 1)} периода.")
            .WarningIf(!IsInBounds,
                "Оценка вышла за границы объединённого и внутригруппового оценщиков. " +
                "Обычно это признак слабых инструментов или неверной спецификации лагов.")
            .WarningIf(Ar2PValue < 0.05,
                "Автокорреляция второго порядка в разностях отвергает валидность лаговых " +
                "инструментов: моментные условия нарушены, оценка несостоятельна.")
            .WarningIf(Instruments > Units,
                $"Инструментов ({Instruments}) больше, чем объектов ({Units}). " +
                "Разрастание набора инструментов делает тест Саргана неинформативным " +
                "и смещает оценку к объединённому МНК.")
            .Warning("Тест AR(2) реализован в упрощённом виде — без поправки на оценённые " +
                     "параметры. На малых панелях он консервативен: настоящее p-значение " +
                     "может быть ниже расчётного.")
            .Recommendation("Ограничивайте глубину лагов в инструментах. Полный набор " +
                            "растёт квадратично по числу периодов и переопределяет модель.")
            .Recommendation("Всегда приводите три оценки рядом — объединённую, внутригрупповую " +
                            "и динамическую: их взаимное расположение и есть аргумент " +
                            "в пользу последней.")
            .Build();
    }
}

/// <summary>
/// Динамические панели: разностный обобщённый метод моментов Ареллано — Бонда.
/// </summary>
/// <remarks>
/// <para>
/// В модели с лагом отклика
/// </para>
/// <code>
/// y_it = rho * y_i,t-1 + x_it' beta + alpha_i + e_it
/// </code>
/// <para>
/// оба классических оценщика смещены. Объединённый МНК приписывает лагу часть
/// индивидуального эффекта и завышает инерцию; внутригрупповое преобразование
/// создаёт корреляцию между преобразованным лагом и преобразованной ошибкой и
/// занижает её (смещение Никелла порядка <c>1/T</c>).
/// </para>
/// <para>
/// Ареллано и Бонд предлагают взять первые разности, что убирает эффект, и
/// инструментировать разность лага уровнями достаточной глубины:
/// </para>
/// <code>
/// d y_it = rho * d y_i,t-1 + d x_it' beta + d e_it
/// инструменты для уравнения периода t: y_i1, ..., y_i,t-2
/// </code>
/// <para>
/// Валидность инструментов опирается на отсутствие автокорреляции исходной
/// ошибки: в разностях допускается AR(1), но не AR(2). Поэтому тест на
/// автокорреляцию второго порядка — не формальность, а прямая проверка
/// идентифицирующего предположения.
/// </para>
/// </remarks>
public static class DynamicPanel
{
    /// <summary>Оценивает динамическую панель разностным методом моментов.</summary>
    /// <param name="dataset">Панельные данные; отклик задаётся в уровнях.</param>
    /// <param name="maxLags">Максимальная глубина лагов в инструментах; ноль — без ограничения.</param>
    /// <returns>Коэффициенты, границы смещения и тесты валидности.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Периодов недостаточно для разностного метода.</exception>
    public static DynamicPanelResult ArellanoBond(PanelDataset dataset, int maxLags = 3)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        dataset.Validate();

        var units = BuildUnits(dataset);

        if (units.Count == 0 || units.Values.All(u => u.Count < 4))
            throw new ArgumentException(
                "Для разностного метода нужно минимум четыре периода на объект.", nameof(dataset));

        int exogenousCount = dataset.Regressors.Width;
        int parameters = 1 + exogenousCount;

        // Максимальная глубина инструментов задаёт ширину блока
        int maxPeriods = units.Values.Max(u => u.Count);
        int instrumentColumns = 0;
        for (int t = 2; t < maxPeriods; t++)
        {
            int available = maxLags > 0 ? Math.Min(maxLags, t - 1) : t - 1;
            instrumentColumns += available;
        }

        instrumentColumns += exogenousCount;

        var xRows = new List<double[]>();
        var zRows = new List<double[]>();
        var yValues = new List<double>();
        var equationUnit = new List<int>();
        var equationTime = new List<int>();

        foreach ((int unit, List<PanelPoint> series) in units)
        {
            for (int t = 2; t < series.Count; t++)
            {
                var regressors = new double[parameters];
                regressors[0] = series[t - 1].Y - series[t - 2].Y;

                for (int j = 0; j < exogenousCount; j++)
                    regressors[1 + j] = series[t].X[j] - series[t - 1].X[j];

                var instruments = new double[instrumentColumns];
                int offset = 0;

                for (int s = 2; s < maxPeriods; s++)
                {
                    int available = maxLags > 0 ? Math.Min(maxLags, s - 1) : s - 1;

                    if (s == t)
                    {
                        for (int lag = 0; lag < available; lag++)
                        {
                            int index = t - 2 - lag;
                            if (index >= 0) instruments[offset + lag] = series[index].Y;
                        }
                    }

                    offset += available;
                }

                // Разности экзогенных регрессоров инструментируют сами себя
                for (int j = 0; j < exogenousCount; j++)
                    instruments[offset + j] = series[t].X[j] - series[t - 1].X[j];

                xRows.Add(regressors);
                zRows.Add(instruments);
                yValues.Add(series[t].Y - series[t - 1].Y);
                equationUnit.Add(unit);
                equationTime.Add(t);
            }
        }

        if (xRows.Count <= parameters)
            throw new ArgumentException("После разностей уравнений не осталось.", nameof(dataset));

        int n = xRows.Count;
        var x = new double[n, parameters];
        var z = new double[n, instrumentColumns];
        var y = new double[n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < parameters; j++) x[i, j] = xRows[i][j];
            for (int j = 0; j < instrumentColumns; j++) z[i, j] = zRows[i][j];
            y[i] = yValues[i];
        }

        double[,] weight = OneStepWeight(z, equationUnit, equationTime);
        double[,] weightInverse = EconMath.Inverse(weight)
            ?? throw new ArgumentException("Матрица инструментов вырождена.", nameof(dataset));

        double[,] zt = LinearAlgebra.Transpose(z);
        double[,] ztx = LinearAlgebra.Multiply(zt, x);
        double[] zty = LinearAlgebra.Multiply(zt, y);
        double[,] xtz = LinearAlgebra.Transpose(ztx);

        double[,] left = LinearAlgebra.Multiply(xtz, weightInverse);
        double[,] bread = LinearAlgebra.Multiply(left, ztx);
        double[,] breadInverse = EconMath.Inverse(bread)
            ?? throw new ArgumentException("Модель не идентифицирована.", nameof(dataset));

        double[] beta = LinearAlgebra.Multiply(breadInverse, LinearAlgebra.Multiply(left, zty));

        var residuals = new double[n];
        for (int i = 0; i < n; i++)
        {
            double prediction = 0;
            for (int j = 0; j < parameters; j++) prediction += x[i, j] * beta[j];
            residuals[i] = y[i] - prediction;
        }

        double[,] covariance = RobustCovariance(
            x, z, residuals, equationUnit, weightInverse, breadInverse);

        var names = new List<string> { "лаг отклика" };
        for (int j = 0; j < exogenousCount; j++)
            names.Add(j < dataset.Names.Count ? dataset.Names[j] : $"x{j + 1}");

        var coefficients = new List<Coefficient>(parameters);
        for (int j = 0; j < parameters; j++)
        {
            double error = Math.Sqrt(Math.Max(covariance[j, j], 0));
            double t = error > 0 ? beta[j] / error : 0;
            double p = Distributions.NormalPValue(t);

            coefficients.Add(new Coefficient(
                names[j], beta[j], error, t, p,
                beta[j] - (1.96 * error), beta[j] + (1.96 * error)));
        }

        (double sargan, double sarganP) = Sargan(z, residuals, weightInverse, instrumentColumns - parameters);
        (double ar2, double ar2P) = AutocorrelationTest(residuals, equationUnit, equationTime, 2);

        (double pooled, double within) = Bounds(dataset, units);

        return new DynamicPanelResult
        {
            Coefficients = coefficients,
            Persistence = beta[0],
            PooledPersistence = pooled,
            WithinPersistence = within,
            SarganStatistic = sargan,
            SarganPValue = sarganP,
            ArellanoBondAr2 = ar2,
            Ar2PValue = ar2P,
            Instruments = instrumentColumns,
            Units = units.Count,
            Observations = n,
        };
    }

    /// <summary>Наблюдение панели, упорядоченное по времени.</summary>
    private sealed record PanelPoint(int Period, double Y, double[] X);

    /// <summary>Раскладывает панель по объектам с сортировкой по периодам.</summary>
    private static Dictionary<int, List<PanelPoint>> BuildUnits(PanelDataset dataset)
    {
        var units = new Dictionary<int, List<PanelPoint>>();

        for (int i = 0; i < dataset.Observations; i++)
        {
            var row = new double[dataset.Regressors.Width];
            for (int j = 0; j < row.Length; j++) row[j] = dataset.Regressors[i, j];

            int unit = dataset.Units[i];
            if (!units.TryGetValue(unit, out List<PanelPoint>? series))
            {
                series = [];
                units[unit] = series;
            }

            series.Add(new PanelPoint(dataset.Periods[i], dataset.Response[i], row));
        }

        foreach (int unit in units.Keys.ToList())
            units[unit] = [.. units[unit].OrderBy(p => p.Period)];

        return units;
    }

    /// <summary>Одношаговая весовая матрица с блоками для разностей.</summary>
    private static double[,] OneStepWeight(
        double[,] z, IReadOnlyList<int> equationUnit, IReadOnlyList<int> equationTime)
    {
        int n = z.GetLength(0), m = z.GetLength(1);
        var weight = new double[m, m];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (equationUnit[i] != equationUnit[j]) continue;

                int gap = Math.Abs(equationTime[i] - equationTime[j]);

                // Ковариация разностей белого шума: 2 на диагонали, -1 у соседей
                double h = gap == 0 ? 2 : gap == 1 ? -1 : 0;
                if (h == 0) continue;

                for (int a = 0; a < m; a++)
                    for (int b = 0; b < m; b++) weight[a, b] += h * z[i, a] * z[j, b];
            }
        }

        for (int a = 0; a < m; a++) weight[a, a] += 1e-8;
        return weight;
    }

    /// <summary>Устойчивая ковариационная матрица оценок.</summary>
    private static double[,] RobustCovariance(
        double[,] x, double[,] z, double[] residuals, IReadOnlyList<int> equationUnit,
        double[,] weightInverse, double[,] breadInverse)
    {
        int n = z.GetLength(0), m = z.GetLength(1);
        var meat = new double[m, m];
        var groups = new Dictionary<int, double[]>();

        for (int i = 0; i < n; i++)
        {
            int unit = equationUnit[i];
            if (!groups.TryGetValue(unit, out double[]? sum))
            {
                sum = new double[m];
                groups[unit] = sum;
            }

            for (int a = 0; a < m; a++) sum[a] += z[i, a] * residuals[i];
        }

        foreach (double[] sum in groups.Values)
            for (int a = 0; a < m; a++)
                for (int b = 0; b < m; b++) meat[a, b] += sum[a] * sum[b];

        double[,] middle = LinearAlgebra.Multiply(LinearAlgebra.Multiply(weightInverse, meat), weightInverse);
        double[,] zt = LinearAlgebra.Transpose(z);
        double[,] ztx = LinearAlgebra.Multiply(zt, x);
        double[,] xtz = LinearAlgebra.Transpose(ztx);

        double[,] sandwich = LinearAlgebra.Multiply(
            LinearAlgebra.Multiply(LinearAlgebra.Multiply(breadInverse, xtz), middle),
            LinearAlgebra.Multiply(ztx, breadInverse));

        return sandwich;
    }

    /// <summary>Тест Саргана на сверхидентифицирующие ограничения.</summary>
    private static (double Statistic, double PValue) Sargan(
        double[,] z, double[] residuals, double[,] weightInverse, int restrictions)
    {
        if (restrictions <= 0) return (0, 1);

        int n = z.GetLength(0), m = z.GetLength(1);
        var moments = new double[m];

        for (int a = 0; a < m; a++)
            for (int i = 0; i < n; i++) moments[a] += z[i, a] * residuals[i];

        double variance = residuals.Sum(e => e * e) / Math.Max(1, n);
        double statistic = LinearAlgebra.QuadraticForm(moments, weightInverse) / Math.Max(variance, 1e-300);

        return (statistic, Distributions.ChiSquarePValue(statistic, restrictions));
    }

    /// <summary>
    /// Упрощённый тест Ареллано — Бонда на автокорреляцию порядка <paramref name="order"/>.
    /// </summary>
    /// <remarks>
    /// Поправка на оценённые параметры не вносится, поэтому тест консервативен:
    /// настоящее p-значение может быть ниже расчётного.
    /// </remarks>
    private static (double Statistic, double PValue) AutocorrelationTest(
        double[] residuals, IReadOnlyList<int> equationUnit, IReadOnlyList<int> equationTime, int order)
    {
        double numerator = 0, denominator = 0;

        for (int i = 0; i < residuals.Length; i++)
        {
            for (int j = 0; j < residuals.Length; j++)
            {
                if (equationUnit[i] != equationUnit[j]) continue;
                if (equationTime[i] - equationTime[j] != order) continue;

                double product = residuals[i] * residuals[j];
                numerator += product;
                denominator += product * product;
            }
        }

        if (denominator <= 0) return (0, 1);

        double statistic = numerator / Math.Sqrt(denominator);
        return (statistic, Distributions.NormalPValue(statistic));
    }

    /// <summary>Оценки инерции объединённым МНК и с фиксированными эффектами.</summary>
    private static (double Pooled, double Within) Bounds(
        PanelDataset dataset, Dictionary<int, List<PanelPoint>> units)
    {
        var xs = new List<double[]>();
        var ys = new List<double>();
        var unitIds = new List<int>();
        var periods = new List<int>();

        foreach ((int unit, List<PanelPoint> series) in units)
        {
            for (int t = 1; t < series.Count; t++)
            {
                var row = new double[1 + dataset.Regressors.Width];
                row[0] = series[t - 1].Y;
                for (int j = 0; j < dataset.Regressors.Width; j++) row[1 + j] = series[t].X[j];

                xs.Add(row);
                ys.Add(series[t].Y);
                unitIds.Add(unit);
                periods.Add(series[t].Period);
            }
        }

        var x = new Matrix(xs.Count, xs[0].Length);
        var y = new Vector(xs.Count);

        for (int i = 0; i < xs.Count; i++)
        {
            for (int j = 0; j < xs[0].Length; j++) x[i, j] = xs[i][j];
            y[i] = ys[i];
        }

        var names = new List<string> { "лаг отклика" };
        for (int j = 0; j < dataset.Regressors.Width; j++)
            names.Add(j < dataset.Names.Count ? dataset.Names[j] : $"x{j + 1}");

        var lagged = new PanelDataset
        {
            Regressors = x,
            Response = y,
            Units = unitIds,
            Periods = periods,
            Names = names,
        };

        PanelResult pooled = PanelData.Fit(lagged, PanelEstimator.Pooled);
        PanelResult within = PanelData.Fit(lagged, PanelEstimator.FixedEffects);

        double pooledEstimate = pooled.Coefficients.FirstOrDefault(c => c.Name == "лаг отклика")?.Estimate ?? 0;
        double withinEstimate = within.Coefficients.FirstOrDefault(c => c.Name == "лаг отклика")?.Estimate ?? 0;

        return (pooledEstimate, withinEstimate);
    }
}
