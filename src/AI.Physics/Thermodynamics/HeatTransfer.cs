using AI.Insights;
using AI.Units;

namespace AI.Physics.Thermodynamics;

/// <summary>
/// Теплопередача: теплопроводность, конвекция и излучение.
/// </summary>
/// <remarks>
/// Три механизма складываются, но зависят от температуры по-разному: проводимость и конвекция
/// линейны по разности температур, излучение — по разности четвёртых степеней. Поэтому при
/// низких температурах излучением обычно пренебрегают, а при высоких оно становится главным.
/// </remarks>
public static class HeatTransfer
{
    /// <summary>Плотность теплового потока по закону Фурье: <c>q = λ·ΔT/δ</c></summary>
    /// <param name="conductivity">Коэффициент теплопроводности, Вт/(м·К)</param>
    /// <param name="temperatureDifference">Разность температур</param>
    /// <param name="thickness">Толщина стенки</param>
    public static Quantity Conduction(Quantity conductivity, Quantity temperatureDifference, Quantity thickness)
    {
        double lambda = conductivity.RequireSi(ConductivityDimension, nameof(conductivity));
        double delta = temperatureDifference.RequireSi(Dimension.TemperatureDim, nameof(temperatureDifference));
        double d = thickness.RequireSi(Dimension.LengthDim, nameof(thickness));

        return new Quantity(lambda * delta / d, FluxDimension);
    }

    /// <summary>Плотность потока при конвекции по закону Ньютона: <c>q = α·ΔT</c></summary>
    /// <param name="coefficient">Коэффициент теплоотдачи, Вт/(м²·К)</param>
    /// <param name="temperatureDifference">Разность температур</param>
    public static Quantity Convection(Quantity coefficient, Quantity temperatureDifference)
    {
        double alpha = coefficient.RequireSi(FilmDimension, nameof(coefficient));
        double delta = temperatureDifference.RequireSi(Dimension.TemperatureDim, nameof(temperatureDifference));

        return new Quantity(alpha * delta, FluxDimension);
    }

    /// <summary>
    /// Плотность потока излучения по закону Стефана — Больцмана: <c>q = ε·σ·T⁴</c>
    /// </summary>
    /// <param name="temperature">Абсолютная температура</param>
    /// <param name="emissivity">Степень черноты от нуля до единицы</param>
    public static Quantity Radiation(Quantity temperature, double emissivity = 1.0)
    {
        double t = temperature.RequireSi(Dimension.TemperatureDim, nameof(temperature));

        ArgumentOutOfRangeException.ThrowIfNegative(emissivity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(emissivity, 1.0);

        double sigma = PhysicalConstants.StefanBoltzmannConstant.SiValue;

        return new Quantity(emissivity * sigma * Math.Pow(t, 4), FluxDimension);
    }

    /// <summary>
    /// Результирующий поток излучения между телом и окружением
    /// </summary>
    /// <param name="bodyTemperature">Температура тела</param>
    /// <param name="ambientTemperature">Температура окружения</param>
    /// <param name="emissivity">Степень черноты</param>
    public static Quantity NetRadiation(Quantity bodyTemperature, Quantity ambientTemperature, double emissivity = 1.0)
    {
        double body = bodyTemperature.RequireSi(Dimension.TemperatureDim, nameof(bodyTemperature));
        double ambient = ambientTemperature.RequireSi(Dimension.TemperatureDim, nameof(ambientTemperature));

        double sigma = PhysicalConstants.StefanBoltzmannConstant.SiValue;
        double flux = emissivity * sigma * (Math.Pow(body, 4) - Math.Pow(ambient, 4));

        return new Quantity(flux, FluxDimension);
    }

    /// <summary>
    /// Термическое сопротивление многослойной стенки: <c>R = Σ δᵢ/λᵢ</c>
    /// </summary>
    /// <param name="thicknesses">Толщины слоёв</param>
    /// <param name="conductivities">Теплопроводности слоёв</param>
    public static Quantity WallResistance(IReadOnlyList<Quantity> thicknesses, IReadOnlyList<Quantity> conductivities)
    {
        ArgumentNullException.ThrowIfNull(thicknesses);
        ArgumentNullException.ThrowIfNull(conductivities);

        if (thicknesses.Count != conductivities.Count)
            throw new ArgumentException("Число слоёв и число теплопроводностей должны совпадать", nameof(conductivities));

        double total = 0;

        for (int layer = 0; layer < thicknesses.Count; layer++)
        {
            double d = thicknesses[layer].RequireSi(Dimension.LengthDim, nameof(thicknesses));
            double lambda = conductivities[layer].RequireSi(ConductivityDimension, nameof(conductivities));

            total += d / lambda;
        }

        return new Quantity(total, Dimension.TemperatureDim * Dimension.Area / Dimension.Power);
    }

    /// <summary>
    /// Время остывания тела по закону Ньютона при малом числе Био
    /// </summary>
    /// <param name="initialExcess">Начальное превышение температуры над средой</param>
    /// <param name="finalExcess">Конечное превышение</param>
    /// <param name="timeConstant">Постоянная времени остывания</param>
    public static Quantity CoolingTime(Quantity initialExcess, Quantity finalExcess, Quantity timeConstant)
    {
        double start = initialExcess.RequireSi(Dimension.TemperatureDim, nameof(initialExcess));
        double end = finalExcess.RequireSi(Dimension.TemperatureDim, nameof(finalExcess));
        double tau = timeConstant.RequireSi(Dimension.TimeDim, nameof(timeConstant));

        if (start <= 0 || end <= 0 || end > start)
            throw new ArgumentException("Превышения температур должны быть положительными и убывать", nameof(finalExcess));

        return new Quantity(tau * Math.Log(start / end), Dimension.TimeDim);
    }

    /// <summary>Размерность плотности теплового потока, Вт/м²</summary>
    public static Dimension FluxDimension { get; } = Dimension.Power / Dimension.Area;

    /// <summary>Размерность теплопроводности, Вт/(м·К)</summary>
    public static Dimension ConductivityDimension { get; } = Dimension.Power / Dimension.LengthDim / Dimension.TemperatureDim;

    /// <summary>Размерность коэффициента теплоотдачи, Вт/(м²·К)</summary>
    public static Dimension FilmDimension { get; } = Dimension.Power / Dimension.Area / Dimension.TemperatureDim;
}

/// <summary>Показатели термодинамического цикла</summary>
/// <param name="Efficiency">Термический КПД</param>
/// <param name="Name">Название цикла</param>
public readonly record struct CycleResult(double Efficiency, string Name) : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder($"Цикл: {Name}")
            .Summary($"Термический КПД {Fmt.Pct(Efficiency)}. Это предел для идеализированного цикла: "
                + "трение, теплообмен при конечной разности температур и утечки его только уменьшают.")
            .Metric("КПД", Fmt.Pct(Efficiency), null, "доля подведённой теплоты, обращённая в работу",
                Efficiency > 0.5 ? MetricQuality.Good : Efficiency > 0.25 ? MetricQuality.Neutral : MetricQuality.Warning)
            .Metric("Потери", Fmt.Pct(1 - Efficiency), null, "доля теплоты, отданная холодильнику")
            .Finding("Второе начало запрещает обратить в работу всю подведённую теплоту: часть обязана "
                + "уйти холодильнику, и эта часть тем больше, чем ближе температуры нагревателя и холодильника.")
            .Warning("Расчёт идеализирован: процессы считаются обратимыми, рабочее тело — идеальным газом "
                + "с постоянной теплоёмкостью. Действительный КПД машины ниже на треть и более.")
            .Build();
}

