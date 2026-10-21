using AI.Biology.Ecology;
using AI.Biology.Genetics;
using AI.Biology.Populations;
using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Std;

namespace AI.Script.Science;

/// <summary>
/// Популяции, эпидемии, разнообразие сообществ и популяционная генетика.
/// </summary>
/// <remarks>
/// Модели во времени отдают таблицу с колонкой <c>time</c> — её сразу можно рисовать. Своя модель
/// со своими уравнениями — <c>ode.solve</c> либо <c>sim.stock_flow</c>; здесь — классические
/// модели с проверенными формулами и разбором итога.
/// </remarks>
public static partial class BioModule
{
    // --- рост и взаимодействие популяций ---

    /// <summary>
    /// Рост популяции: экспоненциальный либо логистический.
    /// </summary>
    /// <remarks>
    /// Без ёмкости среды (<c>capacity: 0</c>) рост экспоненциальный, <c>N₀·e^(rt)</c>; с ёмкостью —
    /// логистический, S-образный, и численность подходит к <c>capacity</c>, не превышая её.
    /// </remarks>
    [ScriptFn("growth", "Рост популяции во времени: экспоненциальный или логистический",
        Example = "bio.growth(10, 0.3, until: 40, capacity: 1000)")]
    public static ScriptTable Growth(
        IScriptContext context,
        [ScriptParam("начальная численность")] double initial,
        [ScriptParam("удельная скорость роста r за единицу времени")] double rate,
        [ScriptParam("конечное время")] double until,
        [ScriptParam("ёмкость среды K; 0 — экспоненциальный рост без предела")] double capacity = 0,
        [ScriptParam("сколько строк в таблице")] int points = 100)
    {
        const string function = "bio.growth";

        ScriptData.Require(initial > 0, $"{function}: начальная численность положительна");
        ScriptData.Require(capacity >= 0, $"{function}: ёмкость среды не может быть отрицательной");
        ScriptData.Require(until > 0 && points >= 2, $"{function}: нужно until > 0 и хотя бы две строки");

        context.CountAllocation(2L * points);

        var time = new Vector(points);
        var population = new Vector(points);

        for (int i = 0; i < points; i++)
        {
            time[i] = until * i / (points - 1);
            population[i] = capacity > 0
                ? PopulationGrowth.Logistic(initial, rate, capacity, time[i])
                : PopulationGrowth.Exponential(initial, rate, time[i]);
        }

        return ScriptTable.Create([ScriptColumn.FromVector("time", time), ScriptColumn.FromVector("population", population)]);
    }

    [ScriptFn("doubling_time", "Время удвоения численности при экспоненциальном росте",
        Example = "bio.doubling_time(0.03)")]
    public static double DoublingTime([ScriptParam("удельная скорость роста r за единицу времени")] double rate)
        => PopulationGrowth.DoublingTime(rate);

    /// <summary>
    /// Модель «хищник — жертва» Лотки — Вольтерры.
    /// </summary>
    /// <remarks>
    /// <c>prey' = alpha·prey − beta·prey·predators</c>, <c>predators' = delta·prey·predators −
    /// gamma·predators</c>. Численности колеблются вокруг равновесия <c>prey = gamma/delta</c>,
    /// <c>predators = alpha/beta</c>, и пик хищников запаздывает за пиком жертв.
    /// </remarks>
    [ScriptFn("lotka_volterra", "Хищник и жертва по Лотке — Вольтерре: численности во времени",
        Example = "bio.lotka_volterra(40, 9, alpha: 0.1, beta: 0.02, gamma: 0.1, delta: 0.01, until: 200)")]
    public static ScriptTable LotkaVolterraRun(
        IScriptContext context,
        [ScriptParam("начальная численность жертв")] double prey,
        [ScriptParam("начальная численность хищников")] double predators,
        [ScriptParam("скорость размножения жертв")] double alpha,
        [ScriptParam("скорость выедания жертв хищником")] double beta,
        [ScriptParam("смертность хищников")] double gamma,
        [ScriptParam("превращение съеденного в потомство хищников")] double delta,
        [ScriptParam("конечное время")] double until,
        [ScriptParam("сколько строк в таблице")] int points = 200)
    {
        context.CountAllocation(3L * points);

        return ScriptData.Table(LotkaVolterra.Simulate(alpha, beta, gamma, delta, prey, predators, until, points),
            ("time", state => state.Time),
            ("prey", state => state.Prey),
            ("predators", state => state.Predator));
    }

