using AI.ImageEditor.Pixels;
using SkiaSharp;

namespace AI.ImageEditor.Model;

/// <summary>
/// Документ редактора: холст фиксированного размера и стопка слоёв (снизу вверх).
/// Единственный владелец слоёв — освобождает их в <see cref="Dispose"/>.
/// </summary>
public sealed class ImageDocument : IDisposable
{
    private readonly List<Layer> _layers = [];
    private int _nextLayerNumber = 1;

    /// <summary>Ширина холста.</summary>
    public int Width { get; private set; }

    /// <summary>Высота холста.</summary>
    public int Height { get; private set; }

    /// <summary>Слои снизу вверх (индекс 0 — самый нижний).</summary>
    public IReadOnlyList<Layer> Layers => _layers;

    /// <summary>Идентификатор активного слоя (по нему работают кисть и фильтры).</summary>
    public string? ActiveLayerId { get; set; }

    /// <summary>Создаёт документ с одним пустым слоем.</summary>
    public ImageDocument(int width, int height)
    {
        Width = width;
        Height = height;
        var layer = NewLayer("Фон");
        _layers.Add(layer);
        ActiveLayerId = layer.Id;
    }

    /// <summary>Создаёт документ из готового изображения (становится нижним слоем).</summary>
    public static ImageDocument FromBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var doc = new ImageDocument(bitmap.Width, bitmap.Height);
        // Приводим к рабочему формату документа (BGRA), чтобы фильтры не перекодировали.
        var normalized = new SKBitmap(PixelBuffer.InfoFor(bitmap.Width, bitmap.Height));
        using (var canvas = new SKCanvas(normalized))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(bitmap, 0, 0);
        }

        doc._layers[0].ReplaceBitmap(normalized);
        doc._layers[0].Name = "Изображение";
        return doc;
    }

    /// <summary>Активный слой либо верхний, если активный не задан.</summary>
    public Layer? ActiveLayer =>
        (ActiveLayerId is not null ? _layers.FirstOrDefault(l => l.Id == ActiveLayerId) : null)
        ?? _layers.LastOrDefault();

    /// <summary>Находит слой по идентификатору.</summary>
    public Layer? Find(string layerId) => _layers.FirstOrDefault(l => l.Id == layerId);

    /// <summary>Добавляет пустой слой сверху и делает его активным.</summary>
    public Layer AddLayer(string? name = null)
    {
        var layer = NewLayer(name ?? $"Слой {_nextLayerNumber}");
        _layers.Add(layer);
        ActiveLayerId = layer.Id;
        return layer;
    }

    /// <summary>Удаляет слой. Последний слой удалить нельзя — документ не может быть пустым.</summary>
    public bool RemoveLayer(string layerId)
    {
        if (_layers.Count <= 1) return false;

        var layer = Find(layerId);
        if (layer is null) return false;

        _layers.Remove(layer);
        layer.Dispose();

        if (ActiveLayerId == layerId)
            ActiveLayerId = _layers[^1].Id;

        return true;
    }

    /// <summary>Вставляет готовый слой на позицию (используется откатом удаления).</summary>
    public void InsertLayer(Layer layer, int index)
    {
        ArgumentNullException.ThrowIfNull(layer);
        _layers.Insert(Math.Clamp(index, 0, _layers.Count), layer);
    }

    /// <summary>
    /// Синхронизирует размер холста с размером слоёв. Нужен откату кадрирования:
    /// растры слоёв уже восстановлены, остаётся вернуть объявленный размер.
    /// </summary>
    public void ResizeCanvasTo(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        Width = width;
        Height = height;
    }

    /// <summary>Перемещает слой на новую позицию в стопке.</summary>
    public bool MoveLayer(string layerId, int newIndex)
    {
        var layer = Find(layerId);
        if (layer is null) return false;

        newIndex = Math.Clamp(newIndex, 0, _layers.Count - 1);
        _layers.Remove(layer);
        _layers.Insert(newIndex, layer);
        return true;
    }

    /// <summary>
    /// Кадрирование: все слои обрезаются одним прямоугольником, холст меняет размер.
    /// Прямоугольник зажимается в границы документа.
    /// </summary>
    public bool Crop(SKRectI rect)
    {
        var clipped = SKRectI.Intersect(rect, new SKRectI(0, 0, Width, Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return false;
        if (clipped.Width == Width && clipped.Height == Height && clipped.Left == 0 && clipped.Top == 0)
            return false;   // нечего менять

        foreach (var layer in _layers)
        {
            var cropped = new SKBitmap(PixelBuffer.InfoFor(clipped.Width, clipped.Height));
            using (var canvas = new SKCanvas(cropped))
            {
                canvas.Clear(SKColors.Transparent);
                // Сдвигаем исходный слой так, чтобы левый верхний угол рамки попал в (0,0).
                canvas.DrawBitmap(layer.Bitmap, -clipped.Left, -clipped.Top);
            }
            layer.ReplaceBitmap(cropped);
        }

        Width = clipped.Width;
        Height = clipped.Height;
        return true;
    }

    /// <summary>
    /// Сводит видимые слои в один растр (снизу вверх, с учётом непрозрачности
    /// и режима наложения). Skia делает это быстро и корректно.
    /// </summary>
    public SKBitmap Flatten()
    {
        var result = new SKBitmap(PixelBuffer.InfoFor(Width, Height));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        foreach (var layer in _layers)
        {
            if (!layer.Visible || layer.Opacity <= 0) continue;

            using var paint = new SKPaint
            {
                // Альфа краски модулирует непрозрачность слоя.
                Color = SKColors.White.WithAlpha((byte)Math.Clamp(layer.Opacity * 255, 0, 255)),
                BlendMode = layer.BlendMode
            };
            canvas.DrawBitmap(layer.Bitmap, 0, 0, paint);
        }

        return result;
    }

    /// <summary>Кодирует сведённое изображение (PNG сохраняет прозрачность).</summary>
    public byte[] Encode(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 92)
    {
        using var flat = Flatten();
        using var image = SKImage.FromBitmap(flat);
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    private Layer NewLayer(string name) =>
        new($"L{_nextLayerNumber++}", name, Width, Height);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var layer in _layers) layer.Dispose();
        _layers.Clear();
    }
}
