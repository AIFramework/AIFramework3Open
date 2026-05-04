using AI.ComputerVision;
using AI.DataStructs.Algebraic;
using SkiaSharp;
using System;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Extensions;

/// <summary>
/// Extensions for computer vision
/// </summary>
[Serializable]
public static class CVExtension
{
    /// <summary>
    /// Преобразование картинки в матрицу
    /// </summary>
    public static Matrix ToMatrix(this SKBitmap bitmap)
    {
        return ImageMatrixConverter.BmpToMatr(bitmap);
    }

    /// <summary>
    /// Преобразование картинки в матрицу (Взвешенные цвета)
    /// </summary>
    public static Matrix ToMatrix(this SKBitmap bitmap, Vector colorW)
    {
        return ImageMatrixConverter.BmpToMatr(bitmap, colorW);
    }

    /// <summary>
    /// Преобразование картинки в матрицу
    /// </summary>
    public static Matrix ToMatrix(this SKBitmap bitmap, int newWidth, int newHeight)
    {
        using SKBitmap bmp = bitmap.Resize(new SKImageInfo(newWidth, newHeight), new SKSamplingOptions(SKCubicResampler.Mitchell));
        return ImageMatrixConverter.BmpToMatr(bmp);
    }

    /// <summary>
    /// Преобразование картинки в тензор
    /// </summary>
    public static Tensor ToTensor(this SKBitmap bitmap)
    {
        return ImageMatrixConverter.BmpToTensor(bitmap);
    }

    /// <summary>
    /// Преобразование картинки в тензор и изменение размера
    /// </summary>
    public static Tensor ToTensor(this SKBitmap bitmap, int newW, int newH)
    {
        using SKBitmap bmp = bitmap.Resize(new SKImageInfo(newW, newH), new SKSamplingOptions(SKCubicResampler.Mitchell));
        return ImageMatrixConverter.BmpToTensor(bmp);
    }
}
