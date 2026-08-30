using System;
using System.Collections.Generic;
using AI.Econometrics.Numerics;

using AI.Insights;

namespace AI.Economics.Clv;

/// <summary>
/// Модель Pareto/NBD (Schmittlein, Morrison, Colombo, 1987) — «золотой
/// стандарт» прогноза покупок без контракта.
/// </summary>
/// <remarks>
/// <para>
/// Отличие от <see cref="BgNbdModel"/> в механизме ухода. В BG/NBD клиент
/// может уйти только сразу после покупки; в Pareto/NBD уход происходит в
/// непрерывном времени с индивидуальной интенсивностью (экспоненциальное
/// время жизни, гамма-распределение интенсивности по популяции с параметрами
/// <c>s</c> и <c>beta</c>).
/// </para>
/// <para>
/// Практическое следствие: Pareto/NBD аккуратнее описывает клиентов, которые
/// «замолчали» надолго, но формально не завершили серию покупок, и обычно
/// даёт более консервативную оценку вероятности их возвращения. Цена —
/// вычисления с гипергеометрической функцией и более капризная оптимизация.
/// </para>
/// </remarks>
public sealed partial class ParetoNbdModel : ITransactionModel
{
    /// <summary>Форма гамма-распределения интенсивности покупок.</summary>
    public double R { get; private set; } = 1;

    /// <summary>Масштаб гамма-распределения интенсивности покупок.</summary>
    public double Alpha { get; private set; } = 1;

    /// <summary>Форма гамма-распределения интенсивности ухода.</summary>
    public double S { get; private set; } = 1;

    /// <summary>Масштаб гамма-распределения интенсивности ухода.</summary>
    public double Beta { get; private set; } = 1;

    /// <summary>Логарифм правдоподобия в точке оптимума.</summary>
    public double LogLikelihood { get; private set; }

    /// <summary>Число клиентов, по которым обучена модель.</summary>
    public int SampleSize { get; private set; }

    /// <summary>Оценивает параметры методом максимального правдоподобия.</summary>
    /// <param name="customers">Сводки клиентов в формате RFM.</param>
    /// <exception cref="ArgumentNullException">Список клиентов не задан.</exception>
    /// <exception cref="ArgumentException">Список пуст.</exception>
    public void Fit(IReadOnlyList<CustomerSummary> customers)
    {
        ArgumentNullException.ThrowIfNull(customers);
        if (customers.Count == 0)
            throw new ArgumentException("Нужен хотя бы один клиент.", nameof(customers));

        double[] best = NelderMead.MinimizePositive(
            p => -TotalLogLikelihood(customers, p[0], p[1], p[2], p[3]),
            [1.0, 1.0, 1.0, 1.0]);

        R = best[0];
        Alpha = best[1];
        S = best[2];
        Beta = best[3];
        LogLikelihood = TotalLogLikelihood(customers, R, Alpha, S, Beta);
        SampleSize = customers.Count;
    }

    /// <summary>Вероятность того, что клиент всё ещё активен.</summary>
    /// <param name="customer">Сводка клиента.</param>
    /// <returns>Вероятность из отрезка [0; 1].</returns>
    /// <exception cref="ArgumentNullException">Клиент не задан.</exception>
    public double ProbabilityAlive(CustomerSummary customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        (double logAlive, double logTotal) = LogParts(customer, R, Alpha, S, Beta);
        double p = Math.Exp(logAlive - logTotal);
        return EconMath.Clamp(p, 0, 1);
    }

    /// <summary>
    /// Ожидаемое число покупок клиента за ближайшие <paramref name="horizon"/> периодов.
    /// </summary>
    /// <param name="customer">Сводка клиента.</param>
    /// <param name="horizon">Горизонт прогноза.</param>
    /// <returns>Условное математическое ожидание числа покупок.</returns>
    /// <exception cref="ArgumentNullException">Клиент не задан.</exception>
    public double ExpectedTransactions(CustomerSummary customer, double horizon)
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (horizon <= 0 || S <= 1) return 0;

        double t = customer.Age;
        double alive = ProbabilityAlive(customer);

        double factor = (R + customer.Frequency) * (Beta + t)
                      / ((Alpha + t) * (S - 1.0));
        double decay = 1.0 - Math.Pow((Beta + t) / (Beta + t + horizon), S - 1.0);

        double value = factor * decay * alive;
        return double.IsNaN(value) || value < 0 ? 0 : value;
    }

    private static double TotalLogLikelihood(
        IReadOnlyList<CustomerSummary> customers, double r, double alpha, double s, double beta)
    {
        double sum = 0;
        for (int i = 0; i < customers.Count; i++)
        {
            CustomerSummary c = customers[i];
            (_, double logTotal) = LogParts(c, r, alpha, s, beta);

            double ll = EconMath.LogGamma(r + c.Frequency) - EconMath.LogGamma(r)
                      + (r * Math.Log(alpha)) + (s * Math.Log(beta))
                      + logTotal;

            if (double.IsNaN(ll) || double.IsInfinity(ll)) return double.NegativeInfinity;
            sum += ll;
        }
        return sum;
    }

    /// <summary>
    /// Логарифмы двух составляющих правдоподобия: сценария «клиент жив» и
    /// полной суммы по обоим сценариям.
    /// </summary>
    /// <remarks>
    /// Вычисление ведётся в логарифмах со сдвигом на максимум: показатели
    /// степеней различаются на десятки порядков, и прямое возведение
    /// в степень переполняется уже на выборке в пару лет.
    /// </remarks>
    private static (double LogAlive, double LogTotal) LogParts(
        CustomerSummary c, double r, double alpha, double s, double beta)
    {
        double x = c.Frequency;
        double tx = c.Recency;
        double t = c.Age;
        double rsx = r + s + x;

        double logAlive = (-(r + x) * Math.Log(alpha + t)) - (s * Math.Log(beta + t));

        double logPart1, logPart2;
        if (alpha >= beta)
        {
            double z1 = (alpha - beta) / (alpha + tx);
            double z2 = (alpha - beta) / (alpha + t);
            logPart1 = Math.Log(EconMath.Hyp2F1(rsx, s + 1.0, rsx + 1.0, z1)) - (rsx * Math.Log(alpha + tx));
            logPart2 = Math.Log(EconMath.Hyp2F1(rsx, s + 1.0, rsx + 1.0, z2)) - (rsx * Math.Log(alpha + t));
        }
        else
        {
            double z1 = (beta - alpha) / (beta + tx);
            double z2 = (beta - alpha) / (beta + t);
            logPart1 = Math.Log(EconMath.Hyp2F1(rsx, r + x, rsx + 1.0, z1)) - (rsx * Math.Log(beta + tx));
            logPart2 = Math.Log(EconMath.Hyp2F1(rsx, r + x, rsx + 1.0, z2)) - (rsx * Math.Log(beta + t));
        }

        double shift = Math.Max(logAlive, logPart1);
        double a0 = Math.Exp(logPart1 - shift) - Math.Exp(logPart2 - shift);
        if (a0 < 0) a0 = 0;

        double total = Math.Exp(logAlive - shift) + (s / rsx * a0);
        if (total <= 0) return (logAlive, double.NegativeInfinity);

        return (logAlive, shift + Math.Log(total));
    }
}
