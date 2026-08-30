using AI.Units;

namespace AI.Physics.Optics;

/// <summary>
/// Геометрическая оптика: преломление, отражение, линзы и зеркала.
/// </summary>
/// <remarks>
/// Приближение работает, пока размеры препятствий много больше длины волны. Как только
/// они сравнимы, наступает область <see cref="WaveOptics"/>, где лучи бессильны.
/// </remarks>
public static class GeometricOptics
{
    /// <summary>Угол преломления по закону Снеллиуса</summary>
    /// <param name="incidenceDegrees">Угол падения, градусы</param>
    /// <param name="firstIndex">Показатель преломления первой среды</param>
    /// <param name="secondIndex">Показатель преломления второй среды</param>
    /// <returns>Угол преломления в градусах; <c>NaN</c> при полном внутреннем отражении</returns>
    public static double RefractionAngleDegrees(double incidenceDegrees, double firstIndex, double secondIndex)
    {
        double sine = firstIndex * Math.Sin(incidenceDegrees * Math.PI / 180.0) / secondIndex;

        return Math.Abs(sine) > 1.0 ? double.NaN : Math.Asin(sine) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Предельный угол полного внутреннего отражения
    /// </summary>
    /// <param name="denseIndex">Показатель преломления оптически более плотной среды</param>
    /// <param name="rareIndex">Показатель преломления менее плотной среды</param>
    /// <returns>Угол в градусах; <c>NaN</c>, если полное отражение невозможно</returns>
    public static double CriticalAngleDegrees(double denseIndex, double rareIndex)
        => rareIndex >= denseIndex ? double.NaN : Math.Asin(rareIndex / denseIndex) * 180.0 / Math.PI;

    /// <summary>Угол Брюстера, при котором отражённый свет полностью поляризован</summary>
    /// <param name="firstIndex">Показатель преломления первой среды</param>
    /// <param name="secondIndex">Показатель преломления второй среды</param>
    public static double BrewsterAngleDegrees(double firstIndex, double secondIndex)
        => Math.Atan(secondIndex / firstIndex) * 180.0 / Math.PI;

    /// <summary>Скорость света в среде</summary>
    /// <param name="refractiveIndex">Показатель преломления</param>
    public static Quantity SpeedInMedium(double refractiveIndex)
        => PhysicalConstants.SpeedOfLight / refractiveIndex;

    /// <summary>
    /// Положение изображения по формуле тонкой линзы: <c>1/f = 1/d + 1/f′</c>
    /// </summary>
    /// <param name="focalLength">Фокусное расстояние; отрицательное у рассеивающей линзы</param>
    /// <param name="objectDistance">Расстояние до предмета</param>
    /// <returns>Расстояние до изображения; отрицательное означает мнимое изображение</returns>
    public static Quantity ImageDistance(Quantity focalLength, Quantity objectDistance)
    {
        double f = focalLength.RequireSi(Dimension.LengthDim, nameof(focalLength));
        double d = objectDistance.RequireSi(Dimension.LengthDim, nameof(objectDistance));

        double denominator = d - f;

        return new Quantity(Math.Abs(denominator) < 1e-15 ? double.PositiveInfinity : f * d / denominator,
            Dimension.LengthDim);
    }

    /// <summary>
    /// Линейное увеличение: отношение размера изображения к размеру предмета
    /// </summary>
    /// <param name="objectDistance">Расстояние до предмета</param>
    /// <param name="imageDistance">Расстояние до изображения</param>
    public static double Magnification(Quantity objectDistance, Quantity imageDistance)
    {
        double d = objectDistance.RequireSi(Dimension.LengthDim, nameof(objectDistance));
        double image = imageDistance.RequireSi(Dimension.LengthDim, nameof(imageDistance));

        return -image / d;
    }

    /// <summary>
    /// Формула шлифовщика линз: фокусное расстояние по радиусам кривизны
    /// </summary>
    /// <param name="refractiveIndex">Показатель преломления материала линзы</param>
    /// <param name="firstRadius">Радиус первой поверхности</param>
    /// <param name="secondRadius">Радиус второй поверхности</param>
    public static Quantity LensMakerFocalLength(double refractiveIndex, Quantity firstRadius, Quantity secondRadius)
    {
        double r1 = firstRadius.RequireSi(Dimension.LengthDim, nameof(firstRadius));
        double r2 = secondRadius.RequireSi(Dimension.LengthDim, nameof(secondRadius));

        double power = (refractiveIndex - 1) * ((1.0 / r1) - (1.0 / r2));

        return new Quantity(1.0 / power, Dimension.LengthDim);
    }

    /// <summary>Оптическая сила линзы, диоптрии</summary>
    /// <param name="focalLength">Фокусное расстояние</param>
    public static double OpticalPower(Quantity focalLength)
        => 1.0 / focalLength.RequireSi(Dimension.LengthDim, nameof(focalLength));
}

/// <summary>
/// Волновая оптика: интерференция, дифракция, разрешающая способность.
/// </summary>
public static class WaveOptics
{
    /// <summary>
    /// Расстояние между соседними полосами в опыте Юнга: <c>Δy = λL/d</c>
    /// </summary>
    /// <param name="wavelength">Длина волны</param>
    /// <param name="slitSeparation">Расстояние между щелями</param>
    /// <param name="screenDistance">Расстояние до экрана</param>
    public static Quantity FringeSpacing(Quantity wavelength, Quantity slitSeparation, Quantity screenDistance)
    {
        double lambda = wavelength.RequireSi(Dimension.LengthDim, nameof(wavelength));
        double d = slitSeparation.RequireSi(Dimension.LengthDim, nameof(slitSeparation));
        double l = screenDistance.RequireSi(Dimension.LengthDim, nameof(screenDistance));

        return new Quantity(lambda * l / d, Dimension.LengthDim);
    }

