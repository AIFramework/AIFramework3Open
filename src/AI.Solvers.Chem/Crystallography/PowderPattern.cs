using AI.Geometry.Primitives;
using AI.Solvers.Chem.Database;
using AI.Solvers.Chem.Structures;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Crystallography;

/// <summary>
/// Отражение порошковой дифрактограммы
/// </summary>
/// <param name="H">Индекс h</param>
/// <param name="K">Индекс k</param>
/// <param name="L">Индекс l</param>
/// <param name="Spacing">Межплоскостное расстояние, ангстремы</param>
/// <param name="TwoTheta">Угол 2-тета, градусы</param>
/// <param name="Multiplicity">Фактор повторяемости</param>
/// <param name="StructureFactorSquared">Квадрат модуля структурного фактора</param>
/// <param name="Intensity">Относительная интенсивность, %</param>
public readonly record struct Reflection(
    int H,
    int K,
    int L,
    double Spacing,
    double TwoTheta,
    int Multiplicity,
    double StructureFactorSquared,
    double Intensity)
{
    /// <summary>Индексы отражения</summary>
    public string Indices => $"{H}{K}{L}";

    /// <summary>Строка описания отражения</summary>
    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture,
            "{0,6}  d = {1,8:F4}  2theta = {2,7:F3}  I = {3,6:F1}%", Indices, Spacing, TwoTheta, Intensity);
}

/// <summary>
/// Атомные факторы рассеяния
/// </summary>
/// <remarks>
/// Точные значения задаются коэффициентами Кромера-Манна из международных таблиц:
/// их нужно зарегистрировать вызовом <see cref="Register"/>. Без таблицы применяется
/// приближение независимых атомов f = Z: положения линий от этого не зависят вовсе,
/// а относительные интенсивности получаются оценочными - для фазового анализа
/// этого хватает, для уточнения по Ритвельду нет.
/// </remarks>
public static class AtomicScattering
{
    private static readonly Dictionary<string, (double[] A, double[] B, double C)> Coefficients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Регистрирует коэффициенты Кромера-Манна для элемента
    /// </summary>
    /// <param name="element">Символ элемента</param>
    /// <param name="a">Коэффициенты a1..a4</param>
    /// <param name="b">Коэффициенты b1..b4</param>
    /// <param name="c">Свободный член c</param>
    public static void Register(string element, double[] a, double[] b, double c)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Length != 4 || b.Length != 4)
            throw new ArgumentException("Коэффициентов Кромера-Манна должно быть по четыре");

        Coefficients[element] = ((double[])a.Clone(), (double[])b.Clone(), c);
    }

    /// <summary>Есть ли табличные коэффициенты для элемента</summary>
    /// <param name="element">Символ элемента</param>
    public static bool HasCoefficients(string element) => Coefficients.ContainsKey(element);

    /// <summary>
    /// Фактор рассеяния при заданном значении sin(theta)/lambda
    /// </summary>
    /// <param name="element">Символ элемента</param>
    /// <param name="sinThetaOverLambda">Значение sin(theta)/lambda, 1/ангстрем</param>
    /// <param name="database">Справочник элементов для приближения f = Z</param>
    public static double Factor(string element, double sinThetaOverLambda, ChemDatabase database)
    {
        if (Coefficients.TryGetValue(element, out var entry))
        {
            double s2 = sinThetaOverLambda * sinThetaOverLambda;
            double sum = entry.C;

            for (int i = 0; i < 4; i++)
                sum += entry.A[i] * Math.Exp(-entry.B[i] * s2);

            return sum;
        }

        return database?.GetElement(element)?.AtomicNumber ?? 1;
    }
}

/// <summary>
/// Расчёт порошковой рентгенограммы по кристаллической структуре
/// </summary>
/// <remarks>
/// Прямая задача дифракции: перебор отражений в пределах разрешения, структурные
/// факторы, множители повторяемости и Лоренца-поляризации, профиль псевдо-Фойгта.
/// Положения линий определяются только метрикой ячейки и потому точны.
/// </remarks>
public sealed class PowderPattern
{
    /// <summary>Длина волны излучения меди K-alpha, ангстремы</summary>
    public const double CopperKAlpha = 1.5406;

    /// <summary>Длина волны излучения кобальта K-alpha, ангстремы</summary>
    public const double CobaltKAlpha = 1.7890;

    /// <summary>Длина волны излучения молибдена K-alpha, ангстремы</summary>
    public const double MolybdenumKAlpha = 0.7107;

    /// <summary>Отражения, упорядоченные по углу</summary>
    public IReadOnlyList<Reflection> Reflections { get; }

    /// <summary>Длина волны, ангстремы</summary>
    public double Wavelength { get; }

    private PowderPattern(IReadOnlyList<Reflection> reflections, double wavelength)
    {
        Reflections = reflections;
        Wavelength = wavelength;
    }

