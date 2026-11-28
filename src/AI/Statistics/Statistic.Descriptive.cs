using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AI.Statistics;

public partial class Statistic
{
    #region Математическое ожидание

    /// <summary>Оценка математического ожидания (NaN игнорируются).</summary>
    public static double ExpectedValue(IAlgebraicStructure<double> array)
    {
        var (mean, _, _) = StatUtils.MeanAndVariance(array.Data, skipNaN: true);
        return mean;
    }

    /// <summary>Оценка математического ожидания от |x|.</summary>
    public static double ExpectedValueAbs(IAlgebraicStructure<double> array)
    {
        double sum = 0.0;
        int n = 0;
        double[] data = array.Data;
        for (int i = 0; i < data.Length; i++)
        {
            if (!double.IsNaN(data[i]))
            {
                sum += Math.Abs(data[i]);
                n++;
            }
        }
        return n == 0 ? 0.0 : sum / n;
    }

    /// <summary>
    /// Оценка мат. ожидания без проверки NaN (быстрее, но падает на
    /// «грязных» данных).
    /// </summary>
    public static double ExpectedValueNotCheckNaN(IAlgebraicStructure<double> array)
    {
        double[] data = array.Data;
        if (data.Length == 0) return 0.0;

        double sum = 0.0;
        for (int i = 0; i < data.Length; i++) sum += data[i];
        return sum / data.Length;
    }

    /// <summary>Оценка мат. ожидания от |x| без проверки NaN.</summary>
    public static double ExpectedValueAbsNotCheckNaN(IAlgebraicStructure<double> array)
    {
        double[] data = array.Data;
        if (data.Length == 0) return 0.0;

        double sum = 0.0;
        for (int i = 0; i < data.Length; i++) sum += Math.Abs(data[i]);
        return sum / data.Length;
    }

    #endregion

    #region Дисперсия / СКО

    /// <summary>
    /// Дисперсия выборки: по умолчанию выборочная (делитель n − 1), генеральная — явным параметром.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Единственная реализация дисперсии во фреймворке (<see cref="StatUtils.MeanAndVariance"/>):
    /// два прохода с поправкой, точна и на данных с большим сдвигом, где формула E[x²] − E[x]² теряет все знаки.
    /// Совпадает с <c>numpy.var(x, ddof = 1)</c> и <c>pandas.Series.var()</c>; с параметром
    /// <paramref name="population"/> — с <c>numpy.var(x)</c>.
    /// </para>
    /// <para>
    /// Правила краёв одни для всех: значения <c>NaN</c> не входят в расчёт; выборка без значений
    /// и выборка из одного значения дают 0 (разброса нет).
    /// </para>
    /// </remarks>
    /// <param name="array">Выборка.</param>
    /// <param name="population">Генеральная дисперсия (делитель n) вместо выборочной (n − 1).</param>
    public static double CalcVariance(IAlgebraicStructure<double> array, bool population)
        => CalcVariance(array.Data.AsSpan(0, array.Shape.Count), population);

    /// <summary>Выборочная дисперсия (делитель n − 1); правила — как у перегрузки с параметром.</summary>
    /// <param name="array">Выборка.</param>
    public static double CalcVariance(IAlgebraicStructure<double> array) => CalcVariance(array, population: false);

    /// <inheritdoc cref="CalcVariance(IAlgebraicStructure{double}, bool)"/>
    /// <param name="data">Выборка.</param>
    /// <param name="population">Генеральная дисперсия (делитель n) вместо выборочной (n − 1).</param>
    public static double CalcVariance(ReadOnlySpan<double> data, bool population = false)
    {
        var (_, variance, _) = StatUtils.MeanAndVariance(data, skipNaN: true, unbiased: !population);
        return variance;
    }

