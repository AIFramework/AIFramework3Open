using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.Economics.Numerics;

namespace AI.Economics.Risk;

/// <summary>Сценарий стресс-теста.</summary>
/// <param name="Name">Название сценария.</param>
/// <param name="Shocks">Шоки по факторам риска.</param>
/// <param name="Loss">Потери портфеля в сценарии.</param>
/// <param name="LossShare">Потери в долях портфеля.</param>
public sealed record StressScenario(
    string Name, IReadOnlyList<double> Shocks, double Loss, double LossShare);

/// <summary>Результат обратного тестирования модели риска.</summary>
public sealed record BacktestVarResult : IInterpretable
{
    /// <summary>Название модели.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Уровень доверия.</summary>
    public double Confidence { get; init; } = 0.99;

    /// <summary>Число наблюдений.</summary>
    public int Observations { get; init; }

    /// <summary>Число пробоев порога.</summary>
    public int Exceptions { get; init; }

    /// <summary>Ожидаемое число пробоев.</summary>
    public double ExpectedExceptions => Observations * (1 - Confidence);

    /// <summary>Наблюдаемая доля пробоев.</summary>
    public double ExceptionRate => Observations > 0 ? (double)Exceptions / Observations : 0;

    /// <summary>Статистика теста Купца на безусловное покрытие.</summary>
    public double KupiecStatistic { get; init; }

    /// <summary>Уровень значимости теста Купца.</summary>
    public double KupiecPValue { get; init; } = 1;

    /// <summary>Статистика теста Кристофферсена на независимость пробоев.</summary>
    public double IndependenceStatistic { get; init; }

    /// <summary>Уровень значимости теста на независимость.</summary>
    public double IndependencePValue { get; init; } = 1;

    /// <summary>Совместная статистика условного покрытия.</summary>
    public double ConditionalCoverageStatistic { get; init; }

    /// <summary>Уровень значимости совместного теста.</summary>
    public double ConditionalCoveragePValue { get; init; } = 1;

    /// <summary>Максимальная серия пробоев подряд.</summary>
    public int LongestExceptionRun { get; init; }

    /// <summary>Средний убыток при пробое относительно порога.</summary>
    public double AverageExceptionSeverity { get; init; }

    /// <summary>Зона светофора банковского надзора.</summary>
    public string TrafficLight { get; init; } = string.Empty;

    /// <summary>Принята ли модель по обоим тестам.</summary>
    public bool IsAccepted => KupiecPValue >= 0.05 && IndependencePValue >= 0.05;

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        bool tooMany = Exceptions > ExpectedExceptions;
        bool clustered = IndependencePValue < 0.05;

