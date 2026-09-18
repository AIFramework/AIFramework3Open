using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Text;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>io</c>: файлы хранилища прогона.
/// </summary>
/// <remarks>
/// Ни одна функция не открывает файл сама: байты читаются и пишутся через
/// <see cref="IScriptSandbox"/>. Где они лежат — в папке, в памяти, у хоста, — модуль не знает,
/// поэтому один и тот же скрипт работает над любым хранилищем, а «нельзя выйти за рабочую
/// папку» держится на хранилище, а не на аккуратности каждой функции.
/// </remarks>
[ScriptModule("io", "Файлы прогона: список, открытие по виду, чтение и запись", Version = "0.2", Group = "данные")]
public static partial class IoModule
{
    /// <summary>Предельный размер читаемого файла по умолчанию.</summary>
    public const long MaxFileBytes = 256L * 1024 * 1024;

    private static readonly UTF8Encoding s_utf8 = new(false);

    /// <summary>
    /// Открывает файл функцией, которая объявила его расширение.
    /// </summary>
    /// <remarks>
    /// Модель не обязана помнить, чем читается какой формат: она пишет <c>io.load</c>, а читателя
    /// выбирает расширение. Читателей объявляют сами модули (<see cref="ScriptFnAttribute.Reads"/>),
    /// поэтому <c>io</c> не знает ни про картинки, ни про книги Excel: подключили модуль к хосту
    /// — формат открылся, не подключили — отказ перечисляет то, что открывается.
    /// </remarks>
    [ScriptFn("load", "Открывает файл по его виду: таблица, JSON, текст, картинка",
        Example = "let t = io.load(\"sales.csv\")")]
    public static async Task<ScriptValue> Load(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("какие колонки таблицы взять; нет — все")] string[]? cols = null)
    {
        string extension = ScriptFileKinds.Extension(path);

        ScriptFunction reader = FindReader(context.Modules, extension) ?? throw new ScriptError(
            DiagnosticCodes.BadFileFormat,
            extension.Length == 0
                ? $"io.load: у файла '{path}' нет расширения, вид не определить"
                : $"io.load: файлы .{extension} этот хост не открывает",
            $"открываются: {string.Join(", ", KnownExtensions(context.Modules))}; что это за файл: io.info(\"{path}\")");

        var arguments = new ScriptValue[reader.Parameters.Count];

        for (int i = 0; i < arguments.Length; i++)
            arguments[i] = i == 0 ? ScriptValue.Str(path) : reader.Parameters[i].Default;

        // Колонки отдаются читателю, если он умеет не читать лишние (Parquet, базы); иначе
        // выбираются из прочитанной таблицы: итог тот же, а памяти уходит больше.
        int columns = reader.FindParameter("cols") is { } parameter ? reader.Parameters.ToList().IndexOf(parameter) : -1;

        if (cols is { Length: > 0 } && columns > 0) arguments[columns] = Marshaller.FromClr(cols);

        ScriptValue value = await reader.Invoke(arguments, context).ConfigureAwait(false);

        return cols is { Length: > 0 } && columns <= 0 && value.Type == ScriptType.Table
            ? ScriptValue.Table(value.AsTable().Select(cols))
            : value;
    }

    /// <summary>
    /// Записывает значение файлом той функцией, которая объявила это расширение.
    /// </summary>
    /// <remarks>
    /// Пара к <c>io.load</c>: модель пишет <c>io.save(отчёт, "итог.xlsx")</c>, а чем именно
    /// писать, решает расширение. Писатели объявляют себя сами
    /// (<see cref="ScriptFnAttribute.Writes"/>), поэтому <c>io</c> не знает ни про книги Excel,
    /// ни про картинки.
    /// </remarks>
    [ScriptFn("save", "Сохраняет значение файлом: таблицу, документ, картинку, текст",
        Example = "io.save(сводная, \"итог.csv\")")]
    public static async Task<string> Save(
        IScriptContext context,
        [ScriptParam("что сохранить")] ScriptValue value,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        string extension = ScriptFileKinds.Extension(path);

        ScriptFunction writer = FindWriter(context.Modules, extension, value) ?? throw new ScriptError(
            DiagnosticCodes.BadFileFormat,
            extension.Length == 0
                ? $"io.save: у пути '{path}' нет расширения, вид файла не определить"
                : $"io.save: файлы .{extension} этот хост не записывает",
            $"записываются: {string.Join(", ", KnownExtensions(context.Modules, writers: true))}");

        var arguments = new ScriptValue[writer.Parameters.Count];

        for (int i = 0; i < arguments.Length; i++)
        {
            arguments[i] = i switch
            {
                0 => value,
                1 => ScriptValue.Str(path),
                _ => writer.Parameters[i].Default,
            };
        }

        ScriptValue written = await writer.Invoke(arguments, context).ConfigureAwait(false);

        return written.Type == ScriptType.Str ? written.AsString() : path;
    }

    [ScriptFn("files", "Таблица файлов папки: путь, имя, вид, медиатип, размер, дата",
        Example = "io.files(mask: \"*.csv\") |> table.filter(row => row.size > 0)",
        Columns = "path,name,kind,media_type,size,modified")]
    public static async Task<ScriptTable> Files(
        IScriptContext context,
        [ScriptParam("маска имени")] string mask = "*",
        [ScriptParam("папка относительно рабочей")] string dir = ".")
    {
        IReadOnlyList<ScriptFileInfo> files = await context.Sandbox
            .ListAsync(dir, mask, context.Cancellation)
            .ConfigureAwait(false);

        return ScriptTable.Create(
        [
            Column("path", files, file => ScriptValue.Str(file.Path)),
            Column("name", files, file => ScriptValue.Str(file.Name)),
            Column("kind", files, file => ScriptValue.Str(file.Kind)),
            Column("media_type", files, file => ScriptValue.Str(file.MediaType)),
            Column("size", files, file => ScriptValue.Num(file.Size)),
            Column("modified", files, Modified),
        ]);
    }

    [ScriptFn("info", "Сведения о файле: путь, имя, вид, медиатип, размер, дата",
        Example = "io.info(\"sales.csv\").kind")]
    public static async Task<ScriptRecord> Info(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        ScriptFileInfo file = await Require(context, path).ConfigureAwait(false);

        return ScriptRecord.From(
        [
            Field("path", ScriptValue.Str(file.Path)),
            Field("name", ScriptValue.Str(file.Name)),
            Field("kind", ScriptValue.Str(file.Kind)),
            Field("media_type", ScriptValue.Str(file.MediaType)),
            Field("size", ScriptValue.Num(file.Size)),
            Field("modified", Modified(file)),
        ]);
    }

    [ScriptFn("read_csv", "Читает таблицу из CSV", Example = "io.read_csv(\"sales.csv\", sep: \";\")", Reads = "csv,tsv")]
    public static async Task<ScriptTable> ReadCsv(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("разделитель; пусто — определить автоматически")] string sep = "",
        [ScriptParam("есть ли строка заголовка")] bool header = true)
    {
        string text = await ReadText(context, path).ConfigureAwait(false);
        ScriptTable table = Csv.Parse(text, sep, header, path);

        context.CountAllocation((long)table.RowCount * Math.Max(1, table.ColumnCount));

        return table;
    }

    [ScriptFn("write_csv", "Пишет таблицу в CSV", Example = "t |> io.write_csv(\"out.csv\")", Writes = "csv")]
    public static Task<string> WriteCsv(
        IScriptContext context,
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("разделитель")] string sep = ",")
        => WriteText(context, path, Csv.Write(t, sep));

    [ScriptFn("read_json", "Читает значение из JSON", Example = "io.read_json(\"config.json\")", Reads = "json")]
    public static async Task<ScriptValue> ReadJson(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
        => Json.Parse(await ReadText(context, path).ConfigureAwait(false), path);

    [ScriptFn("write_json", "Пишет значение в JSON", Example = "cfg |> io.write_json(\"config.json\")", Writes = "json")]
    public static Task<string> WriteJson(
        IScriptContext context,
        [ScriptParam("значение")] ScriptValue value,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("человекочитаемое форматирование")] bool pretty = true)
        => WriteText(context, path, Json.Write(value, pretty));

    [ScriptFn("read_text", "Читает файл целиком", Example = "io.read_text(\"notes.md\")", Reads = "txt,md,log")]
    public static Task<string> ReadText(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
        => ReadAll(context, path);

    [ScriptFn("write_text", "Пишет текст в файл", Example = "text |> io.write_text(\"notes.md\")",
        Writes = "txt,md,log")]
    public static Task<string> WriteTextFile(
        IScriptContext context,
        [ScriptParam("текст")] string text,
        [ScriptParam("путь относительно рабочей папки")] string path)
        => WriteText(context, path, text);

    [ScriptFn("read_lines", "Читает файл списком строк", Example = "io.read_lines(\"log.txt\")")]
    public static async Task<ScriptList> ReadLines(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        string[] lines = (await ReadAll(context, path).ConfigureAwait(false))
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');

        var items = new ScriptValue[lines.Length];

        for (int i = 0; i < lines.Length; i++) items[i] = ScriptValue.Str(lines[i]);

        return ScriptList.Own(items);
    }

    [ScriptFn("ls", "Перечисляет файлы рабочей папки", Example = "io.ls(\".\", mask: \"*.csv\")")]
    public static async Task<string[]> List(
        IScriptContext context,
        [ScriptParam("папка относительно рабочей")] string dir = ".",
        [ScriptParam("маска имени")] string mask = "*")
    {
        IReadOnlyList<ScriptFileInfo> files = await context.Sandbox
            .ListAsync(dir, mask, context.Cancellation)
            .ConfigureAwait(false);

        return [.. files.Select(file => file.Path)];
    }

    [ScriptFn("exists", "Есть ли такой файл", Example = "io.exists(\"sales.csv\")")]
    public static async Task<bool> Exists(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        try
        {
            return await context.Sandbox.InfoAsync(path, context.Cancellation).ConfigureAwait(false) != null;
        }
        catch (ScriptError)
        {
            return false;
        }
    }

    [ScriptFn("workdir", "Рабочая папка прогона", Example = "print(io.workdir())")]
    public static string Workdir(IScriptContext context) => context.Sandbox.Root;

    private static async Task<ScriptFileInfo> Require(IScriptContext context, string path)
    {
        ScriptFileInfo? file = await context.Sandbox.InfoAsync(path, context.Cancellation).ConfigureAwait(false);

        return file ?? throw new ScriptError(
            DiagnosticCodes.FileNotFound,
            $"файл не найден: '{path}'",
            $"рабочая папка прогона: {context.Sandbox.Root}\nсписок файлов: io.files() либо io.ls(\".\")");
    }

    /// <summary>
    /// Читает файл целиком, проверяя путь и размер.
    /// </summary>
    /// <remarks>
    /// Размер проверяется ДО чтения: файл на десять гигабайт положил бы процесс раньше, чем
    /// сработал бы потолок памяти на значения.
    /// </remarks>
    private static async Task<string> ReadAll(IScriptContext context, string path)
    {
        ScriptFileInfo file = await Require(context, path).ConfigureAwait(false);

        if (file.Size > MaxFileBytes)
        {
            throw new ScriptError(
                DiagnosticCodes.MemoryLimit,
                $"файл '{path}' занимает {file.Size / (1024 * 1024)} МБ при потолке {MaxFileBytes / (1024 * 1024)} МБ");
        }

        context.CountAllocation(file.Size / 8);

        byte[] bytes = await context.Sandbox.ReadAsync(path, context.Cancellation).ConfigureAwait(false);

        using var reader = new StreamReader(new MemoryStream(bytes), s_utf8, detectEncodingFromByteOrderMarks: true);

        return await reader.ReadToEndAsync(context.Cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Пишет текст файлом и отмечает его артефактом прогона.
    /// </summary>
    /// <remarks>
    /// Отметка стоит здесь, а не в <c>io.save</c>: скрипт вправе записать файл и напрямую, а
    /// хост обязан узнать о нём в любом случае — иначе отчёт остаётся в хранилище, о котором
    /// пользователю никто не сказал.
    /// </remarks>
    private static async Task<string> WriteText(IScriptContext context, string path, string text)
    {
        byte[] bytes = s_utf8.GetBytes(text);

        await context.Sandbox.WriteAsync(path, bytes, context.Cancellation).ConfigureAwait(false);

        context.FileSaved(new ScriptFileInfo(path, bytes.LongLength, DateTimeOffset.UtcNow));

        return path;
    }

    private static ScriptFunction? FindReader(IReadOnlyList<IScriptModule> modules, string extension)
    {
        if (extension.Length == 0) return null;

        foreach (IScriptModule module in modules)
        {
            foreach (ScriptFunction function in module.Functions)
            {
                if (IsReader(function) && function.Reads.Contains(extension, StringComparer.OrdinalIgnoreCase)) return function;
            }
        }

        return null;
    }

    /// <summary>
    /// Писатель для расширения и значения.
    /// </summary>
    /// <remarks>
    /// Одно расширение пишут разные функции: «.md» — и текст, и документ. Выбирается та, что
    /// принимает значение этого типа; не нашлось такой — первая объявившая расширение, и тогда
    /// отказ по типу скажет, что именно не так.
    /// </remarks>
    private static ScriptFunction? FindWriter(IReadOnlyList<IScriptModule> modules, string extension, ScriptValue value)
    {
        if (extension.Length == 0) return null;

        ScriptFunction? first = null;

        foreach (IScriptModule module in modules)
        {
            foreach (ScriptFunction function in module.Functions)
            {
                if (!IsWriter(function) || !function.Writes.Contains(extension, StringComparer.OrdinalIgnoreCase)) continue;

                if (function.Parameters.Count > 0 && function.Parameters[0].Type == value.Type) return function;

                first ??= function;
            }
        }

        return first;
    }

    private static IEnumerable<string> KnownExtensions(IReadOnlyList<IScriptModule> modules, bool writers = false) =>
        modules
            .SelectMany(module => module.Functions)
            .Where(function => writers ? IsWriter(function) : IsReader(function))
            .SelectMany(function => writers ? function.Writes : function.Reads)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal);

    /// <summary>Читатель — функция, которой хватает одного пути: остальное у неё по умолчанию.</summary>
    private static bool IsReader(ScriptFunction function) => function.Reads.Count > 0 && function.RequiredCount == 1;

    /// <summary>Писатель — функция, которой хватает значения и пути.</summary>
    private static bool IsWriter(ScriptFunction function) => function.Writes.Count > 0 && function.RequiredCount <= 2;

    private static ScriptColumn Column(string name, IReadOnlyList<ScriptFileInfo> files, Func<ScriptFileInfo, ScriptValue> value) =>
        ScriptColumn.From(name, files.Select(value));

    private static ScriptValue Modified(ScriptFileInfo file) =>
        file.Modified is DateTimeOffset moment ? ScriptValue.Date(moment.UtcDateTime) : ScriptValue.None;

    private static KeyValuePair<string, ScriptValue> Field(string name, ScriptValue value) => new(name, value);
}
