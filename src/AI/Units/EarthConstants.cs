#nullable enable
namespace AI.Units;

/// <summary>
/// Параметры Земли в единицах СИ: радиусы, фигура, масса.
/// </summary>
/// <remarks>
/// <para>
/// Одна таблица на геодезию, проекции, орбиты и распространение радиоволн. Значения — константы
/// времени компиляции, потому что чаще всего нужны как значение параметра по умолчанию.
/// </para>
/// <para>
/// Радиус Земли — не одно число: у эллипсоида две полуоси, а «средний радиус» зависит от того,
/// что усредняют. Здесь <see cref="MeanRadius"/> — среднее трёх полуосей по определению
/// Международного геодезического и геофизического союза. Стандарты, которые задают своё значение
/// (6371 км в рекомендациях МСЭ-R, 6 356 766 м в стандартной атмосфере 1976 года), держат его у
/// себя: это часть их формул, и подстановка другого радиуса дала бы расхождение с таблицами стандарта.
/// </para>
/// </remarks>
public static class EarthConstants
{
    /// <summary>Средний радиус R₁ = (2a + b)/3 эллипсоида GRS 80, м</summary>
    public const double MeanRadius = 6_371_008.8;

    /// <summary>Большая (экваториальная) полуось эллипсоида WGS 84, м</summary>
    public const double Wgs84SemiMajorAxis = 6_378_137.0;

    /// <summary>Обратное сжатие 1/f эллипсоида WGS 84</summary>
    public const double Wgs84InverseFlattening = 298.257223563;

    /// <summary>Большая полуось эллипсоида Красовского 1940 года, м</summary>
    public const double KrasovskySemiMajorAxis = 6_378_245.0;

    /// <summary>Обратное сжатие 1/f эллипсоида Красовского</summary>
    public const double KrasovskyInverseFlattening = 298.3;

    /// <summary>Масса Земли, кг</summary>
    public const double Mass = 5.9722e24;
}