        return new InterpretationBuilder($"Обратное тестирование модели риска: {Model}")
            .Summary($"На {Observations} наблюдениях порог пробит {Exceptions} раз при " +
                     $"ожидаемых {Fmt.Num(ExpectedExceptions, 1)}. Доля пробоев " +
                     $"{Fmt.Pct(ExceptionRate, 2)} против заявленных " +
                     $"{Fmt.Pct(1 - Confidence, 2)}. Тест Купца p = {Fmt.Num(KupiecPValue, 4)}, " +
                     $"тест на независимость p = {Fmt.Num(IndependencePValue, 4)}. " +
                     $"Зона надзора: {TrafficLight}. Модель " +
                     $"{(IsAccepted ? "принимается" : "отвергается")}.")
            .Metric("Пробоев", Exceptions, null,
                $"ожидалось {Fmt.Num(ExpectedExceptions, 1)}",
                Math.Abs(Exceptions - ExpectedExceptions) <= 2 * Math.Sqrt(ExpectedExceptions)
                    ? MetricQuality.Good : MetricQuality.Warning, 0)
            .Metric("Доля пробоев", ExceptionRate, null,
                $"заявлено {Fmt.Pct(1 - Confidence, 2)}", MetricQuality.Neutral, 4)
            .Metric("Тест Купца", KupiecStatistic, null,
                $"p = {Fmt.Num(KupiecPValue, 4)}; проверка числа пробоев",
                KupiecPValue >= 0.05 ? MetricQuality.Good : MetricQuality.Critical, 3)
            .Metric("Тест на независимость", IndependenceStatistic, null,
                $"p = {Fmt.Num(IndependencePValue, 4)}; проверка группировки пробоев",
                IndependencePValue >= 0.05 ? MetricQuality.Good : MetricQuality.Critical, 3)
            .Metric("Совместный тест", ConditionalCoverageStatistic, null,
                $"p = {Fmt.Num(ConditionalCoveragePValue, 4)}; число и независимость вместе",
                ConditionalCoveragePValue >= 0.05 ? MetricQuality.Good : MetricQuality.Critical, 3)
            .Metric("Максимальная серия пробоев", LongestExceptionRun, null,
                LongestExceptionRun > 2 ? "пробои идут подряд" : "пробои разрознены",
                LongestExceptionRun > 2 ? MetricQuality.Warning : MetricQuality.Good, 0)
            .Metric("Тяжесть пробоя", AverageExceptionSeverity, "×",
                "во сколько раз средний убыток при пробое превышает порог",
                AverageExceptionSeverity > 1.5 ? MetricQuality.Warning : MetricQuality.Neutral, 2)
            .Metric("Зона надзора", TrafficLight, null,
                "классификация по числу пробоев на годовом окне",
                TrafficLight == "зелёная" ? MetricQuality.Good
                    : TrafficLight == "жёлтая" ? MetricQuality.Warning : MetricQuality.Critical)
            .Finding("Проверка модели риска состоит из двух независимых вопросов: " +
                     "верно ли число пробоев и не идут ли они подряд. Модель может " +
                     "давать правильную частоту и при этом пропускать все убытки " +
                     "одного кризисного месяца — тест Купца этого не увидит.")
            .FindingIf(!tooMany && Exceptions < ExpectedExceptions * 0.5,
                $"Пробоев заметно меньше ожидаемого ({Exceptions} против " +
                $"{Fmt.Num(ExpectedExceptions, 1)}). Модель переоценивает риск: " +
                "капитал резервируется избыточно, а лимиты сдерживают операции без причины.")
            .FindingIf(clustered,
                $"Пробои группируются: максимальная серия {LongestExceptionRun}. " +
                "Это признак того, что модель не учитывает кластеризацию волатильности — " +
                "условная дисперсия исправляет именно эту проблему.")
            .WarningIf(tooMany && KupiecPValue < 0.05,
                $"Пробоев значимо больше ожидаемого. Модель занижает риск, " +
                "и рассчитанный по ней капитал недостаточен.")
            .WarningIf(AverageExceptionSeverity > 1.5,
                $"При пробое убыток в среднем в {Fmt.Num(AverageExceptionSeverity, 2)} раза " +
                "превышает порог. Даже при верном числе пробоев их тяжесть означает " +
                "недооценку хвоста.")
            .WarningIf(Observations < 250,
                $"Всего {Observations} наблюдений. При уровне {Fmt.Pct(Confidence, 0)} " +
                "ожидается лишь несколько пробоев, и тесты почти лишены мощности.")
            .Warning("Обратное тестирование проверяет модель на прошлом. Оно не может " +
                     "обнаружить недооценку риска событий, которых в выборке не было, — " +
                     "для этого нужны стресс-тесты.")
            .Recommendation("Проверяйте оба теста вместе: принятие модели требует и верной " +
                            "частоты пробоев, и их независимости во времени.")
            .Recommendation("Дополняйте обратное тестирование проверкой ожидаемых потерь " +
                            "в хвосте: число пробоев ничего не говорит об их величине.")
            .Build();
    }
}

/// <summary>Результат стресс-тестирования портфеля.</summary>
public sealed record StressTestResult : IInterpretable
{
    /// <summary>Название портфеля.</summary>
    public string Portfolio { get; init; } = string.Empty;

    /// <summary>Сценарии по убыванию потерь.</summary>
    public IReadOnlyList<StressScenario> Scenarios { get; init; } = [];

    /// <summary>Потери в наихудшем сценарии.</summary>
    public double WorstLoss { get; init; }

    /// <summary>Стоимость под риском для сравнения.</summary>
    public double ValueAtRisk { get; init; }

    /// <summary>Комбинация шоков, приводящая к заданному уровню потерь.</summary>
    public IReadOnlyList<double> ReverseStressShocks { get; init; } = [];

    /// <summary>Целевой уровень потерь обратного стресс-теста.</summary>
    public double ReverseStressTarget { get; init; }

    /// <summary>Правдоподобие обратного сценария в стандартных отклонениях.</summary>
    public double ReverseStressDistance { get; init; }

