using System;
using AI.ControlSystems.Internal;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Linear;

/// <summary>
/// Дискретная линейная модель в пространстве состояний:
/// x[k+1] = A x[k] + B u[k], y[k] = C x[k] + D u[k].
/// </summary>
[Serializable]
public sealed class DiscreteLtiModel
{
    /// <summary>Матрица A (n×n).</summary>
    public Matrix A { get; }

    /// <summary>Матрица B (n×m).</summary>
    public Matrix B { get; }

    /// <summary>Матрица C (p×n).</summary>
    public Matrix C { get; }

    /// <summary>Матрица D (p×m), может быть нулевой.</summary>
    public Matrix D { get; }

    /// <summary>Текущее состояние x (размерность n).</summary>
    public Vector State { get; private set; }

    public int StateDimension => A.Height;
    public int InputDimension => B.Width;
    public int OutputDimension => C.Height;

    public DiscreteLtiModel(Matrix a, Matrix b, Matrix c, Matrix d, Vector initialState)
    {
        Validate(a, b, c, d, initialState);
        A = a;
        B = b;
        C = c;
        D = d;
        State = initialState;
    }

    /// <summary>С нулевым начальным состоянием.</summary>
    public DiscreteLtiModel(Matrix a, Matrix b, Matrix c, Matrix d)
        : this(a, b, c, d, new Vector(a.Height))
    {
    }

    /// <summary>Без прямой связи D = 0.</summary>
    public DiscreteLtiModel(Matrix a, Matrix b, Matrix c, Vector initialState)
        : this(a, b, c, ZerosD(c.Height, b.Width), initialState)
    {
    }

    public DiscreteLtiModel(Matrix a, Matrix b, Matrix c)
        : this(a, b, c, new Vector(a.Height))
    {
    }

    private static Matrix ZerosD(int p, int m) => new Matrix(p, m);

    private static void Validate(Matrix a, Matrix b, Matrix c, Matrix d, Vector x0)
    {
        if (a == null || b == null || c == null || d == null || x0 == null)
            throw new ArgumentNullException();
        int n = a.Height;
        if (!a.IsSquared || a.Width != n)
            throw new ArgumentException("A должна быть квадратной n×n.");
        if (b.Height != n)
            throw new ArgumentException("Число строк B должно совпадать с порядком A.");
        if (c.Width != n)
            throw new ArgumentException("Число столбцов C должно быть n.");
        if (d.Height != c.Height || d.Width != b.Width)
            throw new ArgumentException("Размерность D должна быть p×m.");
        if (x0.Count != n)
            throw new ArgumentException("Размерность начального состояния должна быть n.");
    }

    /// <summary>Сброс состояния в ноль.</summary>
    public void Reset() => State = new Vector(StateDimension);

    /// <summary>Один шаг модели: x <- A x + B u, возвращает y = C x + D u.</summary>
    public Vector Step(Vector u)
    {
        if (u.Count != InputDimension)
            throw new ArgumentException("Размерность u не совпадает с m.");
        Vector x = ControlLinAlg.MatVec(A, State) + ControlLinAlg.MatVec(B, u);
        State = x;
        return ControlLinAlg.MatVec(C, State) + ControlLinAlg.MatVec(D, u);
    }

    /// <summary>Выход без обновления состояния (y = C x + D u).</summary>
    public Vector OutputFor(Vector u)
    {
        if (u.Count != InputDimension)
            throw new ArgumentException("Размерность u не совпадает с m.");
        return ControlLinAlg.MatVec(C, State) + ControlLinAlg.MatVec(D, u);
    }
}
