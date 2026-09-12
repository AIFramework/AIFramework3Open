using AI.DataStructs.Algebraic;

namespace AI.Physics.Acoustics;

/// <summary>
/// Октавные полосы 63 Гц — 8 кГц: средние частоты, коррекция А и порог слышимости.
/// Спектр шума задаётся вектором из восьми уровней, по одному на полосу.
/// </summary>
public static class OctaveBands
{
    /// <summary>Число полос</summary>
    public const int Count = 8;

    /// <summary>
    /// Точные средние частоты полос 1000·10^(3k/10), Гц; 63, 125, …, 8000 — их номинальные округления.
    /// По точным частотам ISO 9613-1 считает поглощение воздухом
    /// </summary>
    public static Vector MidbandFrequencies => new(Enumerable.Range(-4, Count).Select(k => 1000.0 * Math.Pow(10, 0.3 * k)));

    /// <summary>Коррекция А на средних частотах полос, дБ (МЭК 61672-1)</summary>
    public static Vector AWeighting => new(-26.2, -16.1, -8.6, -3.2, 0.0, 1.2, 1.0, -1.1);

    /// <summary>
    /// Порог слышимости в свободном поле на средних частотах полос, дБ (ISO 226). Это порог для тона;
    /// для шума в октавной полосе он близок, но не совпадает
    /// </summary>
    public static Vector HearingThreshold => new(37.5, 22.1, 11.4, 4.4, 2.4, -1.3, -5.4, 12.6);

    /// <summary>Энергетическая сумма уровней: 10·lg Σ 10^(L/10)</summary>
    /// <param name="levels">Уровни, дБ</param>
    public static double Sum(Vector levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        double energy = 0;
        foreach (double level in levels)
            energy += Math.Pow(10.0, level / 10.0);

        return 10.0 * Math.Log10(energy);
    }

    /// <summary>Уровень звука с коррекцией А по октавному спектру, дБА</summary>
    /// <param name="bandLevels">Уровни по восьми октавным полосам, дБ</param>
    public static double AWeightedLevel(Vector bandLevels) => Sum(RequireBands(bandLevels) + AWeighting);

    /// <summary>
    /// Запас слышимости, дБ: насколько сигнал в самой выгодной полосе превышает маскирующий фон вместе
    /// с порогом слышимости. Больше нуля — сигнал выделяется хотя бы в одной полосе
    /// </summary>
    /// <remarks>
    /// Октава шире критической полосы слуха, поэтому критерий грубый: тихий сигнал на широком фоне бывает
    /// слышен и при отрицательном запасе. В вероятностной модели запас разумно переводить в вероятность
    /// через разброс в несколько децибел.
    /// </remarks>
    /// <param name="signalLevels">Уровни сигнала по полосам, дБ</param>
    /// <param name="backgroundLevels">Уровни фона по полосам, дБ</param>
    public static double AudibilityMarginDb(Vector signalLevels, Vector backgroundLevels)
    {
        RequireBands(signalLevels);
        RequireBands(backgroundLevels);

        Vector threshold = HearingThreshold;
        double margin = double.NegativeInfinity;
        for (int band = 0; band < Count; band++)
        {
            double masking = Sum(new Vector(backgroundLevels[band], threshold[band]));
            margin = Math.Max(margin, signalLevels[band] - masking);
        }

        return margin;
    }

    /// <summary>Проверка, что уровни заданы по всем восьми полосам</summary>
    internal static Vector RequireBands(Vector levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Count != Count)
            throw new ArgumentException($"Ожидается {Count} октавных полос, получено {levels.Count}", nameof(levels));

        return levels;
    }
}