    /// <summary>
    /// Старое название с кириллической «С». Оставлено для обратной
    /// совместимости — делегирует в <see cref="CalcVariance(IAlgebraicStructure{double})"/>.
    /// </summary>
    [Obsolete("Используйте CalcVariance (латинская С). Метод оставлен для обратной совместимости.", false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static double СalcVariance(IAlgebraicStructure<double> array)
        => CalcVariance(array);

    /// <summary>Среднеквадратичное отклонение — корень из дисперсии; правила те же.</summary>
    /// <param name="array">Выборка.</param>
    /// <param name="population">Генеральное (делитель n) вместо выборочного (n − 1).</param>
    public static double CalcStd(IAlgebraicStructure<double> array, bool population)
        => Math.Sqrt(CalcVariance(array, population));

    /// <summary>Выборочное среднеквадратичное отклонение (делитель n − 1).</summary>
    /// <param name="array">Выборка.</param>
    public static double CalcStd(IAlgebraicStructure<double> array) => CalcStd(array, population: false);

    /// <inheritdoc cref="CalcStd(IAlgebraicStructure{double}, bool)"/>
    /// <param name="data">Выборка.</param>
    /// <param name="population">Генеральное (делитель n) вместо выборочного (n − 1).</param>
    public static double CalcStd(ReadOnlySpan<double> data, bool population = false)
        => Math.Sqrt(CalcVariance(data, population));

    #endregion

    #region Ковариация и корреляция

    /// <summary>
    /// Ковариация двух выборок: по умолчанию выборочная (делитель n − 1), генеральная — параметром.
    /// </summary>
    /// <remarks>
    /// Два прохода с поправкой, как у дисперсии. Пары, где хотя бы одно значение <c>NaN</c>, не входят
    /// в расчёт; меньше двух пар — 0. Совпадает с <c>numpy.cov(x, y, ddof = 1)</c>.
    /// </remarks>
    /// <param name="xS">Первая выборка.</param>
    /// <param name="yS">Вторая выборка той же длины.</param>
    /// <param name="population">Генеральная ковариация (делитель n).</param>
    /// <exception cref="ArgumentException">Длины выборок различаются.</exception>
    public static double Cov(IAlgebraicStructure<double> xS, IAlgebraicStructure<double> yS, bool population)
        => Cov(xS.Data.AsSpan(0, xS.Shape.Count), yS.Data.AsSpan(0, yS.Shape.Count), population);

    /// <summary>Выборочная ковариация (делитель n − 1); правила — как у перегрузки с параметром.</summary>
    /// <param name="xS">Первая выборка.</param>
    /// <param name="yS">Вторая выборка той же длины.</param>
    public static double Cov(IAlgebraicStructure<double> xS, IAlgebraicStructure<double> yS) => Cov(xS, yS, population: false);

    /// <inheritdoc cref="Cov(IAlgebraicStructure{double}, IAlgebraicStructure{double}, bool)"/>
    /// <param name="x">Первая выборка.</param>
    /// <param name="y">Вторая выборка той же длины.</param>
    /// <param name="population">Генеральная ковариация (делитель n).</param>
    public static double Cov(ReadOnlySpan<double> x, ReadOnlySpan<double> y, bool population = false)
    {
        int n = x.Length;
        if (n != y.Length)
            throw new ArgumentException(
                $"Размеры векторов должны совпадать: {x.Length} vs {y.Length}", nameof(y));

        // Два прохода, как у дисперсии: средние, затем сумма произведений отклонений с поправкой
        // на остатки сумм отклонений
        double sx = 0, sy = 0;
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            if (double.IsNaN(x[i]) || double.IsNaN(y[i])) continue;
            k++;
            sx += x[i];
            sy += y[i];
        }

        if (k < 2) return 0.0;

        double mx = sx / k, my = sy / k;
        double c = 0, rx = 0, ry = 0;
        for (int i = 0; i < n; i++)
        {
            if (double.IsNaN(x[i]) || double.IsNaN(y[i])) continue;
            double dx = x[i] - mx, dy = y[i] - my;
            c += dx * dy;
            rx += dx;
            ry += dy;
        }

        c -= rx * ry / k;

        return k < 2 ? 0.0 : c / (population ? k : k - 1);
    }

    /// <summary>Коэффициент корреляции Пирсона двух векторов; считает <see cref="Pearson"/>.</summary>
    /// <exception cref="ArgumentException">Длины выборок различаются.</exception>
    public static double CorrelationCoefficient(Vector x, Vector y)
        => Pearson(((IAlgebraicStructure<double>)x).Data.AsSpan(0, x.Count), ((IAlgebraicStructure<double>)y).Data.AsSpan(0, y.Count));

    /// <summary>Коэффициент корреляции Пирсона по двум выборкам — сама реализация.</summary>
    /// <remarks>
    /// Пары с <c>NaN</c> не входят в расчёт; меньше двух пар или нулевой разброс одной из выборок — 0
    /// (связь не определена). Результат приведён к [−1; 1]. Совпадает с <c>numpy.corrcoef</c>.
    /// </remarks>
    /// <param name="x">Первая выборка.</param>
    /// <param name="y">Вторая выборка той же длины.</param>
    /// <exception cref="ArgumentException">Длины выборок различаются.</exception>
    public static double Pearson(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        if (x.Length != y.Length)
            throw new ArgumentException(
                $"Размеры векторов должны совпадать: {x.Length} vs {y.Length}", nameof(y));

        int n = x.Length;
        double mx = 0, my = 0;
        int k = 0;

        for (int i = 0; i < n; i++)
        {
            if (double.IsNaN(x[i]) || double.IsNaN(y[i])) continue;
            k++;
            mx += x[i];
            my += y[i];
        }

        if (k < 2) return 0.0;

        mx /= k; my /= k;
        double cor = 0, dx2 = 0, dy2 = 0;

        for (int i = 0; i < n; i++)
        {
            if (double.IsNaN(x[i]) || double.IsNaN(y[i])) continue;
            double dx = x[i] - mx;
            double dy = y[i] - my;
            cor += dx * dy;
            dx2 += dx * dx;
            dy2 += dy * dy;
        }

        double denom = Math.Sqrt(dx2 * dy2);
        if (denom < double.Epsilon) return 0.0;

        cor /= denom;
        if (cor > 1.0) cor = 1.0;
        if (cor < -1.0) cor = -1.0;
        return cor;
    }

    /// <summary>Коэффициент корреляции Пирсона (алгебр. структуры).</summary>
    public static double CorrelationCoefficient(
        IAlgebraicStructure<double> X, IAlgebraicStructure<double> Y)
        => Pearson(X.Data.AsSpan(0, X.Shape.Count), Y.Data.AsSpan(0, Y.Shape.Count));

    #endregion

    #region Асимметрия и эксцесс

    /// <summary>
    /// Коэффициент асимметрии выборки.
    /// </summary>
    /// <remarks>
    /// По умолчанию моментная оценка g₁ = m₃ / m₂^1.5 (моменты с делителем n) — то же, что
    /// <c>scipy.stats.skew(x)</c>. С параметром <paramref name="biasCorrected"/> — поправленная
    /// оценка G₁ = g₁·√(n(n − 1))/(n − 2), как в Excel, pandas и <c>scipy.stats.skew(x, bias = False)</c>.
    /// Значения <c>NaN</c> не входят в расчёт. Когда оценка не определена (нулевой разброс, меньше
    /// трёх значений для G₁), результат — <c>NaN</c>.
    /// </remarks>
    /// <param name="data">Выборка.</param>
    /// <param name="biasCorrected">Поправленная оценка G₁ вместо моментной g₁.</param>
    public static double Skewness(ReadOnlySpan<double> data, bool biasCorrected = false)
    {
        (int n, double m2, double m3, _) = CentralMoments(data);

        if (n == 0 || !(m2 > 0)) return double.NaN;

        double g1 = m3 / (m2 * Math.Sqrt(m2));

        if (!biasCorrected) return g1;

        return n < 3 ? double.NaN : g1 * Math.Sqrt((double)n * (n - 1)) / (n - 2);
    }

    /// <summary>
    /// Коэффициент эксцесса выборки (нормальное распределение даёт 0).
    /// </summary>
    /// <remarks>
    /// По умолчанию моментная оценка g₂ = m₄ / m₂² − 3 — то же, что <c>scipy.stats.kurtosis(x)</c>.
    /// С параметром <paramref name="biasCorrected"/> — поправленная оценка
    /// G₂ = (n − 1)/((n − 2)(n − 3))·((n + 1)·g₂ + 6), как в Excel, pandas и
    /// <c>scipy.stats.kurtosis(x, bias = False)</c>. Когда оценка не определена (нулевой разброс,
    /// меньше четырёх значений для G₂), результат — <c>NaN</c>.
    /// </remarks>
    /// <param name="data">Выборка.</param>
    /// <param name="biasCorrected">Поправленная оценка G₂ вместо моментной g₂.</param>
    public static double ExcessKurtosis(ReadOnlySpan<double> data, bool biasCorrected = false)
    {
        (int n, double m2, _, double m4) = CentralMoments(data);

        if (n == 0 || !(m2 > 0)) return double.NaN;

        double g2 = (m4 / (m2 * m2)) - 3;

        if (!biasCorrected) return g2;

        return n < 4 ? double.NaN : (n - 1.0) / ((n - 2.0) * (n - 3.0)) * (((n + 1) * g2) + 6);
    }

    /// <summary>Число значений и центральные моменты второго, третьего и четвёртого порядка (делитель n).</summary>
    private static (int Count, double M2, double M3, double M4) CentralMoments(ReadOnlySpan<double> data)
    {
        (double mean, _, int n) = StatUtils.MeanAndVariance(data, skipNaN: true, unbiased: false);

        if (n == 0) return (0, 0, 0, 0);

        double m2 = 0, m3 = 0, m4 = 0;

        foreach (double value in data)
        {
            if (double.IsNaN(value)) continue;

            double d = value - mean, d2 = d * d;

            m2 += d2;
            m3 += d2 * d;
            m4 += d2 * d2;
        }

        return (n, m2 / n, m3 / n, m4 / n);
    }

    #endregion

    #region Нормировка и ранги

    /// <summary>
    /// Z-оценки: каждое значение минус среднее, делённое на среднеквадратичное отклонение.
    /// </summary>
    /// <remarks>
    /// Единственная реализация z-нормировки. Отклонение по умолчанию выборочное (n − 1), как и
    /// <see cref="CalcStd(ReadOnlySpan{double}, bool)"/>. Правило нулевого разброса одно: у постоянной
    /// выборки все z-оценки равны нулю — каждое значение совпадает со средним, и делить нечего.
    /// <c>NaN</c> в расчёт среднего и отклонения не входит и остаётся <c>NaN</c> на своём месте.
    /// </remarks>
    /// <param name="data">Выборка.</param>
    /// <param name="population">Генеральное отклонение (делитель n).</param>
    public static double[] ZScores(ReadOnlySpan<double> data, bool population = false)
    {
        (double mean, double variance, _) = StatUtils.MeanAndVariance(data, skipNaN: true, unbiased: !population);
        double std = Math.Sqrt(variance);
        var result = new double[data.Length];

        for (int i = 0; i < data.Length; i++)
        {
            result[i] = double.IsNaN(data[i]) ? double.NaN : std > 0 ? (data[i] - mean) / std : 0.0;
        }

        return result;
    }

    /// <summary>
    /// Нормировка на отрезок [0; 1]: (x − min)/(max − min).
    /// </summary>
    /// <remarks>
    /// Единственная реализация. Правило нулевого размаха одно: у постоянной выборки все значения
    /// переходят в 0. <c>NaN</c> в поиск границ не входит и остаётся <c>NaN</c> на своём месте.
    /// </remarks>
    /// <param name="data">Выборка.</param>
    public static double[] MinMaxScale(ReadOnlySpan<double> data)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;

        foreach (double value in data)
        {
            if (double.IsNaN(value)) continue;
            if (value < min) min = value;
            if (value > max) max = value;
        }

        double span = max - min;
        var result = new double[data.Length];

        for (int i = 0; i < data.Length; i++)
        {
            result[i] = double.IsNaN(data[i]) ? double.NaN : span > 0 ? (data[i] - min) / span : 0.0;
        }

        return result;
    }

