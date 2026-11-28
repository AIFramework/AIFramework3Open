using AI.DataStructs.Algebraic;
using System;

namespace AI.HighLevelFunctions;

/// <summary>
/// Математические функции для векторов и матриц
/// </summary>
public static partial class FunctionsForEachElements
{

    /// <summary>Гамма-функция Γ(x).</summary>
    /// <param name="x">Аргумент функции.</param>
    [Obsolete("Используйте SpecialFunctions.Gamma — единственную реализацию.")]
    public static double Gamma(double x) => SpecialFunctions.Gamma(x);

    /// <summary>Натуральный логарифм гамма-функции.</summary>
    /// <param name="x">Аргумент функции (должен быть > 0).</param>
    [Obsolete("Используйте SpecialFunctions.LogGamma — единственную реализацию.")]
    public static double LogGamma(double x) => SpecialFunctions.LogGamma(x);

    /// <summary>
    /// Разворачивание арктангенса
    /// </summary>
    /// <param name="data">Данные с функции atan</param>
    /// <param name="diffMax">Максимальный разрыв</param>
    public static Vector Unwrap(Vector data, double diffMax = 2)
    {
        Vector diff = Functions.Diff(data);
        Vector newDat = new Vector(data.Count);
        double ofset = 0;

        for (int i = 0; i < diff.Count; i++)
        {
            if (diff[i] >= diffMax)
                ofset += diff[i];

            else if (diff[i] <= -diffMax)
                ofset += diff[i];

            newDat[i] = ofset - data[i];
        }

        return newDat;
    }
    /// <summary>
    /// Возведение числа в вектор степеней
    /// </summary>
    /// <param name="a">Число</param>
    /// <param name="pow">Вектор степеней</param>
    public static Vector Pow(double a, Vector pow)
    {
        Vector outp = new Vector(pow.Count);

        for (int i = 0; i < pow.Count; i++)
            outp[i] = Math.Pow(a, pow[i]);

        return outp;
    }
    /// <summary>
    /// Устранение выбросов на одном отсчете сигнала(пиков)
    /// </summary>
    /// <param name="data">Сигнал</param>
    /// <returns></returns>
    public static Vector PeakDel(Vector data)
    {
        Vector newDat = new Vector(data.Count)
        {
            [0] = data[0]
        };

        for (int i = 1, max = data.Count - 1; i < max; i++)
        {
            double mean = (data[i - 1] + data[i + 1]) / 2.0;
            if (Math.Abs(data[i - 1] - data[i + 1]) < Math.Abs(mean - data[i]))
                newDat[i] = mean;

            else
                newDat[i] = data[i];
        }

        return newDat;
    }
    /// <summary>
    /// Возведение -1 в степень
    /// </summary>
    /// <param name="pow">Степень</param>
    /// <returns></returns>
    public static int MinusOnePow(int pow)
    {
        return (pow % 2 != 0) ? -1 : 1;
    }
    /// <summary>
    /// Функции Радемахера
    /// </summary>
    /// <param name="x">Параметр</param>
    /// <param name="num">Номер функции</param>
    /// <returns></returns>
    public static double Rad(double x, int num)
    {
        return Math.Sign(Math.Sin(Math.Pow(2, num) * Math.PI * x));
    }
    /// <summary>
    /// Функции Радемахера
    /// </summary>
    /// <param name="x">Вектор параметров</param>
    /// <param name="num">Номер функции</param>
    /// <returns></returns>
    public static Vector Rad(Vector x, int num)
    {
        return x.Transform(r => Rad(r, num));
    }
    /// <summary>
    /// Функции Уолша
    /// </summary>
    /// <param name="x">Параметр</param>
    /// <param name="num">Номер функции</param>
    /// <returns></returns>
    public static double Walsh(double x, int num)
    {
        return Math.Sign(Math.Sin(num * Math.PI * x));
    }
    /// <summary>
    /// Функции Радемахера
    /// </summary>
    /// <param name="x">Вектор параметров</param>
    /// <param name="num">Номер функции</param>
    /// <returns></returns>
    public static Vector Walsh(Vector x, int num)
    {
        return x.Transform(r => Walsh(r, num));
    }
    /// <summary>Функция ошибок erf(x).</summary>
    /// <param name="x">Аргумент</param>
    [Obsolete("Используйте SpecialFunctions.Erf — единственную реализацию.")]
    public static double Erf(double x) => SpecialFunctions.Erf(x);

