using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Hosting;

/// <summary>
/// Хранилище с корнем в папке на диске: наружу выйти нельзя.
/// </summary>
public sealed class WorkspaceSandbox : IScriptSandbox
{
    private readonly string _root;
    private readonly bool _readOnly;

    /// <summary>Создаёт хранилище.</summary>
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

    /// <inheritdoc/>
    public bool IsReadOnly => _readOnly;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ScriptFileInfo>> ListAsync(string directory, string mask, CancellationToken cancellationToken = default)
    {
        string full = Resolve(string.IsNullOrWhiteSpace(directory) ? ScriptPaths.RootPath : directory, forWriting: false);

        if (!Directory.Exists(full)) return Task.FromResult<IReadOnlyList<ScriptFileInfo>>([]);

        var files = new List<ScriptFileInfo>();

        foreach (string file in Directory.EnumerateFiles(full))
        {
            if (!IsInside(file) || !ScriptPaths.Matches(Path.GetFileName(file), mask)) continue;

            files.Add(Describe(file));
        }

        files.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));

        return Task.FromResult<IReadOnlyList<ScriptFileInfo>>(files);
    }

    /// <inheritdoc/>
    public Task<ScriptFileInfo?> InfoAsync(string path, CancellationToken cancellationToken = default)
    {
        string full = Resolve(path, forWriting: false);

        return Task.FromResult<ScriptFileInfo?>(File.Exists(full) ? Describe(full) : null);
    }

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        string full = Resolve(path, forWriting: false);

        if (!File.Exists(full)) throw new ScriptError(DiagnosticCodes.FileNotFound, $"файл не найден: '{path}'");

        return File.ReadAllBytesAsync(full, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        string full = Resolve(path, forWriting: false);

        if (!File.Exists(full)) throw new ScriptError(DiagnosticCodes.FileNotFound, $"файл не найден: '{path}'");

        return Task.FromResult<Stream>(new FileStream(
            full, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1 << 16, useAsync: true));
    }

    /// <inheritdoc/>
    public Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default) =>
        File.WriteAllBytesAsync(Resolve(path, forWriting: true), data, cancellationToken);

    /// <summary>
    /// Переводит путь из скрипта в абсолютный путь внутри папки.
    /// </summary>
    /// <remarks>
    /// Сначала общая нормализация (<see cref="ScriptPaths.Normalize"/>), потом проверка уже
    /// КАНОНИЗИРОВАННОГО пути: <c>..</c> убирает нормализация, а ссылку наружу — только сверка
    /// итогового полного пути с корнем.
    /// </remarks>
    public string Resolve(string path, bool forWriting)
    {
        if (forWriting && _readOnly)
        {
            throw new ScriptError(
                DiagnosticCodes.SandboxDenied,
                $"запись запрещена настройками прогона: '{path}'",
                "прогон открыт только на чтение");
        }

        string full = Path.GetFullPath(Path.Combine(_root, ScriptPaths.Normalize(path)));

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

    private ScriptFileInfo Describe(string full)
    {
        var info = new FileInfo(full);

        return new ScriptFileInfo(
            Path.GetRelativePath(_root, full).Replace('\\', '/'),
            info.Length,
            new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero));
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
