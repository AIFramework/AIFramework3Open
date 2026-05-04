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
    #region РњРµС‚РѕРґС‹
    /// <summary>
    /// Р—Р°РјРµРЅР° РЅРµРѕРїСЂРµРґРµР»РµРЅРЅРѕСЃС‚Рё(nan) РЅР° СЃСЂРµРґРЅРµРµ Р·РЅР°С‡РµРЅРёРµ
    /// </summary>
    public Matrix NanToMean()
    {
        Matrix outp = new Matrix(Height, Width);
        double m = Mean();

        for (int i = 0; i < Data.Length; i++)
            outp.Data[i] = double.IsNaN(Data[i]) ? m : Data[i];

        return outp;
    }
    /// <summary>
    ///  Р—Р°РјРµРЅР° РЅРµРѕРїСЂРµРґРµР»РµРЅРЅРѕСЃС‚Рё(nan) РЅР° Р·Р°РґР°РЅРЅРѕРµ Р·РЅР°С‡РµРЅРёРµ
    /// </summary>
    /// <param name="value">Р§РёСЃР»Рѕ</param>
    public Matrix NanToValue(double value = 0)
    {
        Matrix outp = new Matrix(Height, Width);

        for (int i = 0; i < Data.Length; i++)
            outp.Data[i] = double.IsNaN(Data[i]) ? value : Data[i];

        return outp;
    }
    /// <summary>
    /// РџРѕР»СѓС‡РµРЅРёРµ РјРёРЅРѕСЂР°
    /// </summary>
    /// <param name="h">Р‘РµР· РєР°РєРѕР№ СЃС‚СЂРѕРєРё</param>
    /// <param name="w">Р‘РµР· РєР°РєРѕРіРѕ СЃС‚РѕР»Р±С†Р°</param>
    public double GetMinor(int h, int w)
    {
        Matrix result = new Matrix(Height - 1, Height - 1);

        for (int i = 0, i1 = 0; i < Height; i++)
        {
            if (i != h)
            {
                for (int j = 0, j1 = 0; j < Width; j++)
                {
                    if (j != w)
                    {
                        result[i1, j1] = this[i, j];
                        j1++;
                    }
                }
                i1++;
            }
        }

        double minor = result.Determinant;

        return minor;
    }
    /// <summary>
    /// Р’С‹С‡РёСЃР»РµРЅРёРµ РѕР±СЂР°С‚РЅРѕР№ РјР°С‚СЂРёС†С‹
    /// </summary>
    public Matrix GetInvertMatrix()
    {
        if (!IsSquared)
        {
            throw new InvalidOperationException("Matrix is not squared");
        }

        if (IsZero)
        {
            throw new InvalidOperationException("Matrix is zero");
        }

        if (IsDiagonal)
        {
            Matrix output = new Matrix(Height, Height);

            for (int i = 0; i < Height; i++)
            {
                output[i, i] = 1.0 / this[i, i];
            }

            return output;
        }
        else
        {

            Matrix output = new Matrix(Height, Height);
            double det = Determinant;

            // РЈР»СѓС‡С€РµРЅРЅР°СЏ РїСЂРѕРІРµСЂРєР° РѕРїСЂРµРґРµР»РёС‚РµР»СЏ
            const double DET_EPSILON = 1e-10;
            if (Math.Abs(det) < DET_EPSILON)
            {
                throw new InvalidOperationException(
                    $"РћРїСЂРµРґРµР»РёС‚РµР»СЊ Р±Р»РёР·РѕРє Рє РЅСѓР»СЋ (det = {det}). РњР°С‚СЂРёС†Р° РІС‹СЂРѕР¶РґРµРЅРЅР°СЏ РёР»Рё РїР»РѕС…Рѕ РѕР±СѓСЃР»РѕРІР»РµРЅРЅР°СЏ.");
            }

            // РџСЂРѕРІРµСЂРєР° РЅР° NaN/Infinity
            if (double.IsNaN(det) || double.IsInfinity(det))
            {
                throw new InvalidOperationException(
                    $"РћРїСЂРµРґРµР»РёС‚РµР»СЊ РёРјРµРµС‚ РЅРµРґРѕРїСѓСЃС‚РёРјРѕРµ Р·РЅР°С‡РµРЅРёРµ (det = {det}).");
            }


            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    output[i, j] = FunctionsForEachElements.MinusOnePow(j + i) * GetMinor(i, j) / det;
                }
            }

            return output.Transpose();
        }
    }
    /// <summary>
    /// РњРёРЅРёРјР°Р»СЊРЅРѕРµ Р·РЅР°С‡РµРЅРёРµ РјР°С‚СЂРёС†С‹
    /// </summary>
    /// <returns></returns>
    public double Min()
    {
        return Data.Min();
    }
    /// <summary>
    ///  РњР°РєСЃРёРјР°Р»СЊРЅРѕРµ Р·РЅР°С‡РµРЅРёРµ(Matrix)
    /// </summary>
    /// <returns></returns>
    public double Max()
    {
        return Data.Max();
    }
    /// <summary>
    /// РЎСЂРµРґРЅРµРµ Р°СЂРёС„РјРµС‚РёС‡РµСЃРєРѕРµ РјР°С‚СЂРёС†С‹ 
    /// </summary>
    public double Mean()
    {
        double m = 0, n = 0;
        int len = Shape.Count;

        for (int i = 0; i < len; i++)
        {
            if (!double.IsNaN(Data[i]))
            {
                m += Data[i];
                n++;
            }
        }

        return m / n;
    }
    /// <summary>
    /// РЎСѓРјРјР° 
    /// </summary>
    public double Sum()
    {
        double m = 0;
        int len = Shape.Count;

        for (int i = 0; i < len; i++)
        {
            if (!double.IsNaN(Data[i]))
            {
                m += Data[i];
            }
        }

        return m;
    }
    /// <summary>
    /// Р”РёСЃРїРµСЂСЃРёСЏ
    /// </summary>
    public double Dispersion()
    {
        double m = 0, sq = 0, n = 0;
        int len = Shape.Count;

        for (int i = 0; i < len; i++)
        {
            if (!double.IsNaN(Data[i]))
            {
                sq += Data[i] * Data[i];
                m += Data[i];
                n++;
            }

        }

        n = n > 0 ? n : AISettings.GlobalEps;
        m /= n;
        sq /= n;

        return sq - (m * m);
    }
    /// <summary>
    /// РЎСЂРµРґРЅРµРєРІР°РґСЂР°С‚РёС‡РЅРѕРµ РѕС‚РєР»РѕРЅРµРЅРёРµ
    /// </summary>
    public double Std()
    {
        return Math.Sqrt(Dispersion());
    }
    /// <summary>
    /// РђРґР°РјР°СЂРѕРІРѕ РїСЂРѕРёР·РІРµРґРµРЅРёРµ(РїРѕСЌР»РµРјРµРЅС‚РЅРѕРµ)
    /// </summary>
    /// <param name="matrix"></param>
    /// <returns></returns>
    public Matrix AdamarProduct(Matrix matrix)
    {
        if (matrix.Shape != Shape)
            throw new InvalidOperationException("Matrices dimensions don't match for Hadamard product");

        int len = Shape.Count;
        Matrix matrixOut = new Matrix(Height, Width);

        for (int i = 0; i < len; i++)
        {
            matrixOut.Data[i] = matrix.Data[i] * Data[i];
        }

        return matrixOut;
    }

    /// <summary>
    /// РњР°РєСЃ РїСѓР»РёРЅРі
    /// </summary>
    /// <param name="poolH">РЁР°Рі РїРѕ РІС‹СЃРѕС‚Рµ</param>
    /// <param name="poolW">РЁР°Рі РїРѕ С€РёСЂРёРЅРµ</param>
    /// <param name="indexPool">РњР°РєСЃРёРјР°Р»СЊРЅС‹Рµ РёРЅРґРµРєСЃС‹ РІ РёСЃС…РѕРґРЅРѕР№ РјР°С‚СЂРёС†Рµ</param>
    /// <returns></returns>
    public Matrix MaxPool(int poolH, int poolW, out int[,] indexPool)
    {
        int newH = Height / poolH, newW = Width / poolW;
        Matrix outp = new Matrix(newH, newW);
        indexPool = new int[2, newH * newW];
        int k = 0;

        for (int i = 0; i < newH * poolH; i += poolH)
        {
            int l = 0;
            for (int j = 0; j < newW * poolW; j += poolW)
            {
                double max = this[i, j];
                int maxI = i, maxJ = j;

                for (int i2 = i; i2 < i + poolH; i2++)
                {
                    for (int j2 = j; j2 < j + poolW; j2++)
                    {
                        if (this[i2, j2] > max)
                        {
                            max = this[i2, j2];
                            maxI = i2;
                            maxJ = j2;
                        }
                    }
                }

                int poolIdx = k * newW + l;
                outp[k, l] = max;
                indexPool[0, poolIdx] = maxI;
                indexPool[1, poolIdx] = maxJ;

                l++;
            }

            k++;
        }

        return outp;
    }
    /// <summary>
    /// РЈРјРЅРѕР¶РµРЅРёРµ РјР°С‚СЂРёС†С‹ РЅР° РІРµРєС‚РѕСЂ СЃС‚РѕР»Р±РµС†
    /// </summary>
    /// <param name="vectCol">Р’РµРєС‚РѕСЂ СЃС‚РѕР»Р±РµС†</param>
    /// <returns></returns>
    public Vector MulMatrOnVectColumn(Vector vectCol)
    {
        Vector outp = new Vector(Height);

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                outp[i] += this[i, j] * vectCol[j];
            }
        }

        return outp;
    }

    /// <summary>
    /// РњРёРЅРёРјР°РєСЃ РЅРѕСЂРјР°Р»РёР·Р°С†РёСЏ
    /// </summary>
    /// <returns></returns>
    public Matrix Minimax(double maxValue = 1, double minValue = 0)
    {
        double min = Min();
        double max = Max();
        double range = max - min;

        // Р—Р°С‰РёС‚Р° РѕС‚ РґРµР»РµРЅРёСЏ РЅР° РЅРѕР»СЊ: РµСЃР»Рё РІСЃРµ СЌР»РµРјРµРЅС‚С‹ РѕРґРёРЅР°РєРѕРІС‹Рµ
        if (Math.Abs(range) < double.Epsilon)
        {
            // Р’РѕР·РІСЂР°С‰Р°РµРј РјР°С‚СЂРёС†Сѓ, Р·Р°РїРѕР»РЅРµРЅРЅСѓСЋ СЃСЂРµРґРЅРёРј Р·РЅР°С‡РµРЅРёРµРј РґРёР°РїР°Р·РѕРЅР°
            double midValue = (maxValue + minValue) / 2.0;
            return new Matrix(Height, Width) + midValue;
        }

        double denom = range / maxValue;
        min += minValue * denom;

        return (this - min) / denom;
    }

    /// <summary>
    ///  РџСЂРµРґСЃС‚Р°РІР»РµРЅРёРµ РјР°С‚СЂРёС†С‹ РєР°Рє РІРµРєС‚РѕСЂР°
    /// </summary>
    public Vector LikeVector()
    {
        if (Height != 1)
        {
            throw new InvalidCastException("Cannot convert matrix to vector");
        }

        double[] vector = new double[Width];

        for (int i = 0; i < Width; i++)
        {
            vector[i] = Data[i];
        }

        return new Vector(vector);
    }
    /// <summary>
    /// Р“СЂР°РґРёРµРЅС‚ СЃРІРµСЂС‚РєРё
    /// </summary>
    /// <param name="core"></param>
    /// <param name="delts"></param>
    /// <returns></returns>
    public Matrix GradientMatrixConvDelts(Matrix core, Matrix delts)
    {
        Matrix grad = new Matrix(core.Height, core.Width);

        for (int i = 0; i < core.Height; i++)
        {
            for (int j = 0; j < core.Width; j++)
            {
                for (int y = 0; y < delts.Height; y++)
                {
                    for (int x = 0; x < delts.Width; x++)
                    {
                        grad[i, j] += delts[y, x] * this[y + i, x + j];
                    }
                }
            }
        }

        return grad / Math.Sqrt(Height * Width);
    }
    /// <summary>
    /// Р’С‹РґРµР»РµРЅРёРµ СЂРµРіРёРѕРЅР°
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="dx"></param>
    /// <param name="dy"></param>
    /// <returns></returns>
    public Matrix Region(int x, int y, int dx, int dy)
    {
        int maxX = x + dx;
        int maxY = y + dy;
        Matrix region = new Matrix(dx, dy);

        for (int i = x; i < maxX; i++)
        {
            for (int j = y; j < maxY; j++)
            {
                region[j - y, i - x] = this[j, i];
            }
        }

        return region;
    }
    /// <summary>
    /// РўСЂР°РЅСЃРїРѕРЅРёСЂРѕРІР°РЅРёРµ РјР°С‚СЂРёС†С‹
    /// </summary>
    /// <returns>Р’РѕР·РІСЂР°С‰Р°РµС‚ С‚СЂР°РЅСЃРїРѕРЅРёСЂРѕРІР°РЅРЅСѓСЋ РјР°С‚СЂРёС†Сѓ</returns>
    public Matrix Transpose()
    {
        double[,] T = new double[Width, Height];

        for (int i = 0; i < Height; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                T[j, i] = this[i, j];
            }
        }

        return new Matrix(T);
    }
    /// <summary>
    /// РўСЂР°РЅСЃС„РѕСЂРјРёСЂРѕРІР°РЅРёРµ РјР°С‚СЂРёС†С‹
    /// </summary>
    /// <param name="transformFunc">Р¤СѓРЅРєС†РёСЏ С‚СЂР°РЅСЃС„РѕСЂРјР°С†РёРё</param>
    /// <returns></returns>
    public Matrix Transform(Func<double, double> transformFunc)
    {
        Matrix T = new Matrix(Height, Width);
        int len = Shape.Count;

        for (int i = 0; i < len; i++)
        {
            T.Data[i] = transformFunc(Data[i]);
        }

        return T;
    }
    /// <summary>
    /// РљРѕРїРёСЂРѕРІР°РЅРёРµ РјР°С‚СЂРёС†С‹
    /// </summary>
    /// <returns>Р’РѕР·РІСЂР°С‰Р°РµС‚ РєРѕРїРёСЋ</returns>
    public Matrix Copy()
    {
        Matrix B = new Matrix(Height, Width);
        Buffer.BlockCopy(Data, 0, B.Data, 0, Data.Length * 8);
        return B;
    }
    /// <summary>
    /// РћРєСѓРіР»РµРЅРёРµ Р·РЅР°С‡РµРЅРёР№
    /// </summary>
    /// <param name="n">Р”Рѕ РєР°РєРѕРіРѕ Р·РЅР°РєР°</param>
    public Matrix Round(int n)
    {
        Matrix matr = new Matrix(Height, Width);
        int count = Shape.Count;

        for (int i = 0; i < count; i++)
        {
            matr.Data[i] = Math.Round(Data[i], n);
        }

        return matr;
    }
    /// <summary>
    /// РџРµСЂРµРІРѕРґРёС‚ РїСЂРѕРёР·РІРѕР»СЊРЅСѓСЋ РјР°С‚СЂРёС†Сѓ РІ С‚СЂРµСѓРіРѕР»СЊРЅСѓСЋ
    /// </summary>
    /// <returns>Р”РёР°РіРѕРЅР°Р»СЊРЅР°СЏ РјР°С‚СЂРёС†Р°</returns>
    public Matrix ToTriangularMatr()
    {
        Matrix matrix = Copy();
        int n = matrix.Height;

        for (int i = 0; i < n - 1; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double koef = matrix[j, i] / matrix[i, i];

                for (int k = i; k < n; k++)
                {
                    matrix[j, k] -= matrix[i, k] * koef;
                }
            }
        }
        return matrix.Transform(x => double.IsNaN(x) ? 0 : x);
    }
    /// <summary>
    /// Р’РѕР·РІСЂР°С‰Р°РµС‚ РІРµРєС‚РѕСЂ СЃ РЅСѓР¶РЅРѕРіРѕ СЃСЂРµР·Р°, РЅСѓР¶РЅС‹Р№ РёРЅРґРµРєСЃ
    /// </summary>
    /// <param name="index">РРЅРґРµРєСЃ</param>
    /// <param name="dimension">РЎСЂРµР·/СЂР°Р·РјРµСЂРЅРѕСЃС‚СЊ</param>
    /// <returns>Р’РµРєС‚РѕСЂ</returns>
    public Vector GetVector(int index, int dimension)
    {
        Vector result;

        switch (dimension)
        {
            case 0:
                result = new Vector(Height);
                for (int i = 0; i < Height; i++)
                {
                    result[i] = this[i, index];
                }

                return result;
            case 1:
                result = new Vector(Width);
                for (int i = 0; i < Width; i++)
                {
                    result[i] = this[index, i];
                }

                return result;
        }

        throw new Exception("РРЅРґРµРєСЃ РјРѕР¶РµС‚ Р±С‹С‚СЊ С‚РѕР»СЊРєРѕ 1 РёР»Рё 0");
    }
    /// <summary>
    /// РџРµСЂРµРіСЂСѓРїРїРёСЂРѕРІРєР° РјР°С‚СЂРёС†С‹ (Р—Р°РјРµРЅР° РёРЅРґРµРєСЃРѕРІ)
    /// </summary>
    /// <param name="i">РќР° РєР°РєРѕР№ РёРЅРґРµРєСЃ Р·Р°РјРµРЅРёС‚СЊ</param>
    /// <param name="j">РљР°РєРѕР№ РёРЅРґРµРєСЃ Р·Р°РјРµРЅРёС‚СЊ</param>
    /// <param name="dimension">Р Р°Р·РјРµСЂРЅРѕСЃС‚СЊ СЃСЂРµР·Р° 0 РёР»Рё 1</param>
    public void Swap(int i, int j, int dimension)
    {
        if (i != j)
        {
            double c;
            switch (dimension)
            {
                case 0:
                    for (int k = 0; k < Height; k++)
                    {
                        c = this[k, i];
                        this[k, i] = this[k, j];
                        this[k, j] = c;
                    }
                    break;
                case 1:
                    for (int k = 0; k < Width; k++)
                    {
                        c = this[i, k];
                        this[i, k] = this[j, k];
                        this[j, k] = c;
                    }
                    break;
            }
        }
    }
    /// <summary>
    /// След матрицы (сумма диагональных элементов).
    /// </summary>
    public double Trace()
    {
        int n = Math.Min(Height, Width);
        double sum = 0;
        for (int i = 0; i < n; i++) sum += this[i, i];
        return sum;
    }
    #endregion
}
