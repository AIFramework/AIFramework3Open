using AI.Insights;
using AI.Units;

namespace AI.Physics.Mechanics;

/// <summary>Режим колебаний осциллятора</summary>
public enum DampingRegime
{
    /// <summary>Затухания нет</summary>
    Undamped,

    /// <summary>Колебательный режим: система колеблется с убывающей амплитудой</summary>
    Underdamped,

    /// <summary>Критическое затухание: возврат к равновесию за наименьшее время без колебаний</summary>
    Critical,

    /// <summary>Апериодический режим: возврат медленнее критического, без колебаний</summary>
    Overdamped
}

/// <summary>
/// Гармонический осциллятор с вязким затуханием: <c>m·ẍ + c·ẋ + k·x = 0</c>
/// </summary>
public sealed class HarmonicOscillator : IInterpretable
{
    /// <summary>Создаёт осциллятор</summary>
    /// <param name="mass">Масса</param>
    /// <param name="stiffness">Жёсткость, Н/м</param>
    /// <param name="damping">Коэффициент вязкого трения, Н·с/м; по умолчанию нулевой</param>
    public HarmonicOscillator(Quantity mass, Quantity stiffness, Quantity damping = default)
    {
        Mass = mass.RequireSi(Dimension.MassDim, nameof(mass));
        Stiffness = stiffness.RequireSi(Dimension.Force / Dimension.LengthDim, nameof(stiffness));

        Damping = damping.Dimension.IsDimensionless && damping.SiValue == 0.0
            ? 0.0
            : damping.RequireSi(Dimension.Force / Dimension.Velocity, nameof(damping));

        if (Mass <= 0 || Stiffness <= 0)
            throw new ArgumentException("Масса и жёсткость должны быть положительными", nameof(mass));
    }

    /// <summary>Масса, кг</summary>
    public double Mass { get; }

    /// <summary>Жёсткость, Н/м</summary>
    public double Stiffness { get; }

    /// <summary>Коэффициент вязкого трения, Н·с/м</summary>
    public double Damping { get; }

    /// <summary>Собственная круговая частота недемпфированной системы</summary>
    public Quantity NaturalFrequency => new(Math.Sqrt(Stiffness / Mass), Dimension.Frequency);

    /// <summary>Период свободных колебаний без затухания</summary>
    public Quantity Period => new(2 * Math.PI * Math.Sqrt(Mass / Stiffness), Dimension.TimeDim);

    /// <summary>Критический коэффициент затухания</summary>
    public double CriticalDamping => 2 * Math.Sqrt(Stiffness * Mass);

    /// <summary>Относительное затухание ζ</summary>
    public double DampingRatio => Damping / CriticalDamping;

    /// <summary>Режим колебаний</summary>
    public DampingRegime Regime => DampingRatio switch
    {
        0 => DampingRegime.Undamped,
        < 1 => DampingRegime.Underdamped,
        1 => DampingRegime.Critical,
        _ => DampingRegime.Overdamped
    };

    /// <summary>
    /// Частота затухающих колебаний <c>ω_d = ω₀·√(1 − ζ²)</c>;
    /// не определена вне колебательного режима
    /// </summary>
    public Quantity DampedFrequency
    {
        get
        {
            double ratio = DampingRatio;

            return ratio >= 1
                ? new Quantity(double.NaN, Dimension.Frequency)
                : new Quantity(NaturalFrequency.SiValue * Math.Sqrt(1 - (ratio * ratio)), Dimension.Frequency);
        }
    }

    /// <summary>Добротность <c>Q = 1/(2ζ)</c></summary>
    public double QualityFactor => DampingRatio <= 0 ? double.PositiveInfinity : 1.0 / (2 * DampingRatio);

    /// <summary>
    /// Отклонение в момент времени при начальном смещении и нулевой начальной скорости
    /// </summary>
    /// <param name="initialDisplacement">Начальное отклонение</param>
    /// <param name="time">Время</param>
    public Quantity Displacement(Quantity initialDisplacement, Quantity time)
    {
        double x0 = initialDisplacement.RequireSi(Dimension.LengthDim, nameof(initialDisplacement));
        double t = time.RequireSi(Dimension.TimeDim, nameof(time));
        double omega = NaturalFrequency.SiValue;
        double zeta = DampingRatio;

        double value = zeta switch
        {
            0 => x0 * Math.Cos(omega * t),
            < 1 => Underdamped(x0, t, omega, zeta),
            1 => x0 * (1 + (omega * t)) * Math.Exp(-omega * t),
            _ => Overdamped(x0, t, omega, zeta)
        };

        return new Quantity(value, Dimension.LengthDim);
    }

    private static double Underdamped(double x0, double t, double omega, double zeta)
    {
        double damped = omega * Math.Sqrt(1 - (zeta * zeta));
        double envelope = Math.Exp(-zeta * omega * t);

        return envelope * x0 * ((Math.Cos(damped * t)) + (zeta / Math.Sqrt(1 - (zeta * zeta)) * Math.Sin(damped * t)));
    }