    /// <summary>Названия факторов риска.</summary>
    public IReadOnlyList<string> Factors { get; init; } = [];

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        StressScenario? worst = Scenarios.FirstOrDefault();
        double ratio = ValueAtRisk > 0 ? WorstLoss / ValueAtRisk : 0;
        bool plausible = ReverseStressDistance < 3;

        var builder = new InterpretationBuilder($"Стресс-тестирование: {Portfolio}")
            .Summary($"Наихудший сценарий «{worst?.Name}» даёт потери " +
                     $"{Fmt.Pct(worst?.LossShare ?? 0, 2)} портфеля — в " +
                     $"{Fmt.Num(ratio, 1)} раза больше стоимости под риском. " +
                     $"Обратный стресс-тест: потери {Fmt.Pct(ReverseStressTarget, 0)} " +
                     $"достигаются комбинацией шоков на расстоянии " +
                     $"{Fmt.Num(ReverseStressDistance, 2)} стандартных отклонений — " +
                     $"такое сочетание {(plausible ? "правдоподобно" : "маловероятно")}.")
            .Metric("Наихудшие потери", WorstLoss, null,
                $"сценарий «{worst?.Name}»", MetricQuality.Warning, 4)
            .Metric("Отношение к стоимости под риском", ratio, "×",
                "во сколько раз стресс превышает статистическую оценку",
                ratio > 3 ? MetricQuality.Critical : MetricQuality.Warning, 2)
            .Metric("Расстояние обратного сценария", ReverseStressDistance, "σ",
                plausible ? "сценарий реалистичен" : "сценарий за пределами наблюдаемого",
                plausible ? MetricQuality.Critical : MetricQuality.Neutral, 2);

        foreach (StressScenario scenario in Scenarios)
        {
            builder.Metric(scenario.Name, scenario.LossShare, null,
                $"потери {Fmt.Money(scenario.Loss)}; шоки " +
                string.Join(", ", scenario.Shocks.Select(s => Fmt.Pct(s, 0))),
                MetricQuality.Unknown, 4);
        }

        for (int i = 0; i < ReverseStressShocks.Count && i < Factors.Count; i++)
        {
            builder.Metric($"Обратный шок: {Factors[i]}", ReverseStressShocks[i], null,
                "изменение фактора, приводящее к целевым потерям", MetricQuality.Unknown, 3);
        }

        return builder
            .Finding("Стресс-тест отвечает на вопрос, который статистическая оценка риска " +
                     "не решает: что произойдёт в сценарии, которого в истории не было. " +
                     "Он не даёт вероятности, но задаёт масштаб потерь.")
            .FindingIf(ratio > 2,
                $"Стрессовые потери превышают стоимость под риском в {Fmt.Num(ratio, 1)} раза. " +
                "Разрыв между этими двумя числами и есть мера того, насколько " +
                "статистическая модель полагается на спокойный период выборки.")
            .Finding("Обратный стресс-тест переворачивает вопрос: не «сколько мы потеряем " +
                     "в таком-то сценарии», а «какой сценарий уничтожит заданную часть " +
                     "капитала». Второй вопрос обычно оказывается полезнее.")
            .WarningIf(plausible,
                $"Комбинация шоков, приводящая к потере {Fmt.Pct(ReverseStressTarget, 0)}, " +
                $"лежит всего в {Fmt.Num(ReverseStressDistance, 2)} стандартных отклонениях " +
                "от нормы. Такое сочетание вполне возможно, и к нему нужно готовиться.")
            .Warning("Сценарии задаются вручную и отражают воображение аналитика. " +
                     "Систематический пропуск — сценарии, в которых нарушаются " +
                     "исторические корреляции между факторами.")
            .Recommendation("Включайте в набор исторические кризисы целиком, а не только " +
                            "отдельные шоки: одновременное движение всех факторов " +
                            "и есть суть кризиса.")
            .Recommendation("Используйте обратный стресс-тест для выбора лимитов: он " +
                            "переводит абстрактный аппетит к риску в конкретные " +
                            "пороги по факторам.")
            .Build();
    }
}

