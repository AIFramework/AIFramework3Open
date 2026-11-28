namespace AI.DSP.IIR;

/// <summary>
/// Тип аналогового прототипа БИХ-фильтра.
/// </summary>
public enum IirFamily
{
    /// <summary>Баттерворт: максимально плоская АЧХ.</summary>
    Butterworth,

    /// <summary>Чебышёв I рода: равноволновая полоса пропускания.</summary>
    ChebyshevI,

    /// <summary>Чебышёв II рода: равноволновая полоса задерживания.</summary>
    ChebyshevII,

    /// <summary>Эллиптический (Кауэр): равноволновые обе полосы, наименьший порядок.</summary>
    Elliptic,

    /// <summary>Бессель: максимально плоская групповая задержка.</summary>
    Bessel,
}
