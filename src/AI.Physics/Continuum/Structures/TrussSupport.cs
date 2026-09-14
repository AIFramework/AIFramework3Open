#nullable enable
namespace AI.Physics.Continuum.Structures;

/// <summary>Закрепленные оси узла пространственной фермы</summary>
[Flags]
public enum TrussSupport
{
    /// <summary>Узел свободен</summary>
    None = 0,

    /// <summary>Запрещен сдвиг по x</summary>
    X = 1,

    /// <summary>Запрещен сдвиг по y</summary>
    Y = 2,

    /// <summary>Запрещен сдвиг по z</summary>
    Z = 4,

    /// <summary>Неподвижный шарнир: запрещены все сдвиги</summary>
    All = X | Y | Z,
}
