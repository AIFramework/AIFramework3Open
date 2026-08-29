using AI.Solvers.Chem.Metrology;
using AI.Solvers.Chem.Structures;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Dynamics;

/// <summary>
/// Водородная связь, найденная по геометрическому признаку
/// </summary>
/// <param name="Donor">Номер атома-донора</param>
/// <param name="Hydrogen">Номер водорода</param>
/// <param name="Acceptor">Номер атома-акцептора</param>
/// <param name="Distance">Расстояние водород - акцептор, ангстремы</param>
/// <param name="Angle">Угол донор - водород - акцептор, градусы</param>
public readonly record struct HydrogenBond(int Donor, int Hydrogen, int Acceptor, double Distance, double Angle);

/// <summary>
/// Коэффициент диффузии, найденный по среднеквадратичному смещению
/// </summary>
/// <param name="Value">Коэффициент, квадратные ангстремы на пикосекунду</param>
/// <param name="StandardError">Стандартная ошибка</param>
/// <param name="R2">Коэффициент детерминации линейного участка</param>
public readonly record struct DiffusionResult(double Value, double StandardError, double R2)
{
    /// <summary>Коэффициент диффузии в квадратных сантиметрах на секунду</summary>
    public double SquareCentimetresPerSecond => Value * 1e-4;

    /// <summary>Строка описания результата</summary>
    public override string ToString()
        => string.Format(CultureInfo.InvariantCulture,
            "D = {0:E3} см2/с ({1:F4} A2/пс), R2 = {2:F4}", SquareCentimetresPerSecond, Value, R2);
}

/// <summary>
/// Анализ траектории молекулярной динамики: структура, подвижность, водородные связи
/// </summary>
public static class TrajectoryAnalysis
{
    /// <summary>
    /// Радиальная функция распределения g(r)
    /// </summary>
    /// <param name="trajectory">Траектория периодической системы</param>
    /// <param name="first">Элемент первого сорта</param>
    /// <param name="second">Элемент второго сорта; null - тот же, что первый</param>
    /// <param name="maxDistance">Верхняя граница по расстоянию, ангстремы</param>
    /// <param name="bins">Число интервалов гистограммы</param>
    public static (double[] Distance, double[] G) RadialDistribution(
        Trajectory trajectory,
        string first,
        string second = null,
        double maxDistance = 10,
        int bins = 200)
    {
        ArgumentNullException.ThrowIfNull(trajectory);

        if (!trajectory.IsPeriodic)
            throw new ArgumentException("Функция распределения считается для периодической системы", nameof(trajectory));

        if (bins < 2)
            throw new ArgumentException("Интервалов должно быть не менее двух", nameof(bins));

        second ??= first;

        UnitCell cell = trajectory.Cell;
        var firstIndices = trajectory.IndicesOf(first);
        var secondIndices = trajectory.IndicesOf(second);

        if (firstIndices.Count == 0 || secondIndices.Count == 0)
            throw new ArgumentException("В системе нет атомов заданного сорта");

        // Дальше половины наименьшего размера ячейки образ уже не однозначен
        double limit = Math.Min(maxDistance, MinimumHalfWidth(cell));
        double step = limit / bins;
        var histogram = new double[bins];
        bool same = string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

        foreach (MolecularStructure frame in trajectory.Frames)
        {
            foreach (int i in firstIndices)
            {
                foreach (int j in secondIndices)
                {
                    if (i == j)
                        continue;

                    double distance = cell.MinimumImage(frame.Atoms[i].Position, frame.Atoms[j].Position).Length;

                    if (distance >= limit)
                        continue;

                    histogram[(int)(distance / step)]++;
                }
            }
        }

        double density = (same ? secondIndices.Count - 1 : secondIndices.Count) / cell.Volume;
        var distances = new double[bins];
        var g = new double[bins];

        for (int bin = 0; bin < bins; bin++)
        {
            double inner = bin * step;
            double outer = inner + step;
            double centre = inner + (step / 2);
            double shell = 4.0 / 3 * Math.PI * ((outer * outer * outer) - (inner * inner * inner));

            distances[bin] = centre;
            g[bin] = histogram[bin] / (trajectory.Count * firstIndices.Count * shell * density);
        }

        return (distances, g);
    }

