using AI.ClassicMath.MatrixUtils;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Microwave.Propagation;

/// <summary>Сценарий развёртывания TR 38.901</summary>
public enum Tr38901Environment
{
    /// <summary>UMa — городская макросота, антенна над крышами</summary>
    UrbanMacro,

    /// <summary>UMi — городская микросота, уличный каньон, антенна ниже крыш</summary>
    UrbanMicro,

    /// <summary>RMa — сельская макросота</summary>
    RuralMacro,

    /// <summary>InH — офис со смешанной планировкой</summary>
    IndoorMixedOffice,

    /// <summary>InH — открытый офис</summary>
    IndoorOpenOffice
}

/// <summary>Крупномасштабный параметр канала TR 38.901</summary>
/// <remarks>Порядок перечисления — порядок в разложении Холецкого матрицы взаимных корреляций</remarks>
public enum LargeScaleParameter
{
    /// <summary>Разброс задержек DS</summary>
    DelaySpread,

    /// <summary>Азимутальный разброс ухода ASD</summary>
    AzimuthSpreadDeparture,

    /// <summary>Азимутальный разброс прихода ASA</summary>
    AzimuthSpreadArrival,

    /// <summary>Затенение SF</summary>
    ShadowFading,

    /// <summary>K-фактор</summary>
    KFactor,

    /// <summary>Зенитный разброс прихода ZSA</summary>
    ZenithSpreadArrival,

    /// <summary>Зенитный разброс ухода ZSD</summary>
    ZenithSpreadDeparture
}

/// <summary>Зависимость параметра от частоты: a·lg(b + f) + c, f в ГГц</summary>
/// <param name="Slope">Коэффициент a при логарифме</param>
/// <param name="Offset">Сдвиг b под логарифмом</param>
/// <param name="Intercept">Постоянная c</param>
public readonly record struct LogFrequencyLaw(double Slope, double Offset, double Intercept)
{
    /// <summary>Постоянная, не зависящая от частоты</summary>
    public static LogFrequencyLaw Constant(double value) => new(0, 0, value);

    /// <summary>Значение на частоте</summary>
    /// <param name="frequencyGHz">Частота, ГГц</param>
    public double At(double frequencyGHz) => Slope == 0 ? Intercept : (Slope * Math.Log10(Offset + frequencyGHz)) + Intercept;
}

/// <summary>
/// Параметры одного состояния (прямая видимость или нет) сценария TR 38.901 v16.1: таблица 7.5-6.
/// </summary>
/// <remarks>
/// Логарифмические параметры — десятичные логарифмы разбросов: lg(DS/1 с), lg(ASD/1°) и так далее. Таблицы
/// перенесены из открытой реализации Sionna (NVIDIA) и сверены с конфигурациями QuaDRiGa (Fraunhofer HHI).
/// </remarks>
public sealed class Tr38901Parameters
{
    private double[,]? _factor;

    internal Tr38901Parameters()
    {
    }

    /// <summary>Среднее lg DS</summary>
    public LogFrequencyLaw DelaySpreadMean { get; internal init; }

    /// <summary>СКО lg DS</summary>
    public LogFrequencyLaw DelaySpreadSigma { get; internal init; }

    /// <summary>Среднее lg ASD</summary>
    public LogFrequencyLaw AzimuthSpreadDepartureMean { get; internal init; }

    /// <summary>СКО lg ASD</summary>
    public LogFrequencyLaw AzimuthSpreadDepartureSigma { get; internal init; }

    /// <summary>Среднее lg ASA</summary>
    public LogFrequencyLaw AzimuthSpreadArrivalMean { get; internal init; }

    /// <summary>СКО lg ASA</summary>
    public LogFrequencyLaw AzimuthSpreadArrivalSigma { get; internal init; }

    /// <summary>Среднее lg ZSA</summary>
    public LogFrequencyLaw ZenithSpreadArrivalMean { get; internal init; }

    /// <summary>СКО lg ZSA</summary>
    public LogFrequencyLaw ZenithSpreadArrivalSigma { get; internal init; }

    /// <summary>СКО lg ZSD; среднее зависит от геометрии — <see cref="Tr38901Scenario.ZenithSpreadDepartureMean"/></summary>
    public LogFrequencyLaw ZenithSpreadDepartureSigma { get; internal init; }

    /// <summary>Среднее K-фактора, дБ</summary>
    public double KFactorMeanDb { get; internal init; }

    /// <summary>СКО K-фактора, дБ</summary>
    public double KFactorSigmaDb { get; internal init; }

    /// <summary>СКО затенения, дБ; у RMa с прямой видимостью зависит от расстояния — <see cref="Tr38901Scenario.ShadowFadingSigmaDb"/></summary>
    public double ShadowFadingSigmaDb { get; internal init; }

    /// <summary>Среднее кроссполяризационного отношения XPR, дБ</summary>
    public double CrossPolarizationMeanDb { get; internal init; }

    /// <summary>СКО XPR, дБ</summary>
    public double CrossPolarizationSigmaDb { get; internal init; }

    /// <summary>Число кластеров N</summary>
    public int ClusterCount { get; internal init; }

    /// <summary>Число лучей в кластере M</summary>
    public int RaysPerCluster => 20;

    /// <summary>Коэффициент масштаба задержек r_τ</summary>
    public double DelayScaling { get; internal init; }

    /// <summary>СКО затенения отдельного кластера ζ, дБ</summary>
    public double ClusterShadowingDb { get; internal init; }

    /// <summary>Азимутальный разброс ухода внутри кластера c_ASD, градусы</summary>
    public double ClusterAzimuthSpreadDepartureDeg { get; internal init; }

    /// <summary>Азимутальный разброс прихода внутри кластера c_ASA, градусы</summary>
    public double ClusterAzimuthSpreadArrivalDeg { get; internal init; }

