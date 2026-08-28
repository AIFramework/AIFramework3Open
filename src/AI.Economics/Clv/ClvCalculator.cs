using System;
using System.Collections.Generic;
using System.Linq;

using AI.Economics.Insights;

namespace AI.Economics.Clv;

/// <summary>Пожизненная ценность одного клиента.</summary>
public sealed record CustomerClv
{
    /// <summary>Идентификатор клиента.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Вероятность того, что клиент активен.</summary>
    public double ProbabilityAlive { get; init; }

    /// <summary>Ожидаемое число покупок на горизонте.</summary>
    public double ExpectedTransactions { get; init; }

    /// <summary>Ожидаемый средний чек с поправкой на регрессию к среднему.</summary>
    public double ExpectedValue { get; init; }

    /// <summary>Дисконтированная пожизненная ценность по марже.</summary>
    public double Clv { get; init; }

    /// <summary>Недисконтированная пожизненная ценность по марже.</summary>
    public double UndiscountedClv { get; init; }
}

/// <summary>Сводка расчёта CLV по портфелю клиентов.</summary>
public sealed partial record ClvPortfolio
{
    /// <summary>Клиенты, отсортированные по убыванию CLV.</summary>
    public IReadOnlyList<CustomerClv> Customers { get; init; } = [];

    /// <summary>Суммарная дисконтированная ценность портфеля.</summary>
    public double TotalClv { get; init; }

    /// <summary>Средний CLV на клиента.</summary>
    public double MeanClv { get; init; }

    /// <summary>Доля суммарного CLV, приходящаяся на верхние 10 % клиентов.</summary>
    public double Top10PercentShare { get; init; }

    /// <summary>Средняя вероятность активности по портфелю.</summary>
    public double MeanProbabilityAlive { get; init; }
}

/// <summary>
/// Сборка пожизненной ценности из двух моделей: числа покупок и их размера.
/// </summary>
/// <remarks>
/// Разделение неслучайно. Частота покупок и средний чек управляются разными
/// механизмами и предсказываются разными моделями; их независимость —
/// то самое допущение Gamma-Gamma, которое надо проверять на данных.
/// Дисконтирование ведётся по шагам, а не разом на конец горизонта:
/// покупка на 24-м месяце стоит сегодня заметно меньше покупки на первом.
/// </remarks>
public static class ClvCalculator
{
    /// <summary>Считает CLV по портфелю клиентов.</summary>
    /// <param name="transactions">Модель числа покупок (BG/NBD либо Pareto/NBD).</param>
    /// <param name="monetary">Модель среднего чека.</param>
    /// <param name="customers">Сводки клиентов.</param>
    /// <param name="horizon">Горизонт прогноза в единицах времени модели.</param>
    /// <param name="steps">Число шагов дисконтирования на горизонте.</param>
    /// <param name="discountRatePerStep">Ставка дисконтирования на один шаг.</param>
    /// <param name="marginRate">Доля маржи в чеке, от 0 до 1.</param>
    /// <returns>Сводка портфеля с разбивкой по клиентам.</returns>
    /// <exception cref="ArgumentNullException">Не задана модель или список клиентов.</exception>
    public static ClvPortfolio Compute(
        ITransactionModel transactions,
        GammaGammaModel monetary,
        IReadOnlyList<CustomerSummary> customers,
        double horizon,
        int steps = 12,
        double discountRatePerStep = 0,
        double marginRate = 1.0)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(monetary);
        ArgumentNullException.ThrowIfNull(customers);
        if (steps < 1) steps = 1;

        var results = new List<CustomerClv>(customers.Count);

        foreach (CustomerSummary c in customers)
        {
            double value = monetary.ConditionalExpectedValue(c) * marginRate;
            double discounted = 0;
            double previous = 0;

            for (int k = 1; k <= steps; k++)
            {
                double t = horizon * k / steps;
                double cumulative = transactions.ExpectedTransactions(c, t);
                double increment = Math.Max(cumulative - previous, 0);
                previous = cumulative;

                double df = discountRatePerStep > 0 ? Math.Pow(1.0 + discountRatePerStep, -k) : 1.0;
                discounted += increment * value * df;
            }

            results.Add(new CustomerClv
            {
                Id = c.Id,
                ProbabilityAlive = transactions.ProbabilityAlive(c),
                ExpectedTransactions = previous,
                ExpectedValue = monetary.ConditionalExpectedValue(c),
                Clv = discounted,
                UndiscountedClv = previous * value,
            });
        }

        List<CustomerClv> ordered = [.. results.OrderByDescending(r => r.Clv)];
        double total = ordered.Sum(r => r.Clv);
        int topCount = Math.Max(1, (int)Math.Round(ordered.Count * 0.1));
        double top = ordered.Take(topCount).Sum(r => r.Clv);

        return new ClvPortfolio
        {
            Customers = ordered,
            TotalClv = total,
            MeanClv = ordered.Count > 0 ? total / ordered.Count : 0,
            Top10PercentShare = total > 0 ? top / total : 0,
            MeanProbabilityAlive = ordered.Count > 0 ? ordered.Average(r => r.ProbabilityAlive) : 0,
        };
    }
}
