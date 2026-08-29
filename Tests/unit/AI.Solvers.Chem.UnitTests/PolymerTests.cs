using AI.Solvers.Chem.Metrology;
using AI.Solvers.Chem.Polymers;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Полимеры: молекулярно-массовое распределение, кинетика полимеризации, свойства.</summary>
public class PolymerTests
{
    /// <summary>У монодисперсного образца все средние совпадают, полидисперсность равна единице.</summary>
    [Fact]
    public void Distribution_MonodisperseSampleHasUnitDispersity()
    {
        var distribution = new MolarMassDistribution(new[] { 100000.0 }, new[] { 1.0 });

        Assert.Equal(100000, distribution.NumberAverage, 6);
        Assert.Equal(100000, distribution.WeightAverage, 6);
        Assert.Equal(100000, distribution.ZAverage, 6);
        Assert.Equal(1.0, distribution.Dispersity, 9);
    }

    /// <summary>Средние смеси двух фракций считаются по определению моментов.</summary>
    [Fact]
    public void Distribution_ComputesMomentsOfBinaryBlend()
    {
        var distribution = new MolarMassDistribution(new[] { 10000.0, 100000.0 }, new[] { 1.0, 3.0 });

        Assert.Equal(30769.23, distribution.NumberAverage, 2);
        Assert.Equal(77500.0, distribution.WeightAverage, 6);
        Assert.Equal(97096.77, distribution.ZAverage, 2);
        Assert.Equal(2.51875, distribution.Dispersity, 5);
        Assert.Equal(100000.0, distribution.PeakMass, 6);
    }

    /// <summary>
    /// Вязкостное среднее при a = 1 совпадает со средневесовым, при a = -1 - со среднечисленным.
    /// </summary>
    [Fact]
    public void Distribution_ViscosityAverageMatchesLimitingCases()
    {
        var distribution = new MolarMassDistribution(new[] { 10000.0, 100000.0 }, new[] { 1.0, 1.0 });

        Assert.Equal(distribution.WeightAverage, distribution.ViscosityAverage(1.0), 6);
        Assert.Equal(distribution.NumberAverage, distribution.ViscosityAverage(-1.0), 6);
    }

    /// <summary>Распределение Флори при p = 0.99 даёт Mn = M0/(1-p) и полидисперсность 1+p.</summary>
    [Fact]
    public void Distribution_FloryMostProbableDistribution()
    {
        MolarMassDistribution distribution = MolarMassDistribution.Flory(100, 0.99, 5000);

        Assert.Equal(10000, distribution.NumberAverage, 0);
        Assert.Equal(19900, distribution.WeightAverage, 0);
        Assert.Equal(1.99, distribution.Dispersity, 2);
    }

    /// <summary>Градуировка колонки строится по логарифму массы стандартов.</summary>
    [Fact]
    public void Distribution_CalibratesColumnByStandards()
    {
        LinearFit calibration = MolarMassDistribution.Calibrate(
            new[] { 10.0, 12.0, 14.0 },
            new[] { 1e6, 1e5, 1e4 });

        Assert.Equal(-0.5, calibration.Slope, 9);
        Assert.Equal(11.0, calibration.Intercept, 9);
        Assert.Equal(1.0, calibration.R2, 9);
    }

    /// <summary>Хроматограмма и градуировка дают распределение с полидисперсностью больше единицы.</summary>
    [Fact]
    public void Distribution_BuildsFromChromatogram()
    {
        LinearFit calibration = MolarMassDistribution.Calibrate(
            new[] { 10.0, 12.0, 14.0 },
            new[] { 1e6, 1e5, 1e4 });

        var volumes = new double[41];
        var signal = new double[41];

        for (int i = 0; i < volumes.Length; i++)
        {
            volumes[i] = 10 + (i * 0.1);

            // Симметричный по объёму пик: гауссиана с центром 12 мл
            double delta = volumes[i] - 12;
            signal[i] = Math.Exp(-delta * delta / 0.5);
        }

        MolarMassDistribution distribution = MolarMassDistribution.FromChromatogram(
            volumes, signal, calibration, 1e-4);

        Assert.Equal(1e5, distribution.PeakMass, 3);
        Assert.True(distribution.Dispersity > 1, "распределение по объёму обязано давать полидисперсность выше единицы");
        Assert.True(distribution.NumberAverage < distribution.WeightAverage);
        Assert.True(distribution.WeightAverage < distribution.ZAverage);
    }

