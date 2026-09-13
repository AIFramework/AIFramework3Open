using System.Numerics;
using AI.Insights;

namespace AI.Microwave.Propagation;

/// <summary>
/// Многолучевой канал: набор путей и то, что из него следует — импульсная и частотная характеристики,
/// разброс задержек, полоса и время когерентности, K-фактор.
/// </summary>
/// <remarks>
/// <para>
/// Канал — сумма путей <c>h(t, τ) = Σ aₖ·e^(j2π·f_Dk·t)·δ(τ − τₖ)</c>, частотная характеристика
/// <c>H(f) = Σ aₖ·e^(−j2π·f·τₖ)</c>, где f — отстройка от несущей. Пути задаёт кто угодно: трассировка лучей
/// (<see cref="RayTracingScene"/>), двухлучевая модель (<see cref="TwoRayGround"/>), стохастическая модель
/// с дискретными задержками (<see cref="TappedDelayLineChannel"/>).
/// </para>
/// <para>
/// Разброс задержек и доплеровский разброс — вторые центральные моменты по мощности путей. Полоса
/// когерентности считается не по правилу 1/(5σ_τ), а по точной частотной корреляции
/// <c>R(Δf) = Σ Pₖ·e^(−j2π·Δf·τₖ)/Σ Pₖ</c> — как первая отстройка, где |R| опускается до заданного уровня;
/// так же время когерентности — по временной корреляции. Если корреляция до уровня не опускается
/// (например, один путь сильно преобладает), полоса бесконечна: канал частотно-плоский на этом уровне.
/// </para>
/// </remarks>
public sealed class MultipathChannel : IInterpretable
{
    private readonly PropagationPath[] _paths;

    /// <summary>Создаёт канал</summary>
    /// <param name="paths">Пути</param>
    /// <param name="carrierFrequencyHz">Несущая, Гц; NaN, если не задана</param>
    public MultipathChannel(IEnumerable<PropagationPath> paths, double carrierFrequencyHz = double.NaN)
    {
        ArgumentNullException.ThrowIfNull(paths);

        _paths = paths.OrderBy(p => p.DelaySeconds).ToArray();

        foreach (PropagationPath path in _paths)
        {
            if (!(path.DelaySeconds >= 0) || double.IsInfinity(path.DelaySeconds))
                throw new ArgumentException("Задержка пути должна быть конечной и неотрицательной", nameof(paths));

            if (double.IsNaN(path.Gain.Real) || double.IsNaN(path.Gain.Imaginary) || double.IsNaN(path.DopplerHz))
                throw new ArgumentException("Амплитуда и доплер пути должны быть заданы", nameof(paths));
        }

        CarrierFrequencyHz = carrierFrequencyHz;
    }

    /// <summary>Пути по возрастанию задержки</summary>
    public IReadOnlyList<PropagationPath> Paths => _paths;

    /// <summary>Число путей</summary>
    public int Count => _paths.Length;

    /// <summary>Несущая, Гц</summary>
    public double CarrierFrequencyHz { get; }

    /// <summary>Суммарная мощность путей в разах — усиление канала, усреднённое по замираниям</summary>
    public double TotalPower => _paths.Sum(p => p.Power);

    /// <summary>
    /// Среднее усиление канала, дБ: сумма мощностей путей. Потери на трассе — это число со знаком минус
    /// </summary>
    public double PathGainDb => 10 * Math.Log10(TotalPower);

    /// <summary>
    /// Узкополосное усиление на несущей, дБ: |H(0)|², с интерференцией путей — то, что видит
    /// немодулированный сигнал в этой точке
    /// </summary>
    public double NarrowbandGainDb
    {
        get
        {
            Complex h = FrequencyResponse(0);
            return 10 * Math.Log10((h.Real * h.Real) + (h.Imaginary * h.Imaginary));
        }
    }

    /// <summary>Задержка первого прихода, с</summary>
    public double FirstArrivalSeconds => _paths.Length == 0 ? 0 : _paths[0].DelaySeconds;

    /// <summary>Средняя задержка, взвешенная по мощности, с</summary>
    public double MeanDelaySeconds => Moment(p => p.DelaySeconds, 1);

    /// <summary>Среднеквадратичный разброс задержек, с</summary>
    public double RmsDelaySpreadSeconds => Spread(p => p.DelaySeconds);

    /// <summary>Средний доплеровский сдвиг, взвешенный по мощности, Гц</summary>
    public double MeanDopplerHz => Moment(p => p.DopplerHz, 1);

    /// <summary>Среднеквадратичный доплеровский разброс, Гц</summary>
    public double DopplerSpreadHz => Spread(p => p.DopplerHz);

    /// <summary>Наибольший по модулю доплеровский сдвиг, Гц</summary>
    public double MaxDopplerHz => _paths.Length == 0 ? 0 : _paths.Max(p => Math.Abs(p.DopplerHz));

