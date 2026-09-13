using AI.Statistics;
using AI.Units;

namespace AI.Physics.Fluids;

/// <summary>Ламинарный пограничный слой на пластине по Блазиусу</summary>
/// <param name="Reynolds">Местное число Рейнольдса Ux/ν</param>
/// <param name="Thickness">Толщина δ₉₉, на которой скорость достигает 99 % внешней</param>
/// <param name="DisplacementThickness">Толщина вытеснения δ*</param>
/// <param name="MomentumThickness">Толщина потери импульса θ</param>
/// <param name="LocalSkinFriction">Местный коэффициент трения c_f</param>
/// <param name="AverageSkinFriction">Средний коэффициент трения от кромки до x</param>
/// <param name="WallShearStress">Касательное напряжение на стенке</param>
/// <param name="Laminar">Число Рейнольдса ниже 5·10⁵, и слой ещё ламинарный</param>
public readonly record struct BoundaryLayer(
    double Reynolds, Quantity Thickness, Quantity DisplacementThickness, Quantity MomentumThickness,
    double LocalSkinFriction, double AverageSkinFriction, Quantity WallShearStress, bool Laminar);

/// <summary>
/// Точные решения уравнений Навье — Стокса и пограничный слой.
/// </summary>
/// <remarks>
/// <para>
/// Уравнения Навье — Стокса нелинейны, но у нескольких течений нелинейный член обращается в нуль,
/// и решение находится точно: течение Пуазейля в трубе и щели, течение Куэтта между пластинами,
/// первая задача Стокса о внезапно сдвинутой пластине. При малых числах Рейнольдса инерцией можно
/// пренебречь целиком — отсюда закон Стокса для шара. Эти решения описывают реальные течения
/// в капиллярах, подшипниках, отстойниках и служат эталоном для численных методов.
/// </para>
/// <para>
/// Все течения ламинарные: в трубе — при Re &lt; 2300, для шара по Стоксу — при Re &lt; 1.
/// </para>
/// </remarks>
public static class ViscousFlow
{
    /// <summary>Расход через круглую трубу по Хагену — Пуазейлю: πR⁴Δp/(8μL)</summary>
    /// <param name="pressureDrop">Перепад давления на длине трубы</param>
    /// <param name="radius">Внутренний радиус</param>
    /// <param name="length">Длина</param>
    /// <param name="viscosity">Динамическая вязкость</param>
    public static Quantity PoiseuilleFlowRate(Quantity pressureDrop, Quantity radius, Quantity length, Quantity viscosity)
    {
        (double dp, double r, double l, double mu) = Pipe(pressureDrop, radius, length, viscosity);

        return new Quantity(Math.PI * Math.Pow(r, 4) * dp / (8 * mu * l), Dimension.Volume / Dimension.TimeDim);
    }

    /// <summary>Скорость на расстоянии от оси: Δp(R² − r²)/(4μL) — параболический профиль</summary>
    /// <param name="pressureDrop">Перепад давления</param>
    /// <param name="radius">Радиус трубы</param>
    /// <param name="length">Длина</param>
    /// <param name="viscosity">Динамическая вязкость</param>
    /// <param name="distanceFromAxis">Расстояние от оси</param>
    public static Quantity PoiseuilleVelocity(
        Quantity pressureDrop, Quantity radius, Quantity length, Quantity viscosity, Quantity distanceFromAxis)
    {
        (double dp, double r, double l, double mu) = Pipe(pressureDrop, radius, length, viscosity);
        double y = distanceFromAxis.RequireSi(Dimension.LengthDim, nameof(distanceFromAxis));

        if (!(Math.Abs(y) <= r * (1 + 1e-12)))
            throw new ArgumentOutOfRangeException(nameof(distanceFromAxis), "Точка должна лежать в сечении трубы");

        return new Quantity(dp * ((r * r) - (y * y)) / (4 * mu * l), Dimension.Velocity);
    }

    /// <summary>Касательное напряжение на стенке трубы: ΔpR/(2L) — от вязкости не зависит</summary>
    /// <param name="pressureDrop">Перепад давления</param>
    /// <param name="radius">Радиус трубы</param>
    /// <param name="length">Длина</param>
    public static Quantity PoiseuilleWallShear(Quantity pressureDrop, Quantity radius, Quantity length)
    {
        double dp = pressureDrop.RequireSi(Dimension.Pressure, nameof(pressureDrop));
        double r = Positive(radius, Dimension.LengthDim, nameof(radius));
        double l = Positive(length, Dimension.LengthDim, nameof(length));

        return new Quantity(dp * r / (2 * l), Dimension.Pressure);
    }

