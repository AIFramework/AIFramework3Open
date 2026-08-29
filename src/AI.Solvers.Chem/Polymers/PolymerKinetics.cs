using AI.Solvers.Chem.Metrology;

namespace AI.Solvers.Chem.Polymers;

/// <summary>
/// Константы сополимеризации, найденные линеаризацией
/// </summary>
/// <param name="R1">Константа r1</param>
/// <param name="R2">Константа r2</param>
/// <param name="R1Error">Стандартная ошибка r1</param>
/// <param name="R2Error">Стандартная ошибка r2</param>
/// <param name="R2Coefficient">Коэффициент детерминации линеаризации</param>
/// <param name="Method">Название метода</param>
public readonly record struct ReactivityRatios(
    double R1,
    double R2,
    double R1Error,
    double R2Error,
    double R2Coefficient,
    string Method)
{
    /// <summary>
    /// Произведение r1·r2: около единицы отвечает идеальной сополимеризации,
    /// близкое к нулю - чередованию звеньев
    /// </summary>
    public double Product => R1 * R2;

    /// <summary>Характер сополимеризации по произведению констант</summary>
    public string Behaviour => Product switch
    {
        < 0.05 => "чередующаяся",
        < 0.8 => "статистическая с тенденцией к чередованию",
        <= 1.2 => "идеальная (статистическая)",
        _ => "блочная (тенденция к гомополимеризации)"
    };
}

/// <summary>
/// Кинетика и статистика полимеризации: степень полимеризации, гель-точка,
/// передача цепи, состав сополимера
/// </summary>
public static class PolymerKinetics
{
    /// <summary>
    /// Средняя степень полимеризации по Карозерсу при стехиометрическом дисбалансе
    /// </summary>
    /// <param name="conversion">Степень завершённости реакции p</param>
    /// <param name="stoichiometricRatio">Отношение количеств функциональных групп r (не больше 1)</param>
    public static double CarothersDegree(double conversion, double stoichiometricRatio = 1.0)
    {
        if (conversion is < 0 or >= 1)
            throw new ArgumentException("Степень завершённости должна лежать в интервале [0; 1)", nameof(conversion));

        if (stoichiometricRatio is <= 0 or > 1)
            throw new ArgumentException("Отношение групп должно лежать в интервале (0; 1]", nameof(stoichiometricRatio));

        double r = stoichiometricRatio;

        return (1 + r) / (1 + r - (2 * r * conversion));
    }

    /// <summary>
    /// Степень завершённости, нужная для заданной степени полимеризации
    /// </summary>
    /// <param name="degree">Требуемая средняя степень полимеризации</param>
    /// <param name="stoichiometricRatio">Отношение количеств функциональных групп</param>
    public static double ConversionForDegree(double degree, double stoichiometricRatio = 1.0)
    {
        if (degree <= 1)
            throw new ArgumentException("Степень полимеризации должна превышать единицу", nameof(degree));

        double r = stoichiometricRatio;

        return (1 + r - ((1 + r) / degree)) / (2 * r);
    }

    /// <summary>
    /// Средняя функциональность смеси мономеров
    /// </summary>
    /// <param name="amounts">Количества мономеров, моль</param>
    /// <param name="functionalities">Функциональности мономеров</param>
    public static double AverageFunctionality(IReadOnlyList<double> amounts, IReadOnlyList<double> functionalities)
    {
        ArgumentNullException.ThrowIfNull(amounts);
        ArgumentNullException.ThrowIfNull(functionalities);

        if (amounts.Count != functionalities.Count)
            throw new ArgumentException("Число количеств и число функциональностей должно совпадать");

        double total = amounts.Sum();

        if (total <= 0)
            throw new ArgumentException("Суммарное количество мономеров должно быть положительным", nameof(amounts));

        double groups = 0;

        for (int i = 0; i < amounts.Count; i++)
            groups += amounts[i] * functionalities[i];

        return groups / total;
    }

    /// <summary>
    /// Гель-точка по Флори-Штокмайеру: степень завершённости, при которой
    /// образуется бесконечная сетка
    /// </summary>
    /// <param name="averageFunctionality">Средняя функциональность смеси</param>
    public static double GelPoint(double averageFunctionality)
    {
        if (averageFunctionality <= 2)
            throw new ArgumentException("При средней функциональности не выше двух сетка не образуется",
                nameof(averageFunctionality));

        return 2.0 / averageFunctionality;
    }

    /// <summary>
    /// Уравнение Майо: степень полимеризации с учётом передачи цепи
    /// </summary>
    /// <param name="degreeWithoutTransfer">Степень полимеризации без передатчика</param>
    /// <param name="transferConstant">Константа передачи цепи Cs</param>
    /// <param name="transferAgentConcentration">Концентрация передатчика</param>
    /// <param name="monomerConcentration">Концентрация мономера</param>
    public static double MayoDegree(double degreeWithoutTransfer, double transferConstant,
        double transferAgentConcentration, double monomerConcentration)
    {
        if (degreeWithoutTransfer <= 0)
            throw new ArgumentException("Степень полимеризации должна быть положительной", nameof(degreeWithoutTransfer));

        if (monomerConcentration <= 0)
            throw new ArgumentException("Концентрация мономера должна быть положительной", nameof(monomerConcentration));

        double inverse = (1 / degreeWithoutTransfer)
            + (transferConstant * transferAgentConcentration / monomerConcentration);

        return 1 / inverse;
    }

