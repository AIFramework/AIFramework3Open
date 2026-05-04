using AI.DataStructs.Shapes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Vd = System.Numerics.Vector<double>;

namespace AI.DataStructs.Algebraic;

/// <summary>
/// Класс, реализующий вектор и операции над ним
/// </summary>
[Serializable]
public partial class Vector : List<double>, IAlgebraicStructure<double>, IEquatable<Vector>, IEquatable<List<double>>, ISavable, ITextSavable, IByteConvertable
{
    #region Поля и свойства
    /// <summary>
    /// Данные вектора
    /// </summary>
    double[] IAlgebraicStructure<double>.Data => ToArray();
    /// <summary>
    /// Форма (размерность вектора)
    /// </summary>
    public Shape Shape => new Shape1D(Count);


    /// <summary>
    /// Получение значения по индексу, аналогично как Python(поддержка отрицательных индексов)
    /// </summary>
    /// <param name="i">Индекс</param>
    public new double this[int i]
    {
        get
        {
            if (i >= 0) return base[i];
            else return base[Count + i];
        }
        set
        {
            if (i >= 0) base[i] = value;
            else base[Count + i] = value;
        }
    }
    /// <summary>
    /// Получение или установка значений по маске, аналогично как Python
    /// </summary>
    /// <param name="mask">Маска (true - позиции для вставки или извлечения)</param>
    /// <exception cref="Exception">Возникает исключение при несоответствии числа позиций для вставки и размерности вектора для вставки</exception>
    public Vector this[bool[] mask]
    {
        get
        {
            int count = 0;
            for (int i = 0; i < mask.Length; i++) if (mask[i]) count++;

            Vector result = new Vector(count);

            for (int i = 0, j = 0; i < mask.Length; i++)
                if (mask[i]) result[j++] = this[i];

            return result;
        }
        set
        {
            int count = 0;
            for (int i = 0; i < mask.Length; i++) if (mask[i]) count++;

            if (value.Count != count)
                throw new Exception("Число позиций для вставки в маске должно совпадать с размерностью вектора");



            for (int i = 0, j = 0; i < mask.Length; i++)
            {
                if (mask[i]) this[i] = value[j++];
            }
        }
    }
    /// <summary>
    /// Получение среза, аналогично как Python(поддержка отрицательных индексов и шагов)
    /// </summary>
    /// <param name="start">Начало</param>
    /// <param name="end">Конец</param>
    /// <param name="step">Шаг (если отрицательный, то последовательность переворачивается)</param>
    public Vector this[int? start, int? end, int step = 1]
    {
        get
        {
            int a = 0;
            int b = Count;

            if (start != null)
                a = start.Value >= 0 ? start.Value : Count + start.Value;

            if (end != null)
                b = end.Value >= 0 ? end.Value : Count + end.Value;

            int s = Math.Abs(step);

            Vector ret = new Vector((b - a) / s);

            for (int i = a, j = 0; i < b; i += s)
                if (j < ret.Count) ret[j++] = base[i];

            return step < 0 ? ret.Revers() : ret;
        }

        set
        {
            int a = 0;
            int b = Count;

            if (start != null)
                a = start.Value >= 0 ? start.Value : Count + start.Value;

            if (end != null)
                b = end.Value >= 0 ? end.Value : Count + end.Value;

            int s = Math.Abs(step);
            Vector inp = step >= 0 ? value : value.Revers();

            for (int i = a, j = 0; i < b; i += s)
                if (j < inp.Count)
                    base[i] = inp[j++];
        }
    }

    #endregion

    #region Конструкторы
    /// <summary>
    /// Создает вектор емкости 0
    /// </summary>
    public Vector() : base(0) { AddRange(new double[0]); }
    /// <summary>
    /// Создает вектор емкости n
    /// </summary>
    /// <param name="n"></param>
    public Vector(int n) : base(n) { AddRange(new double[n]); }
    /// <summary>
    /// Создает вектор размерности 1 с заданным значением
    /// </summary>
    /// <param name="value"></param>
    public Vector(double value)
    {
        Add(value);
    }
    /// <summary>
    /// Создает вектор из массива чисел типа double
    /// </summary>
    /// <param name="vector"></param>
    public Vector(params double[] vector)
    {
        AddRange(vector);
    }
    /// <summary>
    /// Создает вектор из интерфейса IEnumerable double
    /// </summary>
    /// <param name="data"></param>
    public Vector(IEnumerable<double> data)
    {
        AddRange(data);
    }
    /// <summary>
    /// Создает вектор из интерфейса IEnumerable float
    /// </summary>
    /// <param name="data"></param>
    public Vector(IEnumerable<float> data)
    {
        double[] d = new double[data.Count()];

        int c = 0;

        foreach (float item in data)
        {
            d[c++] = item;
        }

        AddRange(d);
    }
    #endregion

