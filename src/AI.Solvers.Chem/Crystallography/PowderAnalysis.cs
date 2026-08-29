using AI.Solvers.Chem.Kinetics;
using AI.Solvers.Chem.Metrology;
using AI.Solvers.Chem.Structures;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Crystallography;

/// <summary>
/// Тип решётки Браве, определённый по погасаниям
/// </summary>
public enum LatticeCentering
{
    /// <summary>Примитивная</summary>
    Primitive,

    /// <summary>Объёмноцентрированная</summary>
    BodyCentred,

    /// <summary>Гранецентрированная</summary>
    FaceCentred
}

/// <summary>
/// Проиндицированная линия дифрактограммы
/// </summary>
/// <param name="TwoTheta">Наблюдённый угол 2-тета, градусы</param>
/// <param name="Spacing">Наблюдённое межплоскостное расстояние, ангстремы</param>
/// <param name="H">Индекс h</param>
/// <param name="K">Индекс k</param>
/// <param name="L">Индекс l</param>
/// <param name="CalculatedTwoTheta">Угол, посчитанный по найденной ячейке</param>
public readonly record struct IndexedLine(
    double TwoTheta,
    double Spacing,
    int H,
    int K,
    int L,
    double CalculatedTwoTheta)
{
    /// <summary>Индексы отражения</summary>
    public string Indices => $"{H}{K}{L}";

    /// <summary>Сумма квадратов индексов</summary>
    public int SquaredSum => (H * H) + (K * K) + (L * L);

    /// <summary>Расхождение расчёта и опыта по углу, градусы</summary>
    public double Delta => CalculatedTwoTheta - TwoTheta;

    /// <summary>Строка описания линии</summary>
    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture,
            "{0,6}  2theta набл. = {1,7:F3}  расч. = {2,7:F3}  d = {3,8:F4}  разность = {4,7:F3}",
            Indices, TwoTheta, CalculatedTwoTheta, Spacing, Delta);
}

/// <summary>
/// Результат индицирования порошковой дифрактограммы
/// </summary>
public sealed class IndexingResult
{
    /// <summary>Найденная ячейка</summary>
    public UnitCell Cell { get; init; }

    /// <summary>Тип центрировки</summary>
    public LatticeCentering Centering { get; init; }

    /// <summary>Проиндицированные линии</summary>
    public IReadOnlyList<IndexedLine> Lines { get; init; }

    /// <summary>Наибольшее расхождение по углу, градусы</summary>
    public double MaxDeviation { get; init; }

    /// <summary>
    /// Критерий качества индицирования: отношение числа наблюдённых линий
    /// к среднему расхождению и числу возможных отражений
    /// </summary>
    public double FigureOfMerit { get; init; }

    /// <summary>Отчёт по индицированию</summary>
    public string Report()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine("Индицирование порошковой дифрактограммы");
        text.AppendLine($"  Ячейка: {Cell}");
        text.AppendLine($"  Центрировка: {Describe(Centering)}");
        text.AppendLine(string.Format(culture, "  Наибольшее расхождение: {0:F3} градуса", MaxDeviation));
        text.AppendLine(string.Format(culture, "  Критерий F({0}) = {1:F1}", Lines.Count, FigureOfMerit));

        foreach (IndexedLine line in Lines)
            text.AppendLine("  " + line);

        return text.ToString();
    }

    private static string Describe(LatticeCentering centering) => centering switch
    {
        LatticeCentering.BodyCentred => "объёмноцентрированная (I)",
        LatticeCentering.FaceCentred => "гранецентрированная (F)",
        _ => "примитивная (P)"
    };
}

/// <summary>
/// Доля фазы в смеси по методу корундовых чисел
/// </summary>
/// <param name="Phase">Название фазы</param>
/// <param name="Intensity">Интенсивность опорной линии</param>
/// <param name="ReferenceIntensityRatio">Корундовое число I/Ic</param>
/// <param name="MassFraction">Массовая доля, %</param>
public readonly record struct PhaseQuantity(
    string Phase,
    double Intensity,
    double ReferenceIntensityRatio,
    double MassFraction);

