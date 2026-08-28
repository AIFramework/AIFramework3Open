using AI.DataStructs.Algebraic;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Signals;

/// <summary>
/// Хроматограмма или спектр: пара «ось - сигнал» с операциями подготовки и разметки пиков
/// </summary>
public sealed class Chromatogram
{
    /// <summary>Ось времени или длин волн</summary>
    public double[] Time { get; }

    /// <summary>Сигнал детектора</summary>
    public double[] Signal { get; }

    /// <summary>Название пробы или канала детектора</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Число точек</summary>
    public int Count => Time.Length;

    /// <summary>Шаг по оси (по первым точкам)</summary>
    public double Step => Count > 1 ? Time[1] - Time[0] : 0;

    /// <summary>Оценка шума базовой линии</summary>
    public double Noise => BaselineCorrection.EstimateNoise(Signal);

    /// <summary>Создаёт хроматограмму из массивов</summary>
    /// <param name="time">Ось времени</param>
    /// <param name="signal">Сигнал</param>
    public Chromatogram(double[] time, double[] signal)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(signal);

        if (time.Length != signal.Length)
            throw new ArgumentException("Time and signal arrays must have the same length");

        if (time.Length < 2)
            throw new ArgumentException("A chromatogram needs at least two points");

        Time = (double[])time.Clone();
        Signal = (double[])signal.Clone();
    }

    /// <summary>Создаёт хроматограмму из векторов фреймворка</summary>
    /// <param name="time">Ось времени</param>
    /// <param name="signal">Сигнал</param>
    public Chromatogram(Vector time, Vector signal)
        : this(time.ToArray(), signal.ToArray())
    {
    }

    /// <summary>
    /// Равномерная ось от нуля с заданным шагом
    /// </summary>
    /// <param name="signal">Сигнал</param>
    /// <param name="step">Шаг по времени</param>
    /// <param name="start">Начальное время</param>
    public static Chromatogram FromSignal(double[] signal, double step = 1.0, double start = 0)
    {
        ArgumentNullException.ThrowIfNull(signal);

        var time = new double[signal.Length];

        for (int i = 0; i < signal.Length; i++)
            time[i] = start + (i * step);

        return new Chromatogram(time, signal);
    }

    /// <summary>Сглаженная копия</summary>
    /// <param name="window">Окно сглаживания</param>
    /// <param name="order">Порядок полинома</param>
    public Chromatogram Smooth(int window = 9, int order = 2)
        => new(Time, SavitzkyGolay.Apply(Signal, window, order)) { Name = Name };

    /// <summary>Копия с вычтенной базовой линией</summary>
    /// <param name="mode">Способ оценки базовой линии</param>
    /// <param name="order">Порядок полинома</param>
    /// <param name="window">Окно морфологического метода</param>
    public Chromatogram SubtractBaseline(BaselineMode mode = BaselineMode.Asymmetric, int order = 3, int window = 51)
    {
        double[] baseline = mode switch
        {
            BaselineMode.Asymmetric => BaselineCorrection.AsymmetricLeastSquares(Signal),
            BaselineMode.Polynomial => BaselineCorrection.ModifiedPolynomial(Signal, order),
            BaselineMode.Rolling => BaselineCorrection.RollingMinimum(Signal, window),
            _ => new double[Signal.Length]
        };

        return new Chromatogram(Time, BaselineCorrection.Subtract(Signal, baseline)) { Name = Name };
    }

    /// <summary>Первая производная сигнала</summary>
    /// <param name="window">Окно</param>
    /// <param name="order">Порядок полинома</param>
    public double[] Derivative(int window = 9, int order = 2)
        => SavitzkyGolay.Apply(Signal, window, order, derivative: 1, spacing: Step == 0 ? 1 : Step);

    /// <summary>Поиск пиков</summary>
    /// <param name="options">Настройки поиска</param>
    public IReadOnlyList<Peak> FindPeaks(PeakDetectionOptions options = null)
        => PeakDetector.Detect(Time, Signal, options);

    /// <summary>
    /// Отчёт по пикам: таблица с параметрами и проверкой пригодности системы
    /// </summary>
    /// <param name="peaks">Найденные пики</param>
    /// <param name="holdupTime">Мёртвое время колонки для расчёта k'; 0 - не считать</param>
    public string Report(IReadOnlyList<Peak> peaks, double holdupTime = 0)
    {
        ArgumentNullException.ThrowIfNull(peaks);

        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine(string.IsNullOrEmpty(Name) ? "Хроматограмма" : $"Хроматограмма: {Name}");
        text.AppendLine($"  Точек: {Count}, шум базовой линии: {Noise.ToString("G3", culture)}");
        text.AppendLine($"  Пиков найдено: {peaks.Count}");
        text.AppendLine();
        text.AppendLine("   №  имя           tR        площадь     высота     w½        S, %    тарелок   As");

        for (int i = 0; i < peaks.Count; i++)
        {
            var peak = peaks[i];

            text.AppendLine(string.Format(culture,
                "  {0,2}  {1,-12} {2,-9:G5} {3,-11:G5} {4,-10:G5} {5,-9:G4} {6,6:F2}  {7,8:F0}  {8,5:F2}",
                i + 1,
                Truncate(peak.Name, 12),
                peak.RetentionTime,
                peak.Area,
                peak.Height,
                peak.WidthAtHalfHeight,
                peak.AreaPercent,
                peak.TheoreticalPlates,
                peak.AsymmetryFactor));
        }

        if (peaks.Count > 1)
        {
            text.AppendLine();
            text.AppendLine("  Разрешение соседних пиков:");

            for (int i = 1; i < peaks.Count; i++)
            {
                double resolution = Peak.Resolution(peaks[i - 1], peaks[i]);
                string verdict = resolution >= 1.5 ? "полное" : resolution >= 1.0 ? "частичное" : "недостаточное";

                text.AppendLine(string.Format(culture,
                    "    {0} / {1}: Rs = {2:F2} ({3})", i, i + 1, resolution, verdict));
            }
        }

        if (holdupTime > 0)
        {
            text.AppendLine();
            text.AppendLine("  Коэффициенты удерживания k':");

            for (int i = 0; i < peaks.Count; i++)
                text.AppendLine(string.Format(culture, "    {0}: {1:F2}", i + 1, peaks[i].CapacityFactor(holdupTime)));
        }

        return text.ToString();
    }

    private static string Truncate(string value, int length)
    {
        if (string.IsNullOrEmpty(value))
            return "-";

        return value.Length <= length ? value : value.Substring(0, length - 1) + "…";
    }
}
