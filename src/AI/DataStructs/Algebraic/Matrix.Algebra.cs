using AI.DataStructs.Shapes;
using AI.Extensions;
using AI.HighLevelFunctions;
using AI.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace AI.DataStructs.Algebraic;

public partial class Matrix
{
    #region ╨б╤В╨░╤В╨╕╤З╨╡╤Б╨║╨╕╨╡ ╨╝╨╡╤В╨╛╨┤╤Л
    /// <summary>
    /// ╨Ч╨░╨╝╨╡╨╜╨░ ╨╜╨╡╨╛╨┐╤А╨╡╨┤╨╡╨╗╨╡╨╜╨╜╨╛╤Б╤В╨╕ ╤Б╤А╨╡╨┤╨╜╨╕╨╝ ╨╖╨╜╨░╤З╨╡╨╜╨╕╨╡╨╝
    /// </summary>
    /// <param name="matrices"></param>
    public static Matrix[] NanToMeanOfFeatures(Matrix[] matrices)
    {
        Matrix[] outp = new Matrix[matrices.Length];

        for (int i = 0; i < matrices.Length; i++)
        {
            outp[i] = matrices[i].NanToValue();
        }

        Matrix mean = MeanMatrix(outp);

        for (int k = 0; k < matrices.Length; k++)
        {
            for (int i = 0; i < outp[0].Height; i++)
            {
                for (int j = 0; j < outp[0].Width; j++)
                {
                    outp[k][i, j] = double.IsNaN(matrices[k][i, j]) ? mean[i, j] : outp[k][i, j];
                }
            }
        }

        return outp;
    }
    /// <summary>
    /// ╨г╨╝╨╜╨╛╨╢╨╡╨╜╨╕╨╡ ╨▓╨╡╨║╤В╨╛╤А╨░-╤Б╤В╨╛╨╗╨▒╤Ж╨░ ╨╜╨░ ╨▓╨╡╨║╤В╨╛╤А ╤Б╤В╤А╨╛╨║╤Г, ╨▓╨╛╨╖╨▓╤А╨░╤Й╨░╨╡╤В╤Б╤П ╨╝╨░╤В╤А╨╕╤Ж╨░ ╤А╨╡╨╖╤Г╨╗╤М╤В╨░╤В╨░
    /// </summary>
    /// <param name="ABinaryBip">╨С╨╕╨╜╨░╤А╨╜╤Л╨╣ ╨▓╨╡╨║╤В╨╛╤А</param>
    /// <param name="B">╤Б╤В╤А╨╛╨║╨░</param>
    /// <returns></returns>
    public static Matrix Mul2VecFast(Vector ABinaryBip, Vector B)
    {
        Matrix matr = new Matrix(ABinaryBip.Count, B.Count);

        for (int i = 0; i < ABinaryBip.Count; i++)
        {

            if (ABinaryBip[i] < 0)
            {
                for (int j = 0; j < B.Count; j++)
                {
                    matr[i, j] = -B[j];
                }
            }
            else
            {
                for (int j = 0; j < B.Count; j++)
                {
                    matr[i, j] = B[j];
                }
            }

        }

        return matr;
    }
    /// <summary>
    /// ╨г╨╝╨╜╨╛╨╢╨╡╨╜╨╕╨╡ ╨▓╨╡╨║╤В╨╛╤А╨░-╤Б╤В╨╛╨╗╨▒╤Ж╨░ ╨╜╨░ ╨▓╨╡╨║╤В╨╛╤А ╤Б╤В╤А╨╛╨║╤Г, ╨▓╨╛╨╖╨▓╤А╨░╤Й╨░╨╡╤В╤Б╤П ╨╝╨░╤В╤А╨╕╤Ж╨░ ╤А╨╡╨╖╤Г╨╗╤М╤В╨░╤В╨░
    /// </summary>
    /// <param name="A">╤Б╤В╨╛╨╗╨▒╨╡╤Ж</param>
    /// <param name="B">╤Б╤В╤А╨╛╨║╨░</param>
    /// <returns></returns>
    public static Matrix Mul2Vec(Vector A, Vector B)
    {
        Matrix matr = new Matrix(A.Count, B.Count);

        for (int i = 0; i < A.Count; i++)
        {
            for (int j = 0; j < B.Count; j++)
            {
                matr[i, j] = B[j] * A[i];
            }
        }

        return matr;
    }
    /// <summary>
    /// ╨б╨╗╨╛╨╢╨╡╨╜╨╕╨╡ ╨▓╨╡╨║╤В╨╛╤А╨░-╤Б╤В╨╛╨╗╨▒╤Ж╨░ ╨╜╨░ ╨▓╨╡╨║╤В╨╛╤А ╤Б╤В╤А╨╛╨║╤Г ╨┐╨╛ ╤Б╨╗╨╡╨┤╤Г╤О╤Й╨╡╨╝╤Г ╨┐╤А╨░╨▓╨╕╨╗╤Г "matr[i, j] = B[j] + A[i];" ╨▓╨╛╨╖╨▓╤А╨░╤Й╨░╨╡╤В╤Б╤П ╨╝╨░╤В╤А╨╕╤Ж╨░ ╤А╨╡╨╖╤Г╨╗╤М╤В╨░╤В╨░
    /// </summary>
    /// <param name="A">╤Б╤В╨╛╨╗╨▒╨╡╤Ж</param>
    /// <param name="B">╤Б╤В╤А╨╛╨║╨░</param>
    public static Matrix Sum2Vec(Vector A, Vector B)
    {
        Matrix matr = new Matrix(A.Count, B.Count);

        for (int i = 0; i < A.Count; i++)
        {
            for (int j = 0; j < B.Count; j++)
            {
                matr[i, j] = B[j] + A[i];
            }
        }

        return matr;
    }
    /// <summary>
    /// ╨Т╤Л╤З╨╕╤Б╨╗╨╡╨╜╨╕╨╡ ╨╜╨╛╤А╨╝╤Л ╨┐╨╛ ╤Б╨╗╨╡╨┤. ╨┐╤А╨░╨▓╨╕╨╗╤Г  matr[i, j] = Math.Sqrt(B[j]*B[j]+ A[i]*A[i]);, ╨▓╨╛╨╖╨▓╤А╨░╤Й╨░╨╡╤В╤Б╤П ╨╝╨░╤В╤А╨╕╤Ж╨░ ╤А╨╡╨╖╤Г╨╗╤М╤В╨░╤В╨░
    /// </summary>
    /// <param name="A">╤Б╤В╨╛╨╗╨▒╨╡╤Ж</param>
    /// <param name="B">╤Б╤В╤А╨╛╨║╨░</param>
    /// <returns></returns>
    public static Matrix Norm2Vec(Vector A, Vector B)
    {
        Matrix matr = new Matrix(A.Count, B.Count);

        for (int i = 0; i < A.Count; i++)
        {
            for (int j = 0; j < B.Count; j++)
            {
                matr[i, j] = Math.Sqrt((B[j] * B[j]) + (A[i] * A[i]));
            }
        }

        return matr;
    }
    /// <summary>
    /// ╨б╨╛╨╖╨┤╨░╤С╤В ╨╡╨┤╨╕╨╜╨╕╤З╨╜╤Г╤О ╨╝╨░╤В╤А╨╕╤Ж╤Г ╤А╨░╨╖╨╝╨╡╤А╨╛╨╝ n├Чn
    /// </summary>
    public static Matrix Identity(int n)
    {
        Matrix I = new Matrix(n, n);
        for (int i = 0; i < n; i++)
            I[i, i] = 1.0;
        return I;
    }

