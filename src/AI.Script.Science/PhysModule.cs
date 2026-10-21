using AI.Physics.Fluids;
using AI.Physics.Mechanics;
using AI.Physics.Optics;
using AI.Physics.Relativity;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Std;
using AI.Units;

namespace AI.Script.Science;

/// <summary>
/// Пространство <c>phys</c>: школьная и инженерная физика одним вызовом.
/// </summary>
/// <remarks>
/// Каждая функция отвечает на типовой вопрос задачника — дальность броска, вторая космическая,
/// постоянная времени цепи — формулой из <c>AI.Physics</c>, а не пересказом формулы в скрипте.
/// <para>
/// Все числа — в основных единицах СИ: метры, секунды, килограммы, кельвины, паскали, вольты,
/// омы, фарады; углы — в градусах. Величины с единицами (<c>qty</c>) сюда не принимаются
/// намеренно: справка печатает тип параметра, и <c>num</c> в ней однозначнее, чем «число либо
/// величина нужной размерности». Перевести величину в число СИ — <c>qty.value(v, unit: "m/s")</c>.
/// </para>
/// </remarks>
[ScriptModule("phys", "Физика: броски, орбиты, газы, теплота, цепи, распад, атмосфера", Version = "0.1", Group = "отрасли")]
public static partial class PhysModule
{
    /// <summary>Стандартное ускорение свободного падения, м/с².</summary>
    private const double StandardGravity = 9.80665;

    /// <summary>Масса Земли, кг: центральное тело орбит по умолчанию.</summary>
    private const double EarthMass = 5.9722e24;

    /// <summary>Средний радиус Земли, м.</summary>
    private const double EarthRadius = 6.371e6;

    /// <summary>Плотность воздуха у земли по стандартной атмосфере, кг/м³.</summary>
    private const double AirDensity = 1.225;

    // --- механика ---

    /// <summary>
    /// Бросок под углом к горизонту в однородном поле тяжести.
    /// </summary>
    /// <remarks>
    /// Без сопротивления воздуха и с уровня земли на тот же уровень — так в учебнике. Для мяча или
    /// пули ответ завышен в разы: сопротивление — это система уравнений движения, её решает
    /// <c>ode.solve</c> с событием падения.
    /// </remarks>
    [ScriptFn("projectile", "Бросок под углом без сопротивления: дальность, высота, время полёта",
        Example = "phys.projectile(50, 45).range")]
    public static ScriptRecord Projectile(
        [ScriptParam("начальная скорость, м/с")] double speed,
        [ScriptParam("угол к горизонту, градусы")] double angle,
        [ScriptParam("ускорение свободного падения, м/с²")] double gravity = StandardGravity)
    {
        TrajectoryResult result = AI.Physics.Mechanics.Projectile.Launch(
            new Quantity(speed, Dimension.Velocity), angle, new Quantity(gravity, Dimension.Acceleration));

        return ScriptData.Record(result,
            ("range", result.Range.SiValue),
            ("max_height", result.MaxHeight.SiValue),
            ("flight_time", result.FlightTime.SiValue),
            ("impact_speed", result.ImpactSpeed.SiValue));
    }

    [ScriptFn("fall_time", "Время свободного падения с высоты без начальной скорости, с",
        Example = "phys.fall_time(20)")]
    public static double FallTime(
        [ScriptParam("высота, м")] double height,
        [ScriptParam("ускорение свободного падения, м/с²")] double gravity = StandardGravity)
        => Kinematics.FreeFallTime(new Quantity(height, Dimension.LengthDim), new Quantity(gravity, Dimension.Acceleration)).SiValue;

    [ScriptFn("stopping_distance", "Тормозной путь до остановки при постоянном замедлении, м",
        Example = "phys.stopping_distance(20, 7)")]
    public static double StoppingDistance(
        [ScriptParam("начальная скорость, м/с")] double speed,
        [ScriptParam("модуль замедления, м/с²")] double deceleration)
        => Kinematics.StoppingDistance(new Quantity(speed, Dimension.Velocity), new Quantity(deceleration, Dimension.Acceleration)).SiValue;

