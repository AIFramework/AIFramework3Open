#nullable enable
using System;
using System.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.Geometry.Numerics;

/// <summary>
/// Точные геометрические предикаты на плоскости: ориентация тройки точек и положение точки относительно окружности.
/// </summary>
/// <remarks>
/// Знак вычисляется так, будто координаты заданы точно, без допуска. Сначала определитель считается в числах
/// с плавающей точкой, и если он по модулю больше оценки ошибки округления (фильтр Шевчука, стадия A), его знак
/// верен. Иначе определитель пересчитывается точно в целых числах: каждое число double есть мантисса, умноженная
/// на степень двойки, и после приведения к общему порядку все вычитания и умножения выполняются без округления.
/// Точный путь нужен только для почти вырожденных входов, поэтому средняя цена близка к обычному вычислению.
/// </remarks>
public static class RobustPredicates
{
    private const double Epsilon = 1.1102230246251565e-16; // 2^-53, половина машинного эпсилон
    private const double OrientBound = (3.0 + 16.0 * Epsilon) * Epsilon;
    private const double InCircleBound = (10.0 + 96.0 * Epsilon) * Epsilon;

    // Ниже этого порога произведения могут уйти в денормализованные числа, и оценка ошибки перестает быть верной
    private const double Tiny = 1e-280;

    /// <summary>
    /// Ориентация тройки точек: +1, если a, b, c обходятся против часовой стрелки, -1, если по часовой, 0, если лежат
    /// на одной прямой. Результат точен для любых конечных координат.
    /// </summary>
    /// <param name="a">Первая точка (2D).</param>
    /// <param name="b">Вторая точка (2D).</param>
    /// <param name="c">Третья точка (2D).</param>
    /// <returns>Знак определителя ориентации.</returns>
    public static int Orient2D(Vector a, Vector b, Vector c)
    {
        return Orient2D(a[0], a[1], b[0], b[1], c[0], c[1]);
    }

    /// <summary>
    /// Ориентация тройки точек, заданных координатами: +1 (против часовой стрелки), -1 (по часовой), 0 (на одной прямой).
    /// </summary>
    /// <param name="ax">Абсцисса первой точки.</param>
    /// <param name="ay">Ордината первой точки.</param>
    /// <param name="bx">Абсцисса второй точки.</param>
    /// <param name="by">Ордината второй точки.</param>
    /// <param name="cx">Абсцисса третьей точки.</param>
    /// <param name="cy">Ордината третьей точки.</param>
    /// <returns>Знак определителя ориентации.</returns>
    /// <exception cref="ArgumentException">Среди координат есть бесконечность или NaN.</exception>
    public static int Orient2D(double ax, double ay, double bx, double by, double cx, double cy)
    {
        double left = (ax - cx) * (by - cy);
        double right = (ay - cy) * (bx - cx);
        double det = left - right;
        double sum = Math.Abs(left) + Math.Abs(right);

        if (sum >= Tiny && double.IsFinite(sum) && Math.Abs(det) > OrientBound * sum)
            return Math.Sign(det);

        // Разность равна нулю только при точном равенстве чисел, поэтому нулевой множитель дает точный ноль произведения
        if ((ax == cx || by == cy) && (ay == cy || bx == cx))
            return 0;

        return ExactOrient(ax, ay, bx, by, cx, cy);
    }

    /// <summary>
    /// Положение точки d относительно окружности через a, b, c: +1 внутри, -1 снаружи, 0 на окружности
    /// (для a, b, c против часовой стрелки; при обходе по часовой знак меняется). Результат точен для любых конечных
    /// координат.
    /// </summary>
    /// <param name="a">Первая точка окружности (2D).</param>
    /// <param name="b">Вторая точка окружности (2D).</param>
    /// <param name="c">Третья точка окружности (2D).</param>
    /// <param name="d">Проверяемая точка (2D).</param>
    /// <returns>Знак определителя «в круге».</returns>
    public static int InCircle(Vector a, Vector b, Vector c, Vector d)
    {
        return InCircle(a[0], a[1], b[0], b[1], c[0], c[1], d[0], d[1]);
    }

