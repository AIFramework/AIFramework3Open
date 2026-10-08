using AI.DataStructs.Algebraic;
using SkiaSharp;
using System;

namespace AI.ComputerVision.SpatialFilters;

/// <summary>
/// Класс для реализации кастомных фильтров через наследование и изменения ф-ии ядра
/// </summary>
[Serializable]
public class CustomFilter : ISpatialFilterGray
{
    /// <summary>
    /// Ядро фильтра
    /// </summary>
    protected Matrix filter_kernel;

    /// <summary>
    /// Класс для реализации кастомных фильтров через наследование и изменения ф-ии ядра
    /// </summary>
    public CustomFilter(Matrix fMatrix)
    {
        filter_kernel = fMatrix;
    }

    /// <summary>
    /// Класс для реализации кастомных фильтров через наследование и изменения ф-ии ядра
    /// </summary>
    public CustomFilter()
    {
        filter_kernel = new Matrix(3, 3);
        filter_kernel[1, 1] = 1;
    }

    /// <summary>
    /// Фильтрация
    /// </summary>
    public Matrix Filtration(Matrix input)
    {
        return ImgFilters.SpatialFilter(input, filter_kernel);
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
}
