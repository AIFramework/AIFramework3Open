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

    /// <summary>Норма Фробениуса</summary>
    internal static double Frobenius(Matrix m)
    {
        double s = 0;
        for (int i = 0; i < m.Data.Length; i++)
            s += m.Data[i] * m.Data[i];
        return Math.Sqrt(s);
    }

    /// <summary>Бесконечная норма: наибольшая сумма модулей по строке</summary>
    internal static double InfinityNorm(Matrix m)
    {
        double best = 0;
        for (int i = 0; i < m.Height; i++)
        {
            double row = 0;
            for (int j = 0; j < m.Width; j++)
                row += Math.Abs(m[i, j]);
            best = Math.Max(best, row);
        }
        return best;
    }

    /// <summary>
    /// Матричная экспонента масштабированием и возведением в квадрат: exp(A) = (exp(A/2^s))^(2^s).
    /// </summary>
    /// <remarks>
    /// Степень s подбирается по бесконечной норме так, чтобы ‖A/2^s‖ ≤ 1/2; тогда ряд Тейлора
    /// сходится за два десятка членов без сокращения больших слагаемых. Прежде масштаб считался по
    /// наибольшему элементу, а не по норме.
    /// </remarks>
    internal static Matrix MatrixExp(Matrix a, double tol = 1e-16, int maxTaylor = 60)
    {
        if (!a.IsSquared)
            throw new ArgumentException("Ожидается квадратная матрица.", nameof(a));
        int n = a.Height;
        double norm = InfinityNorm(a);
        if (!double.IsFinite(norm))
            throw new ArgumentException("Матрица содержит бесконечные элементы.", nameof(a));

        int s = norm > 0.5 ? (int)Math.Ceiling(Math.Log2(norm / 0.5)) : 0;
        Matrix x = a * Math.Pow(2, -s);
        Matrix e = Eye(n);
        Matrix term = Eye(n);
        for (int k = 1; k <= maxTaylor; k++)
        {
            term = term * x * (1.0 / k);
            e = e + term;
            if (MaxAbs(term) <= tol * Math.Max(1, MaxAbs(e)))
                break;
        }

        for (int i = 0; i < s; i++)
            e = e * e;
        return e;
    }

    /// <summary>Подматрица: строки [row, row + rows), столбцы [col, col + cols)</summary>
    internal static Matrix Block(Matrix m, int row, int col, int rows, int cols)
    {
        var r = new Matrix(rows, cols);
        for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
                r[i, j] = m[row + i, col + j];
        return r;
    }

    /// <summary>Вписывает блок в матрицу начиная с позиции (row, col)</summary>
    internal static void SetBlock(Matrix target, int row, int col, Matrix block)
    {
        for (int i = 0; i < block.Height; i++)
            for (int j = 0; j < block.Width; j++)
                target[row + i, col + j] = block[i, j];
    }

    /// <summary>Обратная матрица: LU с выбором главного элемента из ядра</summary>
    /// <remarks>
    /// Здесь было своё LU: прежний Matrix.GetInvertMatrix отвергал хорошо обусловленные матрицы
    /// малого масштаба по абсолютному порогу определителя. Ядро исправлено, и этот метод остался
    /// единственной точкой обращения в сборке.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Матрица вырождена или численно вырождена</exception>
    internal static Matrix Inverse(Matrix m) => m.GetInvertMatrix();

    internal static Matrix Transpose(Matrix m) => m.Transpose();

    internal static Matrix Symmetrize(Matrix p)
    {
        return (p + Transpose(p)) * 0.5;
    }

    /// <summary>Все ли элементы конечны</summary>
    internal static bool IsFinite(Matrix m)
    {
        for (int i = 0; i < m.Data.Length; i++)
            if (!double.IsFinite(m.Data[i]))
                return false;
        return true;
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

    /// <summary>След матрицы</summary>
    internal static double Trace(Matrix m)
    {
        double s = 0;
        for (int i = 0; i < Math.Min(m.Height, m.Width); i++)
            s += m[i, i];
        return s;
    }
}
