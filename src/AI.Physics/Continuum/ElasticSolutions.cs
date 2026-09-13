using AI.Units;

namespace AI.Physics.Continuum;

/// <summary>Условие по оси толстостенного цилиндра</summary>
public enum AxialCondition
{
    /// <summary>Открытые торцы: осевое напряжение нулевое</summary>
    OpenEnds,

    /// <summary>Закрытые торцы: давление на днища даёт осевое напряжение</summary>
    ClosedEnds,

    /// <summary>Плоская деформация: осевое удлинение запрещено, длинная труба в грунте</summary>
    PlaneStrain
}

/// <summary>Напряжения в толстостенном цилиндре</summary>
/// <param name="Radial">Радиальное</param>
/// <param name="Hoop">Окружное</param>
/// <param name="Axial">Осевое</param>
public readonly record struct CylinderStress(Quantity Radial, Quantity Hoop, Quantity Axial);

/// <summary>
/// Толстостенные цилиндр и сфера под давлением — задачи Ламе.
/// </summary>
/// <remarks>
/// Точные решения теории упругости: радиальное и окружное напряжения — <c>A ∓ B/r²</c> у цилиндра
/// и <c>A ∓ B/r³</c> у сферы, постоянные находятся из давлений на поверхностях. Окружное напряжение
/// наибольшее на внутренней поверхности, поэтому стенку толще нескольких процентов радиуса нельзя
/// считать по формуле тонкой оболочки pr/t — она занижает максимум.
/// </remarks>
public static class ThickWalledVessels
{
    /// <summary>Напряжения в цилиндре на радиусе r</summary>
    /// <param name="innerRadius">Внутренний радиус a</param>
    /// <param name="outerRadius">Наружный радиус b</param>
    /// <param name="innerPressure">Внутреннее давление</param>
    /// <param name="outerPressure">Наружное давление</param>
    /// <param name="radius">Радиус точки, a ≤ r ≤ b</param>
    /// <param name="axial">Условие по оси</param>
    /// <param name="poissonRatio">Коэффициент Пуассона — нужен только при плоской деформации</param>
    public static CylinderStress Cylinder(
        Quantity innerRadius, Quantity outerRadius, Quantity innerPressure, Quantity outerPressure,
        Quantity radius, AxialCondition axial = AxialCondition.ClosedEnds, double poissonRatio = 0.3)
    {
        (double a, double b, double pi, double po, double r) = Read(innerRadius, outerRadius, innerPressure, outerPressure, radius);

        double a2 = a * a, b2 = b * b;
        double mean = ((pi * a2) - (po * b2)) / (b2 - a2);
        double varying = (pi - po) * a2 * b2 / ((b2 - a2) * r * r);

        double radial = mean - varying;
        double hoop = mean + varying;
        double axialStress = axial switch
        {
            AxialCondition.OpenEnds => 0,
            AxialCondition.ClosedEnds => mean,
            _ => poissonRatio * (radial + hoop)
        };

        return new CylinderStress(P(radial), P(hoop), P(axialStress));
    }

    /// <summary>Радиальное перемещение точки цилиндра</summary>
    /// <param name="innerRadius">Внутренний радиус</param>
    /// <param name="outerRadius">Наружный радиус</param>
    /// <param name="innerPressure">Внутреннее давление</param>
    /// <param name="outerPressure">Наружное давление</param>
    /// <param name="radius">Радиус точки</param>
    /// <param name="material">Материал</param>
    /// <param name="axial">Условие по оси</param>
    public static Quantity CylinderRadialDisplacement(
        Quantity innerRadius, Quantity outerRadius, Quantity innerPressure, Quantity outerPressure,
        Quantity radius, ElasticMaterial material, AxialCondition axial = AxialCondition.ClosedEnds)
    {
        ArgumentNullException.ThrowIfNull(material);

        CylinderStress s = Cylinder(innerRadius, outerRadius, innerPressure, outerPressure, radius, axial, material.PoissonRatio);
        double r = radius.SiValue;
        double nu = material.PoissonRatio;

        // Окружная деформация и есть u/r
        double hoopStrain = (s.Hoop.SiValue - (nu * (s.Radial.SiValue + s.Axial.SiValue))) / material.YoungModulus.SiValue;

        return new Quantity(r * hoopStrain, Dimension.LengthDim);
    }