    /// <summary>Зенитный разброс прихода внутри кластера c_ZSA, градусы</summary>
    public double ClusterZenithSpreadArrivalDeg { get; internal init; }

    /// <summary>Масштаб азимутов C_φ^NLOS по числу кластеров (таблица 7.5-2)</summary>
    public double AzimuthScaling { get; internal init; }

    /// <summary>Масштаб зенитов C_θ^NLOS по числу кластеров (таблица 7.5-4)</summary>
    public double ZenithScaling { get; internal init; }

    internal (double Minimum, double Intercept, double Slope) ClusterDelaySpreadLaw { get; init; }

    internal double[] CorrelationDistances { get; init; } = [];

    internal double[,] CrossCorrelations { get; init; } = new double[7, 7];

    /// <summary>
    /// Разброс задержек внутри кластера c_DS, нс: max(a, b − c·lg f); где таблица его не задаёт, 3,91 нс
    /// </summary>
    /// <param name="frequencyGHz">Частота крупномасштабных параметров, ГГц</param>
    public double ClusterDelaySpreadNs(double frequencyGHz)
    {
        (double minimum, double intercept, double slope) = ClusterDelaySpreadLaw;

        return Math.Max(minimum, intercept - (slope * Math.Log10(frequencyGHz)));
    }

    /// <summary>Расстояние корреляции параметра, м</summary>
    public double CorrelationDistanceM(LargeScaleParameter parameter) => CorrelationDistances[(int)parameter];

    /// <summary>Взаимная корреляция двух параметров</summary>
    public double CrossCorrelation(LargeScaleParameter first, LargeScaleParameter second) => CrossCorrelations[(int)first, (int)second];

    /// <summary>
    /// Множитель L с L·Lᵀ = C для матрицы взаимных корреляций. Некоторые таблицы стандарта не положительно
    /// определены; тогда матрица сжимается к единичной, C' = (C + εI)/(1 + ε), с наименьшим ε из ряда, при котором
    /// разложение существует
    /// </summary>
    internal double[,] CorrelationFactor => _factor ??= Factor(CrossCorrelations);

    /// <summary>Наибольшее отклонение L·Lᵀ от таблицы — ноль, если таблица положительно определена</summary>
    public double CorrelationRegularization
    {
        get
        {
            double[,] l = CorrelationFactor;
            double worst = 0;

            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < 7; j++)
                {
                    double sum = 0;

                    for (int k = 0; k < 7; k++)
                        sum += l[i, k] * l[j, k];

                    worst = Math.Max(worst, Math.Abs(sum - CrossCorrelations[i, j]));
                }
            }

            return worst;
        }
    }

    private static double[,] Factor(double[,] correlations)
    {
        double[] shrinks = [0, 1e-4, 1e-3, 1e-2, 3e-2, 0.1, 0.3, 1];

        foreach (double shrink in shrinks)
        {
            var matrix = new Matrix(7, 7);

            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < 7; j++)
                    matrix[i, j] = (correlations[i, j] + (i == j ? shrink : 0)) / (1 + shrink);
            }

            try
            {
                Matrix lower = Cholesky.Decompose(matrix);
                var result = new double[7, 7];

                for (int i = 0; i < 7; i++)
                {
                    for (int j = 0; j <= i; j++)
                        result[i, j] = lower[i, j];
                }

                return result;
            }
            catch (InvalidOperationException)
            {
                // Не положительно определена — сжимаем сильнее
            }
        }

        throw new InvalidOperationException("Матрица взаимных корреляций не приводится к положительно определённой");
    }
}

/// <summary>
/// Сценарий TR 38.901 v16.1: потери на трассе (7.4.1), вероятность прямой видимости (7.4.2) и таблицы
/// крупномасштабных параметров (7.5) для городской макро- и микросоты, сельской макросоты и офиса.
/// </summary>
/// <remarks>
/// <para>
/// Высоты антенн — координаты Z над землёй; расстояние d_2D — по горизонтали, d_3D — по прямой. Частоты
/// в формулах потерь — в гигагерцах. Модели определены на 0,5–100 ГГц и для расстояний от 10 м (в офисе от 1 м)
/// до 5 км (сельская — до 10 км); за этими пределами формулы продолжаются как есть.
/// </para>
/// <para>
/// Для крупномасштабных параметров частота ограничена снизу: 6 ГГц у UMa и InH, 2 ГГц у UMi (примечания
/// к таблице 7.5-6) — ниже этих частот измерений не было.
/// </para>
/// </remarks>
public sealed class Tr38901Scenario : IPropagationModel
{
    // Ширина улиц и высота зданий сельской макросоты по умолчанию, м
    private const double RuralStreetWidth = 20;
    private const double RuralBuildingHeight = 5;

    private readonly double _frequencyFloorGHz;

    /// <inheritdoc />
    double IPropagationModel.ShadowCorrelationDistanceM => NonLineOfSight.CorrelationDistanceM(LargeScaleParameter.ShadowFading);

    /// <inheritdoc />
    double IPropagationModel.StateCorrelationDistanceM => LineOfSightCorrelationDistanceM;

    /// <summary>
    /// Потери между антеннами для карт покрытия: вероятность прямой видимости и потери обоих состояний с их
    /// затенением. Высоты — Z антенн, расстояние по горизонтали не меньше метра, эффективная высота окружения
    /// UMa — 1 м
    /// </summary>
    /// <param name="transmitter">Положение антенны базовой станции, м</param>
    /// <param name="receiver">Положение антенны абонента, м</param>
    /// <param name="frequencyHz">Несущая, Гц</param>
    public LinkLoss Loss(AI.Geometry.Primitives.Vector3 transmitter, AI.Geometry.Primitives.Vector3 receiver, double frequencyHz)
    {
        double dx = receiver.X - transmitter.X, dy = receiver.Y - transmitter.Y;
        double d2 = Math.Max(Math.Sqrt((dx * dx) + (dy * dy)), 1);
        double hBS = transmitter.Z, hUT = receiver.Z;

        return new LinkLoss(
            LineOfSightProbability(d2, hUT),
            PathLossDb(true, d2, hBS, hUT, frequencyHz),
            ShadowFadingSigmaDb(true, d2, hBS, hUT, frequencyHz),
            PathLossDb(false, d2, hBS, hUT, frequencyHz),
            ShadowFadingSigmaDb(false, d2, hBS, hUT, frequencyHz));
    }

