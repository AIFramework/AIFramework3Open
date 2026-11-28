using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using System;
using System.Collections.Generic;
using System.Linq;
using Complex = System.Numerics.Complex;

namespace AI.Distances;

/// <summary>
/// Базовые функции измерения расстояний
/// </summary>
[Serializable]
public static class BaseDist
{
    /// <summary>
    /// Lp семейство расстояний
    /// </summary>
    public static double LpDist(IAlgebraicStructure<double> A, IAlgebraicStructure<double> B, int p)
    {
        if (A.Shape.Count != B.Shape.Count)
        {
            throw new InvalidOperationException("Размерности не совпадают");
        }

        double res = 0, sempl;
        int len = A.Shape.Count;

        for (int i = 0; i < len; i++)
        {
            sempl = Math.Pow(A.Data[i] - B.Data[i], p);
            res += Math.Abs(sempl);
        }

        res = Math.Pow(res, 1.0 / p);

        return res;
    }
    /// <summary>
    /// Евклидово расстояние
    /// </summary>
    public static double EuclideanDistance(IAlgebraicStructure<double> A, IAlgebraicStructure<double> B)
    {
        return Math.Sqrt(SquareEucl(A, B));
    }

    /// <summary>
    /// Евклидово расстояние
    /// </summary>
    public static double EuclideanDistanceC(ComplexVector A, ComplexVector B)
    {
        return Math.Sqrt(Complex.Abs(SquareEucl(A, B)));
    }


    /// <summary>
    /// Расстояние L-infinity
    /// </summary>
    public static double LinfDist(IAlgebraicStructure<double> A, IAlgebraicStructure<double> B)
    {
        return LinfDist((Vector)A.Data, (Vector)B.Data);
    }
    /// <summary>
    /// Косинусное сходство
    /// </summary>
    public static double Cos(IAlgebraicStructure<double> a, IAlgebraicStructure<double> b)
    {
        return ((Vector)a.Data).Cos(b.Data);
    }
    /// <summary>
    /// Косинусное расстояние
    /// </summary>
    public static double CosDistRelu(IAlgebraicStructure<double> a, IAlgebraicStructure<double> b)
    {
        double eps = 1e-8;
        double cos = ((Vector)a.Data).Cos(b.Data);
        return 1.0 - (cos < eps ? eps : cos);
    }

    /// <summary>
    /// Косинус со знаком минус: чем ближе направления векторов, тем меньше значение.
    /// </summary>
    /// <remarks>
    /// Не расстояние: значения лежат в [−1; 1], у совпадающих направлений — −1, а не 0. Годится там,
    /// где нужен только порядок (ближайший сосед, сортировка по близости). Косинусное расстояние в
    /// обычном смысле — <c>1 − <see cref="Cos"/></c>.
    /// </remarks>
    public static double NegativeCosine(IAlgebraicStructure<double> a, IAlgebraicStructure<double> b) => -Cos(a, b);

    /// <summary>Косинус со знаком минус.</summary>
    [Obsolete("Имя вводило в заблуждение: это −cos, а не расстояние. Используйте BaseDist.NegativeCosine либо 1 − BaseDist.Cos.")]
    public static double CosDist(IAlgebraicStructure<double> a, IAlgebraicStructure<double> b) => NegativeCosine(a, b);

