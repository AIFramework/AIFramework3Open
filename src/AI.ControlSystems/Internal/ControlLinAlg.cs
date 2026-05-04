using System;
using AI.DataStructs.Algebraic;

namespace AI.ControlSystems.Internal;

/// <summary>Вспомогательные операции для линейных регуляторов (без внешнего API).</summary>
internal static class ControlLinAlg
{
    internal static Matrix Eye(int n)
    {
        Matrix m = new Matrix(n, n);
        for (int i = 0; i < n; i++)
            m[i, i] = 1.0;
        return m;
    }

    internal static Matrix Zeros(int rows, int cols)
    {
        return new Matrix(rows, cols);
    }

    internal static double MaxAbs(Matrix m)
    {
        double a = 0;
        for (int i = 0; i < m.Data.Length; i++)
        {
            double v = Math.Abs(m.Data[i]);
            if (v > a) a = v;
        }
        return a;
    }

    /// <summary>Матричная экспонента через масштабирование и степень двойки: exp(A) = (exp(A/2^s))^(2^s).</summary>
    internal static Matrix MatrixExp(Matrix a, double tol = 1e-14, int maxTaylor = 80)
    {
        if (!a.IsSquared)
            throw new ArgumentException("Ожидается квадратная матрица.", nameof(a));
        int n = a.Height;
        double norm = MaxAbs(a);
        int s = 0;
        if (norm > 1e-6)
        {
            while (norm / (1 << s) > 0.5)
                s++;
        }

        double scale = 1.0 / (1 << s);
        Matrix x = a * scale;
        Matrix e = Eye(n);
        Matrix term = Eye(n);
        for (int k = 1; k <= maxTaylor; k++)
        {
            term = term * x * (1.0 / k);
            e = e + term;
            if (MaxAbs(term) < tol)
                break;
        }

        for (int i = 0; i < s; i++)
            e = e * e;
        return e;
    }

    /// <summary>∫₀^dt exp(A τ) dτ = ∑ A^k dt^(k+1)/(k+1)!.</summary>
    internal static Matrix IntegrateExpDt(Matrix a, double dt, double tol = 1e-14, int maxTerms = 80)
    {
        if (!a.IsSquared)
            throw new ArgumentException("Ожидается квадратная матрица.", nameof(a));
        int n = a.Height;
        Matrix acc = Zeros(n, n);
        Matrix ap = Eye(n);
        double dtPow = dt;
        for (int k = 0; k < maxTerms; k++)
        {
            double fk = Factorial(k + 1);
            Matrix term = ap * (dtPow / fk);
            acc = acc + term;
            if (k > 2 && MaxAbs(term) < tol)
                break;
            ap = ap * a;
            dtPow *= dt;
        }
        return acc;
    }

    private static double Factorial(int n)
    {
        double r = 1.0;
        for (int i = 2; i <= n; i++)
            r *= i;
        return r;
    }

    internal static Matrix Transpose(Matrix m) => m.Transpose();

    internal static Matrix Symmetrize(Matrix p)
    {
        return (p + Transpose(p)) * 0.5;
    }

    /// <summary>Умножение матрицы m×n на столбец длины n (как в классической линейной алгебре).</summary>
    internal static Vector MatVec(Matrix m, Vector v)
    {
        if (m.Width != v.Count)
            throw new ArgumentException("Число столбцов матрицы должно совпадать с длиной вектора.");
        int rows = m.Height;
        Vector r = new Vector(rows);
        for (int i = 0; i < rows; i++)
        {
            double s = 0;
            for (int j = 0; j < v.Count; j++)
                s += m[i, j] * v[j];
            r[i] = s;
        }
        return r;
    }

    /// <summary>Столбец B (n×1) как вектор для умножения M * B.</summary>
    internal static Vector MatVec(Matrix m, Matrix column)
    {
        if (column == null)
            throw new ArgumentNullException(nameof(column));
        if (column.Width != 1)
            throw new ArgumentException("Ожидается один столбец.");
        var v = new Vector(column.Height);
        for (int i = 0; i < column.Height; i++)
            v[i] = column[i, 0];
        return MatVec(m, v);
    }

    internal static Vector Negate(Vector v)
    {
        Vector r = new Vector(v.Count);
        for (int i = 0; i < v.Count; i++)
            r[i] = -v[i];
        return r;
    }
}