    /// <summary>
    /// Отношение мощности прямого луча к мощности остальных путей, в разах; 0 без прямой видимости
    /// </summary>
    public double RicianKFactor
    {
        get
        {
            double los = _paths.Where(p => p.Kind == PathKind.LineOfSight).Sum(p => p.Power);
            double rest = TotalPower - los;

            return los == 0 ? 0 : rest <= 0 ? double.PositiveInfinity : los / rest;
        }
    }

    /// <summary>
    /// Наибольшая избыточная задержка: от первого прихода до последнего пути не слабее порога
    /// относительно сильнейшего, с
    /// </summary>
    /// <param name="thresholdDb">Порог ниже сильнейшего пути, дБ</param>
    public double MaximumExcessDelaySeconds(double thresholdDb = 30)
    {
        if (_paths.Length == 0)
            return 0;

        double strongest = _paths.Max(p => p.Power);
        double floor = strongest * Math.Pow(10, -thresholdDb / 10);

        return _paths.Where(p => p.Power >= floor).Max(p => p.DelaySeconds) - FirstArrivalSeconds;
    }

    /// <summary>Частотная характеристика на отстройке от несущей</summary>
    /// <param name="frequencyOffsetHz">Отстройка, Гц</param>
    public Complex FrequencyResponse(double frequencyOffsetHz)
    {
        Complex sum = Complex.Zero;

        foreach (PropagationPath path in _paths)
            sum += path.Gain * Complex.FromPolarCoordinates(1, -2 * Math.PI * frequencyOffsetHz * path.DelaySeconds);

        return sum;
    }

    /// <summary>Частотная характеристика на наборе отстроек</summary>
    /// <param name="frequencyOffsetsHz">Отстройки, Гц</param>
    public Complex[] FrequencyResponse(IReadOnlyList<double> frequencyOffsetsHz)
    {
        ArgumentNullException.ThrowIfNull(frequencyOffsetsHz);

        return frequencyOffsetsHz.Select(FrequencyResponse).ToArray();
    }

    /// <summary>Модуль нормированной частотной корреляции |R(Δf)|</summary>
    /// <param name="separationHz">Разнос частот, Гц</param>
    public double FrequencyCorrelation(double separationHz)
        => Correlation(p => -2 * Math.PI * separationHz * p.DelaySeconds);

    /// <summary>Модуль нормированной временной корреляции |R(Δt)|</summary>
    /// <param name="lagSeconds">Сдвиг во времени, с</param>
    public double TimeCorrelation(double lagSeconds)
        => Correlation(p => 2 * Math.PI * p.DopplerHz * lagSeconds);

    /// <summary>
    /// Полоса когерентности: наименьший разнос частот, при котором |R(Δf)| опускается до уровня, Гц
    /// </summary>
    /// <param name="level">Уровень корреляции; обычно 0,5 или 0,9</param>
    public double CoherenceBandwidthHz(double level = 0.5)
    {
        double span = _paths.Length == 0 ? 0 : _paths[^1].DelaySeconds - _paths[0].DelaySeconds;

        return FirstCrossing(FrequencyCorrelation, level, span);
    }

    /// <summary>
    /// Время когерентности: наименьший сдвиг во времени, при котором |R(Δt)| опускается до уровня, с
    /// </summary>
    /// <param name="level">Уровень корреляции</param>
    public double CoherenceTimeSeconds(double level = 0.5)
    {
        double span = _paths.Length == 0 ? 0 : _paths.Max(p => p.DopplerHz) - _paths.Min(p => p.DopplerHz);

        return FirstCrossing(TimeCorrelation, level, span);
    }

    /// <summary>
    /// Импульсная характеристика на сетке отсчётов при полосе, равной частоте дискретизации
    /// </summary>
    /// <param name="sampleRate">Частота дискретизации, Гц</param>
    /// <param name="length">Число отсчётов</param>
    /// <param name="relativeToFirstArrival">Отсчитывать задержки от первого прихода, отбросив общую задержку</param>
    /// <remarks>
    /// Каждый путь даёт отсчёты <c>aₖ·sinc(n − τₖ·fs)</c> — так выглядит канал, пропущенный через идеальный
    /// фильтр полосы fs. Путь с задержкой, кратной периоду дискретизации, занимает один отсчёт; с дробной —
    /// растекается по соседним. Хвосты sinc бесконечны и обрезаются длиной.
    /// </remarks>
    public Complex[] ImpulseResponse(double sampleRate, int length, bool relativeToFirstArrival = true)
    {
        RequirePositive(sampleRate, nameof(sampleRate));
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        double origin = relativeToFirstArrival ? FirstArrivalSeconds : 0;
        var response = new Complex[length];

        foreach (PropagationPath path in _paths)
        {
            double position = (path.DelaySeconds - origin) * sampleRate;

            for (int n = 0; n < length; n++)
                response[n] += path.Gain * Sinc(n - position);
        }

        return response;
    }

