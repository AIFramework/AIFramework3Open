using AI.Solvers.Chem.Metrology;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Polymers;

/// <summary>
/// Молекулярно-массовое распределение полимера
/// </summary>
/// <remarks>
/// Распределение хранится срезами «молярная масса - массовая доля»: именно в таком
/// виде его даёт гель-проникающая хроматография, где каждой точке хроматограммы
/// отвечает своя масса по градуировке.
/// </remarks>
public sealed partial class MolarMassDistribution
{
    private readonly double[] _masses;
    private readonly double[] _weights;

    /// <summary>Молярные массы срезов, г/моль</summary>
    public IReadOnlyList<double> Masses => _masses;

    /// <summary>Массовые доли срезов (нормированы на единицу)</summary>
    public IReadOnlyList<double> Weights => _weights;

    /// <summary>Среднечисленная молярная масса Mn</summary>
    public double NumberAverage { get; }

    /// <summary>Средневесовая молярная масса Mw</summary>
    public double WeightAverage { get; }

    /// <summary>Z-средняя молярная масса Mz</summary>
    public double ZAverage { get; }

    /// <summary>Коэффициент полидисперсности Mw/Mn</summary>
    public double Dispersity => NumberAverage > 0 ? WeightAverage / NumberAverage : double.NaN;

    /// <summary>Молярная масса в максимуме распределения (Mp)</summary>
    public double PeakMass { get; }

    /// <summary>Создаёт распределение по срезам</summary>
    /// <param name="masses">Молярные массы срезов, г/моль</param>
    /// <param name="weights">Массовые доли или интенсивности срезов</param>
    public MolarMassDistribution(IReadOnlyList<double> masses, IReadOnlyList<double> weights)
    {
        ArgumentNullException.ThrowIfNull(masses);
        ArgumentNullException.ThrowIfNull(weights);

        if (masses.Count != weights.Count)
            throw new ArgumentException("Число масс и число долей должно совпадать");

        var validMasses = new List<double>(masses.Count);
        var validWeights = new List<double>(masses.Count);

        for (int i = 0; i < masses.Count; i++)
        {
            if (masses[i] > 0 && weights[i] > 0)
            {
                validMasses.Add(masses[i]);
                validWeights.Add(weights[i]);
            }
        }

        if (validMasses.Count == 0)
            throw new ArgumentException("Распределение не содержит положительных срезов");

        double total = validWeights.Sum();

        _masses = validMasses.ToArray();
        _weights = validWeights.Select(w => w / total).ToArray();

        // Через массовые доли: Mn = 1 / sum(wi/Mi), Mw = sum(wi·Mi), Mz = sum(wi·Mi^2)/sum(wi·Mi)
        double inverseSum = 0, firstMoment = 0, secondMoment = 0;

        for (int i = 0; i < _masses.Length; i++)
        {
            inverseSum += _weights[i] / _masses[i];
            firstMoment += _weights[i] * _masses[i];
            secondMoment += _weights[i] * _masses[i] * _masses[i];
        }

        NumberAverage = 1.0 / inverseSum;
        WeightAverage = firstMoment;
        ZAverage = firstMoment > 0 ? secondMoment / firstMoment : double.NaN;

        int peak = 0;

        for (int i = 1; i < _weights.Length; i++)
        {
            if (_weights[i] > _weights[peak])
                peak = i;
        }

        PeakMass = _masses[peak];
    }

