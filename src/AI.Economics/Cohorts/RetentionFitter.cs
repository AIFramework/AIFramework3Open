using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;
using AI.Econometrics.Numerics;
using AI.Statistics;

namespace AI.Economics.Cohorts;

/// <summary>
/// Подгонка кривых удержания по когортным данным методом максимального
/// правдоподобия с доверительным интервалом на экстраполяцию хвоста.
/// </summary>
/// <remarks>
/// <para>
/// Зачем нужна подгонка вместо «среднего retention». Наблюдаемая доля
/// удержания почти никогда не постоянна: слабые клиенты отваливаются первыми,
/// оставшиеся живут дольше, и мгновенный отток монотонно падает. Модель
/// с постоянным оттоком, откалиброванная по первым месяцам, занижает
/// пожизненную ценность в разы; она же, откалиброванная по последним, —
/// завышает.
/// </para>
/// <para>
/// Функция правдоподобия — дискретная с правым цензурированием: клиенты,
/// ушедшие в период <c>t</c>, дают вклад <c>S(t-1) - S(t)</c>, а дожившие до
/// конца наблюдения — вклад <c>S(T)</c>. Это стандартная постановка
/// Fader — Hardie для sBG, применённая здесь ко всем четырём семействам,
/// поэтому их AIC сопоставим напрямую.
/// </para>
/// </remarks>
public static class RetentionFitter
{
    /// <summary>Число повторов параметрического бутстрапа по умолчанию.</summary>
    public const int DefaultBootstrapSamples = 200;

    /// <summary>
    /// Подгоняет кривую удержания по наблюдённому доживанию.
    /// </summary>
    /// <param name="observedSurvival">
    /// Наблюдённая доля доживания по возрастам когорты: <c>S(0) = 1</c>,
    /// далее <c>S(1), S(2), ...</c>.
    /// </param>
    /// <param name="cohortSize">Размер когорты — определяет точность и ширину интервала.</param>
    /// <param name="model">Семейство кривых.</param>
    /// <param name="horizon">
    /// Горизонт кривой в периодах. Если меньше числа наблюдений, берётся число наблюдений.
    /// </param>
    /// <param name="confidenceLevel">Уровень доверия интервала, по умолчанию 0,9.</param>
    /// <param name="bootstrapSamples">Число повторов бутстрапа; 0 — без интервалов.</param>
    /// <param name="seed">Зерно генератора для воспроизводимости.</param>
    /// <returns>Параметры, качество подгонки и экстраполяция с интервалом.</returns>
    /// <exception cref="ArgumentNullException">Кривая не задана.</exception>
    /// <exception cref="ArgumentException">Кривая короче двух точек.</exception>
    public static RetentionFitResult Fit(
        Vector observedSurvival,
        double cohortSize,
        RetentionModel model,
        int horizon = 36,
        double confidenceLevel = 0.9,
        int bootstrapSamples = DefaultBootstrapSamples,
        int seed = 42)
    {
        ArgumentNullException.ThrowIfNull(observedSurvival);
        if (observedSurvival.Count < 2)
            throw new ArgumentException("Нужно минимум два наблюдения удержания.", nameof(observedSurvival));

        double[] observed = Normalize(observedSurvival);
        int observedPeriods = observed.Length - 1;
        if (horizon < observedPeriods) horizon = observedPeriods;

        double n0 = Math.Max(cohortSize, 1.0);
        double[] counts = observed.Select(s => s * n0).ToArray();

        double[] p = FitCounts(counts, model);
        double[] survival = RetentionCurves.Survival(model, p, horizon);

        double ll = LogLikelihood(counts, model, p);
        double rmse = Rmse(observed, survival);
        double lifetime = survival.Sum();

        (double[] lower, double[] upper, double lifeLo, double lifeHi) =
            bootstrapSamples > 0
                ? Bootstrap(model, p, n0, observedPeriods, horizon, confidenceLevel, bootstrapSamples, seed)
                : (survival, survival, lifetime, lifetime);

        return new RetentionFitResult
        {
            Model = model,
            Parameters = p,
            ParameterNames = RetentionCurves.ParameterNames(model),
            LogLikelihood = ll,
            Aic = (2 * p.Length) - (2 * ll),
            Rmse = rmse,
            Survival = new Vector(survival),
            SurvivalLower = new Vector(lower),
            SurvivalUpper = new Vector(upper),
            RetentionRates = new Vector(RetentionCurves.RetentionRates(survival)),
            Observed = new Vector(observed),
            ObservedPeriods = observedPeriods,
            ExpectedLifetime = lifetime,
            ExpectedLifetimeLower = lifeLo,
            ExpectedLifetimeUpper = lifeHi,
            ConfidenceLevel = confidenceLevel,
        };
    }

    /// <summary>
    /// Подгоняет все четыре семейства и возвращает их, отсортированными по AIC.
    /// </summary>
    /// <param name="observedSurvival">Наблюдённая доля доживания.</param>
    /// <param name="cohortSize">Размер когорты.</param>
    /// <param name="horizon">Горизонт экстраполяции.</param>
    /// <param name="confidenceLevel">Уровень доверия интервала.</param>
    /// <param name="bootstrapSamples">Число повторов бутстрапа.</param>
    /// <param name="seed">Зерно генератора.</param>
    /// <returns>Список подгонок: первая — лучшая по AIC.</returns>
    public static IReadOnlyList<RetentionFitResult> FitAll(
        Vector observedSurvival,
        double cohortSize,
        int horizon = 36,
        double confidenceLevel = 0.9,
        int bootstrapSamples = DefaultBootstrapSamples,
        int seed = 42)
    {
        RetentionModel[] models =
        [
            RetentionModel.Exponential,
            RetentionModel.PowerLaw,
            RetentionModel.Weibull,
            RetentionModel.ShiftedBetaGeometric,
        ];

        return [.. models
            .Select(m => Fit(observedSurvival, cohortSize, m, horizon, confidenceLevel, bootstrapSamples, seed))
            .OrderBy(r => r.Aic)];
    }