/// <summary>
/// Обработка порошковой дифрактограммы: индицирование, уточнение ячейки,
/// размер областей когерентного рассеяния, количественный фазовый анализ
/// </summary>
/// <remarks>
/// Обратная задача дифракции решается в том же порядке, в каком её решают руками:
/// сначала по положениям линий подбирается метрика решётки, затем найденная ячейка
/// уточняется методом наименьших квадратов по всем линиям сразу.
/// </remarks>
public static class PowderAnalysis
{
    /// <summary>Межплоскостное расстояние по углу отражения</summary>
    /// <param name="twoTheta">Угол 2-тета, градусы</param>
    /// <param name="wavelength">Длина волны, ангстремы</param>
    public static double SpacingFromAngle(double twoTheta, double wavelength)
    {
        if (twoTheta is <= 0 or >= 180)
            throw new ArgumentException("Угол 2-тета должен лежать в интервале (0; 180) градусов", nameof(twoTheta));

        return wavelength / (2 * Math.Sin(twoTheta * Math.PI / 360));
    }

    /// <summary>Угол отражения по межплоскостному расстоянию</summary>
    /// <param name="spacing">Межплоскостное расстояние, ангстремы</param>
    /// <param name="wavelength">Длина волны, ангстремы</param>
    public static double AngleFromSpacing(double spacing, double wavelength)
    {
        double sine = wavelength / (2 * spacing);

        return sine is > 1 or <= 0 ? double.NaN : 2 * Math.Asin(sine) * 180 / Math.PI;
    }

    /// <summary>
    /// Индицирует дифрактограмму в предположении кубической решётки
    /// </summary>
    /// <param name="twoThetaValues">Углы наблюдённых линий, градусы</param>
    /// <param name="wavelength">Длина волны, ангстремы</param>
    /// <param name="tolerance">Допуск на отклонение суммы квадратов индексов от целого</param>
    /// <returns>Лучший вариант индицирования либо null, если ни один не подошёл</returns>
    public static IndexingResult IndexCubic(
        IReadOnlyList<double> twoThetaValues,
        double wavelength = PowderPattern.CopperKAlpha,
        double tolerance = 0.03)
    {
        ArgumentNullException.ThrowIfNull(twoThetaValues);

        if (twoThetaValues.Count < 2)
            throw new ArgumentException("Для индицирования нужно не менее двух линий", nameof(twoThetaValues));

        var angles = twoThetaValues.OrderBy(v => v).ToArray();
        var spacings = angles.Select(a => SpacingFromAngle(a, wavelength)).ToArray();

        // Величина Q = 1/d^2 растёт пропорционально сумме квадратов индексов
        var q = spacings.Select(d => 1 / (d * d)).ToArray();

        IndexingResult best = null;

        for (int first = 1; first <= 12; first++)
        {
            if (!IsRepresentable(first))
                continue;

            double edge = Math.Sqrt(first / q[0]);
            var assignment = new int[angles.Length];
            bool ok = true;

            for (int i = 0; i < angles.Length && ok; i++)
            {
                double value = q[i] * edge * edge;
                int rounded = (int)Math.Round(value);

                ok = rounded >= 1 && IsRepresentable(rounded) && Math.Abs(value - rounded) <= tolerance * rounded;

                if (ok)
                    assignment[i] = rounded;
            }

            if (!ok)
                continue;

            LatticeCentering centering = DetectCentering(assignment);
            UnitCell cell = RefineCubicEdge(angles, assignment, wavelength, edge);
            IndexingResult candidate = Describe(angles, spacings, assignment, cell, centering, wavelength);

            if (best == null || candidate.MaxDeviation < best.MaxDeviation)
                best = candidate;
        }

        return best;
    }