    private static double Overdamped(double x0, double t, double omega, double zeta)
    {
        double root = Math.Sqrt((zeta * zeta) - 1);
        double first = -omega * (zeta - root);
        double second = -omega * (zeta + root);

        double a = x0 * second / (second - first);
        double b = x0 - a;

        return (a * Math.Exp(first * t)) + (b * Math.Exp(second * t));
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double zeta = DampingRatio;

        return new InterpretationBuilder("Гармонический осциллятор")
            .Summary($"Собственная частота {Fmt.Num(NaturalFrequency.SiValue, 4)} рад/с, "
                + $"период {Fmt.Num(Period.SiValue, 4)} с, относительное затухание {Fmt.Num(zeta, 4)} — "
                + $"{RegimeName(Regime)}.")
            .Metric("Собственная частота", Fmt.Num(NaturalFrequency.SiValue, 4), "рад/с", "√(k/m)")
            .Metric("Период", Fmt.Num(Period.SiValue, 4), "с", "без учёта затухания")
            .Metric("Относительное затухание", Fmt.Num(zeta, 4), null, "единица отвечает критическому",
                zeta is > 0.3 and < 1.5 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Добротность", Fmt.Num(QualityFactor, 2), null, "во сколько раз запас энергии больше потерь за период")
            .FindingIf(Regime == DampingRegime.Underdamped,
                $"Система колеблется с частотой {Fmt.Num(DampedFrequency.SiValue, 4)} рад/с; "
                + "амплитуда убывает по экспоненте.")
            .FindingIf(Regime == DampingRegime.Critical,
                "Критическое затухание: возврат к равновесию за наименьшее время без перерегулирования — "
                + "то, к чему стремятся при настройке амортизаторов и приводов.")
            .FindingIf(Regime == DampingRegime.Overdamped,
                "Апериодический режим: система возвращается к равновесию медленнее, чем при критическом "
                + "затухании. Увеличение трения здесь только вредит быстродействию.")
            .WarningIf(zeta < 0.05 && zeta > 0,
                "Затухание мало: вблизи резонанса амплитуда вырастет в десятки раз. Проверьте, "
                + "не попадает ли рабочая частота в эту область.")
            .Warning("Модель линейная: трение считается вязким, жёсткость постоянной. Сухое трение "
                + "и нелинейная пружина дают качественно иное поведение.")
            .Build();
    }

    private static string RegimeName(DampingRegime regime) => regime switch
    {
        DampingRegime.Undamped => "затухания нет",
        DampingRegime.Underdamped => "колебательный режим",
        DampingRegime.Critical => "критическое затухание",
        _ => "апериодический режим"
    };
}

/// <summary>Результат столкновения двух тел вдоль прямой</summary>
/// <param name="FirstSpeed">Скорость первого тела после удара</param>
/// <param name="SecondSpeed">Скорость второго тела после удара</param>
/// <param name="EnergyLoss">Потерянная кинетическая энергия</param>
public readonly record struct CollisionResult(Quantity FirstSpeed, Quantity SecondSpeed, Quantity EnergyLoss);

/// <summary>
/// Столкновения тел вдоль прямой.
/// </summary>
/// <remarks>
/// Импульс сохраняется всегда, энергия — только при упругом ударе. Коэффициент
/// восстановления от нуля (тела слипаются) до единицы (упругий удар) описывает всё
/// промежуточное, и именно он, а не «потери в процентах», является измеряемой величиной.
/// </remarks>
public static class Collisions
{
    /// <summary>
    /// Столкновение с заданным коэффициентом восстановления
    /// </summary>
    /// <param name="firstMass">Масса первого тела</param>
    /// <param name="firstSpeed">Скорость первого тела до удара</param>
    /// <param name="secondMass">Масса второго тела</param>
    /// <param name="secondSpeed">Скорость второго тела до удара</param>
    /// <param name="restitution">Коэффициент восстановления от нуля до единицы</param>
    public static CollisionResult Collide(
        Quantity firstMass, Quantity firstSpeed,
        Quantity secondMass, Quantity secondSpeed,
        double restitution = 1.0)
    {
        double m1 = firstMass.RequireSi(Dimension.MassDim, nameof(firstMass));
        double m2 = secondMass.RequireSi(Dimension.MassDim, nameof(secondMass));
        double u1 = firstSpeed.RequireSi(Dimension.Velocity, nameof(firstSpeed));
        double u2 = secondSpeed.RequireSi(Dimension.Velocity, nameof(secondSpeed));

        ArgumentOutOfRangeException.ThrowIfNegative(restitution);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(restitution, 1.0);

        double total = m1 + m2;
        double v1 = ((m1 * u1) + (m2 * u2) + (m2 * restitution * (u2 - u1))) / total;
        double v2 = ((m1 * u1) + (m2 * u2) + (m1 * restitution * (u1 - u2))) / total;

        double before = (0.5 * m1 * u1 * u1) + (0.5 * m2 * u2 * u2);
        double after = (0.5 * m1 * v1 * v1) + (0.5 * m2 * v2 * v2);

        return new CollisionResult(
            new Quantity(v1, Dimension.Velocity),
            new Quantity(v2, Dimension.Velocity),
            new Quantity(before - after, Dimension.Energy));
    }

