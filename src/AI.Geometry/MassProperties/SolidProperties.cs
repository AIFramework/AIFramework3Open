#nullable enable

using AI.Geometry.Primitives;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Geometry.MassProperties;

/// <summary>
/// Объем, центр масс и тензор инерции однородного тела единичной плотности.
/// </summary>
/// <param name="Volume">Объем.</param>
/// <param name="Centroid">Центр масс (центр объема).</param>
/// <param name="Inertia">Тензор инерции 3×3 относительно центра масс при единичной плотности.</param>
public readonly record struct SolidProperties(double Volume, Vector3 Centroid, Matrix Inertia)
{
    /// <summary>
    /// Тензор инерции тела заданной массы относительно центра масс: тензор единичной плотности,
    /// умноженный на плотность масса/объем.
    /// </summary>
    /// <param name="mass">Масса тела.</param>
    public Matrix InertiaForMass(double mass) => Inertia * (mass / Volume);
}
