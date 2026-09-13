using System.Numerics;
using AI.Insights;
using AI.Statistics;
using Vector3 = AI.Geometry.Primitives.Vector3;

namespace AI.Microwave.Propagation;

/// <summary>Крупномасштабные параметры одной реализации канала TR 38.901</summary>
/// <param name="DelaySpreadSeconds">Разброс задержек DS, с</param>
/// <param name="AzimuthSpreadDepartureDeg">Азимутальный разброс ухода ASD, градусы, не больше 104</param>
/// <param name="AzimuthSpreadArrivalDeg">Азимутальный разброс прихода ASA, градусы, не больше 104</param>
/// <param name="ZenithSpreadDepartureDeg">Зенитный разброс ухода ZSD, градусы, не больше 52</param>
/// <param name="ZenithSpreadArrivalDeg">Зенитный разброс прихода ZSA, градусы, не больше 52</param>
/// <param name="KFactorDb">K-фактор, дБ; NaN без прямой видимости</param>
/// <param name="ShadowFadingDb">Затенение, дБ: положительное — сигнал сильнее медианы потерь</param>
public sealed record LargeScaleParameters(
    double DelaySpreadSeconds,
    double AzimuthSpreadDepartureDeg,
    double AzimuthSpreadArrivalDeg,
    double ZenithSpreadDepartureDeg,
    double ZenithSpreadArrivalDeg,
    double KFactorDb,
    double ShadowFadingDb);

/// <summary>Кластер реализации TR 38.901</summary>
/// <param name="DelaySeconds">Задержка относительно прямого пути, с (с прямой видимостью — уже умноженная на 1/C_τ)</param>
/// <param name="Power">Доля мощности рассеянной части канала</param>
/// <param name="AzimuthOfArrivalDeg">Азимут прихода, градусы</param>
/// <param name="ZenithOfArrivalDeg">Зенит прихода, градусы</param>
/// <param name="AzimuthOfDepartureDeg">Азимут ухода, градусы</param>
/// <param name="ZenithOfDepartureDeg">Зенит ухода, градусы</param>
public sealed record Tr38901Cluster(
    double DelaySeconds,
    double Power,
    double AzimuthOfArrivalDeg,
    double ZenithOfArrivalDeg,
    double AzimuthOfDepartureDeg,
    double ZenithOfDepartureDeg)
{
    /// <summary>Один из двух сильнейших кластеров, разделённых на три подкластера по задержке</summary>
    public bool IsSplit { get; init; }
}

/// <summary>Луч канала MIMO: задержка, доплер и матрица коэффициентов «приёмный элемент × передающий элемент»</summary>
/// <param name="DelaySeconds">Задержка распространения, с</param>
/// <param name="DopplerHz">Мгновенный доплеровский сдвиг, Гц</param>
/// <param name="Coefficients">Коэффициенты [элемент абонента, элемент базовой станции] с потерями на трассе</param>
/// <param name="Kind">Прямой путь или рассеянный</param>
public sealed record MimoPath(double DelaySeconds, double DopplerHz, Complex[,] Coefficients, PathKind Kind)
{
    /// <summary>Азимут прихода к абоненту, градусы</summary>
    public double AzimuthOfArrivalDeg { get; init; }

    /// <summary>Зенит прихода к абоненту, градусы</summary>
    public double ZenithOfArrivalDeg { get; init; }

    /// <summary>Азимут ухода от базовой станции, градусы</summary>
    public double AzimuthOfDepartureDeg { get; init; }

    /// <summary>Зенит ухода от базовой станции, градусы</summary>
    public double ZenithOfDepartureDeg { get; init; }

    /// <summary>Длина пути, м</summary>
    public double LengthMetres { get; init; }

    /// <summary>Номер кластера; прямой путь относится к первому</summary>
    public int Cluster { get; init; }
}

/// <summary>
/// Геометрическая стохастическая модель канала TR 38.901 v16.1 (раздел 7.5) — аналог QuaDRiGa: кластеры и лучи
/// с углами прихода и ухода, поляризацией и диаграммами антенных решёток, канал MIMO во времени и по частоте.
/// </summary>
/// <remarks>
/// <para>
/// Канал разыгрывается по шагам стандарта. Сценарий даёт состояние прямой видимости, потери на трассе и
/// крупномасштабные параметры — разбросы задержек и углов, K-фактор, затенение — как коррелированные
/// логнормальные величины. Задержки кластеров экспоненциальны с масштабом r_τ·DS, мощности спадают с задержкой
/// и затеняются независимо; углы кластеров строятся обращением гауссова (азимут) и лапласова (зенит) профиля
/// мощности, внутри кластера — 20 лучей со смещениями из таблицы 7.5-3, случайно сопоставленными между собой.
/// Каждый луч получает кроссполяризационное отношение и четыре случайные фазы. Два сильнейших кластера делятся
/// на три подкластера с задержками +0, +1,28·c_DS, +2,56·c_DS.
/// </para>
/// <para>
/// Коэффициент луча — формула (7.5-22): поле приёмного элемента, поляризационная матрица, поле передающего
/// элемента и фазы положения элементов решёток. Прямой путь — формула (7.5-29) с матрицей diag(1, −1).
/// </para>
/// <para>
/// Движение абонента — как в QuaDRiGa, через последний рассеиватель. Для каждого луча по углу прихода и длине
/// пути находится точка однократного отражения на эллипсоиде с фокусами в антеннах; при движении абонента она
/// неподвижна, и угол прихода, задержка и фаза пересчитываются по геометрии. Угол ухода от базовой станции
/// сохраняется. В первом порядке по времени это совпадает с доплеровским множителем стандарта
/// <c>exp(j2π·r̂_rx·v·t/λ)</c>, но остаётся согласованным на десятках метров пути. Прямой путь пересчитывается
/// точно.
/// </para>
/// </remarks>
public sealed class Tr38901ChannelModel
{
    /// <summary>Создаёт модель</summary>
    /// <param name="scenario">Сценарий</param>
    /// <param name="carrierFrequencyHz">Несущая, Гц, от 0,5 до 100 ГГц</param>
    public Tr38901ChannelModel(Tr38901Scenario scenario, double carrierFrequencyHz)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        if (!(carrierFrequencyHz >= 0.5e9 && carrierFrequencyHz <= 100e9))
            throw new ArgumentOutOfRangeException(nameof(carrierFrequencyHz), carrierFrequencyHz, "Модель TR 38.901 определена на 0,5–100 ГГц");

