namespace AI.DSP.IIR;

/// <summary>
/// Фактическое выполнение ТЗ спроектированным фильтром.
/// </summary>
/// <param name="PassbandRippleDb">Наибольшее ослабление в полосе пропускания относительно максимума АЧХ, дБ.</param>
/// <param name="StopbandAttenuationDb">Наименьшее затухание в полосе задерживания относительно максимума АЧХ, дБ.</param>
/// <param name="PassbandMet">Требование к полосе пропускания выполнено.</param>
/// <param name="StopbandMet">Требование к полосе задерживания выполнено.</param>
public sealed record IirSpecificationReport(double PassbandRippleDb, double StopbandAttenuationDb, bool PassbandMet, bool StopbandMet)
{
    /// <summary>ТЗ выполнено целиком.</summary>
    public bool Satisfied => PassbandMet && StopbandMet;
}
