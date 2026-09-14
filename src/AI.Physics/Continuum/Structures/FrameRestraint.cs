#nullable enable
namespace AI.Physics.Continuum.Structures;

/// <summary>Закрепления узла плоской рамы: сдвиг по x, по z и поворот</summary>
[Flags]
public enum FrameRestraint
{
    /// <summary>Узел свободен</summary>
    None = 0,

    /// <summary>Запрещен сдвиг по x</summary>
    X = 1,

    /// <summary>Запрещен сдвиг по z; с одной этой связью узел есть подвижная шарнирная опора</summary>
    Z = 2,

    /// <summary>Запрещен поворот</summary>
    Rotation = 4,

    /// <summary>Неподвижная шарнирная опора: сдвиги запрещены, поворот свободен</summary>
    Pin = X | Z,

    /// <summary>Заделка: запрещено все</summary>
    Clamp = X | Z | Rotation,
}
