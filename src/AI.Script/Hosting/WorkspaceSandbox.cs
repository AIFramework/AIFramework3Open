using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Hosting;

/// <summary>
/// Хранилище с корнем в папке на диске: наружу выйти нельзя.
/// </summary>
/// <remarks>
/// Все обращения к диску идут через <see cref="Resolve"/>: он сверяет реальное расположение
/// цели, а не строку пути. Чтение и запись дополнительно сверяют путь уже открытого файла.
/// </remarks>
public sealed class WorkspaceSandbox : IScriptSandbox
{
    private readonly string _root;
    private readonly string _realRoot;
    private readonly bool _readOnly;

    /// <summary>Создаёт хранилище.</summary>
    /// <param name="root">Корневая папка; создаётся, если её нет.</param>
    /// <param name="readOnly">Запретить запись.</param>
    public WorkspaceSandbox(string root, bool readOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        // Приставка \\?\ снимается: с ней система не приводит «/» к «\», а пути скрипта пишутся и так, и так.
        _root = ScriptRealPath.Plain(Path.GetFullPath(root));
        _readOnly = readOnly;

        _ = Directory.CreateDirectory(_root);

        // Корень сам может стоять за ссылкой (подставной диск, /tmp на macOS): сверка идёт
        // с его реальным расположением, а не со строкой.
        _realRoot = ScriptRealPath.Of(_root);
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

        foreach (FileInfo file in new DirectoryInfo(full).EnumerateFiles())
        {
            if (!ScriptPaths.Matches(file.Name, mask)) continue;

            // Файл-ссылка показывается, только если ведёт внутрь: то, что нельзя прочитать,
            // в списке не появляется.
            FileInfo? content = (file.Attributes & FileAttributes.ReparsePoint) == 0 ? file : TargetInside(file.FullName);

            if (content != null) files.Add(Describe(file.FullName, content));
        }

        files.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));

        return Task.FromResult<IReadOnlyList<ScriptFileInfo>>(files);
    }

    /// <inheritdoc/>
    public Task<ScriptFileInfo?> InfoAsync(string path, CancellationToken cancellationToken = default)
    {
        string full = Resolve(path, forWriting: false);

        return Task.FromResult<ScriptFileInfo?>(File.Exists(full) ? Describe(full, new FileInfo(ScriptRealPath.Of(full))) : null);
    }

    /// <inheritdoc/>
    public async Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = OpenInside(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        byte[] data = new byte[stream.Length];

        await stream.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);

        return data;
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(OpenInside(path, FileMode.Open, FileAccess.Read, FileShare.Read));

    /// <inheritdoc/>
    public async Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        // Открывается без усечения: содержимое стирается только после сверки дескриптора, то
        // есть когда уже известно, что открыт файл внутри рабочей папки.
        await using FileStream stream = OpenInside(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

        stream.SetLength(0);

        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Переводит путь из скрипта в абсолютный путь внутри папки.
    /// </summary>
    /// <remarks>
    /// Единственная дверь на диск. Сначала общая нормализация (<see cref="ScriptPaths.Normalize"/>)
    /// убирает <c>..</c> и абсолютные пути, потом сверяется РЕАЛЬНОЕ расположение цели
    /// (<see cref="ScriptRealPath.Of"/>): символическая ссылка или точка соединения внутри
    /// рабочей папки, ведущая наружу, даёт отказ. Для нового файла сверяется реальное
    /// расположение ближайшего существующего предка — до того, как создаются папки.
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

        if (!ScriptRealPath.IsInside(full, _root))
        {
            // Сюда попадают имена устройств Windows: «NUL» и «con.txt» система разворачивает
            // в путь устройства, а не в файл папки.
            throw new ScriptError(
                DiagnosticCodes.SandboxDenied,
                $"'{path}' — не файл рабочей папки, а имя устройства системы",
                "выберите другое имя файла");
        }

        RequireInside(full, path);

        if (forWriting)
        {
            string? directory = Path.GetDirectoryName(full);

            if (directory != null)
            {
                _ = Directory.CreateDirectory(directory);

                // Папки созданы — их реальное расположение сверяется ещё раз.
                RequireInside(directory, path);
            }
        }

        return full;
    }

    /// <summary>Открывает файл и сверяет путь открытого дескриптора с рабочей папкой.</summary>
    private FileStream OpenInside(string path, FileMode mode, FileAccess access, FileShare share)
    {
        string full = Resolve(path, forWriting: access != FileAccess.Read);

        if (mode == FileMode.Open && !File.Exists(full)) throw new ScriptError(DiagnosticCodes.FileNotFound, $"файл не найден: '{path}'");

        var stream = new FileStream(full, mode, access, share, bufferSize: 1 << 16, useAsync: true);

        try
        {
            string? opened = ScriptRealPath.OfHandle(stream.SafeFileHandle);

            if (opened != null && !ScriptRealPath.IsInside(opened, _realRoot)) throw Outside(path);
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        return stream;
    }

    private void RequireInside(string full, string path)
    {
        if (!ScriptRealPath.IsInside(ScriptRealPath.Of(full), _realRoot)) throw Outside(path);
    }

    /// <summary>Цель файла-ссылки, если она внутри рабочей папки; иначе <c>null</c>.</summary>
    private FileInfo? TargetInside(string full)
    {
        try
        {
            string real = ScriptRealPath.Of(full);

            return ScriptRealPath.IsInside(real, _realRoot) ? new FileInfo(real) : null;
        }
        catch (ScriptError)
        {
            return null;
        }
    }

    private ScriptError Outside(string path) =>
        new(DiagnosticCodes.SandboxDenied,
            $"путь выходит за рабочую папку: '{path}' проходит через ссылку (symlink или junction), которая ведёт наружу",
            $"разрешено только то, что на самом деле лежит внутри {_root}");

    private ScriptFileInfo Describe(string full, FileInfo content) =>
        new(Path.GetRelativePath(_root, full).Replace('\\', '/'),
            content.Length,
            new DateTimeOffset(content.LastWriteTimeUtc, TimeSpan.Zero));
}
