
using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Чтение большого файла потоком.
/// </summary>
/// <remarks>
/// <c>io.load</c> читает файл целиком, и выгрузка из учетной системы на гигабайты в прогон не
/// ложится. <c>io.scan</c> читает запись за записью и оставляет только отобранное: условие,
/// колонки и предел строк работают прямо в читателе. CSV разбирается здесь; другие форматы
/// читает функция модуля, объявившая это расширение и умеющая отбирать (параметр <c>where</c>),
/// так <c>io</c> не знает ни про Parquet, ни про базы.
/// </remarks>
public static partial class IoModule
{
    /// <summary>Параметр, по которому читатель модуля признается потоковым.</summary>
    private const string WhereParameter = "where";

    [ScriptFn("scan", "Большой файл потоком: условие, колонки и предел строк прямо при чтении",
        Example = "io.scan(\"log.csv\", where: row => row.amount > 0, cols: [\"id\", \"amount\"], limit: 1000)")]
    public static async Task<ScriptTable> Scan(
        IScriptContext context,
        [ScriptParam("путь к файлу: csv, tsv либо формат потокового читателя")] string path,
        [ScriptParam("условие на строку-запись; нет — все строки")] ScriptCallable? where = null,
        [ScriptParam("какие колонки оставить; нет — все")] string[]? cols = null,
        [ScriptParam("сколько строк отобрать; 0 — без предела")] int limit = 0,
        [ScriptParam("разделитель CSV; пусто — определить по заголовку")] string sep = "")
    {
        string extension = ScriptFileKinds.Extension(path);

        if (extension is "csv" or "tsv" or "txt")
        {
            _ = await Require(context, path).ConfigureAwait(false);

            RowScan? scan = null;

            await using Stream stream = await context.Sandbox.OpenReadAsync(path, context.Cancellation).ConfigureAwait(false);
            using var reader = new StreamReader(stream, s_utf8, detectEncodingFromByteOrderMarks: true, bufferSize: 1 << 16);

            await new CsvStream(reader, path)
                .ScanAsync(sep.Length > 0 || extension != "tsv" ? sep : "\t",
                    names => scan = new RowScan(context, "io.scan", names, where, cols, limit), context.Cancellation)
                .ConfigureAwait(false);

            return scan?.Table() ?? ScriptTable.Empty;
        }

        ScriptFunction scanner = FindScanner(context.Modules, extension) ?? throw new ScriptError(
            DiagnosticCodes.BadFileFormat,
            $"io.scan: файлы .{extension} потоком этот хост не читает",
            $"потоком читаются: {string.Join(", ", ScanExtensions(context.Modules))}; целиком: io.load(\"{path}\")");

        return (await scanner.Invoke(Arguments(scanner, path, where, cols, limit), context).ConfigureAwait(false))
            .AsTable("io.scan");
    }

    /// <summary>Аргументы читателя по именам: путь, условие, колонки и предел, остальное по умолчанию.</summary>
    private static ScriptValue[] Arguments(ScriptFunction reader, string path, ScriptCallable? where, string[]? cols, int limit)
    {
        var arguments = new ScriptValue[reader.Parameters.Count];

        for (int i = 0; i < arguments.Length; i++)
        {
            arguments[i] = reader.Parameters[i].Name switch
            {
                _ when i == 0 => ScriptValue.Str(path),
                WhereParameter => where is null ? ScriptValue.None : ScriptValue.Fn(where),
                "cols" => cols is null ? ScriptValue.None : Marshaller.FromClr(cols),
                "limit" => ScriptValue.Num(limit),
                _ => reader.Parameters[i].Default,
            };
        }

        return arguments;
    }

    private static ScriptFunction? FindScanner(IReadOnlyList<IScriptModule> modules, string extension) =>
        modules.SelectMany(module => module.Functions).FirstOrDefault(function =>
            function.FindParameter(WhereParameter) is not null
            && function.Reads.Contains(extension, StringComparer.OrdinalIgnoreCase));

    private static IEnumerable<string> ScanExtensions(IReadOnlyList<IScriptModule> modules) =>
        new[] { "csv", "tsv", "txt" }.Concat(modules.SelectMany(module => module.Functions)
            .Where(function => function.FindParameter(WhereParameter) is not null)
            .SelectMany(function => function.Reads)).Distinct(StringComparer.OrdinalIgnoreCase);
}