    /// <summary>
    /// Координационное число по функции распределения, посчитанной для той же пары сортов
    /// </summary>
    /// <param name="trajectory">Траектория, по которой считалась функция распределения</param>
    /// <param name="distances">Расстояния функции распределения</param>
    /// <param name="g">Значения функции распределения</param>
    /// <param name="radius">Верхний предел интегрирования, ангстремы</param>
    /// <param name="first">Элемент первого сорта</param>
    /// <param name="second">Элемент второго сорта; null - тот же, что первый</param>
    /// <remarks>
    /// Плотность здесь обязана совпадать с той, на которую нормировалась функция
    /// распределения: для одного сорта атомов это (N - 1)/V, а не N/V, иначе
    /// координационное число завышается на N/(N-1).
    /// </remarks>
    public static double CoordinationNumber(
        Trajectory trajectory,
        IReadOnlyList<double> distances,
        IReadOnlyList<double> g,
        double radius,
        string first,
        string second = null)
    {
        ArgumentNullException.ThrowIfNull(trajectory);

        if (!trajectory.IsPeriodic)
            throw new ArgumentException("Плотность определена для периодической системы", nameof(trajectory));

        second ??= first;
        int count = trajectory.IndicesOf(second).Count;
        bool same = string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

        return CoordinationNumber(distances, g, (same ? count - 1 : count) / trajectory.Cell.Volume, radius);
    }

    /// <summary>
    /// Координационное число: интеграл функции распределения до заданного радиуса
    /// </summary>
    /// <param name="distances">Расстояния функции распределения</param>
    /// <param name="g">Значения функции распределения</param>
    /// <param name="density">Плотность второго сорта атомов, штук на кубический ангстрем</param>
    /// <param name="radius">Верхний предел интегрирования, ангстремы</param>
    public static double CoordinationNumber(
        IReadOnlyList<double> distances, IReadOnlyList<double> g, double density, double radius)
    {
        ArgumentNullException.ThrowIfNull(distances);
        ArgumentNullException.ThrowIfNull(g);

        if (distances.Count != g.Count)
            throw new ArgumentException("Число точек по расстоянию и по функции должно совпадать");

        double step = distances.Count > 1 ? distances[1] - distances[0] : 0;
        double sum = 0;

        for (int i = 0; i < distances.Count && distances[i] <= radius; i++)
            sum += 4 * Math.PI * distances[i] * distances[i] * g[i] * step;

        return sum * density;
    }

    /// <summary>
    /// Среднеквадратичное смещение по нескольким началам отсчёта
    /// </summary>
    /// <param name="trajectory">Траектория с развёрнутыми координатами</param>
    /// <param name="indices">Номера прослеживаемых атомов; null - все</param>
    /// <param name="maxLag">Наибольший сдвиг в кадрах; 0 - половина траектории</param>
    public static (double[] Time, double[] Displacement) MeanSquareDisplacement(
        Trajectory trajectory,
        IReadOnlyList<int> indices = null,
        int maxLag = 0)
    {
        ArgumentNullException.ThrowIfNull(trajectory);

        if (trajectory.Count < 3)
            throw new ArgumentException("Нужно не менее трёх кадров", nameof(trajectory));

        indices ??= trajectory.AllIndices();

        if (maxLag <= 0)
            maxLag = trajectory.Count / 2;

        maxLag = Math.Min(maxLag, trajectory.Count - 1);

        var time = new double[maxLag + 1];
        var displacement = new double[maxLag + 1];

        for (int lag = 1; lag <= maxLag; lag++)
        {
            double sum = 0;
            int samples = 0;

            for (int origin = 0; origin + lag < trajectory.Count; origin++)
            {
                foreach (int atom in indices)
                {
                    Vector3 delta = trajectory.Position(origin + lag, atom) - trajectory.Position(origin, atom);
                    sum += delta.Dot(delta);
                    samples++;
                }
            }

            time[lag] = lag * trajectory.TimeStep;
            displacement[lag] = samples > 0 ? sum / samples : 0;
        }

        return (time, displacement);
    }

