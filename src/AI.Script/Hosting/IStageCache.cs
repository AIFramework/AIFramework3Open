using AI.Script.Runtime;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AI.Script.Hosting;

/// <summary>
/// Хранилище результатов стадий между вызовами и между прогонами.
/// </summary>
/// <remarks>
/// Ключ строится из текста стадии, версий модулей и значений аргументов: изменил тело стадии
/// или входные данные — получил другой ключ и честный пересчёт. Инвалидации «по времени» нет
/// намеренно: устаревающий по часам кэш означал бы, что один и тот же скрипт на одних и тех же
/// данных даёт разный ответ в зависимости от того, когда его запустили.
/// </remarks>
public interface IStageCache
{
    /// <summary>Ищет готовый результат.</summary>
    /// <param name="key">Ключ стадии.</param>
    /// <param name="value">Найденное значение.</param>
    /// <returns><c>true</c>, если результат найден.</returns>
    bool TryGet(string key, out ScriptValue value);

    /// <summary>Кладёт результат.</summary>
    void Put(string key, ScriptValue value);
}

/// <summary>Кэш, который ничего не хранит: режим <c>options.cache: "off"</c>.</summary>
public sealed class DisabledStageCache : IStageCache
{
    /// <summary>Единственный экземпляр.</summary>
    public static readonly DisabledStageCache Instance = new();

    private DisabledStageCache()
    {
    }

    /// <inheritdoc/>
    public bool TryGet(string key, out ScriptValue value)
    {
        value = ScriptValue.None;

        return false;
    }

    /// <inheritdoc/>
    public void Put(string key, ScriptValue value)
    {
    }
}

/// <summary>
/// Кэш в памяти процесса.
/// </summary>
/// <remarks>
/// Живёт столько же, сколько объект кэша: положив его в поле хоста, вызывающий получает кэш
/// между прогонами; создав новый на каждый прогон — только внутри прогона. Решает это тот, кто
/// запускает, а не язык.
/// </remarks>
public sealed class MemoryStageCache : IStageCache
{
    private readonly ConcurrentDictionary<string, ScriptValue> _entries = new(StringComparer.Ordinal);

    /// <summary>Сколько записей лежит в кэше.</summary>
    public int Count => _entries.Count;

    /// <inheritdoc/>
    public bool TryGet(string key, out ScriptValue value) => _entries.TryGetValue(key, out value);

    /// <inheritdoc/>
    public void Put(string key, ScriptValue value) => _entries[key] = value;

    /// <summary>Очищает кэш.</summary>
    public void Clear() => _entries.Clear();
}

/// <summary>
/// Кэш в папке на диске: переживает перезапуск процесса.
/// </summary>
/// <remarks>
/// Один файл на запись, имя файла — ключ: так удаление кэша сводится к удалению папки, а
/// испорченная запись портит одну стадию, а не всё хранилище.
/// <para>
/// Запись идёт через временный файл с последующим переименованием: прерванный прогон не должен
/// оставлять после себя полуфайл, который следующий прогон примет за готовый результат.
/// </para>
/// </remarks>
public sealed class FileStageCache : IStageCache
{
    private readonly string _directory;
    private readonly MemoryStageCache _hot = new();

    /// <summary>Создаёт кэш в указанной папке; папка создаётся при первой записи.</summary>
    public FileStageCache(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _directory = Path.GetFullPath(directory);
    }

    /// <summary>Папка хранилища.</summary>
    public string Directory => _directory;

    /// <inheritdoc/>
    public bool TryGet(string key, out ScriptValue value)
    {
        // Сначала горячая копия: повторный вызов той же стадии в одном прогоне не должен
        // ходить на диск.
        if (_hot.TryGet(key, out value)) return true;

        string path = PathOf(key);

        if (!File.Exists(path)) return false;

        try
        {
            using FileStream stream = File.OpenRead(path);

            if (!ScriptValueCodec.TryRead(stream, out value)) return false;
        }
#pragma warning disable CA1031 // Недоступный файл кэша — не отказ прогона, а промах кэша.
        catch (Exception)
#pragma warning restore CA1031
        {
            value = ScriptValue.None;

            return false;
        }

        _hot.Put(key, value);

        return true;
    }

    /// <inheritdoc/>
    public void Put(string key, ScriptValue value)
    {
        _hot.Put(key, value);

        if (!ScriptValueCodec.CanWrite(value)) return;

        try
        {
            _ = System.IO.Directory.CreateDirectory(_directory);

            string path = PathOf(key);
            string temporary = path + ".tmp";

            using (FileStream stream = File.Create(temporary)) ScriptValueCodec.Write(stream, value);

            File.Move(temporary, path, overwrite: true);
        }
#pragma warning disable CA1031 // Кэш — ускорение, а не хранилище: неудачная запись молча пропускается.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private string PathOf(string key) => Path.Combine(_directory, Safe(key) + ".aisc");

    /// <summary>
    /// Приводит ключ к безопасному имени файла.
    /// </summary>
    /// <remarks>
    /// Ключи строит <see cref="ValueDigest"/> и они уже шестнадцатеричные, но кэш — публичный
    /// тип: чужой ключ с <c>../</c> внутри не должен превращаться в запись мимо папки.
    /// </remarks>
    private static string Safe(string key)
    {
        foreach (char c in key)
        {
            if (char.IsAsciiLetterOrDigit(c)) continue;

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32].ToLowerInvariant();
        }

        return key.Length <= 64 ? key : key[..64];
    }
}