    /// <summary>
    /// Столкновение двух тел вдоль прямой.
    /// </summary>
    /// <remarks>
    /// Импульс сохраняется всегда, энергия — только при упругом ударе (<c>restitution: 1</c>); при
    /// нуле тела слипаются. Скорость со знаком: навстречу — отрицательная.
    /// </remarks>
    [ScriptFn("collision", "Удар двух тел вдоль прямой: скорости после удара и потеря энергии",
        Example = "phys.collision(2, 3, 1, -1, restitution: 0.5)")]
    public static ScriptRecord Collision(
        [ScriptParam("масса первого тела, кг")] double m1,
        [ScriptParam("скорость первого тела до удара, м/с")] double v1,
        [ScriptParam("масса второго тела, кг")] double m2,
        [ScriptParam("скорость второго тела до удара, м/с")] double v2,
        [ScriptParam("коэффициент восстановления: 1 — упругий удар, 0 — тела слипаются")] double restitution = 1)
    {
        CollisionResult result = Collisions.Collide(
            new Quantity(m1, Dimension.MassDim), new Quantity(v1, Dimension.Velocity),
            new Quantity(m2, Dimension.MassDim), new Quantity(v2, Dimension.Velocity),
            restitution);

        return ScriptData.Plain(
            ("v1", result.FirstSpeed.SiValue),
            ("v2", result.SecondSpeed.SiValue),
            ("energy_loss", result.EnergyLoss.SiValue));
    }

    /// <summary>
    /// Осциллятор с вязким затуханием <c>m·ẍ + c·ẋ + k·x = 0</c>.
    /// </summary>
    /// <remarks>
    /// <c>omega</c> — круговая собственная частота, рад/с; <c>frequency</c> — она же в герцах;
    /// <c>regime</c> — <c>undamped</c>, <c>underdamped</c>, <c>critical</c> либо <c>overdamped</c>.
    /// Ход колебаний во времени — <c>ode.solve</c> с правой частью <c>{ x: s.v, v: -(k*s.x + c*s.v)/m }</c>.
    /// </remarks>
    [ScriptFn("oscillator", "Осциллятор масса — пружина: частота, период, затухание, добротность",
        Example = "phys.oscillator(1, 100, damping: 2).period")]
    public static ScriptRecord Oscillator(
        [ScriptParam("масса, кг")] double mass,
        [ScriptParam("жёсткость, Н/м")] double stiffness,
        [ScriptParam("коэффициент вязкого трения, Н·с/м")] double damping = 0)
    {
        var oscillator = new HarmonicOscillator(
            new Quantity(mass, Dimension.MassDim),
            new Quantity(stiffness, Dimension.Force / Dimension.LengthDim),
            new Quantity(damping, Dimension.Force / Dimension.Velocity));

        return ScriptData.Record(oscillator,
            ("omega", oscillator.NaturalFrequency.SiValue),
            ("frequency", oscillator.NaturalFrequency.SiValue / (2 * Math.PI)),
            ("period", oscillator.Period.SiValue),
            ("damping_ratio", oscillator.DampingRatio),
            ("damped_omega", oscillator.DampedFrequency.SiValue),
            ("quality", oscillator.QualityFactor),
            ("regime", oscillator.Regime switch
            {
                DampingRegime.Undamped => "undamped",
                DampingRegime.Underdamped => "underdamped",
                DampingRegime.Critical => "critical",
                _ => "overdamped",
            }));
    }