    /// <summary>Оценка параметров по счётчикам доживших методом максимального правдоподобия.</summary>
    private static double[] FitCounts(double[] counts, RetentionModel model)
    {
        double[] observed = counts.Select(c => c / Math.Max(counts[0], 1e-9)).ToArray();
        double[] start = RetentionCurves.InitialGuess(model, observed);
        return NelderMead.MinimizePositive(p => -LogLikelihood(counts, model, p), start);
    }

    /// <summary>
    /// Логарифм правдоподобия дискретной модели дожития с правым цензурированием.
    /// </summary>
    private static double LogLikelihood(double[] counts, RetentionModel model, double[] p)
    {
        int last = counts.Length - 1;
        double[] s = RetentionCurves.Survival(model, p, last);
        double ll = 0;

        for (int t = 1; t <= last; t++)
        {
            double churned = counts[t - 1] - counts[t];
            if (churned <= 0) continue;

            double prob = s[t - 1] - s[t];
            if (prob <= 1e-300) return double.NegativeInfinity;
            ll += churned * Math.Log(prob);
        }

        if (counts[last] > 0)
        {
            if (s[last] <= 1e-300) return double.NegativeInfinity;
            ll += counts[last] * Math.Log(s[last]);
        }

        return ll;
    }

    /// <summary>
    /// Параметрический бутстрап: из подогнанной модели генерируются синтетические
    /// когорты того же размера, каждая переподгоняется, по разбросу кривых
    /// строится интервал. Так интервал честно отражает и размер когорты,
    /// и длину наблюдения — экстраполяция на 36-й месяц по 6 месяцам данных
    /// получает широкий коридор, а не иллюзию точности.
    /// </summary>
    private static (double[] Lower, double[] Upper, double LifeLo, double LifeHi) Bootstrap(
        RetentionModel model,
        double[] p,
        double cohortSize,
        int observedPeriods,
        int horizon,
        double confidenceLevel,
        int samples,
        int seed)
    {
        double[] baseCurve = RetentionCurves.Survival(model, p, observedPeriods);
        int n0 = (int)Math.Max(Math.Round(cohortSize), 2);
        Random rng = RandomEngine.Create(seed);

        var curves = new List<double[]>(samples);
        var lifetimes = new List<double>(samples);

        for (int b = 0; b < samples; b++)
        {
            // Каждому клиенту сопоставляется его собственный порог: так
            // синтетическая кривая доживания монотонна по построению
            var u = new double[n0];
            for (int i = 0; i < n0; i++) u[i] = rng.NextDouble();
            Array.Sort(u);

            var counts = new double[observedPeriods + 1];
            for (int t = 0; t <= observedPeriods; t++)
            {
                int alive = LowerBound(u, baseCurve[t]);
                counts[t] = alive;
            }

            if (counts[0] < 2) continue;

            double[] pb = FitCounts(counts, model);
            double[] curve = RetentionCurves.Survival(model, pb, horizon);
            curves.Add(curve);
            lifetimes.Add(curve.Sum());
        }

        double alpha = (1.0 - confidenceLevel) / 2.0;
        var lower = new double[horizon + 1];
        var upper = new double[horizon + 1];

        if (curves.Count == 0)
        {
            double[] fallback = RetentionCurves.Survival(model, p, horizon);
            return (fallback, fallback, fallback.Sum(), fallback.Sum());
        }

        var column = new double[curves.Count];
        for (int t = 0; t <= horizon; t++)
        {
            for (int b = 0; b < curves.Count; b++) column[b] = curves[b][t];
            Array.Sort(column);
            lower[t] = EconMath.Quantile(column, alpha);
            upper[t] = EconMath.Quantile(column, 1.0 - alpha);
        }

        double[] sortedLife = [.. lifetimes.OrderBy(v => v)];
        return (lower, upper,
            EconMath.Quantile(sortedLife, alpha),
            EconMath.Quantile(sortedLife, 1.0 - alpha));
    }

    /// <summary>Число элементов отсортированного массива, строго меньших порога.</summary>
    private static int LowerBound(double[] sorted, double value)
    {
        int lo = 0, hi = sorted.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (sorted[mid] < value) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private static double Rmse(double[] observed, double[] fitted)
    {
        double sum = 0;
        for (int t = 0; t < observed.Length; t++)
        {
            double d = observed[t] - fitted[t];
            sum += d * d;
        }
        return Math.Sqrt(sum / observed.Length);
    }

    /// <summary>Приводит наблюдения к невозрастающей кривой с <c>S(0) = 1</c>.</summary>
    private static double[] Normalize(Vector v)
    {
        var s = new double[v.Count];
        for (int i = 0; i < v.Count; i++) s[i] = v[i];

        if (s[0] > 0 && Math.Abs(s[0] - 1.0) > 1e-9)
        {
            double scale = s[0];
            for (int i = 0; i < s.Length; i++) s[i] /= scale;
        }

        s[0] = 1.0;
        for (int i = 1; i < s.Length; i++)
        {
            if (double.IsNaN(s[i]) || s[i] < 0) s[i] = 0;
            if (s[i] > s[i - 1]) s[i] = s[i - 1];
        }

        return s;
    }
}