    /// <summary>Радиальное и окружное напряжения в толстостенной сфере</summary>
    /// <param name="innerRadius">Внутренний радиус</param>
    /// <param name="outerRadius">Наружный радиус</param>
    /// <param name="innerPressure">Внутреннее давление</param>
    /// <param name="outerPressure">Наружное давление</param>
    /// <param name="radius">Радиус точки</param>
    public static (Quantity Radial, Quantity Hoop) Sphere(
        Quantity innerRadius, Quantity outerRadius, Quantity innerPressure, Quantity outerPressure, Quantity radius)
    {
        (double a, double b, double pi, double po, double r) = Read(innerRadius, outerRadius, innerPressure, outerPressure, radius);

        double a3 = a * a * a, b3 = b * b * b;
        double mean = ((pi * a3) - (po * b3)) / (b3 - a3);
        double varying = (pi - po) * a3 * b3 / ((b3 - a3) * r * r * r);

        return (P(mean - varying), P(mean + (varying / 2)));
    }

    private static (double, double, double, double, double) Read(
        Quantity innerRadius, Quantity outerRadius, Quantity innerPressure, Quantity outerPressure, Quantity radius)
    {
        double a = innerRadius.RequireSi(Dimension.LengthDim, nameof(innerRadius));
        double b = outerRadius.RequireSi(Dimension.LengthDim, nameof(outerRadius));
        double r = radius.RequireSi(Dimension.LengthDim, nameof(radius));
        double pi = Pressure(innerPressure, nameof(innerPressure));
        double po = Pressure(outerPressure, nameof(outerPressure));

        if (!(a > 0 && b > a))
            throw new ArgumentOutOfRangeException(nameof(outerRadius), "Нужны 0 < a < b");

        if (!(r >= a * (1 - 1e-12) && r <= b * (1 + 1e-12)))
            throw new ArgumentOutOfRangeException(nameof(radius), "Точка должна лежать в стенке: a ≤ r ≤ b");

        return (a, b, pi, po, r);
    }

    internal static double Pressure(Quantity value, string name)
        => value.Dimension.IsDimensionless && value.SiValue == 0 ? 0 : value.RequireSi(Dimension.Pressure, name);

    internal static Quantity P(double value) => new(value, Dimension.Pressure);
}

/// <summary>
/// Пластина с круглым отверстием при одноосном растяжении — задача Кирша (1898).
/// </summary>
/// <remarks>
/// Отверстие утраивает напряжение на своём краю поперёк нагрузки: <c>σ_θθ(a, 90°) = 3σ</c>, а на
/// краю вдоль нагрузки даёт сжатие −σ. Концентрация не зависит от размера отверстия — маленькая
/// дырка опасна так же, как большая, — и спадает с расстоянием как (a/r)²: в двух радиусах от края
/// превышение уже около 7 %. Решение для бесконечной пластины; при ширине меньше десяти диаметров
/// концентрация растёт.
/// </remarks>
public static class PlateWithHole
{
    /// <summary>Коэффициент концентрации напряжений у отверстия в бесконечной пластине</summary>
    public const double StressConcentrationFactor = 3.0;

    /// <summary>Напряжения в полярных координатах вокруг отверстия</summary>
    /// <param name="remoteStress">Растягивающее напряжение вдали от отверстия</param>
    /// <param name="holeRadius">Радиус отверстия a</param>
    /// <param name="radius">Расстояние от центра r ≥ a</param>
    /// <param name="angleDegrees">Угол от направления нагрузки, градусы</param>
    public static (Quantity Radial, Quantity Hoop, Quantity Shear) Stress(
        Quantity remoteStress, Quantity holeRadius, Quantity radius, double angleDegrees)
    {
        double s = remoteStress.RequireSi(Dimension.Pressure, nameof(remoteStress));
        double a = holeRadius.RequireSi(Dimension.LengthDim, nameof(holeRadius));
        double r = radius.RequireSi(Dimension.LengthDim, nameof(radius));

        if (!(a > 0) || !(r >= a * (1 - 1e-12)))
            throw new ArgumentOutOfRangeException(nameof(radius), "Точка должна лежать вне отверстия: r ≥ a > 0");

        double q = a * a / (r * r);
        double twice = 2 * angleDegrees * Math.PI / 180;

        double radial = (s / 2 * (1 - q)) + (s / 2 * (1 - (4 * q) + (3 * q * q)) * Math.Cos(twice));
        double hoop = (s / 2 * (1 + q)) - (s / 2 * (1 + (3 * q * q)) * Math.Cos(twice));
        double shear = -s / 2 * (1 + (2 * q) - (3 * q * q)) * Math.Sin(twice);

        return (ThickWalledVessels.P(radial), ThickWalledVessels.P(hoop), ThickWalledVessels.P(shear));
    }
}

