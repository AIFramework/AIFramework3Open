using AI.Microwave.Physics;

namespace AI.Microwave.Propagation;

/// <summary>
/// Потери радиосигнала на трассе, дБ: свободное пространство и эмпирическая модель Окумуры — Хаты
/// для сотовой связи.
/// </summary>
public static class PathLoss
{
    /// <summary>
    /// Потери в свободном пространстве: L = 20·lg(4πd/λ). Нижняя граница любых реальных потерь.
    /// </summary>
    /// <param name="frequencyHz">Частота, Гц</param>
    /// <param name="distanceM">Расстояние, м</param>
    public static double FreeSpaceDb(double frequencyHz, double distanceM)
    {
        Guard.RequirePositive(frequencyHz, nameof(frequencyHz));
        Guard.RequirePositive(distanceM, nameof(distanceM));

        return 20.0 * Math.Log10(4.0 * Math.PI * distanceM / MicrowavePhysics.Wavelength(frequencyHz));
    }

    /// <summary>
    /// Медианные потери по Окумуре — Хате (150–1500 МГц) и её продолжению COST-231 (1500–2000 МГц)
    /// </summary>
    /// <remarks>
    /// <para>
    /// Модель построена по измерениям Окумуры для расстояний 1–20 км, высоты базовой станции 30–200 м
    /// и абонента 1–10 м; за этими пределами результат — экстраполяция.
    /// </para>
    /// <para>
    /// Это медиана: реальные потери разбросаны вокруг неё логнормальным затенением, которое учитывает
    /// <see cref="LinkBudget.CoverageProbability"/>. Лес на трассе добавляется отдельно — <see cref="VegetationLoss"/>.
    /// </para>
    /// <para>
    /// COST-231 определена для города и пригорода, поэтому открытая местность выше 1500 МГц не поддерживается.
    /// </para>
    /// </remarks>
    /// <param name="frequencyHz">Частота, Гц, от 150 МГц до 2 ГГц</param>
    /// <param name="distanceM">Расстояние, м</param>
    /// <param name="baseHeightM">Высота антенны базовой станции, м</param>
    /// <param name="mobileHeightM">Высота антенны абонента, м</param>
    /// <param name="environment">Тип местности</param>
    public static double HataDb(double frequencyHz, double distanceM, double baseHeightM, double mobileHeightM, HataEnvironment environment)
    {
        double f = frequencyHz / 1e6;
        if (!(f >= 150 && f <= 2000))
            throw new ArgumentOutOfRangeException(nameof(frequencyHz), frequencyHz, "Модель Хаты — COST-231 определена на 150–2000 МГц");

        Guard.RequirePositive(distanceM, nameof(distanceM));
        Guard.RequirePositive(baseHeightM, nameof(baseHeightM));
        Guard.RequirePositive(mobileHeightM, nameof(mobileHeightM));

        double logF = Math.Log10(f);
        double logHb = Math.Log10(baseHeightM);
        double distanceTerm = (44.9 - (6.55 * logHb)) * Math.Log10(distanceM / 1000.0);

        if (f > 1500)
        {
            if (environment == HataEnvironment.Open)
                throw new NotSupportedException("COST-231 не определена для открытой местности: выше 1500 МГц модель применима к городу и пригороду");

            double metropolitan = environment == HataEnvironment.LargeCity ? 3.0 : 0.0;
            return 46.3 + (33.9 * logF) - (13.82 * logHb) - MediumCityMobileCorrection(logF, mobileHeightM) + distanceTerm + metropolitan;
        }

        double mobileCorrection = environment == HataEnvironment.LargeCity
            ? LargeCityMobileCorrection(f, mobileHeightM)
            : MediumCityMobileCorrection(logF, mobileHeightM);
        double urban = 69.55 + (26.16 * logF) - (13.82 * logHb) - mobileCorrection + distanceTerm;

        return environment switch
        {
            HataEnvironment.Suburban => urban - (2.0 * Math.Pow(Math.Log10(f / 28.0), 2)) - 5.4,
            HataEnvironment.Open => urban - (4.78 * logF * logF) + (18.33 * logF) - 40.94,
            _ => urban,
        };
    }

    /// <summary>Поправка на высоту абонента для небольшого и среднего города: a(hm) = (1.1·lg f − 0.7)·hm − (1.56·lg f − 0.8)</summary>
    private static double MediumCityMobileCorrection(double logF, double mobileHeightM) =>
        (((1.1 * logF) - 0.7) * mobileHeightM) - ((1.56 * logF) - 0.8);

    /// <summary>Поправка на высоту абонента для крупного города, отдельно до и после 300 МГц</summary>
    private static double LargeCityMobileCorrection(double frequencyMHz, double mobileHeightM) =>
        frequencyMHz <= 300
            ? (8.29 * Math.Pow(Math.Log10(1.54 * mobileHeightM), 2)) - 1.1
            : (3.2 * Math.Pow(Math.Log10(11.75 * mobileHeightM), 2)) - 4.97;
}
