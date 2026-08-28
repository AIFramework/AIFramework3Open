using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Econometrics;
using AI.Economics.Insights;
using AI.Statistics;

namespace AI.Economics.Corporate;

/// <summary>Тип реального опциона в проекте.</summary>
public enum ProjectOption
{
    /// <summary>Опцион на отсрочку запуска: инвестировать можно в любой момент до истечения.</summary>
    Defer,

    /// <summary>Опцион на расширение: докупить долю масштаба за дополнительные вложения.</summary>
    Expand,

    /// <summary>Опцион на отказ: выйти из проекта, получив ликвидационную стоимость.</summary>
    Abandon,
}

/// <summary>Входные данные оценки реального опциона методом наименьших квадратов.</summary>
public sealed record ProjectOptionInput
{
    /// <summary>Название проекта.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Тип опциона.</summary>
    public ProjectOption Option { get; init; } = ProjectOption.Defer;

    /// <summary>Текущая стоимость проекта без опциона.</summary>
    public double ProjectValue { get; init; } = 100;

    /// <summary>Инвестиции, необходимые для реализации опциона.</summary>
    public double InvestmentCost { get; init; } = 100;

    /// <summary>Ликвидационная стоимость при отказе от проекта.</summary>
    public double SalvageValue { get; init; }

    /// <summary>Коэффициент расширения масштаба.</summary>
    public double ExpansionFactor { get; init; } = 0.5;

    /// <summary>Срок жизни опциона в годах.</summary>
    public double Horizon { get; init; } = 3;

    /// <summary>Волатильность стоимости проекта.</summary>
    public double Volatility { get; init; } = 0.4;

    /// <summary>Безрисковая ставка.</summary>
    public double RiskFreeRate { get; init; } = 0.08;

    /// <summary>Дивидендная доходность проекта: упущенный поток от откладывания.</summary>
    public double ConvenienceYield { get; init; }

    /// <summary>Число моментов возможного решения.</summary>
    public int Steps { get; init; } = 12;

    /// <summary>Число траекторий.</summary>
    public int Paths { get; init; } = 20_000;

    /// <summary>Зерно генератора.</summary>
    public int Seed { get; init; } = 42;
}

/// <summary>Результат оценки реального опциона методом Лонгстаффа — Шварца.</summary>
public sealed record ProjectOptionResult : IInterpretable
{
    /// <summary>Название проекта.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Тип опциона.</summary>
    public ProjectOption Option { get; init; }

    /// <summary>Стоимость проекта вместе с опционом.</summary>
    public double TotalValue { get; init; }

    /// <summary>Чистая приведённая стоимость проекта без учёта гибкости.</summary>
    public double StaticNpv { get; init; }

    /// <summary>Стоимость самой гибкости.</summary>
    public double OptionValue => TotalValue - StaticNpv;

    /// <summary>Стандартная ошибка оценки по методу Монте-Карло.</summary>
    public double StandardError { get; init; }

    /// <summary>Доля траекторий, на которых опцион исполняется.</summary>
    public double ExerciseProbability { get; init; }

    /// <summary>Среднее время до исполнения в годах.</summary>
    public double ExpectedExerciseTime { get; init; }

    /// <summary>Граница исполнения по шагам: критическое значение стоимости проекта.</summary>
    public Vector ExerciseBoundary { get; init; } = new(0);

