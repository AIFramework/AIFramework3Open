using AI.MathUtils.ODE;
using AI.DataStructs.Algebraic;
using AI.Insights;

namespace AI.Biology.Populations;

/// <summary>
/// Рост популяции: экспоненциальная и логистическая модели.
/// </summary>
/// <remarks>
/// Экспоненциальный рост описывает начало заселения, когда ресурсов вдоволь; логистический
/// добавляет ёмкость среды и потому не уходит в бесконечность. Обе модели детерминированы
/// и непрерывны: для малых популяций, где важна случайность отдельных рождений и смертей,
/// нужны стохастические модели.
/// </remarks>
public static class PopulationGrowth
{
    /// <summary>Численность при экспоненциальном росте: <c>N = N₀·e^(rt)</c></summary>
    /// <param name="initial">Начальная численность</param>
    /// <param name="rate">Удельная скорость роста</param>
    /// <param name="time">Время</param>
    public static double Exponential(double initial, double rate, double time)
        => initial * Math.Exp(rate * time);

    /// <summary>Время удвоения при экспоненциальном росте</summary>
    /// <param name="rate">Удельная скорость роста</param>
    public static double DoublingTime(double rate)
        => rate <= 0 ? double.PositiveInfinity : Math.Log(2) / rate;

    /// <summary>
    /// Численность при логистическом росте: <c>N = K/(1 + ((K−N₀)/N₀)·e^(−rt))</c>
    /// </summary>
    /// <param name="initial">Начальная численность</param>
    /// <param name="rate">Удельная скорость роста</param>
    /// <param name="capacity">Ёмкость среды</param>
    /// <param name="time">Время</param>
    public static double Logistic(double initial, double rate, double capacity, double time)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        if (initial <= 0)
            return 0;

        double factor = (capacity - initial) / initial;

        return capacity / (1 + (factor * Math.Exp(-rate * time)));
    }

    /// <summary>
    /// Время достижения точки перегиба, где скорость роста наибольшая
    /// </summary>
    /// <param name="initial">Начальная численность</param>
    /// <param name="rate">Удельная скорость роста</param>
    /// <param name="capacity">Ёмкость среды</param>
    /// <remarks>Перегиб приходится ровно на половину ёмкости среды.</remarks>
    public static double InflectionTime(double initial, double rate, double capacity)
    {
        if (initial <= 0 || initial >= capacity || rate <= 0)
            return double.NaN;

        return Math.Log((capacity - initial) / initial) / rate;
    }
}

/// <summary>Состояние системы «хищник — жертва» в момент времени</summary>
/// <param name="Time">Время</param>
/// <param name="Prey">Численность жертв</param>
/// <param name="Predator">Численность хищников</param>
public readonly record struct PredatorPreyState(double Time, double Prey, double Predator);

/// <summary>
/// Модель Лотки — Вольтерры: колебания численностей хищника и жертвы.
/// </summary>
/// <remarks>
/// Система интегрируется методом Рунге — Кутты из <c>AI.ClassicMath</c>: собственного
/// интегратора здесь нет.
/// </remarks>
public static class LotkaVolterra
{
    /// <summary>
    /// Рассчитывает динамику системы
    /// </summary>
    /// <param name="preyGrowth">Скорость размножения жертв α</param>
    /// <param name="predationRate">Скорость выедания β</param>
    /// <param name="predatorMortality">Смертность хищников γ</param>
    /// <param name="conversionRate">Коэффициент превращения пищи в потомство δ</param>
    /// <param name="initialPrey">Начальная численность жертв</param>
    /// <param name="initialPredator">Начальная численность хищников</param>
    /// <param name="finalTime">Конечное время</param>
    /// <param name="points">Число точек вывода</param>
    public static IReadOnlyList<PredatorPreyState> Simulate(
        double preyGrowth, double predationRate,
        double predatorMortality, double conversionRate,
        double initialPrey, double initialPredator,
        double finalTime, int points = 200)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(finalTime);

        var times = new double[points];
        double step = finalTime / (points - 1);

        for (int i = 0; i < points; i++)
            times[i] = i * step;

