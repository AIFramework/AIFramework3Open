using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;

namespace AI.ComputerVision;

/// <summary>
/// Сравнение изображений
/// </summary>
[Serializable]
public class CompImg
{
    /// <summary>
    /// Получение вектора из изображения
    /// </summary>
    public static Vector GetVectorFromImg(SKBitmap bitmap)
    {
        using SKBitmap bmp = bitmap.Resize(new SKImageInfo(128, 128), new SKSamplingOptions(SKCubicResampler.Mitchell));

        Matrix matrix = ImageMatrixConverter.BmpToMatr(bmp);

        Vector m1 = matrix.Data;
        double mean = m1.Average();
        double min = mean - (2 * m1.Std());
        double max = mean + (2 * m1.Std());

        matrix = (matrix - min) / max;

        Vector vect = ComplexMatrix.MatrixFFT(matrix).MagnitudeMatrix.Data;
        vect = vect.GetInterval(20, 4000);
        return vect.GetUnitVector();
    }

    /// <summary>
    /// Получение вектора из изображения
    /// </summary>
    public static Vector GetVectorFromImg(SKBitmap bitmap, string pathNorm)
    {
        Vector std = Vector.Load(Path.Combine(pathNorm, "std.vec"));
        Vector mean = Vector.Load(Path.Combine(pathNorm, "mean.vec"));

        Vector outp = GetVectorFromImg(bitmap) - mean;
        outp /= std;

        return outp;
    }

    /// <summary>
    /// Получение вектора из изображения
    /// </summary>
    public static Vector GetVectorFromImgI(SKBitmap bitmap, string pathNorm)
    {
        Vector ind = Vector.Load(Path.Combine(pathNorm, "ind.vec"));
        Vector mean = Vector.Load(Path.Combine(pathNorm, "mean.vec"));

        Vector outp = GetVectorFromImg(bitmap) - mean;

        Vector data = new Vector();

        for (int i = 0; i < ind.Count; i++)
            data.Add(outp[(int)ind[i]]);

        return data;
    }

    /// <summary>
    /// Получение вектора из изображения
    /// </summary>
    public static Vector GetVectorFromPath(string path)
    {
        using SKBitmap bmp = ImageMatrixConverter.GetBitmap(path);
        return GetVectorFromImg(bmp);
    }

    /// <summary>
    /// Сохраняет ско и средний вектор
    /// </summary>
    public static void SaveMeanStdVect(string path2fold, string pathSave)
    {
        string[] files = Directory.GetFiles(path2fold);
        Vector[] vects = new Vector[files.Length];

        for (int i = 0; i < files.Length; i++)
            vects[i] = GetVectorFromPath(files[i]);

        Vector std = AI.Statistics.Statistic.EnsembleDispersion(vects).Transform(Math.Sqrt),
            mean = Vector.Mean(vects);

        std.Save(Path.Combine(pathSave, "std.vec"));
        mean.Save(Path.Combine(pathSave, "mean.vec"));
    }
}
