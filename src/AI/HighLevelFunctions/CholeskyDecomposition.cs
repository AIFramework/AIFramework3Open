using AI.DataStructs.Algebraic;
using System;

namespace AI.HighLevelFunctions;

/// <summary>
/// Результат разложения Холецкого с регуляризацией.
/// </summary>
/// <param name="Lower">Нижнетреугольный множитель L: <c>A + Shift·I = L·Lᵀ</c>.</param>
/// <param name="Regularized">Понадобился ли сдвиг диагонали.</param>
/// <param name="Shift">Величина сдвига; 0, если матрица разложилась как есть.</param>
public readonly record struct RegularizedCholesky(Matrix Lower, bool Regularized, double Shift);

/// <summary>
/// Разложение Холецкого симметричной положительно определённой матрицы: <c>A = L·Lᵀ</c>.
/// </summary>
/// <remarks>
/// <para>
/// Единственная реализация разложения. <see cref="Decompose"/> не подправляет вход: матрица, которая не
/// является положительно определённой, — это отказ, а не повод вернуть «какой-нибудь» множитель.
/// Отказом считается и численная вырожденность: ведущий элемент меньше <c>n·ε</c> от диагонального
/// элемента матрицы, то есть обусловленность хуже примерно 10¹⁵.
/// </para>
/// <para>
/// Выборочные ковариационные матрицы из-за округления бывают вырождены на волос. Для них есть
/// <see cref="DecomposeRegularized"/>: сдвиг диагонали применяется только после неудачи обычного
/// разложения, растёт до первого успеха, и результат сообщает, сработал ли он и какой величины.
/// </para>
/// </remarks>
public static class CholeskyDecomposition
{
    private const string NotPositiveDefinite = "Матрица не является положительно определённой";

    /// <summary>
    /// Нижнетреугольный множитель L, такой что <c>A = L·Lᵀ</c>.
    /// </summary>
    /// <param name="matrix">Симметричная положительно определённая матрица; читается нижний треугольник.</param>
    /// <exception cref="ArgumentException">Матрица не квадратная.</exception>
    /// <exception cref="InvalidOperationException">Матрица не является положительно определённой.</exception>
    public static Matrix Decompose(Matrix matrix)
    {
        if (!TryDecompose(matrix, out Matrix lower))
            throw new InvalidOperationException(NotPositiveDefinite);

        return lower;
    }

    /// <summary>
    /// Разложение без исключения: <c>false</c>, если матрица не является положительно определённой.
    /// </summary>
    /// <param name="matrix">Симметричная матрица; читается нижний треугольник.</param>
    /// <param name="lower">Множитель L; при неудаче заполнен частично и использовать его нельзя.</param>
    /// <exception cref="ArgumentException">Матрица не квадратная.</exception>
    public static bool TryDecompose(Matrix matrix, out Matrix lower) => TryDecompose(matrix, 0, out lower);

