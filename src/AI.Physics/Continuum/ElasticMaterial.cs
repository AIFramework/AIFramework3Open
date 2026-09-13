using AI.Units;

namespace AI.Physics.Continuum;

/// <summary>Скорости упругих волн в изотропном теле</summary>
/// <param name="Longitudinal">Продольная (P) волна в безграничной среде: √(M/ρ)</param>
/// <param name="Shear">Поперечная (S) волна: √(G/ρ)</param>
/// <param name="Bar">Продольная волна в тонком стержне: √(E/ρ)</param>
/// <param name="Rayleigh">Поверхностная волна Рэлея</param>
public readonly record struct ElasticWaveSpeeds(Quantity Longitudinal, Quantity Shear, Quantity Bar, Quantity Rayleigh);

/// <summary>Как стеснено тепловое расширение</summary>
public enum ThermalConstraint
{
    /// <summary>Стержень между неподвижными стенками: стеснено одно направление</summary>
    Uniaxial,

    /// <summary>Плёнка на толстой подложке: стеснены два направления в плоскости</summary>
    Biaxial,

    /// <summary>Включение в жёсткой обойме: стеснены все три</summary>
    Triaxial
}

/// <summary>
/// Изотропный линейно-упругий материал: упругие постоянные и закон Гука.
/// </summary>
/// <remarks>
/// <para>
/// У изотропного тела две независимые постоянные; любые две из модуля Юнга E, коэффициента
/// Пуассона ν, модуля сдвига G, модуля объёмного сжатия K и постоянных Ламе λ, μ определяют
/// остальные. Здесь хранятся E и ν, остальное пересчитывается. Устойчивость требует
/// E &gt; 0 и −1 &lt; ν &lt; ½: при ν = ½ материал несжимаем, K и λ бесконечны, и закон Гука
/// в перемещениях теряет смысл — резину и жидкости так не описывают.
/// </para>
/// <para>
/// Закон Гука: <c>σ = λ·tr(ε)·I + 2μ·ε</c> и обратно <c>ε = ((1 + ν)·σ − ν·tr(σ)·I)/E</c>.
/// Деформации малые, материал однороден, нагружение ниже предела упругости.
/// </para>
/// </remarks>
public sealed class ElasticMaterial
{
    private readonly double _young;

    private ElasticMaterial(double young, double poisson)
    {
        if (!(young > 0) || double.IsInfinity(young))
            throw new ArgumentOutOfRangeException(nameof(young), "Модуль Юнга должен быть положительным и конечным");

        if (!(poisson > -1 && poisson < 0.5))
            throw new ArgumentOutOfRangeException(nameof(poisson),
                "Коэффициент Пуассона изотропного тела лежит строго между −1 и ½: при ½ материал несжимаем");

        _young = young;
        PoissonRatio = poisson;
    }

    /// <summary>Материал по модулю Юнга и коэффициенту Пуассона</summary>
    /// <param name="youngModulus">Модуль Юнга</param>
    /// <param name="poissonRatio">Коэффициент Пуассона</param>
    public static ElasticMaterial FromYoungAndPoisson(Quantity youngModulus, double poissonRatio)
        => new(youngModulus.RequireSi(Dimension.Pressure, nameof(youngModulus)), poissonRatio);

    /// <summary>Материал по модулям объёмного сжатия и сдвига</summary>
    /// <param name="bulkModulus">Модуль объёмного сжатия K</param>
    /// <param name="shearModulus">Модуль сдвига G</param>
    public static ElasticMaterial FromBulkAndShear(Quantity bulkModulus, Quantity shearModulus)
    {
        double k = bulkModulus.RequireSi(Dimension.Pressure, nameof(bulkModulus));
        double g = shearModulus.RequireSi(Dimension.Pressure, nameof(shearModulus));

        if (!(k > 0 && g > 0))
            throw new ArgumentOutOfRangeException(nameof(bulkModulus), "Модули K и G должны быть положительными");

        return new ElasticMaterial(9 * k * g / ((3 * k) + g), ((3 * k) - (2 * g)) / (2 * ((3 * k) + g)));
    }