    /// <summary>
    /// Считает дифрактограмму по структуре
    /// </summary>
    /// <param name="crystal">Кристаллическая структура</param>
    /// <param name="database">Справочник элементов</param>
    /// <param name="wavelength">Длина волны, ангстремы</param>
    /// <param name="maxTwoTheta">Верхняя граница по 2-тета, градусы</param>
    /// <param name="maxIndex">Наибольший перебираемый индекс</param>
    public static PowderPattern Calculate(
        Crystal crystal,
        ChemDatabase database,
        double wavelength = CopperKAlpha,
        double maxTwoTheta = 90,
        int maxIndex = 6)
    {
        ArgumentNullException.ThrowIfNull(crystal);

        UnitCell cell = crystal.Cell;
        MolecularStructure contents = crystal.Contents;
        double minSpacing = wavelength / (2 * Math.Sin(maxTwoTheta * Math.PI / 360));

        var groups = new Dictionary<string, (double Spacing, double TwoTheta, int Multiplicity, double Amplitude, int H, int K, int L)>();

        for (int h = -maxIndex; h <= maxIndex; h++)
        {
            for (int k = -maxIndex; k <= maxIndex; k++)
            {
                for (int l = -maxIndex; l <= maxIndex; l++)
                {
                    if (h == 0 && k == 0 && l == 0)
                        continue;

                    double spacing = cell.InterplanarSpacing(h, k, l);

                    if (spacing < minSpacing)
                        continue;

                    double angle = cell.BraggAngle(h, k, l, wavelength);

                    if (double.IsNaN(angle))
                        continue;

                    double intensity = StructureFactorSquared(contents, cell, h, k, l, spacing, database);

                    if (intensity < 1e-8)
                        continue;

                    // Отражения одного семейства совпадают по d и по |F|
                    string key = string.Format(CultureInfo.InvariantCulture, "{0:F5}|{1:F5}", spacing, intensity);

                    if (groups.TryGetValue(key, out var existing))
                    {
                        groups[key] = existing with { Multiplicity = existing.Multiplicity + 1 };
                    }
                    else
                    {
                        groups[key] = (spacing, 2 * angle, 1, intensity,
                            Math.Abs(h), Math.Abs(k), Math.Abs(l));
                    }
                }
            }
        }

        var reflections = new List<Reflection>();
        double maxIntensity = 0;

        foreach (var group in groups.Values)
        {
            double theta = group.TwoTheta * Math.PI / 360;
            double lorentz = (1 + (Math.Cos(2 * theta) * Math.Cos(2 * theta)))
                / (Math.Sin(theta) * Math.Sin(theta) * Math.Cos(theta));

            double intensity = group.Multiplicity * group.Amplitude * lorentz;
            maxIntensity = Math.Max(maxIntensity, intensity);

            reflections.Add(new Reflection(group.H, group.K, group.L, group.Spacing, group.TwoTheta,
                group.Multiplicity, group.Amplitude, intensity));
        }

        var scaled = reflections
            .Select(r => r with { Intensity = maxIntensity > 0 ? 100 * r.Intensity / maxIntensity : 0 })
            .OrderBy(r => r.TwoTheta)
            .ToList();

        return new PowderPattern(scaled, wavelength);
    }

    /// <summary>
    /// Синтезирует профиль дифрактограммы функцией псевдо-Фойгта
    /// </summary>
    /// <param name="from">Начало диапазона по 2-тета, градусы</param>
    /// <param name="to">Конец диапазона, градусы</param>
    /// <param name="step">Шаг по 2-тета, градусы</param>
    /// <param name="fullWidth">Ширина линий на половине высоты, градусы</param>
    /// <param name="lorentzFraction">Доля лоренцевой составляющей профиля</param>
    public (double[] TwoTheta, double[] Intensity) Profile(
        double from = 5,
        double to = 90,
        double step = 0.02,
        double fullWidth = 0.15,
        double lorentzFraction = 0.5)
    {
        if (step <= 0 || to <= from)
            throw new ArgumentException("Некорректный диапазон углов");

        int points = (int)Math.Round((to - from) / step) + 1;
        var angles = new double[points];
        var intensity = new double[points];

        for (int i = 0; i < points; i++)
            angles[i] = from + (i * step);

        double half = fullWidth / 2;

        foreach (Reflection reflection in Reflections)
        {
            for (int i = 0; i < points; i++)
            {
                double delta = angles[i] - reflection.TwoTheta;

                if (Math.Abs(delta) > 10 * fullWidth)
                    continue;

                double gauss = Math.Exp(-Math.Log(2) * delta * delta / (half * half));
                double lorentz = 1 / (1 + (delta * delta / (half * half)));

                intensity[i] += reflection.Intensity
                    * ((lorentzFraction * lorentz) + ((1 - lorentzFraction) * gauss));
            }
        }

        return (angles, intensity);
    }

    /// <summary>Отчёт по наиболее сильным линиям</summary>
    /// <param name="count">Число выводимых линий</param>
    public string Report(int count = 10)
    {
        var text = new StringBuilder();

        text.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "Порошковая дифрактограмма, lambda = {0:F4} A, отражений: {1}", Wavelength, Reflections.Count));

        foreach (Reflection reflection in Reflections.OrderByDescending(r => r.Intensity).Take(count).OrderBy(r => r.TwoTheta))
            text.AppendLine("  " + reflection);

        return text.ToString();
    }

    private static double StructureFactorSquared(
        MolecularStructure contents, UnitCell cell, int h, int k, int l, double spacing, ChemDatabase database)
    {
        double s = 1 / (2 * spacing);
        double real = 0, imaginary = 0;

        foreach (AtomSite atom in contents.Atoms)
        {
            Vector3 fractional = cell.ToFractional(atom.Position);
            double phase = 2 * Math.PI * ((h * fractional.X) + (k * fractional.Y) + (l * fractional.Z));

            double factor = AtomicScattering.Factor(atom.Element, s, database)
                * atom.Occupancy
                * Math.Exp(-atom.ThermalParameter * s * s);

            real += factor * Math.Cos(phase);
            imaginary += factor * Math.Sin(phase);
        }

        return (real * real) + (imaginary * imaginary);
    }
}