    /// <summary>Пустое распределение отвергается.</summary>
    [Fact]
    public void Distribution_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => new MolarMassDistribution(new[] { 0.0 }, new[] { 1.0 }));
        Assert.Throws<ArgumentException>(() => new MolarMassDistribution(new[] { 1.0, 2.0 }, new[] { 1.0 }));
    }

    /// <summary>Уравнение Карозерса: при 99 процентах завершённости степень равна ста.</summary>
    [Fact]
    public void Kinetics_CarothersDegreeAtFullStoichiometry()
    {
        Assert.Equal(100, PolymerKinetics.CarothersDegree(0.99), 9);
        Assert.Equal(0.99, PolymerKinetics.ConversionForDegree(100), 9);
    }

    /// <summary>Избыток одного мономера ограничивает степень полимеризации даже при полной конверсии.</summary>
    [Fact]
    public void Kinetics_StoichiometricImbalanceLimitsDegree()
    {
        double degree = PolymerKinetics.CarothersDegree(0.99999999, 0.99);

        Assert.Equal(199, degree, 2);
        Assert.True(degree < PolymerKinetics.CarothersDegree(0.99999999));
    }

    /// <summary>Гель-точка по средней функциональности смеси.</summary>
    [Fact]
    public void Kinetics_GelPointFromAverageFunctionality()
    {
        double functionality = PolymerKinetics.AverageFunctionality(new[] { 2.0, 1.0 }, new[] { 2.0, 3.0 });

        Assert.Equal(7.0 / 3, functionality, 9);
        Assert.Equal(6.0 / 7, PolymerKinetics.GelPoint(functionality), 9);
        Assert.Throws<ArgumentException>(() => PolymerKinetics.GelPoint(2.0));
    }

    /// <summary>Уравнение Майо: передатчик цепи снижает степень полимеризации.</summary>
    [Fact]
    public void Kinetics_ChainTransferReducesDegree()
    {
        double degree = PolymerKinetics.MayoDegree(1000, 0.01, 0.01, 1.0);

        Assert.Equal(909.09, degree, 2);
        Assert.True(degree < 1000);
    }

    /// <summary>Константа передачи цепи восстанавливается как наклон зависимости 1/Xn.</summary>
    [Fact]
    public void Kinetics_RecoversTransferConstant()
    {
        var ratios = new[] { 0.0, 0.005, 0.01, 0.02, 0.05 };
        var degrees = ratios.Select(r => 1.0 / ((1.0 / 1000) + (0.01 * r))).ToArray();

        LinearFit fit = PolymerKinetics.TransferConstant(ratios, degrees);

        Assert.Equal(0.01, fit.Slope, 9);
        Assert.Equal(0.001, fit.Intercept, 9);
    }

    /// <summary>При r1 = r2 = 1 состав сополимера повторяет состав смеси.</summary>
    [Fact]
    public void Kinetics_IdealCopolymerizationKeepsComposition()
    {
        foreach (double fraction in new[] { 0.1, 0.3, 0.5, 0.9 })
            Assert.Equal(fraction, PolymerKinetics.CopolymerComposition(fraction, 1, 1), 9);
    }

    /// <summary>Азеотроп существует не при всех константах сополимеризации.</summary>
    [Fact]
    public void Kinetics_AzeotropicCompositionExistsOnlyForSomeRatios()
    {
        Assert.Equal(0.5, PolymerKinetics.AzeotropicComposition(0.5, 0.5) ?? double.NaN, 9);
        Assert.Null(PolymerKinetics.AzeotropicComposition(1, 1));
        Assert.Null(PolymerKinetics.AzeotropicComposition(2, 0.5));
    }

    /// <summary>Линеаризации Файнмана-Росса и Келена-Тюдоша возвращают заложенные константы.</summary>
    [Fact]
    public void Kinetics_RecoversReactivityRatios()
    {
        const double r1 = 0.5;
        const double r2 = 0.3;

        var monomer = new[] { 0.2, 0.35, 0.5, 0.65, 0.8 };
        var copolymer = monomer.Select(f => PolymerKinetics.CopolymerComposition(f, r1, r2)).ToArray();

        ReactivityRatios fineman = PolymerKinetics.FinemanRoss(monomer, copolymer);
        ReactivityRatios kelen = PolymerKinetics.KelenTudos(monomer, copolymer);

        Assert.Equal(r1, fineman.R1, 6);
        Assert.Equal(r2, fineman.R2, 6);
        Assert.Equal(r1, kelen.R1, 6);
        Assert.Equal(r2, kelen.R2, 6);
        Assert.Equal(0.15, fineman.Product, 6);
        Assert.Contains("чередованию", fineman.Behaviour);
    }

    /// <summary>Для расчёта констант нужно не менее трёх опытов с корректными долями.</summary>
    [Fact]
    public void Kinetics_RejectsInsufficientCopolymerData()
    {
        Assert.Throws<ArgumentException>(() =>
            PolymerKinetics.FinemanRoss(new[] { 0.3, 0.5 }, new[] { 0.4, 0.6 }));

        Assert.Throws<ArgumentException>(() =>
            PolymerKinetics.FinemanRoss(new[] { 0.0, 0.5, 0.8 }, new[] { 0.4, 0.6, 0.7 }));
    }

    /// <summary>Уравнение Фокса для смеси двух полимеров.</summary>
    [Fact]
    public void Properties_FoxEquationForBlend()
    {
        double tg = PolymerProperties.FoxGlassTransition(new[] { 0.5, 0.5 }, new[] { 373.0, 273.0 });

        Assert.Equal(315.26, tg, 2);
        Assert.True(tg is > 273 and < 373);
    }

    /// <summary>При k = 1 уравнение Гордона-Тейлора вырождается в среднее по массе.</summary>
    [Fact]
    public void Properties_GordonTaylorReducesToWeightedMean()
    {
        Assert.Equal(323, PolymerProperties.GordonTaylorGlassTransition(0.5, 373, 273, 1.0), 9);
        Assert.Equal(373, PolymerProperties.GordonTaylorGlassTransition(1.0, 373, 273, 0.5), 9);
    }

    /// <summary>Уравнение Марка-Хаувинка обратимо по молярной массе.</summary>
    [Fact]
    public void Properties_MarkHouwinkRoundTrip()
    {
        double viscosity = PolymerProperties.IntrinsicViscosity(100000, 5e-4, 0.7);

        // Множитель 100000^0.7 равен 10^3.5, то есть 3162.3
        Assert.Equal(1.58114, viscosity, 5);
        Assert.Equal(100000, PolymerProperties.ViscosityAverageMass(viscosity, 5e-4, 0.7), 3);
    }

    /// <summary>Экстраполяция Хаггинса даёт характеристическую вязкость и константу.</summary>
    [Fact]
    public void Properties_HugginsExtrapolation()
    {
        const double intrinsic = 1.5;
        const double huggins = 0.35;

        var concentrations = new[] { 0.1, 0.2, 0.3, 0.4, 0.5 };
        var reduced = concentrations
            .Select(c => intrinsic + (huggins * intrinsic * intrinsic * c))
            .ToArray();

        var (foundIntrinsic, foundHuggins, r2) = PolymerProperties.HugginsExtrapolation(concentrations, reduced);

        Assert.Equal(intrinsic, foundIntrinsic, 9);
        Assert.Equal(huggins, foundHuggins, 9);
        Assert.Equal(1.0, r2, 9);
    }

    /// <summary>Степень кристалличности по теплоте плавления с поправкой на долю полимера.</summary>
    [Fact]
    public void Properties_CrystallinityFromMeltingEnthalpy()
    {
        Assert.Equal(31.74, PolymerProperties.Crystallinity(93, 293), 2);
        Assert.Equal(63.48, PolymerProperties.Crystallinity(93, 293, 0.5), 2);
        Assert.Throws<ArgumentException>(() => PolymerProperties.Crystallinity(93, 0));
    }

    /// <summary>Молярная масса между узлами сетки обратно пропорциональна модулю.</summary>
    [Fact]
    public void Properties_MassBetweenCrosslinks()
    {
        double mass = PolymerProperties.MassBetweenCrosslinks(1e6, 1000, 300);

        Assert.Equal(7483.02, mass, 2);
        Assert.Equal(mass / 2, PolymerProperties.MassBetweenCrosslinks(2e6, 1000, 300), 6);
    }
}
