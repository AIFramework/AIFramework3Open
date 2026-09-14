#nullable enable
namespace AI.Physics.Electromagnetism.CircuitAnalysis;

/// <summary>Вид элемента цепи постоянного тока</summary>
public enum DcElementKind
{
    /// <summary>Сопротивление</summary>
    Resistor,

    /// <summary>Идеальный источник напряжения</summary>
    VoltageSource,

    /// <summary>Идеальный источник тока</summary>
    CurrentSource,

    /// <summary>Конденсатор: в установившемся режиме постоянного тока разрыв</summary>
    Capacitor,

    /// <summary>Катушка индуктивности: в установившемся режиме постоянного тока перемычка</summary>
    Inductor,
}
