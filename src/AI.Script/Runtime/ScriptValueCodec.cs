using AI.DataStructs.Algebraic;
using System.Text;

namespace AI.Script.Runtime;

/// <summary>
/// Двоичная запись и чтение значений языка — для кэша стадий на диске.
/// </summary>
/// <remarks>
/// Собственный формат, а не JSON: таблица на сто тысяч строк в JSON занимает втрое больше и
/// читается на порядок дольше, а кэш, который дороже пересчёта, бессмыслен.
/// <para>
/// Формат сознательно не претендует на переносимость между версиями: в ключе кэша участвуют
/// текст стадии и версии модулей, поэтому старая запись просто никогда не будет запрошена.
/// Заголовок с версией формата всё же есть — чтобы чтение чужого файла отказывало явно, а не
/// разбирало мусор.
/// </para>
/// </remarks>
public static class ScriptValueCodec
{
    /// <summary>Версия формата; при несовпадении запись считается непригодной.</summary>
    public const int Version = 1;

    private const int Magic = 0x53414953;

    /// <summary>
    /// Можно ли записать значение.
    /// </summary>
    /// <remarks>
    /// Дескрипторы и функции не записываются: обученная модель — это живой объект, а не
    /// данные. Стадия, возвращающая дескриптор, кэшируется только в памяти прогона.
    /// </remarks>
    public static bool CanWrite(ScriptValue value) => value.Type switch
    {
        ScriptType.Fn or ScriptType.Handle or ScriptType.CVec or ScriptType.Tensor => false,
        ScriptType.List => AllWritable(value.AsList()),
        ScriptType.Record => AllWritable(value.AsRecord()),
        ScriptType.Table => AllWritable(value.AsTable()),
        _ => true,
    };

    /// <summary>Записывает значение в поток.</summary>
    public static void Write(Stream stream, ScriptValue value)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(Magic);
        writer.Write(Version);

        WriteValue(writer, value);
    }

    /// <summary>
    /// Читает значение из потока.
    /// </summary>
    /// <param name="stream">Поток.</param>
    /// <param name="value">Прочитанное значение.</param>
    /// <returns><c>false</c>, если поток не является записью пригодной версии.</returns>
    public static bool TryRead(Stream stream, out ScriptValue value)
    {
        ArgumentNullException.ThrowIfNull(stream);

        value = ScriptValue.None;

        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) return false;

            value = ReadValue(reader);

            return true;
        }
#pragma warning disable CA1031 // Испорченный файл кэша — не повод ронять прогон: он просто не кэш.
        catch (Exception)
