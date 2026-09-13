namespace AI.Microwave.Coverage;

/// <summary>
/// Пересчёт SINR в спектральную эффективность по ослабленной формуле Шеннона:
/// 0 ниже порога, min(α·log₂(1 + SINR), η_max) выше.
/// </summary>
/// <remarks>
/// Так 3GPP TR 36.942 (приложение A) аппроксимирует реальную адаптацию модуляции и кодирования: коэффициент α
/// учитывает накладные расходы и неидеальность кодов, порог — наименее устойчивую схему, потолок — самую плотную.
/// Параметры 36.942 для LTE: вниз α = 0,6, порог −10 дБ, потолок 4,4 бит/с/Гц (достигается около 22 дБ); вверх
/// α = 0,4, −10 дБ, 2,0 бит/с/Гц (около 15 дБ).
/// </remarks>
/// <param name="Attenuation">Коэффициент α</param>
/// <param name="MinimumSinrDb">Порог, ниже которого связи нет, дБ</param>
/// <param name="MaximumEfficiency">Потолок спектральной эффективности, бит/с/Гц</param>
public sealed record ThroughputMapping(double Attenuation, double MinimumSinrDb, double MaximumEfficiency)
{
    /// <summary>Идеальная ёмкость Шеннона без порога и потолка</summary>
    public static ThroughputMapping Shannon { get; } = new(1, double.NegativeInfinity, double.PositiveInfinity);

    /// <summary>LTE, нисходящий канал, TR 36.942</summary>
    public static ThroughputMapping Lte36942Downlink { get; } = new(0.6, -10, 4.4);

    /// <summary>LTE, восходящий канал, TR 36.942</summary>
    public static ThroughputMapping Lte36942Uplink { get; } = new(0.4, -10, 2.0);

    /// <summary>
    /// NR с 256-QAM: α и порог как у LTE в 36.942, потолок — наибольшая эффективность таблицы CQI 2 из TS 38.214,
    /// 7,4063 бит/с/Гц. Это допущение, а не норма стандарта
    /// </summary>
    public static ThroughputMapping Nr256Qam { get; } = new(0.6, -10, 7.4063);

    /// <summary>Спектральная эффективность, бит/с/Гц</summary>
    /// <param name="sinrDb">SINR, дБ</param>
    public double SpectralEfficiency(double sinrDb)
        => sinrDb < MinimumSinrDb ? 0 : Math.Min(Attenuation * Math.Log2(1 + Math.Pow(10, sinrDb / 10)), MaximumEfficiency);
}
