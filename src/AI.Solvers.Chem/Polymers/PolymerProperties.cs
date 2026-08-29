namespace AI.Solvers.Chem.Polymers;

/// <summary>
/// Свойства полимеров и их смесей: температура стеклования, вязкость раствора,
/// степень кристалличности
/// </summary>
public static class PolymerProperties
{
    /// <summary>
    /// Температура стеклования смеси по уравнению Фокса: 1/Tg = сумма(wi/Tgi)
    /// </summary>
    /// <param name="weightFractions">Массовые доли компонентов</param>
    /// <param name="glassTemperatures">Температуры стеклования компонентов, K</param>
    public static double FoxGlassTransition(
        IReadOnlyList<double> weightFractions,
        IReadOnlyList<double> glassTemperatures)
    {
        ArgumentNullException.ThrowIfNull(weightFractions);
        ArgumentNullException.ThrowIfNull(glassTemperatures);

        if (weightFractions.Count != glassTemperatures.Count)
            throw new ArgumentException("Число долей и число температур должно совпадать");

        double total = weightFractions.Sum();

        if (total <= 0)
            throw new ArgumentException("Сумма массовых долей должна быть положительной", nameof(weightFractions));

        double inverse = 0;

        for (int i = 0; i < weightFractions.Count; i++)
        {
            if (glassTemperatures[i] <= 0)
                throw new ArgumentException("Температуры стеклования задаются в кельвинах", nameof(glassTemperatures));

            inverse += weightFractions[i] / total / glassTemperatures[i];
        }

        return 1 / inverse;
    }

    /// <summary>
    /// Температура стеклования смеси по Гордону-Тейлору
    /// </summary>
    /// <param name="firstFraction">Массовая доля первого компонента</param>
    /// <param name="firstGlassTemperature">Температура стеклования первого компонента, K</param>
    /// <param name="secondGlassTemperature">Температура стеклования второго компонента, K</param>
    /// <param name="k">Параметр k уравнения</param>
    public static double GordonTaylorGlassTransition(
        double firstFraction,
        double firstGlassTemperature,
        double secondGlassTemperature,
        double k)
    {
        if (firstFraction is < 0 or > 1)
            throw new ArgumentException("Массовая доля должна лежать в интервале [0; 1]", nameof(firstFraction));

        double w1 = firstFraction, w2 = 1 - firstFraction;

        return ((w1 * firstGlassTemperature) + (k * w2 * secondGlassTemperature)) / (w1 + (k * w2));
    }

    /// <summary>
    /// Характеристическая вязкость по Марку-Хаувинку: [eta] = K·M^a
    /// </summary>
    /// <param name="molarMass">Молярная масса, г/моль</param>
    /// <param name="k">Константа K, дл/г</param>
    /// <param name="a">Показатель a</param>
    public static double IntrinsicViscosity(double molarMass, double k, double a)
    {
        if (molarMass <= 0)
            throw new ArgumentException("Молярная масса должна быть положительной", nameof(molarMass));

        return k * Math.Pow(molarMass, a);
    }

    /// <summary>
    /// Вязкостная молярная масса по характеристической вязкости
    /// </summary>
    /// <param name="intrinsicViscosity">Характеристическая вязкость, дл/г</param>
    /// <param name="k">Константа K, дл/г</param>
    /// <param name="a">Показатель a</param>
    public static double ViscosityAverageMass(double intrinsicViscosity, double k, double a)
    {
        if (intrinsicViscosity <= 0 || k <= 0 || a <= 0)
            throw new ArgumentException("Вязкость и параметры уравнения должны быть положительными");

        return Math.Pow(intrinsicViscosity / k, 1 / a);
    }

    /// <summary>
    /// Характеристическая вязкость экстраполяцией по Хаггинсу:
    /// приведённая вязкость при нулевой концентрации
    /// </summary>
    /// <param name="concentrations">Концентрации растворов, г/дл</param>
    /// <param name="reducedViscosities">Приведённые вязкости, дл/г</param>
    public static (double IntrinsicViscosity, double HugginsConstant, double R2) HugginsExtrapolation(
        IReadOnlyList<double> concentrations,
        IReadOnlyList<double> reducedViscosities)
    {
        ArgumentNullException.ThrowIfNull(concentrations);
        ArgumentNullException.ThrowIfNull(reducedViscosities);

        if (concentrations.Count != reducedViscosities.Count)
            throw new ArgumentException("Число концентраций и число вязкостей должно совпадать");

        var fit = Metrology.LinearFit.Fit(concentrations.ToArray(), reducedViscosities.ToArray());

        // Наклон равен kH·[eta]^2, свободный член - самой характеристической вязкости
        double intrinsic = fit.Intercept;
        double huggins = intrinsic > 0 ? fit.Slope / (intrinsic * intrinsic) : double.NaN;

        return (intrinsic, huggins, fit.R2);
    }

    /// <summary>
    /// Степень кристалличности по теплоте плавления, %
    /// </summary>
    /// <param name="meltingEnthalpy">Теплота плавления образца, Дж/г</param>
    /// <param name="perfectCrystalEnthalpy">Теплота плавления полностью кристаллического полимера, Дж/г</param>
    /// <param name="polymerFraction">Массовая доля полимера в образце</param>
    public static double Crystallinity(double meltingEnthalpy, double perfectCrystalEnthalpy, double polymerFraction = 1.0)
    {
        if (perfectCrystalEnthalpy <= 0)
            throw new ArgumentException("Теплота плавления кристалла должна быть положительной",
                nameof(perfectCrystalEnthalpy));

        if (polymerFraction is <= 0 or > 1)
            throw new ArgumentException("Массовая доля полимера должна лежать в интервале (0; 1]", nameof(polymerFraction));

        return 100.0 * meltingEnthalpy / (perfectCrystalEnthalpy * polymerFraction);
    }

    /// <summary>
    /// Молярная масса между узлами сетки по модулю равновесной высокоэластичности
    /// </summary>
    /// <param name="modulus">Модуль упругости, Па</param>
    /// <param name="density">Плотность, кг/м3</param>
    /// <param name="temperature">Температура, K</param>
    public static double MassBetweenCrosslinks(double modulus, double density, double temperature)
    {
        if (modulus <= 0)
            throw new ArgumentException("Модуль должен быть положительным", nameof(modulus));

        // E = 3·rho·R·T/Mc, масса выражается в г/моль
        return 3 * density * 8.314462618 * temperature / modulus * 1000;
    }
}
