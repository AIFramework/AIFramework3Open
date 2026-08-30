using System;
using System.Collections.Generic;
using AI.Economics.Numerics;

using AI.Insights;

namespace AI.Economics.Clv;

/// <summary>
/// Модель BG/NBD (Fader, Hardie, Lee, 2005) — прогноз числа покупок клиента,
/// который может уйти незаметно, без расторжения договора.
/// </summary>
/// <remarks>
/// <para>
/// Постановка: e-commerce и маркетплейсы не знают, ушёл ли клиент, — они видят
/// только то, что покупок давно не было. Модель описывает это двумя
/// процессами: пока клиент «жив», он покупает пуассоновским потоком с
/// индивидуальной интенсивностью (гамма-распределение по популяции, параметры
/// <c>r</c>, <c>alpha</c>); после каждой покупки он с вероятностью <c>p</c>
/// уходит навсегда (бета-распределение по популяции, параметры <c>a</c>,
/// <c>b</c>).
/// </para>
/// <para>
/// Отсюда главный практический вывод: клиент с 20 покупками и молчанием
/// два месяца почти наверняка ушёл, а клиент с 2 покупками и тем же
/// молчанием, скорее всего, просто редко покупает. Средний отток такого
/// различия не делает.
/// </para>
/// </remarks>
public sealed partial class BgNbdModel : ITransactionModel
{
    /// <summary>Форма гамма-распределения интенсивности покупок.</summary>
    public double R { get; private set; } = 1;

    /// <summary>Масштаб гамма-распределения интенсивности покупок.</summary>
    public double Alpha { get; private set; } = 1;

    /// <summary>Первый параметр бета-распределения вероятности ухода.</summary>
    public double A { get; private set; } = 1;

    /// <summary>Второй параметр бета-распределения вероятности ухода.</summary>
    public double B { get; private set; } = 1;

    /// <summary>Логарифм правдоподобия в точке оптимума.</summary>
    public double LogLikelihood { get; private set; }

    /// <summary>Число клиентов, по которым обучена модель.</summary>
    public int SampleSize { get; private set; }

    /// <summary>Оценивает параметры модели методом максимального правдоподобия.</summary>
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
        A = best[2];
        B = best[3];
        LogLikelihood = TotalLogLikelihood(customers, R, Alpha, A, B);
        SampleSize = customers.Count;
    }

    /// <summary>
    /// Вероятность того, что клиент всё ещё «жив» — то есть совершит хотя бы
    /// одну покупку в будущем.
    /// </summary>
    /// <param name="customer">Сводка клиента.</param>
    /// <returns>Вероятность из отрезка [0; 1].</returns>
    /// <exception cref="ArgumentNullException">Клиент не задан.</exception>
    public double ProbabilityAlive(CustomerSummary customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        double x = customer.Frequency;
        if (x <= 0) return 1.0;

        double ratio = A / (B + x - 1.0)
                     * Math.Pow((Alpha + customer.Age) / (Alpha + customer.Recency), R + x);

        return 1.0 / (1.0 + ratio);
    }

    /// <summary>
    /// Ожидаемое число покупок клиента за ближайшие <paramref name="horizon"/>
    /// периодов с учётом его истории.
    /// </summary>
    /// <param name="customer">Сводка клиента.</param>
    /// <param name="horizon">Горизонт прогноза в тех же единицах времени.</param>
    /// <returns>Условное математическое ожидание числа покупок.</returns>
    /// <exception cref="ArgumentNullException">Клиент не задан.</exception>
    public double ExpectedTransactions(CustomerSummary customer, double horizon)
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (horizon <= 0) return 0;

        double x = customer.Frequency;
        double tx = customer.Recency;
        double t = customer.Age;

        double z = horizon / (Alpha + t + horizon);
        double hyp = EconMath.Hyp2F1(R + x, B + x, A + B + x - 1.0, z);

        double first = (A + B + x - 1.0) / (A - 1.0);
        double second = 1.0 - (Math.Pow((Alpha + t) / (Alpha + t + horizon), R + x) * hyp);

        double denominator = 1.0;
        if (x > 0)
            denominator += A / (B + x - 1.0) * Math.Pow((Alpha + t) / (Alpha + tx), R + x);

        double value = first * second / denominator;
        return double.IsNaN(value) || value < 0 ? 0 : value;
    }

    /// <summary>
    /// Ожидаемое число покупок «среднего» клиента популяции за время
    /// <paramref name="t"/> от момента первой покупки.
    /// </summary>
    /// <param name="t">Длительность интервала.</param>
    /// <returns>Безусловное математическое ожидание числа покупок.</returns>
    public double ExpectedTransactionsPopulation(double t)
    {
        if (t <= 0) return 0;

        double hyp = EconMath.Hyp2F1(R, B, A + B - 1.0, t / (Alpha + t));
        double value = (A + B - 1.0) / (A - 1.0) * (1.0 - (Math.Pow(Alpha / (Alpha + t), R) * hyp));
        return double.IsNaN(value) || value < 0 ? 0 : value;
    }

    /// <summary>Суммарный логарифм правдоподобия выборки.</summary>
    private static double TotalLogLikelihood(
        IReadOnlyList<CustomerSummary> customers, double r, double alpha, double a, double b)
    {
        double sum = 0;
        for (int i = 0; i < customers.Count; i++)
        {
            double ll = LogLikelihoodOne(customers[i], r, alpha, a, b);
            if (double.IsNaN(ll) || double.IsInfinity(ll)) return double.NegativeInfinity;
            sum += ll;
        }
        return sum;
    }

    /// <summary>
    /// Вклад одного клиента в логарифм правдоподобия. Второе слагаемое под
    /// логарифмом отвечает сценарию «клиент ушёл сразу после последней покупки»
    /// и существует только при <c>x &gt; 0</c>.
    /// </summary>
    private static double LogLikelihoodOne(
        CustomerSummary c, double r, double alpha, double a, double b)
    {
        double x = c.Frequency;
        double tx = c.Recency;
        double t = c.Age;

        double lnA = EconMath.LogGamma(r + x) - EconMath.LogGamma(r) + (r * Math.Log(alpha));
        double lnB = EconMath.LogGamma(a + b) + EconMath.LogGamma(b + x)
                   - EconMath.LogGamma(b) - EconMath.LogGamma(a + b + x);

        double logTerm1 = -(r + x) * Math.Log(alpha + t);

        if (x <= 0) return lnA + lnB + logTerm1;

        double logTerm2 = Math.Log(a / (b + x - 1.0)) - ((r + x) * Math.Log(alpha + tx));

        // Стабильное сложение экспонент: показатели различаются на порядки
        double m = Math.Max(logTerm1, logTerm2);
        double mixed = m + Math.Log(Math.Exp(logTerm1 - m) + Math.Exp(logTerm2 - m));

        return lnA + lnB + mixed;
    }
}
