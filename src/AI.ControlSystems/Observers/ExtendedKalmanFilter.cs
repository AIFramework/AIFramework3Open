using System;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Observers;

/// <summary>
/// Расширенный фильтр Калмана: нелинейные f, h задаются снаружи; на каждом шаге передаются
/// уже линеаризованные якобианы F = ∂f/∂x, H = ∂h/∂x и предсказанные x⁺, ŷ.
/// </summary>
[Serializable]
public sealed class ExtendedKalmanFilter
{
    /// <summary>Оценка состояния.</summary>
    public Vector State { get; private set; }

    /// <summary>Ковариация ошибки P.</summary>
    public Matrix Covariance { get; private set; }

    public ExtendedKalmanFilter(Vector x0, Matrix p0)
    {
        if (x0 == null || p0 == null)
            throw new ArgumentNullException();
        if (!p0.IsSquared || p0.Height != x0.Count)
            throw new ArgumentException("P должна быть n×n, совпадающая с размерностью x.");
        State = x0;
        Covariance = ControlLinAlg.Symmetrize(p0);
    }

    public int StateDimension => State.Count;

    /// <summary>
    /// Шаг предсказания: заданное нелинейное обновление состояния x и якобиан F, шум Q.
    /// </summary>
    public void Predict(Vector xNext, Matrix fJacobian, Matrix q)
    {
        if (xNext == null || fJacobian == null || q == null)
            throw new ArgumentNullException();
        if (xNext.Count != StateDimension || fJacobian.Height != StateDimension || fJacobian.Width != StateDimension
            || q.Height != StateDimension || q.Width != StateDimension)
            throw new ArgumentException("Несогласованные размеры.");
        State = xNext;
        Matrix ft = fJacobian.Transpose();
        Covariance = ControlLinAlg.Symmetrize(fJacobian * Covariance * ft + q);
    }

    /// <summary>
    /// Шаг коррекции: инновация y − ŷ уже в ньютоновской форме через H и R.
    /// </summary>
    public void Update(Vector y, Vector yPredicted, Matrix hJacobian, Matrix r)
    {
        if (y == null || yPredicted == null || hJacobian == null || r == null)
            throw new ArgumentNullException();
        int p = y.Count;
        if (yPredicted.Count != p || hJacobian.Height != p || hJacobian.Width != StateDimension
            || r.Height != p || r.Width != p)
            throw new ArgumentException("Несогласованные размеры измерения.");

        Vector nu = y - yPredicted;
        Matrix ht = hJacobian.Transpose();
        Matrix s = hJacobian * Covariance * ht + r;
        Matrix sInv = s.GetInvertMatrix();
        Matrix k = Covariance * ht * sInv;
        State = State + ControlLinAlg.MatVec(k, nu);
        Matrix ikh = ControlLinAlg.Eye(StateDimension) - k * hJacobian;
        Covariance = ControlLinAlg.Symmetrize(ikh * Covariance * ikh.Transpose() + k * r * k.Transpose());
    }

    public void Reset(Vector x0, Matrix p0)
    {
        State = x0;
        Covariance = ControlLinAlg.Symmetrize(p0);
    }
}
