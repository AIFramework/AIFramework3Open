using AI.Psychology.Internal;
using AI.Statistics;

namespace AI.Psychology.Psychometrics;

/// <summary>
/// Стандартные шкалы тестовых баллов: z, T, IQ, стэны, станайны и процентильные ранги.
/// </summary>
/// <remarks>
/// <para>
/// Сырой балл сам по себе ничего не значит: 30 из 40 — много или мало, зависит от теста и от того,
/// с кем сравнивать. Нормы переводят балл в положение внутри выборки стандартизации: z = (x − μ)/σ,
/// T = 50 + 10z, IQ = 100 + 15z. Стэны (1–10) и станайны (1–9) режут ось z на полосы по половине СКО:
/// у станайнов средняя полоса накрывает z ∈ [−0,25; 0,25), у стэнов середина шкалы 5,5 приходится
/// на z = 0.
/// </para>
/// <para>
/// Переход от z к процентилю через нормальное распределение верен только для нормально
/// распределённых баллов; для остальных процентильный ранг берут по самой выборке норм.
/// </para>
/// </remarks>
public static class StandardScores
{
    /// <summary>z-оценка: (x − μ)/σ</summary>
    /// <param name="raw">Сырой балл</param>
    /// <param name="mean">Среднее выборки норм</param>
    /// <param name="standardDeviation">СКО выборки норм</param>
    public static double ZScore(double raw, double mean, double standardDeviation)
    {
        Numerics.RequireFinite(raw, nameof(raw));
        Numerics.RequireFinite(mean, nameof(mean));
        Numerics.RequirePositive(standardDeviation, nameof(standardDeviation));

        return (raw - mean) / standardDeviation;
    }

    /// <summary>T-балл: 50 + 10z</summary>
    /// <param name="z">z-оценка</param>
    public static double TScore(double z) => 50 + (10 * z);

    /// <summary>Балл по шкале отклонений IQ: 100 + 15z</summary>
    /// <param name="z">z-оценка</param>
    public static double DeviationIq(double z) => 100 + (15 * z);

    /// <summary>Станайн 1–9: полосы по 0,5 СКО, средняя — z ∈ [−0,25; 0,25)</summary>
    /// <param name="z">z-оценка</param>
    public static int Stanine(double z)
    {
        Numerics.RequireFinite(z, nameof(z));

        return Math.Clamp((int)Math.Floor((2 * z) + 0.5) + 5, 1, 9);
    }

    /// <summary>Стэн 1–10: полосы по 0,5 СКО, стэн 6 начинается с z = 0</summary>
    /// <param name="z">z-оценка</param>
    public static int Sten(double z)
    {
        Numerics.RequireFinite(z, nameof(z));

        return Math.Clamp((int)Math.Floor(2 * z) + 6, 1, 10);
    }

    /// <summary>Процентиль при нормальном распределении баллов: 100·Φ(z)</summary>
    /// <param name="z">z-оценка</param>
    public static double PercentileFromZ(double z) => 100 * StatInference.NormalCdf(z);

    /// <summary>
    /// Процентильный ранг по выборке норм: доля баллов ниже данного плюс половина равных ему
    /// </summary>
    /// <param name="norms">Баллы выборки стандартизации</param>
    /// <param name="raw">Сырой балл</param>
    public static double PercentileRank(IReadOnlyList<double> norms, double raw)
    {
        ArgumentNullException.ThrowIfNull(norms);

        if (norms.Count == 0)
            throw new ArgumentException("Выборка норм пуста", nameof(norms));

        int below = norms.Count(x => x < raw);
        int equal = norms.Count(x => x == raw);

        return 100.0 * (below + (0.5 * equal)) / norms.Count;
    }
}