    /// <summary>
    /// Уточняет параметры ячейки методом наименьших квадратов по проиндицированным линиям
    /// </summary>
    /// <param name="lines">Проиндицированные линии</param>
    /// <param name="start">Начальное приближение ячейки</param>
    /// <param name="wavelength">Длина волны, ангстремы</param>
    /// <param name="system">Сингония, задающая число свободных параметров</param>
    public static (UnitCell Cell, NonlinearFitResult Fit) RefineCell(
        IReadOnlyList<IndexedLine> lines,
        UnitCell start,
        double wavelength = PowderPattern.CopperKAlpha,
        CrystalSystem? system = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(start);

        if (lines.Count == 0)
            throw new ArgumentException("Нет линий для уточнения", nameof(lines));

        CrystalSystem target = system ?? start.System;
        double[] initial = FreeParameters(start, target);

        if (lines.Count < initial.Length)
            throw new ArgumentException("Линий меньше, чем уточняемых параметров", nameof(lines));

        double[] Residuals(double[] parameters)
        {
            var result = new double[lines.Count];
            UnitCell cell;

            try
            {
                cell = BuildCell(parameters, target);
            }
            catch (ArgumentException)
            {
                // Недопустимая ячейка на пробном шаге: возвращаем большие остатки,
                // чтобы спуск ушёл обратно в область смысла
                Array.Fill(result, 1e6);

                return result;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                double calculated = 2 * cell.BraggAngle(lines[i].H, lines[i].K, lines[i].L, wavelength);
                result[i] = double.IsNaN(calculated) ? 1e6 : calculated - lines[i].TwoTheta;
            }

            return result;
        }

        double mean = lines.Average(l => l.TwoTheta);
        double variance = lines.Sum(l => (l.TwoTheta - mean) * (l.TwoTheta - mean));

        // Метрика решётки уже известна с точностью долей процента, и глобальный поиск
        // отжигом здесь только увёл бы решение из физичной области
        NonlinearFitResult fit = NonlinearFit.Fit(Residuals, initial, variance,
            new NonlinearFitOptions { AnnealingIterations = 0 });

        return (BuildCell(fit.Parameters, target), fit);
    }

    /// <summary>
    /// Размер области когерентного рассеяния по формуле Шеррера, ангстремы
    /// </summary>
    /// <param name="twoTheta">Положение линии, градусы</param>
    /// <param name="fullWidth">Ширина линии на половине высоты, градусы</param>
    /// <param name="wavelength">Длина волны, ангстремы</param>
    /// <param name="shapeFactor">Форм-фактор K</param>
    /// <param name="instrumentalWidth">Инструментальная ширина линии, градусы</param>
    public static double ScherrerSize(
        double twoTheta,
        double fullWidth,
        double wavelength = PowderPattern.CopperKAlpha,
        double shapeFactor = 0.9,
        double instrumentalWidth = 0)
    {
        double physical = PhysicalWidth(fullWidth, instrumentalWidth);

        if (physical <= 0)
            return double.PositiveInfinity;

        double theta = twoTheta * Math.PI / 360;
        double beta = physical * Math.PI / 180;

        return shapeFactor * wavelength / (beta * Math.Cos(theta));
    }

    /// <summary>
    /// Разделение вкладов размера кристаллитов и микродеформаций по Вильямсону-Холлу
    /// </summary>
    /// <param name="twoThetaValues">Положения линий, градусы</param>
    /// <param name="fullWidths">Ширины линий на половине высоты, градусы</param>
    /// <param name="wavelength">Длина волны, ангстремы</param>
    /// <param name="shapeFactor">Форм-фактор K</param>
    /// <param name="instrumentalWidth">Инструментальная ширина линии, градусы</param>
    public static (double Size, double Strain, double R2) WilliamsonHall(
        IReadOnlyList<double> twoThetaValues,
        IReadOnlyList<double> fullWidths,
        double wavelength = PowderPattern.CopperKAlpha,
        double shapeFactor = 0.9,
        double instrumentalWidth = 0)
    {
        ArgumentNullException.ThrowIfNull(twoThetaValues);
        ArgumentNullException.ThrowIfNull(fullWidths);

        if (twoThetaValues.Count != fullWidths.Count)
            throw new ArgumentException("Число углов и число ширин должно совпадать");

        if (twoThetaValues.Count < 3)
            throw new ArgumentException("Нужно не менее трёх линий", nameof(twoThetaValues));

        var x = new double[twoThetaValues.Count];
        var y = new double[twoThetaValues.Count];

        for (int i = 0; i < x.Length; i++)
        {
            double theta = twoThetaValues[i] * Math.PI / 360;
            double beta = PhysicalWidth(fullWidths[i], instrumentalWidth) * Math.PI / 180;

            // beta·cos(theta) = K·lambda/D + 4·eps·sin(theta)
            x[i] = 4 * Math.Sin(theta);
            y[i] = beta * Math.Cos(theta);
        }

        LinearFit fit = LinearFit.Fit(x, y);
        double size = fit.Intercept > 0 ? shapeFactor * wavelength / fit.Intercept : double.PositiveInfinity;

        return (size, fit.Slope, fit.R2);
    }

