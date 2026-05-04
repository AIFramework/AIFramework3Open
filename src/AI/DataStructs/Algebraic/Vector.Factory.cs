using AI.HighLevelFunctions;
using AI.Statistics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vd = System.Numerics.Vector<double>;

namespace AI.DataStructs.Algebraic;

public partial class Vector
{
    #region Статические методы

    /// <summary>
    /// Вычисляет векторное произведение для 2D или 3D векторов.
    /// Для 2D v1=(x1,y1), v2=(x2,y2) возвращает (0, 0, x1*y2 − y1*x2).
    /// </summary>
    /// <exception cref="ArgumentNullException">Если один из векторов null.</exception>
    /// <exception cref="ArgumentException">Если размерности не равны 2 или 3.</exception>
    public static Vector Cross(Vector v1, Vector v2)
    {
        if (v1 == null || v2 == null)
            throw new ArgumentNullException("Векторы, участвующие в произведении, не могут быть null.");

        if (v1.Count != v2.Count)
            throw new ArgumentException("Векторы, участвующие в произведении, должны иметь одинаковую размерность.");

        switch (v1.Count)
        {
            case 3:
                double x3d = v1[1] * v2[2] - v1[2] * v2[1];
                double y3d = v1[2] * v2[0] - v1[0] * v2[2];
                double z3d = v1[0] * v2[1] - v1[1] * v2[0];
                return new Vector([x3d, y3d, z3d]);

            case 2:
                double z2d = v1[0] * v2[1] - v1[1] * v2[0];
                return new Vector([0, 0, z2d]);

            default:
                throw new ArgumentException("Векторное произведение определено только для 2D и 3D векторов.");
        }
    }

    /// <summary>
    /// Скалярное произведение векторов (SIMD)
    /// </summary>
    public static double Dot(Vector v1, Vector v2)
    {
        if (v1 == null || v2 == null)
            throw new ArgumentNullException("Векторы участвующие в скалярном произведении не могут быть null");

        if (v1.Count != v2.Count)
            throw new ArgumentException("Векторы участвующие в скалярном произведении не могут различаться по размерности");

        if (v1.Count == 0) return 0;

        double[] a = v1.ToArray(), b = v2.ToArray();
        int n = a.Length, w = Vd.Count, end = n - n % w;
        var acc = Vd.Zero;
        for (int i = 0; i < end; i += w) acc += new Vd(a, i) * new Vd(b, i);
        double dot = 0;
        for (int i = 0; i < w; i++) dot += acc[i];
        for (int i = end; i < n; i++) dot += a[i] * b[i];
        return dot;
    }

    /// <summary>
    /// Смешивает два вектора, используя функцию смешивания
    /// </summary>
    public static Vector Crosser(Vector x, Vector y, Func<double, double, double> cross)
    {
        Vector outp = new Vector(x.Count);
        for (int i = 0; i < outp.Count; i++)
            outp[i] = cross(x[i], y[i]);
        return outp;
    }

    /// <summary>
    /// Соединение векторов с перекрытием, суммированием в области перекрытия
    /// </summary>
    /// <param name="data">Векторы</param>
    /// <param name="col">Область перекрытия (коллизии)</param>
    public static Vector SummWithCollision(Vector[] data, int col = 0)
    {
        int shiftAll = (data.Length - 1) * col;
        int len = 0;
        for (int i = 0; i < data.Length; i++) len += data[i].Count;
        len -= shiftAll;

        Vector outp = new Vector(len);
        int ind = 0;

        for (int i = 0; i < data.Length; i++)
            for (int j = (i != 0) ? col : 0, k = 0; j < data[i].Count; j++)
                if ((j < data[i].Count - col) || i == data.Length - 1)
                    outp[ind++] = data[i][j];
                else
                    outp[ind + k] = data[i][j] + data[i + 1][k++];

        return outp;
    }

    /// <summary>
    /// Преобразование индекса в one-hot вектор: 1 в позиции индекса, 0 в остальных
    /// </summary>
    public static Vector OneHotPol(int index, int maxInd)
    {
        Vector outp = new Vector(maxInd + 1)
        {
            [index] = 1
        };
        return outp;
    }

    /// <summary>
    /// Преобразование индекса в one-hot вектор: 1 в позиции индекса, −1 в остальных
    /// </summary>
    public static Vector OneHotBePol(int index, int maxInd)
    {
        Vector outp = new Vector(maxInd + 1) - 1;
        outp[index] = 1;
        return outp;
    }

    /// <summary>
    /// Конкатенация (последовательное соединение) векторов
    /// </summary>
    public static Vector Concat(Vector[] vectors)
    {
        int n = 0;
        for (int i = 0; i < vectors.Length; i++) n += vectors[i].Count;

        Vector resultVector = new Vector(n);
        for (int i = 0, k = 0; i < vectors.Length; i++)
            for (int j = 0; j < vectors[i].Count; j++)
                resultVector[k++] = vectors[i][j];

        return resultVector;
    }

