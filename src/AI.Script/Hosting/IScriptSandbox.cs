using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Hosting;

/// <summary>
/// Хранилище файлов прогона.
/// </summary>
/// <remarks>
/// Исполнение чужого кода — штатный сценарий языка, а не исключение: скрипт пишет модель.
/// Поэтому модуль не открывает файл сам, а просит у хранилища байты по пути, и открыть то, о
/// чём хранилище не знает, не может в принципе.
/// <para>
/// Хранилище отдаёт байты, а не путь на диске: файлы бывают в папке, в памяти, в объектном
/// хранилище хоста, и модулю незачем знать, где именно. Путь из скрипта всегда относительный
/// и приводится к виду <c>папка/файл</c> через <see cref="ScriptPaths.Normalize"/>: выход
/// наружу отсекается там, одинаково для всех реализаций.
/// </para>
/// </remarks>
public interface IScriptSandbox
{
    /// <summary>Разрешена ли работа с файлами вообще.</summary>
    bool Enabled { get; }

    /// <summary>Как назвать корень в сообщениях: папка, «память», «файлы сессии».</summary>
    string Root { get; }

    /// <summary>Запрещена ли запись.</summary>
    bool IsReadOnly { get; }

    /// <summary>Файлы папки по маске, без вложенных папок; порядок — по пути.</summary>
    /// <param name="directory">Папка относительно корня; <c>.</c> — сам корень.</param>
    /// <param name="mask">Маска имени, например <c>*.csv</c>.</param>
    /// <param name="cancellationToken">Отмена.</param>
    Task<IReadOnlyList<ScriptFileInfo>> ListAsync(string directory, string mask, CancellationToken cancellationToken = default);

    /// <summary>Сведения о файле; <c>null</c>, если его нет.</summary>
    Task<ScriptFileInfo?> InfoAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Содержимое файла; отказ <see cref="DiagnosticCodes.FileNotFound"/>, если его нет.</summary>
    Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Открывает файл на чтение потоком.
    /// </summary>
    /// <remarks>
    /// Для файлов, которые целиком в память не ложатся: выгрузка на миллионы строк читается
    /// по строке. По умолчанию поток строится поверх <see cref="ReadAsync"/>, то есть файл все
    /// равно читается целиком; хранилище, умеющее отдавать поток, переопределяет метод.
    /// </remarks>
    async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) =>
        new MemoryStream(await ReadAsync(path, cancellationToken).ConfigureAwait(false), writable: false);

    /// <summary>Записывает файл, создавая папки по пути; отказ, если запись запрещена.</summary>
    Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default);
}

/// <summary>Сведения о файле хранилища.</summary>
/// <param name="Path">Путь относительно корня, через <c>/</c>.</param>
/// <param name="Size">Размер в байтах.</param>
/// <param name="Modified">Время изменения, если хранилище его знает.</param>
/// <param name="StoredMediaType">Медиатип от хранилища; <c>null</c> — вывести из расширения.</param>
public sealed record ScriptFileInfo(string Path, long Size, DateTimeOffset? Modified = null, string? StoredMediaType = null)
{
    /// <summary>Имя файла без папки.</summary>
    public string Name => Path[(Path.LastIndexOf('/') + 1)..];

    /// <summary>Вид файла: <c>table</c>, <c>text</c>, <c>image</c>… — см. <see cref="ScriptFileKinds"/>.</summary>
    public string Kind => ScriptFileKinds.KindOf(Path);

    /// <summary>Медиатип.</summary>
    public string MediaType => StoredMediaType ?? ScriptFileKinds.MediaTypeOf(Path);
}

/// <summary>
/// Хранилище, запрещающее любую работу с файлами.
/// </summary>
/// <remarks>
/// Значение по умолчанию: хост, которому файлы не нужны, не должен их случайно разрешить,
/// забыв настроить хранилище.
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
    public bool IsReadOnly => true;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ScriptFileInfo>> ListAsync(string directory, string mask, CancellationToken cancellationToken = default) =>
        throw Denied();

    /// <inheritdoc/>
    public Task<ScriptFileInfo?> InfoAsync(string path, CancellationToken cancellationToken = default) => throw Denied();

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default) => throw Denied();

    /// <inheritdoc/>
    public Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default) => throw Denied();

    private static ScriptError Denied() =>
        new(DiagnosticCodes.SandboxDenied,
            "работа с файлами запрещена настройками прогона",
            "хост должен задать хранилище: RunOptions.Sandbox = new WorkspaceSandbox(путь) либо new MemorySandbox()");
}
