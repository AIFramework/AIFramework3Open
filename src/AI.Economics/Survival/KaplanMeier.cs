using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Economics.Numerics;

using AI.Economics.Insights;

namespace AI.Economics.Survival;

/// <summary>
/// Непараметрическая оценка Каплана — Мейера: кривая доживания клиентов
/// с учётом цензурирования.
/// </summary>
/// <remarks>
/// Оценка не предполагает никакой формы кривой — в этом её сила и слабость.
/// Она честно показывает, что происходило на данных, но не экстраполируется
/// за горизонт наблюдений: для этого нужна параметрическая подгонка
/// (<see cref="AI.Economics.Cohorts.RetentionFitter"/>).
/// </remarks>
public sealed partial class KaplanMeier
{
    private double[] _times = [];
    private double[] _survival = [];
    private double[] _lower = [];
    private double[] _upper = [];
    private double[] _atRisk = [];
    private double[] _events = [];

    /// <summary>Моменты событий, по возрастанию.</summary>
    public Vector Times => new(_times);

    /// <summary>Оценка доли доживших в моменты событий.</summary>
    public Vector SurvivalCurve => new(_survival);

    /// <summary>Нижняя граница доверительного интервала (лог-логарифмическое преобразование).</summary>
    public Vector Lower => new(_lower);

    /// <summary>Верхняя граница доверительного интервала.</summary>
    public Vector Upper => new(_upper);

    /// <summary>Число клиентов под риском в каждый момент события.</summary>
    public Vector AtRisk => new(_atRisk);

    /// <summary>Число событий в каждый момент.</summary>
    public Vector Events => new(_events);

    /// <summary>Медианное время жизни; <c>NaN</c>, если кривая не опустилась до 0,5.</summary>
    public double MedianSurvivalTime { get; private set; } = double.NaN;

    /// <summary>Уровень доверия интервалов.</summary>
    public double ConfidenceLevel { get; private set; } = 0.95;

    /// <summary>Строит оценку по наблюдениям.</summary>
    /// <param name="data">Наблюдения дожития.</param>
    /// <param name="confidenceLevel">Уровень доверия интервалов.</param>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Данные пусты.</exception>
    public void Fit(IReadOnlyList<SurvivalRecord> data, double confidenceLevel = 0.95)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Count == 0) throw new ArgumentException("Пустая выборка.", nameof(data));

        ConfidenceLevel = confidenceLevel;
        double z = EconMath.NormalInv(1.0 - ((1.0 - confidenceLevel) / 2.0));

        List<SurvivalRecord> ordered = [.. data.OrderBy(r => r.Time)];
        double[] distinct = [.. ordered.Where(r => r.Event).Select(r => r.Time).Distinct().OrderBy(t => t)];

        var times = new List<double>(distinct.Length);
        var surv = new List<double>(distinct.Length);
        var lo = new List<double>(distinct.Length);
        var hi = new List<double>(distinct.Length);
        var risk = new List<double>(distinct.Length);
        var evt = new List<double>(distinct.Length);

        double s = 1.0;
        double greenwood = 0;

        foreach (double t in distinct)
        {
            int n = ordered.Count(r => r.Time >= t);
            int d = ordered.Count(r => r.Event && Math.Abs(r.Time - t) < 1e-12);
            if (n <= 0) continue;

            s *= 1.0 - ((double)d / n);
            if (n > d) greenwood += (double)d / (n * (double)(n - d));

            times.Add(t);
            surv.Add(s);
            risk.Add(n);
            evt.Add(d);

            // Лог-логарифмическое преобразование: интервал не выходит за [0; 1]
            if (s is > 0 and < 1 && greenwood > 0)
            {
                double lnS = Math.Log(s);
                double se = Math.Sqrt(greenwood) / Math.Abs(lnS);
                double half = z * se;
                lo.Add(Math.Pow(s, Math.Exp(half)));
                hi.Add(Math.Pow(s, Math.Exp(-half)));
            }
            else
            {
                lo.Add(s);
                hi.Add(s);
            }
        }

        _times = [.. times];
        _survival = [.. surv];
        _lower = [.. lo];
        _upper = [.. hi];
        _atRisk = [.. risk];
        _events = [.. evt];

        MedianSurvivalTime = double.NaN;
        for (int i = 0; i < _survival.Length; i++)
            if (_survival[i] <= 0.5) { MedianSurvivalTime = _times[i]; break; }
    }

    /// <summary>Значение кривой доживания в произвольный момент (ступенчатая функция).</summary>
    /// <param name="time">Момент времени.</param>
    /// <returns>Доля доживших.</returns>
    public double SurvivalAt(double time)
    {
        double s = 1.0;
        for (int i = 0; i < _times.Length && _times[i] <= time; i++) s = _survival[i];
        return s;
    }

    /// <summary>
    /// Ограниченное среднее время жизни — площадь под кривой до момента
    /// <paramref name="tau"/>. В отличие от медианы определено всегда.
    /// </summary>
    /// <param name="tau">Правая граница интегрирования.</param>
    /// <returns>Ожидаемое время жизни на отрезке [0; tau].</returns>
    public double RestrictedMeanSurvival(double tau)
    {
        double area = 0;
        double prevTime = 0;
        double prevS = 1.0;

        for (int i = 0; i < _times.Length && _times[i] <= tau; i++)
        {
            area += prevS * (_times[i] - prevTime);
            prevTime = _times[i];
            prevS = _survival[i];
        }

        return area + (prevS * Math.Max(tau - prevTime, 0));
    }

    /// <summary>
    /// Лог-ранговый критерий сравнения кривых двух групп.
    /// </summary>
    /// <param name="data">Наблюдения с заполненным полем <see cref="SurvivalRecord.Group"/>.</param>
    /// <param name="groupA">Метка первой группы.</param>
    /// <param name="groupB">Метка второй группы.</param>
    /// <returns>Статистика хи-квадрат с одной степенью свободы и p-значение.</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    public static (double ChiSquare, double PValue) LogRankTest(
        IReadOnlyList<SurvivalRecord> data, int groupA = 0, int groupB = 1)
    {
        ArgumentNullException.ThrowIfNull(data);

        List<SurvivalRecord> a = [.. data.Where(r => r.Group == groupA)];
        List<SurvivalRecord> b = [.. data.Where(r => r.Group == groupB)];
        if (a.Count == 0 || b.Count == 0) return (0, 1);

        double[] eventTimes = [.. data.Where(r => r.Event).Select(r => r.Time).Distinct().OrderBy(t => t)];

        double observedA = 0, expectedA = 0, variance = 0;

        foreach (double t in eventTimes)
        {
            int nA = a.Count(r => r.Time >= t);
            int nB = b.Count(r => r.Time >= t);
            int n = nA + nB;
            if (n <= 1) continue;

            int dA = a.Count(r => r.Event && Math.Abs(r.Time - t) < 1e-12);
            int dB = b.Count(r => r.Event && Math.Abs(r.Time - t) < 1e-12);
            int d = dA + dB;
            if (d == 0) continue;

            observedA += dA;
            expectedA += (double)d * nA / n;
            variance += (double)d * nA * nB * (n - d) / ((double)n * n * (n - 1));
        }

        if (variance <= 0) return (0, 1);

        double chi = (observedA - expectedA) * (observedA - expectedA) / variance;

        // Для одной степени свободы p = 2 (1 - Ф(sqrt(chi))) — точное выражение
        double p = 2.0 * (1.0 - EconMath.NormalCdf(Math.Sqrt(chi)));
        return (chi, EconMath.Clamp(p, 0, 1));
    }
}