    /// <summary>
    /// Косинусная близость двух векторов: скалярное произведение, делённое на произведение длин.
    /// </summary>
    /// <remarks>
    /// Единственная реализация формулы. У нулевого вектора направления нет, и близость не
    /// определена: возвращается <paramref name="whenUndefined"/> — по умолчанию <c>NaN</c>. Вызывающий,
    /// которому в этом случае нужен ноль (поиск по вложениям), говорит это явно.
    /// </remarks>
    /// <param name="a">Первый вектор.</param>
    /// <param name="b">Второй вектор той же длины.</param>
    /// <param name="whenUndefined">Что вернуть, если один из векторов нулевой.</param>
    /// <exception cref="ArgumentException">Длины векторов различаются.</exception>
    public static double CosineSimilarity(ReadOnlySpan<double> a, ReadOnlySpan<double> b, double whenUndefined = double.NaN)
    {
        if (a.Length != b.Length)
            throw new ArgumentException($"Длины векторов различаются: {a.Length} и {b.Length}", nameof(b));

        double dot = 0, normA = 0, normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return normA == 0 || normB == 0 ? whenUndefined : dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    /// <inheritdoc cref="CosineSimilarity(ReadOnlySpan{double}, ReadOnlySpan{double}, double)"/>
    public static double CosineSimilarity(IAlgebraicStructure<double> a, IAlgebraicStructure<double> b, double whenUndefined = double.NaN)
        => CosineSimilarity(a.Data.AsSpan(0, a.Shape.Count), b.Data.AsSpan(0, b.Shape.Count), whenUndefined);

    /// <summary>Евклидово расстояние между двумя точками.</summary>
    /// <exception cref="ArgumentException">Размерности точек различаются.</exception>
    public static double EuclideanDistance(ReadOnlySpan<double> a, ReadOnlySpan<double> b) => Math.Sqrt(SquaredEuclidean(a, b));

    /// <summary>Квадрат евклидова расстояния — единственная реализация суммы квадратов разностей.</summary>
    /// <remarks>
    /// Имя отдельное от <c>SquareEucl</c>: у того есть комплексная перегрузка, и вызов с двумя
    /// векторами между ними неоднозначен.
    /// </remarks>
    /// <exception cref="ArgumentException">Размерности точек различаются.</exception>
    public static double SquaredEuclidean(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException($"Размерности точек различаются: {a.Length} и {b.Length}", nameof(b));

        double sum = 0;

        for (int i = 0; i < a.Length; i++)
        {
            double dif = a[i] - b[i];
            sum += dif * dif;
        }

        return sum;
    }

    /// <summary>
    /// Квадрат эвклидова расстояния
    /// </summary>
    public static double SquareEucl(IAlgebraicStructure<double> a, IAlgebraicStructure<double> b) => SquaredEuclidean(a, b);

    /// <inheritdoc cref="SquaredEuclidean(ReadOnlySpan{double}, ReadOnlySpan{double})"/>
    public static double SquaredEuclidean(IAlgebraicStructure<double> a, IAlgebraicStructure<double> b)
        => SquaredEuclidean(a.Data.AsSpan(0, a.Shape.Count), b.Data.AsSpan(0, b.Shape.Count));


    /// <summary>
    /// Квадрат эвклидова расстояния
    /// </summary>
    public static Complex SquareEucl(ComplexVector a, ComplexVector b)
    {
        Complex sum = 0;

        for (int i = 0; i < a.Shape.Count; i++)
        {
            Complex dif = a[i] - b[i];
            sum += dif * dif;
        }


        return sum;
    }

    /// <summary>
    /// L2
    /// </summary>
    /// <param name="cv"></param>
    /// <returns></returns>
    public static double L2(ComplexVector cv)
    {
        Complex sum = 0;

        for (int i = 0; i < cv.Count; i++)
            sum += cv[i] * cv[i];

        return Math.Sqrt(Complex.Abs(sum));
    }

    /// <summary>
    /// L2
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    public static double L2(IAlgebraicStructure<double> a)
    {
        double sum = 0;

        for (int i = 0; i < a.Shape.Count; i++)
            sum += a.Data[i] * a.Data[i];

        return sum;
    }

    /// <summary>
    /// Манхетонское расстояние
    /// </summary>
    public static double ManhattanDistance(IAlgebraicStructure<double> a, IAlgebraicStructure<double> b)
    {
        double sum = 0;

        for (int i = 0; i < a.Shape.Count; i++)
        {
            sum += Math.Abs(a.Data[i] - b.Data[i]);
        }

        return sum;
    }

    #region Схожесть на множествах

    /// <summary>
    /// Коэфициент Жаккара
    /// </summary>
    /// <typeparam name="T">Тип данных множества</typeparam>
    /// <param name="set1">Множество 1</param>
    /// <param name="set2">Множество 2</param>
    public static double JaccardCoef<T>(HashSet<T> set1, HashSet<T> set2)
    {
        double numerator = set1.Intersect(set2).Count();
        double denumerator = set1.Union(set2).Count();
        return numerator / denumerator;
    }
    /// <summary>
    /// Коэфициент Жаккара с нормировкой на минимальное множество
    /// </summary>
    /// <typeparam name="T">Тип данных множества</typeparam>
    /// <param name="set1">Множество 1</param>
    /// <param name="set2">Множество 2</param>
    public static double JaccardCoefMin<T>(HashSet<T> set1, HashSet<T> set2)
    {
        double numerator = set1.Intersect(set2).Count();
        double denumerator = Math.Min(set1.Count(), set2.Count());
        return numerator / denumerator;
    }

    #endregion
}