    /// <summary>
    /// Пропускает комплексную огибающую через канал с учётом доплеровского вращения путей
    /// </summary>
    /// <param name="input">Отсчёты входного сигнала</param>
    /// <param name="sampleRate">Частота дискретизации, Гц</param>
    /// <param name="startTimeSeconds">Время первого отсчёта — от него отсчитывается доплеровский набег</param>
    /// <param name="relativeToFirstArrival">Отбросить общую задержку первого прихода</param>
    /// <remarks>
    /// Дробные задержки — интерполяцией окном Ланцоша шириной 32 отсчёта: для сигнала в полосе до 0,4·fs
    /// ошибка порядка 10⁻³. Выход той же длины, что вход; отсчёты до начала входа считаются нулевыми.
    /// </remarks>
    public Complex[] Apply(IReadOnlyList<Complex> input, double sampleRate, double startTimeSeconds = 0, bool relativeToFirstArrival = true)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequirePositive(sampleRate, nameof(sampleRate));

        double origin = relativeToFirstArrival ? FirstArrivalSeconds : 0;
        double[] delays = _paths.Select(p => (p.DelaySeconds - origin) * sampleRate).ToArray();

        return FractionalDelay.Convolve(input, delays, (k, n) =>
            _paths[k].Gain * Complex.FromPolarCoordinates(1, 2 * Math.PI * _paths[k].DopplerHz * (startTimeSeconds + (n / sampleRate))));
    }

    /// <summary>Канал в момент t: фазы путей повёрнуты доплеровским набегом</summary>
    /// <param name="timeSeconds">Момент времени, с</param>
    public MultipathChannel Snapshot(double timeSeconds) => new(_paths.Select(p => p.At(timeSeconds)), CarrierFrequencyHz);

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double spread = RmsDelaySpreadSeconds;
        double bandwidth = CoherenceBandwidthHz();
        double k = RicianKFactor;
        double excess = MaximumExcessDelaySeconds();
        bool flat = double.IsPositiveInfinity(bandwidth);

        return new InterpretationBuilder("Многолучевой канал")
            .Summary(Count == 0
                ? "Путей нет: сигнал до приёмника не доходит."
                : $"Путей {Count}, среднее усиление {Fmt.Num(PathGainDb, 1)} дБ; разброс задержек {Fmt.Num(spread * 1e9, 1)} нс, "
                  + (flat
                      ? "частотная корреляция не опускается до 0,5 — канал частотно-плоский. "
                      : $"полоса когерентности по уровню 0,5 — {Fmt.Num(bandwidth / 1e6, 3)} МГц. ")
                  + (k > 0 ? $"K-фактор {Fmt.Num(10 * Math.Log10(k), 1)} дБ." : "Прямой видимости нет."))
            .Metric("Путей", Count, null, null, MetricQuality.Unknown, 0)
            .Metric("Среднее усиление", Fmt.Num(PathGainDb, 2), "дБ", "сумма мощностей путей")
            .Metric("Узкополосное усиление", Fmt.Num(NarrowbandGainDb, 2), "дБ", "|H(0)|² с интерференцией путей")
            .Metric("Разброс задержек", Fmt.Num(spread * 1e9, 2), "нс", "среднеквадратичный, по мощности")
            .Metric("Наибольшая избыточная задержка", Fmt.Num(excess * 1e9, 1), "нс", "до пути на 30 дБ слабее сильнейшего")
            .Metric("Полоса когерентности", flat ? "∞" : Fmt.Num(bandwidth / 1e6, 4), "МГц", "|R(Δf)| = 0,5")
            .Metric("Доплеровский разброс", Fmt.Num(DopplerSpreadHz, 2), "Гц", null)
            .FindingIf(!flat,
                $"Сигнал шире {Fmt.Num(bandwidth / 1e6, 3)} МГц испытывает частотно-избирательные замирания: часть полосы "
                + "гаснет, часть нет, и символы наползают друг на друга. Нужен эквалайзер или OFDM с циклическим префиксом "
                + $"длиннее {Fmt.Num(excess * 1e9, 0)} нс.")
            .FindingIf(k > 10,
                "Прямой луч сильно преобладает: замирания райсовские и неглубокие, запас на замирания можно брать малым.")
            .FindingIf(Count > 0 && k == 0,
                "Без прямой видимости огибающая при движении распределена по Рэлею: провалы на 20 дБ ниже среднего "
                + "случаются примерно в одном проценте времени.")
            .Warning("Модель лучевая: учтены только перечисленные пути. Диффузное рассеяние на шероховатостях и мелких "
                + "предметах добавляет к ним множество слабых путей и увеличивает разброс задержек.")
            .Build();
    }

    private double Moment(Func<PropagationPath, double> value, int power)
    {
        double total = TotalPower;

        return total == 0 ? 0 : _paths.Sum(p => p.Power * Math.Pow(value(p), power)) / total;
    }

    private double Spread(Func<PropagationPath, double> value)
    {
        double total = TotalPower;

        if (total == 0)
            return 0;

        double mean = _paths.Sum(p => p.Power * value(p)) / total;

        return Math.Sqrt(_paths.Sum(p => p.Power * (value(p) - mean) * (value(p) - mean)) / total);
    }

    private double Correlation(Func<PropagationPath, double> phase)
    {
        double total = TotalPower;

        if (total == 0)
            return 0;

        Complex sum = Complex.Zero;

        foreach (PropagationPath path in _paths)
            sum += path.Power * Complex.FromPolarCoordinates(1, phase(path));

        return sum.Magnitude / total;
    }

    // Первый аргумент, где корреляция опускается до уровня: шаг по сетке от нуля, затем деление пополам.
    // Сетка идёт до 20/span — дальше корреляция дискретного набора путей почти периодична и нового не даёт
    private static double FirstCrossing(Func<double, double> correlation, double level, double span)
    {
        if (!(level > 0 && level < 1))
            throw new ArgumentOutOfRangeException(nameof(level), "Уровень корреляции лежит между 0 и 1");

        if (!(span > 0))
            return double.PositiveInfinity;

        double step = 1 / (400 * span);
        double previous = 0;

        for (double x = step; x <= 20 / span; x += step)
        {
            if (correlation(x) > level)
            {
                previous = x;
                continue;
            }

            double low = previous, high = x;

            for (int i = 0; i < 60; i++)
            {
                double middle = (low + high) / 2;

                if (correlation(middle) > level)
                    low = middle;
                else
                    high = middle;
            }

            return (low + high) / 2;
        }

        return double.PositiveInfinity;
    }

    private static double Sinc(double x) => Math.Abs(x) < 1e-12 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x);

    private static void RequirePositive(double value, string name)
    {
        if (!(value > 0) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, value, "Значение должно быть конечным и положительным");
    }
}