    /// <summary>
    /// ╨Т╨╛╨╖╨▓╨╡╨┤╨╡╨╜╨╕╨╡ ╨╝╨░╤В╤А╨╕╤Ж╤Л ╨▓ ╤Б╤В╨╡╨┐╨╡╨╜╤М 
    /// ╨┐╤Г╤В╨╡╨╝ ╨╝╨░╤В╤А╨╕╤З╨╜╨╛╨│╨╛ ╤Г╨╝╨╜╨╛╨╢╨╡╨╜╨╕╤П ╨╜╨░ ╤Б╨░╨╝╤Г ╤Б╨╡╨▒╤П
    /// </summary>
    /// <param name="A">╨Т╤Е╨╛╨┤╨╜╨░╤П ╨╝╨░╤В╤А╨╕╤Ж╨░</param>
    /// <param name="exponent">╨б╤В╨╡╨┐╨╡╨╜╤М (0 ╨▓╨╛╨╖╨▓╤А╨░╤Й╨░╨╡╤В ╨╡╨┤╨╕╨╜╨╕╤З╨╜╤Г╤О ╨╝╨░╤В╤А╨╕╤Ж╤Г)</param>
    public static Matrix Pow(Matrix A, int exponent)
    {
        if (!A.IsSquared)
            throw new InvalidOperationException("Matrix must be square for exponentiation");

        if (exponent < 0)
            throw new ArgumentOutOfRangeException(nameof(exponent), "Exponent must be non-negative");

        if (exponent == 0)
            return Identity(A.Height);

        Matrix B = A.Copy();

        for (int i = 1; i < exponent; i++)
        {
            B *= A;
        }

        return B;
    }
    /// <summary>
    /// ╨а╨░╨╖╨╗╨╛╨╢╨╡╨╜╨╕╨╡ ╨╝╨░╤В╤А╨╕╤Ж╤Л ╨╜╨░ ╤Б╤В╨╛╨╗╨▒╤Ж╤Л
    /// </summary>
    /// <param name="matr">╨Ь╨░╤В╤А╨╕╤Ж╨░</param>
    /// <returns>╨Ь╨░╤Б╤Б╨╕╨▓ ╨▓╨╡╨║╤В╨╛╤А╨╛╨▓</returns>
    public static Vector[] GetColumns(Matrix matr)
    {
        Vector[] columns = new Vector[matr.Width];

        for (int i = 0; i < columns.Length; i++)
        {
            columns[i] = new Vector(matr.Height);
            for (int j = 0; j < matr.Height; j++)
            {
                columns[i][j] = matr[j, i];
            }
        }

        return columns;
    }

