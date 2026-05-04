using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace AI.DataStructs;

/// <summary>
/// Безопасный сериализатор — замена <see cref="BinarySerializer"/>.
/// Формат файла: [8 байт magic "AIFW_V1\0"] + [32 байт SHA-256] + [payload].
/// Для алгебраических типов (Vector/Matrix/Tensor и др.) payload — бинарный
/// формат InMemoryDataStream. Для ML-моделей — UTF-8 JSON.
/// При загрузке SHA-256 верифицируется до десериализации.
/// </summary>
public static class SafeSerializer
{
    private static readonly byte[] Magic = "AIFW_V1\0"u8.ToArray();

    // ------------------------------------------------------------------
    // Bytes API — для типов с GetBytes() / FromBytes()
    // ------------------------------------------------------------------

    public static void SaveBytes(string path, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(path);
        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        WriteFrame(fs, payload);
    }

    public static void SaveBytes(Stream stream, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(stream);
        WriteFrame(stream, payload);
    }

    public static byte[] LoadBytes(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Файл модели не найден.", path);

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadFrame(fs, path);
    }

    public static byte[] LoadBytes(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return ReadFrame(stream, null);
    }

    // ------------------------------------------------------------------
    // JSON API — для ML-моделей
    // ------------------------------------------------------------------

    public static void Save<T>(string path, T obj, JsonSerializerOptions? options = null)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(obj, options);
        SaveBytes(path, json);
    }

    public static void Save<T>(Stream stream, T obj, JsonSerializerOptions? options = null)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(obj, options);
        SaveBytes(stream, json);
    }

    public static T Load<T>(string path, JsonSerializerOptions? options = null)
    {
        byte[] json = LoadBytes(path);
        return JsonSerializer.Deserialize<T>(json, options)
            ?? throw new InvalidDataException($"Десериализация вернула null для типа {typeof(T).Name}.");
    }

    public static T Load<T>(Stream stream, JsonSerializerOptions? options = null)
    {
        byte[] json = LoadBytes(stream);
        return JsonSerializer.Deserialize<T>(json, options)
            ?? throw new InvalidDataException($"Десериализация вернула null для типа {typeof(T).Name}.");
    }

    // ------------------------------------------------------------------
    // Внутренние методы
    // ------------------------------------------------------------------

    private static void WriteFrame(Stream stream, byte[] payload)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(payload, hash);
        stream.Write(Magic);
        stream.Write(hash);
        stream.Write(payload);
    }

    private static byte[] ReadFrame(Stream stream, string? path)
    {
        // Читаем всё в буфер, чтобы не зависеть от поддержки Length/Position
        byte[] all;
        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            all = ms.ToArray();
        }

        if (all.Length < 40)
            throw new InvalidDataException(Error(path, "файл слишком мал для формата AIFW_V1."));

        if (!all.AsSpan(0, 8).SequenceEqual(Magic))
            throw new InvalidDataException(Error(path,
                "неверный заголовок. Файл не является моделью AIFW_V1 " +
                "или был сохранён устаревшим BinaryFormatter и несовместим с текущей версией."));

        ReadOnlySpan<byte> storedHash = all.AsSpan(8, 32);
        byte[] payload = all[40..];

        Span<byte> computedHash = stackalloc byte[32];
        SHA256.HashData(payload, computedHash);

        if (!CryptographicOperations.FixedTimeEquals(storedHash, computedHash))
            throw new InvalidDataException(Error(path,
                "SHA-256 хеш не совпадает — файл повреждён или изменён."));

        return payload;
    }

    private static string Error(string? path, string detail) =>
        path is null
            ? $"Ошибка чтения модели: {detail}"
            : $"Ошибка чтения модели '{path}': {detail}";
}
