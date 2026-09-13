using System.Numerics;
using AI.HighLevelFunctions;

namespace AI.Microwave.Propagation;

/// <summary>
/// Замирания во времени: комплексный коэффициент канала при движении приёмника среди рассеивателей,
/// модель Кларка — Джейкса суммой синусоид.
/// </summary>
/// <remarks>
/// <para>
/// Волны приходят со всех сторон равновероятно, и волна с азимута α получает доплеровский сдвиг
/// <c>f_D·cos α</c>. Их сумма — комплексный гауссов процесс с огибающей по Рэлею, доплеровским спектром в виде
/// «чаши» <c>1/√(1 − (f/f_D)²)</c> и автокорреляцией <c>J₀(2π·f_D·τ)</c>: провалы разделены примерно полуволной
/// пройденного пути.
/// </para>
/// <para>
/// Процесс строится суммой N синусоид: углы прихода равномерно расставлены по окружности со случайным общим
/// сдвигом, фазы случайны. Такая расстановка даёт точную автокорреляцию J₀ в среднем по реализациям и
/// быстрее, чем полностью случайные углы, сходится по времени; при N ≥ 16 огибающая неотличима от
/// рэлеевской. Мощность процесса нормирована на единицу.
/// </para>
/// <para>
/// Райсовский процесс добавляет прямой луч с долей мощности K/(K + 1) и доплеровским сдвигом
/// <c>ρ·f_D</c>; по умолчанию ρ = 0,7, как в моделях с дискретными задержками TR 38.901.
/// </para>
/// </remarks>
public sealed class FadingProcess
{
    private readonly double[] _frequencies;
    private readonly double[] _phases;
    private readonly double _diffuseAmplitude;
    private readonly double _losAmplitude;
    private readonly double _losFrequency;
    private readonly double _losPhase;

    private FadingProcess(double maxDopplerHz, double kFactor, double[] frequencies, double[] phases, double losFrequency, double losPhase)
    {
        MaxDopplerHz = maxDopplerHz;
        KFactor = kFactor;
        _frequencies = frequencies;
        _phases = phases;
        _losFrequency = losFrequency;
        _losPhase = losPhase;

        bool pureLos = double.IsPositiveInfinity(kFactor);
        _losAmplitude = pureLos ? 1 : Math.Sqrt(kFactor / (kFactor + 1));
        _diffuseAmplitude = pureLos || frequencies.Length == 0 ? 0 : Math.Sqrt(1 / (kFactor + 1)) / Math.Sqrt(frequencies.Length);
    }

    /// <summary>Наибольший доплеровский сдвиг, Гц</summary>
    public double MaxDopplerHz { get; }

    /// <summary>K-фактор в разах: 0 — Рэлей, бесконечность — только прямой луч</summary>
    public double KFactor { get; }

    /// <summary>Число синусоид рассеянной части</summary>
    public int Sinusoids => _frequencies.Length;

    /// <summary>Рэлеевские замирания без прямого луча</summary>
    /// <param name="maxDopplerHz">Наибольший доплеровский сдвиг v/λ, Гц</param>
    /// <param name="random">Генератор случайных чисел</param>
    /// <param name="sinusoids">Число синусоид</param>
    public static FadingProcess Rayleigh(double maxDopplerHz, Random random, int sinusoids = 32)
        => Rician(0, maxDopplerHz, random, 0.7, sinusoids);

    /// <summary>Райсовские замирания: прямой луч и рассеянная часть</summary>
    /// <param name="kFactor">K-фактор в разах: мощность прямого луча к мощности рассеянной части</param>
    /// <param name="maxDopplerHz">Наибольший доплеровский сдвиг, Гц</param>
    /// <param name="random">Генератор случайных чисел</param>
    /// <param name="losDopplerRatio">Доплер прямого луча в долях наибольшего — косинус угла прихода</param>
    /// <param name="sinusoids">Число синусоид</param>
    public static FadingProcess Rician(double kFactor, double maxDopplerHz, Random random, double losDopplerRatio = 0.7, int sinusoids = 32)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (!(kFactor >= 0) || double.IsNaN(kFactor))
            throw new ArgumentOutOfRangeException(nameof(kFactor), "K-фактор неотрицателен");

        if (!(maxDopplerHz >= 0) || double.IsInfinity(maxDopplerHz))
            throw new ArgumentOutOfRangeException(nameof(maxDopplerHz), "Доплеровский сдвиг — конечное неотрицательное число");