    /// <summary>
    /// ╨а╨░╨╖╨╗╨╛╨╢╨╡╨╜╨╕╨╡ ╨╝╨░╤В╤А╨╕╤Ж╤Л ╨╜╨░ ╤Б╤В╤А╨╛╨║╨╕
    /// </summary>
    /// <param name="matr">╨Ь╨░╤В╤А╨╕╤Ж╨░</param>
    /// <returns>╨Ь╨░╤Б╤Б╨╕╨▓ ╨▓╨╡╨║╤В╨╛╤А╨╛╨▓</returns>
    public static Vector[] GetRows(Matrix matr)
    {
        Vector[] rows = new Vector[matr.Height];

        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new Vector(matr.Width);
            for (int j = 0; j < matr.Width; j++)
            {
                rows[i][j] = matr[i, j];
            }
        }

        return rows;
    }
    /// <summary>
    /// ╨Р╨╗╤М╤В╨╡╤А╨╜╨░╤В╨╕╨▓╨╜╨░╤П ╨╝╨░╤В╤А╨╕╤Ж╨░
    /// </summary>
    /// <param name="functions">╨д╤Г╨╜╨║╤Ж╨╕╨╕</param>
    /// <param name="values">╨Ч╨╜╨░╤З╨╡╨╜╨╕╤П</param>
    /// <returns>╨Т╨╛╨╖╨▓╤А╨░╤Й╨░╨╡╤В ╨░╨╗╤М╤В╨╡╤А╨╜╨░╤В╨╕╨▓╨╜╤Г╤О ╨╝╨░╤В╤А╨╕╤Ж╤Г</returns>
    public static Matrix AlternativMatrix(Func<double, double>[] functions, Vector values)
    {
        Matrix matr = new Matrix(values.Count, functions.Length);

        for (int i = 0; i < values.Count; i++)
        {
            for (int j = 0; j < functions.Length; j++)
            {
                matr[i, j] = functions[j](values[i]);
            }
        }

        return matr;
    }
    /// <summary>
    /// ╨Ю╤А╤В╨╛╨│╨╛╨╜╨░╨╗╤М╨╜╨░╤П ╨╝╨░╤В╤А╨╕╤Ж╨░
    /// </summary>
    /// <param name="functions">╨Я╨╛╤А╨╛╨╢╨┤╨░╤О╤Й╨░╤П ╤Д╤Г╨╜╨║╤Ж╨╕╤П</param>
    /// <param name="values">╨Ч╨╜╨░╤З╨╡╨╜╨╕╤П</param>
    /// <param name="count">╨з╨╕╤Б╨╗╨╛ ╨▓╤Л╤Е╨╛╨┤╨╛╨▓</param>
    /// <returns>╨Т╨╛╨╖╨▓╤А╨░╤Й╨░╨╡╤В ╨╛╤А╤В╨╛╨│╨╛╨╜╨░╨╗╤М╨╜╤Г╤О ╨╝╨░╤В╤А╨╕╤Ж╤Г</returns>
    public static Matrix OrtogonalMatrix(Func<int, double, double> functions, Vector values, int count)
    {
        Matrix matr = new Matrix(values.Count, count);

        for (int i = 0; i < values.Count; i++)
        {
            for (int j = 0; j < count; j++)
            {
                matr[i, j] = functions(j, values[i]);
            }
        }

        return matr;
    }
    #endregion
}
