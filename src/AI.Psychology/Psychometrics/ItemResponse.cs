using AI.Insights;
using AI.Psychology.Internal;

namespace AI.Psychology.Psychometrics;

/// <summary>Задание теста в модели IRT</summary>
/// <param name="Discrimination">Дискриминативность a: насколько круто растёт вероятность верного ответа</param>
/// <param name="Difficulty">Трудность b: уровень способности, где вероятность посередине между c и 1</param>
/// <param name="Guessing">Угадывание c: вероятность верного ответа у самых слабых</param>
public sealed record IrtItem(double Discrimination = 1.0, double Difficulty = 0.0, double Guessing = 0.0)
{
    /// <summary>Вероятность верного ответа: c + (1 − c)/(1 + e^(−a(θ − b)))</summary>
    /// <param name="ability">Способность θ</param>
    public double Probability(double ability)
    {
        Validate();

        return Guessing + ((1 - Guessing) * Numerics.Logistic(Discrimination * (ability - Difficulty)));
    }

    /// <summary>Информация задания: a²·(1 − P)/P·((P − c)/(1 − c))²</summary>
    /// <param name="ability">Способность θ</param>
    public double Information(double ability)
    {
        double p = Probability(ability);

        if (p <= 0 || p >= 1)
            return 0;

        double lift = (p - Guessing) / (1 - Guessing);

        return Discrimination * Discrimination * (1 - p) / p * lift * lift;
    }

    internal double LogLikelihood(double ability, int response)
    {
        double z = Discrimination * (ability - Difficulty);

        if (Guessing == 0)
            return response == 1 ? -Numerics.Softplus(-z) : -Numerics.Softplus(z);

        return response == 1
            ? Math.Log(Guessing + ((1 - Guessing) * Numerics.Logistic(z)))
            : Math.Log(1 - Guessing) - Numerics.Softplus(z);
    }

    internal void Validate()
    {
        Numerics.RequirePositive(Discrimination, nameof(Discrimination));
        Numerics.RequireFinite(Difficulty, nameof(Difficulty));

        if (!(Guessing >= 0 && Guessing < 1))
            throw new ArgumentOutOfRangeException(nameof(Guessing), "Угадывание лежит на [0; 1)");
    }
}

/// <summary>Метод оценки способности</summary>
public enum AbilityMethod
{
    /// <summary>Максимальное правдоподобие: без априорных допущений, но бесконечно при всех верных или всех неверных ответах</summary>
    MaximumLikelihood,

    /// <summary>Апостериорное среднее при нормальном априорном распределении: всегда конечно, но смещено к среднему</summary>
    ExpectedAPosteriori
}

/// <summary>Оценка способности испытуемого</summary>
/// <param name="Ability">Оценка θ</param>
/// <param name="StandardError">Стандартная ошибка; для апостериорного среднего — апостериорное СКО</param>
/// <param name="Method">Метод</param>
/// <param name="IsFinite">Конечна ли оценка</param>
public readonly record struct AbilityEstimate(double Ability, double StandardError, AbilityMethod Method, bool IsFinite);

/// <summary>Модель калибровки заданий</summary>
public enum IrtModel
{
    /// <summary>Модель Раша: общая дискриминативность у всех заданий</summary>
    Rasch,

    /// <summary>Двухпараметрическая логистическая модель: своя дискриминативность у каждого задания</summary>
    TwoParameter
}

/// <summary>Результат калибровки заданий</summary>
public sealed class IrtCalibration : IInterpretable
{
    internal IrtCalibration(
        IrtModel model, IReadOnlyList<IrtItem> items, int respondents, double logLikelihood,
        int iterations, bool converged, IReadOnlyList<int> extremeItems)
    {
        Model = model;
        Items = items;
        Respondents = respondents;
        MarginalLogLikelihood = logLikelihood;
        Iterations = iterations;
        Converged = converged;
        ExtremeItems = extremeItems;
    }

    /// <summary>Модель</summary>
    public IrtModel Model { get; }

    /// <summary>Откалиброванные задания</summary>
    public IReadOnlyList<IrtItem> Items { get; }

    /// <summary>Число испытуемых</summary>
    public int Respondents { get; }

    /// <summary>Маргинальный логарифм правдоподобия</summary>
    public double MarginalLogLikelihood { get; }

    /// <summary>Число итераций EM</summary>
    public int Iterations { get; }

    /// <summary>Сошёлся ли EM</summary>
    public bool Converged { get; }

