namespace AI.Solvers.Chem.Signals;

/// <summary>
/// Способ оценки базовой линии перед поиском пиков
/// </summary>
public enum BaselineMode
{
    /// <summary>Не корректировать</summary>
    None,

    /// <summary>Асимметричный МНК: устойчив к высоким пикам, выбор по умолчанию</summary>
    Asymmetric,

    /// <summary>Итерационный полином: для плавного дрейфа и коротких сигналов</summary>
    Polynomial,

    /// <summary>Морфологический (минимум в окне)</summary>
    Rolling
}

/// <summary>
/// Настройки поиска пиков
/// </summary>
public sealed class PeakDetectionOptions
{
    /// <summary>Окно сглаживания Савицкого-Голея; 0 или 1 - без сглаживания</summary>
    public int SmoothingWindow { get; set; } = 9;

    /// <summary>Порядок полинома сглаживания</summary>
    public int SmoothingOrder { get; set; } = 2;

    /// <summary>Способ оценки базовой линии</summary>
    public BaselineMode Baseline { get; set; } = BaselineMode.None;

    /// <summary>Порядок полинома базовой линии</summary>
    public int BaselineOrder { get; set; } = 3;

    /// <summary>Окно морфологической базовой линии в точках</summary>
    public int BaselineWindow { get; set; } = 51;

    /// <summary>Штраф за кривизну базовой линии для асимметричного МНК</summary>
    public double BaselineSmoothness { get; set; } = 1e6;

    /// <summary>Вес точек выше базовой линии для асимметричного МНК</summary>
    public double BaselineAsymmetry { get; set; } = 0.01;

    /// <summary>Минимальная высота пика в долях от максимума сигнала</summary>
    public double RelativeHeightThreshold { get; set; } = 0.01;

    /// <summary>Минимальная абсолютная высота; null - не ограничивать</summary>
    public double? AbsoluteHeightThreshold { get; set; }

    /// <summary>
    /// Минимальное отношение сигнал/шум для пика: высота должна превышать
    /// SignalToNoise · σ(шума)
    /// </summary>
    public double SignalToNoise { get; set; } = 3.0;

    /// <summary>Минимальное число точек в пике</summary>
    public int MinPoints { get; set; } = 3;

    /// <summary>Максимальное число пиков в результате; 0 - без ограничения</summary>
    public int MaxPeaks { get; set; }
}

/// <summary>
/// Поиск и интегрирование пиков хроматограммы или спектра
/// </summary>
/// <remarks>
/// Границы пика ищутся спуском от вершины до впадины, площадь считается методом
/// трапеций над линией, соединяющей границы. Это то, что делает штатный
/// интегратор хроматографа, - и потому результат сопоставим с ним.
/// </remarks>
public static class PeakDetector
{
    /// <summary>
    /// Находит пики и считает их характеристики
    /// </summary>
    /// <param name="time">Ось времени (или длин волн)</param>
    /// <param name="signal">Сигнал детектора</param>
    /// <param name="options">Настройки поиска</param>
    public static IReadOnlyList<Peak> Detect(double[] time, double[] signal, PeakDetectionOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(signal);

        if (time.Length != signal.Length)
            throw new ArgumentException("Time and signal arrays must have the same length");

        options ??= new PeakDetectionOptions();

        if (signal.Length < 5)
            return Array.Empty<Peak>();

        // 1. Базовая линия
        double[] corrected = options.Baseline switch
        {
            BaselineMode.Asymmetric => BaselineCorrection.Subtract(
                signal, BaselineCorrection.AsymmetricLeastSquares(
                    signal, options.BaselineSmoothness, options.BaselineAsymmetry)),
            BaselineMode.Polynomial => BaselineCorrection.Subtract(
                signal, BaselineCorrection.ModifiedPolynomial(signal, options.BaselineOrder)),
            BaselineMode.Rolling => BaselineCorrection.Subtract(
                signal, BaselineCorrection.RollingMinimum(signal, options.BaselineWindow)),
            _ => (double[])signal.Clone()
        };

        // 2. Сглаживание для устойчивого поиска вершин; площади считаются по исходному сигналу
        double[] smoothed = options.SmoothingWindow >= 3
            ? SavitzkyGolay.Apply(corrected, MakeOdd(options.SmoothingWindow), options.SmoothingOrder)
            : corrected;

        double noise = BaselineCorrection.EstimateNoise(corrected);
        double maxSignal = smoothed.Max();

        double threshold = Math.Max(
            options.AbsoluteHeightThreshold ?? double.NegativeInfinity,
            Math.Max(options.RelativeHeightThreshold * maxSignal, options.SignalToNoise * noise));

        var peaks = new List<Peak>();

        for (int i = 1; i < smoothed.Length - 1; i++)
        {
            if (smoothed[i] <= smoothed[i - 1] || smoothed[i] < smoothed[i + 1])
                continue;

            int start = DescendLeft(smoothed, i);
            int end = DescendRight(smoothed, i);

            if (end - start + 1 < options.MinPoints)
                continue;

            double baselineAtApex = InterpolateBaseline(time, corrected, start, end, time[i]);
            double height = corrected[i] - baselineAtApex;

            if (height < threshold)
                continue;

            peaks.Add(Build(time, corrected, i, start, end, height, baselineAtApex));
        }

        // 3. Доли площадей и ограничение количества
        var result = peaks
            .OrderByDescending(p => p.Area)
            .Take(options.MaxPeaks > 0 ? options.MaxPeaks : peaks.Count)
            .OrderBy(p => p.RetentionTime)
            .ToList();

        double totalArea = result.Sum(p => p.Area);

        foreach (var peak in result)
            peak.AreaPercent = totalArea > 0 ? 100.0 * peak.Area / totalArea : 0;

        return result;
    }

