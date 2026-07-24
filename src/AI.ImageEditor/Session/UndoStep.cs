using AI.ImageEditor.Commands;
using AI.ImageEditor.Model;
using SkiaSharp;

namespace AI.ImageEditor.Session;

/// <summary>
/// Шаг отмены. Хранит <b>минимум</b> данных для отката конкретной команды:
/// для пиксельных операций — предыдущий растр одного слоя, для кадрирования —
/// растры всех слоёв и размер, для структурных — просто обратное действие.
/// Полные снимки документа не делаются: на больших холстах это съело бы память.
/// </summary>
public sealed class UndoStep : IDisposable
{
    private readonly Func<ImageDocument, bool> _restore;
    private readonly List<IDisposable> _owned = [];

    /// <summary>Команда, породившая шаг (нужна для повтора).</summary>
    public EditCommand? Command { get; }

    private UndoStep(EditCommand? command, Func<ImageDocument, bool> restore)
    {
        Command = command;
        _restore = restore;
    }

    /// <summary>Откатывает изменение.</summary>
    public bool Restore(ImageDocument document) => _restore(document);

    // ── Фабрики под конкретные виды правок ──────────────────────────────────

    /// <summary>Откат пикселей одного слоя (кисть, ластик, фильтр).</summary>
    public static UndoStep ForLayerPixels(Layer layer, EditCommand command)
    {
        var layerId = layer.Id;
        var backup = layer.Bitmap.Copy();

        var step = new UndoStep(command, doc =>
        {
            var target = doc.Find(layerId);
            if (target is null) return false;
            target.ReplaceBitmap(backup.Copy());   // копия — исходник может пригодиться снова
            return true;
        });
        step._owned.Add(backup);
        return step;
    }

    /// <summary>Откат всего документа (кадрирование меняет все слои и размер).</summary>
    public static UndoStep ForWholeDocument(ImageDocument document, EditCommand command)
    {
        // Сохраняем растры всех слоёв в порядке стопки.
        var snapshots = document.Layers.Select(l => (l.Id, Bitmap: l.Bitmap.Copy())).ToList();

        var step = new UndoStep(command, doc =>
        {
            foreach (var (id, bmp) in snapshots)
                doc.Find(id)?.ReplaceBitmap(bmp.Copy());

            // Размер холста восстановится вместе с растрами слоёв.
            doc.ResizeCanvasTo(snapshots[0].Bitmap.Width, snapshots[0].Bitmap.Height);
            return true;
        });

        foreach (var (_, bmp) in snapshots) step._owned.Add(bmp);
        return step;
    }

    /// <summary>Возврат удалённого слоя на его позицию.</summary>
    public static UndoStep ForRestoredLayer(Layer backup, int index, EditCommand command)
    {
        var step = new UndoStep(command, doc =>
        {
            doc.InsertLayer(backup.Clone(backup.Id), index);
            return true;
        });
        step._owned.Add(backup);
        return step;
    }

    /// <summary>Структурная правка — откат задаётся лямбдой, копии растров не нужны.</summary>
    public static UndoStep ForStructure(ImageDocument document, EditCommand command,
        Func<ImageDocument, bool> restore) => new(command, restore);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var d in _owned) d.Dispose();
        _owned.Clear();
    }
}
