using System;

namespace AI.Economics.Numerics;

/// <summary>
/// Специальные функции и линейная алгебра, нужные экономическим моделям.
/// </summary>
/// <remarks>
/// Класс внутренний: наружу торчат доменные модели, а не математика.
/// Вынесен отдельно, потому что одни и те же функции нужны сразу нескольким
/// моделям — <c>LogGamma</c> для BG/NBD и Gamma-Gamma, <c>Hyp2F1</c> для
/// BG/NBD и Pareto/NBD, нормальное распределение для реальных опционов и
/// доверительных интервалов Каплана — Мейера.
/// </remarks>
internal static class EconMath
{
    /// <summary>Коэффициенты аппроксимации Ланцоша (g = 7, n = 9).</summary>
    private static readonly double[] LanczosG7 =
    [
        0.99999999999980993,
        676.5203681218851,
        -1259.1392167224028,
        771.32342877765313,
        -176.61502916214059,
        12.507343278686905,
        -0.13857109526572012,
        9.9843695780195716e-6,
        1.5056327351493116e-7,
    ];

    /// <summary>Натуральный логарифм гамма-функции, аппроксимация Ланцоша.</summary>
    /// <param name="x">Аргумент, <c>x &gt; 0</c>.</param>
    /// <returns>Значение <c>ln Г(x)</c>.</returns>
    public static double LogGamma(double x)
    {
        if (x <= 0 || double.IsNaN(x)) return double.NaN;

        if (x < 0.5)
        {
            // Формула отражения: Г(x)Г(1-x) = pi / sin(pi x)
            return Math.Log(Math.PI / Math.Abs(Math.Sin(Math.PI * x))) - LogGamma(1.0 - x);
        }

        double z = x - 1.0;
        double a = LanczosG7[0];
        for (int i = 1; i < LanczosG7.Length; i++) a += LanczosG7[i] / (z + i);

        double t = z + 7.5;
        return (0.5 * Math.Log(2 * Math.PI)) + ((z + 0.5) * Math.Log(t)) - t + Math.Log(a);
    }

    /// <summary>Логарифм бета-функции <c>ln B(a, b)</c>.</summary>
    public static double LogBeta(double a, double b) => LogGamma(a) + LogGamma(b) - LogGamma(a + b);

    /// <summary>Дигамма-функция (логарифмическая производная гамма-функции).</summary>
    /// <param name="x">Аргумент, <c>x &gt; 0</c>.</param>
    public static double Digamma(double x)
    {
        double result = 0;

        // Рекуррентно поднимаем аргумент до области, где асимптотика точна
        while (x < 6)
        {
            result -= 1.0 / x;
            x += 1;
        }

        double inv = 1.0 / x;
        double inv2 = inv * inv;
        return result + Math.Log(x) - (0.5 * inv)
             - (inv2 * ((1.0 / 12) - (inv2 * ((1.0 / 120) - (inv2 / 252)))));
    }

    /// <summary>
    /// Гипергеометрическая функция Гаусса <c>2F1(a, b; c; z)</c> степенным рядом.
    /// </summary>
    /// <param name="a">Первый верхний параметр.</param>
    /// <param name="b">Второй верхний параметр.</param>
    /// <param name="c">Нижний параметр.</param>
    /// <param name="z">Аргумент, требуется <c>|z| &lt; 1</c>.</param>
    /// <remarks>
    /// В моделях BG/NBD и Pareto/NBD аргумент имеет вид <c>t / (a + T + t)</c>
    /// либо <c>(a - b) / (a + t)</c> и всегда лежит строго внутри круга
    /// сходимости, поэтому продолжения ряда не требуется.
    /// </remarks>
    public static double Hyp2F1(double a, double b, double c, double z)
    {
        if (Math.Abs(z) >= 1) return double.NaN;

        double term = 1.0;
        double sum = 1.0;

        for (int n = 0; n < 20000; n++)
        {
            term *= (a + n) * (b + n) / ((c + n) * (n + 1)) * z;
            sum += term;
            if (Math.Abs(term) < 1e-14 * Math.Abs(sum)) break;
        }

        return sum;
    }

    /// <summary>Плотность стандартного нормального распределения.</summary>
    public static double NormalPdf(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);

    /// <summary>Функция распределения стандартного нормального закона.</summary>
    public static double NormalCdf(double x) => 0.5 * (1.0 + Erf(x / Math.Sqrt(2)));

