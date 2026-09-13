using System.Numerics;

namespace AI.Microwave.Propagation;

/// <summary>Отвод модели с дискретными задержками</summary>
/// <param name="NormalizedDelay">Задержка в единицах разброса задержек</param>
/// <param name="PowerDb">Мощность, дБ относительно других отводов</param>
/// <param name="LineOfSight">Прямой луч без замираний (иначе — рэлеевский отвод)</param>
public readonly record struct DelayTap(double NormalizedDelay, double PowerDb, bool LineOfSight = false);

/// <summary>
/// Модель канала с дискретными задержками (TDL) — нормированный профиль задержек TR 38.901.
/// </summary>
/// <remarks>
/// <para>
/// Пять профилей TR 38.901 v16.1, раздел 7.7.2: TDL-A, TDL-B, TDL-C — без прямой видимости, TDL-D и TDL-E — с
/// прямым лучом. Задержки нормированы так, что среднеквадратичный разброс задержек профиля равен единице:
/// реальные задержки получаются умножением на нужный разброс (например, 30 нс для малой соты, 300 нс для
/// городской макросоты). У профилей с прямым лучом первый отвод разделён на прямую составляющую и
/// рэлеевскую с той же задержкой; их отношение — K-фактор: 13,3 дБ у TDL-D и 22 дБ у TDL-E.
/// </para>
/// <para>
/// Таблицы перенесены из открытой реализации Sionna (NVIDIA) и проверяются тестом: нормированный разброс
/// задержек каждого профиля должен быть равен единице, а K-факторы — табличным.
/// </para>
/// <para>
/// Каждый рэлеевский отвод замирает независимо с доплеровским спектром Джейкса, прямой луч вращается
/// с доплеровским сдвигом 0,7·f_D (<see cref="FadingProcess"/>). Мощности отводов нормированы на единицу
/// в сумме, так что модель задаёт только малый масштаб: потери на трассе и затенение добавляются
/// отдельно.
/// </para>
/// </remarks>
public sealed class TappedDelayLineModel
{
    private readonly DelayTap[] _taps;

    private TappedDelayLineModel(string name, DelayTap[] taps)
    {
        Name = name;
        _taps = taps;
    }

    /// <summary>Название профиля</summary>
    public string Name { get; }

    /// <summary>Отводы в порядке таблицы</summary>
    public IReadOnlyList<DelayTap> Taps => _taps;

    /// <summary>Есть ли прямой луч</summary>
    public bool HasLineOfSight => _taps.Any(t => t.LineOfSight);

    /// <summary>
    /// K-фактор первого отвода, дБ: прямой луч к рэлеевской составляющей с той же задержкой; NaN без прямого луча
    /// </summary>
    public double FirstTapKFactorDb
    {
        get
        {
            DelayTap[] los = _taps.Where(t => t.LineOfSight).ToArray();

            if (los.Length == 0)
                return double.NaN;

            double delay = los[0].NormalizedDelay;
            double scattered = _taps.Where(t => !t.LineOfSight && Math.Abs(t.NormalizedDelay - delay) < 1e-12).Sum(t => FromDb(t.PowerDb));

            return scattered == 0 ? double.PositiveInfinity : 10 * Math.Log10(los.Sum(t => FromDb(t.PowerDb)) / scattered);
        }
    }

    /// <summary>Разброс задержек нормированного профиля — у таблиц TR 38.901 равен единице</summary>
    public double NormalizedRmsDelaySpread => AveragePowerDelayProfile(1).RmsDelaySpreadSeconds;

    /// <summary>TDL-A: без прямой видимости, 23 отвода</summary>
    public static TappedDelayLineModel TdlA { get; } = Nlos("TDL-A",
        [0.0, 0.3819, 0.4025, 0.5868, 0.4610, 0.5375, 0.6708, 0.5750, 0.7618, 1.5375, 1.8978, 2.2242, 2.1718, 2.4942, 2.5119, 3.0582, 4.0810, 4.4579, 4.5695, 4.7966, 5.0066, 5.3043, 9.6586],
        [-13.4, 0.0, -2.2, -4.0, -6.0, -8.2, -9.9, -10.5, -7.5, -15.9, -6.6, -16.7, -12.4, -15.2, -10.8, -11.3, -12.7, -16.2, -18.3, -18.9, -16.6, -19.9, -29.7]);