    /// <summary>Дополнительная функция ошибок erfc(x) = 1 - erf(x).</summary>
    /// <param name="x">Аргумент</param>
    [Obsolete("Используйте SpecialFunctions.Erfc — единственную реализацию.")]
    public static double Erfc(double x) => SpecialFunctions.Erfc(x);

    /// <summary>
    /// Функция ошибок
    /// </summary>
    /// <param name="Inp">Входной вектор</param>
    /// <returns></returns>
    public static Vector Erf(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = SpecialFunctions.Erf(Inp[i]);

        return A;
    }
    /// <summary>
    /// Генерирование последовательности
    /// </summary>
    /// <param name="began">Начальное значение</param>
    /// <param name="step">Шаг</param>
    /// <param name="end">Конечное значение</param>
    /// <returns>Возвращает последовательность</returns>
    public static Vector GenerateTheSequence(double began, double step, double end)
    {
        // Исправлено: явно вычисляем количество точек для избежания проблем с floating point
        int Count = (int)Math.Round((end - began) / step);

        // Если began + Count * step превышает end, это нормально для последовательности [began, end]
        // Но если не включает end, добавляем точку
        if (Math.Abs(began + Count * step - end) > step * 0.1)
            Count++;

        Vector sequen = new Vector(Count);

        for (int i = 0; i < Count; i++)
            sequen[i] = began + (i * step);

        return sequen;
    }
    /// <summary>
    /// Генерирование последовательности
    /// </summary>
    /// <param name="began">Начальное значение</param>
    /// <param name="end">Конечное значение</param>
    /// <returns>Возвращает послеовательность с шагом 1</returns>
    public static Vector GenerateTheSequence(double began, double end)
    {
        double n = end - began;
        int Count = (n % 1 == 0) ? (int)n : (int)(n + 1);

        Vector sequen = new Vector(Count);

        for (int i = 0; i < Count; i++)
            sequen[i] = began + i;

        return sequen;
    }
    /// <summary>
    /// Перевод градусов в радианы
    /// </summary>
    /// <param name="grad">значение в градусах</param>
    public static double GradToRad(double grad)
    {
        return grad * Math.PI / 180.0;
    }
    /// <summary>
    /// Перевод градусов в радианы
    /// </summary>
    /// <param name="Inp">значения в градусах</param>
    public static Vector GradToRad(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = GradToRad(Inp[i]);

        return A;
    }
    /// <summary>
    /// Перевод радиан в градусы
    /// </summary>
    /// <param name="rad">значение в радианах</param>
    public static double RadToGrad(double rad)
    {
        return rad * 180.0 / Math.PI;
    }
    /// <summary>
    /// Перевод радиан в градусы
    /// </summary>
    /// <param name="Inp">значение в радианах</param>
    public static Vector RadToGrad(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = RadToGrad(Inp[i]);

        return A;
    }
    /// <summary>
    /// Факториал целым числом; помещается только до 20!.
    /// </summary>
    /// <param name="x">Число</param>
    /// <returns>Факториал</returns>
    /// <exception cref="ArgumentOutOfRangeException">Число отрицательно.</exception>
    /// <exception cref="OverflowException">Число больше 20: результат не помещается в <see cref="long"/>.</exception>
    [Obsolete("Используйте SpecialFunctions.Factorial: он возвращает double и не переполняется на 21!.")]
    public static long Factorial(int x)
    {
        if (x > 20)
            throw new OverflowException($"{x}! не помещается в long; SpecialFunctions.Factorial возвращает double.");

        return (long)SpecialFunctions.Factorial(x);
    }
    /// <summary>
    /// Вычисление факториала векторов поэлементно
    /// </summary>
    /// <param name="Inp">Входной вектор</param>
    /// <returns>Факториал</returns>
    public static Vector Factorial(Vector Inp)
    {
        Vector A = new Vector(Inp.Count);
        for (int i = 0; i < Inp.Count; i++)
            A[i] = SpecialFunctions.Factorial((int)Inp[i]);

        return A;
    }
}