/// <summary>Свёртка с дробными задержками: общая для детерминированного и стохастического каналов</summary>
internal static class FractionalDelay
{
    private const int HalfWidth = 16;

    /// <summary>
    /// y[n] = Σₖ gₖ(n)·x(n − dₖ), где x между отсчётами восстанавливается окном Ланцоша
    /// </summary>
    /// <param name="input">Вход</param>
    /// <param name="delaysInSamples">Задержки путей в отсчётах, неотрицательные</param>
    /// <param name="gain">Амплитуда пути k в отсчёт n</param>
    public static Complex[] Convolve(IReadOnlyList<Complex> input, IReadOnlyList<double> delaysInSamples, Func<int, int, Complex> gain)
    {
        int length = input.Count;
        var output = new Complex[length];

        for (int k = 0; k < delaysInSamples.Count; k++)
        {
            double delay = delaysInSamples[k];
            int whole = (int)Math.Floor(delay);
            double fraction = delay - whole;
            bool integer = fraction < 1e-12;
            double[] weights = integer ? [1.0] : Weights(fraction);
            int first = integer ? 0 : -HalfWidth + 1;

            for (int n = 0; n < length; n++)
            {
                Complex value = Complex.Zero;

                // x(n − d) = Σ x[m]·L(n − d − m), m около n − whole − fraction
                for (int w = 0; w < weights.Length; w++)
                {
                    int m = n - whole - first - w;

                    if (m >= 0 && m < length)
                        value += input[m] * weights[w];
                }

                if (value != Complex.Zero)
                    output[n] += gain(k, n) * value;
            }
        }

        return output;
    }

    // Веса ядра Ланцоша L(t) = sinc(t)·sinc(t/a) в точках t = first + w + fraction… для отсчёта m = n − whole − first − w
    private static double[] Weights(double fraction)
    {
        var weights = new double[2 * HalfWidth];

        for (int w = 0; w < weights.Length; w++)
        {
            // Для m = n − whole − first − w расстояние t = (n − whole − fraction) − m = first + w − fraction
            double t = (-HalfWidth + 1 + w) - fraction;
            weights[w] = Kernel(t);
        }

        return weights;
    }

    private static double Kernel(double t)
    {
        if (Math.Abs(t) < 1e-12)
            return 1;

        if (Math.Abs(t) >= HalfWidth)
            return 0;

        double pt = Math.PI * t;

        return HalfWidth * Math.Sin(pt) * Math.Sin(pt / HalfWidth) / (pt * pt);
    }
}
