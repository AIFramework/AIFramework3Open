using AI.Units;

namespace AI.Physics.Electromagnetism;

/// <summary>
/// Электростатика: взаимодействие зарядов, поле, потенциал, ёмкость.
/// </summary>
public static class Electrostatics
{
    /// <summary>Сила взаимодействия точечных зарядов по закону Кулона</summary>
    /// <param name="firstCharge">Первый заряд</param>
    /// <param name="secondCharge">Второй заряд</param>
    /// <param name="distance">Расстояние между зарядами</param>
    /// <param name="relativePermittivity">Относительная диэлектрическая проницаемость среды</param>
    public static Quantity CoulombForce(
        Quantity firstCharge, Quantity secondCharge, Quantity distance, double relativePermittivity = 1.0)
    {
        double q1 = firstCharge.RequireSi(Dimension.Charge, nameof(firstCharge));
        double q2 = secondCharge.RequireSi(Dimension.Charge, nameof(secondCharge));
        double r = distance.RequireSi(Dimension.LengthDim, nameof(distance));

        if (r <= 0)
            throw new ArgumentException("Расстояние должно быть положительным", nameof(distance));

        double epsilon = PhysicalConstants.VacuumPermittivity.SiValue * relativePermittivity;
        double force = q1 * q2 / (4 * Math.PI * epsilon * r * r);

        return new Quantity(force, Dimension.Force);
    }

    /// <summary>Напряжённость поля точечного заряда</summary>
    /// <param name="charge">Заряд</param>
    /// <param name="distance">Расстояние</param>
    /// <param name="relativePermittivity">Относительная диэлектрическая проницаемость</param>
    public static Quantity FieldOfPointCharge(Quantity charge, Quantity distance, double relativePermittivity = 1.0)
    {
        double q = charge.RequireSi(Dimension.Charge, nameof(charge));
        double r = distance.RequireSi(Dimension.LengthDim, nameof(distance));
        double epsilon = PhysicalConstants.VacuumPermittivity.SiValue * relativePermittivity;

        return new Quantity(q / (4 * Math.PI * epsilon * r * r), FieldDimension);
    }

    /// <summary>Потенциал поля точечного заряда</summary>
    /// <param name="charge">Заряд</param>
    /// <param name="distance">Расстояние</param>
    /// <param name="relativePermittivity">Относительная диэлектрическая проницаемость</param>
    public static Quantity Potential(Quantity charge, Quantity distance, double relativePermittivity = 1.0)
    {
        double q = charge.RequireSi(Dimension.Charge, nameof(charge));
        double r = distance.RequireSi(Dimension.LengthDim, nameof(distance));
        double epsilon = PhysicalConstants.VacuumPermittivity.SiValue * relativePermittivity;

        return new Quantity(q / (4 * Math.PI * epsilon * r), Dimension.Voltage);
    }

    /// <summary>Ёмкость плоского конденсатора: <c>C = ε·S/d</c></summary>
    /// <param name="area">Площадь пластины</param>
    /// <param name="separation">Расстояние между пластинами</param>
    /// <param name="relativePermittivity">Относительная диэлектрическая проницаемость</param>
    public static Quantity ParallelPlateCapacitance(
        Quantity area, Quantity separation, double relativePermittivity = 1.0)
    {
        double s = area.RequireSi(Dimension.Area, nameof(area));
        double d = separation.RequireSi(Dimension.LengthDim, nameof(separation));
        double epsilon = PhysicalConstants.VacuumPermittivity.SiValue * relativePermittivity;

        return new Quantity(epsilon * s / d, Dimension.Capacitance);
    }

    /// <summary>Энергия заряженного конденсатора: <c>W = C·U²/2</c></summary>
    /// <param name="capacitance">Ёмкость</param>
    /// <param name="voltage">Напряжение</param>
    public static Quantity CapacitorEnergy(Quantity capacitance, Quantity voltage)
    {
        double c = capacitance.RequireSi(Dimension.Capacitance, nameof(capacitance));
        double u = voltage.RequireSi(Dimension.Voltage, nameof(voltage));

        return new Quantity(0.5 * c * u * u, Dimension.Energy);
    }

    /// <summary>Размерность напряжённости электрического поля, В/м</summary>
    public static Dimension FieldDimension { get; } = Dimension.Voltage / Dimension.LengthDim;
}

/// <summary>Режим переходного процесса в контуре</summary>
public enum CircuitRegime
{
    /// <summary>Колебательный: ток и напряжение колеблются с убывающей амплитудой</summary>
    Underdamped,

    /// <summary>Критический</summary>
    Critical,

    /// <summary>Апериодический</summary>
    Overdamped
}

/// <summary>
/// Цепи постоянного и переменного тока: закон Ома, переходные процессы, резонанс.
/// </summary>
public static class Circuits
{
    /// <summary>Ток по закону Ома: <c>I = U/R</c></summary>
    /// <param name="voltage">Напряжение</param>
    /// <param name="resistance">Сопротивление</param>
    public static Quantity Current(Quantity voltage, Quantity resistance)
    {
        double u = voltage.RequireSi(Dimension.Voltage, nameof(voltage));
        double r = resistance.RequireSi(Dimension.Resistance, nameof(resistance));

        return new Quantity(u / r, Dimension.CurrentDim);
    }

