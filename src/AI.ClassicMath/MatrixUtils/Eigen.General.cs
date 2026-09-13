using AI.DataStructs.Algebraic;
using System;
using System.Linq;
using Complex = System.Numerics.Complex;

namespace AI.ClassicMath.MatrixUtils;

public static partial class Eigen
{
    private const int MaxShiftsPerEigenvalue = 60;

    /// <summary>
    /// Собственные значения произвольной вещественной квадратной матрицы, в том числе комплексные
    /// </summary>
    /// <remarks>
    /// <para>
    /// Классическая схема EISPACK: балансировка степенями двойки, приведение к форме Хессенберга
    /// исключением Гаусса с выбором главного элемента и QR-итерации с двойным сдвигом Фрэнсиса.
    /// Комплексные значения вещественной матрицы идут сопряжёнными парами.
    /// </para>
    /// <para>
    /// Прежний <see cref="EigenValuesVectors"/> делает QR-итерации без сдвигов с грубым порогом и
    /// возвращает только вещественные числа, поэтому комплексные полюса — обычное дело в теории
    /// управления — терял. Для симметричных матриц точнее и дешевле <see cref="Symmetric"/>.
    /// </para>
    /// </remarks>
    /// <param name="matrix">Квадратная матрица</param>
    /// <returns>Собственные значения по убыванию вещественной части</returns>
    /// <exception cref="InvalidOperationException">Итерации не сошлись</exception>
    public static Complex[] General(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        EnsureSquare(matrix, nameof(matrix));

        int n = matrix.Height;

        if (n == 0)
            return [];

        // Индексы с единицы: алгоритм переписан из EISPACK/Numerical Recipes без перестановки индексов
        var a = new double[n + 1, n + 1];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double value = matrix[i, j];

                if (!double.IsFinite(value))
                    throw new ArgumentException("Матрица содержит бесконечные или неопределённые элементы", nameof(matrix));

                a[i + 1, j + 1] = value;
            }
        }

        Balance(a, n);
        ReduceToHessenberg(a, n);

        for (int i = 3; i <= n; i++)
        {
            for (int j = 1; j <= i - 2; j++)
                a[i, j] = 0;
        }

        var real = new double[n + 1];
        var imaginary = new double[n + 1];

        HessenbergQr(a, n, real, imaginary);

        return Enumerable.Range(1, n)
            .Select(i => new Complex(real[i], imaginary[i]))
            .OrderByDescending(z => z.Real)
            .ThenByDescending(z => z.Imaginary)
            .ToArray();
    }

    /// <summary>Спектральный радиус — наибольший модуль собственного значения</summary>
    /// <param name="matrix">Квадратная матрица</param>
    public static double SpectralRadius(Matrix matrix)
        => General(matrix).Select(Complex.Abs).DefaultIfEmpty(0).Max();

    // Уравнивание норм строк и столбцов степенями двойки: без ошибок округления, но с меньшей чувствительностью
    private static void Balance(double[,] a, int n)
    {
        const double Radix = 2;
        const double SquaredRadix = Radix * Radix;
        bool done = false;

        while (!done)
        {
            done = true;

            for (int i = 1; i <= n; i++)
            {
                double column = 0, row = 0;

                for (int j = 1; j <= n; j++)
                {
                    if (j == i)
                        continue;

                    column += Math.Abs(a[j, i]);
                    row += Math.Abs(a[i, j]);
                }

                if (column == 0 || row == 0)
                    continue;

                double g = row / Radix;
                double f = 1;
                double s = column + row;

                while (column < g)
                {
                    f *= Radix;
                    column *= SquaredRadix;
                }

                g = row * Radix;

                while (column > g)
                {
                    f /= Radix;
                    column /= SquaredRadix;
                }

                if ((column + row) / f < 0.95 * s)
                {
                    done = false;
                    g = 1 / f;

                    for (int j = 1; j <= n; j++)
                        a[i, j] *= g;

                    for (int j = 1; j <= n; j++)
                        a[j, i] *= f;
                }
            }
        }
    }

    // Приведение к верхней форме Хессенберга исключением с выбором главного элемента
    private static void ReduceToHessenberg(double[,] a, int n)
    {
        for (int m = 2; m < n; m++)
        {
            double x = 0;
            int pivot = m;

            for (int j = m; j <= n; j++)
            {
                if (Math.Abs(a[j, m - 1]) > Math.Abs(x))
                {
                    x = a[j, m - 1];
                    pivot = j;
                }
            }

            if (pivot != m)
            {
                for (int j = m - 1; j <= n; j++)
                    (a[pivot, j], a[m, j]) = (a[m, j], a[pivot, j]);

                for (int j = 1; j <= n; j++)
                    (a[j, pivot], a[j, m]) = (a[j, m], a[j, pivot]);
            }

            if (x == 0)
                continue;

            for (int i = m + 1; i <= n; i++)
            {
                double y = a[i, m - 1];

                if (y == 0)
                    continue;

                y /= x;
                a[i, m - 1] = y;

                for (int j = m; j <= n; j++)
                    a[i, j] -= y * a[m, j];

                for (int j = 1; j <= n; j++)
                    a[j, m] += y * a[j, i];
            }
        }
    }

    // QR-итерации с двойным сдвигом Фрэнсиса для матрицы Хессенберга
    private static void HessenbergQr(double[,] a, int n, double[] wr, double[] wi)
    {
        double norm = 0;

        for (int i = 1; i <= n; i++)
        {
            for (int j = Math.Max(i - 1, 1); j <= n; j++)
                norm += Math.Abs(a[i, j]);
        }

        int nn = n;
        double t = 0;
        double p = 0, q = 0, r = 0, s, w, x, y, z = 0;

        while (nn >= 1)
        {
            int iterations = 0;
            int l;

            do
            {
                for (l = nn; l >= 2; l--)
                {
                    s = Math.Abs(a[l - 1, l - 1]) + Math.Abs(a[l, l]);

                    if (s == 0)
                        s = norm;

                    if (Math.Abs(a[l, l - 1]) + s == s)
                    {
                        a[l, l - 1] = 0;
                        break;
                    }
                }

                x = a[nn, nn];

                if (l == nn)
                {
                    wr[nn] = x + t;
                    wi[nn--] = 0;
                    continue;
                }

                y = a[nn - 1, nn - 1];
                w = a[nn, nn - 1] * a[nn - 1, nn];

                if (l == nn - 1)
                {
                    p = 0.5 * (y - x);
                    q = (p * p) + w;
                    z = Math.Sqrt(Math.Abs(q));
                    x += t;

                    if (q >= 0)
                    {
                        z = p + Sign(z, p);
                        wr[nn - 1] = wr[nn] = x + z;

                        if (z != 0)
                            wr[nn] = x - (w / z);

                        wi[nn - 1] = wi[nn] = 0;
                    }
                    else
                    {
                        wr[nn - 1] = wr[nn] = x + p;
                        wi[nn - 1] = -(wi[nn] = z);
                    }

                    nn -= 2;
                    continue;
                }

                if (iterations == MaxShiftsPerEigenvalue)
                    throw new InvalidOperationException("QR-итерации для собственных значений не сошлись");

                // Исключительный сдвиг, если обычные не помогают
                if (iterations == 10 || iterations == 20)
                {
                    t += x;

                    for (int i = 1; i <= nn; i++)
                        a[i, i] -= x;

                    s = Math.Abs(a[nn, nn - 1]) + Math.Abs(a[nn - 1, nn - 2]);
                    y = x = 0.75 * s;
                    w = -0.4375 * s * s;
                }

                iterations++;

                int m;

                for (m = nn - 2; m >= l; m--)
                {
                    z = a[m, m];
                    r = x - z;
                    s = y - z;
                    p = (((r * s) - w) / a[m + 1, m]) + a[m, m + 1];
                    q = a[m + 1, m + 1] - z - r - s;
                    r = a[m + 2, m + 1];
                    s = Math.Abs(p) + Math.Abs(q) + Math.Abs(r);
                    p /= s;
                    q /= s;
                    r /= s;

                    if (m == l)
                        break;

                    double u = Math.Abs(a[m, m - 1]) * (Math.Abs(q) + Math.Abs(r));
                    double v = Math.Abs(p) * (Math.Abs(a[m - 1, m - 1]) + Math.Abs(z) + Math.Abs(a[m + 1, m + 1]));

                    if (u + v == v)
                        break;
                }

                for (int i = m + 2; i <= nn; i++)
                {
                    a[i, i - 2] = 0;

                    if (i != m + 2)
                        a[i, i - 3] = 0;
                }

                for (int k = m; k <= nn - 1; k++)
                {
                    if (k != m)
                    {
                        p = a[k, k - 1];
                        q = a[k + 1, k - 1];
                        r = k != nn - 1 ? a[k + 2, k - 1] : 0;
                        x = Math.Abs(p) + Math.Abs(q) + Math.Abs(r);

                        if (x != 0)
                        {
                            p /= x;
                            q /= x;
                            r /= x;
                        }
                    }

                    s = Sign(Math.Sqrt((p * p) + (q * q) + (r * r)), p);

                    if (s == 0)
                        continue;

                    if (k == m)
                    {
                        if (l != m)
                            a[k, k - 1] = -a[k, k - 1];
                    }
                    else
                    {
                        a[k, k - 1] = -s * x;
                    }

                    p += s;
                    x = p / s;
                    y = q / s;
                    z = r / s;
                    q /= p;
                    r /= p;

                    for (int j = k; j <= nn; j++)
                    {
                        p = a[k, j] + (q * a[k + 1, j]);

                        if (k != nn - 1)
                        {
                            p += r * a[k + 2, j];
                            a[k + 2, j] -= p * z;
                        }

                        a[k + 1, j] -= p * y;
                        a[k, j] -= p * x;
                    }

                    int last = nn < k + 3 ? nn : k + 3;

                    for (int i = l; i <= last; i++)
                    {
                        p = (x * a[i, k]) + (y * a[i, k + 1]);

                        if (k != nn - 1)
                        {
                            p += z * a[i, k + 2];
                            a[i, k + 2] -= p * r;
                        }

                        a[i, k + 1] -= p * q;
                        a[i, k] -= p;
                    }
                }
            }
            while (l < nn - 1);
        }
    }

    private static double Sign(double magnitude, double sign) => sign >= 0 ? Math.Abs(magnitude) : -Math.Abs(magnitude);
}
