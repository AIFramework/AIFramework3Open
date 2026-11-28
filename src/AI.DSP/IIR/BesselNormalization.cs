namespace AI.DSP.IIR;

/// <summary>
/// Нормировка частоты фильтра Бесселя (как norm в scipy.signal.bessel).
/// </summary>
public enum BesselNormalization
{
    /// <summary>Фаза: на частоте среза фазовый сдвиг равен половине предельного; асимптоты как у Баттерворта.</summary>
    Phase,

    /// <summary>Групповая задержка на нулевой частоте равна 1/ω_c.</summary>
    Delay,

    /// <summary>АЧХ на частоте среза равна −3 дБ.</summary>
    Magnitude,
}
