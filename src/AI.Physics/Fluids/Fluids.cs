using AI.Insights;
using AI.Units;

namespace AI.Physics.Fluids;

/// <summary>
/// Гидростатика: давление, выталкивающая сила, барометрическая формула.
/// </summary>
public static class Hydrostatics
{
    /// <summary>Гидростатическое давление на глубине: <c>p = ρgh</c></summary>
    /// <param name="density">Плотность жидкости</param>
    /// <param name="depth">Глубина</param>
    /// <param name="gravity">Ускорение свободного падения; по умолчанию стандартное</param>
    public static Quantity Pressure(Quantity density, Quantity depth, Quantity gravity = default)
    {
        double rho = density.RequireSi(Dimension.Density, nameof(density));
        double h = depth.RequireSi(Dimension.LengthDim, nameof(depth));
        double g = StandardGravity(gravity);

        return new Quantity(rho * g * h, Dimension.Pressure);
    }

    /// <summary>Выталкивающая сила по закону Архимеда: <c>F = ρgV</c></summary>
    /// <param name="fluidDensity">Плотность жидкости</param>
    /// <param name="displacedVolume">Вытесненный объём</param>
    /// <param name="gravity">Ускорение свободного падения</param>
    public static Quantity Buoyancy(Quantity fluidDensity, Quantity displacedVolume, Quantity gravity = default)
    {
        double rho = fluidDensity.RequireSi(Dimension.Density, nameof(fluidDensity));
        double v = displacedVolume.RequireSi(Dimension.Volume, nameof(displacedVolume));
        double g = StandardGravity(gravity);

        return new Quantity(rho * g * v, Dimension.Force);
    }

    /// <summary>
    /// Доля объёма тела, погружённая при плавании
    /// </summary>
    /// <param name="bodyDensity">Плотность тела</param>
    /// <param name="fluidDensity">Плотность жидкости</param>
    /// <returns>Доля от нуля до единицы; единица означает, что тело тонет</returns>
    public static double SubmergedFraction(Quantity bodyDensity, Quantity fluidDensity)
    {
        double body = bodyDensity.RequireSi(Dimension.Density, nameof(bodyDensity));
        double fluid = fluidDensity.RequireSi(Dimension.Density, nameof(fluidDensity));

        return Math.Min(1.0, body / fluid);
    }

    /// <summary>
    /// Барометрическая формула для изотермической атмосферы
    /// </summary>
    /// <param name="groundPressure">Давление на нулевой высоте</param>
    /// <param name="height">Высота</param>
    /// <param name="temperature">Температура</param>
    /// <param name="molarMass">Молярная масса воздуха; по умолчанию 0.029 кг/моль</param>
    public static Quantity BarometricPressure(
        Quantity groundPressure, Quantity height, Quantity temperature, Quantity molarMass = default)
    {
        double p0 = groundPressure.RequireSi(Dimension.Pressure, nameof(groundPressure));
        double h = height.RequireSi(Dimension.LengthDim, nameof(height));
        double t = temperature.RequireSi(Dimension.TemperatureDim, nameof(temperature));

        Dimension molar = Dimension.MassDim / Dimension.AmountDim;

        double m = molarMass.Dimension.IsDimensionless && molarMass.SiValue == 0.0
            ? 0.0289644
            : molarMass.RequireSi(molar, nameof(molarMass));

        double g = PhysicalConstants.StandardGravity.SiValue;
        double r = PhysicalConstants.GasConstant.SiValue;

        return new Quantity(p0 * Math.Exp(-m * g * h / (r * t)), Dimension.Pressure);
    }

    internal static double StandardGravity(Quantity gravity)
        => gravity.Dimension.IsDimensionless && gravity.SiValue == 0.0
            ? PhysicalConstants.StandardGravity.SiValue
            : gravity.RequireSi(Dimension.Acceleration, nameof(gravity));
}

/// <summary>Режим течения в трубе</summary>
public enum FlowRegime
{
    /// <summary>Ламинарный: слои жидкости не перемешиваются, Re меньше 2300</summary>
    Laminar,

    /// <summary>Переходный: течение неустойчиво, Re от 2300 до 4000</summary>
    Transitional,

    /// <summary>Турбулентный: Re больше 4000</summary>
    Turbulent
}

