using AI.Psychology.Internal;

namespace AI.Psychology.Psychophysics;

/// <summary>Показатель степени закона Стивенса и масштаб, найденные по данным</summary>
/// <param name="Exponent">Показатель степени</param>
/// <param name="Scale">Масштабный множитель</param>
/// <param name="RSquared">Доля объяснённой дисперсии в двойных логарифмах</param>
public readonly record struct StevensFit(double Exponent, double Scale, double RSquared);

/// <summary>
/// Классические законы психофизики: связь физической силы стимула с ощущением.
/// </summary>
/// <remarks>
/// <para>
/// Закон Вебера: едва заметное различие пропорционально силе стимула, ΔI = k·I. Фехнер вывел из него
/// логарифмическую шкалу ощущения S = c·ln(I/I₀): равные доли Вебера дают равные приросты ощущения.
/// Стивенс показал прямым шкалированием, что ощущение растёт как степень, S = k·Iᵃ, с показателем,
/// своим для каждой модальности: громкость растёт медленнее силы звука, боль от тока — быстрее силы тока.
/// </para>
/// <para>
/// Закон Вебера нарушается у краёв диапазона: у порога и у предела различение хуже. Показатели Стивенса
/// получены на усреднённых оценках групп испытуемых при определённых условиях опыта и у отдельного
/// человека заметно разнятся.
/// </para>
/// </remarks>
public static class PsychophysicalLaws
{
    /// <summary>
    /// Типичные показатели Стивенса (Stevens, 1975) для нескольких модальностей
    /// </summary>
    public static IReadOnlyDictionary<string, double> StevensExponents { get; } = new Dictionary<string, double>
    {
        ["громкость, тон 3000 Гц"] = 0.67,
        ["яркость, пятно 5°, темнота"] = 0.33,
        ["длина линии"] = 1.0,
        ["площадь"] = 0.7,
        ["тяжесть поднимаемого груза"] = 1.45,
        ["удар током 60 Гц"] = 3.5
    };

    /// <summary>Едва заметное различие по закону Вебера: ΔI = k·I</summary>
    /// <param name="intensity">Сила стимула</param>
    /// <param name="weberFraction">Доля Вебера k</param>
    public static double JustNoticeableDifference(double intensity, double weberFraction)
    {
        Numerics.RequirePositive(intensity, nameof(intensity));
        Numerics.RequirePositive(weberFraction, nameof(weberFraction));

        return weberFraction * intensity;
    }

    /// <summary>
    /// Ощущение по Фехнеру: S = c·ln(I/I₀); ниже абсолютного порога ощущения нет
    /// </summary>
    /// <param name="intensity">Сила стимула</param>
    /// <param name="threshold">Абсолютный порог I₀</param>
    /// <param name="scale">Масштаб c</param>
    public static double FechnerSensation(double intensity, double threshold, double scale = 1)
    {
        Numerics.RequirePositive(intensity, nameof(intensity));
        Numerics.RequirePositive(threshold, nameof(threshold));
        Numerics.RequirePositive(scale, nameof(scale));

        return intensity <= threshold ? 0 : scale * Math.Log(intensity / threshold);
    }

    /// <summary>Ощущение по Стивенсу: S = k·Iᵃ</summary>
    /// <param name="intensity">Сила стимула</param>
    /// <param name="exponent">Показатель степени a</param>
    /// <param name="scale">Масштаб k</param>
    public static double StevensMagnitude(double intensity, double exponent, double scale = 1)
    {
        if (!(intensity >= 0) || double.IsInfinity(intensity))
            throw new ArgumentOutOfRangeException(nameof(intensity), "Сила стимула — конечное неотрицательное число");

        Numerics.RequirePositive(exponent, nameof(exponent));
        Numerics.RequirePositive(scale, nameof(scale));

        return scale * Math.Pow(intensity, exponent);
    }

    /// <summary>
    /// Показатель Стивенса по оценкам величины — прямая в двойных логарифмах
    /// </summary>
    /// <param name="intensities">Силы стимулов</param>
    /// <param name="magnitudes">Оценки величины ощущения, обычно геометрические средние по испытуемым</param>
    public static StevensFit FitStevens(IReadOnlyList<double> intensities, IReadOnlyList<double> magnitudes)
    {
        ArgumentNullException.ThrowIfNull(intensities);
        ArgumentNullException.ThrowIfNull(magnitudes);

        if (intensities.Any(i => !(i > 0)) || magnitudes.Any(m => !(m > 0)))
            throw new ArgumentException("Силы стимулов и оценки должны быть положительными: закон степенной");

        (double intercept, double slope, double r2) = Numerics.Line(
            intensities.Select(i => Math.Log(i)).ToArray(), magnitudes.Select(m => Math.Log(m)).ToArray());

        return new StevensFit(slope, Math.Exp(intercept), r2);
    }
}
