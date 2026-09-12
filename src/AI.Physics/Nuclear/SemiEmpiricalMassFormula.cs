using AI.Units;

namespace AI.Physics.Nuclear;

/// <summary>
/// Полуэмпирическая формула масс Вайцзеккера: ядро как капля заряженной жидкости.
/// </summary>
/// <remarks>
/// <para>
/// Пять слагаемых — объём, поверхность, кулоновское отталкивание, асимметрия и спаривание —
/// описывают энергию связи средних и тяжёлых ядер с точностью около процента. Этого хватает,
/// чтобы объяснить, почему связь на нуклон наибольшая у железа и никеля и почему у тяжёлых ядер
/// нейтронов больше, чем протонов.
/// </para>
/// <para>
/// Чего формула не знает — оболочек: у ядер с магическими числами протонов или нейтронов
/// (2, 8, 20, 28, 50, 82, 126) связь сильнее предсказанной. Для лёгких ядер модель капли
/// не годится: у них почти все нуклоны на поверхности.
/// </para>
/// </remarks>
public sealed class SemiEmpiricalMassFormula
{
    /// <summary>Формула с коэффициентами Рольфа</summary>
    public SemiEmpiricalMassFormula()
        : this(MassFormulaCoefficients.Rohlf)
    {
    }

    /// <summary>Формула с заданными коэффициентами</summary>
    /// <param name="coefficients">Коэффициенты, МэВ</param>
    public SemiEmpiricalMassFormula(MassFormulaCoefficients coefficients)
    {
        if (!(coefficients.VolumeMeV > 0 && coefficients.SurfaceMeV > 0 && coefficients.CoulombMeV > 0
            && coefficients.AsymmetryMeV > 0 && coefficients.PairingMeV >= 0))
            throw new ArgumentOutOfRangeException(nameof(coefficients), "Коэффициенты формулы Вайцзеккера положительны");

        Coefficients = coefficients;
    }

    /// <summary>Коэффициенты</summary>
    public MassFormulaCoefficients Coefficients { get; }

    /// <summary>Слагаемые энергии связи</summary>
    /// <param name="nuclide">Нуклид</param>
    public MassFormulaTerms Terms(Nuclide nuclide)
    {
        RequireNuclide(nuclide);

        double a = nuclide.MassNumber;
        double z = nuclide.ProtonNumber;
        double cube = Math.Cbrt(a);
        double excess = a - (2 * z);
        double pairing = Coefficients.PairingMeV / Math.Sqrt(a);

        return new MassFormulaTerms(
            MeV(Coefficients.VolumeMeV * a),
            MeV(Coefficients.SurfaceMeV * cube * cube),
            MeV(Coefficients.CoulombMeV * z * (z - 1) / cube),
            MeV(Coefficients.AsymmetryMeV * excess * excess / a),
            MeV(nuclide.IsEvenEven ? pairing : nuclide.IsOddOdd ? -pairing : 0));
    }

    /// <summary>Энергия связи по формуле</summary>
    /// <param name="nuclide">Нуклид</param>
    public Quantity BindingEnergy(Nuclide nuclide) => Terms(nuclide).Total;

    /// <summary>
    /// Масса атома по формуле: протоны вместе с электронами — как атомы водорода, плюс нейтроны,
    /// минус энергия связи
    /// </summary>
    /// <param name="nuclide">Нуклид</param>
    public Quantity AtomicMass(Nuclide nuclide)
    {
        double c = PhysicalConstants.SpeedOfLight.SiValue;
        double mass = (nuclide.ProtonNumber * NuclearBinding.HydrogenAtomMass.SiValue)
            + (nuclide.NeutronNumber * PhysicalConstants.NeutronMass.SiValue)
            - (BindingEnergy(nuclide).SiValue / (c * c));

        return new Quantity(mass, Dimension.MassDim);
    }

    /// <summary>
    /// Число протонов у самого лёгкого, то есть устойчивого к бета-распаду, ядра среди изобар
    /// с данным массовым числом
    /// </summary>
    /// <remarks>
    /// Изобары связаны бета-распадами, и распад идёт, пока масса атома убывает. Минимум лежит там,
    /// где кулоновское отталкивание уравновешено асимметрией; у тяжёлых ядер он смещён к избытку
    /// нейтронов. Спаривание делает картину ступенчатой: у чётных A устойчивыми бывают только
    /// чётно-чётные ядра.
    /// </remarks>
    /// <param name="massNumber">Массовое число</param>
    public int MostStableProtonNumber(int massNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(massNumber);

        int best = 1;
        double lightest = double.PositiveInfinity;

        for (int z = 1; z <= massNumber; z++)
        {
            double mass = AtomicMass(new Nuclide(z, massNumber)).SiValue;

            if (mass < lightest)
            {
                lightest = mass;
                best = z;
            }
        }

        return best;
    }

    private static void RequireNuclide(Nuclide nuclide)
    {
        if (nuclide.MassNumber < 1)
            throw new ArgumentException("Нуклид не задан: массовое число должно быть положительным", nameof(nuclide));
    }

    private static Quantity MeV(double value) => Quantity.Of(value * 1e6, Si.ElectronVolt);
}
