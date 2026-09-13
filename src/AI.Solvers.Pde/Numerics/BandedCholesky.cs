using AI.DataStructs.Algebraic;

namespace AI.Solvers.Pde.Numerics;

/// <summary>
/// Разложение Холецкого симметричной положительно определённой ленточной матрицы.
/// </summary>
/// <remarks>
/// <para>
/// Заполнение при разложении не выходит за ленту, поэтому память — <c>n·(p+1)</c>, разложение —
/// порядка <c>n·p²</c>, решение — две подстановки за <c>2·n·p</c>, где <c>p</c> — полуширина ленты.
/// Для пятиточечного шаблона на сетке m×m полуширина равна m: разложение стоит m⁴, решение — 2m³.
/// </para>
/// <para>
/// Разложение окупается, когда одна и та же матрица решается много раз — например, на каждом шаге
/// по времени. Для одной системы метод сопряжённых градиентов обычно дешевле. Ширину ленты задаёт
/// нумерация неизвестных: на треугольной сетке с произвольной нумерацией лента может оказаться
/// почти полной, и тогда разложение теряет смысл.
/// </para>
/// </remarks>
public sealed class BandedCholesky
{
    // Строка i хранит L[i, i−p..i]; элементы левее нулевого столбца не используются
    private readonly double[] _band;

    private BandedCholesky(int size, int halfBandwidth, double[] band)
    {
        Size = size;
        HalfBandwidth = halfBandwidth;
        _band = band;
    }

    /// <summary>Размер матрицы</summary>
    public int Size { get; }

    /// <summary>Полуширина ленты: наибольшее |i − j| среди ненулевых элементов</summary>
    public int HalfBandwidth { get; }

    /// <summary>Раскладывает матрицу: A = L·Lᵀ</summary>
    /// <param name="matrix">Симметричная положительно определённая матрица</param>
    /// <exception cref="ArgumentException">Матрица не квадратная или несимметричная</exception>
    /// <exception cref="InvalidOperationException">Матрица не положительно определена</exception>
    public static BandedCholesky Factor(SparseMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (matrix.Rows != matrix.Columns)
            throw new ArgumentException("Матрица должна быть квадратной", nameof(matrix));

        int n = matrix.Rows;
        int p = 0;

        foreach ((int row, int column, double value) in matrix.NonZeros())
        {
            p = Math.Max(p, Math.Abs(row - column));

            if (column != row && Math.Abs(matrix[column, row] - value) > 1e-12 * Math.Abs(value))
                throw new ArgumentException($"Матрица несимметрична: элементы ({row}; {column}) и ({column}; {row}) различаются", nameof(matrix));
        }

        int width = p + 1;
        var band = new double[n * width];

        foreach ((int row, int column, double value) in matrix.NonZeros())
        {
            if (column <= row)
                band[(row * width) + column - row + p] = value;
        }

        for (int i = 0; i < n; i++)
        {
            int first = Math.Max(0, i - p);

            for (int j = first; j <= i; j++)
            {
                double sum = band[(i * width) + j - i + p];

                for (int k = Math.Max(first, j - p); k < j; k++)
                    sum -= band[(i * width) + k - i + p] * band[(j * width) + k - j + p];

                if (j < i)
                {
                    band[(i * width) + j - i + p] = sum / band[(j * width) + p];
                }
                else if (sum > 0)
                {
                    band[(i * width) + p] = Math.Sqrt(sum);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Матрица не положительно определена: ведущий минор порядка {i + 1} неположителен");
                }
            }
        }

        return new BandedCholesky(n, p, band);
    }

    /// <summary>Решает A·x = b</summary>
    /// <param name="rightHandSide">Правая часть</param>
    public Vector Solve(Vector rightHandSide)
    {
        ArgumentNullException.ThrowIfNull(rightHandSide);

        var solution = new double[Size];
        Solve(rightHandSide.ToArray(), solution);

        return new Vector(solution);
    }

    /// <summary>Решает A·x = b без выделения памяти</summary>
    /// <param name="rightHandSide">Правая часть</param>
    /// <param name="solution">Буфер для решения; может совпадать с правой частью</param>
    public void Solve(ReadOnlySpan<double> rightHandSide, Span<double> solution)
    {
        if (rightHandSide.Length != Size || solution.Length != Size)
            throw new ArgumentException("Длина векторов не совпадает с размером матрицы", nameof(rightHandSide));

        int p = HalfBandwidth, width = p + 1;
        rightHandSide.CopyTo(solution);

        // L·y = b
        for (int i = 0; i < Size; i++)
        {
            double sum = solution[i];

            for (int k = Math.Max(0, i - p); k < i; k++)
                sum -= _band[(i * width) + k - i + p] * solution[k];

            solution[i] = sum / _band[(i * width) + p];
        }

        // Lᵀ·x = y
        for (int i = Size - 1; i >= 0; i--)
        {
            double sum = solution[i];
            int last = Math.Min(Size - 1, i + p);

            for (int k = i + 1; k <= last; k++)
                sum -= _band[(k * width) + i - k + p] * solution[k];

            solution[i] = sum / _band[(i * width) + p];
        }
    }

    /// <summary>Краткое описание разложения</summary>
    public override string ToString() => $"Холецкий, {Size}×{Size}, полуширина ленты {HalfBandwidth}";
}
