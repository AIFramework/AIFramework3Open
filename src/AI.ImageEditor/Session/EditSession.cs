using AI.ImageEditor.Commands;
using AI.ImageEditor.Filters;
using AI.ImageEditor.Model;
using AI.ImageEditor.Ops;
using SkiaSharp;

namespace AI.ImageEditor.Session;

/// <summary>
/// Сессия редактирования: документ + применение команд + история отмены + версия.
/// <para>
/// Сервер держит сессию как источник правды. Клиент рисует у себя мгновенно и шлёт
/// компактные команды; <see cref="Version"/> растёт на каждой применённой команде,
/// поэтому после переподключения клиент сравнивает версию и, если разошлись,
/// запрашивает актуальный кадр — правки при этом не теряются.
/// </para>
/// </summary>
public sealed class EditSession : IDisposable
{
    private readonly List<UndoStep> _undo = [];
    private readonly List<EditCommand> _redo = [];
    private readonly int _undoLimit;

    /// <summary>Документ сессии.</summary>
    public ImageDocument Document { get; }

    /// <summary>Версия состояния: +1 на каждую успешно применённую команду.</summary>
    public int Version { get; private set; }

    /// <summary>Есть ли что отменять.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Есть ли что повторять.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Создаёт сессию поверх документа.</summary>
    public EditSession(ImageDocument document, int undoLimit = 20)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _undoLimit = Math.Max(1, undoLimit);
    }

    /// <summary>Создаёт сессию из изображения.</summary>
    public static EditSession FromBitmap(SKBitmap bitmap) => new(ImageDocument.FromBitmap(bitmap));

    /// <summary>
    /// Применяет команду. Возвращает <c>true</c>, если состояние изменилось
    /// (тогда версия увеличена и в историю отмены добавлен шаг).
    /// </summary>
    public bool Apply(EditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var step = Execute(command);
        if (step is null) return false;

        PushUndo(step);
        _redo.Clear();          // новая ветка правок обнуляет повтор
        Version++;
        return true;
    }

    /// <summary>Отменяет последнюю команду.</summary>
    public bool Undo()
    {
        if (_undo.Count == 0) return false;

        var step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        step.Restore(Document);
        step.Dispose();

        if (step.Command is not null) _redo.Add(step.Command);
        Version++;
        return true;
    }

    /// <summary>Повторяет отменённую команду.</summary>
    public bool Redo()
    {
        if (_redo.Count == 0) return false;

        var command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);

        var step = Execute(command);
        if (step is null) return false;

        PushUndo(step);
        Version++;
        return true;
    }

    /// <summary>Кодирует текущее состояние (для ресинка клиента и сохранения).</summary>
    public byte[] Encode(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int quality = 92) =>
        Document.Encode(format, quality);

    /// <summary>
    /// Полностью заменяет пиксели активного слоя (результат внешней обработки —
    /// например, ИИ-правки). Операция попадает в историю, поэтому её можно отменить.
    /// Растр приводится к размеру холста, чтобы слои оставались согласованными.
    /// </summary>
    public bool ReplaceActiveLayer(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var layer = Document.ActiveLayer;
        if (layer is null) return false;

        var undo = UndoStep.ForLayerPixels(layer, command: null);

        var target = new SKBitmap(Pixels.PixelBuffer.InfoFor(Document.Width, Document.Height));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            // Вписываем с сохранением пропорций: модель может вернуть другой размер.
            var scale = Math.Min((float)Document.Width / bitmap.Width, (float)Document.Height / bitmap.Height);
            var w = bitmap.Width * scale;
            var h = bitmap.Height * scale;
            canvas.DrawBitmap(bitmap, new SKRect((Document.Width - w) / 2, (Document.Height - h) / 2,
                (Document.Width + w) / 2, (Document.Height + h) / 2));
        }

        layer.ReplaceBitmap(target);
        PushUndo(undo);
        _redo.Clear();
        Version++;
        return true;
    }

    // ── Исполнение команд ───────────────────────────────────────────────────

    /// <summary>Выполняет команду и возвращает шаг отмены (null — команда не применилась).</summary>
    private UndoStep? Execute(EditCommand c)
    {
        switch (c.Type)
        {
            case EditCommandTypes.Stroke: return ExecuteStroke(c);
            case EditCommandTypes.Filter: return ExecuteFilter(c);
            case EditCommandTypes.Crop: return ExecuteCrop(c);
            case EditCommandTypes.LayerAdd: return ExecuteLayerAdd(c);
            case EditCommandTypes.LayerRemove: return ExecuteLayerRemove(c);
            case EditCommandTypes.LayerMove: return ExecuteLayerMove(c);
            case EditCommandTypes.LayerProps: return ExecuteLayerProps(c);
            case EditCommandTypes.LayerSelect: return ExecuteLayerSelect(c);
            default: return null;
        }
    }

    private UndoStep? ExecuteStroke(EditCommand c)
    {
        var layer = Target(c);
        if (layer is null || c.Points is null || c.Points.Length < 2) return null;

        var points = new List<StrokePoint>(c.Points.Length / 2);
        for (var i = 0; i + 1 < c.Points.Length; i += 2)
            points.Add(new StrokePoint(c.Points[i], c.Points[i + 1]));

        var undo = UndoStep.ForLayerPixels(layer, c);
        Painter.Stroke(layer, points, new BrushSettings(
            c.Radius, FromArgb(c.Color), c.Hardness, c.Opacity, c.Erase));
        return undo;
    }

    private UndoStep? ExecuteFilter(EditCommand c)
    {
        var layer = Target(c);
        if (layer is null || string.IsNullOrWhiteSpace(c.FilterId)) return null;

        var filter = FilterRegistry.Create(c.FilterId!, new FilterParams(c.Params ?? []));
        if (filter is null) return null;

        var undo = UndoStep.ForLayerPixels(layer, c);
        var buffer = layer.ReadPixels();
        filter.Apply(buffer);
        layer.WritePixels(buffer);
        return undo;
    }

    private UndoStep? ExecuteCrop(EditCommand c)
    {
        if (c.Width <= 0 || c.Height <= 0) return null;

        var undo = UndoStep.ForWholeDocument(Document, c);
        if (!Document.Crop(new SKRectI(c.X, c.Y, c.X + c.Width, c.Y + c.Height)))
        {
            undo.Dispose();
            return null;
        }
        return undo;
    }

    private UndoStep ExecuteLayerAdd(EditCommand c)
    {
        var layer = Document.AddLayer(c.Name);
        return UndoStep.ForStructure(Document, c, doc => doc.RemoveLayer(layer.Id));
    }

    private UndoStep? ExecuteLayerRemove(EditCommand c)
    {
        var layer = Target(c);
        if (layer is null) return null;

        // Для отмены сохраняем копию слоя и его позицию.
        var index = Document.Layers.ToList().IndexOf(layer);
        var backup = layer.Clone(layer.Id);
        if (!Document.RemoveLayer(layer.Id)) { backup.Dispose(); return null; }

        return UndoStep.ForRestoredLayer(backup, index, c);
    }

    private UndoStep? ExecuteLayerMove(EditCommand c)
    {
        var layer = Target(c);
        if (layer is null) return null;

        var oldIndex = Document.Layers.ToList().IndexOf(layer);
        if (!Document.MoveLayer(layer.Id, c.Index)) return null;

        var id = layer.Id;
        return UndoStep.ForStructure(Document, c, doc => doc.MoveLayer(id, oldIndex));
    }

    private UndoStep? ExecuteLayerProps(EditCommand c)
    {
        var layer = Target(c);
        if (layer is null) return null;

        var (oldVisible, oldOpacity, oldName) = (layer.Visible, layer.Opacity, layer.Name);
        if (c.Visible is { } v) layer.Visible = v;
        if (c.LayerOpacity is { } o) layer.Opacity = Math.Clamp(o, 0, 1);
        if (!string.IsNullOrWhiteSpace(c.Name)) layer.Name = c.Name!;

        var id = layer.Id;
        return UndoStep.ForStructure(Document, c, doc =>
        {
            var l = doc.Find(id);
            if (l is null) return false;
            l.Visible = oldVisible; l.Opacity = oldOpacity; l.Name = oldName;
            return true;
        });
    }

    private UndoStep? ExecuteLayerSelect(EditCommand c)
    {
        if (c.LayerId is null || Document.Find(c.LayerId) is null) return null;

        var old = Document.ActiveLayerId;
        Document.ActiveLayerId = c.LayerId;
        return UndoStep.ForStructure(Document, c, doc => { doc.ActiveLayerId = old; return true; });
    }

    /// <summary>Слой команды либо активный.</summary>
    private Layer? Target(EditCommand c) =>
        c.LayerId is not null ? Document.Find(c.LayerId) : Document.ActiveLayer;

    private void PushUndo(UndoStep step)
    {
        _undo.Add(step);
        while (_undo.Count > _undoLimit)
        {
            _undo[0].Dispose();
            _undo.RemoveAt(0);
        }
    }

    /// <summary>0xAARRGGBB → SKColor.</summary>
    private static SKColor FromArgb(uint argb) => new(
        (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF),
        (byte)((argb >> 24) & 0xFF));

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var s in _undo) s.Dispose();
        _undo.Clear();
        _redo.Clear();
        Document.Dispose();
    }
}
