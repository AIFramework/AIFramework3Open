using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Econometrics.Numerics;

using AI.Insights;

namespace AI.Economics.Survival;

/// <summary>Оценка одного коэффициента регрессии Кокса.</summary>
public sealed record CoxCoefficient
{
    /// <summary>Имя ковариаты.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Оценка коэффициента <c>beta</c>.</summary>
    public double Beta { get; init; }

    /// <summary>Стандартная ошибка оценки.</summary>
    public double StandardError { get; init; }

    /// <summary>
    /// Отношение рисков <c>exp(beta)</c>: во сколько раз меняется мгновенный
    /// риск оттока при росте признака на единицу.
    /// </summary>
    public double HazardRatio { get; init; }

    /// <summary>Нижняя граница доверительного интервала отношения рисков.</summary>
    public double HazardRatioLower { get; init; }

    /// <summary>Верхняя граница доверительного интервала отношения рисков.</summary>
    public double HazardRatioUpper { get; init; }

    /// <summary>Статистика Вальда <c>beta / SE</c>.</summary>
    public double ZScore { get; init; }

    /// <summary>Двустороннее p-значение.</summary>
    public double PValue { get; init; }
}

/// <summary>
/// Регрессия пропорциональных рисков Кокса: какие признаки клиента ускоряют
/// или замедляют его уход.
/// </summary>
/// <remarks>
/// <para>
/// Модель <c>h(t | x) = h0(t) exp(beta' x)</c>. Базовый риск <c>h0(t)</c>
/// остаётся непараметрическим — это и делает метод устойчивым: форму кривой
/// оттока угадывать не нужно, оцениваются только относительные эффекты.
/// </para>
/// <para>
/// Совпадающие моменты событий обрабатываются приближением Бреслоу.
/// Ответ на вопрос «кто уйдёт и когда» собирается из двух частей:
/// <see cref="Coefficients"/> объясняют, кто в группе риска, а
/// <see cref="PredictSurvival"/> даёт индивидуальную кривую дожития.
/// </para>
/// </remarks>
public sealed partial class CoxProportionalHazards
{
    /// <summary>Максимальное изменение коэффициента за одну итерацию Ньютона.</summary>
    private const double MaxNewtonStep = 2.0;

    private double[] _beta = [];
    private double[] _baselineTimes = [];
    private double[] _baselineCumulativeHazard = [];

    /// <summary>Оценки коэффициентов.</summary>
    public IReadOnlyList<CoxCoefficient> Coefficients { get; private set; } = [];

    /// <summary>Логарифм частичного правдоподобия в точке оптимума.</summary>
    public double LogPartialLikelihood { get; private set; }

    /// <summary>Индекс конкордации Харрелла — доля верно упорядоченных пар.</summary>
    public double ConcordanceIndex { get; private set; }

    /// <summary>Число итераций Ньютона до сходимости.</summary>
    public int Iterations { get; private set; }

    /// <summary>Моменты, в которых оценён базовый накопленный риск.</summary>
    public Vector BaselineTimes => new(_baselineTimes);

    /// <summary>Базовый накопленный риск по Бреслоу.</summary>
    public Vector BaselineCumulativeHazard => new(_baselineCumulativeHazard);