    private Tr38901Scenario(
        Tr38901Environment environment,
        string name,
        double baseStationHeight,
        double terminalHeight,
        double lineOfSightCorrelationDistance,
        double frequencyFloorGHz,
        Tr38901Parameters lineOfSight,
        Tr38901Parameters nonLineOfSight)
    {
        Environment = environment;
        Name = name;
        TypicalBaseStationHeightM = baseStationHeight;
        TypicalTerminalHeightM = terminalHeight;
        LineOfSightCorrelationDistanceM = lineOfSightCorrelationDistance;
        _frequencyFloorGHz = frequencyFloorGHz;
        LineOfSight = lineOfSight;
        NonLineOfSight = nonLineOfSight;
    }

    /// <summary>UMa — городская макросота</summary>
    public static Tr38901Scenario UrbanMacro { get; } = new(
        Tr38901Environment.UrbanMacro, "UMa, городская макросота", 25, 1.5, 50, 6, UrbanMacroLos(), UrbanMacroNlos());

    /// <summary>UMi — городская микросота, уличный каньон</summary>
    public static Tr38901Scenario UrbanMicro { get; } = new(
        Tr38901Environment.UrbanMicro, "UMi, уличный каньон", 10, 1.5, 50, 2, UrbanMicroLos(), UrbanMicroNlos());

    /// <summary>RMa — сельская макросота</summary>
    public static Tr38901Scenario RuralMacro { get; } = new(
        Tr38901Environment.RuralMacro, "RMa, сельская макросота", 35, 1.5, 60, 0, RuralMacroLos(), RuralMacroNlos());

    /// <summary>InH — офис со смешанной планировкой</summary>
    public static Tr38901Scenario IndoorMixedOffice { get; } = new(
        Tr38901Environment.IndoorMixedOffice, "InH, офис со смешанной планировкой", 3, 1, 10, 6, IndoorLos(), IndoorNlos());

    /// <summary>InH — открытый офис</summary>
    public static Tr38901Scenario IndoorOpenOffice { get; } = new(
        Tr38901Environment.IndoorOpenOffice, "InH, открытый офис", 3, 1, 10, 6, IndoorLos(), IndoorNlos());

    /// <summary>Все сценарии</summary>
    public static IReadOnlyList<Tr38901Scenario> All { get; } = [UrbanMacro, UrbanMicro, RuralMacro, IndoorMixedOffice, IndoorOpenOffice];

    /// <summary>Сценарий</summary>
    public Tr38901Environment Environment { get; }

    /// <summary>Название</summary>
    public string Name { get; }

    /// <summary>Типовая высота базовой станции, м</summary>
    public double TypicalBaseStationHeightM { get; }

    /// <summary>Типовая высота абонента, м</summary>
    public double TypicalTerminalHeightM { get; }

    /// <summary>Расстояние корреляции состояния прямой видимости, м (таблица 7.6.3.1-2)</summary>
    public double LineOfSightCorrelationDistanceM { get; }

    /// <summary>Параметры с прямой видимостью</summary>
    public Tr38901Parameters LineOfSight { get; }

    /// <summary>Параметры без прямой видимости</summary>
    public Tr38901Parameters NonLineOfSight { get; }

    /// <summary>Параметры состояния</summary>
    public Tr38901Parameters Parameters(bool lineOfSight) => lineOfSight ? LineOfSight : NonLineOfSight;

    /// <summary>Частота для таблиц крупномасштабных параметров, ГГц: несущая, ограниченная снизу</summary>
    /// <param name="carrierFrequencyHz">Несущая, Гц</param>
    public double LargeScaleFrequencyGHz(double carrierFrequencyHz) => Math.Max(carrierFrequencyHz / 1e9, _frequencyFloorGHz);

    /// <summary>Вероятность прямой видимости (таблица 7.4.2-1)</summary>
    /// <param name="distance2DM">Расстояние по горизонтали, м</param>
    /// <param name="terminalHeightM">Высота абонента, м — важна только для UMa</param>
    public double LineOfSightProbability(double distance2DM, double terminalHeightM = 1.5)
    {
        double d = distance2DM;

        switch (Environment)
        {
            case Tr38901Environment.UrbanMacro:
            {
                if (d <= 18)
                    return 1;

                double heightFactor = terminalHeightM <= 13 ? 0 : Math.Pow((terminalHeightM - 13) / 10, 1.5);

                return ((18 / d) + (Math.Exp(-d / 63) * (1 - (18 / d))))
                    * (1 + (heightFactor * 1.25 * Math.Pow(d / 100, 3) * Math.Exp(-d / 150)));
            }

            case Tr38901Environment.UrbanMicro:
                return d <= 18 ? 1 : (18 / d) + (Math.Exp(-d / 36) * (1 - (18 / d)));

            case Tr38901Environment.RuralMacro:
                return d <= 10 ? 1 : Math.Exp(-(d - 10) / 1000);

            case Tr38901Environment.IndoorMixedOffice:
                return d <= 1.2 ? 1 : d < 6.5 ? Math.Exp(-(d - 1.2) / 4.7) : Math.Exp(-(d - 6.5) / 32.6) * 0.32;

            default:
                return d <= 5 ? 1 : d <= 49 ? Math.Exp(-(d - 5) / 70.8) : Math.Exp(-(d - 49) / 211.7) * 0.54;
        }
    }

