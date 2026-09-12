namespace AI.Microwave.Propagation;

/// <summary>
/// Дополнительные потери радиосигнала в растительности — лес, лесополоса, сад, дБ.
/// </summary>
public static class VegetationLoss
{
    /// <summary>
    /// Модель ITU-R P.833 для трассы, один конец которой в лесу: A = Am·(1 − exp(−d·γ/Am))
    /// </summary>
    /// <remarks>
    /// Потери сначала растут линейно с глубиной леса, на γ дБ/м, а затем насыщаются на Am: сигнал перестаёт
    /// идти сквозь стволы и огибает кроны сверху. Поэтому километры леса не дают сотен децибел, как дала бы
    /// линейная модель. γ и Am зависят от частоты, породы и сезона и берутся из измерений или справочника;
    /// в вероятностной модели это как раз параметры с разбросом.
    /// </remarks>
    /// <param name="depthM">Путь сигнала внутри леса, м</param>
    /// <param name="specificAttenuationDbPerM">Удельное затухание γ на коротких путях, дБ/м</param>
    /// <param name="maxAttenuationDb">Предельные потери Am, дБ</param>
    public static double Itu833Db(double depthM, double specificAttenuationDbPerM, double maxAttenuationDb)
    {
        if (!(depthM >= 0) || double.IsInfinity(depthM))
            throw new ArgumentOutOfRangeException(nameof(depthM), depthM, "Глубина леса — конечное неотрицательное число");

        Guard.RequirePositive(specificAttenuationDbPerM, nameof(specificAttenuationDbPerM));
        Guard.RequirePositive(maxAttenuationDb, nameof(maxAttenuationDb));

        return maxAttenuationDb * (1.0 - Math.Exp(-depthM * specificAttenuationDbPerM / maxAttenuationDb));
    }

    /// <summary>
    /// Модель Вайсбергера (модифицированное экспоненциальное затухание) для густого леса с листвой:
    /// L = 0.45·f^0.284·d при d ≤ 14 м и 1.33·f^0.284·d^0.588 при 14 &lt; d ≤ 400 м, f в ГГц
    /// </summary>
    /// <remarks>
    /// Применима на 230 МГц — 95 ГГц и только до 400 м леса; насыщение потерь на больших глубинах
    /// описывает <see cref="Itu833Db"/>.
    /// </remarks>
    /// <param name="frequencyHz">Частота, Гц</param>
    /// <param name="depthM">Путь сигнала внутри леса, м</param>
    public static double WeissbergerDb(double frequencyHz, double depthM)
    {
        double frequencyGHz = frequencyHz / 1e9;
        if (!(frequencyGHz >= 0.23 && frequencyGHz <= 95))
            throw new ArgumentOutOfRangeException(nameof(frequencyHz), frequencyHz, "Модель Вайсбергера определена на 230 МГц — 95 ГГц");

        if (!(depthM >= 0 && depthM <= 400))
            throw new ArgumentOutOfRangeException(nameof(depthM), depthM, "Модель Вайсбергера определена для леса глубиной до 400 м");

        double frequencyFactor = Math.Pow(frequencyGHz, 0.284);
        return depthM <= 14 ? 0.45 * frequencyFactor * depthM : 1.33 * frequencyFactor * Math.Pow(depthM, 0.588);
    }
}
