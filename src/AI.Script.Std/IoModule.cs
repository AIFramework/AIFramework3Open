using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Text;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>io</c>: чтение и запись файлов внутри песочницы.
/// </summary>
/// <remarks>
/// Ни одна функция не открывает файл по пути из скрипта напрямую: путь всегда проходит через
/// <see cref="IScriptSandbox"/>. Так «нельзя выйти за рабочую папку» перестаёт зависеть от
/// аккуратности каждой отдельной функции — а функций будет много.
/// </remarks>
[ScriptModule("io", "Чтение и запись файлов в рабочей папке прогона", Version = "0.1")]
public static class IoModule
{
    /// <summary>Предельный размер читаемого файла по умолчанию.</summary>
    public const long MaxFileBytes = 256L * 1024 * 1024;

    [ScriptFn("read_csv", "Читает таблицу из CSV", Example = "io.read_csv(\"sales.csv\", sep: \";\")")]
    public static ScriptTable ReadCsv(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("разделитель; пусто — определить автоматически")] string sep = "",
        [ScriptParam("есть ли строка заголовка")] bool header = true)
    {
        string text = ReadAll(context, path);
        ScriptTable table = Csv.Parse(text, sep, header, path);

        context.CountAllocation((long)table.RowCount * Math.Max(1, table.ColumnCount));

        return table;
    }

    [ScriptFn("write_csv", "Пишет таблицу в CSV", Example = "t |> io.write_csv(\"out.csv\")")]
    public static string WriteCsv(
        IScriptContext context,
        [ScriptParam("таблица")] ScriptTable t,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("разделитель")] string sep = ",")
    {
        string full = context.Sandbox.Resolve(path, forWriting: true);

        File.WriteAllText(full, Csv.Write(t, sep), new UTF8Encoding(false));

        return path;
    }

    [ScriptFn("read_json", "Читает значение из JSON", Example = "io.read_json(\"config.json\")")]
    public static ScriptValue ReadJson(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
        => Json.Parse(ReadAll(context, path), path);

    [ScriptFn("write_json", "Пишет значение в JSON", Example = "cfg |> io.write_json(\"config.json\")")]
    public static string WriteJson(
        IScriptContext context,
        [ScriptParam("значение")] ScriptValue value,
        [ScriptParam("путь относительно рабочей папки")] string path,
        [ScriptParam("человекочитаемое форматирование")] bool pretty = true)
    {
        string full = context.Sandbox.Resolve(path, forWriting: true);

        File.WriteAllText(full, Json.Write(value, pretty), new UTF8Encoding(false));

        return path;
    }

    [ScriptFn("read_text", "Читает файл целиком", Example = "io.read_text(\"notes.md\")")]
    public static string ReadText(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
        => ReadAll(context, path);

    [ScriptFn("write_text", "Пишет текст в файл", Example = "text |> io.write_text(\"notes.md\")")]
    public static string WriteText(
        IScriptContext context,
        [ScriptParam("текст")] string text,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        string full = context.Sandbox.Resolve(path, forWriting: true);

        File.WriteAllText(full, text, new UTF8Encoding(false));

        return path;
    }

    [ScriptFn("read_lines", "Читает файл списком строк", Example = "io.read_lines(\"log.txt\")")]
    public static ScriptList ReadLines(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        string[] lines = ReadAll(context, path)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');

        var items = new ScriptValue[lines.Length];

        for (int i = 0; i < lines.Length; i++) items[i] = ScriptValue.Str(lines[i]);

        return ScriptList.Own(items);
    }

    [ScriptFn("ls", "Перечисляет файлы рабочей папки", Example = "io.ls(\".\", mask: \"*.csv\")")]
    public static string[] List(
        IScriptContext context,
        [ScriptParam("папка относительно рабочей")] string dir = ".",
        [ScriptParam("маска имени")] string mask = "*")
        => [.. context.Sandbox.List(dir, mask)];

    [ScriptFn("exists", "Есть ли такой файл", Example = "io.exists(\"sales.csv\")")]
    public static bool Exists(
        IScriptContext context,
        [ScriptParam("путь относительно рабочей папки")] string path)
    {
        try
        {
            return File.Exists(context.Sandbox.Resolve(path, forWriting: false));
        }
        catch (ScriptError)
        {
            return false;
        }
    }

    [ScriptFn("workdir", "Рабочая папка прогона", Example = "print(io.workdir())")]
    public static string Workdir(IScriptContext context) => context.Sandbox.Root;

    /// <summary>
    /// Читает файл целиком, проверяя путь и размер.
    /// </summary>
    /// <remarks>
    /// Размер проверяется ДО чтения: файл на десять гигабайт положил бы процесс раньше, чем
    /// сработал бы потолок памяти на значения.
    /// </remarks>
    private static string ReadAll(IScriptContext context, string path)
    {
        string full = context.Sandbox.Resolve(path, forWriting: false);

        if (!File.Exists(full))
        {
            throw new ScriptError(
                DiagnosticCodes.FileNotFound,
                $"файл не найден: '{path}'",
                $"рабочая папка прогона: {context.Sandbox.Root}\nсписок файлов: io.ls(\".\")");
        }

        var info = new FileInfo(full);

        if (info.Length > MaxFileBytes)
        {
            throw new ScriptError(
                DiagnosticCodes.MemoryLimit,
                $"файл '{path}' занимает {info.Length / (1024 * 1024)} МБ при потолке {MaxFileBytes / (1024 * 1024)} МБ");
        }

        context.CountAllocation(info.Length / 8);

        return File.ReadAllText(full);
    }
}
