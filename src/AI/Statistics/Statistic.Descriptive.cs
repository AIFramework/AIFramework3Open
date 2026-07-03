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
        var (mean, _, _) = StatUtils.Welford(array.Data, skipNaN: true);
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
    /// Оценка несмещённой дисперсии (NaN игнорируются, Welford).
    /// </summary>
    public static double CalcVariance(IAlgebraicStructure<double> array)
    {
        var (_, variance, _) = StatUtils.Welford(array.Data, skipNaN: true, unbiased: true);
        return variance;
    }

    /// <summary>
    /// Старое название с кириллической «С». Оставлено для обратной
    /// совместимости — делегирует в <see cref="CalcVariance"/>.
    /// </summary>
    [Obsolete("Используйте CalcVariance (латинская С). Метод оставлен для обратной совместимости.", false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static double СalcVariance(IAlgebraicStructure<double> array)
        => CalcVariance(array);

    /// <summary>СКО (корень из <see cref="CalcVariance"/>).</summary>
    public static double CalcStd(IAlgebraicStructure<double> array)
        => Math.Sqrt(CalcVariance(array));

    #endregion

    #region Ковариация и корреляция

    /// <summary>
    /// Ковариация двух векторов (NaN-записи в любой паре игнорируются).
    /// Один проход, Welford-подобная устойчивая форма.
    /// </summary>
    public static double Cov(IAlgebraicStructure<double> xS, IAlgebraicStructure<double> yS)
    {
        Vector x = xS.Data;
        Vector y = yS.Data;
        int n = x.Count;
        if (n != y.Count)
            throw new ArgumentException(
                $"Размеры векторов должны совпадать: {x.Count} vs {y.Count}", nameof(yS));

        double mx = 0, my = 0, c = 0;
        int k = 0;
        for (int i = 0; i < n; i++)
        {
            double xi = x[i], yi = y[i];
            if (double.IsNaN(xi) || double.IsNaN(yi)) continue;
            k++;
            double dx = xi - mx;
            mx += dx / k;
            double dy = yi - my;
            my += dy / k;
            c += dx * (yi - my);
        }

        return k < 2 ? 0.0 : c / (k - 1);
    }

    /// <summary>Коэффициент корреляции Пирсона (NaN игнорируются).</summary>
    public static double CorrelationCoefficient(Vector x, Vector y)
    {
        if (x.Count != y.Count)
            throw new ArgumentException(
                $"Размеры векторов должны совпадать: {x.Count} vs {y.Count}", nameof(y));

        int n = x.Count;
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
        => CorrelationCoefficient(X.Data, Y.Data);

    #endregion

    #region Гистограмма

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

        double step = (MaxValue - MinValue) / bins;
        double invStep = 1.0 / step;

        int binned = 0; // число элементов, реально попавших в бины
        for (int j = 0; j < _n; j++)
        {
            double v = _vector[j];
            if (double.IsNaN(v)) continue;

            int idx = (int)((v - MinValue) * invStep);
            if (idx == bins) idx = bins - 1; // max попадает в последний бин
            if (idx < 0 || idx >= bins) continue;
            h.Y[idx]++;
            binned++;
        }

        // позиции центров бинов — левая граница (как в исходном коде)
        for (int i = 0; i < bins; i++)
            h.X[i] = MinValue + (i * step);

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
    public double Asymmetry()
        => STD == 0 ? 0 : CentralMoment(3) / (STD * STD * STD);

    /// <summary>Эксцесс распределения (CM(4)/σ^4 − 3).</summary>
    public double Excess()
        => STD == 0 ? 0 : (CentralMoment(4) / (STD * STD * STD * STD)) - 3;

    #endregion
}
