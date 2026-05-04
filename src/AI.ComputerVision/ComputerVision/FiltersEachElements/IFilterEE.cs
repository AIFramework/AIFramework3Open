using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AI.ComputerVision.FiltersEachElements;

/// <summary>
/// Интерфейс поэлементного фильтра
/// </summary>
public interface IFilterEE
{
    /// <summary>
    /// Фильтрация
    /// </summary>
    Matrix Filtration(Matrix input);
    /// <summary>
    /// Фильтрация
    /// </summary>
    SKBitmap Filtration(SKBitmap input);
    /// <summary>
    /// Фильтрация
    /// </summary>
    SKBitmap Filtration(string path);
}