    /// <summary>
    /// Коэффициент диффузии по соотношению Эйнштейна на линейном участке смещения
    /// </summary>
    /// <param name="time">Время, пикосекунды</param>
    /// <param name="displacement">Среднеквадратичное смещение, квадратные ангстремы</param>
    /// <param name="skipFraction">Доля начала кривой, исключаемая из подгонки</param>
    public static DiffusionResult Diffusion(
        IReadOnlyList<double> time,
        IReadOnlyList<double> displacement,
        double skipFraction = 0.2)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(displacement);

        if (time.Count != displacement.Count)
            throw new ArgumentException("Число точек по времени и по смещению должно совпадать");

        if (skipFraction is < 0 or >= 1)
            throw new ArgumentException("Отбрасываемая доля должна лежать в интервале [0; 1)", nameof(skipFraction));

        int start = Math.Max(1, (int)(time.Count * skipFraction));
        int count = time.Count - start;

        if (count < 3)
            throw new ArgumentException("Слишком мало точек на линейном участке", nameof(time));

        var x = new double[count];
        var y = new double[count];

        for (int i = 0; i < count; i++)
        {
            x[i] = time[start + i];
            y[i] = displacement[start + i];
        }

        LinearFit fit = LinearFit.Fit(x, y);

        // Трёхмерное движение: наклон равен 6·D
        return new DiffusionResult(fit.Slope / 6, fit.SlopeStdError / 6, fit.R2);
    }

    /// <summary>
    /// Нормированная автокорреляционная функция ряда
    /// </summary>
    /// <param name="values">Ряд значений</param>
    /// <param name="maxLag">Наибольший сдвиг; 0 - половина ряда</param>
    public static double[] Autocorrelation(IReadOnlyList<double> values, int maxLag = 0)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Count < 3)
            throw new ArgumentException("Ряд слишком короткий", nameof(values));

        if (maxLag <= 0)
            maxLag = values.Count / 2;

        maxLag = Math.Min(maxLag, values.Count - 1);

        double mean = values.Average();
        double variance = values.Sum(v => (v - mean) * (v - mean));

        var result = new double[maxLag + 1];

        if (variance <= 0)
            return result;

        for (int lag = 0; lag <= maxLag; lag++)
        {
            double sum = 0;

            for (int i = 0; i + lag < values.Count; i++)
                sum += (values[i] - mean) * (values[i + lag] - mean);

            // Нормировка на дисперсию всего ряда: значение при нулевом сдвиге равно единице
            result[lag] = sum / variance;
        }

        return result;
    }

    /// <summary>
    /// Время корреляции: интеграл автокорреляционной функции до первого перехода через ноль
    /// </summary>
    /// <param name="autocorrelation">Автокорреляционная функция</param>
    /// <param name="timeStep">Шаг по времени, пикосекунды</param>
    public static double CorrelationTime(IReadOnlyList<double> autocorrelation, double timeStep)
    {
        ArgumentNullException.ThrowIfNull(autocorrelation);

        double sum = 0;

        for (int lag = 0; lag < autocorrelation.Count; lag++)
        {
            if (autocorrelation[lag] <= 0)
                break;

            sum += autocorrelation[lag] * timeStep;
        }

        return sum;
    }

    /// <summary>
    /// Ищет водородные связи в кадре по геометрическому признаку
    /// </summary>
    /// <param name="structure">Структура</param>
    /// <param name="maxDistance">Наибольшее расстояние водород - акцептор, ангстремы</param>
    /// <param name="minAngle">Наименьший угол донор - водород - акцептор, градусы</param>
    /// <param name="maxCovalent">Наибольшая длина связи донор - водород, ангстремы</param>
    public static IReadOnlyList<HydrogenBond> HydrogenBonds(
        MolecularStructure structure,
        double maxDistance = 2.5,
        double minAngle = 120,
        double maxCovalent = 1.2)
    {
        ArgumentNullException.ThrowIfNull(structure);

        var result = new List<HydrogenBond>();
        var hydrogens = new List<int>();
        var heavy = new List<int>();

        for (int i = 0; i < structure.Count; i++)
        {
            string element = structure.Atoms[i].Element;

            if (element == "H")
                hydrogens.Add(i);
            else if (element is "N" or "O" or "F")
                heavy.Add(i);
        }

        foreach (int hydrogen in hydrogens)
        {
            int donor = -1;
            double best = maxCovalent;

            foreach (int candidate in heavy)
            {
                double distance = structure.Distance(hydrogen, candidate);

                if (distance < best)
                {
                    best = distance;
                    donor = candidate;
                }
            }

            if (donor < 0)
                continue;

            foreach (int acceptor in heavy)
            {
                if (acceptor == donor)
                    continue;

                double distance = structure.Distance(hydrogen, acceptor);

                if (distance > maxDistance)
                    continue;

                double angle = structure.Angle(donor, hydrogen, acceptor);

                if (angle >= minAngle)
                    result.Add(new HydrogenBond(donor, hydrogen, acceptor, distance, angle));
            }
        }

        return result;
    }

    /// <summary>
    /// Среднее число водородных связей на кадр траектории
    /// </summary>
    /// <param name="trajectory">Траектория</param>
    /// <param name="maxDistance">Наибольшее расстояние водород - акцептор, ангстремы</param>
    /// <param name="minAngle">Наименьший угол донор - водород - акцептор, градусы</param>
    public static (double Average, double[] PerFrame) HydrogenBondCount(
        Trajectory trajectory,
        double maxDistance = 2.5,
        double minAngle = 120)
    {
        ArgumentNullException.ThrowIfNull(trajectory);

        var counts = new double[trajectory.Count];

        for (int frame = 0; frame < trajectory.Count; frame++)
            counts[frame] = HydrogenBonds(trajectory.Frames[frame], maxDistance, minAngle).Count;

        return (counts.Average(), counts);
    }

    /// <summary>Отчёт по траектории</summary>
    /// <param name="trajectory">Траектория</param>
    public static string Report(Trajectory trajectory)
    {
        ArgumentNullException.ThrowIfNull(trajectory);

        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine("Траектория молекулярной динамики");
        text.AppendLine(string.Format(culture, "  Кадров: {0}, атомов: {1}", trajectory.Count, trajectory.AtomCount));
        text.AppendLine(string.Format(culture, "  Шаг: {0:F4} пс, длительность: {1:F3} пс",
            trajectory.TimeStep, trajectory.Duration));

        if (trajectory.IsPeriodic)
            text.AppendLine($"  Ячейка: {trajectory.Cell}");

        text.AppendLine($"  Состав кадра: {trajectory.Frames[0].Formula}");

        if (trajectory.Count > 2)
        {
            var (time, displacement) = MeanSquareDisplacement(trajectory);
            text.AppendLine(string.Format(culture, "  Смещение за {0:F3} пс: {1:F4} A2",
                time[^1], displacement[^1]));
        }

        return text.ToString();
    }

    private static double MinimumHalfWidth(UnitCell cell)
    {
        // Высоты ячейки: объём, делённый на площадь соответствующей грани
        double heightA = cell.Volume / cell.VectorB.Cross(cell.VectorC).Length;
        double heightB = cell.Volume / cell.VectorC.Cross(cell.VectorA).Length;
        double heightC = cell.Volume / cell.VectorA.Cross(cell.VectorB).Length;

        return Math.Min(heightA, Math.Min(heightB, heightC)) / 2;
    }
}