    /// <summary>Кинетическая энергия тела</summary>
    /// <param name="mass">Масса</param>
    /// <param name="speed">Скорость</param>
    public static Quantity KineticEnergy(Quantity mass, Quantity speed)
    {
        double m = mass.RequireSi(Dimension.MassDim, nameof(mass));
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));

        return new Quantity(0.5 * m * v * v, Dimension.Energy);
    }

    /// <summary>Импульс тела</summary>
    /// <param name="mass">Масса</param>
    /// <param name="speed">Скорость</param>
    public static Quantity Momentum(Quantity mass, Quantity speed)
    {
        double m = mass.RequireSi(Dimension.MassDim, nameof(mass));
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));

        return new Quantity(m * v, Dimension.MassDim * Dimension.Velocity);
    }
}

/// <summary>
/// Моменты инерции однородных тел и вращательная динамика.
/// </summary>
public static class RigidBody
{
    /// <summary>Момент инерции сплошного цилиндра или диска относительно оси симметрии</summary>
    /// <param name="mass">Масса</param>
    /// <param name="radius">Радиус</param>
    public static Quantity SolidCylinder(Quantity mass, Quantity radius)
        => Inertia(mass, radius, 0.5);

    /// <summary>Момент инерции тонкого обруча или трубы относительно оси симметрии</summary>
    /// <param name="mass">Масса</param>
    /// <param name="radius">Радиус</param>
    public static Quantity Hoop(Quantity mass, Quantity radius) => Inertia(mass, radius, 1.0);

    /// <summary>Момент инерции сплошного шара относительно диаметра</summary>
    /// <param name="mass">Масса</param>
    /// <param name="radius">Радиус</param>
    public static Quantity SolidSphere(Quantity mass, Quantity radius) => Inertia(mass, radius, 0.4);

    /// <summary>Момент инерции сферической оболочки относительно диаметра</summary>
    /// <param name="mass">Масса</param>
    /// <param name="radius">Радиус</param>
    public static Quantity SphericalShell(Quantity mass, Quantity radius) => Inertia(mass, radius, 2.0 / 3.0);

    /// <summary>Момент инерции стержня относительно оси через центр перпендикулярно стержню</summary>
    /// <param name="mass">Масса</param>
    /// <param name="length">Длина</param>
    public static Quantity RodAboutCentre(Quantity mass, Quantity length) => Inertia(mass, length, 1.0 / 12.0);

    /// <summary>Момент инерции стержня относительно оси через конец</summary>
    /// <param name="mass">Масса</param>
    /// <param name="length">Длина</param>
    public static Quantity RodAboutEnd(Quantity mass, Quantity length) => Inertia(mass, length, 1.0 / 3.0);

    /// <summary>
    /// Теорема Гюйгенса — Штейнера: перенос оси на расстояние <paramref name="distance"/>
    /// </summary>
    /// <param name="centralInertia">Момент инерции относительно оси через центр масс</param>
    /// <param name="mass">Масса</param>
    /// <param name="distance">Расстояние между осями</param>
    public static Quantity ParallelAxis(Quantity centralInertia, Quantity mass, Quantity distance)
    {
        Dimension inertiaDimension = Dimension.MassDim * Dimension.Area;

        double i = centralInertia.RequireSi(inertiaDimension, nameof(centralInertia));
        double m = mass.RequireSi(Dimension.MassDim, nameof(mass));
        double d = distance.RequireSi(Dimension.LengthDim, nameof(distance));

        return new Quantity(i + (m * d * d), inertiaDimension);
    }

    /// <summary>Энергия вращения <c>E = I·ω²/2</c></summary>
    /// <param name="inertia">Момент инерции</param>
    /// <param name="angularSpeed">Угловая скорость</param>
    public static Quantity RotationalEnergy(Quantity inertia, Quantity angularSpeed)
    {
        double i = inertia.RequireSi(Dimension.MassDim * Dimension.Area, nameof(inertia));
        double omega = angularSpeed.RequireSi(Dimension.Frequency, nameof(angularSpeed));

        return new Quantity(0.5 * i * omega * omega, Dimension.Energy);
    }

    private static Quantity Inertia(Quantity mass, Quantity size, double factor)
    {
        double m = mass.RequireSi(Dimension.MassDim, nameof(mass));
        double r = size.RequireSi(Dimension.LengthDim, nameof(size));

        return new Quantity(factor * m * r * r, Dimension.MassDim * Dimension.Area);
    }
}