    /// <summary>Точка перелома двухнаклонной модели потерь, м; NaN у офиса, где её нет</summary>
    /// <param name="baseStationHeightM">Высота базовой станции, м</param>
    /// <param name="terminalHeightM">Высота абонента, м</param>
    /// <param name="carrierFrequencyHz">Несущая, Гц</param>
    /// <param name="environmentHeightM">Эффективная высота окружения h_E у UMa, м</param>
    public double BreakpointDistanceM(double baseStationHeightM, double terminalHeightM, double carrierFrequencyHz, double environmentHeightM = 1)
        => Environment switch
        {
            Tr38901Environment.UrbanMacro => 4 * (baseStationHeightM - environmentHeightM) * (terminalHeightM - environmentHeightM) * carrierFrequencyHz / Wave.SpeedOfLight,
            Tr38901Environment.UrbanMicro => 4 * (baseStationHeightM - 1) * (terminalHeightM - 1) * carrierFrequencyHz / Wave.SpeedOfLight,
            Tr38901Environment.RuralMacro => 2 * Math.PI * baseStationHeightM * terminalHeightM * carrierFrequencyHz / Wave.SpeedOfLight,
            _ => double.NaN,
        };

    /// <summary>Потери на трассе, дБ (таблица 7.4.1-1); без прямой видимости — не меньше, чем с ней</summary>
    /// <param name="lineOfSight">Есть ли прямая видимость</param>
    /// <param name="distance2DM">Расстояние по горизонтали, м</param>
    /// <param name="baseStationHeightM">Высота базовой станции, м</param>
    /// <param name="terminalHeightM">Высота абонента, м</param>
    /// <param name="carrierFrequencyHz">Несущая, Гц</param>
    /// <param name="environmentHeightM">Эффективная высота окружения h_E у UMa, м; у UMi она всегда 1 м</param>
    public double PathLossDb(
        bool lineOfSight,
        double distance2DM,
        double baseStationHeightM,
        double terminalHeightM,
        double carrierFrequencyHz,
        double environmentHeightM = 1)
    {
        Wave.RequirePositive(carrierFrequencyHz, nameof(carrierFrequencyHz));

        if (!(distance2DM >= 0) || double.IsInfinity(distance2DM))
            throw new ArgumentOutOfRangeException(nameof(distance2DM), distance2DM, "Расстояние должно быть конечным и неотрицательным");

        double heightDifference = baseStationHeightM - terminalHeightM;
        double d3 = Math.Sqrt((distance2DM * distance2DM) + (heightDifference * heightDifference));
        Wave.RequirePositive(d3, nameof(distance2DM));

        double fc = carrierFrequencyHz / 1e9;
        double lgD = Math.Log10(d3), lgF = Math.Log10(fc);
        double los;

        switch (Environment)
        {
            case Tr38901Environment.UrbanMacro:
            case Tr38901Environment.UrbanMicro:
            {
                bool macro = Environment == Tr38901Environment.UrbanMacro;
                double breakpoint = BreakpointDistanceM(baseStationHeightM, terminalHeightM, carrierFrequencyHz, environmentHeightM);

                los = distance2DM <= breakpoint
                    ? (macro ? 28 + (22 * lgD) : 32.4 + (21 * lgD)) + (20 * lgF)
                    : (macro ? 28 : 32.4) + (40 * lgD) + (20 * lgF)
                      - ((macro ? 9 : 9.5) * Math.Log10((breakpoint * breakpoint) + (heightDifference * heightDifference)));
                break;
            }

            case Tr38901Environment.RuralMacro:
            {
                double breakpoint = BreakpointDistanceM(baseStationHeightM, terminalHeightM, carrierFrequencyHz);

                los = distance2DM <= breakpoint
                    ? RuralFirstSlope(d3, carrierFrequencyHz)
                    : RuralFirstSlope(breakpoint, carrierFrequencyHz) + (40 * Math.Log10(d3 / breakpoint));
                break;
            }

            default:
                los = 32.4 + (17.3 * lgD) + (20 * lgF);
                break;
        }

        if (lineOfSight)
            return los;

        double nlos = Environment switch
        {
            Tr38901Environment.UrbanMacro => 13.54 + (39.08 * lgD) + (20 * lgF) - (0.6 * (terminalHeightM - 1.5)),
            Tr38901Environment.UrbanMicro => (35.3 * lgD) + 22.4 + (21.3 * lgF) - (0.3 * (terminalHeightM - 1.5)),
            Tr38901Environment.RuralMacro => 161.04
                - (7.1 * Math.Log10(RuralStreetWidth))
                + (7.5 * Math.Log10(RuralBuildingHeight))
                - ((24.37 - (3.7 * Math.Pow(RuralBuildingHeight / baseStationHeightM, 2))) * Math.Log10(baseStationHeightM))
                + ((43.42 - (3.1 * Math.Log10(baseStationHeightM))) * (lgD - 3))
                + (20 * lgF)
                - ((3.2 * Math.Pow(Math.Log10(11.75 * terminalHeightM), 2)) - 4.97),
            _ => (38.3 * lgD) + 17.30 + (24.9 * lgF),
        };

        return Math.Max(los, nlos);
    }

    /// <summary>СКО затенения, дБ: у RMa с прямой видимостью 4 дБ до точки перелома и 6 дБ за ней</summary>
    /// <param name="lineOfSight">Есть ли прямая видимость</param>
    /// <param name="distance2DM">Расстояние по горизонтали, м</param>
    /// <param name="baseStationHeightM">Высота базовой станции, м</param>
    /// <param name="terminalHeightM">Высота абонента, м</param>
    /// <param name="carrierFrequencyHz">Несущая, Гц</param>
    public double ShadowFadingSigmaDb(bool lineOfSight, double distance2DM, double baseStationHeightM, double terminalHeightM, double carrierFrequencyHz)
        => Environment == Tr38901Environment.RuralMacro && lineOfSight
            ? distance2DM <= BreakpointDistanceM(baseStationHeightM, terminalHeightM, carrierFrequencyHz) ? 4 : 6
            : Parameters(lineOfSight).ShadowFadingSigmaDb;