    /// <summary>
    /// Эпидемия по модели SIR в долях населения.
    /// </summary>
    /// <remarks>
    /// <c>beta</c> — заражений на одного больного в сутки при полностью восприимчивом населении,
    /// <c>gamma</c> — доля выздоравливающих за сутки (1 / длительность болезни). <c>r0 = beta/gamma</c>.
    /// В ответе таблица <c>time, s, i, r</c>, пик (<c>peak</c>, <c>peak_time</c>), итоговая доля
    /// переболевших <c>final_size</c> и порог коллективного иммунитета.
    /// </remarks>
    [ScriptFn("sir", "Эпидемия SIR: ход по суткам, R₀, пик и итоговая доля переболевших",
        Example = "bio.sir(0.3, 0.1).peak_time")]
    public static ScriptRecord Sir(
        IScriptContext context,
        [ScriptParam("скорость заражения β, в сутки")] double beta,
        [ScriptParam("скорость выздоровления γ, в сутки")] double gamma,
        [ScriptParam("начальная доля заразных")] double infected = 1e-4,
        [ScriptParam("длительность расчёта, сутки")] double until = 180,
        [ScriptParam("сколько строк в таблице")] int points = 361)
    {
        ScriptData.Require(infected is > 0 and < 1, "bio.sir: начальная доля заразных лежит строго между 0 и 1");

        context.CountAllocation(4L * points);

        EpidemicResult result = EpidemicModels.Sir(beta, gamma, infected, until, points);

        return ScriptData.Record(result,
            ("table", ScriptData.Table(result.States,
                ("time", state => state.Time),
                ("s", state => state.Susceptible),
                ("i", state => state.Infected),
                ("r", state => state.Recovered))),
            ("r0", result.BasicReproductionNumber),
            ("peak", result.PeakInfected),
            ("peak_time", result.PeakTime),
            ("final_size", result.FinalSize),
            ("herd_immunity", result.HerdImmunityThreshold));
    }

    // --- сообщества ---

    /// <summary>
    /// Разнообразие сообщества по численностям видов.
    /// </summary>
    /// <remarks>
    /// <c>shannon</c> — индекс Шеннона (натуральный логарифм), <c>simpson</c> — вероятность, что две
    /// случайные особи разных видов, <c>evenness</c> — выравненность Пиелу, <c>chao1</c> — оценка
    /// истинного числа видов с поправкой на не попавшие в выборку редкие.
    /// </remarks>
    [ScriptFn("diversity", "Разнообразие сообщества: число видов, индексы Шеннона и Симпсона",
        Example = "bio.diversity(<50, 30, 15, 4, 1>).shannon")]
    public static ScriptRecord DiversityOf([ScriptParam("численности видов: целые неотрицательные")] Vector counts)
    {
        int[] abundances = Counts(counts, "bio.diversity");

        ScriptData.Require(abundances.Sum() > 0, "bio.diversity: в выборке нет ни одной особи");

        return ScriptData.Plain(
            ("richness", Diversity.Richness(abundances)),
            ("shannon", Diversity.Shannon(abundances)),
            ("simpson", Diversity.Simpson(abundances)),
            ("evenness", Diversity.Evenness(abundances)),
            ("chao1", Diversity.Chao1(abundances)));
    }

    [ScriptFn("bray_curtis", "Несходство двух сообществ по Брею — Кёртису, от 0 до 1",
        Example = "bio.bray_curtis(<10, 5, 0>, <8, 2, 4>)")]
    public static double BrayCurtis(
        [ScriptParam("численности видов в первом сообществе")] Vector first,
        [ScriptParam("численности тех же видов во втором")] Vector second)
        => Diversity.BrayCurtis(Counts(first, "bio.bray_curtis"), Counts(second, "bio.bray_curtis"));

    // --- генетика ---