#pragma warning restore CA1031
        {
            value = ScriptValue.None;

            return false;
        }
    }

    private static bool AllWritable(ScriptList list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (!CanWrite(list[i])) return false;
        }

        return true;
    }

    private static bool AllWritable(ScriptRecord record)
    {
        for (int i = 0; i < record.Count; i++)
        {
            if (!CanWrite(record.Values[i])) return false;
        }

        return true;
    }

    private static bool AllWritable(ScriptTable table)
    {
        foreach (ScriptColumn column in table.Columns)
        {
            for (int i = 0; i < column.Count; i++)
            {
                if (!CanWrite(column[i])) return false;
            }
        }

        return true;
    }

    private static void WriteValue(BinaryWriter writer, ScriptValue value)
    {
        writer.Write((byte)value.Type);

        switch (value.Type)
        {
            case ScriptType.None:
                break;

            case ScriptType.Num:
                writer.Write(value.RawNumber);
                break;

            case ScriptType.Bool:
                writer.Write(value.RawNumber != 0);
                break;

            case ScriptType.Str:
                writer.Write(value.AsString());
                break;

            case ScriptType.Date:
                writer.Write(value.AsDate().Ticks);
                break;

            case ScriptType.Dur:
                writer.Write(value.AsDuration().Ticks);
                break;

            case ScriptType.Range:
                {
                    ScriptRange range = value.AsRange();

                    writer.Write(range.Start);
                    writer.Write(range.End);
                    writer.Write(range.Step);
                    break;
                }

            case ScriptType.Vec:
                {
                    Vector vector = value.AsVector();

                    writer.Write(vector.Count);
                    for (int i = 0; i < vector.Count; i++) writer.Write(vector[i]);

                    break;
                }

            case ScriptType.Mat:
                {
                    Matrix matrix = value.AsMatrix();

                    writer.Write(matrix.Height);
                    writer.Write(matrix.Width);

                    for (int i = 0; i < matrix.Height; i++)
                    {
                        for (int j = 0; j < matrix.Width; j++) writer.Write(matrix[i, j]);
                    }

                    break;
                }

            case ScriptType.List:
                {
                    ScriptList list = value.AsList();

                    writer.Write(list.Count);
                    for (int i = 0; i < list.Count; i++) WriteValue(writer, list[i]);

                    break;
                }

            case ScriptType.Record:
                {
                    ScriptRecord record = value.AsRecord();

                    writer.Write(record.Count);

                    for (int i = 0; i < record.Count; i++)
                    {
                        writer.Write(record.Keys[i]);
                        WriteValue(writer, record.Values[i]);
                    }

                    break;
                }

            case ScriptType.Table:
                {
                    ScriptTable table = value.AsTable();

                    writer.Write(table.ColumnCount);
                    writer.Write(table.RowCount);

                    foreach (ScriptColumn column in table.Columns)
                    {
                        writer.Write(column.Name);
                        for (int i = 0; i < column.Count; i++) WriteValue(writer, column[i]);
                    }

                    break;
                }

            default:
                throw new NotSupportedException($"значение типа {value.Type.ToName()} не записывается в кэш");
        }
    }

    private static ScriptValue ReadValue(BinaryReader reader)
    {
        var type = (ScriptType)reader.ReadByte();

        switch (type)
        {
            case ScriptType.None:
                return ScriptValue.None;

            case ScriptType.Num:
                return ScriptValue.Num(reader.ReadDouble());

            case ScriptType.Bool:
                return ScriptValue.Bool(reader.ReadBoolean());

            case ScriptType.Str:
                return ScriptValue.Str(reader.ReadString());

            case ScriptType.Date:
                return ScriptValue.Date(new DateTime(reader.ReadInt64()));

            case ScriptType.Dur:
                return ScriptValue.Dur(new TimeSpan(reader.ReadInt64()));

            case ScriptType.Range:
                {
                    double start = reader.ReadDouble();
                    double end = reader.ReadDouble();
                    double step = reader.ReadDouble();

                    return ScriptValue.Range(new ScriptRange(start, end, step));
                }

            case ScriptType.Vec:
                {
                    int count = reader.ReadInt32();
                    var vector = new Vector(count);

                    for (int i = 0; i < count; i++) vector[i] = reader.ReadDouble();

                    return ScriptValue.Vec(vector);
                }

            case ScriptType.Mat:
                {
                    int height = reader.ReadInt32();
                    int width = reader.ReadInt32();
                    var matrix = new Matrix(height, width);

                    for (int i = 0; i < height; i++)
                    {
                        for (int j = 0; j < width; j++) matrix[i, j] = reader.ReadDouble();
                    }

                    return ScriptValue.Mat(matrix);
                }

            case ScriptType.List:
                {
                    int count = reader.ReadInt32();
                    var items = new ScriptValue[count];

                    for (int i = 0; i < count; i++) items[i] = ReadValue(reader);

                    return ScriptValue.List(ScriptList.Own(items));
                }

            case ScriptType.Record:
                {
                    int count = reader.ReadInt32();
                    var fields = new List<KeyValuePair<string, ScriptValue>>(count);

                    for (int i = 0; i < count; i++)
                    {
                        string key = reader.ReadString();
                        fields.Add(new KeyValuePair<string, ScriptValue>(key, ReadValue(reader)));
                    }

                    return ScriptValue.Record(ScriptRecord.From(fields));
                }

            case ScriptType.Table:
                {
                    int columns = reader.ReadInt32();
                    int rows = reader.ReadInt32();
                    var built = new List<ScriptColumn>(columns);

                    for (int c = 0; c < columns; c++)
                    {
                        string name = reader.ReadString();
                        var values = new ScriptValue[rows];

                        for (int i = 0; i < rows; i++) values[i] = ReadValue(reader);

                        built.Add(ScriptColumn.Own(name, values));
                    }

                    return ScriptValue.Table(ScriptTable.Create(built));
                }

            default:
                throw new NotSupportedException($"неизвестный тег значения {(int)type}");
        }
    }
}