        Scenario = scenario;
        CarrierFrequencyHz = carrierFrequencyHz;
    }

    /// <summary>Сценарий</summary>
    public Tr38901Scenario Scenario { get; }

    /// <summary>Несущая, Гц</summary>
    public double CarrierFrequencyHz { get; }

    /// <summary>Длина волны, м</summary>
    public double WavelengthM => Wave.SpeedOfLight / CarrierFrequencyHz;

    /// <summary>Решётка базовой станции; по умолчанию изотропный элемент</summary>
    public AntennaArray BaseStationArray { get; init; } = AntennaArray.Isotropic;

    /// <summary>Решётка абонента; по умолчанию изотропный элемент</summary>
    public AntennaArray TerminalArray { get; init; } = AntennaArray.Isotropic;

    /// <summary>Принудительное состояние прямой видимости; null — по вероятности сценария</summary>
    public bool? LineOfSight { get; init; }

    /// <summary>
    /// Розыгрыш обстановки вокруг базовой станции: карты крупномасштабных параметров и состояния прямой
    /// видимости, общие для всех абонентов розыгрыша
    /// </summary>
    /// <param name="baseStation">Положение базовой станции, м; Z — высота</param>
    /// <param name="rng">Генератор случайных чисел</param>
    /// <param name="spatiallyConsistent">
    /// Строить карты (соседние абоненты видят близкие параметры); иначе параметры каждого абонента независимы
    /// </param>
    public Tr38901Drop CreateDrop(Vector3 baseStation, Random rng, bool spatiallyConsistent = true)
        => new(this, baseStation, rng, spatiallyConsistent);

    /// <summary>Одна независимая реализация канала между базовой станцией и абонентом</summary>
    /// <param name="baseStation">Положение базовой станции, м</param>
    /// <param name="terminal">Положение абонента, м</param>
    /// <param name="rng">Генератор случайных чисел</param>
    /// <param name="terminalVelocity">Скорость абонента, м/с</param>
    public Tr38901Channel Generate(Vector3 baseStation, Vector3 terminal, Random rng, Vector3 terminalVelocity = default)
        => CreateDrop(baseStation, rng, spatiallyConsistent: false).Link(terminal, terminalVelocity);
}

/// <summary>
/// Розыгрыш (drop) TR 38.901: базовая станция и карты крупномасштабных параметров вокруг неё.
/// </summary>
/// <remarks>
/// Пространственная согласованность (раздел 7.6.3): каждый из семи параметров — гауссово поле
/// <see cref="SpatialRandomField"/> с расстоянием корреляции из таблицы сценария, поля перемешиваются множителем
/// Холецкого матрицы взаимных корреляций. Для прямой видимости и без неё карты свои. Состояние прямой видимости —
/// сравнение вероятности сценария с равномерной величиной Φ(g), где g — ещё одно поле. Поэтому абоненты в метре
/// друг от друга видят почти одинаковые параметры, а в сотнях метров — независимые.
/// </remarks>
public sealed class Tr38901Drop
{
    private const int ParameterCount = 7;

    private readonly Random _rng;
    private readonly SpatialRandomField? _lineOfSightField;
    private readonly SpatialRandomField[]? _losFields;
    private readonly SpatialRandomField[]? _nlosFields;

    internal Tr38901Drop(Tr38901ChannelModel model, Vector3 baseStation, Random rng, bool spatiallyConsistent)
    {
        ArgumentNullException.ThrowIfNull(rng);

        Model = model;
        BaseStation = baseStation;
        IsSpatiallyConsistent = spatiallyConsistent;
        _rng = rng;

        if (!spatiallyConsistent)
            return;

        _lineOfSightField = new SpatialRandomField(model.Scenario.LineOfSightCorrelationDistanceM, rng);
        _losFields = Fields(model.Scenario.LineOfSight, rng);
        _nlosFields = Fields(model.Scenario.NonLineOfSight, rng);
    }

    /// <summary>Модель</summary>
    public Tr38901ChannelModel Model { get; }

    /// <summary>Положение базовой станции, м</summary>
    public Vector3 BaseStation { get; }

    /// <summary>Построены ли карты; без них каждый вызов даёт новую независимую реализацию</summary>
    public bool IsSpatiallyConsistent { get; }

    /// <summary>Есть ли прямая видимость в точке</summary>
    /// <param name="terminal">Положение абонента, м</param>
    public bool IsLineOfSight(Vector3 terminal)
    {
        if (Model.LineOfSight is bool forced)
            return forced;

        double probability = Model.Scenario.LineOfSightProbability(Distance2D(terminal), terminal.Z);
        double uniform = _lineOfSightField is null
            ? _rng.NextDouble()
            : StatInference.NormalCdf(_lineOfSightField.Value(terminal.X, terminal.Y));

        return uniform < probability;
    }