    /// <summary>
    /// Положение точки (dx, dy) относительно окружности через три точки: +1 внутри, -1 снаружи, 0 на окружности
    /// (для обхода против часовой стрелки).
    /// </summary>
    /// <param name="ax">Абсцисса первой точки.</param>
    /// <param name="ay">Ордината первой точки.</param>
    /// <param name="bx">Абсцисса второй точки.</param>
    /// <param name="by">Ордината второй точки.</param>
    /// <param name="cx">Абсцисса третьей точки.</param>
    /// <param name="cy">Ордината третьей точки.</param>
    /// <param name="dx">Абсцисса проверяемой точки.</param>
    /// <param name="dy">Ордината проверяемой точки.</param>
    /// <returns>Знак определителя «в круге».</returns>
    /// <exception cref="ArgumentException">Среди координат есть бесконечность или NaN.</exception>
    public static int InCircle(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
    {
        double adx = ax - dx, ady = ay - dy;
        double bdx = bx - dx, bdy = by - dy;
        double cdx = cx - dx, cdy = cy - dy;

        double bdxcdy = bdx * cdy, cdxbdy = cdx * bdy;
        double cdxady = cdx * ady, adxcdy = adx * cdy;
        double adxbdy = adx * bdy, bdxady = bdx * ady;
        double alift = adx * adx + ady * ady;
        double blift = bdx * bdx + bdy * bdy;
        double clift = cdx * cdx + cdy * cdy;

        double det = alift * (bdxcdy - cdxbdy) + blift * (cdxady - adxcdy) + clift * (adxbdy - bdxady);
        double permanent = (Math.Abs(bdxcdy) + Math.Abs(cdxbdy)) * alift
                         + (Math.Abs(cdxady) + Math.Abs(adxcdy)) * blift
                         + (Math.Abs(adxbdy) + Math.Abs(bdxady)) * clift;

        if (permanent >= Tiny && double.IsFinite(permanent) && Math.Abs(det) > InCircleBound * permanent)
            return Math.Sign(det);

        return ExactInCircle(ax, ay, bx, by, cx, cy, dx, dy);
    }

    private static int ExactOrient(double ax, double ay, double bx, double by, double cx, double cy)
    {
        var v = ToIntegers(stackalloc double[] { ax, ay, bx, by, cx, cy });
        BigInteger det = (v[0] - v[4]) * (v[3] - v[5]) - (v[1] - v[5]) * (v[2] - v[4]);
        return det.Sign;
    }

    private static int ExactInCircle(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy)
    {
        var v = ToIntegers(stackalloc double[] { ax, ay, bx, by, cx, cy, dx, dy });
        BigInteger adx = v[0] - v[6], ady = v[1] - v[7];
        BigInteger bdx = v[2] - v[6], bdy = v[3] - v[7];
        BigInteger cdx = v[4] - v[6], cdy = v[5] - v[7];

        BigInteger det = (adx * adx + ady * ady) * (bdx * cdy - cdx * bdy)
                       + (bdx * bdx + bdy * bdy) * (cdx * ady - adx * cdy)
                       + (cdx * cdx + cdy * cdy) * (adx * bdy - bdx * ady);
        return det.Sign;
    }

    /// <summary>
    /// Переводит числа в целые с общим множителем 2^e (e есть наименьший порядок среди ненулевых чисел).
    /// Множитель положителен и одинаков для всех, поэтому знак любого однородного многочлена не меняется.
    /// </summary>
    private static BigInteger[] ToIntegers(ReadOnlySpan<double> values)
    {
        var mantissas = new long[values.Length];
        var exponents = new int[values.Length];
        int minExponent = int.MaxValue;

        for (int i = 0; i < values.Length; i++)
        {
            double value = values[i];
            if (!double.IsFinite(value))
                throw new ArgumentException("Координаты должны быть конечными числами", nameof(values));

            long bits = BitConverter.DoubleToInt64Bits(value);
            int biased = (int)((bits >> 52) & 0x7FF);
            long fraction = bits & 0xFFFFFFFFFFFFFL;
            if (biased == 0 && fraction == 0)
                continue;

            long mantissa = biased == 0 ? fraction : fraction | (1L << 52);
            int exponent = (biased == 0 ? 1 : biased) - 1075;
            mantissas[i] = bits < 0 ? -mantissa : mantissa;
            exponents[i] = exponent;
            minExponent = Math.Min(minExponent, exponent);
        }

        var result = new BigInteger[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            if (mantissas[i] != 0)
                result[i] = new BigInteger(mantissas[i]) << (exponents[i] - minExponent);
        }

        return result;
    }
}
