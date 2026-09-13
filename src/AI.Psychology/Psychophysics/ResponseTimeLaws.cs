using AI.Psychology.Internal;

namespace AI.Psychology.Psychophysics;

/// <summary>Линейный закон времени реакции, найденный по данным</summary>
/// <param name="Intercept">Свободный член a, с</param>
/// <param name="Slope">Наклон b, с на бит</param>
/// <param name="RSquared">Доля объяснённой дисперсии</param>
/// <param name="Count">Число точек</param>
public readonly record struct LinearLawFit(double Intercept, double Slope, double RSquared, int Count);

/// <summary>
/// Законы времени реакции и движения: Хика — Хаймана и Фиттса.
/// </summary>
/// <remarks>
/// <para>
/// Закон Хика — Хаймана: время выбора растёт линейно с количеством информации в стимуле,
/// RT = a + b·H, где H = Σ pᵢ·log₂(1/pᵢ) бит; для n равновероятных вариантов H = log₂ n. Хик (1952)
/// писал log₂(n + 1), учитывая ещё и неопределённость, будет ли стимул вообще; Хайман (1953) показал,
/// что при неравных вероятностях время следует именно энтропии.
/// </para>
/// <para>
/// Закон Фиттса: время прицельного движения MT = a + b·ID, где индекс трудности в формулировке
/// Шеннона (Маккензи, 1992) ID = log₂(D/W + 1). Пропускная способность ID/MT измеряется в битах в
/// секунду и позволяет сравнивать устройства ввода; по ISO 9241-9 ширина берётся эффективной — по
/// разбросу реальных точек попадания: We = 4,133·σ.
/// </para>
/// </remarks>
public static class ResponseTimeLaws
{
    /// <summary>Множитель эффективной ширины цели: √(2πe) ≈ 4,133</summary>
    public static readonly double EffectiveWidthFactor = Math.Sqrt(2 * Math.PI * Math.E);

    /// <summary>Количество информации в стимуле, бит: энтропия по основанию 2</summary>
    /// <param name="probabilities">Вероятности вариантов; в сумме единица</param>
    public static double InformationBits(IReadOnlyList<double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(probabilities);

        if (probabilities.Count == 0)
            throw new ArgumentException("Нужен хотя бы один вариант", nameof(probabilities));

        foreach (double p in probabilities)
            Numerics.RequireProbability(p, nameof(probabilities));

        if (Math.Abs(probabilities.Sum() - 1) > 1e-9)
            throw new ArgumentException("Вероятности вариантов должны в сумме давать единицу", nameof(probabilities));

        return probabilities.Where(p => p > 0).Sum(p => -p * Math.Log2(p));
    }

    /// <summary>Время выбора по закону Хика — Хаймана: a + b·H</summary>
    /// <param name="intercept">a, с</param>
    /// <param name="slope">b, с на бит</param>
    /// <param name="bits">Информация в стимуле H, бит</param>
    public static double ChoiceTime(double intercept, double slope, double bits)
    {
        Numerics.RequireFinite(intercept, nameof(intercept));
        Numerics.RequireFinite(slope, nameof(slope));

        if (!(bits >= 0) || double.IsInfinity(bits))
            throw new ArgumentOutOfRangeException(nameof(bits), "Информация — конечное неотрицательное число");

        return intercept + (slope * bits);
    }

    /// <summary>Индекс трудности Фиттса в формулировке Шеннона: log₂(D/W + 1), бит</summary>
    /// <param name="distance">Расстояние до цели D</param>
    /// <param name="width">Ширина цели W</param>
    public static double IndexOfDifficulty(double distance, double width)
    {
        if (!(distance >= 0) || double.IsInfinity(distance))
            throw new ArgumentOutOfRangeException(nameof(distance), "Расстояние — конечное неотрицательное число");

        Numerics.RequirePositive(width, nameof(width));

        return Math.Log2((distance / width) + 1);
    }

    /// <summary>Время движения по закону Фиттса: a + b·ID</summary>
    /// <param name="intercept">a, с</param>
    /// <param name="slope">b, с на бит</param>
    /// <param name="distance">Расстояние до цели</param>
    /// <param name="width">Ширина цели</param>
    public static double MovementTime(double intercept, double slope, double distance, double width)
        => ChoiceTime(intercept, slope, IndexOfDifficulty(distance, width));

    /// <summary>Эффективная ширина цели по разбросу точек попадания: We = 4,133·σ</summary>
    /// <param name="endpointStandardDeviation">СКО точек попадания вдоль направления движения</param>
    public static double EffectiveWidth(double endpointStandardDeviation)
    {
        Numerics.RequirePositive(endpointStandardDeviation, nameof(endpointStandardDeviation));

        return EffectiveWidthFactor * endpointStandardDeviation;
    }

    /// <summary>Пропускная способность Фиттса, бит/с: ID/MT</summary>
    /// <param name="distance">Расстояние до цели</param>
    /// <param name="width">Ширина цели, лучше эффективная</param>
    /// <param name="movementTime">Среднее время движения, с</param>
    public static double Throughput(double distance, double width, double movementTime)
    {
        Numerics.RequirePositive(movementTime, nameof(movementTime));

        return IndexOfDifficulty(distance, width) / movementTime;
    }

    /// <summary>
    /// Коэффициенты линейного закона по измерениям: время против информации или индекса трудности
    /// </summary>
    /// <param name="predictor">Бит информации или индекс трудности каждого условия</param>
    /// <param name="time">Среднее время в каждом условии, с</param>
    public static LinearLawFit Fit(IReadOnlyList<double> predictor, IReadOnlyList<double> time)
    {
        (double intercept, double slope, double r2) = Numerics.Line(predictor, time);

        return new LinearLawFit(intercept, slope, r2, predictor.Count);
    }
}