    /// <summary>TDL-B: без прямой видимости, 23 отвода</summary>
    public static TappedDelayLineModel TdlB { get; } = Nlos("TDL-B",
        [0.0000, 0.1072, 0.2155, 0.2095, 0.2870, 0.2986, 0.3752, 0.5055, 0.3681, 0.3697, 0.5700, 0.5283, 1.1021, 1.2756, 1.5474, 1.7842, 2.0169, 2.8294, 3.0219, 3.6187, 4.1067, 4.2790, 4.7834],
        [0.0, -2.2, -4.0, -3.2, -9.8, -1.2, -3.4, -5.2, -7.6, -3.0, -8.9, -9.0, -4.8, -5.7, -7.5, -1.9, -7.6, -12.2, -9.8, -11.4, -14.9, -9.2, -11.3]);

    /// <summary>TDL-C: без прямой видимости, 24 отвода</summary>
    public static TappedDelayLineModel TdlC { get; } = Nlos("TDL-C",
        [0.0, 0.2099, 0.2219, 0.2329, 0.2176, 0.6366, 0.6448, 0.6560, 0.6584, 0.7935, 0.8213, 0.9336, 1.2285, 1.3083, 2.1704, 2.7105, 4.2589, 4.6003, 5.4902, 5.6077, 6.3065, 6.6374, 7.0427, 8.6523],
        [-4.4, -1.2, -3.5, -5.2, -2.5, 0.0, -2.2, -3.9, -7.4, -7.1, -10.7, -11.1, -5.1, -6.8, -8.7, -13.2, -13.9, -13.9, -15.8, -17.1, -16.0, -15.7, -21.6, -22.8]);

    /// <summary>TDL-D: прямой луч с K = 13,3 дБ и 12 рэлеевских отводов</summary>
    public static TappedDelayLineModel TdlD { get; } = Los("TDL-D",
        [0.0, 0.0, 0.035, 0.612, 1.363, 1.405, 1.804, 2.596, 1.775, 4.042, 7.937, 9.424, 9.708, 12.525],
        [-0.2, -13.5, -18.8, -21.0, -22.8, -17.9, -20.1, -21.9, -22.9, -27.8, -23.6, -24.8, -30.0, -27.7]);

    /// <summary>TDL-E: прямой луч с K = 22 дБ и 13 рэлеевских отводов</summary>
    public static TappedDelayLineModel TdlE { get; } = Los("TDL-E",
        [0.0, 0.0, 0.5133, 0.5440, 0.5630, 0.5440, 0.7112, 1.9092, 1.9293, 1.9589, 2.6426, 3.7136, 5.4524, 12.0034, 20.6519],
        [-0.03, -22.03, -15.8, -18.1, -19.8, -22.9, -22.4, -18.6, -20.8, -22.6, -22.3, -25.6, -20.2, -29.8, -29.2]);

    /// <summary>Собственный профиль</summary>
    /// <param name="name">Название</param>
    /// <param name="taps">Отводы</param>
    public static TappedDelayLineModel Custom(string name, IEnumerable<DelayTap> taps)
    {
        ArgumentNullException.ThrowIfNull(taps);

        DelayTap[] array = taps.ToArray();

        if (array.Length == 0 || array.Any(t => !(t.NormalizedDelay >= 0) || double.IsNaN(t.PowerDb)))
            throw new ArgumentException("Нужен хотя бы один отвод с неотрицательной задержкой и заданной мощностью", nameof(taps));

        return new TappedDelayLineModel(name, array);
    }

    /// <summary>
    /// Средний профиль задержек: пути с мощностями отводов, нормированными на единицу, без замираний
    /// </summary>
    /// <param name="delaySpreadSeconds">Нужный разброс задержек, с</param>
    public MultipathChannel AveragePowerDelayProfile(double delaySpreadSeconds)
    {
        Wave.RequirePositive(delaySpreadSeconds, nameof(delaySpreadSeconds));

        double total = _taps.Sum(t => FromDb(t.PowerDb));

        return new MultipathChannel(_taps.Select(t => new PropagationPath(
            t.NormalizedDelay * delaySpreadSeconds,
            Math.Sqrt(FromDb(t.PowerDb) / total),
            0,
            t.LineOfSight ? PathKind.LineOfSight : PathKind.Scattered)));
    }

