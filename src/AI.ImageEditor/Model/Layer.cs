using AI.ImageEditor.Pixels;
using SkiaSharp;

namespace AI.ImageEditor.Model;

/// <summary>
/// Слой изображения: собственный растр в размерах документа + параметры наложения.
/// Владеет своим <see cref="SKBitmap"/> и освобождает его в <see cref="Dispose"/>.
/// </summary>
public sealed class Layer : IDisposable
{
    /// <summary>Идентификатор слоя (стабильный, уходит в командах с клиента).</summary>
    public string Id { get; }

    /// <summary>Отображаемое имя.</summary>
    public string Name { get; set; }

    /// <summary>Растр слоя (BGRA, размеры документа).</summary>
    public SKBitmap Bitmap { get; private set; }

    /// <summary>Непрозрачность 0..1.</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>Видимость.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Режим наложения на нижележащие слои.</summary>
    public SKBlendMode BlendMode { get; set; } = SKBlendMode.SrcOver;

    /// <summary>Создаёт пустой (прозрачный) слой.</summary>
    public Layer(string id, string name, int width, int height)
    {
        Id = id;
        Name = name;
        Bitmap = new SKBitmap(PixelBuffer.InfoFor(width, height));
        Bitmap.Erase(SKColors.Transparent);
    }

    /// <summary>Создаёт слой поверх готового растра (растр переходит во владение слоя).</summary>
    public Layer(string id, string name, SKBitmap bitmap)
    {
        Id = id;
        Name = name;
        Bitmap = bitmap;
    }

    /// <summary>Заменяет растр слоя (старый освобождается).</summary>
    public void ReplaceBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var old = Bitmap;
        Bitmap = bitmap;
        old.Dispose();
    }

    /// <summary>Читает пиксели слоя в буфер для фильтрации.</summary>
    public PixelBuffer ReadPixels() => PixelBuffer.FromBitmap(Bitmap);

    /// <summary>Записывает буфер обратно в растр слоя.</summary>
    public void WritePixels(PixelBuffer buffer) => ReplaceBitmap(buffer.ToBitmap());

    /// <summary>Глубокая копия слоя (для undo и дублирования).</summary>
    public Layer Clone(string newId) =>
        new(newId, Name, Bitmap.Copy())
        {
            Opacity = Opacity,
            Visible = Visible,
            BlendMode = BlendMode
        };

    /// <inheritdoc />
    public void Dispose() => Bitmap.Dispose();
}