/// <summary>Результат расчёта течения в трубе</summary>
/// <param name="Reynolds">Число Рейнольдса</param>
/// <param name="Regime">Режим течения</param>
/// <param name="FrictionFactor">Коэффициент гидравлического трения</param>
/// <param name="PressureLoss">Потеря давления на длине трубы</param>
public readonly record struct PipeFlowResult(
    double Reynolds, FlowRegime Regime, double FrictionFactor, Quantity PressureLoss) : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Течение в трубе")
            .Summary($"Число Рейнольдса {Fmt.Num(Reynolds, 0)} — {RegimeName(Regime)}. "
                + $"Коэффициент трения {Fmt.Num(FrictionFactor, 4)}, потеря давления "
                + $"{Fmt.Num(PressureLoss.In(UnitRegistry.Parse("kPa")), 2)} кПа.")
            .Metric("Число Рейнольдса", Fmt.Num(Reynolds, 0), null, "отношение сил инерции к силам вязкости")
            .Metric("Режим", RegimeName(Regime), null, "определяет и трение, и перемешивание",
                Regime == FlowRegime.Transitional ? MetricQuality.Warning : MetricQuality.Neutral)
            .Metric("Коэффициент трения", Fmt.Num(FrictionFactor, 4), null, "входит в формулу Дарси — Вейсбаха")
            .Metric("Потеря давления", Fmt.Num(PressureLoss.In(UnitRegistry.Parse("kPa")), 3), "кПа",
                "на заданной длине трубы")
            .FindingIf(Regime == FlowRegime.Laminar,
                "Ламинарный режим: коэффициент трения равен 64/Re и от шероховатости не зависит — "
                + "полировка трубы здесь ничего не даст.")
            .FindingIf(Regime == FlowRegime.Turbulent,
                "Турбулентный режим: трение зависит от шероховатости, и её уменьшение снижает потери.")
            .WarningIf(Regime == FlowRegime.Transitional,
                "Переходная область: расчёт ненадёжен, действительное трение может отличаться в полтора раза. "
                + "Режим неустойчив и в жизни — течение перескакивает между ламинарным и турбулентным.")
            .Warning("Формула Дарси — Вейсбаха учитывает только трение о стенки. Местные сопротивления — "
                + "повороты, задвижки, сужения — считаются отдельно и часто дают больше, чем сама труба.")
            .Build();

    private static string RegimeName(FlowRegime regime) => regime switch
    {
        FlowRegime.Laminar => "ламинарное течение",
        FlowRegime.Transitional => "переходный режим",
        _ => "турбулентное течение"
    };
}

/// <summary>
/// Динамика жидкости: уравнение Бернулли, режимы течения, потери на трение, сопротивление тел.
/// </summary>
public static class FlowDynamics
{
    /// <summary>
    /// Скорость истечения из отверстия по формуле Торричелли: <c>v = √(2gh)</c>
    /// </summary>
    /// <param name="head">Высота столба жидкости над отверстием</param>
    /// <param name="gravity">Ускорение свободного падения</param>
    public static Quantity TorricelliSpeed(Quantity head, Quantity gravity = default)
    {
        double h = head.RequireSi(Dimension.LengthDim, nameof(head));
        double g = Hydrostatics.StandardGravity(gravity);

        return new Quantity(Math.Sqrt(2 * g * h), Dimension.Velocity);
    }

    /// <summary>
    /// Скорость во втором сечении по уравнению неразрывности: <c>v₁S₁ = v₂S₂</c>
    /// </summary>
    /// <param name="firstSpeed">Скорость в первом сечении</param>
    /// <param name="firstArea">Площадь первого сечения</param>
    /// <param name="secondArea">Площадь второго сечения</param>
    public static Quantity ContinuitySpeed(Quantity firstSpeed, Quantity firstArea, Quantity secondArea)
    {
        double v1 = firstSpeed.RequireSi(Dimension.Velocity, nameof(firstSpeed));
        double s1 = firstArea.RequireSi(Dimension.Area, nameof(firstArea));
        double s2 = secondArea.RequireSi(Dimension.Area, nameof(secondArea));

        return new Quantity(v1 * s1 / s2, Dimension.Velocity);
    }

    /// <summary>
    /// Давление во втором сечении по уравнению Бернулли
    /// </summary>
    /// <param name="firstPressure">Давление в первом сечении</param>
    /// <param name="density">Плотность жидкости</param>
    /// <param name="firstSpeed">Скорость в первом сечении</param>
    /// <param name="secondSpeed">Скорость во втором сечении</param>
    /// <param name="heightDifference">Превышение второго сечения над первым</param>
    /// <param name="gravity">Ускорение свободного падения</param>
    public static Quantity BernoulliPressure(
        Quantity firstPressure, Quantity density,
        Quantity firstSpeed, Quantity secondSpeed,
        Quantity heightDifference = default, Quantity gravity = default)
    {
        double p1 = firstPressure.RequireSi(Dimension.Pressure, nameof(firstPressure));
        double rho = density.RequireSi(Dimension.Density, nameof(density));
        double v1 = firstSpeed.RequireSi(Dimension.Velocity, nameof(firstSpeed));
        double v2 = secondSpeed.RequireSi(Dimension.Velocity, nameof(secondSpeed));

        double dz = heightDifference.Dimension.IsDimensionless && heightDifference.SiValue == 0.0
            ? 0.0
            : heightDifference.RequireSi(Dimension.LengthDim, nameof(heightDifference));

        double g = Hydrostatics.StandardGravity(gravity);
        double p2 = p1 + (0.5 * rho * ((v1 * v1) - (v2 * v2))) - (rho * g * dz);

        return new Quantity(p2, Dimension.Pressure);
    }