    /// <summary>Материал по постоянным Ламе</summary>
    /// <param name="lambda">Первая постоянная λ</param>
    /// <param name="shearModulus">Вторая постоянная μ — модуль сдвига</param>
    public static ElasticMaterial FromLame(Quantity lambda, Quantity shearModulus)
    {
        double l = lambda.RequireSi(Dimension.Pressure, nameof(lambda));
        double mu = shearModulus.RequireSi(Dimension.Pressure, nameof(shearModulus));

        if (!(mu > 0) || !(l + mu > 0))
            throw new ArgumentOutOfRangeException(nameof(lambda), "Нужны μ > 0 и λ + μ > 0");

        return new ElasticMaterial(mu * ((3 * l) + (2 * mu)) / (l + mu), l / (2 * (l + mu)));
    }

    /// <summary>
    /// Материал по скоростям продольной и поперечной волн и плотности — так упругие свойства
    /// горных пород определяют сейсморазведкой и ультразвуком
    /// </summary>
    /// <param name="longitudinalSpeed">Скорость P-волны</param>
    /// <param name="shearSpeed">Скорость S-волны</param>
    /// <param name="density">Плотность</param>
    public static ElasticMaterial FromWaveSpeeds(Quantity longitudinalSpeed, Quantity shearSpeed, Quantity density)
    {
        double cp = longitudinalSpeed.RequireSi(Dimension.Velocity, nameof(longitudinalSpeed));
        double cs = shearSpeed.RequireSi(Dimension.Velocity, nameof(shearSpeed));
        double rho = density.RequireSi(Dimension.Density, nameof(density));

        if (!(cp > cs * Math.Sqrt(4.0 / 3.0)) || !(cs > 0) || !(rho > 0))
            throw new ArgumentOutOfRangeException(nameof(longitudinalSpeed),
                "Для устойчивого тела cp > cs·√(4/3): иначе модуль объёмного сжатия отрицателен");

        double mu = rho * cs * cs;
        double lambda = (rho * cp * cp) - (2 * mu);

        return FromLame(new Quantity(lambda, Dimension.Pressure), new Quantity(mu, Dimension.Pressure));
    }

    /// <summary>Модуль Юнга E</summary>
    public Quantity YoungModulus => new(_young, Dimension.Pressure);

    /// <summary>Коэффициент Пуассона ν</summary>
    public double PoissonRatio { get; }

    /// <summary>Модуль сдвига G = E/(2(1 + ν)) — вторая постоянная Ламе μ</summary>
    public Quantity ShearModulus => new(Mu, Dimension.Pressure);

    /// <summary>Модуль объёмного сжатия K = E/(3(1 − 2ν))</summary>
    public Quantity BulkModulus => new(_young / (3 * (1 - (2 * PoissonRatio))), Dimension.Pressure);

    /// <summary>Первая постоянная Ламе λ = Eν/((1 + ν)(1 − 2ν))</summary>
    public Quantity LameLambda => new(Lambda, Dimension.Pressure);

    /// <summary>Модуль одноосной деформации M = λ + 2μ: жёсткость при запрете поперечного расширения</summary>
    public Quantity PWaveModulus => new(Lambda + (2 * Mu), Dimension.Pressure);

    /// <summary>
    /// Модуль при плоской деформации E/(1 − ν²): входит в механику трещин и контакт Герца
    /// </summary>
    public Quantity PlaneStrainModulus => new(_young / (1 - (PoissonRatio * PoissonRatio)), Dimension.Pressure);

    /// <summary>Напряжения по деформациям</summary>
    /// <param name="strain">Тензор малых деформаций</param>
    public SymmetricTensor StressFromStrain(SymmetricTensor strain)
    {
        ArgumentNullException.ThrowIfNull(strain);
        RequireDimension(strain, Dimension.None, nameof(strain));

        double trace = strain.Trace.SiValue;

        return SymmetricTensor.Linear(strain, 2 * Mu, Lambda * trace, Dimension.Pressure);
    }

    /// <summary>Деформации по напряжениям</summary>
    /// <param name="stress">Тензор напряжений</param>
    public SymmetricTensor StrainFromStress(SymmetricTensor stress)
    {
        ArgumentNullException.ThrowIfNull(stress);
        RequireDimension(stress, Dimension.Pressure, nameof(stress));

        double trace = stress.Trace.SiValue;

        return SymmetricTensor.Linear(stress, (1 + PoissonRatio) / _young, -PoissonRatio * trace / _young, Dimension.None);
    }