    /// <summary>
    /// Количественный фазовый анализ по корундовым числам
    /// </summary>
    /// <param name="phases">Названия фаз</param>
    /// <param name="intensities">Интенсивности опорных линий</param>
    /// <param name="referenceIntensityRatios">Корундовые числа I/Ic</param>
    public static IReadOnlyList<PhaseQuantity> QuantifyByRir(
        IReadOnlyList<string> phases,
        IReadOnlyList<double> intensities,
        IReadOnlyList<double> referenceIntensityRatios)
    {
        ArgumentNullException.ThrowIfNull(phases);
        ArgumentNullException.ThrowIfNull(intensities);
        ArgumentNullException.ThrowIfNull(referenceIntensityRatios);

        if (phases.Count != intensities.Count || phases.Count != referenceIntensityRatios.Count)
            throw new ArgumentException("Число фаз, интенсивностей и корундовых чисел должно совпадать");

        if (referenceIntensityRatios.Any(r => r <= 0))
            throw new ArgumentException("Корундовые числа должны быть положительными",
                nameof(referenceIntensityRatios));

        var reduced = new double[phases.Count];

        for (int i = 0; i < phases.Count; i++)
            reduced[i] = Math.Max(0, intensities[i]) / referenceIntensityRatios[i];

        double total = reduced.Sum();

        if (total <= 0)
            throw new ArgumentException("Все интенсивности нулевые", nameof(intensities));

        var result = new List<PhaseQuantity>(phases.Count);

        for (int i = 0; i < phases.Count; i++)
        {
            result.Add(new PhaseQuantity(phases[i], intensities[i], referenceIntensityRatios[i],
                100 * reduced[i] / total));
        }

        return result;
    }

    private static double PhysicalWidth(double observed, double instrumental)
    {
        if (observed <= 0)
            throw new ArgumentException("Ширина линии должна быть положительной", nameof(observed));

        if (instrumental <= 0)
            return observed;

        double squared = (observed * observed) - (instrumental * instrumental);

        return squared > 0 ? Math.Sqrt(squared) : 0;
    }

    // Уточнение ребра кубической ячейки: в переменных Q задача линейна по 1/a^2
    private static UnitCell RefineCubicEdge(
        IReadOnlyList<double> angles, IReadOnlyList<int> assignment, double wavelength, double start)
    {
        double numerator = 0, denominator = 0;

        for (int i = 0; i < angles.Count; i++)
        {
            double d = SpacingFromAngle(angles[i], wavelength);
            double q = 1 / (d * d);

            numerator += assignment[i] * q;
            denominator += (double)assignment[i] * assignment[i];
        }

        double inverseSquare = denominator > 0 ? numerator / denominator : 1 / (start * start);

        return UnitCell.Cubic(inverseSquare > 0 ? Math.Sqrt(1 / inverseSquare) : start);
    }

    private static IndexingResult Describe(
        IReadOnlyList<double> angles,
        IReadOnlyList<double> spacings,
        IReadOnlyList<int> assignment,
        UnitCell cell,
        LatticeCentering centering,
        double wavelength)
    {
        var lines = new List<IndexedLine>(angles.Count);
        double maxDeviation = 0, sumDeviation = 0;

        for (int i = 0; i < angles.Count; i++)
        {
            var (h, k, l) = IndicesFor(assignment[i]);
            double calculated = 2 * cell.BraggAngle(h, k, l, wavelength);

            lines.Add(new IndexedLine(angles[i], spacings[i], h, k, l, calculated));

            double deviation = Math.Abs(calculated - angles[i]);
            maxDeviation = Math.Max(maxDeviation, deviation);
            sumDeviation += deviation;
        }

        int possible = CountPossibleLines(assignment[^1], centering);
        double mean = sumDeviation / angles.Count;
        double merit = mean > 1e-12
            ? angles.Count / (mean * Math.Max(1, possible))
            : double.PositiveInfinity;

        return new IndexingResult
        {
            Cell = cell,
            Centering = centering,
            Lines = lines,
            MaxDeviation = maxDeviation,
            FigureOfMerit = merit
        };
    }

