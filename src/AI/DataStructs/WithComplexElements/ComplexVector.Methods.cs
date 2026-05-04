using AI.DataStructs.Shapes;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AI.DataStructs.WithComplexElements;

public partial class ComplexVector
{
    #region Методы
    /// <summary>
    /// Zero padding or cropping to the desired vector size.
    /// </summary>
    /// <param name="n">New dimension</param>
    public ComplexVector CutAndZero(int n)
    {
        ComplexVector x = new ComplexVector(n);

        if (n > Count)
        {
            for (int i = 0; i < Count; i++)
            {
                x[i] = this[i];
            }
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                x[i] = this[i];
            }
        }

        return x;
    }
    /// <summary>
    /// Vector cloning
    /// </summary>
    public ComplexVector Clone()
    {
        return new ComplexVector(this);
    }
    /// <summary>
    /// Vector reverse (mirror image)
    /// </summary>
    public ComplexVector Revers()
    {
        Complex[] newVect = new Complex[Count];

        for (int i = 0; i < Count; i++)
        {
            newVect[i] = this[Count - i - 1];
        }

        return new ComplexVector(newVect);
    }
    /// <summary>
    ///  Shift the sequence by a certain number.Example: the sequence 1 2 3 is shifted by 2 - this is {0 0 1 2 3}, by 4 - {0 0 0 0 1 2 3}
    /// </summary>
    /// <param name="valueShift">Shift amount</param>
    public ComplexVector Shift(int valueShift)
    {
        int count = Count + valueShift;
        Complex[] newVect = new Complex[count];

        for (int i = 0; i < valueShift; i++)
        {
            newVect[i] = new Complex(0, 0);
        }

        for (int i = valueShift; i < count; i++)
        {
            newVect[i] = this[i - valueShift];
        }

        return new ComplexVector(newVect);
    }
    /// <summary>
    /// Centering an array of values ​​obtained by the Fourier transform
    /// </summary>
    public ComplexVector FurCentr()
    {
        Complex[] centr = new Complex[Count];
        for (int i = 0; i < Count / 2; i++)
        {
            centr[i] = this[(Count / 2) + i];
            centr[(Count / 2) + i] = this[i];
        }
        return new ComplexVector(centr);
    }
    /// <summary>
    /// Decimation (thinning) vector
    /// </summary>
    /// <param name="kDecim">Decimation factor</param>
    public ComplexVector Decimation(int kDecim)
    {
        ComplexVector ret;

        if (Count % kDecim == 0)
        {
            ret = new ComplexVector(Count / kDecim);
        }
        else
        {
            ret = new ComplexVector((Count / kDecim) + 1);
        }

        int k = 0;

        for (int i = 0; i < Count; i += kDecim)
        {
            ret[k++] = this[i];
        }

        return ret;
    }
    /// <summary>
    /// Adding a reflected Vector
    /// </summary>
    public ComplexVector AddSimmetr()
    {
        int n2 = 2 * Count;
        ComplexVector newVector = new ComplexVector(n2);

        for (int i = 0; i < Count; i++)
        {
            newVector[i] = this[i];
        }

        for (int i = Count; i < n2; i++)
        {
            newVector[i] = this[n2 - i - 1];
        }

        return newVector;
    }
    /// <summary>
    /// Interpolation by a polynomial of order zero
    /// </summary>
    /// <param name="kInterp">Interpolation factor</param>
    public ComplexVector InterpolayZero(int kInterp)
    {
        ComplexVector ret = new ComplexVector(Count * kInterp);

        for (int i = 0; i < ret.Count; i++)
        {
            ret[i] = this[i / kInterp];
        }

        return ret;
    }
    /// <summary>
    /// Element-wise vector transformation
    /// </summary>
    /// <param name="func">Conversion function</param>
    public ComplexVector Transform(Func<Complex, Complex> func)
    {
        ComplexVector cVect = new ComplexVector(Count);

        for (int i = 0; i < Count; i++)
        {
            cVect[i] = func(this[i]);
        }

        return cVect;
    }
    /// <summary>
    /// Element-wise vector transformation
    /// </summary>
    /// <param name="func">Conversion function</param>
	public void TransformSelf(Func<Complex, Complex> func)
    {
        for (int i = 0; i < Count; i++)
        {
            this[i] = func(this[i]);
        }
    }
    /// <summary>
    /// Complex conjugate number
    /// </summary>
    public ComplexVector ComplexConjugate()
    {
        return Transform(Complex.Conjugate);
    }
    /// <summary>
    /// Complex conjugate number
    /// </summary>
    public void ComplexConjugateSelf()
    {
        TransformSelf(Complex.Conjugate);
    }

    /// <summary>
    /// Сумма значений вектора
    /// </summary>
    /// <returns></returns>
    public Complex Sum()
    {
        Complex av = new Complex(0, 0);

        for (int i = 0; i < Count; i++)
            av += this[i];

        return av;
    }

    /// <summary>
    /// Arithmetic mean of a complex vector
    /// </summary>
    public Complex Mean()
    {
        return Sum() / Count;
    }
    #endregion

    #region Статические методы

    /// <summary>
    /// Скалярное произведение векторов
    /// </summary>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <returns></returns>
    public static Complex Dot(ComplexVector v1, ComplexVector v2)
    {
        if ((v1 == null) || (v2 == null))
            throw new ArgumentNullException("Векторы участвующие в скалярном произведении не могут быть null");


        if (v1.Count != v2.Count)
            throw new ArgumentNullException("Векторы участвующие в скалярном произведении не могут различаться по размерности");

        if (v1.Count == 0 || v2.Count == 0)
            return 0;

        return (v1 * v2).Sum();
    }


    /// <summary>
    /// Converting the vector of phases and amplitudes into a complex vector
    /// </summary>
    /// <param name="magn">Amplitude vector</param>
    /// <param name="phase"> Phase vector(rad)</param>
    public static ComplexVector ComplexVectorPhaseMagn(Vector magn, Vector phase)
    {
        ComplexVector complexVector = new ComplexVector(magn.Count);
        Complex j = new Complex(0, 1);

        for (int i = 0; i < complexVector.Count; i++)
        {
            complexVector[i] = magn[i] * Complex.Exp(-j * phase[i]);
        }

        return complexVector;
    }

    /// <summary>
    /// Converting the vector of phases and amplitudes into a complex vector
    /// </summary>
    /// <param name="magnDb">Amplitude vector(db)</param>
    /// <param name="phaseDeg"> Phase vector(deg)</param>
    /// <param name="dbType">Тип дб по энергия/амплитуда</param>
    public static ComplexVector ComplexVectorPhaseDegMagnDb(Vector magnDb, Vector phaseDeg, DbType dbType = DbType.Energy)
    {
        Vector phaseRad = FunctionsForEachElements.GradToRad(phaseDeg);
        Vector magn = (dbType == DbType.Energy) ? magnDb.Transform(x => Math.Pow(10, x / 10.0)) : magnDb.Transform(x => Math.Pow(10, x / 20.0));
        return ComplexVectorPhaseMagn(magn, phaseRad);
    }

    /// <summary>
    /// Vector transformation(A vector of real arguments is used)
    /// </summary>
    /// <param name="transformFunc">Conversion function, a function of the value of a vector of arguments</param>
    /// <param name="x">Argument vector</param>
    public static ComplexVector TransformVectorX(Vector x, Func<double, Complex> transformFunc)
    {
        ComplexVector output = new ComplexVector(x.Count);

        for (int i = 0; i < x.Count; i++)
        {
            output[i] = transformFunc(x[i]);
        }

        return output;
    }

    /// <summary>
    /// Vector transformation(Using a vector of complex arguments)
    /// </summary>
    /// <param name="transformFunc"> Conversion function, a function of the value of a vector of arguments</param>
    /// <param name="x">Argument vector</param>
    public static ComplexVector TransformVectorX(ComplexVector x, Func<Complex, Complex> transformFunc)
    {
        ComplexVector output = new ComplexVector(x.Count);

        for (int i = 0; i < x.Count; i++)
        {
            output[i] = transformFunc(x[i]);
        }

        return output;
    }

    public static ComplexVector Pow(ComplexVector v1, ComplexVector v2)
    {
        if (v1.Count != v2.Count)
            throw new ArgumentException();

        ComplexVector complexes = new ComplexVector(v2.Count);

        for (int i = 0; i < v1.Count; i++)
            complexes[i] = Complex.Pow(v1[i], v2[i]);

        return complexes;
    }

    public static ComplexVector Pow(ComplexVector v, Complex c)
    {
        ComplexVector complexes = new ComplexVector(v.Count);

        for (int i = 0; i < v.Count; i++)
            complexes[i] = Complex.Pow(v[i], c);

        return complexes;
    }

    public static ComplexVector Pow(Complex c, ComplexVector v)
    {
        ComplexVector complexes = new ComplexVector(v.Count);

        for (int i = 0; i < v.Count; i++)
            complexes[i] = Complex.Pow(c, v[i]);

        return complexes;
    }
    #endregion
}
