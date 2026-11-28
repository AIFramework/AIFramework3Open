using AI.DataStructs.Algebraic;

namespace AI.DSP.IIR;

/// <summary>
/// Минимальный порядок фильтра по ТЗ и частоты среза для дизайна.
/// </summary>
/// <param name="Order">Порядок аналогового прототипа.</param>
/// <param name="Cutoff">Частота (ФНЧ, ФВЧ) или частоты (ПФ, РФ) среза, Гц.</param>
public sealed record IirOrderEstimate(int Order, Vector Cutoff);