    private static LatticeCentering DetectCentering(IReadOnlyList<int> assignment)
    {
        if (assignment.All(AllowsFaceCentred))
            return LatticeCentering.FaceCentred;

        if (assignment.All(n => n % 2 == 0))
            return LatticeCentering.BodyCentred;

        return LatticeCentering.Primitive;
    }

    // Гранецентрированная решётка отражает только при индексах одной чётности
    private static bool AllowsFaceCentred(int squaredSum)
    {
        return EnumerateTriples(squaredSum).Any(t =>
            ((t.H % 2 == 0) && (t.K % 2 == 0) && (t.L % 2 == 0)) ||
            ((t.H % 2 == 1) && (t.K % 2 == 1) && (t.L % 2 == 1)));
    }

    private static int CountPossibleLines(int maxSquaredSum, LatticeCentering centering)
    {
        int count = 0;

        for (int n = 1; n <= maxSquaredSum; n++)
        {
            if (!IsRepresentable(n))
                continue;

            bool allowed = centering switch
            {
                LatticeCentering.FaceCentred => AllowsFaceCentred(n),
                LatticeCentering.BodyCentred => n % 2 == 0,
                _ => true
            };

            if (allowed)
                count++;
        }

        return count;
    }

    // Число представимо суммой трёх квадратов, если оно не вида 4^a·(8b+7)
    private static bool IsRepresentable(int n)
    {
        while (n % 4 == 0)
            n /= 4;

        return n % 8 != 7;
    }

    private static (int H, int K, int L) IndicesFor(int squaredSum)
    {
        foreach (var triple in EnumerateTriples(squaredSum))
            return triple;

        return (0, 0, 0);
    }

    private static IEnumerable<(int H, int K, int L)> EnumerateTriples(int squaredSum)
    {
        int limit = (int)Math.Sqrt(squaredSum);

        for (int h = limit; h >= 0; h--)
        {
            for (int k = h; k >= 0; k--)
            {
                int rest = squaredSum - (h * h) - (k * k);

                if (rest < 0)
                    continue;

                int l = (int)Math.Round(Math.Sqrt(rest));

                if (l <= k && (l * l) == rest)
                    yield return (h, k, l);
            }
        }
    }

    private static double[] FreeParameters(UnitCell cell, CrystalSystem system) => system switch
    {
        CrystalSystem.Cubic => new[] { cell.A },
        CrystalSystem.Tetragonal or CrystalSystem.Hexagonal => new[] { cell.A, cell.C },
        CrystalSystem.Trigonal => new[] { cell.A, cell.Alpha },
        CrystalSystem.Orthorhombic => new[] { cell.A, cell.B, cell.C },
        CrystalSystem.Monoclinic => new[] { cell.A, cell.B, cell.C, cell.Beta },
        _ => new[] { cell.A, cell.B, cell.C, cell.Alpha, cell.Beta, cell.Gamma }
    };

    private static UnitCell BuildCell(double[] parameters, CrystalSystem system) => system switch
    {
        CrystalSystem.Cubic => UnitCell.Cubic(parameters[0]),
        CrystalSystem.Tetragonal => UnitCell.Tetragonal(parameters[0], parameters[1]),
        CrystalSystem.Hexagonal => UnitCell.Hexagonal(parameters[0], parameters[1]),
        CrystalSystem.Trigonal => new UnitCell(parameters[0], parameters[0], parameters[0],
            parameters[1], parameters[1], parameters[1]),
        CrystalSystem.Orthorhombic => new UnitCell(parameters[0], parameters[1], parameters[2]),
        CrystalSystem.Monoclinic => new UnitCell(parameters[0], parameters[1], parameters[2], 90, parameters[3], 90),
        _ => new UnitCell(parameters[0], parameters[1], parameters[2], parameters[3], parameters[4], parameters[5])
    };
}