    /// <summary>Крупномасштабные параметры в точке</summary>
    /// <param name="terminal">Положение абонента, м</param>
    /// <param name="lineOfSight">Состояние прямой видимости</param>
    public LargeScaleParameters LargeScale(Vector3 terminal, bool lineOfSight)
    {
        Tr38901Scenario scenario = Model.Scenario;
        Tr38901Parameters p = scenario.Parameters(lineOfSight);
        double carrier = Model.CarrierFrequencyHz;
        double f = scenario.LargeScaleFrequencyGHz(carrier);
        double d2 = Distance2D(terminal);
        SpatialRandomField[]? fields = lineOfSight ? _losFields : _nlosFields;

        var independent = new double[ParameterCount];

        for (int i = 0; i < ParameterCount; i++)
            independent[i] = fields is null ? RandomEngine.NextGaussian(_rng) : fields[i].Value(terminal.X, terminal.Y);

        double[,] factor = p.CorrelationFactor;

        double Normal(LargeScaleParameter parameter)
        {
            int i = (int)parameter;
            double sum = 0;

            for (int j = 0; j <= i; j++)
                sum += factor[i, j] * independent[j];

            return sum;
        }

        double lgDs = p.DelaySpreadMean.At(f) + (p.DelaySpreadSigma.At(f) * Normal(LargeScaleParameter.DelaySpread));
        double lgAsd = p.AzimuthSpreadDepartureMean.At(f) + (p.AzimuthSpreadDepartureSigma.At(f) * Normal(LargeScaleParameter.AzimuthSpreadDeparture));
        double lgAsa = p.AzimuthSpreadArrivalMean.At(f) + (p.AzimuthSpreadArrivalSigma.At(f) * Normal(LargeScaleParameter.AzimuthSpreadArrival));
        double lgZsa = p.ZenithSpreadArrivalMean.At(f) + (p.ZenithSpreadArrivalSigma.At(f) * Normal(LargeScaleParameter.ZenithSpreadArrival));
        double lgZsd = scenario.ZenithSpreadDepartureMean(lineOfSight, d2, BaseStation.Z, terminal.Z, carrier)
            + (p.ZenithSpreadDepartureSigma.At(f) * Normal(LargeScaleParameter.ZenithSpreadDeparture));
        double k = lineOfSight ? p.KFactorMeanDb + (p.KFactorSigmaDb * Normal(LargeScaleParameter.KFactor)) : double.NaN;
        double sf = scenario.ShadowFadingSigmaDb(lineOfSight, d2, BaseStation.Z, terminal.Z, carrier) * Normal(LargeScaleParameter.ShadowFading);

        return new LargeScaleParameters(
            Math.Pow(10, lgDs),
            Math.Min(Math.Pow(10, lgAsd), 104),
            Math.Min(Math.Pow(10, lgAsa), 104),
            Math.Min(Math.Pow(10, lgZsd), 52),
            Math.Min(Math.Pow(10, lgZsa), 52),
            k,
            sf);
    }

    /// <summary>Канал к абоненту: состояние и крупномасштабные параметры из карт, мелкомасштабные — заново</summary>
    /// <param name="terminal">Положение абонента, м; Z — высота</param>
    /// <param name="terminalVelocity">Скорость абонента, м/с</param>
    public Tr38901Channel Link(Vector3 terminal, Vector3 terminalVelocity = default)
    {
        bool lineOfSight = IsLineOfSight(terminal);

        return new Tr38901Channel(this, terminal, terminalVelocity, lineOfSight, LargeScale(terminal, lineOfSight), _rng);
    }

    private double Distance2D(Vector3 terminal)
    {
        double dx = terminal.X - BaseStation.X, dy = terminal.Y - BaseStation.Y;

        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static SpatialRandomField[] Fields(Tr38901Parameters parameters, Random rng)
        => Enumerable.Range(0, ParameterCount)
            .Select(i => new SpatialRandomField(parameters.CorrelationDistanceM((LargeScaleParameter)i), rng))
            .ToArray();
}

/// <summary>
/// Реализация канала TR 38.901 между базовой станцией и абонентом: кластеры, лучи, матрицы MIMO во времени.
/// </summary>
/// <remarks>
/// Коэффициенты включают потери на трассе и затенение: мощность канала при изотропных антеннах равна
/// <c>10^((−PL + SF)/10)</c>. Задержки абсолютные — от момента излучения, как у <see cref="MultipathChannel"/>,
/// в который реализация переводится для одной пары элементов (<see cref="ToMultipathChannel"/>).
/// </remarks>
public sealed class Tr38901Channel : IInterpretable
{
    // Смещения лучей внутри кластера, таблица 7.5-3
    private static readonly double[] RayOffsets =
    [
        0.0447, -0.0447, 0.1413, -0.1413, 0.2492, -0.2492, 0.3715, -0.3715, 0.5129, -0.5129,
        0.6797, -0.6797, 0.8844, -0.8844, 1.1481, -1.1481, 1.5195, -1.5195, 2.1551, -2.1551,
    ];

    // Лучи подкластеров двух сильнейших кластеров (таблица 7.5-5), номера с нуля; мощности 10/20, 6/20, 4/20
    private static readonly int[][] SubclusterRays = [[0, 1, 2, 3, 4, 5, 6, 7, 18, 19], [8, 9, 10, 11, 16, 17], [12, 13, 14, 15]];
    private static readonly int[] AllRays = Enumerable.Range(0, 20).ToArray();
    private static readonly double[] SubclusterDelayFactors = [0, 1.28, 2.56];

