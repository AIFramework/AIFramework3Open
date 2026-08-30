using System;
using AI.Statistics;

namespace AI.Econometrics.Numerics;

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
    /// <summary>Натуральный логарифм гамма-функции.</summary>
    /// <remarks>
    /// Реализация одна на весь репозиторий — <see cref="StatInference.LogGamma"/>.
    /// Здесь остаётся только имя, привычное вызывающему коду.
    /// </remarks>
    /// <param name="x">Аргумент, x &gt; 0.</param>
    public static double LogGamma(double x) => StatInference.LogGamma(x);

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
    public static double NormalPdf(double x) => StatInference.NormalPdf(x);

    /// <summary>Функция распределения стандартного нормального закона.</summary>
    public static double NormalCdf(double x) => StatInference.NormalCdf(x);

    /// <summary>Функция ошибок.</summary>
    public static double Erf(double x) => StatInference.Erf(x);

    /// <summary>Квантиль стандартного нормального распределения.</summary>
    /// <remarks>
    /// Алгоритм Acklam переехал в <see cref="StatInference.NormalQuantile"/>: он точнее
    /// прежней рациональной аппроксимации ядра, поэтому реализация осталась одна.
    /// </remarks>
    /// <param name="p">Уровень вероятности из интервала (0; 1).</param>
    public static double NormalInv(double p) => StatInference.NormalQuantile(p);

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
