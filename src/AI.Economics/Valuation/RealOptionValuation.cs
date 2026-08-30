using System;
using AI.Economics.Numerics;

using AI.Insights;

namespace AI.Economics.Valuation;

/// <summary>Параметры оценки проекта методом реальных опционов.</summary>
public sealed record RealOptionInput
{
    /// <summary>Приведённая стоимость будущих денежных потоков проекта.</summary>
    public double ProjectValue { get; init; }

    /// <summary>Стоимость запуска — цена исполнения опциона.</summary>
    public double InvestmentCost { get; init; }

    /// <summary>Срок, в течение которого решение можно отложить, лет.</summary>
    public double YearsToDecision { get; init; } = 3;

    /// <summary>Волатильность стоимости проекта, годовая.</summary>
    public double Volatility { get; init; } = 0.5;

    /// <summary>Безрисковая ставка, годовая.</summary>
    public double RiskFreeRate { get; init; } = 0.08;

    /// <summary>
    /// Ежегодная «утечка стоимости»: доля, теряемая из-за того, что конкурент
    /// выйдет на рынок первым. Аналог дивидендной доходности.
    /// </summary>
    public double ValueLeakage { get; init; }

    /// <summary>Число шагов биномиального дерева.</summary>
    public int Steps { get; init; } = 200;
}

/// <summary>Результат оценки реального опциона.</summary>
public sealed partial record RealOptionResult
{
    /// <summary>Статическая чистая приведённая стоимость «делать сейчас или никогда».</summary>
    public double StaticNpv { get; init; }

    /// <summary>Стоимость опциона по формуле Блэка — Шоулза (европейский тип).</summary>
    public double BlackScholesValue { get; init; }

    /// <summary>Стоимость опциона по биномиальному дереву с правом досрочного запуска.</summary>
    public double BinomialValue { get; init; }

    /// <summary>
    /// Премия за гибкость: насколько право подождать дороже немедленного решения.
    /// </summary>
    public double FlexibilityPremium { get; init; }

    /// <summary>Чувствительность стоимости опциона к стоимости проекта.</summary>
    public double Delta { get; init; }

    /// <summary>Вероятность того, что проект будет запущен (риск-нейтральная).</summary>
    public double ExerciseProbability { get; init; }

    /// <summary>Пороговая стоимость проекта, при которой запуск сейчас оправдан.</summary>
    public double ImmediateExerciseThreshold { get; init; }
}

/// <summary>
/// Оценка проектов методом реальных опционов — для НИОКР и всего, что можно
/// отложить, расширить или свернуть.
/// </summary>
/// <remarks>
/// <para>
/// Классический NPV отвечает на вопрос «делать или не делать прямо сейчас»
/// и потому систематически недооценивает исследовательские проекты: он
/// игнорирует, что через год станет известно больше, а обязательства пока
/// не приняты. Проект с отрицательным NPV и высокой неопределённостью может
/// иметь положительную стоимость именно из-за права подождать.
/// </para>
/// <para>
/// Аналогия с финансовым опционом прямая: стоимость проекта — цена базового
/// актива, стоимость запуска — цена исполнения, срок принятия решения —
/// время до экспирации, неопределённость — волатильность. Отсюда и главное
/// ограничение метода: волатильность стоимости непубличного проекта
/// не наблюдаема и берётся из аналогов, поэтому результат чувствителен к
/// допущению, которое нечем проверить.
/// </para>
/// </remarks>
public static class RealOptionValuation
{
    /// <summary>Оценивает опцион на запуск проекта.</summary>
    /// <param name="input">Параметры проекта.</param>
    /// <returns>Стоимости по двум методам и премия за гибкость.</returns>
    /// <exception cref="ArgumentNullException">Параметры не заданы.</exception>
    /// <exception cref="ArgumentException">Некорректные параметры.</exception>
    public static RealOptionResult Evaluate(RealOptionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.YearsToDecision <= 0 || input.Volatility <= 0)
            throw new ArgumentException("Срок и волатильность должны быть положительными.", nameof(input));

        double s = input.ProjectValue;
        double k = input.InvestmentCost;
        double t = input.YearsToDecision;
        double sigma = input.Volatility;
        double r = input.RiskFreeRate;
        double q = input.ValueLeakage;

        double staticNpv = s - k;

        double sqrtT = Math.Sqrt(t);
        double d1 = ((Math.Log(s / k) + ((r - q + (0.5 * sigma * sigma)) * t)) / (sigma * sqrtT));
        double d2 = d1 - (sigma * sqrtT);

        double discountedS = s * Math.Exp(-q * t);
        double discountedK = k * Math.Exp(-r * t);

        double bs = (discountedS * EconMath.NormalCdf(d1)) - (discountedK * EconMath.NormalCdf(d2));
        double delta = Math.Exp(-q * t) * EconMath.NormalCdf(d1);

        double binomial = Binomial(s, k, t, sigma, r, q, Math.Max(input.Steps, 10));

        return new RealOptionResult
        {
            StaticNpv = staticNpv,
            BlackScholesValue = bs,
            BinomialValue = binomial,
            FlexibilityPremium = binomial - Math.Max(staticNpv, 0),
            Delta = delta,
            ExerciseProbability = EconMath.NormalCdf(d2),
            ImmediateExerciseThreshold = ThresholdValue(k, t, sigma, r, q, Math.Max(input.Steps, 10)),
        };
    }

    /// <summary>
    /// Биномиальное дерево Кокса — Росса — Рубинштейна с правом досрочного
    /// исполнения: запустить проект можно в любой момент до конца срока.
    /// </summary>
    private static double Binomial(double s, double k, double t, double sigma, double r, double q, int steps)
    {
        double dt = t / steps;
        double u = Math.Exp(sigma * Math.Sqrt(dt));
        double d = 1.0 / u;
        double disc = Math.Exp(-r * dt);
        double p = ((Math.Exp((r - q) * dt) - d) / (u - d));
        p = EconMath.Clamp(p, 0, 1);

        var values = new double[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            double price = s * Math.Pow(u, steps - i) * Math.Pow(d, i);
            values[i] = Math.Max(price - k, 0);
        }

        for (int step = steps - 1; step >= 0; step--)
        {
            for (int i = 0; i <= step; i++)
            {
                double hold = disc * ((p * values[i]) + ((1 - p) * values[i + 1]));
                double price = s * Math.Pow(u, step - i) * Math.Pow(d, i);
                values[i] = Math.Max(hold, price - k);
            }
        }

        return values[0];
    }

    /// <summary>
    /// Наименьшая стоимость проекта, при которой немедленный запуск не хуже
    /// ожидания. Найдена делением отрезка пополам по разнице «исполнить
    /// сейчас» и «держать опцион».
    /// </summary>
    private static double ThresholdValue(double k, double t, double sigma, double r, double q, int steps)
    {
        double lo = k, hi = k * 20;

        for (int i = 0; i < 60; i++)
        {
            double mid = 0.5 * (lo + hi);
            double option = Binomial(mid, k, t, sigma, r, q, Math.Min(steps, 80));
            if (option > mid - k + 1e-9) lo = mid;
            else hi = mid;
        }

        return 0.5 * (lo + hi);
    }
}