    #region Операторы
    /// <summary>
    /// Addition
    /// </summary>
    public static Vector operator +(Vector A, Vector B)
    {
        int n = A.Count;
        if (n != B.Count)
            throw new InvalidOperationException("Размерности векторов не совпадают");

        double[] a = A.ToArray(), b = B.ToArray(), c = new double[n];
        int w = Vd.Count, end = n - n % w;
        for (int i = 0; i < end; i += w) (new Vd(a, i) + new Vd(b, i)).CopyTo(c, i);
        for (int i = end; i < n; i++) c[i] = a[i] + b[i];
        return new Vector(c);
    }
    /// <summary>
    /// Addition
    /// </summary>
    public static Vector operator +(Vector A, double k)
    {
        double[] a = A.ToArray(), c = new double[A.Count];
        int n = a.Length, w = Vd.Count, end = n - n % w;
        var vk = new Vd(k);
        for (int i = 0; i < end; i += w) (new Vd(a, i) + vk).CopyTo(c, i);
        for (int i = end; i < n; i++) c[i] = a[i] + k;
        return new Vector(c);
    }
    /// <summary>
    /// Addition
    /// </summary>
    public static Vector operator +(double k, Vector A) => A + k;
    /// <summary>
    /// Вычитание
    /// </summary>
    public static Vector operator -(double k, Vector A)
    {
        double[] a = A.ToArray(), c = new double[A.Count];
        int n = a.Length, w = Vd.Count, end = n - n % w;
        var vk = new Vd(k);
        for (int i = 0; i < end; i += w) (vk - new Vd(a, i)).CopyTo(c, i);
        for (int i = end; i < n; i++) c[i] = k - a[i];
        return new Vector(c);
    }
    /// <summary>
    /// Вычитание
    /// </summary>
    public static Vector operator -(Vector A, double k)
    {
        double[] a = A.ToArray(), c = new double[A.Count];
        int n = a.Length, w = Vd.Count, end = n - n % w;
        var vk = new Vd(k);
        for (int i = 0; i < end; i += w) (new Vd(a, i) - vk).CopyTo(c, i);
        for (int i = end; i < n; i++) c[i] = a[i] - k;
        return new Vector(c);
    }
    /// <summary>
    /// Вычитание
    /// </summary>
    public static Vector operator -(Vector A, Vector B)
    {
        int n = A.Count;
        if (n != B.Count)
            throw new InvalidOperationException("Размерности векторов не совпадают");

        double[] a = A.ToArray(), b = B.ToArray(), c = new double[n];
        int w = Vd.Count, end = n - n % w;
        for (int i = 0; i < end; i += w) (new Vd(a, i) - new Vd(b, i)).CopyTo(c, i);
        for (int i = end; i < n; i++) c[i] = a[i] - b[i];
        return new Vector(c);
    }
    /// <summary>
    /// Negation
    /// </summary>
    public static Vector operator -(Vector A) => 0.0 - A;
    /// <summary>
    /// Multiplication
    /// </summary>
    public static Vector operator *(Vector A, Vector B)
    {
        int n = A.Count;
        if (n != B.Count)
            throw new InvalidOperationException("Размерности векторов не совпадают");

        double[] a = A.ToArray(), b = B.ToArray(), c = new double[n];
        int w = Vd.Count, end = n - n % w;
        for (int i = 0; i < end; i += w) (new Vd(a, i) * new Vd(b, i)).CopyTo(c, i);
        for (int i = end; i < n; i++) c[i] = a[i] * b[i];
        return new Vector(c);
    }
    /// <summary>
    /// Multiplication
    /// </summary>
    public static Vector operator *(double k, Vector A) => A * k;
    /// <summary>
    /// Multiplication
    /// </summary>
    public static Vector operator *(Vector A, double k)
    {
        double[] a = A.ToArray(), c = new double[A.Count];
        int n = a.Length, w = Vd.Count, end = n - n % w;
        var vk = new Vd(k);
        for (int i = 0; i < end; i += w) (new Vd(a, i) * vk).CopyTo(c, i);
        for (int i = end; i < n; i++) c[i] = a[i] * k;
        return new Vector(c);
    }
    /// <summary>
    /// Division
    /// </summary>
    public static Vector operator /(Vector A, Vector B)
    {
        int n = A.Count;
        if (n != B.Count)
            throw new InvalidOperationException("Размерности векторов не совпадают");

        double[] a = A.ToArray(), b = B.ToArray(), c = new double[n];
        int w = Vd.Count, end = n - n % w;
        for (int i = 0; i < end; i += w) (new Vd(a, i) / new Vd(b, i)).CopyTo(c, i);
        for (int i = end; i < n; i++) c[i] = a[i] / b[i];
        return new Vector(c);
    }
    /// <summary>
    /// Division
    /// </summary>
    public static Vector operator /(double k, Vector A)
    {
        double[] a = A.ToArray(), c = new double[A.Count];
        int n = a.Length, w = Vd.Count, end = n - n % w;
        var vk = new Vd(k);
        for (int i = 0; i < end; i += w) (vk / new Vd(a, i)).CopyTo(c, i);
        for (int i = end; i < n; i++) c[i] = k / a[i];
        return new Vector(c);
    }
    /// <summary>
    /// Division
    /// </summary>
    public static Vector operator /(Vector A, double k)
    {
        double[] a = A.ToArray(), c = new double[A.Count];
        int n = a.Length, w = Vd.Count, end = n - n % w;
        var vk = new Vd(1.0 / k);
        for (int i = 0; i < end; i += w) (new Vd(a, i) * vk).CopyTo(c, i);
        for (int i = end; i < n; i++) c[i] = a[i] / k;
        return new Vector(c);
    }
    /// <summary>
    /// Remainder of the division
    /// </summary>
    public static Vector operator %(Vector A, double k)
    {
        int n = A.Count;
        Vector C = new Vector(n);
        for (int i = 0; i < n; i++) C[i] = A[i] % k;
        return C;
    }
    /// <summary>
    /// Remainder of the division
    /// </summary>
    public static Vector operator %(double k, Vector A)
    {
        int n = A.Count;
        Vector C = new Vector(n);
        for (int i = 0; i < n; i++) C[i] = k % A[i];
        return C;
    }
    /// <summary>
    /// Remainder of the division for each element
    /// </summary>
    public static Vector operator %(Vector A, Vector B)
    {
        int n1 = A.Count;
        if (n1 != B.Count)
            throw new InvalidOperationException("Размерности векторов не совпадают");

        Vector C = new Vector(n1);
        for (int i = 0; i < n1; i++) C[i] = A[i] % B[i];
        return C;
    }

