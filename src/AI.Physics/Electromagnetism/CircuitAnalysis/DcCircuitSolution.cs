#nullable enable
using AI.DataStructs.Algebraic;

namespace AI.Physics.Electromagnetism.CircuitAnalysis;

/// <summary>
/// Установившийся режим цепи постоянного тока. Все ветви описаны в одном соглашении: ток ветви течет через элемент
/// от первого его узла ко второму, напряжение ветви есть потенциал первого узла минус потенциал второго,
/// а мощность V·I положительна, когда элемент потребляет энергию, и отрицательна, когда отдает ее в цепь
/// </summary>
/// <param name="NodeVoltages">Потенциалы узлов, В; узел 0 есть земля с нулевым потенциалом</param>
/// <param name="BranchCurrents">Токи ветвей в порядке добавления элементов, А</param>
/// <param name="BranchVoltages">Напряжения ветвей, В</param>
/// <param name="BranchPowers">Потребляемые мощности ветвей, Вт</param>
/// <param name="Kinds">Виды элементов в порядке добавления</param>
public sealed record DcCircuitSolution(
    Vector NodeVoltages,
    Vector BranchCurrents,
    Vector BranchVoltages,
    Vector BranchPowers,
    IReadOnlyList<DcElementKind> Kinds)
{
    /// <summary>Мощность, рассеянная всеми сопротивлениями, Вт</summary>
    public double DissipatedPower => Enumerable.Range(0, Kinds.Count)
        .Where(k => Kinds[k] == DcElementKind.Resistor)
        .Sum(k => BranchPowers[k]);

    /// <summary>Мощность, отданная источниками в цепь, Вт; в установившемся режиме равна рассеянной</summary>
    public double SuppliedPower => -Enumerable.Range(0, Kinds.Count)
        .Where(k => Kinds[k] is DcElementKind.VoltageSource or DcElementKind.CurrentSource)
        .Sum(k => BranchPowers[k]);
}
