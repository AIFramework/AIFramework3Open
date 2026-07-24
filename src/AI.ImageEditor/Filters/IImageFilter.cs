using AI.ImageEditor.Pixels;

namespace AI.ImageEditor.Filters;

/// <summary>
/// Фильтр изображения: изменяет буфер на месте.
/// <para>
/// Интерфейс намеренно из одного метода (ISP): любой фильтр — это чистое
/// преобразование пикселей, а параметры он получает при создании.
/// </para>
/// </summary>
public interface IImageFilter
{
    /// <summary>Применяет фильтр к буферу (in-place).</summary>
    void Apply(PixelBuffer buffer);
}

/// <summary>
/// Именованные параметры фильтра (значения нормализованы к double).
/// Тонкая обёртка: избавляет фильтры от разбора словаря и «магических» ключей.
/// </summary>
public sealed class FilterParams
{
    private readonly IReadOnlyDictionary<string, double> _values;

    /// <summary>Пустой набор параметров.</summary>
    public static readonly FilterParams Empty = new(new Dictionary<string, double>());

    /// <summary>Создаёт набор параметров.</summary>
    public FilterParams(IReadOnlyDictionary<string, double> values) =>
        _values = values ?? new Dictionary<string, double>();

    /// <summary>Значение параметра либо <paramref name="fallback"/>.</summary>
    public double Get(string name, double fallback) =>
        _values.TryGetValue(name, out var v) && !double.IsNaN(v) ? v : fallback;

    /// <summary>Значение, ограниченное диапазоном.</summary>
    public double Get(string name, double fallback, double min, double max) =>
        Math.Clamp(Get(name, fallback), min, max);
}
