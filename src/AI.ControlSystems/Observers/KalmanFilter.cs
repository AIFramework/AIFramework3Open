using System;
using AI.ControlSystems.Internal;
using AI.ControlSystems.Linear;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Observers;

/// <summary>Установившийся фильтр Калмана</summary>
/// <param name="Gain">Усиление коррекции L: x̂⁺ = x̄ + L(y − Cx̄)</param>
/// <param name="PriorCovariance">Ковариация ошибки после предсказания</param>
/// <param name="PosteriorCovariance">Ковариация ошибки после коррекции</param>
public sealed record SteadyStateKalman(Matrix Gain, Matrix PriorCovariance, Matrix PosteriorCovariance);

/// <summary>
/// Дискретный линейный фильтр Калмана (управляемый объект):
/// предсказание x̄ = A x + B u, P̄ = A P Aᵀ + Q;
/// коррекция по измерению y с шумом R.
/// </summary>
[Serializable]
public sealed class KalmanFilter
{
    private readonly Matrix _a;
    private readonly Matrix _b;
    private readonly Matrix _c;
    private readonly Matrix _d;

    /// <summary>Ковариация шума процесса Q (n×n).</summary>
    public Matrix Q { get; set; }

    /// <summary>Ковариация шума измерения R (p×p).</summary>
    public Matrix R { get; set; }

    /// <summary>Оценка состояния.</summary>
    public Vector State { get; private set; }

    /// <summary>Ковариация ошибки оценки P.</summary>
    public Matrix Covariance { get; private set; }

    public int StateDimension => _a.Height;

    /// <summary>Размерность управления m.</summary>
    public int InputDimension => _b.Width;

    public KalmanFilter(Matrix a, Matrix b, Matrix c, Matrix d, Matrix q, Matrix r, Vector x0, Matrix p0)
    {
        Validate(a, b, c, d, q, r, x0, p0);
        _a = a;
        _b = b;
        _c = c;
        _d = d;
        Q = q;
        R = r;
        State = x0;
        Covariance = p0;
    }

    public KalmanFilter(Matrix a, Matrix b, Matrix c, Matrix d, Matrix q, Matrix r)
        : this(a, b, c, d, q, r, new Vector(a.Height), ControlLinAlg.Symmetrize(ControlLinAlg.Eye(a.Height) * 1e-2))
    {
    }

    private static void Validate(Matrix a, Matrix b, Matrix c, Matrix d, Matrix q, Matrix r, Vector x0, Matrix p0)
    {
        if (a == null || b == null || c == null || d == null || q == null || r == null || x0 == null || p0 == null)
            throw new ArgumentNullException();
        int n = a.Height;
        if (!a.IsSquared || q.Height != n || q.Width != n || p0.Height != n || p0.Width != n)
            throw new ArgumentException("Неверные размеры A, Q или P0.");
        if (b.Height != n || c.Width != n)
            throw new ArgumentException("Неверные размеры B или C.");
        int p = c.Height;
        if (d.Height != p || d.Width != b.Width)
            throw new ArgumentException("Неверная размерность D.");
        if (r.Height != p || r.Width != p)
            throw new ArgumentException("R должна быть p×p.");
        if (x0.Count != n)
            throw new ArgumentException("x0 должна быть длины n.");
    }

    public void Reset(Vector x0 = null, Matrix p0 = null)
    {
        State = x0 ?? new Vector(StateDimension);
        Covariance = p0 ?? ControlLinAlg.Symmetrize(ControlLinAlg.Eye(StateDimension) * 1e-2);
    }

    /// <summary>
    /// Установившийся режим: ковариация, к которой сходится фильтр, и постоянное усиление
    /// </summary>
    /// <remarks>
    /// Ковариация после предсказания — решение уравнения Риккати, двойственного к LQR: с парой
    /// (Aᵀ, Cᵀ) вместо (A, B). Решение существует, если пара (A, C) обнаруживаема.
    /// </remarks>
    /// <param name="a">A</param>
    /// <param name="c">C</param>
    /// <param name="q">Ковариация шума процесса</param>
    /// <param name="r">Ковариация шума измерения, положительно определённая</param>
    public static SteadyStateKalman SteadyState(Matrix a, Matrix c, Matrix q, Matrix r)
    {
        if (a == null || c == null || q == null || r == null)
            throw new ArgumentNullException();

        Matrix prior = RiccatiEquation.SolveDiscrete(a.Transpose(), c.Transpose(), q, r);
        Matrix ct = c.Transpose();
        Matrix gain = prior * ct * ControlLinAlg.Inverse((c * prior * ct) + r);
        Matrix posterior = ControlLinAlg.Symmetrize((ControlLinAlg.Eye(a.Height) - (gain * c)) * prior);

        return new SteadyStateKalman(gain, prior, posterior);
    }

    /// <summary>Предсказание по модели с управлением u.</summary>
    public void Predict(Vector u)
    {
        if (u.Count != _b.Width)
            throw new ArgumentException("Размерность u.");
        State = ControlLinAlg.MatVec(_a, State) + ControlLinAlg.MatVec(_b, u);
        Matrix at = _a.Transpose();
        Covariance = ControlLinAlg.Symmetrize(_a * Covariance * at + Q);
    }

    /// <summary>Коррекция по измерению y; u — то же управление, что на шаге предсказания (для D u в выходе).</summary>
    public void Update(Vector y, Vector u)
    {
        if (y.Count != _c.Height)
            throw new ArgumentException("Размерность y.");
        if (u.Count != _b.Width)
            throw new ArgumentException("Размерность u.");

        Vector yHat = ControlLinAlg.MatVec(_c, State) + ControlLinAlg.MatVec(_d, u);
        Vector nu = new Vector(y.Count);
        for (int i = 0; i < y.Count; i++)
            nu[i] = y[i] - yHat[i];

        Matrix ct = _c.Transpose();
        Matrix s = _c * Covariance * ct + R;
        Matrix sInv = ControlLinAlg.Inverse(s);
        Matrix pcT = Covariance * ct;
        Matrix k = pcT * sInv;

        State = State + ControlLinAlg.MatVec(k, nu);
        Matrix ikh = ControlLinAlg.Eye(StateDimension) - k * _c;
        Covariance = ControlLinAlg.Symmetrize(ikh * Covariance * ikh.Transpose() + k * R * k.Transpose());
    }
}
