using AI.Psychology.Internal;

namespace AI.Psychology.Psychophysics;

/// <summary>Оценка по нескольким признакам</summary>
/// <param name="Mean">Объединённая оценка</param>
/// <param name="StandardDeviation">Её СКО</param>
/// <param name="Weights">Вес каждого признака в порядке входа</param>
public sealed record CombinedEstimate(double Mean, double StandardDeviation, IReadOnlyList<double> Weights);

/// <summary>
/// Оптимальное объединение признаков: как мозг сводит зрение, осязание и слух в одну оценку.
/// </summary>
/// <remarks>
/// <para>
/// Если признаки независимы и шумят нормально, оценка с наименьшей дисперсией — среднее, взвешенное
/// обратно дисперсиям: wᵢ = (1/σᵢ²)/Σ(1/σⱼ²), и её дисперсия 1/Σ(1/σⱼ²) меньше дисперсии любого
/// признака. Эрнст и Бэнкс (2002) показали, что так люди сводят зрительную и осязательную оценку
/// высоты предмета: вес зрения падает ровно по мере того, как изображение зашумляют.
/// </para>
/// <para>
/// Априорное ожидание входит в ту же формулу как ещё один признак — это байесовская оценка с
/// нормальным априорным распределением. Если признаки коррелированы или один из них систематически
/// смещён, формула даёт слишком уверенную оценку.
/// </para>
/// </remarks>
public static class CueCombination
{
    /// <summary>Оптимальное объединение независимых признаков</summary>
    /// <param name="cues">Оценка и СКО каждого признака</param>
    public static CombinedEstimate Optimal(IReadOnlyList<(double Estimate, double StandardDeviation)> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);

        if (cues.Count == 0)
            throw new ArgumentException("Нужен хотя бы один признак", nameof(cues));

        double precision = 0, weighted = 0;

        foreach ((double estimate, double sd) in cues)
        {
            Numerics.RequireFinite(estimate, nameof(cues));
            Numerics.RequirePositive(sd, nameof(cues));

            precision += 1 / (sd * sd);
            weighted += estimate / (sd * sd);
        }

        double[] weights = cues.Select(c => 1 / (c.StandardDeviation * c.StandardDeviation) / precision).ToArray();

        return new CombinedEstimate(weighted / precision, Math.Sqrt(1 / precision), weights);
    }

    /// <summary>Байесовская оценка: признаки плюс нормальное априорное ожидание</summary>
    /// <param name="priorMean">Априорное среднее</param>
    /// <param name="priorStandardDeviation">Априорное СКО</param>
    /// <param name="cues">Признаки</param>
    /// <returns>Оценка; последний вес — вес априорного ожидания</returns>
    public static CombinedEstimate WithPrior(
        double priorMean, double priorStandardDeviation, IReadOnlyList<(double Estimate, double StandardDeviation)> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);

        return Optimal([.. cues, (priorMean, priorStandardDeviation)]);
    }
}
