using AI.Insights;
using AI.Units;

namespace AI.Physics.Mechanics;

/// <summary>
/// Кинематика точки: равноускоренное движение и баллистика без сопротивления среды.
/// </summary>
/// <remarks>
/// Все величины типизированы: подставить секунды туда, где ожидается скорость, нельзя —
/// это исключение, а не молча неверный ответ. Внутренние вычисления идут в единицах СИ.
/// </remarks>
public static class Kinematics
{
    /// <summary>Скорость при равноускоренном движении: <c>v = v₀ + a·t</c></summary>
    /// <param name="initialSpeed">Начальная скорость</param>
    /// <param name="acceleration">Ускорение</param>
    /// <param name="time">Время</param>
    public static Quantity Speed(Quantity initialSpeed, Quantity acceleration, Quantity time)
    {
        double v0 = initialSpeed.RequireSi(Dimension.Velocity, nameof(initialSpeed));
        double a = acceleration.RequireSi(Dimension.Acceleration, nameof(acceleration));
        double t = time.RequireSi(Dimension.TimeDim, nameof(time));

        return new Quantity(v0 + (a * t), Dimension.Velocity);
    }

    /// <summary>Перемещение: <c>s = v₀·t + a·t²/2</c></summary>
    /// <param name="initialSpeed">Начальная скорость</param>
    /// <param name="acceleration">Ускорение</param>
    /// <param name="time">Время</param>
    public static Quantity Displacement(Quantity initialSpeed, Quantity acceleration, Quantity time)
    {
        double v0 = initialSpeed.RequireSi(Dimension.Velocity, nameof(initialSpeed));
        double a = acceleration.RequireSi(Dimension.Acceleration, nameof(acceleration));
        double t = time.RequireSi(Dimension.TimeDim, nameof(time));

        return new Quantity((v0 * t) + (0.5 * a * t * t), Dimension.LengthDim);
    }

    /// <summary>Путь до остановки при торможении: <c>s = v₀²/(2a)</c></summary>
    /// <param name="initialSpeed">Начальная скорость</param>
    /// <param name="deceleration">Модуль замедления</param>
    public static Quantity StoppingDistance(Quantity initialSpeed, Quantity deceleration)
    {
        double v0 = initialSpeed.RequireSi(Dimension.Velocity, nameof(initialSpeed));
        double a = Math.Abs(deceleration.RequireSi(Dimension.Acceleration, nameof(deceleration)));

        return a <= 0
            ? new Quantity(double.PositiveInfinity, Dimension.LengthDim)
            : new Quantity(v0 * v0 / (2 * a), Dimension.LengthDim);
    }

    /// <summary>Время падения с высоты без начальной скорости</summary>
    /// <param name="height">Высота</param>
    /// <param name="gravity">Ускорение свободного падения; по умолчанию стандартное</param>
    public static Quantity FreeFallTime(Quantity height, Quantity gravity = default)
    {
        double h = height.RequireSi(Dimension.LengthDim, nameof(height));
        double g = Gravity(gravity);

        return new Quantity(Math.Sqrt(2 * h / g), Dimension.TimeDim);
    }

    internal static double Gravity(Quantity gravity)
        => gravity.Dimension.IsDimensionless && gravity.SiValue == 0.0
            ? PhysicalConstants.StandardGravity.SiValue
            : gravity.RequireSi(Dimension.Acceleration, nameof(gravity));
}