    /// <summary>
    /// Ранги значений от единицы; равные значения получают средний ранг своей группы.
    /// </summary>
    /// <remarks>
    /// То же, что <c>scipy.stats.rankdata(x, method = "average")</c>. На этих рангах стоят корреляция
    /// Спирмена, критерии Манна — Уитни и Уилкоксона, площадь под ROC-кривой.
    /// </remarks>
    /// <param name="data">Выборка.</param>
    /// <exception cref="ArgumentException">В выборке есть <c>NaN</c>: у него нет места в порядке.</exception>
    public static double[] AverageRanks(ReadOnlySpan<double> data)
    {
        int n = data.Length;
        double[] keys = data.ToArray();
        int[] order = new int[n];

        for (int i = 0; i < n; i++)
        {
            if (double.IsNaN(keys[i]))
                throw new ArgumentException("Ранги не определены для выборки с NaN.", nameof(data));

            order[i] = i;
        }

        Array.Sort(keys, order);

        var ranks = new double[n];

        for (int start = 0; start < n;)
        {
            int end = start;

            while (end + 1 < n && keys[end + 1] == keys[start]) end++;

            double rank = ((start + end) / 2.0) + 1;

            for (int k = start; k <= end; k++) ranks[order[k]] = rank;

            start = end + 1;
        }

        return ranks;
    }