    /// <summary>
    /// Разложение со сдвигом диагонали, который применяется только при неудаче обычного.
    /// </summary>
    /// <remarks>
    /// Первый сдвиг равен <paramref name="initialRelativeShift"/> от среднего модуля диагонали, каждый
    /// следующий вдесятеро больше. Сдвиг меняет задачу: множитель относится к матрице <c>A + Shift·I</c>,
    /// поэтому вызывающий обязан донести <see cref="RegularizedCholesky.Shift"/> до пользователя.
    /// </remarks>
    /// <param name="matrix">Симметричная матрица; читается нижний треугольник.</param>
    /// <param name="initialRelativeShift">Первый сдвиг в долях среднего модуля диагонали.</param>
    /// <param name="maxAttempts">Сколько раз увеличивать сдвиг.</param>
    /// <exception cref="ArgumentException">Матрица не квадратная.</exception>
    /// <exception cref="InvalidOperationException">
    /// Разложение не удалось и со сдвигом: в матрице не числа или она далека от положительно определённой.
    /// </exception>
    public static RegularizedCholesky DecomposeRegularized(Matrix matrix, double initialRelativeShift = 1e-12, int maxAttempts = 12)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialRelativeShift);

        if (TryDecompose(matrix, 0, out Matrix lower))
            return new RegularizedCholesky(lower, false, 0);

        int n = matrix.Height;
        double scale = 0;

        for (int i = 0; i < n; i++) scale += Math.Abs(matrix[i, i]);

        scale = n > 0 ? scale / n : 0;

        if (!double.IsFinite(scale))
            throw new InvalidOperationException(NotPositiveDefinite + ": на диагонали не числа");

        // У нулевой матрицы масштаба нет: отсчёт идёт от единицы
        double shift = initialRelativeShift * (scale > 0 ? scale : 1);

        for (int attempt = 0; attempt < maxAttempts; attempt++, shift *= 10)
        {
            if (TryDecompose(matrix, shift, out lower))
                return new RegularizedCholesky(lower, true, shift);
        }

        throw new InvalidOperationException(
            NotPositiveDefinite + $" и не становится такой при сдвиге диагонали до {shift / 10:G3}");
    }

    /// <summary>
    /// Решает систему <c>A·x = b</c> с положительно определённой матрицей.
    /// </summary>
    /// <param name="matrix">Симметричная положительно определённая матрица.</param>
    /// <param name="rightHandSide">Правая часть.</param>
    /// <exception cref="InvalidOperationException">Матрица не является положительно определённой.</exception>
    public static Vector Solve(Matrix matrix, Vector rightHandSide) => SolveFactored(Decompose(matrix), rightHandSide);

    /// <summary>
    /// Решает систему по готовому множителю: прямая и обратная подстановки.
    /// </summary>
    /// <param name="lower">Множитель L из <see cref="Decompose"/>.</param>
    /// <param name="rightHandSide">Правая часть.</param>
    public static Vector SolveFactored(Matrix lower, Vector rightHandSide)
    {
        ArgumentNullException.ThrowIfNull(lower);
        ArgumentNullException.ThrowIfNull(rightHandSide);

        int n = lower.Height;

        if (rightHandSide.Count != n)
            throw new ArgumentException("Длина правой части не совпадает с порядком матрицы.", nameof(rightHandSide));

        var y = new Vector(n);

        for (int i = 0; i < n; i++)
        {
            double sum = rightHandSide[i];
            for (int j = 0; j < i; j++) sum -= lower[i, j] * y[j];
            y[i] = sum / lower[i, i];
        }

        var x = new Vector(n);

        for (int i = n - 1; i >= 0; i--)
        {
            double sum = y[i];
            for (int j = i + 1; j < n; j++) sum -= lower[j, i] * x[j];
            x[i] = sum / lower[i, i];
        }

        return x;
    }

    /// <summary>
    /// Обратная матрица по готовому множителю: <c>A⁻¹ = (L⁻¹)ᵀ·L⁻¹</c>.
    /// </summary>
    /// <param name="lower">Множитель L из <see cref="Decompose"/>.</param>
    public static Matrix InverseFromFactor(Matrix lower)
    {
        ArgumentNullException.ThrowIfNull(lower);

        int n = lower.Height;
        var inverseLower = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            inverseLower[i, i] = 1.0 / lower[i, i];

            for (int j = i + 1; j < n; j++)
            {
                double sum = 0;
                for (int k = i; k < j; k++) sum += lower[j, k] * inverseLower[k, i];
                inverseLower[j, i] = -sum / lower[j, j];
            }
        }

        var inverse = new Matrix(n, n);

        for (int i = 0; i < n; i++)
        {
            for (int j = i; j < n; j++)
            {
                double sum = 0;
                for (int k = j; k < n; k++) sum += inverseLower[k, i] * inverseLower[k, j];

                inverse[i, j] = sum;
                inverse[j, i] = sum;
            }
        }

        return inverse;
    }

    /// <summary>
    /// Логарифм определителя по готовому множителю: <c>ln|A| = 2·Σ ln Lᵢᵢ</c>.
    /// </summary>
    /// <param name="lower">Множитель L из <see cref="Decompose"/>.</param>
    public static double LogDeterminantFromFactor(Matrix lower)
    {
        ArgumentNullException.ThrowIfNull(lower);

        double sum = 0;

        for (int i = 0; i < lower.Height; i++) sum += Math.Log(lower[i, i]);

        return 2 * sum;
    }

    private static bool TryDecompose(Matrix matrix, double shift, out Matrix lower)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        int n = matrix.Height;

        if (n != matrix.Width)
            throw new ArgumentException("Матрица должна быть квадратной", nameof(matrix));

        lower = new Matrix(n, n);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = matrix[i, j];
                for (int k = 0; k < j; k++) sum -= lower[i, k] * lower[j, k];

                if (i == j)
                {
                    double diagonal = sum + shift;

                    // Ведущий элемент, от которого после вычитания остался один шум округления, — тоже отказ:
                    // у вырожденной матрицы он выходит то нулём, то 1e-17, и множитель был бы мусором.
                    // Сравнение записано так, чтобы не число тоже считалось отказом
                    double floor = n * 2.220446049250313e-16 * Math.Abs(matrix[i, i] + shift);

                    if (!(diagonal > floor) || double.IsPositiveInfinity(diagonal)) return false;

                    lower[i, i] = Math.Sqrt(diagonal);
                }
                else
                {
                    lower[i, j] = sum / lower[j, j];
                }
            }
        }

        return true;
    }
}