/// <summary>Характеристики баллистической траектории</summary>
/// <param name="Range">Дальность полёта</param>
/// <param name="MaxHeight">Наибольшая высота подъёма</param>
/// <param name="FlightTime">Полное время полёта</param>
/// <param name="ImpactSpeed">Скорость в момент падения</param>
public readonly record struct TrajectoryResult(
    Quantity Range, Quantity MaxHeight, Quantity FlightTime, Quantity ImpactSpeed) : IInterpretable
{
    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double range = Range.SiValue;
        double height = MaxHeight.SiValue;
        double time = FlightTime.SiValue;
        double speed = ImpactSpeed.SiValue;

        // Угол восстанавливается по форме траектории: H/R = tg θ / 4, а доля
        // от наибольшей дальности при той же скорости равна sin 2θ
        double angle = range > 0 ? Math.Atan(4 * height / range) * 180.0 / Math.PI : 90.0;
        double share = Math.Sin(2 * angle * Math.PI / 180.0);
        bool optimal = Math.Abs(angle - 45.0) < 0.5;

        return new InterpretationBuilder("Бросок в однородном поле тяжести")
            .Summary($"Дальность {Fmt.Num(range)} м, наибольшая высота {Fmt.Num(height)} м, "
                + $"время полёта {Fmt.Num(time)} с.")
            .Metric("Дальность", range, "м", "по горизонтали до возвращения на уровень старта")
            .Metric("Наибольшая высота", height, "м")
            .Metric("Время полёта", time, "с")
            .Metric("Скорость падения", speed, "м/с",
                "равна начальной: без сопротивления механическая энергия сохраняется")
            .Metric("Угол броска", angle, "°", "восстановлен по форме траектории: tg θ = 4·H/R",
                MetricQuality.Unknown, 1)
            .FindingIf(optimal, "Угол близок к 45°: при заданной скорости это наибольшая возможная дальность.")
            .FindingIf(!optimal && range > 0,
                $"Дальность составляет {Fmt.Pct(share)} от наибольшей возможной при той же скорости — "
                + "она достигается при 45°.")
            .Warning("Сопротивление воздуха не учтено. Для тяжёлого тела на малой скорости это честно, "
                + "для пули или мяча дальность завышена в разы, а угол наибольшей дальности на деле меньше 45°.")
            .Warning("Бросок с уровня земли на ту же высоту. При старте с возвышения дальность больше, "
                + "а угол наибольшей дальности меньше 45°.")
            .Build();
    }
}

/// <summary>
/// Баллистика в однородном поле тяжести без сопротивления среды.
/// </summary>
/// <remarks>
/// Приближение честно ровно там, где сопротивление мало: тяжёлое тело, небольшая скорость,
/// короткая траектория. Для пули или мяча оно завышает дальность в разы, и правильный ответ
/// требует численного интегрирования с силой сопротивления.
/// </remarks>
public static class Projectile
{
    /// <summary>Полные характеристики броска с уровня земли</summary>
    /// <param name="speed">Начальная скорость</param>
    /// <param name="angleDegrees">Угол к горизонту, градусы</param>
    /// <param name="gravity">Ускорение свободного падения; по умолчанию стандартное</param>
    public static TrajectoryResult Launch(Quantity speed, double angleDegrees, Quantity gravity = default)
    {
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));
        double g = Kinematics.Gravity(gravity);
        double angle = angleDegrees * Math.PI / 180.0;

        double vertical = v * Math.Sin(angle);
        double horizontal = v * Math.Cos(angle);

        double time = 2 * vertical / g;
        double range = horizontal * time;
        double height = vertical * vertical / (2 * g);

        return new TrajectoryResult(
            new Quantity(range, Dimension.LengthDim),
            new Quantity(height, Dimension.LengthDim),
            new Quantity(time, Dimension.TimeDim),
            new Quantity(v, Dimension.Velocity));
    }

    /// <summary>Угол, дающий наибольшую дальность при броске с уровня земли</summary>
    /// <remarks>Ровно 45°: дальность пропорциональна sin(2θ), а он максимален при 2θ = 90°.</remarks>
    public static double OptimalAngleDegrees => 45.0;

    /// <summary>Высота траектории на заданном расстоянии по горизонтали</summary>
    /// <param name="speed">Начальная скорость</param>
    /// <param name="angleDegrees">Угол к горизонту, градусы</param>
    /// <param name="distance">Расстояние по горизонтали</param>
    /// <param name="gravity">Ускорение свободного падения; по умолчанию стандартное</param>
    public static Quantity HeightAt(Quantity speed, double angleDegrees, Quantity distance, Quantity gravity = default)
    {
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));
        double x = distance.RequireSi(Dimension.LengthDim, nameof(distance));
        double g = Kinematics.Gravity(gravity);
        double angle = angleDegrees * Math.PI / 180.0;

        double cosine = Math.Cos(angle);
        double height = (x * Math.Tan(angle)) - (g * x * x / (2 * v * v * cosine * cosine));

        return new Quantity(height, Dimension.LengthDim);
    }
}