/// <summary>
/// Обратное тестирование моделей риска и стресс-тестирование портфеля.
/// </summary>
/// <remarks>
/// <para>
/// Модель риска проверяется двумя независимыми условиями. Безусловное покрытие
/// требует, чтобы доля пробоев порога совпадала с заявленной, — это проверяет
/// тест Купца отношением правдоподобий:
/// </para>
/// <code>
/// LR_uc = -2 * ln( (1-p)^(n-x) * p^x / ((1-x/n)^(n-x) * (x/n)^x ) ~ chi2(1)
/// </code>
/// <para>
/// Независимость требует, чтобы пробои не шли подряд. Тест Кристофферсена
/// сравнивает марковскую цепь первого порядка с независимой схемой; их сумма
/// даёт совместный тест условного покрытия с двумя степенями свободы.
/// </para>
/// <para>
/// Стресс-тест дополняет статистическую оценку сценариями, которых в выборке
/// не было. Обратный стресс-тест решает задачу в противоположную сторону: ищет
/// минимальную по правдоподобию комбинацию шоков, приводящую к заданным
/// потерям. Расстояние этой комбинации от нормы в стандартных отклонениях
/// и есть мера её реалистичности.
/// </para>
/// </remarks>
public static class VarBacktesting
{
    /// <summary>Проводит обратное тестирование модели риска.</summary>
    /// <param name="returns">Фактические доходности.</param>
    /// <param name="varForecasts">Прогнозы стоимости под риском в положительных величинах.</param>
    /// <param name="confidence">Заявленный уровень доверия.</param>
    /// <param name="model">Название модели.</param>
    /// <returns>Результаты тестов Купца и Кристофферсена с зоной надзора.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Длины рядов не совпадают.</exception>
    public static BacktestVarResult Backtest(
        Vector returns, Vector varForecasts, double confidence = 0.99, string model = "модель")
    {
        ArgumentNullException.ThrowIfNull(returns);
        ArgumentNullException.ThrowIfNull(varForecasts);

        if (returns.Count != varForecasts.Count)
            throw new ArgumentException("Ряды должны быть одной длины.", nameof(varForecasts));
        if (returns.Count < 30)
            throw new ArgumentException("Нужно минимум тридцать наблюдений.", nameof(returns));

        int n = returns.Count;
        var breaches = new bool[n];
        int exceptions = 0;
        double severity = 0;

        for (int i = 0; i < n; i++)
        {
            breaches[i] = -returns[i] > varForecasts[i];

            if (!breaches[i]) continue;

            exceptions++;
            if (varForecasts[i] > 0) severity += -returns[i] / varForecasts[i];
        }

        double p = 1 - confidence;
        double kupiec = KupiecStatistic(n, exceptions, p);
        (double independence, double conditional) = ChristoffersenStatistics(breaches, kupiec);

        int longest = 0, current = 0;
        foreach (bool breach in breaches)
        {
            current = breach ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return new BacktestVarResult
        {
            Model = model,
            Confidence = confidence,
            Observations = n,
            Exceptions = exceptions,
            KupiecStatistic = kupiec,
            KupiecPValue = Distributions.ChiSquarePValue(kupiec, 1),
            IndependenceStatistic = independence,
            IndependencePValue = Distributions.ChiSquarePValue(independence, 1),
            ConditionalCoverageStatistic = conditional,
            ConditionalCoveragePValue = Distributions.ChiSquarePValue(conditional, 2),
            LongestExceptionRun = longest,
            AverageExceptionSeverity = exceptions > 0 ? severity / exceptions : 0,
            TrafficLight = TrafficLightZone(n, exceptions, confidence),
        };
    }

    /// <summary>Проводит стресс-тестирование портфеля по сценариям.</summary>
    /// <param name="exposures">Чувствительность портфеля к факторам риска.</param>
    /// <param name="factorVolatility">Волатильности факторов.</param>
    /// <param name="scenarios">Сценарии в виде шоков по факторам.</param>
    /// <param name="factors">Названия факторов.</param>
    /// <param name="valueAtRisk">Стоимость под риском для сравнения.</param>
    /// <param name="reverseTarget">Целевые потери обратного стресс-теста.</param>
    /// <param name="portfolio">Название портфеля.</param>
    /// <returns>Потери по сценариям и обратный стресс-сценарий.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Размерности несогласованы.</exception>
    public static StressTestResult StressTest(
        Vector exposures, Vector factorVolatility,
        IReadOnlyList<(string Name, Vector Shocks)> scenarios,
        IReadOnlyList<string> factors, double valueAtRisk = 0,
        double reverseTarget = 0.2, string portfolio = "портфель")
    {
        ArgumentNullException.ThrowIfNull(exposures);
        ArgumentNullException.ThrowIfNull(factorVolatility);
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(factors);

        if (exposures.Count != factorVolatility.Count)
            throw new ArgumentException("Чувствительности и волатильности должны совпадать по длине.",
                nameof(factorVolatility));

        var results = new List<StressScenario>(scenarios.Count);

        foreach ((string name, Vector shocks) in scenarios)
        {
            double loss = 0;
            for (int j = 0; j < exposures.Count && j < shocks.Count; j++)
                loss -= exposures[j] * shocks[j];

            results.Add(new StressScenario(name, [.. shocks], loss, loss));
        }

        // Обратный стресс-тест: наиболее правдоподобная комбинация шоков,
        // дающая целевые потери. Решение — шок вдоль вектора чувствительностей,
        // взвешенного волатильностями факторов
        double norm = 0;
        for (int j = 0; j < exposures.Count; j++)
            norm += exposures[j] * exposures[j] * factorVolatility[j] * factorVolatility[j];

        var reverse = new List<double>(exposures.Count);
        double distance = 0;

        if (norm > 1e-18)
        {
            double lambda = -reverseTarget / norm;

            for (int j = 0; j < exposures.Count; j++)
            {
                double shock = lambda * exposures[j] * factorVolatility[j] * factorVolatility[j];
                reverse.Add(shock);

                if (factorVolatility[j] > 0)
                    distance += Math.Pow(shock / factorVolatility[j], 2);
            }

            distance = Math.Sqrt(distance);
        }

        return new StressTestResult
        {
            Portfolio = portfolio,
            Scenarios = [.. results.OrderByDescending(s => s.Loss)],
            WorstLoss = results.Count > 0 ? results.Max(s => s.Loss) : 0,
            ValueAtRisk = valueAtRisk,
            ReverseStressShocks = reverse,
            ReverseStressTarget = reverseTarget,
            ReverseStressDistance = distance,
            Factors = factors,
        };
    }

    /// <summary>Статистика теста Купца на безусловное покрытие.</summary>
    private static double KupiecStatistic(int n, int exceptions, double p)
    {
        if (exceptions == 0) return -2 * n * Math.Log(1 - p);
        if (exceptions == n) return -2 * n * Math.Log(p);

        double observed = (double)exceptions / n;

        double restricted = ((n - exceptions) * Math.Log(1 - p)) + (exceptions * Math.Log(p));
        double unrestricted = ((n - exceptions) * Math.Log(1 - observed)) + (exceptions * Math.Log(observed));

        return Math.Max(-2 * (restricted - unrestricted), 0);
    }

    /// <summary>Статистики независимости и условного покрытия по Кристофферсену.</summary>
    private static (double Independence, double Conditional) ChristoffersenStatistics(
        bool[] breaches, double kupiec)
    {
        int n00 = 0, n01 = 0, n10 = 0, n11 = 0;

        for (int i = 1; i < breaches.Length; i++)
        {
            if (!breaches[i - 1] && !breaches[i]) n00++;
            else if (!breaches[i - 1] && breaches[i]) n01++;
            else if (breaches[i - 1] && !breaches[i]) n10++;
            else n11++;
        }

        int total = n00 + n01 + n10 + n11;
        if (total == 0 || n01 + n11 == 0) return (0, kupiec);

        double pi = (double)(n01 + n11) / total;
        double pi0 = n00 + n01 > 0 ? (double)n01 / (n00 + n01) : 0;
        double pi1 = n10 + n11 > 0 ? (double)n11 / (n10 + n11) : 0;

        if (pi <= 0 || pi >= 1 || pi0 <= 0 || pi1 <= 0 || pi0 >= 1 || pi1 >= 1)
            return (0, kupiec);

        double restricted = ((n00 + n10) * Math.Log(1 - pi)) + ((n01 + n11) * Math.Log(pi));
        double unrestricted = (n00 * Math.Log(1 - pi0)) + (n01 * Math.Log(pi0))
            + (n10 * Math.Log(1 - pi1)) + (n11 * Math.Log(pi1));

        double independence = Math.Max(-2 * (restricted - unrestricted), 0);

        return (independence, kupiec + independence);
    }

    /// <summary>Зона банковского светофора по числу пробоев на годовом окне.</summary>
    private static string TrafficLightZone(int observations, int exceptions, double confidence)
    {
        // Пороги приведены к годовому окну из 250 наблюдений
        double scaled = observations > 0 ? exceptions * 250.0 / observations : 0;
        double expected = 250 * (1 - confidence);

        return scaled <= expected + (2 * Math.Sqrt(expected)) ? "зелёная"
            : scaled <= expected + (4 * Math.Sqrt(expected)) ? "жёлтая"
            : "красная";
    }
}
