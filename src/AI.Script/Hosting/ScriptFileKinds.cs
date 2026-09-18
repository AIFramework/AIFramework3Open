namespace AI.Script.Hosting;

/// <summary>
/// Вид и медиатип файла по расширению.
/// </summary>
/// <remarks>
/// Вид — то, что нужно автору скрипта («это таблица»), медиатип — то, что нужно хосту, чтобы
/// показать файл. Оба выводятся из одного списка, чтобы не разойтись. Неизвестное расширение
/// — вид <c>other</c>, а не отказ: перечислить файл можно, даже если открыть его нечем.
/// </remarks>
public static class ScriptFileKinds
{
    private static readonly Dictionary<string, (string Kind, string MediaType)> s_known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csv"] = ("table", "text/csv"),
        ["tsv"] = ("table", "text/tab-separated-values"),
        ["xlsx"] = ("table", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        ["xls"] = ("table", "application/vnd.ms-excel"),
        ["ods"] = ("table", "application/vnd.oasis.opendocument.spreadsheet"),
        ["parquet"] = ("table", "application/vnd.apache.parquet"),
        ["sqlite"] = ("table", "application/vnd.sqlite3"),
        ["json"] = ("data", "application/json"),
        ["xml"] = ("data", "application/xml"),
        ["yaml"] = ("data", "application/yaml"),
        ["yml"] = ("data", "application/yaml"),
        ["txt"] = ("text", "text/plain"),
        ["md"] = ("text", "text/markdown"),
        ["log"] = ("text", "text/plain"),
        ["html"] = ("text", "text/html"),
        ["htm"] = ("text", "text/html"),
        ["docx"] = ("document", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"),
        ["doc"] = ("document", "application/msword"),
        ["odt"] = ("document", "application/vnd.oasis.opendocument.text"),
        ["rtf"] = ("document", "application/rtf"),
        ["pdf"] = ("document", "application/pdf"),
        ["pptx"] = ("document", "application/vnd.openxmlformats-officedocument.presentationml.presentation"),
        ["png"] = ("image", "image/png"),
        ["jpg"] = ("image", "image/jpeg"),
        ["jpeg"] = ("image", "image/jpeg"),
        ["gif"] = ("image", "image/gif"),
        ["bmp"] = ("image", "image/bmp"),
        ["webp"] = ("image", "image/webp"),
        ["svg"] = ("image", "image/svg+xml"),
        ["wav"] = ("audio", "audio/wav"),
        ["mp3"] = ("audio", "audio/mpeg"),
        ["ogg"] = ("audio", "audio/ogg"),
        ["m4a"] = ("audio", "audio/mp4"),
        ["flac"] = ("audio", "audio/flac"),
        ["mp4"] = ("video", "video/mp4"),
        ["mov"] = ("video", "video/quicktime"),
        ["webm"] = ("video", "video/webm"),
        ["avi"] = ("video", "video/x-msvideo"),
        ["mkv"] = ("video", "video/x-matroska"),
        ["zip"] = ("archive", "application/zip"),
    };

    /// <summary>Расширение без точки в нижнем регистре; пусто, если его нет.</summary>
    public static string Extension(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        string name = path[(Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\')) + 1)..];
        int dot = name.LastIndexOf('.');

        return dot <= 0 || dot == name.Length - 1 ? string.Empty : name[(dot + 1)..].ToLowerInvariant();
    }

    /// <summary>Вид файла: <c>table</c>, <c>data</c>, <c>text</c>, <c>document</c>, <c>image</c>, <c>audio</c>, <c>video</c>, <c>archive</c> либо <c>other</c>.</summary>
    public static string KindOf(string path) =>
        s_known.TryGetValue(Extension(path), out var known) ? known.Kind : "other";

    /// <summary>Медиатип по расширению; <c>application/octet-stream</c>, если оно неизвестно.</summary>
    public static string MediaTypeOf(string path) =>
        s_known.TryGetValue(Extension(path), out var known) ? known.MediaType : "application/octet-stream";
}