    /// <summary>Задания, на которые все ответили верно или все неверно: их трудность не оценивается</summary>
    public IReadOnlyList<int> ExtremeItems { get; }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        int weak = Items.Count(i => i.Discrimination < 0.5);
        int extreme = Items.Count(i => Math.Abs(i.Difficulty) > 3);

        var builder = new InterpretationBuilder("Калибровка заданий IRT")
            .Summary($"Модель {(Model == IrtModel.Rasch ? "Раша" : "2PL")}: заданий {Items.Count}, испытуемых {Respondents}, "
                + $"маргинальный логарифм правдоподобия {Fmt.Num(MarginalLogLikelihood, 1)} за {Iterations} итераций EM.")
            .Metric("Заданий", Items.Count, null, null, MetricQuality.Unknown, 0)
            .Metric("Испытуемых", Respondents, null, null, MetricQuality.Unknown, 0);

        foreach ((IrtItem item, int index) in Items.Select((item, index) => (item, index)).Take(12))
            builder = builder.Metric($"Задание {index + 1}", $"a = {Fmt.Num(item.Discrimination, 2)}, b = {Fmt.Num(item.Difficulty, 2)}", null, null);

        return builder
            .FindingIf(weak > 0, $"У {weak} заданий дискриминативность ниже 0,5: они плохо различают сильных и слабых.")
            .FindingIf(extreme > 0, $"У {extreme} заданий трудность за пределами ±3: они слишком лёгкие или трудные для этой выборки.")
            .WarningIf(ExtremeItems.Count > 0,
                $"На задания {string.Join(", ", ExtremeItems.Select(i => i + 1))} ответили одинаково все: их параметры ограничены и не оценены.")
            .WarningIf(!Converged, "EM не сошёлся за отведённое число итераций.")
            .Warning("Шкала способности закреплена нормальным распределением N(0, 1) в выборке калибровки: параметры разных "
                + "выборок сравнимы только после приравнивания шкал. Модель предполагает одномерность и локальную "
                + "независимость ответов; для 2PL нужны сотни испытуемых.")
            .Build();
    }
}

