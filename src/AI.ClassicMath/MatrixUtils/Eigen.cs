using AI.DataStructs.Algebraic;
using System;

namespace AI.ClassicMath.MatrixUtils;

/// <summary>
/// Порядок собственных значений в результате
/// </summary>
public enum EigenOrder
{
    /// <summary>По возрастанию: первым идёт наименьшее значение</summary>
    Ascending,

    /// <summary>По убыванию: первым идёт наибольшее значение</summary>
    Descending
}

/// <summary>
/// Задача на собственные значения для симметричных матриц: обычная, обобщённая
/// и спектральные функции матрицы.
/// </summary>
/// <remarks>
/// <para>
/// Вращения выполняет <see cref="JacobiEigen"/>; здесь добавлены упорядочивание,
/// обобщённая задача и функции от матрицы. Отдельной реализации метода вращений
/// в репозитории быть не должно — все потребители (химия, эконометрика, факторные
/// модели) обращаются сюда.
/// </para>
/// <para>
/// Матрицы предполагаются симметричными; симметричность не проверяется, а
/// используется только верхний треугольник в том виде, в каком его читает
/// <see cref="JacobiEigen"/>.
/// </para>
/// </remarks>
[Serializable]
public static class Eigen
{
    /// <summary>Число проходов метода вращений по умолчанию</summary>
    public const int DefaultIterations = 500;

    /// <summary>Порог сходимости по умолчанию</summary>
    public const double DefaultEps = 1e-13;

    /// <summary>
    /// Собственные значения и векторы симметричной матрицы
    /// </summary>
    /// <param name="matrix">Симметричная квадратная матрица</param>
    /// <param name="order">Порядок собственных значений</param>
    /// <param name="maxIterations">Максимальное число проходов</param>
    /// <param name="eps">Порог сходимости</param>
    /// <returns>Значения и матрица, столбцы которой — соответствующие векторы</returns>
    public static (Vector Values, Matrix Vectors) Symmetric(
        Matrix matrix,
        EigenOrder order = EigenOrder.Ascending,
        int maxIterations = DefaultIterations,
        double eps = DefaultEps)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        EnsureSquare(matrix, nameof(matrix));

