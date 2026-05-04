using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using Vd = System.Numerics.Vector<double>;

namespace AI.DataStructs.Algebraic;

public partial class Vector
{
    #region Методы

    /// <summary>
    /// Декомпозиция вектора, где каждая компонента представляется отдельным вектором
    /// [a, b, c, d] -> [[a], [b], [c], [d]]
    /// </summary>
    public Vector[] Decomposition()
    {
        Vector[] vects = new Vector[Count];
        for (int i = 0; i < Count; i++)
            vects[i] = new Vector(this[i]);
        return vects;
    }

    /// <summary>
    /// Добавление числа в циклический буфер (в начало, последний элемент выталкивается)
    /// </summary>
    public void AddCB(double item)
    {
        int len = Count;
        Vector data = new Vector(len);
        for (int i = 1; i < len; i++)
            data[i] = this[i - 1];
        data[0] = item;
        for (int i = 0; i < len; i++) this[i] = data[i];
    }

    /// <summary>
    /// Добавление в конец циклического буфера (первый элемент выталкивается)
    /// </summary>
    public void AddCBE(double item)
    {
        int len = Count;
        for (int i = 1; i < len; i++)
            base[i - 1] = base[i];
        base[len - 1] = item;
    }

    /// <summary>
    /// Замена неопределенности на указанное число
    /// </summary>
    public Vector NanToValue(double value = 0)
    {
        Vector outpVect = new Vector(Count);
        for (int i = 0; i < outpVect.Count; i++)
            outpVect[i] = double.IsNaN(this[i]) ? value : this[i];
        return outpVect;
    }

    /// <summary>
    /// Замена неопределенности на среднее
    /// </summary>
    public Vector NanToMean()
    {
        double mean = Mean();

        if (double.IsNaN(mean))
            return new Vector(Count);

        Vector outpVect = new Vector(Count);
        for (int i = 0; i < outpVect.Count; i++)
            outpVect[i] = double.IsNaN(this[i]) ? mean : this[i];
        return outpVect;
    }

    /// <summary>
    /// Повтор вектора
    /// </summary>
    /// <param name="count">Число повторов</param>
    public Vector Repeat(int count)
    {
        int k = 0, len = Count * count;
        Vector ret = new Vector(len);
        for (int i = 0; i < count; i++)
            for (int j = 0; j < Count; j++)
                ret[k++] = this[j];
        return ret;
    }

    /// <summary>
    /// Косинусное расстояние между векторами
    /// </summary>
    public double Cos(Vector vect) => AnalyticGeometryFunctions.Cos(vect, this);

    /// <summary>
    /// Получение вектора с единицей в позиции индекса с максимальным значением и -1 в остальных
    /// </summary>
    /// <param name="max">Значение в максимуме</param>
    /// <param name="rest">Значение в на остальных позициях</param>
    public Vector MaxOutVector(double max = 1, double rest = -1)
    {
        int ind = MaxElementIndex();
        Vector ret = new Vector(Count) + rest;
        ret[ind] = max;
        return ret;
    }

    /// <summary>
    /// Вектор направления (с единичной длиной)
    /// </summary>
    public Vector GetUnitVector() => this / NormL2();

    /// <summary>
    /// Округление
    /// </summary>
    public Vector Round(int num)
    {
        Vector outp = new Vector(Count);
        for (int i = 0; i < Count; i++)
            outp[i] = Math.Round(this[i], num);
        return outp;
    }

    /// <summary>
    /// Удаление выбранных элементов
    /// </summary>
    public Vector ElementsDel(Vector elements)
    {
        List<double> lD = Clone();
        foreach (double element in elements)
            _ = lD.Remove(element);
        return FromList(lD);
    }

    /// <summary>
    /// Удаление выбранных элементов
    /// </summary>
    public Vector ElementsDel(double[] elements)
    {
        List<double> lD = Clone();
        foreach (double element in elements)
            _ = lD.Remove(element);
        return FromList(lD);
    }

    /// <summary>
    /// Удаление выбранных элементов
    /// </summary>
    public Vector ElementsDel(List<double> elements)
    {
        List<double> lD = new List<double>();
        lD.AddRange(Clone());
        foreach (double element in elements)
            _ = lD.Remove(element);
        return FromList(lD);
    }

    /// <summary>
    /// Вернуть регион [a; b) из массива
    /// </summary>
    public static Vector GetIntervalDouble(int a, int b, double[] data)
    {
        double[] interval = new double[b - a];
        Buffer.BlockCopy(data, 8 * a, interval, 0, 8 * (b - a));
        return new Vector(interval);
    }

    /// <summary>
    /// Вернуть регион [a; b)
    /// </summary>
    public Vector GetInterval(int a, int b)
    {
        double[] interval = new double[b - a];
        Buffer.BlockCopy(ToArray(), 8 * a, interval, 0, 8 * (b - a));
        return new Vector(interval);
    }

    /// <summary>
    /// Клонирование (копирование) вектора
    /// </summary>
    public Vector Clone() => new Vector(ToArray());

    /// <summary>
    /// Добавление зеркально отражённого вектора к текущему
    /// </summary>
    public Vector AddSimmetr()
    {
        int n2 = 2 * Count;
        Vector newVector = new Vector(n2);
        for (int i = 0; i < Count; i++)
            newVector[i] = this[i];
        for (int i = Count; i < n2; i++)
            newVector[i] = this[n2 - i - 1];
        return newVector;
    }