    /// <summary>Мощность: <c>P = U·I</c></summary>
    /// <param name="voltage">Напряжение</param>
    /// <param name="current">Ток</param>
    public static Quantity Power(Quantity voltage, Quantity current)
    {
        double u = voltage.RequireSi(Dimension.Voltage, nameof(voltage));
        double i = current.RequireSi(Dimension.CurrentDim, nameof(current));

        return new Quantity(u * i, Dimension.Power);
    }

    /// <summary>Постоянная времени RC-цепи</summary>
    /// <param name="resistance">Сопротивление</param>
    /// <param name="capacitance">Ёмкость</param>
    public static Quantity TimeConstantRC(Quantity resistance, Quantity capacitance)
    {
        double r = resistance.RequireSi(Dimension.Resistance, nameof(resistance));
        double c = capacitance.RequireSi(Dimension.Capacitance, nameof(capacitance));

        return new Quantity(r * c, Dimension.TimeDim);
    }

    /// <summary>Постоянная времени RL-цепи</summary>
    /// <param name="inductance">Индуктивность</param>
    /// <param name="resistance">Сопротивление</param>
    public static Quantity TimeConstantRL(Quantity inductance, Quantity resistance)
    {
        double l = inductance.RequireSi(Dimension.Inductance, nameof(inductance));
        double r = resistance.RequireSi(Dimension.Resistance, nameof(resistance));

        return new Quantity(l / r, Dimension.TimeDim);
    }

    /// <summary>
    /// Напряжение на конденсаторе при заряде через сопротивление
    /// </summary>
    /// <param name="supply">Напряжение источника</param>
    /// <param name="timeConstant">Постоянная времени</param>
    /// <param name="time">Время от начала заряда</param>
    public static Quantity ChargingVoltage(Quantity supply, Quantity timeConstant, Quantity time)
    {
        double u = supply.RequireSi(Dimension.Voltage, nameof(supply));
        double tau = timeConstant.RequireSi(Dimension.TimeDim, nameof(timeConstant));
        double t = time.RequireSi(Dimension.TimeDim, nameof(time));

        return new Quantity(u * (1 - Math.Exp(-t / tau)), Dimension.Voltage);
    }

    /// <summary>Резонансная частота контура: <c>f = 1/(2π√(LC))</c></summary>
    /// <param name="inductance">Индуктивность</param>
    /// <param name="capacitance">Ёмкость</param>
    public static Quantity ResonanceFrequency(Quantity inductance, Quantity capacitance)
    {
        double l = inductance.RequireSi(Dimension.Inductance, nameof(inductance));
        double c = capacitance.RequireSi(Dimension.Capacitance, nameof(capacitance));

        return new Quantity(1.0 / (2 * Math.PI * Math.Sqrt(l * c)), Dimension.Frequency);
    }

    /// <summary>
    /// Добротность последовательного контура: <c>Q = √(L/C)/R</c>
    /// </summary>
    /// <param name="inductance">Индуктивность</param>
    /// <param name="capacitance">Ёмкость</param>
    /// <param name="resistance">Сопротивление</param>
    public static double QualityFactor(Quantity inductance, Quantity capacitance, Quantity resistance)
    {
        double l = inductance.RequireSi(Dimension.Inductance, nameof(inductance));
        double c = capacitance.RequireSi(Dimension.Capacitance, nameof(capacitance));
        double r = resistance.RequireSi(Dimension.Resistance, nameof(resistance));

        return Math.Sqrt(l / c) / r;
    }

    /// <summary>Режим переходного процесса в последовательном контуре</summary>
    /// <param name="inductance">Индуктивность</param>
    /// <param name="capacitance">Ёмкость</param>
    /// <param name="resistance">Сопротивление</param>
    public static CircuitRegime Regime(Quantity inductance, Quantity capacitance, Quantity resistance)
    {
        double quality = QualityFactor(inductance, capacitance, resistance);

        // Колебательный режим начинается там же, где у механического осциллятора: ζ = 1/(2Q)
        return quality switch
        {
            > 0.5 => CircuitRegime.Underdamped,
            0.5 => CircuitRegime.Critical,
            _ => CircuitRegime.Overdamped
        };
    }

    /// <summary>Полное сопротивление последовательного контура на заданной частоте</summary>
    /// <param name="resistance">Сопротивление</param>
    /// <param name="inductance">Индуктивность</param>
    /// <param name="capacitance">Ёмкость</param>
    /// <param name="frequency">Частота</param>
    public static Quantity Impedance(
        Quantity resistance, Quantity inductance, Quantity capacitance, Quantity frequency)
    {
        double r = resistance.RequireSi(Dimension.Resistance, nameof(resistance));
        double l = inductance.RequireSi(Dimension.Inductance, nameof(inductance));
        double c = capacitance.RequireSi(Dimension.Capacitance, nameof(capacitance));
        double f = frequency.RequireSi(Dimension.Frequency, nameof(frequency));

        double omega = 2 * Math.PI * f;
        double reactance = (omega * l) - (1.0 / (omega * c));

        return new Quantity(Math.Sqrt((r * r) + (reactance * reactance)), Dimension.Resistance);
    }
}