    /// <summary>Обучает модель методом Ньютона — Рафсона по частичному правдоподобию.</summary>
    /// <param name="data">Наблюдения с заполненными ковариатами.</param>
    /// <param name="covariateNames">Имена ковариат для отчёта.</param>
    /// <param name="maxIterations">Максимум итераций Ньютона.</param>
    /// <param name="tolerance">Порог сходимости по приросту правдоподобия.</param>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Нет ковариат или наблюдений.</exception>
    public void Fit(
        IReadOnlyList<SurvivalRecord> data,
        IReadOnlyList<string>? covariateNames = null,
        int maxIterations = 50,
        double tolerance = 1e-9)
    {
        ArgumentNullException.ThrowIfNull(data);

        List<SurvivalRecord> ordered = [.. data.Where(r => r.Covariates is { Count: > 0 }).OrderBy(r => r.Time)];
        if (ordered.Count == 0)
            throw new ArgumentException("Нужны наблюдения с ковариатами.", nameof(data));

        int p = ordered[0].Covariates!.Count;
        int n = ordered.Count;

        var x = new double[n, p];
        var time = new double[n];
        var evt = new bool[n];

        for (int i = 0; i < n; i++)
        {
            Vector cov = ordered[i].Covariates!;
            for (int j = 0; j < p; j++) x[i, j] = cov[j];
            time[i] = ordered[i].Time;
            evt[i] = ordered[i].Event;
        }

        var beta = new double[p];
        double previous = double.NegativeInfinity;
        double[,]? lastInverse = null;

        for (int iter = 1; iter <= maxIterations; iter++)
        {
            (double ll, double[] gradient, double[,] information) = Derivatives(x, time, evt, beta);

            double[,]? inv = EconMath.Inverse(information);
            if (inv is null) break;
            lastInverse = inv;

            var step = new double[p];
            for (int j = 0; j < p; j++)
                for (int k = 0; k < p; k++) step[j] += inv[j, k] * gradient[k];

            // Демпфирование шага. Без него на разделимых данных (когда ковариата
            // безошибочно предсказывает, кто уйдёт раньше) частичное
            // правдоподобие монотонно, оценка уходит в бесконечность и
            // стандартные ошибки теряют смысл. Ограничение шага оставляет
            // коэффициент большим, но конечным, и сохраняет знак эффекта
            for (int j = 0; j < p; j++)
                beta[j] += Math.Clamp(step[j], -MaxNewtonStep, MaxNewtonStep);

            Iterations = iter;
            if (Math.Abs(ll - previous) < tolerance) { previous = ll; break; }
            previous = ll;
        }

        _beta = beta;
        (LogPartialLikelihood, _, _) = Derivatives(x, time, evt, beta);

        var names = covariateNames is { Count: > 0 }
            ? covariateNames
            : Enumerable.Range(1, p).Select(i => $"x{i}").ToArray();

        var coefficients = new List<CoxCoefficient>(p);
        for (int j = 0; j < p; j++)
        {
            double se = lastInverse is null ? double.NaN : Math.Sqrt(Math.Max(lastInverse[j, j], 0));
            double z = se > 0 ? beta[j] / se : double.NaN;
            double pv = double.IsNaN(z) ? double.NaN : 2.0 * (1.0 - EconMath.NormalCdf(Math.Abs(z)));

            coefficients.Add(new CoxCoefficient
            {
                Name = j < names.Count ? names[j] : $"x{j + 1}",
                Beta = beta[j],
                StandardError = se,
                HazardRatio = Math.Exp(beta[j]),
                HazardRatioLower = Math.Exp(beta[j] - (1.959963985 * se)),
                HazardRatioUpper = Math.Exp(beta[j] + (1.959963985 * se)),
                ZScore = z,
                PValue = pv,
            });
        }

        Coefficients = coefficients;
        ComputeBaseline(x, time, evt, beta);
        ConcordanceIndex = Concordance(x, time, evt, beta);
    }

    /// <summary>Линейный предиктор <c>beta' x</c> — логарифм относительного риска.</summary>
    /// <param name="covariates">Ковариаты клиента.</param>
    /// <returns>Значение линейного предиктора.</returns>
    /// <exception cref="ArgumentNullException">Ковариаты не заданы.</exception>
    public double RiskScore(Vector covariates)
    {
        ArgumentNullException.ThrowIfNull(covariates);

        double s = 0;
        for (int j = 0; j < Math.Min(_beta.Length, covariates.Count); j++) s += _beta[j] * covariates[j];
        return s;
    }

    /// <summary>
    /// Индивидуальная кривая дожития <c>S(t | x) = exp(-H0(t) exp(beta' x))</c>.
    /// </summary>
    /// <param name="covariates">Ковариаты клиента.</param>
    /// <returns>Значения кривой в моменты <see cref="BaselineTimes"/>.</returns>
    public Vector PredictSurvival(Vector covariates)
    {
        double hr = Math.Exp(RiskScore(covariates));
        var s = new Vector(_baselineCumulativeHazard.Length);
        for (int i = 0; i < s.Count; i++) s[i] = Math.Exp(-_baselineCumulativeHazard[i] * hr);
        return s;
    }