    /// <summary>Среднее lg ZSD, зависящее от геометрии (таблицы 7.5-7…7.5-10)</summary>
    /// <param name="lineOfSight">Есть ли прямая видимость</param>
    /// <param name="distance2DM">Расстояние по горизонтали, м</param>
    /// <param name="baseStationHeightM">Высота базовой станции, м</param>
    /// <param name="terminalHeightM">Высота абонента, м</param>
    /// <param name="carrierFrequencyHz">Несущая, Гц</param>
    public double ZenithSpreadDepartureMean(bool lineOfSight, double distance2DM, double baseStationHeightM, double terminalHeightM, double carrierFrequencyHz)
    {
        double km = distance2DM / 1000;

        return Environment switch
        {
            Tr38901Environment.UrbanMacro => Math.Max(-0.5, (-2.1 * km) - (0.01 * (terminalHeightM - 1.5)) + (lineOfSight ? 0.75 : 0.9)),
            Tr38901Environment.UrbanMicro => lineOfSight
                ? Math.Max(-0.21, (-14.8 * km) + (0.01 * Math.Abs(terminalHeightM - baseStationHeightM)) + 0.83)
                : Math.Max(-0.5, (-3.1 * km) + (0.01 * Math.Max(terminalHeightM - baseStationHeightM, 0)) + 0.2),
            Tr38901Environment.RuralMacro => lineOfSight
                ? Math.Max(-1, (-0.17 * km) - (0.01 * (terminalHeightM - 1.5)) + 0.22)
                : Math.Max(-1, (-0.19 * km) - (0.01 * (terminalHeightM - 1.5)) + 0.28),
            _ => lineOfSight ? (-1.43 * Math.Log10(1 + LargeScaleFrequencyGHz(carrierFrequencyHz))) + 2.228 : 1.08,
        };
    }

    /// <summary>Смещение зенита ухода μ_offset,ZOD, градусы: ненулевое только без прямой видимости у UMa, UMi и RMa</summary>
    /// <param name="lineOfSight">Есть ли прямая видимость</param>
    /// <param name="distance2DM">Расстояние по горизонтали, м</param>
    /// <param name="terminalHeightM">Высота абонента, м</param>
    /// <param name="carrierFrequencyHz">Несущая, Гц</param>
    public double ZenithOffsetDepartureDeg(bool lineOfSight, double distance2DM, double terminalHeightM, double carrierFrequencyHz)
    {
        if (lineOfSight)
            return 0;

        switch (Environment)
        {
            case Tr38901Environment.UrbanMacro:
            {
                double lgF = Math.Log10(LargeScaleFrequencyGHz(carrierFrequencyHz));
                double a = (0.208 * lgF) - 0.782, c = (-0.13 * lgF) + 2.03, e = (7.66 * lgF) - 5.96;

                return e - Math.Pow(10, (a * Math.Log10(Math.Max(25, distance2DM))) + c - (0.07 * (terminalHeightM - 1.5)));
            }

            case Tr38901Environment.UrbanMicro:
                return -Math.Pow(10, (-1.5 * Math.Log10(Math.Max(10, distance2DM))) + 3.3);

            case Tr38901Environment.RuralMacro:
                return (Math.Atan((35 - 3.5) / distance2DM) - Math.Atan((35 - 1.5) / distance2DM)) * 180 / Math.PI;

            default:
                return 0;
        }
    }

    /// <summary>Название сценария</summary>
    public override string ToString() => Name;

    /// <summary>
    /// Эффективная высота окружения h_E у UMa (примечание 1 к таблице 7.4.1-1): 1 м с вероятностью 1/(1 + C),
    /// иначе равновероятно из 12, 15, …, h_UT − 1,5 м; у остальных сценариев 1 м
    /// </summary>
    internal double SampleEnvironmentHeight(double distance2DM, double terminalHeightM, Random rng)
    {
        if (Environment != Tr38901Environment.UrbanMacro || terminalHeightM < 13)
            return 1;

        double g = distance2DM <= 18 ? 0 : 1.25 * Math.Pow(distance2DM / 100, 3) * Math.Exp(-distance2DM / 150);
        double c = Math.Pow((terminalHeightM - 13) / 10, 1.5) * g;
        int choices = (int)Math.Floor((terminalHeightM - 1.5 - 12) / 3) + 1;

        return choices < 1 || rng.NextDouble() < 1 / (1 + c) ? 1 : 12 + (3 * rng.Next(choices));
    }

    // Первый участок RMa: свободное пространство плюс поправки на высоту зданий h
    private static double RuralFirstSlope(double d3, double carrierFrequencyHz)
    {
        double h = Math.Pow(RuralBuildingHeight, 1.72);

        return PathLoss.FreeSpaceDb(carrierFrequencyHz, d3)
            + (Math.Min(0.03 * h, 10) * Math.Log10(d3))
            - Math.Min(0.044 * h, 14.77)
            + (0.002 * Math.Log10(RuralBuildingHeight) * d3);
    }

    #region Таблицы 7.5-6 (v16.1)

    private static LogFrequencyLaw Law(double slope, double offset, double intercept) => new(slope, offset, intercept);

    private static LogFrequencyLaw Const(double value) => LogFrequencyLaw.Constant(value);

    // Порядок: DS, ASD, ASA, SF, K, ZSA, ZSD — как в перечислении LargeScaleParameter
    private static double[] Distances(double ds, double asd, double asa, double sf, double k, double zsa, double zsd)
        => [ds, asd, asa, sf, k, zsa, zsd];