    /// <summary>
    /// Сила квадратичного сопротивления среды <c>F = Cd·ρ·v²·S/2</c>.
    /// </summary>
    /// <remarks>
    /// Коэффициент по умолчанию 0.47 — у шара; у автомобиля около 0.3, у парашюта больше единицы.
    /// Плотность по умолчанию — воздух у земли.
    /// </remarks>
    [ScriptFn("drag", "Сила сопротивления воздуха или жидкости при движении тела, Н",
        Example = "phys.drag(30, 0.0038)")]
    public static double Drag(
        [ScriptParam("скорость, м/с")] double speed,
        [ScriptParam("площадь поперечного сечения, м²")] double area,
        [ScriptParam("коэффициент сопротивления")] double cd = 0.47,
        [ScriptParam("плотность среды, кг/м³")] double density = AirDensity)
        => FlowDynamics.DragForce(
            cd, new Quantity(density, Dimension.Density), new Quantity(speed, Dimension.Velocity), new Quantity(area, Dimension.Area)).SiValue;

    [ScriptFn("terminal_speed", "Установившаяся скорость падения в воздухе или жидкости, м/с",
        Example = "phys.terminal_speed(80, 0.7, cd: 1)")]
    public static double TerminalSpeed(
        [ScriptParam("масса тела, кг")] double mass,
        [ScriptParam("площадь поперечного сечения, м²")] double area,
        [ScriptParam("коэффициент сопротивления")] double cd = 0.47,
        [ScriptParam("плотность среды, кг/м³")] double density = AirDensity,
        [ScriptParam("ускорение свободного падения, м/с²")] double gravity = StandardGravity)
        => FlowDynamics.TerminalSpeed(
            new Quantity(mass, Dimension.MassDim), cd, new Quantity(density, Dimension.Density),
            new Quantity(area, Dimension.Area), new Quantity(gravity, Dimension.Acceleration)).SiValue;

    // --- орбиты ---

    /// <summary>
    /// Круговая орбита вокруг тела массы <c>mass</c>.
    /// </summary>
    /// <remarks>
    /// Радиус отсчитывается от центра тела, а не от поверхности: орбита МКС — это
    /// <c>6.371e6 + 4.2e5</c> м. Центральное тело по умолчанию — Земля.
    /// </remarks>
    [ScriptFn("orbit", "Круговая орбита: скорость, период и вторая космическая на радиусе",
        Example = "phys.orbit(6.371e6 + 4.2e5).period")]
    public static ScriptRecord Orbit(
        [ScriptParam("радиус орбиты от центра тела, м")] double radius,
        [ScriptParam("масса центрального тела, кг; по умолчанию Земля")] double mass = EarthMass)
    {
        Quantity mu = Mu(mass);
        var r = new Quantity(radius, Dimension.LengthDim);

        return ScriptData.Plain(
            ("speed", Orbits.CircularSpeed(mu, r).SiValue),
            ("period", Orbits.Period(mu, r).SiValue),
            ("escape_speed", Orbits.EscapeSpeed(mu, r).SiValue));
    }

    [ScriptFn("escape_speed", "Вторая космическая скорость на расстоянии от центра тела, м/с",
        Example = "phys.escape_speed()")]
    public static double EscapeSpeed(
        [ScriptParam("расстояние от центра тела, м; по умолчанию радиус Земли")] double radius = EarthRadius,
        [ScriptParam("масса тела, кг; по умолчанию Земля")] double mass = EarthMass)
        => Orbits.EscapeSpeed(Mu(mass), new Quantity(radius, Dimension.LengthDim)).SiValue;

    /// <summary>
    /// Радиус орбиты по периоду — обратный третий закон Кеплера.
    /// </summary>
    /// <remarks>
    /// Геостационарная орбита — период звёздных суток, 86164 с: радиус около 42 164 км.
    /// </remarks>
    [ScriptFn("orbit_radius", "Радиус орбиты по заданному периоду обращения, м",
        Example = "phys.orbit_radius(86164)")]
    public static double OrbitRadius(
        [ScriptParam("период обращения, с")] double period,
        [ScriptParam("масса центрального тела, кг; по умолчанию Земля")] double mass = EarthMass)
        => Orbits.RadiusForPeriod(Mu(mass), new Quantity(period, Dimension.TimeDim)).SiValue;