    /// <summary>
    /// Проверка равновесия Харди — Вайнберга по числам генотипов.
    /// </summary>
    /// <remarks>
    /// <c>p</c> и <c>q</c> — частоты доминантного и рецессивного аллелей, <c>expected</c> —
    /// ожидаемые числа AA, Aa, aa, <c>equilibrium</c> — согласуются ли наблюдения с равновесием
    /// по критерию хи-квадрат.
    /// </remarks>
    [ScriptFn("hardy_weinberg", "Равновесие Харди — Вайнберга: частоты аллелей и хи-квадрат",
        Example = "bio.hardy_weinberg(298, 489, 213).equilibrium")]
    public static ScriptRecord HardyWeinbergTest(
        [ScriptParam("число особей AA")] int aa,
        [ScriptParam("число особей Aa")] int ab,
        [ScriptParam("число особей aa")] int bb,
        [ScriptParam("уровень значимости")] double alpha = 0.05)
    {
        HardyWeinbergResult result = HardyWeinberg.Test(aa, ab, bb, alpha);

        return ScriptData.Record(result,
            ("p", result.AlleleFrequency),
            ("q", result.RecessiveFrequency),
            ("expected", new Vector(result.ExpectedHomozygousDominant, result.ExpectedHeterozygous, result.ExpectedHomozygousRecessive)),
            ("chi2", result.ChiSquare),
            ("p_value", result.PValue),
            ("equilibrium", result.InEquilibrium));
    }

    [ScriptFn("carrier_frequency", "Доля носителей рецессивного аллеля по доле больных",
        Example = "bio.carrier_frequency(1 / 10000)")]
    public static double CarrierFrequency([ScriptParam("доля особей с рецессивным признаком")] double affected)
        => HardyWeinberg.CarrierFrequency(affected);

    /// <summary>
    /// Соответствие расщепления в потомстве менделевскому.
    /// </summary>
    /// <remarks>
    /// Ожидание — соотношение классов в любых единицах: <c>&lt;3, 1&gt;</c>, <c>&lt;9, 3, 3, 1&gt;</c>;
    /// оно нормируется само. По умолчанию — моногибридное 3 : 1.
    /// </remarks>
    [ScriptFn("mendel", "Расщепление в потомстве против ожидания 3:1, 9:3:3:1: хи-квадрат",
        Example = "bio.mendel(<315, 108, 101, 32>, ratio: <9, 3, 3, 1>).fits")]
    public static ScriptRecord Mendel(
        [ScriptParam("наблюдаемые численности классов")] Vector observed,
        [ScriptParam("ожидаемое соотношение классов; по умолчанию 3 : 1")] Vector? ratio = null,
        [ScriptParam("уровень значимости")] double alpha = 0.05)
    {
        const string function = "bio.mendel";

        int[] counts = Counts(observed, function);
        Vector expected = ratio ?? new Vector(3.0, 1.0);
        double sum = expected.Sum();

        if (expected.Count != counts.Length)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"{function}: классов наблюдено {counts.Length}, а в соотношении {expected.Count}");
        }

        ScriptData.Require(sum > 0 && expected.All(share => share >= 0), $"{function}: соотношение — неотрицательные числа с ненулевой суммой");

        double[] shares = [.. expected.Select(share => share / sum)];
        (double chi, double pValue, bool fits) = AI.Biology.Genetics.Mendel.TestRatio(counts, shares, alpha);

        return ScriptData.Plain(
            ("chi2", chi),
            ("p_value", pValue),
            ("fits", fits),
            ("expected", new Vector([.. shares.Select(share => share * counts.Sum())])));
    }

    /// <summary>Численности из вектора: целые неотрицательные числа.</summary>
    private static int[] Counts(Vector values, string function)
    {
        var counts = new int[values.Count];

        for (int i = 0; i < values.Count; i++)
        {
            ScriptData.Require(values[i] >= 0 && values[i] == Math.Round(values[i]) && values[i] <= int.MaxValue,
                $"{function}: численность {ScriptFormatter.Number(values[i])} в позиции {i} — не целое неотрицательное число");

            counts[i] = (int)values[i];
        }

        return counts;
    }
}