    /// <summary>Удельная энергия деформации ½·σ:ε, Дж/м³</summary>
    /// <param name="strain">Тензор малых деформаций</param>
    public Quantity StrainEnergyDensity(SymmetricTensor strain)
        => StressFromStrain(strain).DoubleContraction(strain) * 0.5;

    /// <summary>Свободная тепловая деформация α·ΔT·I</summary>
    /// <param name="expansionCoefficient">Коэффициент линейного расширения, 1/К</param>
    /// <param name="temperatureChange">Изменение температуры</param>
    public static SymmetricTensor ThermalStrain(double expansionCoefficient, Quantity temperatureChange)
        => SymmetricTensor.Isotropic(
            expansionCoefficient * temperatureChange.RequireSi(Dimension.TemperatureDim, nameof(temperatureChange)),
            Dimension.None);

    /// <summary>
    /// Напряжение при стеснённом тепловом расширении: −EαΔT для стержня, −EαΔT/(1 − ν) для плёнки,
    /// −EαΔT/(1 − 2ν) для включения в жёсткой обойме
    /// </summary>
    /// <param name="expansionCoefficient">Коэффициент линейного расширения, 1/К</param>
    /// <param name="temperatureChange">Изменение температуры; нагрев положителен</param>
    /// <param name="constraint">Какие направления стеснены</param>
    public Quantity ConstrainedThermalStress(double expansionCoefficient, Quantity temperatureChange, ThermalConstraint constraint)
    {
        double free = expansionCoefficient * temperatureChange.RequireSi(Dimension.TemperatureDim, nameof(temperatureChange));

        double factor = constraint switch
        {
            ThermalConstraint.Uniaxial => 1,
            ThermalConstraint.Biaxial => 1 / (1 - PoissonRatio),
            _ => 1 / (1 - (2 * PoissonRatio))
        };

        return new Quantity(-_young * free * factor, Dimension.Pressure);
    }

    /// <summary>Скорости упругих волн</summary>
    /// <param name="density">Плотность материала</param>
    /// <remarks>
    /// Скорость волны Рэлея — корень уравнения Рэлея <c>η³ − 8η² + (24 − 16/κ²)η − 16(1 − 1/κ²) = 0</c>,
    /// где η = (c_R/c_S)², κ = c_P/c_S; на интервале (0, 1) корень единствен и ищется делением пополам.
    /// При ν = ¼ он равен 2 − 2/√3, и c_R = 0,9194·c_S.
    /// </remarks>
    public ElasticWaveSpeeds WaveSpeeds(Quantity density)
    {
        double rho = density.RequireSi(Dimension.Density, nameof(density));

        if (!(rho > 0))
            throw new ArgumentOutOfRangeException(nameof(density), "Плотность должна быть положительной");

        double longitudinal = Math.Sqrt((Lambda + (2 * Mu)) / rho);
        double shear = Math.Sqrt(Mu / rho);
        double inverseKappa = shear * shear / (longitudinal * longitudinal);

        double Rayleigh(double eta) => (eta * eta * eta) - (8 * eta * eta) + ((24 - (16 * inverseKappa)) * eta) - (16 * (1 - inverseKappa));

        double low = 0, high = 1;

        for (int i = 0; i < 200; i++)
        {
            double middle = (low + high) / 2;

            if (Rayleigh(middle) < 0)
                low = middle;
            else
                high = middle;
        }

        return new ElasticWaveSpeeds(
            new Quantity(longitudinal, Dimension.Velocity),
            new Quantity(shear, Dimension.Velocity),
            new Quantity(Math.Sqrt(_young / rho), Dimension.Velocity),
            new Quantity(shear * Math.Sqrt((low + high) / 2), Dimension.Velocity));
    }

    /// <summary>Модуль Юнга и коэффициент Пуассона</summary>
    public override string ToString() => $"E = {_young / 1e9:G4} ГПа, ν = {PoissonRatio:G4}";

    private double Mu => _young / (2 * (1 + PoissonRatio));

    private double Lambda => _young * PoissonRatio / ((1 + PoissonRatio) * (1 - (2 * PoissonRatio)));

    private static void RequireDimension(SymmetricTensor tensor, Dimension expected, string name)
    {
        if (tensor.Dimension != expected)
            throw new DimensionMismatchException(expected, tensor.Dimension, name);
    }
}