    /// <summary>
    /// Гомановский переход между круговыми орбитами.
    /// </summary>
    /// <remarks>
    /// <c>delta_v</c> — сумма двух импульсов, <c>transfer_time</c> — половина периода переходного
    /// эллипса с большой полуосью <c>(from + to) / 2</c>.
    /// </remarks>
    [ScriptFn("hohmann", "Переход Гомана между круговыми орбитами: суммарный Δv и время перелёта",
        Example = "phys.hohmann(6.771e6, 4.2164e7).delta_v")]
    public static ScriptRecord Hohmann(
        [ScriptParam("радиус начальной орбиты, м")] double from,
        [ScriptParam("радиус конечной орбиты, м")] double to,
        [ScriptParam("масса центрального тела, кг; по умолчанию Земля")] double mass = EarthMass)
    {
        Quantity mu = Mu(mass);

        return ScriptData.Plain(
            ("delta_v", Orbits.HohmannDeltaV(mu, new Quantity(from, Dimension.LengthDim), new Quantity(to, Dimension.LengthDim)).SiValue),
            ("transfer_time", Orbits.Period(mu, new Quantity((from + to) / 2, Dimension.LengthDim)).SiValue / 2));
    }

    // --- относительность и оптика ---

    /// <summary>
    /// Лоренц-фактор.
    /// </summary>
    /// <remarks>
    /// <c>gamma</c> — во сколько раз движущиеся часы идут медленнее и во сколько раз короче
    /// движущийся стержень. Скорость не меньше скорости света — отказ.
    /// </remarks>
    [ScriptFn("lorentz", "Специальная относительность: доля скорости света и лоренц-фактор",
        Example = "phys.lorentz(2.6e8).gamma")]
    public static ScriptRecord Lorentz([ScriptParam("скорость, м/с")] double speed)
    {
        double beta = SpecialRelativity.Beta(new Quantity(speed, Dimension.Velocity));

        return ScriptData.Plain(("beta", beta), ("gamma", SpecialRelativity.LorentzFactor(beta)));
    }

    /// <summary>
    /// Тонкая линза: где изображение и какого размера.
    /// </summary>
    /// <remarks>
    /// Фокус рассеивающей линзы отрицательный. Отрицательное <c>image_distance</c> — мнимое
    /// изображение по ту же сторону, что и предмет; <c>magnification</c> меньше нуля —
    /// изображение перевёрнуто.
    /// </remarks>
    [ScriptFn("lens", "Формула тонкой линзы: расстояние до изображения и увеличение",
        Example = "phys.lens(0.1, 0.3)")]
    public static ScriptRecord Lens(
        [ScriptParam("фокусное расстояние, м")] double focal,
        [ScriptParam("расстояние до предмета, м")] double distance)
    {
        var objectDistance = new Quantity(distance, Dimension.LengthDim);
        Quantity image = GeometricOptics.ImageDistance(new Quantity(focal, Dimension.LengthDim), objectDistance);

        return ScriptData.Plain(
            ("image_distance", image.SiValue),
            ("magnification", GeometricOptics.Magnification(objectDistance, image)),
            ("real", image.SiValue > 0));
    }

    [ScriptFn("refraction", "Угол преломления по закону Снеллиуса, градусы; nan — полное отражение",
        Example = "phys.refraction(30, 1, 1.5)")]
    public static double Refraction(
        [ScriptParam("угол падения, градусы")] double angle,
        [ScriptParam("показатель преломления первой среды")] double n1,
        [ScriptParam("показатель преломления второй среды")] double n2)
        => GeometricOptics.RefractionAngleDegrees(angle, n1, n2);

    // --- внутреннее ---

    /// <summary>Гравитационный параметр центрального тела.</summary>
    private static Quantity Mu(double mass) => Orbits.GravitationalParameter(new Quantity(mass, Dimension.MassDim));
}