    /// <summary>
    /// Последовательность, начинающаяся с нуля
    /// </summary>
    /// <param name="step">Шаг</param>
    /// <param name="end">Последнее значение</param>
    public static Vector SeqBeginsWithZero(double step, double end)
        => FunctionsForEachElements.GenerateTheSequence(0, step, end);

    /// <summary>
    /// Последовательность
    /// </summary>
    /// <param name="start">Первое значение</param>
    /// <param name="step">Шаг</param>
    /// <param name="end">Последнее значение</param>
    public static Vector Seq(double start, double step, double end)
        => FunctionsForEachElements.GenerateTheSequence(start, step, end);

    /// <summary>
    /// Вектор отсчётов времени [0; t) с шагом 1/fd
    /// </summary>
    /// <param name="fd">Частота дискретизации</param>
    /// <param name="t">Время в секундах</param>
    public static Vector Time0(double fd, double t)
        => SeqBeginsWithZero(1.0 / fd, t);

    /// <summary>
    /// Разделить на перекрывающиеся окна
    /// </summary>
    /// <param name="inp">Входной вектор</param>
    /// <param name="w">Размер окна</param>
    /// <param name="step">Шаг</param>
    public static Vector[] GetWindows(Vector inp, int w, int step = 2)
    {
        List<Vector> list = new List<Vector>();
        double[] dat = inp.ToArray();
        for (int i = 0; i <= inp.Count - w; i += step)
            list.Add(GetIntervalDouble(i, i + w, dat));
        return list.ToArray();
    }

    /// <summary>
    /// Разделить на окна и применить функцию к каждому
    /// </summary>
    public static Vector[] GetWindowsWithFunc(Func<Vector, Vector> transformer, Vector inp, int w, int step = 2)
    {
        Vector[] vects = GetWindows(inp, w, step);
        for (int i = 0; i < vects.Length; i++)
            vects[i] = transformer(vects[i]);
        return vects;
    }

    /// <summary>
    /// Разделить на окна и свернуть каждое скаляром
    /// </summary>
    public static Vector GetWindowsWithFuncVect(Func<Vector, double> transformer, Vector inp, int w, int step = 2)
    {
        Vector[] vects = GetWindows(inp, w, step);
        Vector vect = new Vector(vects.Length);
        for (int i = 0; i < vects.Length; i++)
            vect[i] = transformer(vects[i]);
        return vect;
    }

    /// <summary>
    /// Масштабирование векторов (z-нормализация по ансамблю)
    /// </summary>
    public static Vector[] ScaleData(Vector[] data)
    {
        Vector mean = Statistic.MeanVector(data);
        Vector std = Statistic.EnsembleDispersion(data);
        std = FunctionsForEachElements.Sqrt(std);
        std = std.Transform(x => x == 0 ? 1e-10 : x);

        Vector[] vects = new Vector[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            vects[i] = data[i] - mean;
            vects[i] /= std;
        }
        return vects;
    }

    /// <summary>
    /// Усреднение по ансамблю
    /// </summary>
    public static Vector Mean(Vector[] vectors)
    {
        Vector result = new Vector(vectors[0].Count);
        for (int i = 0; i < vectors.Length; i++)
            result += vectors[i];
        return result / vectors.Length;
    }

    /// <summary>
    /// Среднеквадратичное отклонение в ансамбле
    /// </summary>
    public static Vector Std(Vector[] vectors)
        => Statistic.EnsembleDispersion(vectors).Transform(Math.Sqrt);

    #endregion

    #region Статические методы инициализации

    /// <summary>
    /// Инициализация вектора из строки вида "[1.0 2.0 3.0]"
    /// </summary>
    public static Vector Parse(string str) => Parse(str, AISettings.GetProvider());

    /// <summary>
    /// Инициализация вектора из строки вида "[1.0 2.0 3.0]"
    /// </summary>
    public static Vector Parse(string str, NumberFormatInfo provider)
    {
        if (str == null) throw new ArgumentNullException(nameof(str));
        if (provider == null) throw new ArgumentNullException(nameof(provider));

        string trimmed = str.Trim();

        if (!trimmed.StartsWith("[") || !trimmed.EndsWith("]"))
            throw new FormatException("Input string is in the wrong format");

        if (trimmed == "[]")
        {
            Vector res = new Vector(3);
            res.Clear();
            return res;
        }

        string content = trimmed.Substring(1, trimmed.Length - 2).Trim();
        string[] nums = content.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return FromStrings(nums, provider);
    }

    /// <summary>
    /// Попытка инициализации вектора из строки
    /// </summary>
    public static bool TryParse(string str, out Vector result)
        => TryParse(str, out result, AISettings.GetProvider());