        if (!(Math.Abs(losDopplerRatio) <= 1))
            throw new ArgumentOutOfRangeException(nameof(losDopplerRatio), "Доля доплера прямого луча лежит на [−1; 1]");

        int count = double.IsPositiveInfinity(kFactor) ? 0 : sinusoids;

        if (count != 0 && count < 4)
            throw new ArgumentOutOfRangeException(nameof(sinusoids), "Нужно не меньше четырёх синусоид");

        double offset = random.NextDouble();
        var frequencies = new double[count];
        var phases = new double[count];

        for (int n = 0; n < count; n++)
        {
            double angle = 2 * Math.PI * (n + offset) / count;
            frequencies[n] = maxDopplerHz * Math.Cos(angle);
            phases[n] = 2 * Math.PI * random.NextDouble();
        }

        return new FadingProcess(maxDopplerHz, kFactor, frequencies, phases, losDopplerRatio * maxDopplerHz, 2 * Math.PI * random.NextDouble());
    }

    /// <summary>Только прямой луч: постоянная амплитуда, фаза вращается с доплеровским сдвигом</summary>
    /// <param name="dopplerHz">Доплеровский сдвиг, Гц</param>
    /// <param name="random">Генератор — для начальной фазы</param>
    public static FadingProcess LineOfSight(double dopplerHz, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return new FadingProcess(Math.Abs(dopplerHz), double.PositiveInfinity, [], [], dopplerHz, 2 * Math.PI * random.NextDouble());
    }

    /// <summary>Комплексный коэффициент в момент времени</summary>
    /// <param name="timeSeconds">Время, с</param>
    public Complex Gain(double timeSeconds)
    {
        Complex diffuse = Complex.Zero;

        for (int n = 0; n < _frequencies.Length; n++)
            diffuse += Complex.FromPolarCoordinates(1, (2 * Math.PI * _frequencies[n] * timeSeconds) + _phases[n]);

        Complex los = _losAmplitude == 0
            ? Complex.Zero
            : Complex.FromPolarCoordinates(_losAmplitude, (2 * Math.PI * _losFrequency * timeSeconds) + _losPhase);

        return los + (_diffuseAmplitude * diffuse);
    }

    /// <summary>Отсчёты процесса на равномерной сетке</summary>
    /// <param name="sampleRate">Частота отсчётов, Гц</param>
    /// <param name="count">Число отсчётов</param>
    /// <param name="startTimeSeconds">Время первого отсчёта, с</param>
    public Complex[] Sample(double sampleRate, int count, double startTimeSeconds = 0)
    {
        Wave.RequirePositive(sampleRate, nameof(sampleRate));
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var samples = new Complex[count];

        for (int i = 0; i < count; i++)
            samples[i] = Gain(startTimeSeconds + (i / sampleRate));

        return samples;
    }

    /// <summary>Нормированная автокорреляция рэлеевского процесса Кларка J₀(2π·f_D·τ)</summary>
    /// <param name="maxDopplerHz">Наибольший доплеровский сдвиг, Гц</param>
    /// <param name="lagSeconds">Сдвиг во времени, с</param>
    public static double Autocorrelation(double maxDopplerHz, double lagSeconds)
        => SpecialFunctions.BesselJ0(2 * Math.PI * maxDopplerHz * lagSeconds);

    /// <summary>
    /// Частота пересечений огибающей рэлеевского процесса уровня ρ вверх: √(2π)·f_D·ρ·e^(−ρ²), в секунду
    /// </summary>
    /// <param name="maxDopplerHz">Наибольший доплеровский сдвиг, Гц</param>
    /// <param name="relativeLevel">Уровень ρ относительно среднеквадратичной огибающей</param>
    public static double LevelCrossingRate(double maxDopplerHz, double relativeLevel)
        => Math.Sqrt(2 * Math.PI) * maxDopplerHz * relativeLevel * Math.Exp(-relativeLevel * relativeLevel);

    /// <summary>
    /// Средняя длительность провала ниже уровня ρ: (e^(ρ²) − 1)/(ρ·f_D·√(2π)), с
    /// </summary>
    /// <param name="maxDopplerHz">Наибольший доплеровский сдвиг, Гц</param>
    /// <param name="relativeLevel">Уровень ρ относительно среднеквадратичной огибающей</param>
    public static double AverageFadeDurationSeconds(double maxDopplerHz, double relativeLevel)
        => (Math.Exp(relativeLevel * relativeLevel) - 1) / (relativeLevel * maxDopplerHz * Math.Sqrt(2 * Math.PI));
}