    /// <summary>Число траекторий.</summary>
    public int Paths { get; init; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool worthWaiting = OptionValue > 0.05 * Math.Abs(StaticNpv) && StaticNpv > 0;
        bool rescuesProject = StaticNpv <= 0 && TotalValue > 0;
        double relative = Math.Abs(StaticNpv) > 1e-9 ? OptionValue / Math.Abs(StaticNpv) : 0;

        var builder = new InterpretationBuilder($"Реальный опцион: {OptionName()} — {Name}")
            .Summary($"Стоимость проекта с учётом гибкости {Fmt.Money(TotalValue)} против " +
                     $"{Fmt.Money(StaticNpv)} без неё: сама гибкость стоит " +
                     $"{Fmt.Money(OptionValue)}. Опцион исполняется на " +
                     $"{Fmt.Pct(ExerciseProbability, 0)} траекторий, в среднем через " +
                     $"{Fmt.Num(ExpectedExerciseTime, 2)} года. Оценка по {Paths} траекториям, " +
                     $"стандартная ошибка {Fmt.Money(StandardError)}.")
            .Metric("Стоимость с гибкостью", Fmt.Money(TotalValue), null,
                "чистая приведённая стоимость с учётом права принять решение позже",
                TotalValue > 0 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Стоимость без гибкости", Fmt.Money(StaticNpv), null,
                "классическая оценка «сейчас или никогда»")
            .Metric("Стоимость опциона", Fmt.Money(OptionValue), null,
                $"{Fmt.Pct(relative, 0)} от статической оценки",
                OptionValue > 0 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Вероятность исполнения", ExerciseProbability, null,
                "доля сценариев, в которых правом воспользуются",
                MetricQuality.Neutral, 3)
            .Metric("Среднее время до решения", ExpectedExerciseTime, "лет",
                "когда именно гибкость реализуется", MetricQuality.Neutral, 2)
            .Metric("Стандартная ошибка", Fmt.Money(StandardError), null,
                "точность оценки методом Монте-Карло",
                StandardError < Math.Abs(TotalValue) * 0.02 ? MetricQuality.Good : MetricQuality.Warning);

        for (int i = 0; i < ExerciseBoundary.Count; i++)
        {
            if (!double.IsFinite(ExerciseBoundary[i])) continue;

            builder.Metric($"Граница исполнения, шаг {i + 1}", ExerciseBoundary[i], null,
                "стоимость проекта, при которой решение принимается", MetricQuality.Unknown, 1);
        }

        return builder
            .Finding("Классическая оценка предполагает решение «сейчас или никогда» и потому " +
                     "занижает стоимость проектов с высокой неопределённостью. Право подождать " +
                     "и посмотреть само имеет цену — и тем большую, чем выше волатильность.")
            .FindingIf(worthWaiting,
                $"Гибкость добавляет {Fmt.Pct(relative, 0)} к статической оценке. Проект " +
                "стоит начинать не немедленно, а по достижении границы исполнения: " +
                "преждевременный запуск уничтожает эту стоимость.")
            .FindingIf(rescuesProject,
                "Без учёта гибкости проект убыточен, с учётом — нет. Решение здесь не " +
                "«вкладывать или нет», а «купить право вложиться позже»: обычно это " +
                "заметно дешевле полной инвестиции.")
            .FindingIf(ExerciseProbability < 0.3,
                $"Опцион исполняется лишь на {Fmt.Pct(ExerciseProbability, 0)} траекторий. " +
                "Его ценность в защите от неблагоприятного исхода, а не в ожидаемом выигрыше.")
            .WarningIf(OptionValue < 0,
                "Расчётная стоимость гибкости отрицательна, что невозможно по построению. " +
                "Увеличьте число траекторий: это шум метода Монте-Карло.")
            .WarningIf(StandardError > Math.Abs(TotalValue) * 0.02,
                $"Стандартная ошибка {Fmt.Money(StandardError)} велика относительно оценки. " +
                "Для устойчивого результата нужно больше траекторий.")
            .Warning("Метод предполагает, что стоимость проекта следует геометрическому " +
                     "броуновскому движению с постоянной волатильностью. Для проектов " +
                     "с этапами и качественными развилками это грубое приближение: " +
                     "волатильность там не постоянна и оценивается хуже всего.")
            .Recommendation("Оценивайте волатильность по разбросу прогнозов денежного потока, " +
                            "а не по волатильности акций отрасли: именно она определяет " +
                            "стоимость опциона и обычно берётся с потолка.")
            .Recommendation("Используйте границу исполнения как правило принятия решения: " +
                            "она переводит абстрактную стоимость опциона в конкретный " +
                            "критерий «запускаем, когда оценка проекта превысит X».")
            .Build();
    }

    /// <summary>Читаемое название опциона.</summary>
    private string OptionName() => Option switch
    {
        ProjectOption.Defer => "отсрочка",
        ProjectOption.Expand => "расширение",
        _ => "отказ",
    };
}

/// <summary>
/// Оценка реальных опционов методом наименьших квадратов Лонгстаффа — Шварца.
/// </summary>
/// <remarks>
/// <para>
/// Опцион с правом исполнения в любой момент нельзя оценить прямым
/// моделированием: на каждом шаге нужно сравнить выигрыш от немедленного
/// исполнения с ожидаемой стоимостью продолжения, а она неизвестна.
/// </para>
/// <para>
/// Лонгстафф и Шварц предложили оценивать стоимость продолжения регрессией.
/// Траектории моделируются вперёд, затем обратным ходом на каждом шаге
/// дисконтированный будущий выигрыш регрессируется на текущее состояние:
/// </para>
/// <code>
/// E[continuation | V_t] ~ a + b * V_t + c * V_t^2
/// исполнять, если payoff(V_t) &gt; предсказанная стоимость продолжения
/// </code>
/// <para>
/// Регрессия строится только по траекториям, где исполнение имеет смысл, —
/// это существенно снижает смещение аппроксимации.
/// </para>
/// <para>
/// Побочный результат метода не менее полезен, чем сама стоимость: граница
/// исполнения по шагам превращает оценку опциона в практическое правило
/// «начинаем проект, когда его стоимость превысит такую-то величину».
/// </para>
/// </remarks>
public static class LongstaffSchwartz
{
    /// <summary>Оценивает реальный опцион проекта.</summary>
    /// <param name="input">Параметры проекта и опциона.</param>
    /// <returns>Стоимость с гибкостью, стоимость самого опциона и граница исполнения.</returns>
    /// <exception cref="ArgumentNullException">Входные данные не заданы.</exception>
    /// <exception cref="ArgumentException">Параметры вне допустимого диапазона.</exception>
    public static ProjectOptionResult Value(ProjectOptionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.ProjectValue <= 0)
            throw new ArgumentException("Стоимость проекта должна быть положительной.", nameof(input));
        if (input.Volatility <= 0)
            throw new ArgumentException("Волатильность должна быть положительной.", nameof(input));
        if (input.Horizon <= 0)
            throw new ArgumentException("Срок опциона должен быть положительным.", nameof(input));
        if (input.Steps < 2 || input.Paths < 500)
            throw new ArgumentException("Нужно минимум два шага и пятьсот траекторий.", nameof(input));