    /// <summary>Расход через щель между пластинами на единицу ширины: Δp·h³/(12μL)</summary>
    /// <param name="pressureDrop">Перепад давления</param>
    /// <param name="gap">Зазор h</param>
    /// <param name="length">Длина</param>
    /// <param name="viscosity">Динамическая вязкость</param>
    /// <remarks>Расход растёт как куб зазора: вдвое более узкая щель пропускает в восемь раз меньше.</remarks>
    public static Quantity ChannelFlowRatePerWidth(Quantity pressureDrop, Quantity gap, Quantity length, Quantity viscosity)
    {
        (double dp, double h, double l, double mu) = Pipe(pressureDrop, gap, length, viscosity);

        return new Quantity(dp * h * h * h / (12 * mu * l), Dimension.Area / Dimension.TimeDim);
    }

    /// <summary>Касательное напряжение в течении Куэтта: μU/h — линейный профиль скорости</summary>
    /// <param name="viscosity">Динамическая вязкость</param>
    /// <param name="plateSpeed">Скорость подвижной пластины</param>
    /// <param name="gap">Зазор</param>
    public static Quantity CouetteShearStress(Quantity viscosity, Quantity plateSpeed, Quantity gap)
    {
        double mu = Positive(viscosity, FlowDynamics.ViscosityDimension, nameof(viscosity));
        double u = plateSpeed.RequireSi(Dimension.Velocity, nameof(plateSpeed));
        double h = Positive(gap, Dimension.LengthDim, nameof(gap));

        return new Quantity(mu * u / h, Dimension.Pressure);
    }

