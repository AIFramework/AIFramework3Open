using System;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Observers;

/// <summary>
/// Дискретный наблюдатель Люенбергера (предиктор + коррекция в одном шаге):
/// x̂[k+1] = A x̂[k] + B u[k] + L (y[k] − C x̂[k] − D u[k]).
/// </summary>
[Serializable]
public sealed class LuenbergerObserver
{
    private readonly Matrix _a;
    private readonly Matrix _b;
    private readonly Matrix _c;
    private readonly Matrix _d;
    private readonly Matrix _l;

    /// <summary>Оценка состояния x̂.</summary>
    public Vector State { get; private set; }

    public int StateDimension => _a.Height;

    public LuenbergerObserver(Matrix a, Matrix b, Matrix c, Matrix d, Matrix l, Vector initialEstimate)
    {
        if (a == null || b == null || c == null || d == null || l == null || initialEstimate == null)
            throw new ArgumentNullException();
        int n = a.Height;
        if (!a.IsSquared || a.Width != n)
            throw new ArgumentException("A должна быть n×n.");
        if (b.Height != n || c.Width != n)
            throw new ArgumentException("Несогласованные размеры A, B, C.");
        if (d.Height != c.Height || d.Width != b.Width)
            throw new ArgumentException("Размерность D должна совпадать с p×m.");
        if (l.Height != n || l.Width != c.Height)
            throw new ArgumentException("L должна быть n×p.");
        if (initialEstimate.Count != n)
            throw new ArgumentException("Начальная оценка должна иметь размерность n.");
        _a = a;
        _b = b;
        _c = c;
        _d = d;
        _l = l;
        State = initialEstimate;
    }

    public LuenbergerObserver(Matrix a, Matrix b, Matrix c, Matrix d, Matrix l)
        : this(a, b, c, d, l, new Vector(a.Height))
    {
    }

    /// <summary>Без прямой связи на выходе объекта (D = 0).</summary>
    public LuenbergerObserver(Matrix a, Matrix b, Matrix c, Matrix l, Vector initialEstimate)
        : this(a, b, c, new Matrix(c.Height, b.Width), l, initialEstimate)
    {
    }

    public void Reset() => State = new Vector(StateDimension);

    /// <summary>Один шаг: u — управление на шаге, y — измерение на шаге.</summary>
    public void Step(Vector u, Vector y)
    {
        if (u.Count != _b.Width)
            throw new ArgumentException("Размерность u.");
        if (y.Count != _c.Height)
            throw new ArgumentException("Размерность y.");

        Vector yHat = ControlLinAlg.MatVec(_c, State) + ControlLinAlg.MatVec(_d, u);
        Vector innov = new Vector(y.Count);
        for (int i = 0; i < y.Count; i++)
            innov[i] = y[i] - yHat[i];

        Vector lx = ControlLinAlg.MatVec(_l, innov);
        Vector xNext = ControlLinAlg.MatVec(_a, State) + ControlLinAlg.MatVec(_b, u) + lx;
        State = xNext;
    }
}
