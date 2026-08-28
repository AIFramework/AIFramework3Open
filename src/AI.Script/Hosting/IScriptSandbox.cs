using AI.Script.Semantics;
using AI.Script.Runtime;

namespace AI.Script.Hosting;

/// <summary>
/// Доступ скрипта к файловой системе.
/// </summary>
/// <remarks>
/// Исполнение чужого кода — штатный сценарий языка, а не исключение: скрипт пишет модель.
/// Поэтому путь не «проверяется», а <b>выдаётся</b> песочницей: модуль <c>io</c> в принципе не
/// умеет открыть файл, о котором песочница не знает.
/// </remarks>
public interface IScriptSandbox
{
    /// <summary>Разрешена ли работа с файлами вообще.</summary>
    bool Enabled { get; }

    /// <summary>Корень рабочей папки для сообщений; может быть пустым.</summary>
    string Root { get; }

    /// <summary>
    /// Переводит путь из скрипта в абсолютный путь внутри песочницы.
    /// </summary>
    /// <param name="path">Путь, каким его написал скрипт.</param>
    /// <param name="forWriting">Открывается ли файл на запись.</param>
    string Resolve(string path, bool forWriting);

    /// <summary>Перечисляет файлы по маске внутри песочницы.</summary>
    /// <param name="directory">Папка относительно корня.</param>
    /// <param name="mask">Маска имени, например <c>*.csv</c>.</param>
    IReadOnlyList<string> List(string directory, string mask);
}

/// <summary>
/// Песочница, запрещающая любую работу с файлами.
/// </summary>
/// <remarks>
/// Значение по умолчанию: хост, которому файлы не нужны, не должен их случайно разрешить,
/// забыв настроить песочницу.
/// </remarks>
public sealed class DeniedSandbox : IScriptSandbox
{
    /// <summary>Единственный экземпляр.</summary>
    public static readonly DeniedSandbox Instance = new();

    private DeniedSandbox()
    {
    }

    /// <inheritdoc/>
    public bool Enabled => false;

    /// <inheritdoc/>
    public string Root => string.Empty;

    /// <inheritdoc/>
    public string Resolve(string path, bool forWriting) => throw Denied();

    /// <inheritdoc/>
    public IReadOnlyList<string> List(string directory, string mask) => throw Denied();

    private static ScriptError Denied() =>
        new(DiagnosticCodes.SandboxDenied,
            "работа с файлами запрещена настройками прогона",
            "хост должен задать рабочую папку: RunOptions.Sandbox = new WorkspaceSandbox(путь)");
}

/// <summary>
/// Песочница с корнем в рабочей папке: наружу выйти нельзя.
/// </summary>
public sealed class WorkspaceSandbox : IScriptSandbox
{
    private readonly string _root;
    private readonly bool _readOnly;

    /// <summary>Создаёт песочницу.</summary>
    /// <param name="root">Корневая папка; создаётся, если её нет.</param>
    /// <param name="readOnly">Запретить запись.</param>
    public WorkspaceSandbox(string root, bool readOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _readOnly = readOnly;

        _ = Directory.CreateDirectory(_root);
    }

    /// <inheritdoc/>
    public bool Enabled => true;

    /// <inheritdoc/>
    public string Root => _root;

    /// <summary>Только чтение.</summary>
    public bool IsReadOnly => _readOnly;

    /// <inheritdoc/>
    public string Resolve(string path, bool forWriting)
    {
        if (forWriting && _readOnly)
        {
            throw new ScriptError(
                DiagnosticCodes.SandboxDenied,
                $"запись запрещена настройками прогона: '{path}'",
                "прогон открыт только на чтение");
        }

        if (string.IsNullOrWhiteSpace(path))
            throw new ScriptError(DiagnosticCodes.SandboxDenied, "пустой путь");

        if (Path.IsPathRooted(path))
        {
            throw new ScriptError(
                DiagnosticCodes.SandboxDenied,
                $"абсолютные пути запрещены: '{path}'",
                $"пути отсчитываются от рабочей папки прогона ({_root})");
        }

        string full = Path.GetFullPath(Path.Combine(_root, path));

        // Проверяется КАНОНИЗИРОВАННЫЙ путь, а не исходная строка: '..' и симлинки убирает
        // именно канонизация, а поиск подстроки '..' в тексте обходится тривиально.
        if (!IsInside(full))
        {
            throw new ScriptError(
                DiagnosticCodes.SandboxDenied,
                $"путь выходит за рабочую папку: '{path}'",
                $"разрешено только внутри {_root}");
        }

        if (forWriting)
        {
            string? directory = Path.GetDirectoryName(full);
            if (directory != null) _ = Directory.CreateDirectory(directory);
        }

        return full;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> List(string directory, string mask)
    {
        string full = Resolve(string.IsNullOrWhiteSpace(directory) ? "." : directory, forWriting: false);

        if (!Directory.Exists(full)) return [];

        var names = new List<string>();

        foreach (string file in Directory.EnumerateFiles(full, string.IsNullOrWhiteSpace(mask) ? "*" : mask))
        {
            if (!IsInside(file)) continue;

            names.Add(Path.GetRelativePath(_root, file).Replace('\\', '/'));
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private bool IsInside(string fullPath)
    {
        string normalized = Path.TrimEndingDirectorySeparator(fullPath);

        if (string.Equals(normalized, _root, PathComparison)) return true;

        return normalized.StartsWith(_root + Path.DirectorySeparatorChar, PathComparison);
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
