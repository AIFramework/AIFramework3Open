using System.Globalization;
using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Office;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Media;

/// <summary>
/// Пространство <c>speech</c>: запись разговора становится документом с таймкодами.
/// </summary>
/// <remarks>
/// Как и у распознавания текста, итог это <c>doc</c>: реплика это блок, а ее время это адрес.
/// Файл уходит службе как есть, без перекодирования: mp3 из звонка расшифровывается и там, где
/// язык сам его не декодирует.
/// </remarks>
[ScriptModule("speech", "Расшифровка речи через службу хоста: документ с таймкодами", Version = "0.1", Group = "тексты")]
public sealed class SpeechModule(IScriptTranscriber? transcriber) : IScriptAvailability
{
    private const string Reason = "расшифровка речи хостом не подключена";

    /// <inheritdoc/>
    public string? Unavailable(string function) => transcriber is null ? Reason : null;

    [ScriptFn("transcribe", "Расшифровка записи документом: отрезок речи на блок, время у каждого",
        Example = "doc.blocks(speech.transcribe(\"call.mp3\"))", Returns = ScriptDocument.TypeName)]
    public async Task<ScriptDocument> Transcribe(
        IScriptContext context,
        [ScriptParam("путь к записи либо звук audio")] ScriptValue audio)
    {
        IScriptTranscriber service = MediaCalls.Require(transcriber, "speech.transcribe", Reason);
        (byte[] data, string name) = await Source(context, audio).ConfigureAwait(false);

        ScriptTranscript transcript = await MediaCalls
            .ExternalAsync(context, "speech.transcribe", token => service.TranscribeAsync(data, name, token), result => (result.Tokens, result.Cost))
            .ConfigureAwait(false);

        var blocks = transcript.Segments
            .Where(segment => !string.IsNullOrWhiteSpace(segment.Text))
            .Select(segment => new DocBlock(DocBlockKind.Paragraph, segment.Text.Trim(),
                Address: $"{Stamp(segment.Start)}-{Stamp(segment.End)}"))
            .ToList();

        context.CountAllocation(blocks.Count);

        return new ScriptDocument(name, "speech", blocks);
    }

    /// <summary>Время отрезка часами, минутами и секундами.</summary>
    public static string Stamp(TimeSpan time) =>
        ((int)time.TotalHours).ToString("00", CultureInfo.InvariantCulture) + time.ToString(@"\:mm\:ss", CultureInfo.InvariantCulture);

    private static async Task<(byte[] Data, string Name)> Source(IScriptContext context, ScriptValue audio)
    {
        if (audio.Type == ScriptType.Str)
        {
            string path = audio.AsString("speech.transcribe");

            return (await context.Sandbox.ReadAsync(path, context.Cancellation).ConfigureAwait(false), path);
        }

        if (audio.Type == ScriptType.Record)
        {
            (float[] samples, int rate) = AudioModule.Samples(audio.AsRecord(), "speech.transcribe");

            return (Wav.Write(samples, rate), "audio.wav");
        }

        throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"speech.transcribe: нужен путь к записи либо звук audio, а пришло {audio.Type.ToName()}",
            "запись открывает audio.load, дорожку ролика дает video.audio");
    }
}