    // Ближе этого рассеиватель к абоненту не ставится: иначе угол прихода крутится при малейшем движении
    private const double MinimumScattererDistanceM = 10;

    private readonly Tr38901Cluster[] _clusters;
    private readonly Ray[] _rays;
    private readonly double[,] _txRotation;
    private readonly double[,] _rxRotation;
    private readonly Vector3[] _txOffsets;
    private readonly Vector3[] _rxOffsets;

    internal Tr38901Channel(Tr38901Drop drop, Vector3 terminal, Vector3 terminalVelocity, bool lineOfSight, LargeScaleParameters largeScale, Random rng)
    {
        Tr38901ChannelModel model = drop.Model;

        Scenario = model.Scenario;
        CarrierFrequencyHz = model.CarrierFrequencyHz;
        BaseStationArray = model.BaseStationArray;
        TerminalArray = model.TerminalArray;
        BaseStation = drop.BaseStation;
        Terminal = terminal;
        TerminalVelocity = terminalVelocity;
        IsLineOfSight = lineOfSight;
        LargeScale = largeScale;

        Vector3 link = terminal - BaseStation;
        Distance2DM = Math.Sqrt((link.X * link.X) + (link.Y * link.Y));
        Distance3DM = link.Length;
        Wave.RequirePositive(Distance3DM, nameof(terminal));

        EnvironmentHeightM = Scenario.SampleEnvironmentHeight(Distance2DM, terminal.Z, rng);
        PathLossDb = Scenario.PathLossDb(lineOfSight, Distance2DM, BaseStation.Z, terminal.Z, CarrierFrequencyHz, EnvironmentHeightM);

        _txRotation = BaseStationArray.RotationMatrix();
        _rxRotation = TerminalArray.RotationMatrix();
        _txOffsets = Offsets(BaseStationArray, _txRotation);
        _rxOffsets = Offsets(TerminalArray, _rxRotation);

        (_clusters, _rays) = Generate(link, rng);
    }

    /// <summary>Сценарий</summary>
    public Tr38901Scenario Scenario { get; }

    /// <summary>Несущая, Гц</summary>
    public double CarrierFrequencyHz { get; }

    /// <summary>Длина волны, м</summary>
    public double WavelengthM => Wave.SpeedOfLight / CarrierFrequencyHz;

    /// <summary>Решётка базовой станции</summary>
    public AntennaArray BaseStationArray { get; }

    /// <summary>Решётка абонента</summary>
    public AntennaArray TerminalArray { get; }

    /// <summary>Положение базовой станции, м</summary>
    public Vector3 BaseStation { get; }

    /// <summary>Положение абонента в момент 0, м</summary>
    public Vector3 Terminal { get; }

    /// <summary>Скорость абонента, м/с</summary>
    public Vector3 TerminalVelocity { get; }

    /// <summary>Есть ли прямая видимость</summary>
    public bool IsLineOfSight { get; }

    /// <summary>Расстояние по горизонтали, м</summary>
    public double Distance2DM { get; }

    /// <summary>Расстояние по прямой, м</summary>
    public double Distance3DM { get; }

    /// <summary>Эффективная высота окружения h_E, м (разыгрывается у UMa)</summary>
    public double EnvironmentHeightM { get; }

    /// <summary>Потери на трассе, дБ</summary>
    public double PathLossDb { get; }

    /// <summary>Затенение, дБ</summary>
    public double ShadowFadingDb => LargeScale.ShadowFadingDb;

    /// <summary>Среднее усиление канала при изотропных антеннах, дБ: −PL + SF</summary>
    public double PathGainDb => ShadowFadingDb - PathLossDb;

    /// <summary>Крупномасштабные параметры</summary>
    public LargeScaleParameters LargeScale { get; }

    /// <summary>Кластеры, оставшиеся после отбрасывания слабых, по возрастанию задержки</summary>
    public IReadOnlyList<Tr38901Cluster> Clusters => _clusters;

    /// <summary>Число лучей вместе с прямым путём</summary>
    public int RayCount => _rays.Length;

    /// <summary>Положение абонента в момент t, м</summary>
    /// <param name="timeSeconds">Время, с</param>
    public Vector3 TerminalAt(double timeSeconds) => Terminal + (timeSeconds * TerminalVelocity);

    /// <summary>Лучи с матрицами коэффициентов в момент t</summary>
    /// <param name="timeSeconds">Время, с: абонент смещён на v·t, лучи пересчитаны по последним рассеивателям</param>
    public IReadOnlyList<MimoPath> Paths(double timeSeconds = 0)
    {
        if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds))
            throw new ArgumentOutOfRangeException(nameof(timeSeconds), timeSeconds, "Время должно быть конечным");

        Vector3 terminal = TerminalAt(timeSeconds);
        double wavelength = WavelengthM;
        double scale = Math.Pow(10, PathGainDb / 20);
        int receivers = TerminalArray.ElementCount, transmitters = BaseStationArray.ElementCount;
        var txTheta = new double[transmitters];
        var txPhi = new double[transmitters];
        var txSteering = new Complex[transmitters];
        var paths = new MimoPath[_rays.Length];

