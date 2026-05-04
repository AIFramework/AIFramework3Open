using AI.DataStructs.Algebraic;
using System;

namespace AI.ClassicMath.MatrixUtils;

/// <summary>
/// Процедура ортогонализации Грама-Шмидта
/// </summary>
[Serializable]
public static class GramSchmidtProcedure
{
    /// <summary>
    /// Проекция столбца матрицы a, на столбец b
    /// </summary>
    /// <param name="a">Вектор-столбец</param>
    /// <param name="b">Матрица 2</param>
    /// <param name="colB">Столбец в матрице 2</param>
    public static Vector GetProj(Vector a, Matrix b, int colB)
    {
        Vector proj = new Vector(a.Count);
        double energy = 0;
        double pCoef = 0;

        for (int i = 0; i < a.Count; i++)
        {
            double bEl = b[i, colB];
            energy += bEl * bEl;
            pCoef += a[i] * bEl;
        }

        if (energy < 1e-30)
            return proj;

        pCoef /= energy;

        for (int i = 0; i < b.Height; i++)
            proj[i] = b[i, colB] * pCoef;

        return proj;
    }

    /// <summary>
    /// Вернуть вектор столбец
    /// </summary>
    /// <param name="a">Матрица</param>
    /// <param name="colA">Индекс вектора</param>
    public static Vector GetVectorCol(Matrix a, int colA)
    {
        Vector col = new Vector(a.Height);

        for (int i = 0; i < a.Height; i++)
            col[i] = a[i, colA];

        return col;
    }

    /// <summary>
    /// Записать столбец
    /// </summary>
    /// <param name="a"></param>
    /// <param name="col"></param>
    /// <param name="indexCol"></param>
    public static void WriteColumn(Matrix a, Vector col, int indexCol)
    {
        for (int i = 0; i < a.Height; i++) a[i, indexCol] = col[i];
    }

    /// <summary>
    /// Ортогонализация
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    public static Matrix Orthogonize(Matrix a)
    {
        Matrix b = new Matrix(a.Height, a.Width);

        WriteColumn(b, GetVectorCol(a, 0), 0); // b[0] = a[0]

        for (int i = 1; i < a.Width; i++)
        {
            var aCol = GetVectorCol(a, i);
            Vector prSum = new Vector(a.Height);

            for (int j = 0; j < i; j++)
                prSum += GetProj(aCol, b, j);

            WriteColumn(b, aCol - prSum, i); // b[i] = a[i] - proj(a[i], b[0]) - ... - proj(a[i], b[i-1])
        }

        return b;
    }

    /// <summary>
    /// Получить ортонормированный базис
    /// </summary>
    /// <param name="a">Базис</param>
    /// <returns></returns>
    public static Matrix GetNormalBasis(Matrix a)
    {
        Matrix ort = Orthogonize(a);
        Matrix ortNorm = new Matrix(a.Height, a.Width);


        for (int i = 0; i < a.Width; i++)
        {
            Vector colum = GetVectorCol(ort, i);
            double norm = colum.NormL2();
            if (norm < 1e-14)
                throw new InvalidOperationException(
                    $"Столбец {i} после ортогонализации имеет нулевую норму. Входные векторы линейно зависимы.");
            colum /= norm;
            WriteColumn(ortNorm, colum, i);
        }

        return ortNorm;
    }
}