        (double[] values, Matrix vectors) = JacobiEigen.Compute(matrix, maxIterations, eps);
        return Sort(values, vectors, order);
    }

    /// <summary>
    /// Обобщённая симметричная задача <c>A·x = λ·B·x</c> с положительно определённой <c>B</c>
    /// </summary>
    /// <remarks>
    /// Приводится к обычной задаче симметричной ортогонализацией по Лёвдину:
    /// <c>B^(-1/2)·A·B^(-1/2)·y = λ·y</c>, после чего векторы возвращаются в исходный базис
    /// умножением на <c>B^(-1/2)</c>. Способ сохраняет симметрию преобразованной матрицы,
    /// в отличие от разложения Холецкого с несимметричным треугольным множителем.
    /// </remarks>
    /// <param name="matrix">Матрица A</param>
    /// <param name="metric">Матрица B: симметричная положительно определённая</param>
    /// <param name="order">Порядок собственных значений</param>
    /// <param name="tolerance">Порог, ниже которого собственное число B считается нулевым</param>
    /// <param name="maxIterations">Максимальное число проходов</param>
    /// <param name="eps">Порог сходимости</param>
    /// <exception cref="ArgumentException">Матрица B вырождена либо размеры не совпадают</exception>
    public static (Vector Values, Matrix Vectors) GeneralizedSymmetric(
        Matrix matrix,
        Matrix metric,
        EigenOrder order = EigenOrder.Ascending,
        double tolerance = 1e-12,
        int maxIterations = DefaultIterations,
        double eps = DefaultEps)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(metric);
        EnsureSquare(matrix, nameof(matrix));
        EnsureSquare(metric, nameof(metric));

        if (matrix.Height != metric.Height)
            throw new ArgumentException("Матрицы A и B должны быть одного размера", nameof(metric));

        Matrix root = InverseSquareRoot(metric, tolerance, maxIterations, eps);
        Matrix transformed = root * matrix * root;

        (Vector values, Matrix vectors) = Symmetric(transformed, order, maxIterations, eps);

        return (values, root * vectors);
    }

    /// <summary>
    /// Функция от симметричной матрицы: <c>f(A) = U·diag(f(λ))·Uᵀ</c>
    /// </summary>
    /// <remarks>
    /// Через эту функцию выражаются корень, обратный корень, матричная экспонента
    /// и логарифм — всё, что применяется к собственным значениям поэлементно.
    /// </remarks>
    /// <param name="matrix">Симметричная матрица</param>
    /// <param name="function">Функция, применяемая к каждому собственному значению</param>
    /// <param name="maxIterations">Максимальное число проходов</param>
    /// <param name="eps">Порог сходимости</param>
    public static Matrix SymmetricFunction(
        Matrix matrix,
        Func<double, double> function,
        int maxIterations = DefaultIterations,
        double eps = DefaultEps)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(function);
        EnsureSquare(matrix, nameof(matrix));

        (double[] values, Matrix vectors) = JacobiEigen.Compute(matrix, maxIterations, eps);
        int n = values.Length;

        var transformed = new double[n];

        for (int k = 0; k < n; k++)
            transformed[k] = function(values[k]);

        var result = new Matrix(n, n);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = 0;

                for (int k = 0; k < n; k++)
                    sum += vectors[i, k] * vectors[j, k] * transformed[k];

                result[i, j] = sum;
                result[j, i] = sum;
            }
        }

        return result;
    }

    /// <summary>
    /// Квадратный корень из симметричной положительно полуопределённой матрицы
    /// </summary>
    /// <param name="matrix">Симметричная матрица с неотрицательным спектром</param>
    /// <param name="tolerance">
    /// Насколько отрицательным может быть собственное число, чтобы считаться нулём:
    /// небольшая отрицательность возникает из-за округления и обнуляется, заметная —
    /// признак того, что матрица не положительно полуопределённая.
    /// </param>
    /// <param name="maxIterations">Максимальное число проходов</param>
    /// <param name="eps">Порог сходимости</param>
    /// <exception cref="ArgumentException">Спектр содержит заметно отрицательное значение</exception>
    public static Matrix SquareRoot(
        Matrix matrix,
        double tolerance = 1e-12,
        int maxIterations = DefaultIterations,
        double eps = DefaultEps)
    {
        double limit = -Math.Abs(tolerance);

        return SymmetricFunction(matrix, value =>
        {
            if (value < limit)
                throw new ArgumentException(
                    $"Матрица не положительно полуопределённая: собственное число {value:E3}",
                    nameof(matrix));

            return Math.Sqrt(Math.Max(value, 0.0));
        }, maxIterations, eps);
    }

    /// <summary>
    /// Обратный квадратный корень <c>A^(-1/2)</c> симметричной положительно определённой матрицы
    /// </summary>
    /// <remarks>
    /// Симметричная ортогонализация по Лёвдину. Используется и в квантовой химии
    /// (приведение задачи с матрицей перекрывания), и в эконометрике
    /// (нормировка матрицы моментов в тесте Йохансена).
    /// </remarks>
    /// <param name="matrix">Симметричная положительно определённая матрица</param>
    /// <param name="tolerance">Порог, ниже которого собственное число считается нулевым</param>
    /// <param name="maxIterations">Максимальное число проходов</param>
    /// <param name="eps">Порог сходимости</param>
    /// <exception cref="ArgumentException">Матрица вырождена: есть собственное число ниже порога</exception>
    public static Matrix InverseSquareRoot(
        Matrix matrix,
        double tolerance = 1e-12,
        int maxIterations = DefaultIterations,
        double eps = DefaultEps)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        return SymmetricFunction(matrix, value =>
        {
            if (value <= tolerance)
                throw new ArgumentException(
                    $"Матрица вырождена: собственное число {value:E3} не превышает порога {tolerance:E3}",
                    nameof(matrix));

            return 1.0 / Math.Sqrt(value);
        }, maxIterations, eps);
    }

    private static (Vector Values, Matrix Vectors) Sort(double[] values, Matrix vectors, EigenOrder order)
    {
        int n = values.Length;
        var index = new int[n];

        for (int i = 0; i < n; i++)
            index[i] = i;

        Array.Sort(index, (left, right) => order == EigenOrder.Ascending
            ? values[left].CompareTo(values[right])
            : values[right].CompareTo(values[left]));

        var sortedValues = new Vector(n);
        var sortedVectors = new Matrix(n, n);

        for (int k = 0; k < n; k++)
        {
            sortedValues[k] = values[index[k]];

            for (int i = 0; i < n; i++)
                sortedVectors[i, k] = vectors[i, index[k]];
        }

        return (sortedValues, sortedVectors);
    }

    private static void EnsureSquare(Matrix matrix, string name)
    {
        if (matrix.Height != matrix.Width)
            throw new ArgumentException("Матрица должна быть квадратной", name);
    }
}