    /// <summary>Одна реализация канала с замираниями</summary>
    /// <param name="delaySpreadSeconds">Разброс задержек, с</param>
    /// <param name="maxDopplerHz">Наибольший доплеровский сдвиг v/λ, Гц</param>
    /// <param name="random">Генератор случайных чисел</param>
    /// <param name="sinusoids">Число синусоид на рэлеевский отвод</param>
    /// <param name="losDopplerRatio">Доплер прямого луча в долях наибольшего</param>
    public TappedDelayLineChannel Realize(
        double delaySpreadSeconds, double maxDopplerHz, Random random, int sinusoids = 32, double losDopplerRatio = 0.7)
    {
        Wave.RequirePositive(delaySpreadSeconds, nameof(delaySpreadSeconds));
        ArgumentNullException.ThrowIfNull(random);

        double total = _taps.Sum(t => FromDb(t.PowerDb));
        var delays = new double[_taps.Length];
        var amplitudes = new double[_taps.Length];
        var processes = new FadingProcess[_taps.Length];

        for (int i = 0; i < _taps.Length; i++)
        {
            delays[i] = _taps[i].NormalizedDelay * delaySpreadSeconds;
            amplitudes[i] = Math.Sqrt(FromDb(_taps[i].PowerDb) / total);
            processes[i] = _taps[i].LineOfSight
                ? FadingProcess.LineOfSight(losDopplerRatio * maxDopplerHz, random)
                : FadingProcess.Rayleigh(maxDopplerHz, random, sinusoids);
        }

        return new TappedDelayLineChannel(this, delays, amplitudes, processes);
    }

    /// <summary>Название профиля</summary>
    public override string ToString() => Name;

    private static TappedDelayLineModel Nlos(string name, double[] delays, double[] powers)
        => new(name, delays.Select((d, i) => new DelayTap(d, powers[i])).ToArray());

    private static TappedDelayLineModel Los(string name, double[] delays, double[] powers)
        => new(name, delays.Select((d, i) => new DelayTap(d, powers[i], i == 0)).ToArray());

    private static double FromDb(double db) => Math.Pow(10, db / 10);
}

/// <summary>Реализация канала с дискретными задержками: у каждого отвода свой процесс замираний</summary>
public sealed class TappedDelayLineChannel
{
    private readonly double[] _delays;
    private readonly double[] _amplitudes;
    private readonly FadingProcess[] _processes;

    internal TappedDelayLineChannel(TappedDelayLineModel model, double[] delays, double[] amplitudes, FadingProcess[] processes)
    {
        Model = model;
        _delays = delays;
        _amplitudes = amplitudes;
        _processes = processes;
    }

    /// <summary>Профиль, по которому построена реализация</summary>
    public TappedDelayLineModel Model { get; }

    /// <summary>Процессы замираний отводов</summary>
    public IReadOnlyList<FadingProcess> Processes => _processes;

    /// <summary>Мгновенный канал в момент времени</summary>
    /// <param name="timeSeconds">Время, с</param>
    public MultipathChannel Snapshot(double timeSeconds)
        => new(_delays.Select((delay, i) => new PropagationPath(
            delay,
            _amplitudes[i] * _processes[i].Gain(timeSeconds),
            double.IsPositiveInfinity(_processes[i]!.KFactor) ? _processes[i].MaxDopplerHz : 0,
            Model.Taps[i].LineOfSight ? PathKind.LineOfSight : PathKind.Scattered)));

    /// <summary>Пропускает комплексную огибающую через меняющийся во времени канал</summary>
    /// <param name="input">Отсчёты входного сигнала</param>
    /// <param name="sampleRate">Частота дискретизации, Гц</param>
    /// <param name="startTimeSeconds">Время первого отсчёта, с</param>
    public Complex[] Apply(IReadOnlyList<Complex> input, double sampleRate, double startTimeSeconds = 0)
    {
        ArgumentNullException.ThrowIfNull(input);
        Wave.RequirePositive(sampleRate, nameof(sampleRate));

        double[] delaysInSamples = _delays.Select(d => d * sampleRate).ToArray();

        return FractionalDelay.Convolve(input, delaysInSamples, (k, n) =>
            _amplitudes[k] * _processes[k].Gain(startTimeSeconds + (n / sampleRate)));
    }
}