    /// <summary>Функция ошибок, аппроксимация Абрамовица — Стиган 7.1.26.</summary>
    public static double Erf(double x)
    {
        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        double t = 1.0 / (1.0 + (0.3275911 * x));
        double y = 1.0 - ((((((((1.061405429 * t) - 1.453152027) * t) + 1.421413741) * t)
                  - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x));

        return sign * y;
    }

    /// <summary>Квантиль стандартного нормального распределения (алгоритм Acklam).</summary>
    /// <param name="p">Уровень вероятности из интервала (0; 1).</param>
    public static double NormalInv(double p)
    {
        if (p <= 0) return double.NegativeInfinity;
        if (p >= 1) return double.PositiveInfinity;

        double[] a = [-3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02,
                       1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00];
        double[] b = [-5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02,
                       6.680131188771972e+01, -1.328068155288572e+01];
        double[] c = [-7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00,
                      -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00];
        double[] d = [7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00,
                      3.754408661907416e+00];

        const double PLow = 0.02425;
        double q, r;

        if (p < PLow)
        {
            q = Math.Sqrt(-2 * Math.Log(p));
            return ((((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q) + c[5]) /
                   ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        if (p <= 1 - PLow)
        {
            q = p - 0.5;
            r = q * q;
            return ((((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q) /
                   (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
        }

        q = Math.Sqrt(-2 * Math.Log(1 - p));
        return -((((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q) + c[5]) /
                ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
    }

    /// <summary>
    /// Решение системы <c>A x = b</c> методом Гаусса с выбором главного элемента.
    /// </summary>
    /// <returns>Вектор решения либо <c>null</c>, если матрица вырождена.</returns>
    public static double[]? SolveLinear(double[,] matrix, double[] rhs)
    {
        int n = rhs.Length;
        var a = (double[,])matrix.Clone();
        var b = (double[])rhs.Clone();

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < n; row++)
                if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col])) pivot = row;

            if (Math.Abs(a[pivot, col]) < 1e-14) return null;

            if (pivot != col)
            {
                for (int k = 0; k < n; k++) (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
                (b[col], b[pivot]) = (b[pivot], b[col]);
            }

            for (int row = col + 1; row < n; row++)
            {
                double f = a[row, col] / a[col, col];
                if (f == 0) continue;
                for (int k = col; k < n; k++) a[row, k] -= f * a[col, k];
                b[row] -= f * b[col];
            }
        }

        var x = new double[n];
        for (int row = n - 1; row >= 0; row--)
        {
            double s = b[row];
            for (int k = row + 1; k < n; k++) s -= a[row, k] * x[k];
            x[row] = s / a[row, row];
        }

        return x;
    }

    /// <summary>Обращение квадратной матрицы методом Гаусса — Жордана.</summary>
    /// <returns>Обратная матрица либо <c>null</c> для вырожденного случая.</returns>
    public static double[,]? Inverse(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        var a = (double[,])matrix.Clone();
        var inv = new double[n, n];
        for (int i = 0; i < n; i++) inv[i, i] = 1.0;

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < n; row++)
                if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col])) pivot = row;

            if (Math.Abs(a[pivot, col]) < 1e-14) return null;

            if (pivot != col)
                for (int k = 0; k < n; k++)
                {
                    (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
                    (inv[col, k], inv[pivot, k]) = (inv[pivot, k], inv[col, k]);
                }

            double diag = a[col, col];
            for (int k = 0; k < n; k++)
            {
                a[col, k] /= diag;
                inv[col, k] /= diag;
            }

            for (int row = 0; row < n; row++)
            {
                if (row == col) continue;
                double f = a[row, col];
                if (f == 0) continue;
                for (int k = 0; k < n; k++)
                {
                    a[row, k] -= f * a[col, k];
                    inv[row, k] -= f * inv[col, k];
                }
            }
        }

        return inv;
    }

    /// <summary>
    /// Выборочный квантиль по методу линейной интерполяции (тип 7, как в R и NumPy).
    /// </summary>
    /// <param name="sorted">Отсортированный по возрастанию массив.</param>
    /// <param name="q">Уровень квантиля из отрезка [0; 1].</param>
    public static double Quantile(double[] sorted, double q)
    {
        if (sorted.Length == 0) return double.NaN;
        if (sorted.Length == 1) return sorted[0];

        double pos = q * (sorted.Length - 1);
        int lo = (int)Math.Floor(pos);
        int hi = (int)Math.Ceiling(pos);
        if (lo < 0) lo = 0;
        if (hi > sorted.Length - 1) hi = sorted.Length - 1;

        // Равенство границ проверяется отдельно: массив может содержать
        // бесконечности (например, «деньги не кончились»), и разность
        // inf - inf дала бы NaN вместо самой бесконечности
        if (lo == hi || sorted[lo].Equals(sorted[hi])) return sorted[lo];

        return sorted[lo] + ((pos - lo) * (sorted[hi] - sorted[lo]));
    }

    /// <summary>Ограничение значения отрезком.</summary>
    public static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
}