    /// <summary>
    /// Угол дифракционного минимума на одной щели: <c>sin θ = mλ/a</c>
    /// </summary>
    /// <param name="wavelength">Длина волны</param>
    /// <param name="slitWidth">Ширина щели</param>
    /// <param name="order">Порядок минимума</param>
    /// <returns>Угол в градусах; <c>NaN</c>, если минимум такого порядка не существует</returns>
    public static double SingleSlitMinimumDegrees(Quantity wavelength, Quantity slitWidth, int order = 1)
    {
        double lambda = wavelength.RequireSi(Dimension.LengthDim, nameof(wavelength));
        double a = slitWidth.RequireSi(Dimension.LengthDim, nameof(slitWidth));

        double sine = order * lambda / a;

        return Math.Abs(sine) > 1 ? double.NaN : Math.Asin(sine) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Угол главного максимума дифракционной решётки: <c>d·sin θ = mλ</c>
    /// </summary>
    /// <param name="wavelength">Длина волны</param>
    /// <param name="gratingPeriod">Период решётки</param>
    /// <param name="order">Порядок максимума</param>
    /// <returns>Угол в градусах; <c>NaN</c>, если максимум такого порядка не наблюдается</returns>
    public static double GratingMaximumDegrees(Quantity wavelength, Quantity gratingPeriod, int order = 1)
    {
        double lambda = wavelength.RequireSi(Dimension.LengthDim, nameof(wavelength));
        double d = gratingPeriod.RequireSi(Dimension.LengthDim, nameof(gratingPeriod));

        double sine = order * lambda / d;

        return Math.Abs(sine) > 1 ? double.NaN : Math.Asin(sine) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Предельное угловое разрешение по критерию Рэлея: <c>θ = 1.22·λ/D</c>
    /// </summary>
    /// <param name="wavelength">Длина волны</param>
    /// <param name="apertureDiameter">Диаметр входного отверстия</param>
    /// <returns>Угол в радианах</returns>
    public static double RayleighResolution(Quantity wavelength, Quantity apertureDiameter)
    {
        double lambda = wavelength.RequireSi(Dimension.LengthDim, nameof(wavelength));
        double diameter = apertureDiameter.RequireSi(Dimension.LengthDim, nameof(apertureDiameter));

        return 1.22 * lambda / diameter;
    }

    /// <summary>
    /// Толщина плёнки, дающая усиление отражённого света при нормальном падении
    /// </summary>
    /// <param name="wavelength">Длина волны в вакууме</param>
    /// <param name="refractiveIndex">Показатель преломления плёнки</param>
    /// <param name="order">Порядок</param>
    /// <remarks>
    /// Учтена потеря полуволны при отражении от оптически более плотной среды: именно она
    /// делает условие максимума «нечётное число четвертей волны», а не «целое число полуволн».
    /// </remarks>
    public static Quantity ConstructiveFilmThickness(Quantity wavelength, double refractiveIndex, int order = 0)
    {
        double lambda = wavelength.RequireSi(Dimension.LengthDim, nameof(wavelength));

        return new Quantity(((2 * order) + 1) * lambda / (4 * refractiveIndex), Dimension.LengthDim);
    }

    /// <summary>Энергия фотона: <c>E = hc/λ</c></summary>
    /// <param name="wavelength">Длина волны</param>
    public static Quantity PhotonEnergy(Quantity wavelength)
    {
        double lambda = wavelength.RequireSi(Dimension.LengthDim, nameof(wavelength));
        double energy = PhysicalConstants.PlanckConstant.SiValue * PhysicalConstants.SpeedOfLight.SiValue / lambda;

        return new Quantity(energy, Dimension.Energy);
    }
}
