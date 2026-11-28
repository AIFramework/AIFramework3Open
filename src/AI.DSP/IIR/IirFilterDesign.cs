using AI.DataStructs.Algebraic;

namespace AI.DSP.IIR;

/// <summary>
/// Результат дизайна БИХ-фильтра: параметры, нули и полюсы цифрового фильтра, звенья второго порядка.
/// </summary>
/// <param name="Family">Тип прототипа.</param>
/// <param name="BandType">Вид полосы.</param>
/// <param name="Order">Порядок прототипа.</param>
/// <param name="SampleRate">Частота дискретизации, Гц.</param>
/// <param name="Cutoff">Частоты среза, Гц.</param>
/// <param name="ZeroPoleGain">Нули, полюсы и усиление цифрового фильтра.</param>
/// <param name="Sections">Звенья второго порядка.</param>
public sealed record IirFilterDesign(IirFamily Family, IirBandType BandType, int Order, double SampleRate, Vector Cutoff,
    ZeroPoleGain ZeroPoleGain, SecondOrderSections Sections)
{
    /// <summary>Фактическое выполнение ТЗ.</summary>
    public IirSpecificationReport Verify(IirSpecification specification) => Sections.Verify(specification);
}
