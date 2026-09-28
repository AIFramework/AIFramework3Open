namespace AiFramework.Tools.Atlas;

/// <summary>
/// Формы тел (<see cref="CodeShapes"/>) по отпечатку единицы. Разбор Roslyn двенадцати тысяч
/// тел занимает секунды, а хуку после правки нужен ответ сразу: готовые формы читаются с
/// диска за доли секунды, заново разбираются только изменившиеся тела.
/// </summary>
/// <remarks>Файл — кэш в <c>%LOCALAPPDATA%\Atlas</c>; при смене правил нормализации поднять <see cref="Version"/>.</remarks>
public sealed class ShapeCache
{
    /// <summary>Версия правил нормализации тел.</summary>
    public const int Version = 1;

    private const string Magic = "AISC";

    private readonly string _path;
    private readonly Dictionary<string, CodeShape?> _shapes;
    private bool _dirty;

    private ShapeCache(string path, Dictionary<string, CodeShape?> shapes)
    {
        _path = path;
        _shapes = shapes;
    }

    /// <summary>Сколько форм в кэше.</summary>
    public int Count => _shapes.Count;

    /// <summary>Открывает кэш; чужая версия или битый файл — пустой кэш.</summary>
    public static ShapeCache Open(string path)
    {
        var shapes = new Dictionary<string, CodeShape?>(StringComparer.Ordinal);

        try
        {
            if (File.Exists(path))
            {
                using var reader = new BinaryReader(File.OpenRead(path));
                if (reader.ReadString() == Magic && reader.ReadInt32() == Version)
                {
                    for (int i = reader.ReadInt32(); i > 0; i--) shapes[reader.ReadString()] = Read(reader);
                }
            }
        }
        catch (Exception error) when (error is EndOfStreamException or IOException)
        {
            shapes.Clear();
        }

        return new ShapeCache(path, shapes);
    }

    /// <summary>Форма тела единицы: из кэша или разбором.</summary>
    public CodeShape? Of(CodeUnit unit)
    {
        if (_shapes.TryGetValue(unit.Hash, out CodeShape? shape)) return shape;

        _shapes[unit.Hash] = shape = CodeShapes.Of(unit.Body);
        _dirty = true;
        return shape;
    }

    /// <summary>Записывает кэш, если в нём появились новые формы.</summary>
    public void Save()
    {
        if (!_dirty) return;

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporary = _path + ".tmp";

        using (var writer = new BinaryWriter(File.Create(temporary)))
        {
            writer.Write(Magic);
            writer.Write(Version);
            writer.Write(_shapes.Count);

            foreach ((string hash, CodeShape? shape) in _shapes)
            {
                writer.Write(hash);
                Write(writer, shape);
            }
        }

        File.Move(temporary, _path, overwrite: true);
        _dirty = false;
    }

    private static CodeShape? Read(BinaryReader reader)
    {
        if (!reader.ReadBoolean()) return null;

        ulong[] signature = new ulong[reader.ReadInt32()];
        for (int i = 0; i < signature.Length; i++) signature[i] = reader.ReadUInt64();

        var shingles = new HashSet<ulong>();
        for (int i = reader.ReadInt32(); i > 0; i--) shingles.Add(reader.ReadUInt64());

        var constants = new HashSet<string>(StringComparer.Ordinal);
        for (int i = reader.ReadInt32(); i > 0; i--) constants.Add(reader.ReadString());

        int[] control = [reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()];
        return new CodeShape(signature, shingles, constants, control, reader.ReadInt32(), reader.ReadInt32());
    }

    private static void Write(BinaryWriter writer, CodeShape? shape)
    {
        writer.Write(shape != null);
        if (shape is null) return;

        writer.Write(shape.Signature.Length);
        foreach (ulong value in shape.Signature) writer.Write(value);

        writer.Write(shape.Shingles.Count);
        foreach (ulong value in shape.Shingles) writer.Write(value);

        writer.Write(shape.Constants.Count);
        foreach (string value in shape.Constants) writer.Write(value);

        foreach (int value in shape.Control) writer.Write(value);
        writer.Write(shape.Tokens);
        writer.Write(shape.Strings);
    }
}
