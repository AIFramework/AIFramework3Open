using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Office;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Vision;

namespace AI.Script.Media;

/// <summary>
/// Пространство <c>ocr</c>: скан или кадр становится документом.
/// </summary>
/// <remarks>
/// Итог это <c>doc</c>, а не строка: к скану договора применимо все из документов, от
/// <c>doc.extract</c> до точного счета, и у каждой строки есть адрес области, откуда она взята.
/// </remarks>
[ScriptModule("ocr", "Текст с изображения службой хоста: документ doc с областями", Version = "0.1", Group = "тексты")]
public sealed class OcrModule(IScriptTextRecognizer? recognizer) : IScriptAvailability
{
    private const string Reason = "распознавание текста хостом не подключено";

    /// <inheritdoc/>
    public string? Unavailable(string function) => recognizer is null ? Reason : null;

    [ScriptFn("read", "Текст изображения документом: строка на блок, адрес области у каждой",
        Example = "doc.text(ocr.read(\"act.jpg\"))", Returns = ScriptDocument.TypeName)]
    public async Task<ScriptDocument> Read(
        IScriptContext context,
        [ScriptParam("изображение cv либо путь к файлу картинки")] ScriptValue image)
    {
        IScriptTextRecognizer service = MediaCalls.Require(recognizer, "ocr.read", Reason);
        (byte[] data, string mediaType, string name) = await Source(context, image).ConfigureAwait(false);

        ScriptRecognition recognition = await MediaCalls
            .ExternalAsync(context, "ocr.read", token => service.ReadAsync(data, mediaType, token), result => (result.Tokens, result.Cost))
            .ConfigureAwait(false);

        var blocks = recognition.Lines
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .Select(line => new DocBlock(DocBlockKind.Paragraph, line.Text.Trim(),
                Address: line.Region is null ? null : $"область {line.Region}"))
            .ToList();

        context.CountAllocation(blocks.Count);

        return new ScriptDocument(name, "ocr", blocks);
    }

    private static async Task<(byte[] Data, string MediaType, string Name)> Source(IScriptContext context, ScriptValue image)
    {
        if (image.Type == ScriptType.Str)
        {
            string path = image.AsString("ocr.read");
            byte[] bytes = await context.Sandbox.ReadAsync(path, context.Cancellation).ConfigureAwait(false);

            return (bytes, MediaCalls.ImageType(path), path);
        }

        if (image.Type == ScriptType.Handle && image.AsHandle("ocr.read").Target is ColorImage color)
            return (color.Png(), "image/png", "изображение");

        throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"ocr.read: нужно изображение cv либо путь к картинке, а пришло {image.Type.ToName()}",
            "картинку открывает io.load, кадры дает video.frames");
    }
}