    private static double[,] Correlations(
        double asdDs, double asaDs, double asaSf, double asdSf, double dsSf, double asdAsa, double asdK,
        double asaK, double dsK, double sfK, double zsdSf, double zsaSf, double zsdK, double zsaK,
        double zsdDs, double zsaDs, double zsdAsd, double zsaAsd, double zsdAsa, double zsaAsa, double zsdZsa)
    {
        const int Ds = 0, Asd = 1, Asa = 2, Sf = 3, K = 4, Zsa = 5, Zsd = 6;
        var c = new double[7, 7];

        void Set(int i, int j, double value) => c[i, j] = c[j, i] = value;

        for (int i = 0; i < 7; i++)
            c[i, i] = 1;

        Set(Asd, Ds, asdDs); Set(Asa, Ds, asaDs); Set(Asa, Sf, asaSf); Set(Asd, Sf, asdSf); Set(Ds, Sf, dsSf);
        Set(Asd, Asa, asdAsa); Set(Asd, K, asdK); Set(Asa, K, asaK); Set(Ds, K, dsK); Set(Sf, K, sfK);
        Set(Zsd, Sf, zsdSf); Set(Zsa, Sf, zsaSf); Set(Zsd, K, zsdK); Set(Zsa, K, zsaK); Set(Zsd, Ds, zsdDs);
        Set(Zsa, Ds, zsaDs); Set(Zsd, Asd, zsdAsd); Set(Zsa, Asd, zsaAsd); Set(Zsd, Asa, zsdAsa);
        Set(Zsa, Asa, zsaAsa); Set(Zsd, Zsa, zsdZsa);

        return c;
    }

    private static Tr38901Parameters UrbanMacroLos() => new()
    {
        DelaySpreadMean = Law(-0.0963, 0, -6.955), DelaySpreadSigma = Const(0.66),
        AzimuthSpreadDepartureMean = Law(0.1114, 0, 1.06), AzimuthSpreadDepartureSigma = Const(0.28),
        AzimuthSpreadArrivalMean = Const(1.81), AzimuthSpreadArrivalSigma = Const(0.20),
        ZenithSpreadArrivalMean = Const(0.95), ZenithSpreadArrivalSigma = Const(0.16),
        ZenithSpreadDepartureSigma = Const(0.40),
        KFactorMeanDb = 9, KFactorSigmaDb = 3.5, ShadowFadingSigmaDb = 4,
        CrossPolarizationMeanDb = 8, CrossPolarizationSigmaDb = 4,
        ClusterCount = 12, DelayScaling = 2.5, ClusterShadowingDb = 3,
        ClusterDelaySpreadLaw = (0.25, 6.5622, 3.4084),
        ClusterAzimuthSpreadDepartureDeg = 5, ClusterAzimuthSpreadArrivalDeg = 11, ClusterZenithSpreadArrivalDeg = 7,
        AzimuthScaling = 1.146, ZenithScaling = 1.104,
        CorrelationDistances = Distances(ds: 30, asd: 18, asa: 15, sf: 37, k: 12, zsa: 15, zsd: 15),
        CrossCorrelations = Correlations(
            asdDs: 0.4, asaDs: 0.8, asaSf: -0.5, asdSf: -0.5, dsSf: -0.4, asdAsa: 0, asdK: 0,
            asaK: -0.2, dsK: -0.4, sfK: 0, zsdSf: 0, zsaSf: -0.8, zsdK: 0, zsaK: 0,
            zsdDs: -0.2, zsaDs: 0, zsdAsd: 0.5, zsaAsd: 0, zsdAsa: -0.3, zsaAsa: 0.4, zsdZsa: 0),
    };

    private static Tr38901Parameters UrbanMacroNlos() => new()
    {
        DelaySpreadMean = Law(-0.204, 0, -6.28), DelaySpreadSigma = Const(0.39),
        AzimuthSpreadDepartureMean = Law(-0.1144, 0, 1.5), AzimuthSpreadDepartureSigma = Const(0.28),
        AzimuthSpreadArrivalMean = Law(-0.27, 0, 2.08), AzimuthSpreadArrivalSigma = Const(0.11),
        ZenithSpreadArrivalMean = Law(-0.3236, 0, 1.512), ZenithSpreadArrivalSigma = Const(0.16),
        ZenithSpreadDepartureSigma = Const(0.49),
        KFactorMeanDb = 0, KFactorSigmaDb = 0, ShadowFadingSigmaDb = 6,
        CrossPolarizationMeanDb = 7, CrossPolarizationSigmaDb = 3,
        ClusterCount = 20, DelayScaling = 2.3, ClusterShadowingDb = 3,
        ClusterDelaySpreadLaw = (0.25, 6.5622, 3.4084),
        ClusterAzimuthSpreadDepartureDeg = 2, ClusterAzimuthSpreadArrivalDeg = 15, ClusterZenithSpreadArrivalDeg = 7,
        AzimuthScaling = 1.289, ZenithScaling = 1.178,
        CorrelationDistances = Distances(ds: 40, asd: 50, asa: 50, sf: 50, k: 1, zsa: 50, zsd: 50),
        CrossCorrelations = Correlations(
            asdDs: 0.4, asaDs: 0.6, asaSf: 0, asdSf: -0.6, dsSf: -0.4, asdAsa: 0.4, asdK: 0,
            asaK: 0, dsK: 0, sfK: 0, zsdSf: 0, zsaSf: -0.4, zsdK: 0, zsaK: 0,
            zsdDs: -0.5, zsaDs: 0, zsdAsd: 0.5, zsaAsd: -0.1, zsdAsa: 0, zsaAsa: 0, zsdZsa: 0),
    };