    /// <summary>
    /// Логарифм частичного правдоподобия, его градиент и информационная матрица
    /// (взятая со знаком плюс, то есть минус гессиан).
    /// </summary>
    private static (double LogLikelihood, double[] Gradient, double[,] Information) Derivatives(
        double[,] x, double[] time, bool[] evt, double[] beta)
    {
        int n = time.Length;
        int p = beta.Length;

        double ll = 0;
        var gradient = new double[p];
        var information = new double[p, p];

        var eta = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = 0;
            for (int j = 0; j < p; j++) s += beta[j] * x[i, j];
            eta[i] = s;
        }

        int index = 0;
        while (index < n)
        {
            double t = time[index];
            int tieEnd = index;
            while (tieEnd < n && Math.Abs(time[tieEnd] - t) < 1e-12) tieEnd++;

            var deaths = new List<int>();
            for (int i = index; i < tieEnd; i++)
                if (evt[i]) deaths.Add(i);

            if (deaths.Count > 0)
            {
                // Множество риска: все, чьё время наблюдения не меньше текущего
                double sum0 = 0;
                var sum1 = new double[p];
                var sum2 = new double[p, p];

                for (int i = index; i < n; i++)
                {
                    double w = Math.Exp(eta[i]);
                    sum0 += w;
                    for (int j = 0; j < p; j++)
                    {
                        sum1[j] += w * x[i, j];
                        for (int k = 0; k < p; k++) sum2[j, k] += w * x[i, j] * x[i, k];
                    }
                }

                if (sum0 <= 0) { index = tieEnd; continue; }

                int d = deaths.Count;
                foreach (int i in deaths)
                {
                    ll += eta[i];
                    for (int j = 0; j < p; j++) gradient[j] += x[i, j];
                }

                ll -= d * Math.Log(sum0);

                for (int j = 0; j < p; j++)
                {
                    double mj = sum1[j] / sum0;
                    gradient[j] -= d * mj;

                    for (int k = 0; k < p; k++)
                        information[j, k] += d * ((sum2[j, k] / sum0) - (mj * (sum1[k] / sum0)));
                }
            }

            index = tieEnd;
        }

        return (ll, gradient, information);
    }

    /// <summary>Базовый накопленный риск по оценке Бреслоу.</summary>
    private void ComputeBaseline(double[,] x, double[] time, bool[] evt, double[] beta)
    {
        int n = time.Length;
        int p = beta.Length;

        var eta = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = 0;
            for (int j = 0; j < p; j++) s += beta[j] * x[i, j];
            eta[i] = Math.Exp(s);
        }

        var times = new List<double>();
        var hazard = new List<double>();
        double cumulative = 0;

        int index = 0;
        while (index < n)
        {
            double t = time[index];
            int tieEnd = index;
            int deaths = 0;
            while (tieEnd < n && Math.Abs(time[tieEnd] - t) < 1e-12)
            {
                if (evt[tieEnd]) deaths++;
                tieEnd++;
            }

            if (deaths > 0)
            {
                double riskSum = 0;
                for (int i = index; i < n; i++) riskSum += eta[i];
                if (riskSum > 0)
                {
                    cumulative += deaths / riskSum;
                    times.Add(t);
                    hazard.Add(cumulative);
                }
            }

            index = tieEnd;
        }

        _baselineTimes = [.. times];
        _baselineCumulativeHazard = [.. hazard];
    }

    /// <summary>
    /// Индекс конкордации: доля сравнимых пар, в которых клиент с большим
    /// риском ушёл раньше. 0,5 — предсказание не лучше монетки.
    /// </summary>
    private static double Concordance(double[,] x, double[] time, bool[] evt, double[] beta)
    {
        int n = time.Length;
        int p = beta.Length;

        var risk = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = 0;
            for (int j = 0; j < p; j++) s += beta[j] * x[i, j];
            risk[i] = s;
        }

        double concordant = 0, comparable = 0;

        for (int i = 0; i < n; i++)
        {
            if (!evt[i]) continue;
            for (int j = 0; j < n; j++)
            {
                if (i == j || time[j] <= time[i]) continue;

                comparable++;
                if (risk[i] > risk[j]) concordant++;
                else if (Math.Abs(risk[i] - risk[j]) < 1e-12) concordant += 0.5;
            }
        }

        return comparable > 0 ? concordant / comparable : double.NaN;
    }
}
