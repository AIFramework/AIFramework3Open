using AI.DataStructs.Algebraic;
using SkiaSharp;
using System;

namespace AI.ComputerVision.FiltersEachElements;

/// <summary>
/// Базовый класс гамма фильтра
/// </summary>
[Serializable]
public class FilterEE : IFilterEE
{
    private Func<double, double> _elFunc;
    private bool _prepNorm;
    private bool _postNorm;

    /// <summary>
    /// Гамма-фильтр
    /// </summary>
    public FilterEE(Func<double, double> elem, bool prepNorm = false, bool postNorm = false)
    {
        Init(elem, prepNorm, postNorm);
    }

    /// <summary>
    /// Гамма-фильтр
    /// </summary>
    public FilterEE()
    {
        _elFunc = (x) => x;
        _prepNorm = false;
        _postNorm = true;
    }

    /// <summary>
    /// Фильтрация
    /// </summary>
    public Matrix Filtration(Matrix input)
    {
        Matrix matrix = input.Copy();

        if (_prepNorm)
            matrix = matrix.Minimax();

        matrix = matrix.Transform(_elFunc);

        if (_postNorm)
            matrix = 255 * matrix.Minimax();

        Normal(matrix);
        return matrix;
    }

    /// <summary>
    /// Фильтрация
    /// </summary>
    public SKBitmap Filtration(SKBitmap input)
    {
        Matrix matrix = ImageMatrixConverter.BmpToMatr(input);
        Matrix filtred = Filtration(matrix);
        var bmp = ImageMatrixConverter.ToBitmap(filtred);
        if (bmp.Width != input.Width || bmp.Height != input.Height)
            return bmp.Resize(new SKImageInfo(input.Width, input.Height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        return bmp;
    }

    /// <summary>
    /// Фильтрация
    /// </summary>
    public SKBitmap Filtration(string path)
    {
        Matrix matrix = ImageMatrixConverter.LoadAsMatrix(path);
        Matrix filtred = Filtration(matrix);
        return ImageMatrixConverter.ToBitmap(filtred);
    }

    /// <summary>
    /// Инициализация гамма-фильтра
    /// </summary>
    public void Init(Func<double, double> elem, bool prepNorm = false, bool postNorm = false)
    {
        _elFunc = elem;
        _prepNorm = prepNorm;
        _postNorm = postNorm;
    }

    private void Normal(Matrix img)
    {
        for (int i = 0; i < img.Data.Length; i++)
        {
            if (img.Data[i] < 0) img.Data[i] = 0;
            if (img.Data[i] > 255) img.Data[i] = 255;
        }
    }
}