    private static Tr38901Parameters UrbanMicroLos() => new()
    {
        DelaySpreadMean = Law(-0.24, 1, -7.14), DelaySpreadSigma = Const(0.38),
        AzimuthSpreadDepartureMean = Law(-0.05, 1, 1.21), AzimuthSpreadDepartureSigma = Const(0.41),
        AzimuthSpreadArrivalMean = Law(-0.08, 1, 1.73), AzimuthSpreadArrivalSigma = Law(0.014, 1, 0.28),
        ZenithSpreadArrivalMean = Law(-0.1, 1, 0.73), ZenithSpreadArrivalSigma = Law(-0.04, 1, 0.34),
        ZenithSpreadDepartureSigma = Const(0.35),
        KFactorMeanDb = 9, KFactorSigmaDb = 5, ShadowFadingSigmaDb = 4,
        CrossPolarizationMeanDb = 9, CrossPolarizationSigmaDb = 3,
        ClusterCount = 12, DelayScaling = 3, ClusterShadowingDb = 3,
        ClusterDelaySpreadLaw = (5, 0, 0),
        ClusterAzimuthSpreadDepartureDeg = 3, ClusterAzimuthSpreadArrivalDeg = 17, ClusterZenithSpreadArrivalDeg = 7,
        AzimuthScaling = 1.146, ZenithScaling = 1.104,
        CorrelationDistances = Distances(ds: 7, asd: 8, asa: 8, sf: 10, k: 15, zsa: 12, zsd: 12),
        CrossCorrelations = Correlations(
            asdDs: 0.5, asaDs: 0.8, asaSf: -0.4, asdSf: -0.5, dsSf: -0.4, asdAsa: 0.4, asdK: -0.2,
            asaK: -0.3, dsK: -0.7, sfK: 0.5, zsdSf: 0, zsaSf: 0, zsdK: 0, zsaK: 0,
            zsdDs: 0, zsaDs: 0.2, zsdAsd: 0.5, zsaAsd: 0.3, zsdAsa: 0, zsaAsa: 0, zsdZsa: 0),
    };

    private static Tr38901Parameters UrbanMicroNlos() => new()
    {
        DelaySpreadMean = Law(-0.24, 1, -6.83), DelaySpreadSigma = Law(0.16, 1, 0.28),
        AzimuthSpreadDepartureMean = Law(-0.23, 1, 1.53), AzimuthSpreadDepartureSigma = Law(0.11, 1, 0.33),
        AzimuthSpreadArrivalMean = Law(-0.08, 1, 1.81), AzimuthSpreadArrivalSigma = Law(0.05, 1, 0.3),
        ZenithSpreadArrivalMean = Law(-0.04, 1, 0.92), ZenithSpreadArrivalSigma = Law(-0.07, 1, 0.41),
        ZenithSpreadDepartureSigma = Const(0.35),
        KFactorMeanDb = 0, KFactorSigmaDb = 0, ShadowFadingSigmaDb = 7.82,
        CrossPolarizationMeanDb = 8, CrossPolarizationSigmaDb = 3,
        ClusterCount = 19, DelayScaling = 2.1, ClusterShadowingDb = 3,
        ClusterDelaySpreadLaw = (11, 0, 0),
        ClusterAzimuthSpreadDepartureDeg = 10, ClusterAzimuthSpreadArrivalDeg = 22, ClusterZenithSpreadArrivalDeg = 7,
        AzimuthScaling = 1.273, ZenithScaling = 1.184,
        CorrelationDistances = Distances(ds: 10, asd: 10, asa: 9, sf: 13, k: 1, zsa: 10, zsd: 10),
        CrossCorrelations = Correlations(
            asdDs: 0, asaDs: 0.4, asaSf: -0.4, asdSf: 0, dsSf: -0.7, asdAsa: 0, asdK: 0,
            asaK: 0, dsK: 0, sfK: 0, zsdSf: 0, zsaSf: 0, zsdK: 0, zsaK: 0,
            zsdDs: -0.5, zsaDs: 0, zsdAsd: 0.5, zsaAsd: 0.5, zsdAsa: 0, zsaAsa: 0.2, zsdZsa: 0),
    };

    private static Tr38901Parameters RuralMacroLos() => new()
    {
        DelaySpreadMean = Const(-7.49), DelaySpreadSigma = Const(0.55),
        AzimuthSpreadDepartureMean = Const(0.90), AzimuthSpreadDepartureSigma = Const(0.38),
        AzimuthSpreadArrivalMean = Const(1.52), AzimuthSpreadArrivalSigma = Const(0.24),
        ZenithSpreadArrivalMean = Const(0.47), ZenithSpreadArrivalSigma = Const(0.40),
        ZenithSpreadDepartureSigma = Const(0.34),
        KFactorMeanDb = 7, KFactorSigmaDb = 4, ShadowFadingSigmaDb = 4,
        CrossPolarizationMeanDb = 12, CrossPolarizationSigmaDb = 4,
        ClusterCount = 11, DelayScaling = 3.8, ClusterShadowingDb = 3,
        ClusterDelaySpreadLaw = (3.91, 0, 0),
        ClusterAzimuthSpreadDepartureDeg = 2, ClusterAzimuthSpreadArrivalDeg = 3, ClusterZenithSpreadArrivalDeg = 3,
        AzimuthScaling = 1.123, ZenithScaling = 1.031,
        CorrelationDistances = Distances(ds: 50, asd: 25, asa: 35, sf: 37, k: 40, zsa: 15, zsd: 15),
        CrossCorrelations = Correlations(
            asdDs: 0, asaDs: 0, asaSf: 0, asdSf: 0, dsSf: -0.5, asdAsa: 0, asdK: 0,
            asaK: 0, dsK: 0, sfK: 0, zsdSf: 0.01, zsaSf: -0.17, zsdK: 0, zsaK: -0.02,
            zsdDs: -0.05, zsaDs: 0.27, zsdAsd: 0.73, zsaAsd: -0.14, zsdAsa: -0.20, zsaAsa: 0.24, zsdZsa: -0.07),
    };