/// <summary>
/// Термодинамические циклы тепловых машин.
/// </summary>
public static class Cycles
{
    /// <summary>
    /// КПД цикла Карно: <c>η = 1 − T_х/T_н</c> — верхняя граница для любой тепловой машины
    /// </summary>
    /// <param name="hotTemperature">Температура нагревателя</param>
    /// <param name="coldTemperature">Температура холодильника</param>
    public static CycleResult Carnot(Quantity hotTemperature, Quantity coldTemperature)
    {
        (double hot, double cold) = ReadTemperatures(hotTemperature, coldTemperature);

        return new CycleResult(1 - (cold / hot), "Карно");
    }

    /// <summary>
    /// КПД цикла Отто: <c>η = 1 − r^(1−γ)</c>, где r — степень сжатия
    /// </summary>
    /// <param name="compressionRatio">Степень сжатия</param>
    /// <param name="kind">Род рабочего тела</param>
    public static CycleResult Otto(double compressionRatio, GasKind kind = GasKind.Diatomic)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(compressionRatio, 1.0);

        double gamma = IdealGas.HeatCapacityRatio(kind);

        return new CycleResult(1 - Math.Pow(compressionRatio, 1 - gamma), "Отто");
    }

    /// <summary>
    /// КПД цикла Дизеля
    /// </summary>
    /// <param name="compressionRatio">Степень сжатия</param>
    /// <param name="cutoffRatio">Степень предварительного расширения</param>
    /// <param name="kind">Род рабочего тела</param>
    public static CycleResult Diesel(double compressionRatio, double cutoffRatio, GasKind kind = GasKind.Diatomic)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(compressionRatio, 1.0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cutoffRatio, 1.0);

        double gamma = IdealGas.HeatCapacityRatio(kind);
        double numerator = Math.Pow(cutoffRatio, gamma) - 1;
        double denominator = gamma * (cutoffRatio - 1);

        return new CycleResult(1 - (Math.Pow(compressionRatio, 1 - gamma) * numerator / denominator), "Дизеля");
    }

    /// <summary>
    /// Холодильный коэффициент идеальной холодильной машины
    /// </summary>
    /// <param name="hotTemperature">Температура нагревателя</param>
    /// <param name="coldTemperature">Температура холодильника</param>
    public static double RefrigeratorCoefficient(Quantity hotTemperature, Quantity coldTemperature)
    {
        (double hot, double cold) = ReadTemperatures(hotTemperature, coldTemperature);

        return cold / (hot - cold);
    }

    /// <summary>
    /// Коэффициент преобразования теплового насоса
    /// </summary>
    /// <param name="hotTemperature">Температура нагреваемого помещения</param>
    /// <param name="coldTemperature">Температура источника</param>
    public static double HeatPumpCoefficient(Quantity hotTemperature, Quantity coldTemperature)
    {
        (double hot, double cold) = ReadTemperatures(hotTemperature, coldTemperature);

        return hot / (hot - cold);
    }

    private static (double Hot, double Cold) ReadTemperatures(Quantity hot, Quantity cold)
    {
        double h = hot.RequireSi(Dimension.TemperatureDim, nameof(hot));
        double c = cold.RequireSi(Dimension.TemperatureDim, nameof(cold));

        if (h <= 0 || c <= 0)
            throw new ArgumentException("Температуры задаются по абсолютной шкале", nameof(hot));

        if (c >= h)
            throw new ArgumentException("Температура холодильника должна быть ниже температуры нагревателя", nameof(cold));

        return (h, c);
    }
}