    /// <summary>
    /// Проверка равенства
    /// </summary>
    public static bool operator ==(Vector A, Vector B)
    {
        if (Equals(A, null) && Equals(B, null)) return true;
        if ((Equals(A, null) && !Equals(B, null)) || (!Equals(A, null) && Equals(B, null))) return false;
        if (A!.Count != B!.Count) return false;

        for (int i = 0; i < A.Count; i++)
            if (A[i] != B[i]) return false;

        return true;
    }
    /// <summary>
    /// Проверка равенства
    /// </summary>
    public static bool operator !=(Vector A, Vector B) => !(A == B);
    /// <summary>
    /// Проверка равенства
    /// </summary>
    public static bool operator ==(Vector left, IList<double> right) => left == FromList(right);
    /// <summary>
    /// Проверка равенства
    /// </summary>
    public static bool operator !=(Vector left, IList<double> right) => left != FromList(right);
    /// <summary>
    /// Проверка равенства
    /// </summary>
    public static bool operator ==(List<double> left, Vector right) => FromList(left) == right;
    /// <summary>
    /// Проверка равенства
    /// </summary>
    public static bool operator !=(List<double> left, Vector right) => FromList(left) != right;

    /// <summary>
    /// Преобразование типа
    /// </summary>
    public static implicit operator double[](Vector vector) => vector?.ToArray();
    /// <summary>
    /// Преобразование типа
    /// </summary>
    public static implicit operator Vector(double[] data) => new Vector(data);
    /// <summary>
    /// Преобразование типа
    /// </summary>
    public static implicit operator Vector(int[] data)
    {
        Vector outp = new Vector(data.Length);
        for (int i = 0; i < data.Length; i++) outp[i] = data[i];
        return outp;
    }
    /// <summary>
    /// Преобразование типа
    /// </summary>
    public static implicit operator Vector(float[] data) => SingleArray2Vector(data);
    /// <summary>
    /// Преобразование типа
    /// </summary>
    public static explicit operator float[](Vector data) => Vector2SingleArray(data);
    /// <summary>
    /// Преобразование типа
    /// </summary>
    public static explicit operator int[](Vector vector)
    {
        int[] outp = new int[vector.Count];
        for (int i = 0; i < vector.Count; i++) outp[i] = (int)vector[i];
        return outp;
    }
    #endregion

    #region Технические методы

    /// <summary>
    /// Перевод вектора в строку
    /// </summary>
    public override string ToString() => ToString(AISettings.GetProvider());

    /// <summary>
    /// Перевод вектора в строку
    /// </summary>
    public string ToString(NumberFormatInfo numberFormatInfo)
    {
        if (Count == 0) return "[]";

        StringBuilder str = new StringBuilder();
        _ = str.Append("[");

        for (int i = 0; i < Count; i++)
        {
            _ = str.Append(this[i].ToString(numberFormatInfo));
            _ = str.Append(" ");
        }

        str.Length--;
        _ = str.Append("]");
        return str.ToString();
    }

    /// <summary>
    /// Проверка равенства
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is Vector vector) return vector == this;
        if (obj is List<double> dList) return dList == this;
        return false;
    }

    /// <summary>
    /// Проверка равенства
    /// </summary>
    public bool Equals(Vector other) => this == other;

    /// <summary>
    /// Проверка равенства
    /// </summary>
    public bool Equals(IEnumerable<double> other) => this == other.ToList();

    /// <summary>
    /// Проверка равенства
    /// </summary>
    public bool Equals(List<double> other) => this == FromList(other);

    /// <summary>
    /// Получение хэш кода
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (double val in this)
                hash = (hash * 23) + val.GetHashCode();
            return hash;
        }
    }
    #endregion
}