    private static Tr38901Parameters RuralMacroNlos() => new()
    {
        DelaySpreadMean = Const(-7.43), DelaySpreadSigma = Const(0.48),
        AzimuthSpreadDepartureMean = Const(0.95), AzimuthSpreadDepartureSigma = Const(0.45),
        AzimuthSpreadArrivalMean = Const(1.52), AzimuthSpreadArrivalSigma = Const(0.13),
        ZenithSpreadArrivalMean = Const(0.58), ZenithSpreadArrivalSigma = Const(0.37),
        ZenithSpreadDepartureSigma = Const(0.30),
        KFactorMeanDb = 0, KFactorSigmaDb = 0, ShadowFadingSigmaDb = 8,
        CrossPolarizationMeanDb = 7, CrossPolarizationSigmaDb = 3,
        ClusterCount = 10, DelayScaling = 1.7, ClusterShadowingDb = 3,
        ClusterDelaySpreadLaw = (3.91, 0, 0),
        ClusterAzimuthSpreadDepartureDeg = 2, ClusterAzimuthSpreadArrivalDeg = 3, ClusterZenithSpreadArrivalDeg = 3,
        AzimuthScaling = 1.090, ZenithScaling = 0.957,
        CorrelationDistances = Distances(ds: 36, asd: 30, asa: 40, sf: 120, k: 1, zsa: 50, zsd: 50),
        CrossCorrelations = Correlations(
            asdDs: -0.4, asaDs: 0, asaSf: 0, asdSf: 0.6, dsSf: -0.5, asdAsa: 0, asdK: 0,
            asaK: 0, dsK: 0, sfK: 0, zsdSf: -0.04, zsaSf: -0.25, zsdK: 0, zsaK: 0,
            zsdDs: -0.10, zsaDs: -0.40, zsdAsd: 0.42, zsaAsd: -0.27, zsdAsa: -0.18, zsaAsa: 0.26, zsdZsa: -0.27),
    };

    private static Tr38901Parameters IndoorLos() => new()
    {
        DelaySpreadMean = Law(-0.01, 1, -7.692), DelaySpreadSigma = Const(0.18),
        AzimuthSpreadDepartureMean = Const(1.6), AzimuthSpreadDepartureSigma = Const(0.18),
        AzimuthSpreadArrivalMean = Law(-0.19, 1, 1.781), AzimuthSpreadArrivalSigma = Law(0.12, 1, 0.119),
        ZenithSpreadArrivalMean = Law(-0.26, 1, 1.44), ZenithSpreadArrivalSigma = Law(-0.04, 1, 0.264),
        ZenithSpreadDepartureSigma = Law(0.13, 1, 0.30),
        KFactorMeanDb = 7, KFactorSigmaDb = 4, ShadowFadingSigmaDb = 3,
        CrossPolarizationMeanDb = 11, CrossPolarizationSigmaDb = 4,
        ClusterCount = 15, DelayScaling = 3.6, ClusterShadowingDb = 6,
        ClusterDelaySpreadLaw = (3.91, 0, 0),
        ClusterAzimuthSpreadDepartureDeg = 5, ClusterAzimuthSpreadArrivalDeg = 8, ClusterZenithSpreadArrivalDeg = 9,
        AzimuthScaling = 1.211, ZenithScaling = 1.1088,
        CorrelationDistances = Distances(ds: 8, asd: 7, asa: 5, sf: 10, k: 4, zsa: 4, zsd: 4),
        CrossCorrelations = Correlations(
            asdDs: 0.6, asaDs: 0.8, asaSf: -0.5, asdSf: -0.4, dsSf: -0.8, asdAsa: 0.4, asdK: 0,
            asaK: 0, dsK: -0.5, sfK: 0.5, zsdSf: 0.2, zsaSf: 0.3, zsdK: 0, zsaK: 0.1,
            zsdDs: 0.1, zsaDs: 0.2, zsdAsd: 0.5, zsaAsd: 0, zsdAsa: 0, zsaAsa: 0.5, zsdZsa: 0),
    };

    private static Tr38901Parameters IndoorNlos() => new()
    {
        DelaySpreadMean = Law(-0.28, 1, -7.173), DelaySpreadSigma = Law(0.1, 1, 0.055),
        AzimuthSpreadDepartureMean = Const(1.62), AzimuthSpreadDepartureSigma = Const(0.25),
        AzimuthSpreadArrivalMean = Law(-0.11, 1, 1.863), AzimuthSpreadArrivalSigma = Law(0.12, 1, 0.059),
        ZenithSpreadArrivalMean = Law(-0.15, 1, 1.387), ZenithSpreadArrivalSigma = Law(-0.09, 1, 0.746),
        ZenithSpreadDepartureSigma = Const(0.36),
        KFactorMeanDb = 0, KFactorSigmaDb = 0, ShadowFadingSigmaDb = 8.03,
        CrossPolarizationMeanDb = 10, CrossPolarizationSigmaDb = 4,
        ClusterCount = 19, DelayScaling = 3, ClusterShadowingDb = 3,
        ClusterDelaySpreadLaw = (3.91, 0, 0),
        ClusterAzimuthSpreadDepartureDeg = 5, ClusterAzimuthSpreadArrivalDeg = 11, ClusterZenithSpreadArrivalDeg = 9,
        AzimuthScaling = 1.273, ZenithScaling = 1.184,
        CorrelationDistances = Distances(ds: 5, asd: 3, asa: 3, sf: 6, k: 1, zsa: 4, zsd: 4),
        CrossCorrelations = Correlations(
            asdDs: 0.4, asaDs: 0, asaSf: -0.4, asdSf: 0, dsSf: -0.5, asdAsa: 0, asdK: 0,
            asaK: 0, dsK: 0, sfK: 0, zsdSf: 0, zsaSf: 0, zsdK: 0, zsaK: 0,
            zsdDs: -0.27, zsaDs: -0.06, zsdAsd: 0.35, zsaAsd: 0.23, zsdAsa: -0.08, zsaAsa: 0.43, zsdZsa: 0.42),
    };

    #endregion
}