    /// <summary>
    /// Интегрирует участок сигнала в заданных границах методом трапеций
    /// над линией, соединяющей края
    /// </summary>
    /// <param name="time">Ось времени</param>
    /// <param name="signal">Сигнал</param>
    /// <param name="start">Индекс начала</param>
    /// <param name="end">Индекс конца</param>
    public static double Integrate(double[] time, double[] signal, int start, int end)
    {
        if (start >= end)
            return 0;

        double area = 0;

        for (int i = start; i < end; i++)
        {
            double leftBase = LineValue(time, signal, start, end, time[i]);
            double rightBase = LineValue(time, signal, start, end, time[i + 1]);
            double left = signal[i] - leftBase;
            double right = signal[i + 1] - rightBase;

            area += (left + right) / 2 * (time[i + 1] - time[i]);
        }

        return area;
    }

    private static Peak Build(double[] time, double[] signal, int apex, int start, int end,
        double height, double baselineAtApex)
    {
        double area = Integrate(time, signal, start, end);

        double halfLeft = CrossingTime(time, signal, apex, start, baselineAtApex + (height * 0.5), toLeft: true);
        double halfRight = CrossingTime(time, signal, apex, end, baselineAtApex + (height * 0.5), toLeft: false);

        double left5 = CrossingTime(time, signal, apex, start, baselineAtApex + (height * 0.05), toLeft: true);
        double right5 = CrossingTime(time, signal, apex, end, baselineAtApex + (height * 0.05), toLeft: false);

        double left10 = CrossingTime(time, signal, apex, start, baselineAtApex + (height * 0.10), toLeft: true);
        double right10 = CrossingTime(time, signal, apex, end, baselineAtApex + (height * 0.10), toLeft: false);

        return new Peak
        {
            ApexIndex = apex,
            StartIndex = start,
            EndIndex = end,
            RetentionTime = ApexPosition(time, signal, apex),
            StartTime = time[start],
            EndTime = time[end],
            Height = height,
            Area = area,
            WidthAtHalfHeight = halfRight - halfLeft,
            LeftWidthAt5Percent = time[apex] - left5,
            RightWidthAt5Percent = right5 - time[apex],
            LeftWidthAt10Percent = time[apex] - left10,
            RightWidthAt10Percent = right10 - time[apex],
            BaselineAtApex = baselineAtApex
        };
    }

    // Уточнение положения вершины параболой по трём точкам
    private static double ApexPosition(double[] time, double[] signal, int apex)
    {
        if (apex <= 0 || apex >= signal.Length - 1)
            return time[apex];

        double y0 = signal[apex - 1], y1 = signal[apex], y2 = signal[apex + 1];
        double denominator = y0 - (2 * y1) + y2;

        if (Math.Abs(denominator) < 1e-15)
            return time[apex];

        double shift = 0.5 * (y0 - y2) / denominator;

        if (Math.Abs(shift) > 1)
            return time[apex];

        double step = (time[apex + 1] - time[apex - 1]) / 2;

        return time[apex] + (shift * step);
    }

    private static int DescendLeft(double[] signal, int apex)
    {
        int i = apex;

        while (i > 0 && signal[i - 1] <= signal[i])
            i--;

        return i;
    }

    private static int DescendRight(double[] signal, int apex)
    {
        int i = apex;

        while (i < signal.Length - 1 && signal[i + 1] <= signal[i])
            i++;

        return i;
    }

    private static double InterpolateBaseline(double[] time, double[] signal, int start, int end, double at)
        => LineValue(time, signal, start, end, at);

    private static double LineValue(double[] time, double[] signal, int start, int end, double at)
    {
        double span = time[end] - time[start];

        if (Math.Abs(span) < 1e-15)
            return signal[start];

        double slope = (signal[end] - signal[start]) / span;

        return signal[start] + (slope * (at - time[start]));
    }

    // Точка пересечения заданного уровня при движении от вершины к границе
    private static double CrossingTime(double[] time, double[] signal, int apex, int limit, double level, bool toLeft)
    {
        int step = toLeft ? -1 : 1;
        int i = apex;

        while (i != limit)
        {
            int next = i + step;

            if (signal[next] <= level)
            {
                double dy = signal[i] - signal[next];

                if (Math.Abs(dy) < 1e-15)
                    return time[next];

                double fraction = (signal[i] - level) / dy;

                return time[i] + (fraction * (time[next] - time[i]));
            }

            i = next;
        }

        return time[limit];
    }

    private static int MakeOdd(int window) => window % 2 == 0 ? window + 1 : window;
}