/// <summary>
/// Теория ответа на задание (IRT): вероятность верного ответа как функция способности и параметров задания.
/// </summary>
/// <remarks>
/// <para>
/// Классическая теория тестов описывает тест целиком; IRT — каждое задание: P(θ) = c + (1 − c)/(1 + e^(−a(θ − b))).
/// Модель Раша — частный случай с общей дискриминативностью и без угадывания, и в ней сумма верных ответов —
/// достаточная статистика: одинаковая сумма даёт одинаковую оценку способности, какие бы задания ни были решены.
/// </para>
/// <para>
/// Информация задания показывает, где на шкале оно измеряет точно: у 2PL её максимум a²/4 приходится на θ = b,
/// у 3PL сдвинут вверх. Информация теста — сумма по заданиям, ошибка оценки — 1/√I. На этом строится
/// адаптивное тестирование: следующим даётся задание, самое информативное при текущей оценке.
/// </para>
/// <para>
/// Калибровка — метод маргинального правдоподобия Бока и Эйткина: способность интегрируется по нормальному
/// распределению на сетке узлов, параметры заданий уточняются алгоритмом EM. Трёхпараметрическая модель
/// только вычисляется, но не калибруется: угадывание по одной выборке оценивается неустойчиво.
/// </para>
/// </remarks>
public static class ItemResponse
{
    /// <summary>Информация теста при данной способности: сумма информаций заданий</summary>
    /// <param name="items">Задания</param>
    /// <param name="ability">Способность θ</param>
    public static double TestInformation(IReadOnlyList<IrtItem> items, double ability)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items.Sum(i => i.Information(ability));
    }

    /// <summary>Оценка способности максимальным правдоподобием (скоринг Фишера)</summary>
    /// <param name="items">Задания с известными параметрами</param>
    /// <param name="responses">Ответы: 1 — верно, 0 — неверно</param>
    public static AbilityEstimate MaximumLikelihood(IReadOnlyList<IrtItem> items, IReadOnlyList<int> responses)
    {
        RequireResponses(items, responses);

        if (responses.All(r => r == 1))
            return new AbilityEstimate(double.PositiveInfinity, double.PositiveInfinity, AbilityMethod.MaximumLikelihood, false);

        if (responses.All(r => r == 0))
            return new AbilityEstimate(double.NegativeInfinity, double.PositiveInfinity, AbilityMethod.MaximumLikelihood, false);

        double theta = 0;

        for (int iteration = 0; iteration < 200; iteration++)
        {
            double score = 0, information = 0;

            for (int i = 0; i < items.Count; i++)
            {
                IrtItem item = items[i];
                double p = item.Probability(theta);
                score += item.Discrimination * (responses[i] - p) * (p - item.Guessing) / (p * (1 - item.Guessing));
                information += item.Information(theta);
            }

            double step = Math.Clamp(score / information, -1, 1);
            theta += step;

            if (Math.Abs(step) < 1e-10)
                break;
        }

        return new AbilityEstimate(theta, 1 / Math.Sqrt(TestInformation(items, theta)), AbilityMethod.MaximumLikelihood, true);
    }

    /// <summary>Апостериорное среднее способности при нормальном априорном распределении</summary>
    /// <param name="items">Задания с известными параметрами</param>
    /// <param name="responses">Ответы: 1 — верно, 0 — неверно</param>
    /// <param name="priorMean">Априорное среднее</param>
    /// <param name="priorStandardDeviation">Априорное СКО</param>
    /// <param name="nodes">Число узлов сетки на ±6 СКО</param>
    public static AbilityEstimate ExpectedAPosteriori(
        IReadOnlyList<IrtItem> items, IReadOnlyList<int> responses,
        double priorMean = 0, double priorStandardDeviation = 1, int nodes = 121)
    {
        RequireResponses(items, responses);
        Numerics.RequireFinite(priorMean, nameof(priorMean));
        Numerics.RequirePositive(priorStandardDeviation, nameof(priorStandardDeviation));
        ArgumentOutOfRangeException.ThrowIfLessThan(nodes, 21);

        var theta = new double[nodes];
        var log = new double[nodes];

        for (int k = 0; k < nodes; k++)
        {
            double standard = -6 + (12.0 * k / (nodes - 1));
            theta[k] = priorMean + (priorStandardDeviation * standard);
            log[k] = -standard * standard / 2;

            for (int i = 0; i < items.Count; i++)
                log[k] += items[i].LogLikelihood(theta[k], responses[i]);
        }

        double max = log.Max();
        double[] weight = log.Select(l => Math.Exp(l - max)).ToArray();
        double total = weight.Sum();
        double mean = 0;

        for (int k = 0; k < nodes; k++)
            mean += weight[k] * theta[k] / total;

        double variance = 0;

        for (int k = 0; k < nodes; k++)
            variance += weight[k] * (theta[k] - mean) * (theta[k] - mean) / total;

        return new AbilityEstimate(mean, Math.Sqrt(variance), AbilityMethod.ExpectedAPosteriori, true);
    }

    /// <summary>Моделирует ответы испытуемых с известной способностью</summary>
    /// <param name="items">Задания</param>
    /// <param name="abilities">Способности испытуемых</param>
    /// <param name="random">Генератор</param>
    /// <returns>Ответы: испытуемые по строкам, задания по столбцам</returns>
    public static int[,] Simulate(IReadOnlyList<IrtItem> items, IReadOnlyList<double> abilities, Random random)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(abilities);
        ArgumentNullException.ThrowIfNull(random);

        var responses = new int[abilities.Count, items.Count];

        for (int j = 0; j < abilities.Count; j++)
            for (int i = 0; i < items.Count; i++)
                responses[j, i] = random.NextDouble() < items[i].Probability(abilities[j]) ? 1 : 0;

        return responses;
    }

    /// <summary>Следующее задание адаптивного теста: самое информативное при текущей оценке</summary>
    /// <param name="bank">Банк заданий</param>
    /// <param name="administered">Номера уже данных заданий</param>
    /// <param name="ability">Текущая оценка способности</param>
    public static int SelectNextItem(IReadOnlyList<IrtItem> bank, IReadOnlySet<int> administered, double ability)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentNullException.ThrowIfNull(administered);

        int best = -1;
        double bestInformation = double.NegativeInfinity;

        for (int i = 0; i < bank.Count; i++)
        {
            if (administered.Contains(i))
                continue;

            double information = bank[i].Information(ability);

            if (information > bestInformation)
            {
                bestInformation = information;
                best = i;
            }
        }

        return best >= 0 ? best : throw new InvalidOperationException("Все задания банка уже даны");
    }

    /// <summary>Калибровка заданий методом маргинального правдоподобия (EM Бока — Эйткина)</summary>
    /// <param name="responses">Ответы: испытуемые по строкам, задания по столбцам, 0 или 1</param>
    /// <param name="model">Модель</param>
    /// <param name="maxIterations">Предел итераций EM</param>
    /// <param name="tolerance">Порог изменения параметров для остановки</param>
    /// <param name="quadratureNodes">Число узлов сетки способности на [−4; 4]</param>
    public static IrtCalibration Calibrate(
        int[,] responses, IrtModel model = IrtModel.TwoParameter,
        int maxIterations = 1000, double tolerance = 1e-5, int quadratureNodes = 41)
    {
        ArgumentNullException.ThrowIfNull(responses);
        ArgumentOutOfRangeException.ThrowIfLessThan(quadratureNodes, 11);

        int n = responses.GetLength(0), m = responses.GetLength(1);

        if (n < 10 || m < 2)
            throw new ArgumentException("Нужно хотя бы 10 испытуемых и 2 задания", nameof(responses));

        foreach (int response in responses)
        {
            if (response is not (0 or 1))
                throw new ArgumentException("Ответы должны быть 0 или 1", nameof(responses));
        }

        int nodes = quadratureNodes;
        var x = new double[nodes];
        var logPrior = new double[nodes];

        for (int k = 0; k < nodes; k++)
        {
            x[k] = -4 + (8.0 * k / (nodes - 1));
            logPrior[k] = -x[k] * x[k] / 2;
        }

        double priorNorm = Math.Log(logPrior.Sum(Math.Exp));

        for (int k = 0; k < nodes; k++)
            logPrior[k] -= priorNorm;

        var slope = Enumerable.Repeat(1.0, m).ToArray();
        var intercept = new double[m];
        var extreme = new List<int>();

        for (int i = 0; i < m; i++)
        {
            int correct = 0;

            for (int j = 0; j < n; j++)
                correct += responses[j, i];

            if (correct == 0 || correct == n)
                extreme.Add(i);

            double p = (correct + 0.5) / (n + 1.0);
            intercept[i] = Math.Log(p / (1 - p));
        }

        var logP = new double[m, nodes];
        var logQ = new double[m, nodes];
        var expectedCorrect = new double[m, nodes];
        var expectedCount = new double[nodes];
        var posterior = new double[nodes];
        double logLikelihood = 0;
        bool converged = false;
        int iterations = 0;

        while (iterations < maxIterations)
        {
            iterations++;

            for (int i = 0; i < m; i++)
            {
                for (int k = 0; k < nodes; k++)
                {
                    double z = (slope[i] * x[k]) + intercept[i];
                    logP[i, k] = -Numerics.Softplus(-z);
                    logQ[i, k] = -Numerics.Softplus(z);
                }
            }

            Array.Clear(expectedCorrect);
            Array.Clear(expectedCount);
            logLikelihood = 0;

            // Шаг E: апостериорное распределение способности каждого испытуемого на сетке
            for (int j = 0; j < n; j++)
            {
                double max = double.NegativeInfinity;

                for (int k = 0; k < nodes; k++)
                {
                    double value = logPrior[k];

                    for (int i = 0; i < m; i++)
                        value += responses[j, i] == 1 ? logP[i, k] : logQ[i, k];

                    posterior[k] = value;
                    max = Math.Max(max, value);
                }

                double sum = 0;

                for (int k = 0; k < nodes; k++)
                {
                    posterior[k] = Math.Exp(posterior[k] - max);
                    sum += posterior[k];
                }

                logLikelihood += max + Math.Log(sum);

                for (int k = 0; k < nodes; k++)
                {
                    double w = posterior[k] / sum;
                    expectedCount[k] += w;

                    for (int i = 0; i < m; i++)
                    {
                        if (responses[j, i] == 1)
                            expectedCorrect[i, k] += w;
                    }
                }
            }

            // Шаг M: параметры заданий по ожидаемым числам верных ответов в узлах
            double change = model == IrtModel.TwoParameter
                ? UpdateTwoParameter(slope, intercept, expectedCorrect, expectedCount, x)
                : UpdateRasch(slope, intercept, expectedCorrect, expectedCount, x);

            if (change < tolerance)
            {
                converged = true;
                break;
            }
        }

        IrtItem[] items = Enumerable.Range(0, m)
            .Select(i => new IrtItem(slope[i], -intercept[i] / slope[i]))
            .ToArray();

        return new IrtCalibration(model, items, n, logLikelihood, iterations, converged, extreme);
    }

    private static double UpdateTwoParameter(double[] slope, double[] intercept, double[,] correct, double[] count, double[] x)
    {
        double change = 0;

        for (int i = 0; i < slope.Length; i++)
        {
            double a = slope[i], d = intercept[i];

            for (int step = 0; step < 25; step++)
            {
                double ga = 0, gd = 0, haa = 0, had = 0, hdd = 0;

                for (int k = 0; k < x.Length; k++)
                {
                    double p = Numerics.Logistic((a * x[k]) + d);
                    double residual = correct[i, k] - (count[k] * p);
                    double w = count[k] * p * (1 - p);
                    ga += residual * x[k];
                    gd += residual;
                    haa += w * x[k] * x[k];
                    had += w * x[k];
                    hdd += w;
                }

                double determinant = (haa * hdd) - (had * had);

                if (determinant <= 1e-12)
                    break;

                double da = ((hdd * ga) - (had * gd)) / determinant;
                double dd = ((haa * gd) - (had * ga)) / determinant;
                double t = 1, before = ItemObjective(a, d, i, correct, count, x);

                while (t > 1e-4 && ItemObjective(a + (t * da), d + (t * dd), i, correct, count, x) < before - 1e-12)
                    t /= 2;

                double newA = Math.Clamp(a + (t * da), 0.02, 25);
                double newD = Math.Clamp(d + (t * dd), -40, 40);
                double moved = Math.Abs(newA - a) + Math.Abs(newD - d);
                a = newA;
                d = newD;

                if (moved < 1e-10)
                    break;
            }

            change = Math.Max(change, Math.Max(Math.Abs(a - slope[i]), Math.Abs(d - intercept[i])));
            slope[i] = a;
            intercept[i] = d;
        }

        return change;
    }

    private static double UpdateRasch(double[] slope, double[] intercept, double[,] correct, double[] count, double[] x)
    {
        double a = slope[0], change = 0;

        for (int i = 0; i < intercept.Length; i++)
        {
            double d = intercept[i];

            for (int step = 0; step < 25; step++)
            {
                double g = 0, h = 0;

                for (int k = 0; k < x.Length; k++)
                {
                    double p = Numerics.Logistic((a * x[k]) + d);
                    g += correct[i, k] - (count[k] * p);
                    h += count[k] * p * (1 - p);
                }

                if (h <= 1e-12)
                    break;

                double newD = Math.Clamp(d + (g / h), -40, 40);
                double moved = Math.Abs(newD - d);
                d = newD;

                if (moved < 1e-10)
                    break;
            }

            change = Math.Max(change, Math.Abs(d - intercept[i]));
            intercept[i] = d;
        }

        for (int step = 0; step < 25; step++)
        {
            double g = 0, h = 0;

            for (int i = 0; i < intercept.Length; i++)
            {
                for (int k = 0; k < x.Length; k++)
                {
                    double p = Numerics.Logistic((a * x[k]) + intercept[i]);
                    g += (correct[i, k] - (count[k] * p)) * x[k];
                    h += count[k] * p * (1 - p) * x[k] * x[k];
                }
            }

            if (h <= 1e-12)
                break;

            double newA = Math.Clamp(a + (g / h), 0.02, 25);
            double moved = Math.Abs(newA - a);
            a = newA;

            if (moved < 1e-10)
                break;
        }

        change = Math.Max(change, Math.Abs(a - slope[0]));
        Array.Fill(slope, a);

        return change;
    }

    private static double ItemObjective(double a, double d, int item, double[,] correct, double[] count, double[] x)
    {
        double value = 0;

        for (int k = 0; k < x.Length; k++)
        {
            double z = (a * x[k]) + d;
            value -= (correct[item, k] * Numerics.Softplus(-z)) + ((count[k] - correct[item, k]) * Numerics.Softplus(z));
        }

        return value;
    }

    private static void RequireResponses(IReadOnlyList<IrtItem> items, IReadOnlyList<int> responses)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(responses);

        if (items.Count == 0 || items.Count != responses.Count)
            throw new ArgumentException("Ответов должно быть столько же, сколько заданий, и хотя бы один", nameof(responses));

        foreach (IrtItem item in items)
            item.Validate();

        if (responses.Any(r => r is not (0 or 1)))
            throw new ArgumentException("Ответы должны быть 0 или 1", nameof(responses));
    }
}
