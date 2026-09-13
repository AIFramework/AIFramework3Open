using System;
using AI.ControlSystems.Internal;
using AI.ControlSystems.Linear;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Adaptive;

/// <summary>
/// MRAC по состоянию для объекта ẋ = A x + B Λ u с неизвестными A и Λ.
/// </summary>
/// <remarks>
/// <para>
/// Эталон <c>ẋ_m = A_m x_m + B_m r</c> с устойчивой A_m задаёт желаемую динамику. Управление
/// <c>u = K_x x + K_r r</c>; идеальные коэффициенты удовлетворяют условиям согласования
/// <c>A + BΛK_x* = A_m</c> и <c>BΛK_r* = B_m</c> — их существование и есть условие применимости.
/// Законы адаптации (Лаврецкий, Вайз):
/// <c>K̇_x = −γ_x·BᵀPe·xᵀ</c>, <c>K̇_r = −γ_r·BᵀPe·rᵀ</c>, где <c>e = x − x_m</c>, а P — решение
/// уравнения Ляпунова <c>A_mᵀP + PA_m = −Q</c>. Функция Ляпунова
/// <c>V = eᵀPe + tr(Λ ΔK Γ⁻¹ ΔKᵀ)</c> убывает, и ошибка слежения стремится к нулю.
/// </para>
/// <para>
/// Известны должны быть B и знаки Λ — здесь Λ считается положительной диагональной, то есть
/// входы не меняют направления действия. Утечка σ (σ-модификация) держит коэффициенты
/// ограниченными при возмущениях и шуме ценой небольшой остаточной ошибки.
/// </para>
/// <para>
/// Эталонная модель интегрируется точно (ZOH), законы адаптации — методом Эйлера с шагом вызова.
/// </para>
/// </remarks>
[Serializable]
public sealed class StateModelReferenceAdaptiveController
{
    private readonly Matrix _am;
    private readonly Matrix _bm;
    private readonly Matrix _pb;
    private readonly int _n;
    private readonly int _m;
    private readonly int _q;
    private Matrix _kx;
    private Matrix _kr;
    private Vector _xm;
    private double _cachedStep = double.NaN;
    private Matrix _amDiscrete;
    private Matrix _bmDiscrete;

    /// <summary>Создаёт регулятор</summary>
    /// <param name="referenceA">A_m эталона (n×n), устойчивая</param>
    /// <param name="referenceB">B_m эталона (n×q)</param>
    /// <param name="inputMatrix">Известная матрица входа B объекта (n×m)</param>
    /// <param name="lyapunovWeight">Q в уравнении Ляпунова; по умолчанию единичная</param>
    public StateModelReferenceAdaptiveController(Matrix referenceA, Matrix referenceB, Matrix inputMatrix, Matrix lyapunovWeight = null)
    {
        if (referenceA == null || referenceB == null || inputMatrix == null)
            throw new ArgumentNullException();

        _n = referenceA.Height;

        if (!referenceA.IsSquared || referenceB.Height != _n || inputMatrix.Height != _n)
            throw new ArgumentException("A_m должна быть n×n, B_m и B — по n строк.");

        if (!SystemAnalysis.IsStableContinuous(referenceA))
            throw new ArgumentException("Эталонная модель должна быть устойчивой: все полюса A_m в левой полуплоскости.", nameof(referenceA));

        _m = inputMatrix.Width;
        _q = referenceB.Width;
        _am = referenceA;
        _bm = referenceB;

        Matrix weight = lyapunovWeight ?? ControlLinAlg.Eye(_n);
        LyapunovSolution = LyapunovEquation.SolveContinuous(referenceA.Transpose(), weight);
        _pb = LyapunovSolution * inputMatrix;

        _kx = new Matrix(_m, _n);
        _kr = new Matrix(_m, _q);
        _xm = new Vector(_n);
        TrackingError = new Vector(_n);
    }

    /// <summary>Коэффициент адаптации обратной связи γ_x</summary>
    public double StateAdaptationGain { get; set; } = 1;

    /// <summary>Коэффициент адаптации прямой связи γ_r</summary>
    public double ReferenceAdaptationGain { get; set; } = 1;

    /// <summary>Утечка σ ≥ 0: σ-модификация для робастности</summary>
    public double Leakage { get; set; }

    /// <summary>Решение P уравнения Ляпунова эталона</summary>
    public Matrix LyapunovSolution { get; }

    /// <summary>Текущие коэффициенты обратной связи K_x (m×n)</summary>
    public Matrix StateGain => _kx.Copy();

    /// <summary>Текущие коэффициенты прямой связи K_r (m×q)</summary>
    public Matrix ReferenceGain => _kr.Copy();

    /// <summary>Состояние эталона x_m</summary>
    public Vector ReferenceState => Copy(_xm);

    /// <summary>Ошибка слежения x − x_m на последнем шаге</summary>
    public Vector TrackingError { get; private set; }

    /// <summary>Сброс эталона и, при необходимости, коэффициентов</summary>
    /// <param name="referenceState">Начальное состояние эталона</param>
    /// <param name="stateGain">Начальные K_x; null — оставить</param>
    /// <param name="referenceGain">Начальные K_r; null — оставить</param>
    public void Reset(Vector referenceState = null, Matrix stateGain = null, Matrix referenceGain = null)
    {
        _xm = referenceState == null ? new Vector(_n) : Copy(referenceState);

        if (stateGain != null)
        {
            if (stateGain.Height != _m || stateGain.Width != _n)
                throw new ArgumentException("K_x должна быть m×n.", nameof(stateGain));
            _kx = stateGain.Copy();
        }

        if (referenceGain != null)
        {
            if (referenceGain.Height != _m || referenceGain.Width != _q)
                throw new ArgumentException("K_r должна быть m×q.", nameof(referenceGain));
            _kr = referenceGain.Copy();
        }
    }

    /// <summary>Один шаг: управление по текущему состоянию и заданию, затем адаптация</summary>
    /// <param name="x">Состояние объекта</param>
    /// <param name="r">Задание</param>
    /// <param name="dt">Шаг</param>
    public Vector Compute(Vector x, Vector r, double dt)
    {
        if (x == null || r == null)
            throw new ArgumentNullException();
        if (x.Count != _n || r.Count != _q)
            throw new ArgumentException("Размерности x и r не совпадают с моделью.");
        if (!(dt > 0))
            throw new ArgumentOutOfRangeException(nameof(dt));

        Vector e = x - _xm;
        Vector u = ControlLinAlg.MatVec(_kx, x) + ControlLinAlg.MatVec(_kr, r);
        Vector projected = ControlLinAlg.MatVec(_pb.Transpose(), e);

        for (int i = 0; i < _m; i++)
        {
            for (int j = 0; j < _n; j++)
                _kx[i, j] += dt * StateAdaptationGain * ((-projected[i] * x[j]) - (Leakage * _kx[i, j]));

            for (int j = 0; j < _q; j++)
                _kr[i, j] += dt * ReferenceAdaptationGain * ((-projected[i] * r[j]) - (Leakage * _kr[i, j]));
        }

        if (dt != _cachedStep)
        {
            Discretization.ZeroOrderHold(_am, _bm, dt, out _amDiscrete, out _bmDiscrete);
            _cachedStep = dt;
        }

        _xm = ControlLinAlg.MatVec(_amDiscrete, _xm) + ControlLinAlg.MatVec(_bmDiscrete, r);
        TrackingError = e;

        return u;
    }

    private static Vector Copy(Vector v)
    {
        var r = new Vector(v.Count);
        for (int i = 0; i < v.Count; i++)
            r[i] = v[i];
        return r;
    }
}