        for (int r = 0; r < _rays.Length; r++)
        {
            Ray ray = _rays[r];
            (double length, Vector3 arrival, Vector3 departure) = Geometry(ray, terminal);

            // Прямой путь — полный набег фазы; рассеянный — случайная фаза плюс изменение длины при движении
            double cycles = (ray.LineOfSight ? length : length - ray.LengthM) / wavelength;
            Complex common = Complex.FromPolarCoordinates(scale * Math.Sqrt(ray.Power), -2 * Math.PI * (cycles % 1.0));

            for (int s = 0; s < transmitters; s++)
            {
                (txTheta[s], txPhi[s]) = BaseStationArray.FieldPattern(_txRotation, s, departure);
                txSteering[s] = Complex.FromPolarCoordinates(1, 2 * Math.PI * departure.Dot(_txOffsets[s]));
            }

            var h = new Complex[receivers, transmitters];

            for (int u = 0; u < receivers; u++)
            {
                (double rxTheta, double rxPhi) = TerminalArray.FieldPattern(_rxRotation, u, arrival);
                Complex rxSteering = Complex.FromPolarCoordinates(1, 2 * Math.PI * arrival.Dot(_rxOffsets[u]));

                for (int s = 0; s < transmitters; s++)
                {
                    Complex polarization = (rxTheta * ((ray.ThetaTheta * txTheta[s]) + (ray.ThetaPhi * txPhi[s])))
                        + (rxPhi * ((ray.PhiTheta * txTheta[s]) + (ray.PhiPhi * txPhi[s])));

                    h[u, s] = common * polarization * rxSteering * txSteering[s];
                }
            }

            (double zoa, double aoa) = Spherical.Angles(arrival);
            (double zod, double aod) = Spherical.Angles(departure);

            paths[r] = new MimoPath(
                length / Wave.SpeedOfLight,
                arrival.Dot(TerminalVelocity) / wavelength,
                h,
                ray.LineOfSight ? PathKind.LineOfSight : PathKind.Scattered)
            {
                AzimuthOfArrivalDeg = aoa,
                ZenithOfArrivalDeg = zoa,
                AzimuthOfDepartureDeg = aod,
                ZenithOfDepartureDeg = zod,
                LengthMetres = length,
                Cluster = ray.Cluster,
            };
        }