    /// <summary>
    /// Константа передачи цепи по серии опытов: наклон зависимости 1/Xn от [S]/[M]
    /// </summary>
    /// <param name="agentToMonomerRatios">Отношения [S]/[M]</param>
    /// <param name="degrees">Наблюдённые степени полимеризации</param>
    public static LinearFit TransferConstant(IReadOnlyList<double> agentToMonomerRatios, IReadOnlyList<double> degrees)
    {
        ArgumentNullException.ThrowIfNull(agentToMonomerRatios);
        ArgumentNullException.ThrowIfNull(degrees);

        if (agentToMonomerRatios.Count != degrees.Count)
            throw new ArgumentException("Число отношений и число степеней должно совпадать");

        return LinearFit.Fit(agentToMonomerRatios.ToArray(), degrees.Select(d => 1.0 / d).ToArray());
    }

    /// <summary>
    /// Мгновенный состав сополимера по уравнению Майо-Льюиса
    /// </summary>
    /// <param name="monomerFraction">Мольная доля первого мономера в смеси</param>
    /// <param name="r1">Константа сополимеризации r1</param>
    /// <param name="r2">Константа сополимеризации r2</param>
    public static double CopolymerComposition(double monomerFraction, double r1, double r2)
    {
        if (monomerFraction is < 0 or > 1)
            throw new ArgumentException("Мольная доля должна лежать в интервале [0; 1]", nameof(monomerFraction));

        double f1 = monomerFraction, f2 = 1 - monomerFraction;
        double numerator = (r1 * f1 * f1) + (f1 * f2);
        double denominator = (r1 * f1 * f1) + (2 * f1 * f2) + (r2 * f2 * f2);

        return denominator > 0 ? numerator / denominator : double.NaN;
    }

    /// <summary>
    /// Азеотропный состав сополимеризации: состав смеси, при котором сополимер
    /// повторяет её по составу
    /// </summary>
    /// <param name="r1">Константа r1</param>
    /// <param name="r2">Константа r2</param>
    public static double? AzeotropicComposition(double r1, double r2)
    {
        double denominator = 2 - r1 - r2;

        if (Math.Abs(denominator) < 1e-12)
            return null;

        double fraction = (1 - r2) / denominator;

        return fraction is > 0 and < 1 ? fraction : null;
    }

    /// <summary>
    /// Константы сополимеризации методом Файнмана-Росса
    /// </summary>
    /// <param name="monomerFractions">Мольные доли первого мономера в смеси</param>
    /// <param name="copolymerFractions">Мольные доли первого звена в сополимере</param>
    public static ReactivityRatios FinemanRoss(
        IReadOnlyList<double> monomerFractions,
        IReadOnlyList<double> copolymerFractions)
    {
        var (g, h) = FinemanRossVariables(monomerFractions, copolymerFractions);
        LinearFit fit = LinearFit.Fit(h, g);

        return new ReactivityRatios(fit.Slope, -fit.Intercept, fit.SlopeStdError, fit.InterceptStdError,
            fit.R2, "Файнман-Росс");
    }

    /// <summary>
    /// Константы сополимеризации методом Келена-Тюдоша: та же линеаризация,
    /// но с выравниванием веса крайних точек
    /// </summary>
    /// <param name="monomerFractions">Мольные доли первого мономера в смеси</param>
    /// <param name="copolymerFractions">Мольные доли первого звена в сополимере</param>
    public static ReactivityRatios KelenTudos(
        IReadOnlyList<double> monomerFractions,
        IReadOnlyList<double> copolymerFractions)
    {
        var (g, h) = FinemanRossVariables(monomerFractions, copolymerFractions);
        double alpha = Math.Sqrt(h.Min() * h.Max());

        var eta = new double[g.Length];
        var xi = new double[g.Length];

        for (int i = 0; i < g.Length; i++)
        {
            eta[i] = g[i] / (alpha + h[i]);
            xi[i] = h[i] / (alpha + h[i]);
        }

        LinearFit fit = LinearFit.Fit(xi, eta);

        // eta = (r1 + r2/alpha)·xi - r2/alpha: свободный член даёт r2, сумма - r1
        double r2 = -fit.Intercept * alpha;
        double r1 = fit.Slope + fit.Intercept;

        return new ReactivityRatios(r1, r2,
            fit.SlopeStdError + fit.InterceptStdError, fit.InterceptStdError * alpha,
            fit.R2, "Келен-Тюдош");
    }

    private static (double[] G, double[] H) FinemanRossVariables(
        IReadOnlyList<double> monomerFractions,
        IReadOnlyList<double> copolymerFractions)
    {
        ArgumentNullException.ThrowIfNull(monomerFractions);
        ArgumentNullException.ThrowIfNull(copolymerFractions);

        if (monomerFractions.Count != copolymerFractions.Count)
            throw new ArgumentException("Число составов смеси и сополимера должно совпадать");

        if (monomerFractions.Count < 3)
            throw new ArgumentException("Нужно не менее трёх опытов", nameof(monomerFractions));

        var g = new double[monomerFractions.Count];
        var h = new double[monomerFractions.Count];

        for (int i = 0; i < monomerFractions.Count; i++)
        {
            double f1 = monomerFractions[i], big1 = copolymerFractions[i];

            if (f1 is <= 0 or >= 1 || big1 is <= 0 or >= 1)
                throw new ArgumentException("Мольные доли должны лежать строго внутри интервала (0; 1)");

            double x = f1 / (1 - f1);
            double y = big1 / (1 - big1);

            g[i] = x * (y - 1) / y;
            h[i] = x * x / y;
        }

        return (g, h);
    }
}