    /// <summary>
    /// Изменение порядка следования компонент вектора {1,2,3} -> {3,2,1}
    /// </summary>
    public Vector Revers()
    {
        double[] newVect = new double[Count];
        for (int i = 0; i < Count; i++)
            newVect[i] = this[Count - i - 1];
        return new Vector(newVect);
    }

    /// <summary>
    /// Обрезка или заполнение нулями вектора до нужного размера
    /// </summary>
    public Vector CutAndZero(int n)
    {
        double[] newVect = new double[n];
        int copyLen = Math.Min(n, Count);
        for (int i = 0; i < copyLen; i++)
            newVect[i] = this[i];
        return new Vector(newVect);
    }

    /// <summary>
    /// Сдвиг на несколько единиц: {1, 2, 3} -shift=2-> {0, 0, 1, 2, 3}
    /// </summary>
    public Vector Shift(int valueShift)
    {
        int count = Count + valueShift;
        double[] newVect = new double[count];
        for (int i = valueShift; i < count; i++)
            newVect[i] = this[i - valueShift];
        return new Vector(newVect);
    }

    /// <summary>
    /// Перевод вектора в матрицу-строку
    /// </summary>
    public Matrix ToMatrix()
    {
        double[,] matrix = new double[1, Count];
        for (int i = 0; i < Count; i++)
            matrix[0, i] = this[i];
        return new Matrix(matrix);
    }

    /// <summary>
    /// Прореживание (без фильтра)
    /// </summary>
    public Vector Downsampling(int n)
    {
        Vector C = (Count % n == 0) ? new Vector(Count / n) : new Vector((Count / n) + 1);
        for (int i = 0, j = 0; i < Count; i += n, j++)
            C[j] = this[i];
        return C;
    }

    /// <summary>
    /// Увеличение размерности (аналог Up Sampling)
    /// </summary>
    /// <param name="kUnPool">Во сколько раз увеличить размерность</param>
    public Vector UnPooling(int kUnPool)
    {
        Vector vector = new Vector(Count * kUnPool);
        for (int i = 0, k = 0; i < vector.Count; i += kUnPool)
            vector[i] = this[k++];
        return vector;
    }

    /// <summary>
    /// Ступенчатая интерполяция
    /// </summary>
    public Vector InterpolayrZero(int kInterp)
    {
        Vector C = new Vector(Count * kInterp);
        for (int i = 0; i < C.Count; i++)
            C[i] = this[i / kInterp];
        return C;
    }

    /// <summary>
    /// Добавить единицу в начало
    /// </summary>
    public Vector AddOne()
    {
        Vector C = Shift(1);
        C[0] = 1;
        return C;
    }

    /// <summary>
    /// Является ли вектор нулевым
    /// </summary>
    public bool IsFilledWithZeros()
    {
        for (int i = 0; i < Count; i++)
            if (this[i] != 0) return false;
        return true;
    }

    /// <summary>
    /// Проверяет, содержит ли вектор более n нулевых элементов
    /// </summary>
    public bool IsFilledWithZeros(int n)
    {
        int count = 0;
        for (int i = 0; i < Count; i++)
            if (this[i] == 0) count++;
        return count > n;
    }

    /// <summary>
    /// Поэлементное преобразование вектора с помощью функции
    /// </summary>
    public Vector Transform(Func<double, double> transformFunc)
    {
        Vector output = new Vector(Count);
        for (int i = 0; i < Count; i++)
            output[i] = transformFunc(this[i]);
        return output;
    }

    /// <summary>
    /// Преобразование вектора по индексу
    /// </summary>
    public Vector TransformByIndex(Func<int, double> transformFunc)
    {
        Vector output = new Vector(Count);
        for (int i = 0; i < Count; i++)
            output[i] = transformFunc(i);
        return output;
    }

    /// <summary>
    /// Преобразование вектора по индексу и значению
    /// </summary>
    public Vector TransformFromIndexAndValue(Func<int, double, double> transformFunc)
    {
        Vector output = new Vector(Count);
        for (int i = 0; i < Count; i++)
            output[i] = transformFunc(i, this[i]);
        return output;
    }

    /// <summary>
    /// Преобразование вектора с использованием вектора-аргумента.
    /// output[i] = transformFunc(x[i], this[i])
    /// </summary>
    public Vector TransformWithArguments(Vector x, Func<double, double, double> transformFunc)
    {
        if (x.Count != Count)
            throw new InvalidOperationException("Length of Вектор входа doesn't match the length of current");

        Vector output = new Vector(Count);
        for (int i = 0; i < Count; i++)
            output[i] = transformFunc(x[i], this[i]);
        return output;
    }

    /// <summary>
    /// Скалярное произведение
    /// </summary>
    public double Dot(Vector features)
    {
        double[] a = ToArray(), b = features.ToArray();
        int n = a.Length, w = Vd.Count, end = n - n % w;
        var acc = Vd.Zero;
        for (int i = 0; i < end; i += w) acc += new Vd(a, i) * new Vd(b, i);
        double dot = 0;
        for (int i = 0; i < w; i++) dot += acc[i];
        for (int i = end; i < n; i++) dot += a[i] * b[i];
        return dot;
    }

    #endregion
}