    /// <summary>
    /// Попытка инициализации вектора из строки
    /// </summary>
    public static bool TryParse(string str, out Vector result, NumberFormatInfo provider)
    {
        if (str == null || provider == null) { result = null; return false; }

        string trimmed = str.Trim();

        if (!trimmed.StartsWith("[") || !trimmed.EndsWith("]")) { result = null; return false; }

        if (trimmed == "[]")
        {
            Vector empty = new Vector(3);
            empty.Clear();
            result = empty;
            return true;
        }

        string content = trimmed.Substring(1, trimmed.Length - 2).Trim();
        string[] nums = content.Split(' ');

        Vector res = new Vector(3);
        res.Clear();

        foreach (string strNum in nums)
        {
            if (!double.TryParse(strNum, NumberStyles.Number, provider, out double num))
            {
                result = null;
                return false;
            }
            res.Add(num);
        }

        result = res;
        return true;
    }

    /// <summary>
    /// Инициализация вектора из массива строк
    /// </summary>
    public static Vector FromStrings(string[] arr) => FromStrings(arr, AISettings.GetProvider());

    /// <summary>
    /// Инициализация вектора из массива строк
    /// </summary>
    public static Vector FromStrings(string[] arr, NumberFormatInfo provider)
    {
        if (arr == null) throw new ArgumentNullException(nameof(arr));
        if (provider == null) throw new ArgumentNullException(nameof(provider));

        Vector result = new Vector(arr.Length);
        result.Clear();

        foreach (string str in arr)
        {
            string trimmed = str.Trim();
            if (trimmed.Length == 0) continue;
            result.Add(double.Parse(trimmed, provider));
        }
        return result;
    }

    /// <summary>
    /// Создание вектора из перечисления
    /// </summary>
    public static Vector FromList(IEnumerable<double> dbs) => new Vector(dbs.ToArray());

    #endregion

    #region Геометрические операции

    /// <summary>
    /// Линейная интерполяция: (1-t)·a + t·b.
    /// </summary>
    public static Vector Lerp(Vector a, Vector b, double t)
    {
        if (a.Count != b.Count)
            throw new ArgumentException("Размерности должны совпадать");

        var result = new Vector(a.Count);
        for (int i = 0; i < a.Count; i++)
            result[i] = a[i] * (1 - t) + b[i] * t;
        return result;
    }

    /// <summary>
    /// Сферическая интерполяция (slerp) через нормализованные векторы.
    /// </summary>
    public static Vector Slerp(Vector a, Vector b, double t)
    {
        double dot = Dot(a, b);
        double na = a.NormL2(), nb = b.NormL2();
        if (na < 1e-15 || nb < 1e-15) return Lerp(a, b, t);
        double cosTheta = Math.Clamp(dot / (na * nb), -1.0, 1.0);
        double theta = Math.Acos(cosTheta);
        if (theta < 1e-10) return Lerp(a, b, t);
        double sinTheta = Math.Sin(theta);
        double wa = Math.Sin((1 - t) * theta) / sinTheta;
        double wb = Math.Sin(t * theta) / sinTheta;
        var result = new Vector(a.Count);
        for (int i = 0; i < a.Count; i++)
            result[i] = wa * a[i] + wb * b[i];
        return result;
    }

    /// <summary>
    /// Смешанное (тройное скалярное) произведение: a · (b × c).
    /// </summary>
    public static double TripleProduct(Vector a, Vector b, Vector c)
    {
        if (a.Count != 3 || b.Count != 3 || c.Count != 3)
            throw new ArgumentException("Тройное произведение определено только для 3D-векторов");
        return Dot(a, Cross(b, c));
    }

    /// <summary>
    /// Отражение вектора v относительно нормали n: v - 2(v·n̂)n̂.
    /// </summary>
    public static Vector Reflect(Vector v, Vector normal)
    {
        double n2 = Dot(normal, normal);
        if (n2 < 1e-30)
            throw new ArgumentException("Нормаль не может быть нулевым вектором");

        double scale = 2.0 * Dot(v, normal) / n2;
        var result = new Vector(v.Count);
        for (int i = 0; i < v.Count; i++)
            result[i] = v[i] - scale * normal[i];
        return result;
    }

    #endregion

    #region Приватные вспомогательные методы

    private static float[] Vector2SingleArray(Vector vector)
    {
        float[] array = new float[vector.Count];
        for (int i = 0; i < vector.Count; i++)
            array[i] = (float)vector[i];
        return array;
    }

    private static Vector SingleArray2Vector(float[] array)
    {
        Vector vector = new Vector(array.Length);
        for (int i = 0; i < vector.Count; i++)
            vector[i] = array[i];
        return vector;
    }

    #endregion
}