    #endregion

    #region Гистограмма

    /// <summary>
    /// Гистограмма счётчиками: границы равных интервалов от минимума до максимума и число значений в каждом.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Единственная реализация разбиения по интервалам; то же, что <c>numpy.histogram(x, bins)</c>.
    /// Интервалы полуоткрытые <c>[a; b)</c>, последний закрыт справа, так что максимум попадает в него.
    /// Значения <c>NaN</c> не считаются. Плотность получается делением счётчиков на
    /// <c>n · ширина</c>.
    /// </para>
    /// <para>
    /// У выборки без размаха (и у пустой) интервалы строятся на отрезке шириной 1 вокруг значения —
    /// как в numpy: единственное значение попадает в средний интервал.
    /// </para>
    /// </remarks>
    /// <param name="data">Выборка.</param>
    /// <param name="bins">Число интервалов, не меньше одного.</param>
    /// <returns>Границы (на одну больше, чем интервалов) и счётчики.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Интервалов меньше одного.</exception>
    public static (double[] Edges, double[] Counts) HistogramCounts(ReadOnlySpan<double> data, int bins)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bins, 1);

        double min = double.PositiveInfinity, max = double.NegativeInfinity;

        foreach (double value in data)
        {
            if (double.IsNaN(value)) continue;
            if (value < min) min = value;
            if (value > max) max = value;
        }

        if (min > max) (min, max) = (0, 0);
        if (min == max) (min, max) = (min - 0.5, max + 0.5);

        var edges = new double[bins + 1];
        var counts = new double[bins];
        double width = max - min;

        for (int i = 0; i <= bins; i++) edges[i] = min + (width * i / bins);

        edges[bins] = max;

        foreach (double value in data)
        {
            if (double.IsNaN(value)) continue;

            int bin = Math.Clamp((int)((value - min) / width * bins), 0, bins - 1);

            // Деление могло округлить номер через границу: он сверяется с самими границами
            if (value < edges[bin]) bin--;
            else if (bin < bins - 1 && value >= edges[bin + 1]) bin++;

            counts[bin]++;
        }

        return (edges, counts);
    }

    /// <summary>
    /// Строит нормированную гистограмму (площадь под ней = 1)
    /// за один проход. Диапазоны бинов — полуинтервалы [a; b), кроме
    /// последнего, который [a; b] (чтобы max попал в последний бин).
    /// NaN-элементы в бины не попадают и в нормировке не участвуют.
    /// Для константных данных (max == min) возвращается один бин,
    /// центрированный на значении, с номинальной шириной 1.0 и
    /// плотностью 1.0 (площадь ровно 1). Для пустого входа или
    /// выборки из одних NaN возвращается вырожденная гистограмма
    /// (все Y = 0).
    /// </summary>
    public Histogramm Histogramm(int bins)
    {
        if (bins <= 0) throw new ArgumentOutOfRangeException(nameof(bins));

        Histogramm h = new Histogramm(bins);

        // Пустой вход — вырожденная гистограмма (все Y = 0).
        if (_n == 0)
        {
            for (int i = 0; i < bins; i++) h.X[i] = MinValue;
            return h;
        }

        if (MinValue == MaxValue)
        {
            int nonNaN = 0;
            for (int j = 0; j < _n; j++)
                if (!double.IsNaN(_vector[j])) nonNaN++;

            // Все элементы NaN — бинить нечего, возвращаем вырожденную
            // гистограмму (все Y = 0), как для пустого входа.
            if (nonNaN == 0)
            {
                for (int i = 0; i < bins; i++) h.X[i] = MinValue;
                return h;
            }

            // Константные данные: один бин, центрированный на значении,
            // с номинальной шириной 1.0. X — левая граница бина (как и в
            // общем случае), плотность = 1/ширина, поэтому площадь под
            // гистограммой равна ровно 1.
            const double nominalWidth = 1.0;
            Histogramm single = new Histogramm(1);
            single.X[0] = MinValue - (nominalWidth / 2.0);
            single.Y[0] = 1.0 / nominalWidth;
            return single;
        }

        // Разбиение и счёт — общие (HistogramCounts); здесь остаётся только нормировка по площади
        (double[] edges, double[] counts) = HistogramCounts(((IAlgebraicStructure<double>)_vector).Data.AsSpan(0, _n), bins);
        double step = (MaxValue - MinValue) / bins;
        int binned = 0; // число элементов, реально попавших в бины

        for (int i = 0; i < bins; i++)
        {
            h.X[i] = edges[i]; // левая граница интервала
            h.Y[i] = counts[i];
            binned += (int)counts[i];
        }

        // Ни один элемент не попал в бины — возвращаем нулевую
        // гистограмму, не деля на ноль.
        if (binned == 0) return h;

        // нормировка по площади: sum(Y) * step = 1. NaN-элементы в бины
        // не попадают, поэтому делим на число забинованных элементов,
        // а не на общий размер выборки.
        double scale = 1.0 / (binned * step);
        for (int i = 0; i < bins; i++) h.Y[i] *= scale;

        return h;
    }

    #endregion

    #region Моменты

    /// <summary>Начальный момент порядка n.</summary>
    public double InitialMoment(int n)
        => ExpectedValue(_vector.Transform(x => Math.Pow(x, n)));

    /// <summary>Центральный момент порядка n.</summary>
    public double CentralMoment(int n)
        => ExpectedValue((_vector - Expected).Transform(x => Math.Pow(x, n)));

    /// <summary>Асимметрия распределения.</summary>
    [Obsolete("Используйте Statistic.Skewness(data, biasCorrected): оценщик назван явно.")]
    public double Asymmetry()
        => Skewness(((IAlgebraicStructure<double>)_vector).Data.AsSpan(0, _vector.Count));

    /// <summary>Эксцесс распределения.</summary>
    [Obsolete("Используйте Statistic.ExcessKurtosis(data, biasCorrected): оценщик назван явно.")]
    public double Excess()
        => ExcessKurtosis(((IAlgebraicStructure<double>)_vector).Data.AsSpan(0, _vector.Count));

    #endregion
}