    /// <summary>Сила сопротивления шара по Стоксу: 6πμRv, при Re &lt; 1</summary>
    /// <param name="viscosity">Динамическая вязкость</param>
    /// <param name="radius">Радиус шара</param>
    /// <param name="speed">Скорость относительно жидкости</param>
    public static Quantity StokesDrag(Quantity viscosity, Quantity radius, Quantity speed)
    {
        double mu = Positive(viscosity, FlowDynamics.ViscosityDimension, nameof(viscosity));
        double r = Positive(radius, Dimension.LengthDim, nameof(radius));
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));

        return new Quantity(6 * Math.PI * mu * r * v, Dimension.Force);
    }

    /// <summary>
    /// Скорость оседания шарика по Стоксу: 2(ρ_ч − ρ_ж)gR²/(9μ); отрицательная — всплывает
    /// </summary>
    /// <param name="particleDensity">Плотность частицы</param>
    /// <param name="fluidDensity">Плотность жидкости</param>
    /// <param name="radius">Радиус частицы</param>
    /// <param name="viscosity">Динамическая вязкость</param>
    /// <param name="gravity">Ускорение свободного падения</param>
    /// <remarks>
    /// Верно, пока число Рейнольдса по диаметру меньше единицы — для песчинки в воде это радиус
    /// порядка десятков микрометров. Скорость растёт как квадрат радиуса: поэтому глина оседает сутками,
    /// а песок — секундами.
    /// </remarks>
    public static Quantity StokesSettlingSpeed(
        Quantity particleDensity, Quantity fluidDensity, Quantity radius, Quantity viscosity, Quantity gravity = default)
    {
        double rp = particleDensity.RequireSi(Dimension.Density, nameof(particleDensity));
        double rf = fluidDensity.RequireSi(Dimension.Density, nameof(fluidDensity));
        double r = Positive(radius, Dimension.LengthDim, nameof(radius));
        double mu = Positive(viscosity, FlowDynamics.ViscosityDimension, nameof(viscosity));
        double g = Hydrostatics.StandardGravity(gravity);

        return new Quantity(2 * (rp - rf) * g * r * r / (9 * mu), Dimension.Velocity);
    }

    /// <summary>
    /// Первая задача Стокса: скорость жидкости у внезапно сдвинутой пластины, U·erfc(y/(2√(νt)))
    /// </summary>
    /// <param name="plateSpeed">Скорость пластины</param>
    /// <param name="kinematicViscosity">Кинематическая вязкость</param>
    /// <param name="distance">Расстояние от пластины</param>
    /// <param name="time">Время после начала движения</param>
    /// <remarks>
    /// Движение проникает в жидкость диффузией завихренности на глубину порядка √(νt): в воде за секунду —
    /// около миллиметра. Функция ошибок из ядра имеет погрешность 1,5·10⁻⁷.
    /// </remarks>
    public static Quantity SuddenlyStartedPlate(Quantity plateSpeed, Quantity kinematicViscosity, Quantity distance, Quantity time)
    {
        double u = plateSpeed.RequireSi(Dimension.Velocity, nameof(plateSpeed));
        double nu = Positive(kinematicViscosity, FlowDynamics.KinematicViscosityDimension, nameof(kinematicViscosity));
        double y = distance.RequireSi(Dimension.LengthDim, nameof(distance));
        double t = Positive(time, Dimension.TimeDim, nameof(time));

        if (y < 0)
            throw new ArgumentOutOfRangeException(nameof(distance), "Расстояние от пластины не может быть отрицательным");

        return new Quantity(u * (1 - StatInference.Erf(y / (2 * Math.Sqrt(nu * t)))), Dimension.Velocity);
    }

    /// <summary>
    /// Ламинарный пограничный слой на пластине без градиента давления по Блазиусу (1908)
    /// </summary>
    /// <param name="freeStreamSpeed">Скорость набегающего потока</param>
    /// <param name="distance">Расстояние от передней кромки</param>
    /// <param name="kinematicViscosity">Кинематическая вязкость</param>
    /// <param name="density">Плотность — для касательного напряжения</param>
    /// <remarks>
    /// Толщина растёт как √x: <c>δ₉₉ = 4,91x/√Re_x</c>, <c>δ* = 1,7208x/√Re_x</c>, <c>θ = 0,664x/√Re_x</c>,
    /// <c>c_f = 0,664/√Re_x</c>. При Re_x около 5·10⁵ слой становится турбулентным, толщина и трение
    /// резко растут, и эти формулы неприменимы — это отмечено признаком <see cref="BoundaryLayer.Laminar"/>.
    /// </remarks>
    public static BoundaryLayer Blasius(Quantity freeStreamSpeed, Quantity distance, Quantity kinematicViscosity, Quantity density)
    {
        double u = Positive(freeStreamSpeed, Dimension.Velocity, nameof(freeStreamSpeed));
        double x = Positive(distance, Dimension.LengthDim, nameof(distance));
        double nu = Positive(kinematicViscosity, FlowDynamics.KinematicViscosityDimension, nameof(kinematicViscosity));
        double rho = Positive(density, Dimension.Density, nameof(density));

        double re = u * x / nu;
        double root = Math.Sqrt(re);
        double local = 0.664 / root;

        return new BoundaryLayer(
            re,
            new Quantity(4.91 * x / root, Dimension.LengthDim),
            new Quantity(1.7208 * x / root, Dimension.LengthDim),
            new Quantity(0.664 * x / root, Dimension.LengthDim),
            local,
            1.328 / root,
            new Quantity(local * 0.5 * rho * u * u, Dimension.Pressure),
            re < 5e5);
    }

    private static (double, double, double, double) Pipe(Quantity pressureDrop, Quantity size, Quantity length, Quantity viscosity)
        => (pressureDrop.RequireSi(Dimension.Pressure, nameof(pressureDrop)),
            Positive(size, Dimension.LengthDim, nameof(size)),
            Positive(length, Dimension.LengthDim, nameof(length)),
            Positive(viscosity, FlowDynamics.ViscosityDimension, nameof(viscosity)));

    private static double Positive(Quantity value, Dimension dimension, string name)
    {
        double v = value.RequireSi(dimension, name);

        return v > 0 && !double.IsInfinity(v) ? v : throw new ArgumentOutOfRangeException(name, "Величина должна быть положительной");
    }
}