        Random rng = RandomEngine.Create(input.Seed);

        int steps = input.Steps, paths = input.Paths;
        double dt = input.Horizon / steps;
        double drift = (input.RiskFreeRate - input.ConvenienceYield - (0.5 * input.Volatility * input.Volatility)) * dt;
        double diffusion = input.Volatility * Math.Sqrt(dt);
        double discount = Math.Exp(-input.RiskFreeRate * dt);

        var values = new double[paths][];
        for (int p = 0; p < paths; p++)
        {
            values[p] = new double[steps + 1];
            values[p][0] = input.ProjectValue;

            for (int t = 1; t <= steps; t++)
                values[p][t] = values[p][t - 1] * Math.Exp(drift + (diffusion * RandomEngine.NextGaussian(rng)));
        }

        var cashFlow = new double[paths];
        var exerciseStep = new int[paths];

        for (int p = 0; p < paths; p++)
        {
            cashFlow[p] = Payoff(values[p][steps], input);
            exerciseStep[p] = cashFlow[p] > 0 ? steps : -1;
        }

        var boundary = new Vector(steps);
        for (int t = 0; t < steps; t++) boundary[t] = double.NaN;

        for (int t = steps - 1; t >= 1; t--)
        {
            for (int p = 0; p < paths; p++) cashFlow[p] *= discount;

            var live = new List<int>(paths);
            for (int p = 0; p < paths; p++)
                if (Payoff(values[p][t], input) > 0) live.Add(p);

            if (live.Count < 20) continue;

            // Стоимость продолжения аппроксимируется квадратичной регрессией
            var x = new Matrix(live.Count, 2);
            var y = new Vector(live.Count);

            for (int i = 0; i < live.Count; i++)
            {
                double value = values[live[i]][t];
                x[i, 0] = value;
                x[i, 1] = value * value;
                y[i] = cashFlow[live[i]];
            }

            RegressionResult fit;
            try { fit = LinearRegression.Fit(x, y); }
            catch (ArgumentException) { continue; }

            double a = fit.Coefficients[0].Estimate;
            double b = fit.Coefficients[1].Estimate;
            double c = fit.Coefficients[2].Estimate;

            double threshold = double.NaN;

            foreach (int p in live)
            {
                double value = values[p][t];
                double continuation = a + (b * value) + (c * value * value);
                double immediate = Payoff(value, input);

                if (immediate <= continuation) continue;

                cashFlow[p] = immediate;
                exerciseStep[p] = t;

                threshold = double.IsNaN(threshold)
                    ? value
                    : input.Option == ProjectOption.Abandon
                        ? Math.Max(threshold, value)
                        : Math.Min(threshold, value);
            }

            boundary[t] = threshold;
        }

        for (int p = 0; p < paths; p++) cashFlow[p] *= discount;

        double mean = cashFlow.Average();
        double variance = cashFlow.Sum(v => (v - mean) * (v - mean)) / Math.Max(1, paths - 1);

        int exercised = exerciseStep.Count(s => s > 0);
        double averageTime = exercised > 0
            ? exerciseStep.Where(s => s > 0).Average() * dt
            : 0;

        double staticValue = Payoff(input.ProjectValue, input);
        double total = input.Option == ProjectOption.Defer ? mean : input.ProjectValue - input.InvestmentCost + mean;
        double baseline = input.Option == ProjectOption.Defer
            ? Math.Max(input.ProjectValue - input.InvestmentCost, 0)
            : input.ProjectValue - input.InvestmentCost;

        return new ProjectOptionResult
        {
            Name = input.Name,
            Option = input.Option,
            TotalValue = total,
            StaticNpv = input.Option == ProjectOption.Defer ? input.ProjectValue - input.InvestmentCost : baseline,
            StandardError = Math.Sqrt(Math.Max(variance, 0) / paths),
            ExerciseProbability = (double)exercised / paths,
            ExpectedExerciseTime = averageTime,
            ExerciseBoundary = boundary,
            Paths = paths,
        };
    }

    /// <summary>Выигрыш от немедленного исполнения опциона.</summary>
    private static double Payoff(double projectValue, ProjectOptionInput input) => input.Option switch
    {
        ProjectOption.Defer => Math.Max(projectValue - input.InvestmentCost, 0),
        ProjectOption.Expand => Math.Max((input.ExpansionFactor * projectValue) - input.InvestmentCost, 0),
        _ => Math.Max(input.SalvageValue - projectValue, 0),
    };
}
