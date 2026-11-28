namespace AI.DSP.IIR;

/// <summary>
/// Вид полосы БИХ-фильтра.
/// </summary>
public enum IirBandType
{
    /// <summary>Фильтр нижних частот (ФНЧ).</summary>
    LowPass,

    /// <summary>Фильтр верхних частот (ФВЧ).</summary>
    HighPass,

    /// <summary>Полосовой фильтр (ПФ).</summary>
    BandPass,

    /// <summary>Режекторный фильтр (РФ).</summary>
    BandStop,
}