    /// <summary>Число Рейнольдса: <c>Re = ρvd/μ</c></summary>
    /// <param name="density">Плотность</param>
    /// <param name="speed">Скорость</param>
    /// <param name="diameter">Характерный размер</param>
    /// <param name="viscosity">Динамическая вязкость</param>
    public static double Reynolds(Quantity density, Quantity speed, Quantity diameter, Quantity viscosity)
    {
        double rho = density.RequireSi(Dimension.Density, nameof(density));
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));
        double d = diameter.RequireSi(Dimension.LengthDim, nameof(diameter));
        double mu = viscosity.RequireSi(ViscosityDimension, nameof(viscosity));

        return rho * v * d / mu;
    }

    /// <summary>
    /// Полный расчёт течения в круглой трубе с потерями по Дарси — Вейсбаху
    /// </summary>
    /// <param name="density">Плотность</param>
    /// <param name="speed">Средняя скорость</param>
    /// <param name="diameter">Внутренний диаметр</param>
    /// <param name="viscosity">Динамическая вязкость</param>
    /// <param name="length">Длина трубы</param>
    /// <param name="roughness">Эквивалентная шероховатость; по умолчанию гладкая труба</param>
    public static PipeFlowResult PipeFlow(
        Quantity density, Quantity speed, Quantity diameter, Quantity viscosity,
        Quantity length, Quantity roughness = default)
    {
        double rho = density.RequireSi(Dimension.Density, nameof(density));
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));
        double d = diameter.RequireSi(Dimension.LengthDim, nameof(diameter));
        double l = length.RequireSi(Dimension.LengthDim, nameof(length));

        double eps = roughness.Dimension.IsDimensionless && roughness.SiValue == 0.0
            ? 0.0
            : roughness.RequireSi(Dimension.LengthDim, nameof(roughness));

        double re = Reynolds(density, speed, diameter, viscosity);
        FlowRegime regime = re < 2300 ? FlowRegime.Laminar : re <= 4000 ? FlowRegime.Transitional : FlowRegime.Turbulent;

        double friction = regime == FlowRegime.Laminar
            ? 64.0 / re
            : SwameeJain(re, eps / d);

        double loss = friction * l / d * rho * v * v / 2.0;

        return new PipeFlowResult(re, regime, friction, new Quantity(loss, Dimension.Pressure));
    }

    /// <summary>
    /// Сила лобового сопротивления: <c>F = C·ρv²S/2</c>
    /// </summary>
    /// <param name="dragCoefficient">Коэффициент сопротивления</param>
    /// <param name="density">Плотность среды</param>
    /// <param name="speed">Скорость</param>
    /// <param name="area">Площадь миделя</param>
    public static Quantity DragForce(double dragCoefficient, Quantity density, Quantity speed, Quantity area)
    {
        double rho = density.RequireSi(Dimension.Density, nameof(density));
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));
        double s = area.RequireSi(Dimension.Area, nameof(area));

        return new Quantity(0.5 * dragCoefficient * rho * v * v * s, Dimension.Force);
    }

    /// <summary>
    /// Установившаяся скорость падения при квадратичном сопротивлении
    /// </summary>
    /// <param name="mass">Масса тела</param>
    /// <param name="dragCoefficient">Коэффициент сопротивления</param>
    /// <param name="density">Плотность среды</param>
    /// <param name="area">Площадь миделя</param>
    /// <param name="gravity">Ускорение свободного падения</param>
    public static Quantity TerminalSpeed(
        Quantity mass, double dragCoefficient, Quantity density, Quantity area, Quantity gravity = default)
    {
        double m = mass.RequireSi(Dimension.MassDim, nameof(mass));
        double rho = density.RequireSi(Dimension.Density, nameof(density));
        double s = area.RequireSi(Dimension.Area, nameof(area));
        double g = Hydrostatics.StandardGravity(gravity);

        return new Quantity(Math.Sqrt(2 * m * g / (dragCoefficient * rho * s)), Dimension.Velocity);
    }

    /// <summary>
    /// Коэффициент трения по формуле Свами — Джайна — явное приближение уравнения Колбрука
    /// </summary>
    /// <remarks>
    /// Уравнение Колбрука неявное и требует итераций; приближение Свами — Джайна отличается
    /// от него не более чем на процент в диапазоне, где обе формулы применимы, и считается
    /// в одно действие.
    /// </remarks>
    private static double SwameeJain(double reynolds, double relativeRoughness)
    {
        double logarithm = Math.Log10((relativeRoughness / 3.7) + (5.74 / Math.Pow(reynolds, 0.9)));

        return 0.25 / (logarithm * logarithm);
    }

    /// <summary>Размерность динамической вязкости, Па·с</summary>
    public static Dimension ViscosityDimension { get; } = Dimension.Pressure * Dimension.TimeDim;
}