        Vector Derivative(double _, Vector state)
        {
            double prey = state[0];
            double predator = state[1];

            return new Vector(
                (preyGrowth * prey) - (predationRate * prey * predator),
                (conversionRate * prey * predator) - (predatorMortality * predator));
        }

        Vector[] solution = RungeKutta.SolveSystem(
            Derivative, 0, new Vector(initialPrey, initialPredator), times, stepsPerInterval: 40);

        var states = new PredatorPreyState[points];

        for (int i = 0; i < points; i++)
            states[i] = new PredatorPreyState(times[i], solution[i][0], solution[i][1]);

        return states;
    }

    /// <summary>
    /// Положение равновесия системы: жертв <c>γ/δ</c>, хищников <c>α/β</c>
    /// </summary>
    /// <param name="preyGrowth">Скорость размножения жертв α</param>
    /// <param name="predationRate">Скорость выедания β</param>
    /// <param name="predatorMortality">Смертность хищников γ</param>
    /// <param name="conversionRate">Коэффициент превращения δ</param>
    public static (double Prey, double Predator) Equilibrium(
        double preyGrowth, double predationRate, double predatorMortality, double conversionRate)
        => (predatorMortality / conversionRate, preyGrowth / predationRate);
}

/// <summary>Состояние эпидемии в момент времени</summary>
/// <param name="Time">Время</param>
/// <param name="Susceptible">Восприимчивые</param>
/// <param name="Infected">Заразные</param>
/// <param name="Recovered">Переболевшие</param>
public readonly record struct EpidemicState(double Time, double Susceptible, double Infected, double Recovered);

/// <summary>Итог расчёта эпидемии</summary>
/// <param name="BasicReproductionNumber">Базовое репродуктивное число</param>
/// <param name="PeakInfected">Наибольшая одновременная доля заразных</param>
/// <param name="PeakTime">Время достижения пика</param>
/// <param name="FinalSize">Итоговая доля переболевших</param>
/// <param name="States">Ход эпидемии</param>
public sealed record EpidemicResult(
    double BasicReproductionNumber,
    double PeakInfected,
    double PeakTime,
    double FinalSize,
    IReadOnlyList<EpidemicState> States) : IInterpretable
{
    /// <summary>Порог коллективного иммунитета: <c>1 − 1/R₀</c></summary>
    public double HerdImmunityThreshold => BasicReproductionNumber <= 1
        ? 0
        : 1 - (1 / BasicReproductionNumber);

    /// <inheritdoc />
    public Interpretation Interpret()
        => new InterpretationBuilder("Модель эпидемии SIR")
            .Summary(BasicReproductionNumber > 1
                ? $"R₀ = {Fmt.Num(BasicReproductionNumber, 2)} больше единицы: эпидемия развивается. "
                  + $"Пик приходится на {Fmt.Num(PeakTime, 1)} суток, одновременно болеет до "
                  + $"{Fmt.Pct(PeakInfected)} населения, переболевает в итоге {Fmt.Pct(FinalSize)}."
                : $"R₀ = {Fmt.Num(BasicReproductionNumber, 2)} не превышает единицы: вспышка затухает, "
                  + "каждый заболевший в среднем не успевает заразить даже одного.")
            .Metric("R₀", Fmt.Num(BasicReproductionNumber, 3), null,
                "сколько человек заражает один больной в полностью восприимчивой популяции",
                BasicReproductionNumber > 1 ? MetricQuality.Warning : MetricQuality.Good)
            .Metric("Пик заболеваемости", Fmt.Pct(PeakInfected), null, "наибольшая доля одновременно болеющих",
                PeakInfected > 0.1 ? MetricQuality.Critical : MetricQuality.Neutral)
            .Metric("Время пика", Fmt.Num(PeakTime, 1), "сут", "от начала вспышки")
            .Metric("Итоговый охват", Fmt.Pct(FinalSize), null, "доля переболевших к концу вспышки")
            .Metric("Порог коллективного иммунитета", Fmt.Pct(HerdImmunityThreshold), null,
                "доля невосприимчивых, при которой передача затухает")
            .FindingIf(BasicReproductionNumber > 1,
                $"Эпидемия прекращается не потому, что кончаются больные, а потому, что кончаются "
                + $"восприимчивые: доля переболевших {Fmt.Pct(FinalSize)} превышает порог "
                + $"{Fmt.Pct(HerdImmunityThreshold)} — вспышка проскакивает его по инерции.")
            .FindingIf(PeakInfected > 0.1,
                "Одновременно болеет более десятой части населения — именно эта величина, а не итоговый "
                + "охват, определяет нагрузку на больницы.")
            .Warning("Модель считает популяцию однородной и хорошо перемешанной: все контактируют со всеми "
                + "с равной вероятностью. Возрастная структура, скученность и сети контактов меняют и пик, "
                + "и итоговый охват.")
            .Warning("Переболевшие считаются пожизненно невосприимчивыми. При угасающем иммунитете "
                + "или изменчивом возбудителе нужна модель с возвратом в восприимчивые.")
            .Recommendation("Сравнивать с данными по пику и его времени, а не по итоговому охвату: "
                + "он наблюдается позже всего и хуже всего измеряется.")
            .Build();
}

