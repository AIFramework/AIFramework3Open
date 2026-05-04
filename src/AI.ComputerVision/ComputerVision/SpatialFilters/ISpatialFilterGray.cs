using AI.DataStructs.Algebraic;
using SkiaSharp;

namespace AI.ComputerVision.SpatialFilters;

/// <summary>
/// Интерфейс пространственного фильтра оттенков серого
/// </summary>
public interface ISpatialFilterGray
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
