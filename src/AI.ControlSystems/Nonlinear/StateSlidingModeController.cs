using System;
using AI.ClassicMath.MatrixUtils;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Nonlinear;

/// <summary>
/// Скользящий режим для линейного объекта ẋ = A x + B (u + d) с согласованным возмущением d.
/// </summary>
/// <remarks>
/// <para>
/// Поверхность скольжения <c>s = S x</c> (m уравнений на m входов) выбирается так, чтобы движение
/// по ней было устойчивым: на поверхности порядок системы падает на m, и оставшаяся динамика
/// задаётся только S. Для объекта второго порядка <c>ẍ = u</c> поверхность <c>s = λx + ẋ</c>
/// даёт на ней <c>ẋ = −λx</c>.
/// </para>
/// <para>
/// Управление <c>u = −(SB)⁻¹(SAx + K·sat(s/Φ))</c> — эквивалентное управление, удерживающее на
/// поверхности номинальный объект, плюс разрывная добавка. Тогда <c>ṡ = −K·sat(s/Φ) + SBd</c>:
/// если Kᵢ больше границы i-й составляющей SBd, траектория достигает поверхности за конечное
/// время <c>|s(0)|/(K − |SBd|)</c> и остаётся на ней, а согласованное возмущение на движение по
/// поверхности не влияет — в этом инвариантность скользящего режима.
/// </para>
/// <para>
/// Φ = 0 — чистое реле с дребезгом; Φ &gt; 0 — пограничный слой, внутри которого закон линейный,
/// и точность ограничена его толщиной. Возмущение, не согласованное с B, скользящий режим не подавляет.
/// </para>
/// </remarks>
[Serializable]
public sealed class StateSlidingModeController
{
    private readonly Matrix _surface;
    private readonly Matrix _surfaceA;
    private readonly Matrix _surfaceBInverse;
    private readonly int _m;

    /// <summary>Создаёт регулятор</summary>
    /// <param name="a">A (n×n)</param>
    /// <param name="b">B (n×m)</param>
    /// <param name="surface">S (m×n) с обратимой SB</param>
    public StateSlidingModeController(Matrix a, Matrix b, Matrix surface)
    {
        if (a == null || b == null || surface == null)
            throw new ArgumentNullException();

        int n = a.Height;
        _m = b.Width;

        if (!a.IsSquared || b.Height != n || surface.Height != _m || surface.Width != n)
            throw new ArgumentException("A должна быть n×n, B — n×m, S — m×n.");

        Matrix sb = surface * b;
        double determinant;

        try
        {
            determinant = LU.Determinant(sb);
        }
        catch (InvalidOperationException)
        {
            determinant = 0;
        }

        if (Math.Abs(determinant) < 1e-12 * Math.Max(1, ControlLinAlg.MaxAbs(sb)))
            throw new ArgumentException("Матрица SB вырождена: управление не действует на поверхность скольжения.", nameof(surface));

        _surface = surface;
        _surfaceA = surface * a;
        _surfaceBInverse = ControlLinAlg.Inverse(sb);

        var gain = new Vector(_m);
        for (int i = 0; i < _m; i++)
            gain[i] = 1;
        SwitchingGain = gain;
    }

    /// <summary>Усиления разрывной части Kᵢ &gt; 0 по поверхностям</summary>
    public Vector SwitchingGain { get; set; }

    /// <summary>Толщина пограничного слоя Φ ≥ 0</summary>
    public double BoundaryLayer { get; set; }

    /// <summary>Значение s = S x</summary>
    /// <param name="x">Состояние</param>
    public Vector Surface(Vector x) => ControlLinAlg.MatVec(_surface, x);

    /// <summary>Управление для текущего состояния</summary>
    /// <param name="x">Состояние</param>
    public Vector Compute(Vector x)
    {
        if (x == null)
            throw new ArgumentNullException(nameof(x));
        if (SwitchingGain == null || SwitchingGain.Count != _m)
            throw new InvalidOperationException("Усилений разрывной части должно быть по числу входов.");

        Vector s = Surface(x);
        Vector drive = ControlLinAlg.MatVec(_surfaceA, x);

        for (int i = 0; i < _m; i++)
        {
            double switching = BoundaryLayer > 0
                ? Math.Clamp(s[i] / BoundaryLayer, -1, 1)
                : Math.Sign(s[i]);

            drive[i] += SwitchingGain[i] * switching;
        }

        return ControlLinAlg.Negate(ControlLinAlg.MatVec(_surfaceBInverse, drive));
    }
}
