using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Collections.Concurrent;
using System.Text;

namespace AI.Script.Hosting;

/// <summary>
/// Хранилище в памяти процесса.
/// </summary>
/// <remarks>
/// Нужно двоим. Хосту, у которого файлы пользователя уже лежат байтами (вложение чата, ответ
/// службы), и незачем писать их на диск ради одного прогона. И тестам: функции модулей обязаны
/// работать над любым хранилищем, и проверять это надо не только папкой на диске.
/// <para>
/// Запрет записи относится к скрипту, а не к хосту: <see cref="Put(string, byte[])"/>
/// наполняет хранилище и в режиме «только чтение».
/// </para>
/// </remarks>
public sealed class MemorySandbox : IScriptSandbox
{
    private readonly ConcurrentDictionary<string, Entry> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _readOnly;

    /// <summary>Создаёт пустое хранилище.</summary>
    /// <param name="readOnly">Запретить скрипту запись.</param>
    public MemorySandbox(bool readOnly = false) => _readOnly = readOnly;

    /// <inheritdoc/>
    public bool Enabled => true;

    /// <inheritdoc/>
    public string Root => "память";

    /// <inheritdoc/>
    public bool IsReadOnly => _readOnly;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ScriptFileInfo>> ListAsync(string directory, string mask, CancellationToken cancellationToken = default)
    {
        string folder = ScriptPaths.Normalize(string.IsNullOrWhiteSpace(directory) ? ScriptPaths.RootPath : directory);
        string prefix = folder == ScriptPaths.RootPath ? string.Empty : folder + "/";
        var files = new List<ScriptFileInfo>();

        foreach (var pair in _files)
        {
            if (!pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            string name = pair.Key[prefix.Length..];

            if (name.Contains('/', StringComparison.Ordinal) || !ScriptPaths.Matches(name, mask)) continue;

            files.Add(Describe(pair.Key, pair.Value));
        }

        files.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));

        return Task.FromResult<IReadOnlyList<ScriptFileInfo>>(files);
    }

    /// <inheritdoc/>
    public Task<ScriptFileInfo?> InfoAsync(string path, CancellationToken cancellationToken = default)
    {
        string key = ScriptPaths.Normalize(path);

        return Task.FromResult<ScriptFileInfo?>(_files.TryGetValue(key, out Entry? entry) ? Describe(key, entry) : null);
    }

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        _files.TryGetValue(ScriptPaths.Normalize(path), out Entry? entry)
            ? Task.FromResult(entry.Data)
            : throw new ScriptError(DiagnosticCodes.FileNotFound, $"файл не найден: '{path}'");

    /// <inheritdoc/>
    public Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        if (_readOnly)
        {
            throw new ScriptError(
                DiagnosticCodes.SandboxDenied,
                $"запись запрещена настройками прогона: '{path}'",
                "прогон открыт только на чтение");
        }

        _ = Put(path, data);

        return Task.CompletedTask;
    }

    /// <summary>Кладёт файл; повторный путь заменяет содержимое.</summary>
    public MemorySandbox Put(string path, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        _files[ScriptPaths.Normalize(path)] = new Entry(data, DateTimeOffset.UtcNow);

        return this;
    }

    /// <summary>Кладёт текстовый файл в UTF-8.</summary>
    public MemorySandbox Put(string path, string text) => Put(path, new UTF8Encoding(false).GetBytes(text ?? string.Empty));

    /// <summary>Содержимое файла; <c>null</c>, если его нет.</summary>
    public byte[]? Get(string path) => _files.TryGetValue(ScriptPaths.Normalize(path), out Entry? entry) ? entry.Data : null;

    private static ScriptFileInfo Describe(string path, Entry entry) => new(path, entry.Data.LongLength, entry.Modified);

    private sealed record Entry(byte[] Data, DateTimeOffset Modified);
}
