using System;
using System.Collections.Generic;
using System.Linq;
using AI.Econometrics.Numerics;

using AI.Insights;

namespace AI.Economics.Clv;

/// <summary>
/// Модель Gamma-Gamma: оценка среднего чека клиента с поправкой на регрессию
/// к среднему.
/// </summary>
/// <remarks>
/// <para>
/// Проблема, которую она решает: у клиента с одной покупкой на 50 000 рублей
/// наблюдённый средний чек равен 50 000, но это почти наверняка везение
/// выборки, а не свойство клиента. Модель смешивает индивидуальное среднее
/// с популяционным, причём вес индивидуального растёт с числом покупок.
/// </para>
/// <para>
/// Формально: чек клиента — гамма-величина с индивидуальным масштабом,
/// сам масштаб — тоже гамма по популяции. Апостериорное среднее выходит
/// линейной комбинацией <c>E[M | x, m] = p (gamma + x m) / (p x + q - 1)</c>.
/// </para>
/// <para>
/// Важно: модель предполагает независимость среднего чека и частоты покупок.
/// Перед применением стоит проверить корреляцию между <c>Frequency</c> и
/// <c>MonetaryValue</c> — на практике допустимой считают величину до 0,1.
/// </para>
/// </remarks>
public sealed partial class GammaGammaModel
{
    /// <summary>Форма гамма-распределения чека внутри клиента.</summary>
    public double P { get; private set; } = 1;

    /// <summary>Форма гамма-распределения масштаба по популяции.</summary>
    public double Q { get; private set; } = 2;

    /// <summary>Масштаб гамма-распределения по популяции.</summary>
    public double Gamma { get; private set; } = 1;

    /// <summary>Логарифм правдоподобия в точке оптимума.</summary>
    public double LogLikelihood { get; private set; }

    /// <summary>Число клиентов, по которым обучена модель.</summary>
    public int SampleSize { get; private set; }

    /// <summary>Средний чек по популяции: <c>p * gamma / (q - 1)</c>.</summary>
    public double PopulationMean => Q > 1 ? P * Gamma / (Q - 1.0) : double.NaN;

    /// <summary>
    /// Оценивает параметры по клиентам, совершившим хотя бы одну повторную покупку.
    /// </summary>
    /// <param name="customers">Сводки клиентов; отбираются с <c>Frequency &gt; 0</c>.</param>
    /// <exception cref="ArgumentNullException">Список клиентов не задан.</exception>
    /// <exception cref="ArgumentException">Нет клиентов с повторными покупками.</exception>
    public void Fit(IReadOnlyList<CustomerSummary> customers)
    {
        ArgumentNullException.ThrowIfNull(customers);

        List<CustomerSummary> usable =
            [.. customers.Where(c => c.Frequency > 0 && c.MonetaryValue > 0)];

        if (usable.Count == 0)
            throw new ArgumentException(
                "Нужны клиенты с повторными покупками и положительным чеком.", nameof(customers));

        double meanValue = usable.Average(c => c.MonetaryValue);

        double[] best = NelderMead.MinimizePositive(
            p => -TotalLogLikelihood(usable, p[0], p[1], p[2]),
            [1.0, 2.0, Math.Max(meanValue, 1.0)]);

        P = best[0];
        Q = best[1];
        Gamma = best[2];
        LogLikelihood = TotalLogLikelihood(usable, P, Q, Gamma);
        SampleSize = usable.Count;
    }

    /// <summary>
    /// Ожидаемый средний чек клиента с учётом регрессии к популяционному среднему.
    /// </summary>
    /// <param name="customer">Сводка клиента.</param>
    /// <returns>Условное ожидание среднего чека.</returns>
    /// <exception cref="ArgumentNullException">Клиент не задан.</exception>
    public double ConditionalExpectedValue(CustomerSummary customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        double x = customer.Frequency;
        double m = customer.MonetaryValue;
        double denominator = (P * x) + Q - 1.0;

        if (denominator <= 0) return m;
        return P * (Gamma + (x * m)) / denominator;
    }

    private static double TotalLogLikelihood(
        IReadOnlyList<CustomerSummary> customers, double p, double q, double gamma)
    {
        double sum = 0;
        for (int i = 0; i < customers.Count; i++)
        {
            double x = customers[i].Frequency;
            double m = customers[i].MonetaryValue;
            double px = p * x;

            double ll = EconMath.LogGamma(px + q) - EconMath.LogGamma(px) - EconMath.LogGamma(q)
                      + (q * Math.Log(gamma))
                      + ((px - 1.0) * Math.Log(m))
                      + (px * Math.Log(x))
                      - ((px + q) * Math.Log(gamma + (x * m)));

            if (double.IsNaN(ll) || double.IsInfinity(ll)) return double.NegativeInfinity;
            sum += ll;
        }
        return sum;
    }
}