    /// <summary>
    /// Строит распределение по хроматограмме ГПХ и градуировке «объём - логарифм массы»
    /// </summary>
    /// <param name="retentionVolumes">Объёмы удерживания срезов</param>
    /// <param name="signal">Сигнал детектора в тех же точках</param>
    /// <param name="calibration">Градуировка lg M от объёма удерживания</param>
    /// <param name="baseline">Уровень отсечки сигнала (шум базовой линии)</param>
    public static MolarMassDistribution FromChromatogram(
        IReadOnlyList<double> retentionVolumes,
        IReadOnlyList<double> signal,
        LinearFit calibration,
        double baseline = 0)
    {
        ArgumentNullException.ThrowIfNull(retentionVolumes);
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(calibration);

        if (retentionVolumes.Count != signal.Count)
            throw new ArgumentException("Число точек объёма и сигнала должно совпадать");

        var masses = new List<double>(signal.Count);
        var weights = new List<double>(signal.Count);

        for (int i = 0; i < signal.Count; i++)
        {
            double height = signal[i] - baseline;

            if (height <= 0)
                continue;

            masses.Add(Math.Pow(10, calibration.Predict(retentionVolumes[i])));
            weights.Add(height);
        }

        return new MolarMassDistribution(masses, weights);
    }

    /// <summary>
    /// Градуировка колонки по узкодисперсным стандартам: lg M от объёма удерживания
    /// </summary>
    /// <param name="retentionVolumes">Объёмы удерживания стандартов</param>
    /// <param name="molarMasses">Паспортные массы стандартов, г/моль</param>
    public static LinearFit Calibrate(IReadOnlyList<double> retentionVolumes, IReadOnlyList<double> molarMasses)
    {
        ArgumentNullException.ThrowIfNull(retentionVolumes);
        ArgumentNullException.ThrowIfNull(molarMasses);

        if (retentionVolumes.Count != molarMasses.Count)
            throw new ArgumentException("Число объёмов и число масс должно совпадать");

        if (molarMasses.Any(m => m <= 0))
            throw new ArgumentException("Молярные массы стандартов должны быть положительными");

        return LinearFit.Fit(retentionVolumes.ToArray(), molarMasses.Select(Math.Log10).ToArray());
    }

    /// <summary>
    /// Наиболее вероятное распределение Флори для поликонденсации
    /// </summary>
    /// <param name="monomerMass">Масса звена, г/моль</param>
    /// <param name="conversion">Степень завершённости реакции p</param>
    /// <param name="maxDegree">Наибольшая учитываемая степень полимеризации</param>
    public static MolarMassDistribution Flory(double monomerMass, double conversion, int maxDegree = 2000)
    {
        if (conversion is <= 0 or >= 1)
            throw new ArgumentException("Степень завершённости должна лежать в интервале (0; 1)", nameof(conversion));

        var masses = new double[maxDegree];
        var weights = new double[maxDegree];

        for (int degree = 1; degree <= maxDegree; degree++)
        {
            masses[degree - 1] = degree * monomerMass;

            // Массовая доля цепей длины x: w = x·(1-p)^2·p^(x-1)
            weights[degree - 1] = degree * (1 - conversion) * (1 - conversion) * Math.Pow(conversion, degree - 1);
        }

        return new MolarMassDistribution(masses, weights);
    }

    /// <summary>
    /// Вязкостная средняя молярная масса по Марку-Хаувинку
    /// </summary>
    /// <param name="exponent">Показатель a уравнения Марка-Хаувинка</param>
    public double ViscosityAverage(double exponent)
    {
        double numerator = 0;

        for (int i = 0; i < _masses.Length; i++)
            numerator += _weights[i] * Math.Pow(_masses[i], exponent);

        return Math.Pow(numerator, 1.0 / exponent);
    }

    /// <summary>Отчёт по распределению</summary>
    public string Report()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine("Молекулярно-массовое распределение");
        text.AppendLine(string.Format(culture, "  Срезов: {0}", _masses.Length));
        text.AppendLine(string.Format(culture, "  Mn = {0:N0} г/моль", NumberAverage));
        text.AppendLine(string.Format(culture, "  Mw = {0:N0} г/моль", WeightAverage));
        text.AppendLine(string.Format(culture, "  Mz = {0:N0} г/моль", ZAverage));
        text.AppendLine(string.Format(culture, "  Mp = {0:N0} г/моль", PeakMass));
        text.AppendLine(string.Format(culture, "  Полидисперсность Mw/Mn = {0:F2}", Dispersity));

        return text.ToString();
    }
}