        return paths;
    }

    /// <summary>
    /// Канал одной пары элементов как <see cref="MultipathChannel"/>: разброс задержек, полоса и время
    /// когерентности, импульсная характеристика, прохождение сигнала
    /// </summary>
    /// <param name="timeSeconds">Время, с</param>
    /// <param name="receiveElement">Элемент абонента</param>
    /// <param name="transmitElement">Элемент базовой станции</param>
    public MultipathChannel ToMultipathChannel(double timeSeconds = 0, int receiveElement = 0, int transmitElement = 0)
    {
        CheckElement(receiveElement, TerminalArray, nameof(receiveElement));
        CheckElement(transmitElement, BaseStationArray, nameof(transmitElement));

        return new MultipathChannel(
            Paths(timeSeconds).Select(p => new PropagationPath(p.DelaySeconds, p.Coefficients[receiveElement, transmitElement], p.DopplerHz, p.Kind)
            {
                AzimuthOfArrivalDeg = p.AzimuthOfArrivalDeg,
                ElevationOfArrivalDeg = 90 - p.ZenithOfArrivalDeg,
                AzimuthOfDepartureDeg = p.AzimuthOfDepartureDeg,
                ElevationOfDepartureDeg = 90 - p.ZenithOfDepartureDeg,
                LengthMetres = p.LengthMetres,
            }),
            CarrierFrequencyHz);
    }

    /// <summary>Матрица канала H(f, t) размером «элементы абонента × элементы базовой станции»</summary>
    /// <param name="frequencyOffsetHz">Отстройка от несущей, Гц</param>
    /// <param name="timeSeconds">Время, с</param>
    public Complex[,] ChannelMatrix(double frequencyOffsetHz = 0, double timeSeconds = 0)
    {
        int receivers = TerminalArray.ElementCount, transmitters = BaseStationArray.ElementCount;
        var h = new Complex[receivers, transmitters];

        foreach (MimoPath path in Paths(timeSeconds))
        {
            Complex rotation = Complex.FromPolarCoordinates(1, -2 * Math.PI * frequencyOffsetHz * path.DelaySeconds);

            for (int u = 0; u < receivers; u++)
            {
                for (int s = 0; s < transmitters; s++)
                    h[u, s] += path.Coefficients[u, s] * rotation;
            }
        }

        return h;
    }

    /// <summary>
    /// Ёмкость канала MIMO без знания канала на передаче: log₂det(I + ρ/N_t·H·Hᴴ), бит/с/Гц
    /// </summary>
    /// <param name="snrDb">
    /// Отношение сигнал/шум ρ на приёмной антенне без потерь на трассе и затенения — то есть при изотропных
    /// антеннах это среднее SNR; усиление диаграмм входит в канал
    /// </param>
    /// <param name="frequencyOffsetHz">Отстройка от несущей, Гц</param>
    /// <param name="timeSeconds">Время, с</param>
    public double CapacityBitsPerHz(double snrDb, double frequencyOffsetHz = 0, double timeSeconds = 0)
    {
        Complex[,] h = ChannelMatrix(frequencyOffsetHz, timeSeconds);
        int receivers = h.GetLength(0), transmitters = h.GetLength(1);
        double scale = Math.Pow(10, (snrDb - PathGainDb) / 10) / transmitters;

        // det(I + c·H·Hᴴ) = det(I + c·Hᴴ·H): берётся меньшая из двух матриц Грама
        bool byColumns = receivers > transmitters;
        int size = Math.Min(receivers, transmitters), inner = Math.Max(receivers, transmitters);
        var gram = new Complex[size, size];

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                Complex sum = Complex.Zero;

                for (int k = 0; k < inner; k++)
                {
                    sum += byColumns
                        ? Complex.Conjugate(h[k, i]) * h[k, j]
                        : h[i, k] * Complex.Conjugate(h[j, k]);
                }

                gram[i, j] = (i == j ? 1 : 0) + (scale * sum);
            }
        }

        return LogDeterminant(gram) / Math.Log(2);
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        MultipathChannel siso = ToMultipathChannel();
        double spread = siso.RmsDelaySpreadSeconds;
        int receivers = TerminalArray.ElementCount, transmitters = BaseStationArray.ElementCount;
        bool mimo = receivers > 1 || transmitters > 1;
        double capacity = CapacityBitsPerHz(10);
        double siso10 = Math.Log2(1 + 10);

        return new InterpretationBuilder($"Канал TR 38.901: {Scenario.Name}")
            .Summary($"{(IsLineOfSight ? "Прямая видимость" : "Прямой видимости нет")}, до базовой станции {Fmt.Num(Distance2DM, 0)} м по горизонтали. "
                + $"Потери на трассе {Fmt.Num(PathLossDb, 1)} дБ, затенение {Fmt.Num(ShadowFadingDb, 1)} дБ; "
                + $"кластеров {Clusters.Count}, лучей {RayCount}, разброс задержек реализации {Fmt.Num(spread * 1e9, 0)} нс"
                + (IsLineOfSight ? $", K-фактор {Fmt.Num(LargeScale.KFactorDb, 1)} дБ." : "."))
            .Metric("Потери на трассе", Fmt.Num(PathLossDb, 2), "дБ", "формула сценария, таблица 7.4.1-1")
            .Metric("Затенение", Fmt.Num(ShadowFadingDb, 2), "дБ", "положительное — сигнал сильнее медианы")
            .Metric("Разброс задержек DS", Fmt.Num(LargeScale.DelaySpreadSeconds * 1e9, 1), "нс", "крупномасштабный параметр")
            .Metric("Разброс задержек реализации", Fmt.Num(spread * 1e9, 1), "нс", "по лучам с диаграммами антенн")
            .Metric("Азимутальный разброс прихода ASA", Fmt.Num(LargeScale.AzimuthSpreadArrivalDeg, 1), "°", null)
            .Metric("Азимутальный разброс ухода ASD", Fmt.Num(LargeScale.AzimuthSpreadDepartureDeg, 1), "°", null)
            .Metric("Зенитный разброс прихода ZSA", Fmt.Num(LargeScale.ZenithSpreadArrivalDeg, 1), "°", null)
            .Metric("Зенитный разброс ухода ZSD", Fmt.Num(LargeScale.ZenithSpreadDepartureDeg, 1), "°", null)
            .Metric(mimo ? $"Ёмкость MIMO {receivers}×{transmitters} при SNR 10 дБ" : "Ёмкость при SNR 10 дБ", Fmt.Num(capacity, 2), "бит/с/Гц", "SNR без потерь на трассе")
            .FindingIf(mimo && capacity > 1.5 * siso10,
                $"Канал держит несколько параллельных потоков: ёмкость {Fmt.Num(capacity, 1)} бит/с/Гц против {Fmt.Num(siso10, 1)} "
                + "у одиночной антенны при том же SNR — рассеяние по углам развязывает элементы решёток.")
            .FindingIf(LargeScale.AzimuthSpreadDepartureDeg < 10,
                $"Разброс углов ухода от базовой станции мал ({Fmt.Num(LargeScale.AzimuthSpreadDepartureDeg, 1)}°): элементы решётки "
                + "с шагом λ/2 сильно коррелированы, выигрыш даёт формирование луча, а не пространственное мультиплексирование.")
            .Warning("Модель не учитывает проникновение в здания (O2I), промышленные сценарии InF, рождение и гибель кластеров "
                + "при перемещениях дальше десятков метров и согласованность мелкомасштабных параметров между разными абонентами.")
            .Build();
    }

    private static Vector3[] Offsets(AntennaArray array, double[,] rotation)
    {
        // Смещения в длинах волн: фаза элемента 2π·(r̂·d/λ) получается прямо скалярным произведением
        var offsets = new Vector3[array.ElementCount];

        for (int i = 0; i < offsets.Length; i++)
            offsets[i] = array.ElementOffset(rotation, i, 1);

        return offsets;
    }

    private static void CheckElement(int element, AntennaArray array, string name)
    {
        if (element < 0 || element >= array.ElementCount)
            throw new ArgumentOutOfRangeException(name, element, $"Номер элемента должен быть от 0 до {array.ElementCount - 1}");
    }

    private (double Length, Vector3 Arrival, Vector3 Departure) Geometry(Ray ray, Vector3 terminal)
    {
        if (ray.LineOfSight)
        {
            Vector3 toBase = BaseStation - terminal;
            double length = toBase.Length;

            return (length, toBase / length, -toBase / length);
        }

        Vector3 toScatterer = ray.Scatterer - terminal;
        double last = toScatterer.Length;

        return last > 0
            ? (ray.LengthM - ray.ScattererDistanceM + last, toScatterer / last, ray.Departure)
            : (ray.LengthM - ray.ScattererDistanceM, ray.Arrival, ray.Departure);
    }

    private (Tr38901Cluster[] Clusters, Ray[] Rays) Generate(Vector3 link, Random rng)
    {
        Tr38901Parameters p = Scenario.Parameters(IsLineOfSight);
        double frequency = Scenario.LargeScaleFrequencyGHz(CarrierFrequencyHz);
        int count = p.ClusterCount, rays = p.RaysPerCluster;
        double spread = LargeScale.DelaySpreadSeconds, scaling = p.DelayScaling;

        // Поправки на прямую видимость — полиномы по K в дБ. Это аппроксимации: ниже −10 дБ поправка C_θ проходит
        // через ноль, поэтому K для них ограничен снизу −5 дБ, где прямая составляющая уже мала
        double k = IsLineOfSight ? Math.Max(LargeScale.KFactorDb, -5) : 0;
        double kLinear = IsLineOfSight ? Math.Pow(10, LargeScale.KFactorDb / 10) : 0;

        // Шаг 5: задержки кластеров — экспоненциальные с масштабом r_τ·DS, от нуля по возрастанию
        var delays = new double[count];

        for (int n = 0; n < count; n++)
            delays[n] = -scaling * spread * Math.Log(1 - rng.NextDouble());

        double earliest = delays.Min();

        for (int n = 0; n < count; n++)
            delays[n] -= earliest;

        Array.Sort(delays);

        // Шаг 6: мощности — экспоненциальный спад по задержке и затенение кластера
        var powers = new double[count];

        for (int n = 0; n < count; n++)
        {
            powers[n] = Math.Exp(-delays[n] * (scaling - 1) / (scaling * spread))
                * Math.Pow(10, -RandomEngine.NextGaussian(rng, 0, p.ClusterShadowingDb) / 10);
        }

        double total = powers.Sum();

        for (int n = 0; n < count; n++)
            powers[n] /= total;

        // Для углов с прямой видимостью первый кластер получает и мощность прямого пути
        var anglePowers = new double[count];

        for (int n = 0; n < count; n++)
            anglePowers[n] = IsLineOfSight ? (powers[n] / (kLinear + 1)) + (n == 0 ? kLinear / (kLinear + 1) : 0) : powers[n];

        (double losZod, double losAod) = Spherical.Angles(link / Distance3DM);
        (double losZoa, double losAoa) = Spherical.Angles(-link / Distance3DM);

        // Шаг 7: углы кластеров
        double phiScaling = p.AzimuthScaling * (IsLineOfSight ? 1.1035 - (0.028 * k) - (0.002 * k * k) + (0.0001 * k * k * k) : 1);
        double thetaScaling = p.ZenithScaling * (IsLineOfSight ? 1.3086 + (0.0339 * k) - (0.0077 * k * k) + (0.0002 * k * k * k) : 1);
        double zsdMean = Scenario.ZenithSpreadDepartureMean(IsLineOfSight, Distance2DM, BaseStation.Z, Terminal.Z, CarrierFrequencyHz);
        double zodOffset = Scenario.ZenithOffsetDepartureDeg(IsLineOfSight, Distance2DM, Terminal.Z, CarrierFrequencyHz);

        double[] aoa = Azimuths(anglePowers, LargeScale.AzimuthSpreadArrivalDeg, phiScaling, losAoa, IsLineOfSight, rng);
        double[] aod = Azimuths(anglePowers, LargeScale.AzimuthSpreadDepartureDeg, phiScaling, losAod, IsLineOfSight, rng);
        double[] zoa = Zeniths(anglePowers, LargeScale.ZenithSpreadArrivalDeg, thetaScaling, losZoa, 0, IsLineOfSight, rng);
        double[] zod = Zeniths(anglePowers, LargeScale.ZenithSpreadDepartureDeg, thetaScaling, losZod, zodOffset, IsLineOfSight, rng);

        // Кластеры слабее сильнейшего на 25 дБ отбрасываются без перенормировки; первый остаётся всегда
        double threshold = powers.Max() * Math.Pow(10, -2.5);
        int[] kept = Enumerable.Range(0, count).Where(n => n == 0 || powers[n] >= threshold).ToArray();
        int[] strongest = kept.OrderByDescending(n => powers[n]).Take(2).ToArray();

        double delayScale = IsLineOfSight ? 1 / (0.7705 - (0.0433 * k) + (0.0002 * k * k) + (0.000017 * k * k * k)) : 1;
        double clusterDelaySpread = p.ClusterDelaySpreadNs(frequency) * 1e-9;
        double zodRaySpread = 3.0 / 8.0 * Math.Pow(10, zsdMean);
        double scatteredShare = IsLineOfSight ? 1 / (kLinear + 1) : 1;
        Vector3 toBase = -link;

        var clusters = new List<Tr38901Cluster>(kept.Length);
        var result = new List<Ray>((kept.Length * rays) + 1);

        if (IsLineOfSight)
        {
            result.Add(new Ray
            {
                LineOfSight = true,
                Power = kLinear / (kLinear + 1),
                LengthM = Distance3DM,
                Departure = link / Distance3DM,
                Arrival = toBase / Distance3DM,
                ThetaTheta = 1,
                PhiPhi = -1,
            });
        }

        foreach (int n in kept)
        {
            bool split = strongest.Contains(n);
            double delay = delays[n] * delayScale;

            clusters.Add(new Tr38901Cluster(
                delay,
                powers[n],
                Spherical.WrapAzimuth(aoa[n]),
                Spherical.FoldZenith(zoa[n]),
                Spherical.WrapAzimuth(aod[n]),
                Spherical.FoldZenith(zod[n]))
            {
                IsSplit = split,
            });

            int[][] groups = split ? SubclusterRays : [AllRays];

            for (int g = 0; g < groups.Length; g++)
            {
                // Шаг 8: лучи сопоставляются случайно внутри кластера, у сильнейших — внутри подкластера
                int[] members = groups[g];
                int[] aodOrder = Shuffled(members, rng), zoaOrder = Shuffled(members, rng), zodOrder = Shuffled(members, rng);
                double rayDelay = delay + (split ? SubclusterDelayFactors[g] * clusterDelaySpread : 0);
                double length = Distance3DM + (Wave.SpeedOfLight * rayDelay);

                for (int i = 0; i < members.Length; i++)
                {
                    Vector3 arrival = Spherical.Direction(
                        Spherical.FoldZenith(zoa[n] + (p.ClusterZenithSpreadArrivalDeg * RayOffsets[zoaOrder[i]])),
                        aoa[n] + (p.ClusterAzimuthSpreadArrivalDeg * RayOffsets[members[i]]));
                    Vector3 departure = Spherical.Direction(
                        Spherical.FoldZenith(zod[n] + (zodRaySpread * RayOffsets[zodOrder[i]])),
                        aod[n] + (p.ClusterAzimuthSpreadDepartureDeg * RayOffsets[aodOrder[i]]));

                    // Шаги 9–10: кроссполяризационное отношение κ и четыре случайные фазы; √(1/κ) = 10^(−X/20)
                    double crossPolar = Math.Pow(10, -RandomEngine.NextGaussian(rng, p.CrossPolarizationMeanDb, p.CrossPolarizationSigmaDb) / 20);
                    double distance = ScattererDistance(length, arrival, toBase);

                    result.Add(new Ray
                    {
                        Cluster = clusters.Count - 1,
                        Power = powers[n] / rays * scatteredShare,
                        LengthM = length,
                        Departure = departure,
                        Arrival = arrival,
                        ScattererDistanceM = distance,
                        Scatterer = Terminal + (distance * arrival),
                        ThetaTheta = RandomPhase(rng),
                        ThetaPhi = crossPolar * RandomPhase(rng),
                        PhiTheta = crossPolar * RandomPhase(rng),
                        PhiPhi = RandomPhase(rng),
                    });
                }
            }
        }

        return (clusters.ToArray(), result.ToArray());
    }

    /// <summary>
    /// Расстояние от абонента до последнего рассеивателя на луче прихода u, при котором путь
    /// «база — рассеиватель — абонент» имеет длину L: r = (L² − |D|²)/(2(L − u·D)), D — от абонента к базе
    /// </summary>
    private static double ScattererDistance(double length, Vector3 arrival, Vector3 toBase)
    {
        double reach = toBase.Length;
        double denominator = 2 * (length - arrival.Dot(toBase));
        double distance = denominator > 0 ? ((length * length) - (reach * reach)) / denominator : length;

        return Math.Clamp(distance, Math.Min(MinimumScattererDistanceM, reach / 2), length);
    }

    private static double[] Azimuths(double[] powers, double spreadDeg, double scaling, double lineOfSightDeg, bool lineOfSight, Random rng)
    {
        double strongest = powers.Max();
        var angles = new double[powers.Length];

        for (int n = 0; n < powers.Length; n++)
        {
            double basis = 2 * (spreadDeg / 1.4) * Math.Sqrt(-Math.Log(powers[n] / strongest)) / scaling;
            angles[n] = (RandomSign(rng) * basis) + RandomEngine.NextGaussian(rng, 0, spreadDeg / 7);
        }

        // С прямой видимостью первый кластер ставится точно на прямой путь
        double shift = lineOfSight ? lineOfSightDeg - angles[0] : lineOfSightDeg;

        for (int n = 0; n < angles.Length; n++)
            angles[n] += shift;

        return angles;
    }

    private static double[] Zeniths(double[] powers, double spreadDeg, double scaling, double lineOfSightDeg, double offsetDeg, bool lineOfSight, Random rng)
    {
        double strongest = powers.Max();
        var angles = new double[powers.Length];

        for (int n = 0; n < powers.Length; n++)
        {
            double basis = -spreadDeg * Math.Log(powers[n] / strongest) / scaling;
            angles[n] = (RandomSign(rng) * basis) + RandomEngine.NextGaussian(rng, 0, spreadDeg / 7);
        }

        double shift = lineOfSight ? lineOfSightDeg - angles[0] : lineOfSightDeg + offsetDeg;

        for (int n = 0; n < angles.Length; n++)
            angles[n] += shift;

        return angles;
    }

    private static int[] Shuffled(int[] items, Random rng)
    {
        int[] result = (int[])items.Clone();
        rng.Shuffle(result);

        return result;
    }

    private static double RandomSign(Random rng) => rng.NextDouble() < 0.5 ? -1 : 1;

    private static Complex RandomPhase(Random rng) => Complex.FromPolarCoordinates(1, (2 * Math.PI * rng.NextDouble()) - Math.PI);

    /// <summary>ln det эрмитовой положительно определённой матрицы через разложение Холецкого</summary>
    private static double LogDeterminant(Complex[,] a)
    {
        int n = a.GetLength(0);
        var l = new Complex[n, n];
        double result = 0;

        for (int j = 0; j < n; j++)
        {
            double diagonal = a[j, j].Real;

            for (int k = 0; k < j; k++)
                diagonal -= (l[j, k].Real * l[j, k].Real) + (l[j, k].Imaginary * l[j, k].Imaginary);

            double pivot = Math.Sqrt(diagonal);
            l[j, j] = pivot;
            result += 2 * Math.Log(pivot);

            for (int i = j + 1; i < n; i++)
            {
                Complex sum = a[i, j];

                for (int k = 0; k < j; k++)
                    sum -= l[i, k] * Complex.Conjugate(l[j, k]);

                l[i, j] = sum / pivot;
            }
        }

        return result;
    }

    private sealed class Ray
    {
        public bool LineOfSight;
        public int Cluster;
        public double Power;
        public double LengthM;
        public Vector3 Departure;
        public Vector3 Arrival;
        public Vector3 Scatterer;
        public double ScattererDistanceM;
        public Complex ThetaTheta;
        public Complex ThetaPhi;
        public Complex PhiTheta;
        public Complex PhiPhi;
    }
}
