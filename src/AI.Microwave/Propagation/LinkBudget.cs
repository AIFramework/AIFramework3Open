using AI.Statistics;
using AI.Units;

namespace AI.Microwave.Propagation;

/// <summary>
/// Бюджет радиолинии: принимаемая мощность и вероятность связи с учётом затенения.
/// </summary>
public static class LinkBudget
{
    /// <summary>
    /// Принимаемая мощность, дБм: ЭИИМ передатчика минус потери на трассе плюс усиление приёмной антенны.
    /// </summary>
    /// <param name="eirpDbm">ЭИИМ передатчика, дБм</param>
    /// <param name="pathLossDb">Суммарные потери на трассе, дБ</param>
    /// <param name="receiverGainDbi">Усиление приёмной антенны, дБи; у телефона около нуля</param>
    public static double ReceivedPowerDbm(double eirpDbm, double pathLossDb, double receiverGainDbi = 0.0) =>
        eirpDbm - pathLossDb + receiverGainDbi;

    /// <summary>
    /// Мощность теплового шума в полосе с учётом коэффициента шума приёмника, дБм: 10·lg(k·T·B / 1 мВт) + NF.
    /// При 290 К это −174 дБм/Гц + 10·lg B + NF
    /// </summary>
    /// <param name="bandwidthHz">Шумовая полоса, Гц</param>
    /// <param name="noiseFigureDb">Коэффициент шума приёмника, дБ</param>
    /// <param name="temperatureK">Шумовая температура, К; 290 К — стандартная</param>
    public static double ThermalNoiseDbm(double bandwidthHz, double noiseFigureDb = 0.0, double temperatureK = 290.0)
    {
        Guard.RequirePositive(bandwidthHz, nameof(bandwidthHz));
        Guard.RequirePositive(temperatureK, nameof(temperatureK));

        return (10.0 * Math.Log10(PhysicalConstants.BoltzmannConstant.SiValue * temperatureK * bandwidthHz * 1000.0)) + noiseFigureDb;
    }

    /// <summary>
    /// Вероятность, что сигнал выше чувствительности приёмника, при логнормальном затенении: P = Φ((Pr − S)/σ)
    /// </summary>
    /// <remarks>
    /// Затенение — случайное отклонение от медианы модели из-за рельефа и препятствий; в сельской местности
    /// его СКО обычно 6–8 дБ.
    /// </remarks>
    /// <param name="medianReceivedDbm">Медианная принимаемая мощность, дБм</param>
    /// <param name="sensitivityDbm">Чувствительность приёмника, дБм</param>
    /// <param name="shadowingSigmaDb">СКО затенения, дБ; 0 — без разброса</param>
    public static double CoverageProbability(double medianReceivedDbm, double sensitivityDbm, double shadowingSigmaDb)
    {
        if (!(shadowingSigmaDb >= 0) || double.IsInfinity(shadowingSigmaDb))
            throw new ArgumentOutOfRangeException(nameof(shadowingSigmaDb), shadowingSigmaDb, "СКО затенения — конечное неотрицательное число");

        if (shadowingSigmaDb == 0)
            return medianReceivedDbm >= sensitivityDbm ? 1.0 : 0.0;

        return StatInference.NormalCdf((medianReceivedDbm - sensitivityDbm) / shadowingSigmaDb);
    }
}