/// <summary>
/// Безразмерные критерии подобия: какие силы в течении главные.
/// </summary>
/// <remarks>
/// Число Рейнольдса — в <see cref="FlowDynamics.Reynolds"/>. Два течения подобны, если совпадают все
/// существенные для них критерии: модель корабля испытывают при том же числе Фруда, самолёта — при том
/// же числе Маха, и совместить оба с Рейнольдсом на модели обычно невозможно.
/// </remarks>
public static class DimensionlessNumbers
{
    /// <summary>Число Фруда v/√(gL): инерция против тяжести, волны на поверхности</summary>
    /// <param name="speed">Скорость</param>
    /// <param name="length">Характерная длина</param>
    /// <param name="gravity">Ускорение свободного падения</param>
    public static double Froude(Quantity speed, Quantity length, Quantity gravity = default)
    {
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));
        double l = length.RequireSi(Dimension.LengthDim, nameof(length));

        return v / Math.Sqrt(Hydrostatics.StandardGravity(gravity) * l);
    }

    /// <summary>Число Маха v/a: сжимаемость существенна при M &gt; 0,3</summary>
    /// <param name="speed">Скорость</param>
    /// <param name="soundSpeed">Скорость звука</param>
    public static double Mach(Quantity speed, Quantity soundSpeed)
        => speed.RequireSi(Dimension.Velocity, nameof(speed)) / soundSpeed.RequireSi(Dimension.Velocity, nameof(soundSpeed));

    /// <summary>Число Вебера ρv²L/σ: инерция против поверхностного натяжения, дробление капель</summary>
    /// <param name="density">Плотность</param>
    /// <param name="speed">Скорость</param>
    /// <param name="length">Размер капли или струи</param>
    /// <param name="surfaceTension">Поверхностное натяжение</param>
    public static double Weber(Quantity density, Quantity speed, Quantity length, Quantity surfaceTension)
    {
        double rho = density.RequireSi(Dimension.Density, nameof(density));
        double v = speed.RequireSi(Dimension.Velocity, nameof(speed));
        double l = length.RequireSi(Dimension.LengthDim, nameof(length));
        double sigma = surfaceTension.RequireSi(Dimension.Force / Dimension.LengthDim, nameof(surfaceTension));

        return rho * v * v * l / sigma;
    }

    /// <summary>Число Прандтля μc_p/k: перенос импульса против переноса тепла</summary>
    /// <param name="viscosity">Динамическая вязкость</param>
    /// <param name="specificHeat">Удельная теплоёмкость при постоянном давлении</param>
    /// <param name="thermalConductivity">Теплопроводность</param>
    public static double Prandtl(Quantity viscosity, Quantity specificHeat, Quantity thermalConductivity)
    {
        double mu = viscosity.RequireSi(FlowDynamics.ViscosityDimension, nameof(viscosity));
        double cp = specificHeat.RequireSi(Dimension.Energy / (Dimension.MassDim * Dimension.TemperatureDim), nameof(specificHeat));
        double k = thermalConductivity.RequireSi(Dimension.Power / (Dimension.LengthDim * Dimension.TemperatureDim), nameof(thermalConductivity));

        return mu * cp / k;
    }

    /// <summary>Число Грасгофа gβΔT·L³/ν²: подъёмная сила нагретой жидкости против вязкости</summary>
    /// <param name="expansionCoefficient">Коэффициент объёмного расширения β, 1/К</param>
    /// <param name="temperatureDifference">Разность температур</param>
    /// <param name="length">Характерная длина</param>
    /// <param name="kinematicViscosity">Кинематическая вязкость</param>
    /// <param name="gravity">Ускорение свободного падения</param>
    public static double Grashof(
        double expansionCoefficient, Quantity temperatureDifference, Quantity length, Quantity kinematicViscosity, Quantity gravity = default)
    {
        double dt = temperatureDifference.RequireSi(Dimension.TemperatureDim, nameof(temperatureDifference));
        double l = length.RequireSi(Dimension.LengthDim, nameof(length));
        double nu = kinematicViscosity.RequireSi(FlowDynamics.KinematicViscosityDimension, nameof(kinematicViscosity));

        return Hydrostatics.StandardGravity(gravity) * expansionCoefficient * dt * l * l * l / (nu * nu);
    }

    /// <summary>Число Струхаля fL/v: частота срыва вихрей; за цилиндром около 0,2</summary>
    /// <param name="frequency">Частота</param>
    /// <param name="length">Характерная длина</param>
    /// <param name="speed">Скорость</param>
    public static double Strouhal(Quantity frequency, Quantity length, Quantity speed)
        => frequency.RequireSi(Dimension.Frequency, nameof(frequency)) * length.RequireSi(Dimension.LengthDim, nameof(length))
            / speed.RequireSi(Dimension.Velocity, nameof(speed));

    /// <summary>Число Кнудсена λ/L: при Kn &gt; 0,01 газ перестаёт быть сплошной средой</summary>
    /// <param name="meanFreePath">Длина свободного пробега молекул</param>
    /// <param name="length">Характерный размер</param>
    public static double Knudsen(Quantity meanFreePath, Quantity length)
        => meanFreePath.RequireSi(Dimension.LengthDim, nameof(meanFreePath)) / length.RequireSi(Dimension.LengthDim, nameof(length));
}
