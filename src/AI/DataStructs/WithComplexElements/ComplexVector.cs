using AI.DataStructs.Shapes;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.DataStructs.WithComplexElements;

/// <summary>
/// Представляет вектор комплексных чисел
/// </summary>
[Serializable]
public partial class ComplexVector : List<Complex>, IComplexStructure, ISavable, IByteConvertable
{
    #region Поля и свойства
    /// <summary>
    /// Массив комплексных чисел
    /// </summary>
    Complex[] IComplexStructure.Data => ToArray();
    /// <summary>
    /// Форма вектора
    /// </summary>
    public Shape Shape => new Shape1D(Count);
    /// <summary>
    /// Реальная (действительная) часть комплексного вектора 
    /// </summary>
    public Vector RealVector
    {
        get
        {
            Vector ret = new Vector(Count);

            for (int i = 0; i < Count; i++)
            {
                ret[i] = this[i].Real;
            }

            return ret;
        }
    }
    /// <summary>
    /// Мнимая часть комплексного вектора
    /// </summary>
    public Vector ImaginaryVector
    {
        get
        {
            Vector ret = new Vector(Count);

            for (int i = 0; i < Count; i++)
            {
                ret[i] = this[i].Imaginary;
            }

            return ret;
        }
    }
    /// <summary>
    /// Модуль комплексного вектора
    /// </summary>
    public Vector MagnitudeVector
    {
        get
        {
            Vector ret = new Vector(Count);

            for (int i = 0; i < Count; i++)
            {
                ret[i] = this[i].Magnitude;
            }

            return ret;
        }
    }
    /// <summary>
    /// Фаза комплексного вектора
    /// </summary>
    public Vector PhaseVector
    {
        get
        {
            Vector ret = new Vector(Count);

            for (int i = 0; i < Count; i++)
            {
                ret[i] = this[i].Phase;
            }

            return ret;
        }
    }
    #endregion

    #region Конструкторы
    /// <summary>
    /// Creates a vector with zeros (0 + 0j) of capacity 3
    /// </summary>
    public ComplexVector() : base(3) { AddRange(new Complex[3]); }
    /// <summary>
    /// Creates a vector with zeros (0 + 0j) of dimension n
    /// </summary>
    /// <param name="n"></param>
    public ComplexVector(int n) : base(n) { AddRange(new Complex[n]); }
    /// <summary>
    /// Creates a vector of dimension 1 with the given value
    /// </summary>
    /// <param name="value"></param>
    public ComplexVector(Complex value)
    {
        Add(value);
    }
    /// <summary>
    /// Creates a vector from the IEnumerable interface of Complex
    /// </summary>
    /// <param name="data"></param>
    public ComplexVector(IEnumerable<Complex> data)
    {
        AddRange(data);
    }
    /// <summary>
    /// Creates a vector based on arrays of real and imaginary parts
    /// </summary>
    /// <param name="vectorReal">Real part</param>
    /// <param name="vectorImg">Imaginary part</param>
    public ComplexVector(double[] vectorReal, double[] vectorImg)
    {
        if (vectorReal == null)
        {
            throw new ArgumentNullException(nameof(vectorReal));
        }

        if (vectorImg == null)
        {
            throw new ArgumentNullException(nameof(vectorImg));
        }

        if (vectorReal.Length != vectorImg.Length)
        {
            throw new InvalidOperationException("Lengths of real and imaginary arrays mismatch");
        }

        Init(vectorReal, vectorImg);
    }
    /// <summary>
    /// Creates a vector based on arrays of real and imaginary parts
    /// </summary>
    /// <param name="vectorReal">Real part</param>
    /// <param name="vectorImg">Imaginary part</param>
    public ComplexVector(Vector vectorReal, Vector vectorImg)
    {
        if (vectorReal == null)
        {
            throw new ArgumentNullException(nameof(vectorReal));
        }

        if (vectorImg == null)
        {
            throw new ArgumentNullException(nameof(vectorImg));
        }

        if (vectorReal.Count != vectorImg.Count)
        {
            throw new InvalidOperationException("Lengths of real and imaginary arrays mismatch");
        }

        Init(vectorReal, vectorImg);
    }
    /// <summary>
    /// Creates a vector based on array of real part, imaginary filled with zeros
    /// </summary>
    /// <param name="vectorReal">Real part</param>
    public ComplexVector(double[] vectorReal)
    {
        if (vectorReal == null)
        {
            throw new ArgumentNullException(nameof(vectorReal));
        }

        Vector vectorImg = new Vector(vectorReal.Length);
        Init(vectorReal, vectorImg);
    }
    /// <summary>
    /// Creates a vector based on vectors of real part, imaginary filled with zeros
    /// </summary>
    /// <param name="vectorReal">Real part</param>
    public ComplexVector(Vector vectorReal)
    {
        if (vectorReal == null)
        {
            throw new ArgumentNullException(nameof(vectorReal));
        }

        Vector vectorImg = new Vector(vectorReal.Count);
        Init(vectorReal, vectorImg);
    }
    #endregion

    #region Приватные методы
    private void Init(double[] vectorReal, double[] vectorImg)
    {
        Capacity = vectorReal.Length;
        Clear();

        for (int i = 0; i < vectorReal.Length; i++)
        {
            Add(new Complex(vectorReal[i], vectorImg[i]));
        }
    }
    #endregion
}

/// <summary>
/// Decibel type
/// </summary>
public enum DbType
{
    /// <summary>
    /// Energetic
    /// </summary>
    Energy,
    /// <summary>
    /// Amplitude
    /// </summary>
    Ampl
}