/// <summary>
/// Компартментные модели распространения инфекции.
/// </summary>
/// <remarks>
/// Система интегрируется методом Рунге — Кутты из <c>AI.ClassicMath</c>.
/// </remarks>
public static class EpidemicModels
{
    /// <summary>
    /// Модель SIR: восприимчивые — заразные — переболевшие
    /// </summary>
    /// <param name="transmissionRate">Скорость передачи β</param>
    /// <param name="recoveryRate">Скорость выздоровления γ</param>
    /// <param name="initialInfectedFraction">Начальная доля заразных</param>
    /// <param name="finalTime">Конечное время в сутках</param>
    /// <param name="points">Число точек вывода</param>
    public static EpidemicResult Sir(
        double transmissionRate, double recoveryRate,
        double initialInfectedFraction = 1e-4,
        double finalTime = 180, int points = 361)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recoveryRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);

        var times = new double[points];
        double step = finalTime / (points - 1);

        for (int i = 0; i < points; i++)
            times[i] = i * step;

        Vector Derivative(double _, Vector state)
        {
            double susceptible = state[0];
            double infected = state[1];

            double newCases = transmissionRate * susceptible * infected;
            double recoveries = recoveryRate * infected;

            return new Vector(-newCases, newCases - recoveries, recoveries);
        }

        Vector[] solution = RungeKutta.SolveSystem(
            Derivative, 0,
            new Vector(1 - initialInfectedFraction, initialInfectedFraction, 0),
            times, stepsPerInterval: 20);

        var states = new EpidemicState[points];
        double peak = 0;
        double peakTime = 0;

        for (int i = 0; i < points; i++)
        {
            states[i] = new EpidemicState(times[i], solution[i][0], solution[i][1], solution[i][2]);

            if (solution[i][1] > peak)
            {
                peak = solution[i][1];
                peakTime = times[i];
            }
        }

        return new EpidemicResult(
            transmissionRate / recoveryRate,
            peak,
            peakTime,
            states[^1].Recovered,
            states);
    }

    /// <summary>
    /// Итоговая доля переболевших как решение уравнения <c>1 − R = exp(−R₀·R)</c>
    /// </summary>
    /// <param name="basicReproductionNumber">Базовое репродуктивное число</param>
    /// <remarks>
    /// Уравнение неявное и решается простой итерацией. При R₀ не больше единицы вспышка
    /// затухает и доля переболевших стремится к нулю.
    /// </remarks>
    public static double FinalEpidemicSize(double basicReproductionNumber)
    {
        if (basicReproductionNumber <= 1)
            return 0;

        double size = 0.5;

        for (int i = 0; i < 200; i++)
            size = 1 - Math.Exp(-basicReproductionNumber * size);

        return size;
    }

    /// <summary>Порог коллективного иммунитета</summary>
    /// <param name="basicReproductionNumber">Базовое репродуктивное число</param>
    public static double HerdImmunityThreshold(double basicReproductionNumber)
        => basicReproductionNumber <= 1 ? 0 : 1 - (1 / basicReproductionNumber);
}
