#nullable enable
namespace AI.Physics.Continuum.Structures;

/// <summary>
/// Три величины узла плоской рамы: для перемещений это сдвиги по x и z, м, и поворот, рад;
/// для реакций и нагрузок это силы по x и z, Н, и момент, Н·м. Поворот и момент положительны против часовой стрелки
/// (от оси x к оси z)
/// </summary>
/// <param name="X">Составляющая по x</param>
/// <param name="Z">Составляющая по z</param>
/// <param name="Rotational">Поворот или момент</param>
public readonly record struct FrameNodeValues(double X, double Z, double Rotational);