/// <summary>Результат контакта Герца</summary>
/// <param name="ContactRadius">Радиус площадки контакта a</param>
/// <param name="MaxPressure">Наибольшее давление p₀ в центре площадки</param>
/// <param name="MeanPressure">Среднее давление F/(πa²) = ⅔·p₀</param>
/// <param name="Approach">Сближение тел δ = a²/R</param>
/// <param name="EffectiveModulus">Приведённый модуль E*</param>
/// <param name="EffectiveRadius">Приведённый радиус R</param>
public readonly record struct HertzContactResult(
    Quantity ContactRadius, Quantity MaxPressure, Quantity MeanPressure, Quantity Approach,
    Quantity EffectiveModulus, Quantity EffectiveRadius);

/// <summary>
/// Контакт упругих шаров по Герцу (1882).
/// </summary>
/// <remarks>
/// Площадка контакта — круг радиуса <c>a = (3FR/(4E*))^⅓</c>, давление распределено по полусфере
/// с максимумом <c>p₀ = 3F/(2πa²)</c>, сближение <c>δ = a²/R</c>. Жёсткость контакта растёт с силой
/// (<c>F ∝ δ^{3/2}</c>), поэтому шарикоподшипник не подчиняется закону Гука как целое.
/// Приведённый модуль <c>1/E* = (1 − ν₁²)/E₁ + (1 − ν₂²)/E₂</c>, радиус <c>1/R = 1/R₁ + 1/R₂</c>.
/// Трения нет, площадка мала по сравнению с радиусами, деформации упругие; наибольшее касательное
/// напряжение лежит под поверхностью, около 0,48a в глубину, — оттуда начинается усталостное выкрашивание.
/// </remarks>
public static class HertzContact
{
    /// <summary>Контакт двух шаров</summary>
    /// <param name="force">Сжимающая сила</param>
    /// <param name="firstRadius">Радиус первого шара</param>
    /// <param name="firstMaterial">Материал первого</param>
    /// <param name="secondRadius">Радиус второго шара</param>
    /// <param name="secondMaterial">Материал второго</param>
    public static HertzContactResult Spheres(
        Quantity force, Quantity firstRadius, ElasticMaterial firstMaterial, Quantity secondRadius, ElasticMaterial secondMaterial)
    {
        double r1 = firstRadius.RequireSi(Dimension.LengthDim, nameof(firstRadius));
        double r2 = secondRadius.RequireSi(Dimension.LengthDim, nameof(secondRadius));

        if (!(r1 > 0 && r2 > 0))
            throw new ArgumentOutOfRangeException(nameof(firstRadius), "Радиусы шаров должны быть положительными");

        return Solve(force, 1 / ((1 / r1) + (1 / r2)), firstMaterial, secondMaterial);
    }

    /// <summary>Шар на упругом полупространстве</summary>
    /// <param name="force">Сжимающая сила</param>
    /// <param name="radius">Радиус шара</param>
    /// <param name="sphereMaterial">Материал шара</param>
    /// <param name="planeMaterial">Материал полупространства</param>
    public static HertzContactResult SphereOnPlane(
        Quantity force, Quantity radius, ElasticMaterial sphereMaterial, ElasticMaterial planeMaterial)
    {
        double r = radius.RequireSi(Dimension.LengthDim, nameof(radius));

        if (!(r > 0))
            throw new ArgumentOutOfRangeException(nameof(radius), "Радиус шара должен быть положительным");

        return Solve(force, r, sphereMaterial, planeMaterial);
    }

    private static HertzContactResult Solve(Quantity force, double radius, ElasticMaterial first, ElasticMaterial second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        double f = force.RequireSi(Dimension.Force, nameof(force));

        if (!(f > 0))
            throw new ArgumentOutOfRangeException(nameof(force), "Сила сжатия должна быть положительной");

        double modulus = 1 / ((1 / first.PlaneStrainModulus.SiValue) + (1 / second.PlaneStrainModulus.SiValue));
        double a = Math.Cbrt(3 * f * radius / (4 * modulus));
        double p0 = 3 * f / (2 * Math.PI * a * a);

        return new HertzContactResult(
            new Quantity(a, Dimension.LengthDim),
            ThickWalledVessels.P(p0),
            ThickWalledVessels.P(2 * p0 / 3),
            new Quantity(a * a / radius, Dimension.LengthDim),
            ThickWalledVessels.P(modulus),
            new Quantity(radius, Dimension.LengthDim));
    }
}
