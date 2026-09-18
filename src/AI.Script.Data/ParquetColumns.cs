using AI.Script.Runtime;
using AI.Script.Semantics;
using Parquet;
using Parquet.Schema;

namespace AI.Script.Data;

/// <summary>Колонка группы строк Parquet значениями языка.</summary>
/// <remarks>
/// Тип берется из схемы файла, а не угадывается по значениям: колонка чисел, вся из нулей, остается
/// числовой. Текст читатель отдает отрезками памяти, а не строками, поэтому у текста своя ветка.
/// </remarks>
internal static class ParquetColumns
{
    public static Task<ScriptValue[]> ReadAsync(ParquetRowGroupReader group, DataField field, int count, CancellationToken ct)
    {
        Type clr = field.ClrType;

        return clr switch
        {
            _ when clr == typeof(ReadOnlyMemory<char>) || clr == typeof(string) =>
                ValuesAsync<ReadOnlyMemory<char>>(group, field, count, text => ScriptValue.Str(text.ToString()), ct),
            _ when clr == typeof(bool) => ValuesAsync<bool>(group, field, count, ScriptValue.Bool, ct),
            _ when clr == typeof(byte) => ValuesAsync<byte>(group, field, count, value => ScriptValue.Num(value), ct),
            _ when clr == typeof(sbyte) => ValuesAsync<sbyte>(group, field, count, value => ScriptValue.Num(value), ct),
            _ when clr == typeof(short) => ValuesAsync<short>(group, field, count, value => ScriptValue.Num(value), ct),
            _ when clr == typeof(ushort) => ValuesAsync<ushort>(group, field, count, value => ScriptValue.Num(value), ct),
            _ when clr == typeof(int) => ValuesAsync<int>(group, field, count, value => ScriptValue.Num(value), ct),
            _ when clr == typeof(uint) => ValuesAsync<uint>(group, field, count, value => ScriptValue.Num(value), ct),
            _ when clr == typeof(long) => ValuesAsync<long>(group, field, count, value => ScriptValue.Num(value), ct),
            _ when clr == typeof(ulong) => ValuesAsync<ulong>(group, field, count, value => ScriptValue.Num(value), ct),
            _ when clr == typeof(float) => ValuesAsync<float>(group, field, count, value => ScriptValue.Num(value), ct),
            _ when clr == typeof(double) => ValuesAsync<double>(group, field, count, ScriptValue.Num, ct),
            _ when clr == typeof(decimal) => ValuesAsync<decimal>(group, field, count, ScriptValue.Dec, ct),
            _ when clr == typeof(DateTime) => ValuesAsync<DateTime>(group, field, count, ScriptValue.Date, ct),
            _ when clr == typeof(DateTimeOffset) => ValuesAsync<DateTimeOffset>(group, field, count, value => ScriptValue.Date(value.UtcDateTime), ct),
            _ when clr == typeof(DateOnly) => ValuesAsync<DateOnly>(group, field, count, value => ScriptValue.Date(value.ToDateTime(TimeOnly.MinValue)), ct),
            _ when clr == typeof(TimeSpan) => ValuesAsync<TimeSpan>(group, field, count, ScriptValue.Dur, ct),
            _ when clr == typeof(byte[]) => BytesAsync(group, field, count, ct),
            _ => throw new ScriptError(
                DiagnosticCodes.BadFileFormat,
                $"data.parquet: колонка «{field.Name}» типа {clr.Name} не читается",
                "возьмите другие колонки через cols"),
        };
    }

    private static async Task<ScriptValue[]> ValuesAsync<T>(
        ParquetRowGroupReader group, DataField field, int count, Func<T, ScriptValue> map, CancellationToken ct)
        where T : struct
    {
        var values = new ScriptValue[count];

        if (field.IsNullable)
        {
            var buffer = new T?[count];

            await group.ReadAsync<T>(field, buffer.AsMemory(), null, ct).ConfigureAwait(false);

            for (int i = 0; i < count; i++) values[i] = buffer[i] is T value ? map(value) : ScriptValue.None;
        }
        else
        {
            var buffer = new T[count];

            await group.ReadAsync<T>(field, buffer.AsMemory(), null, ct).ConfigureAwait(false);

            for (int i = 0; i < count; i++) values[i] = map(buffer[i]);
        }

        return values;
    }

    private static async Task<ScriptValue[]> BytesAsync(ParquetRowGroupReader group, DataField field, int count, CancellationToken ct)
    {
        byte[]?[] buffer = new byte[count][];

        await group.ReadAsync(field, buffer.AsMemory(), null, ct).ConfigureAwait(false);

        return Array.ConvertAll(buffer, bytes => bytes is null ? ScriptValue.None : ScriptValue.Str($"[{bytes.Length} байт]"));
    }
}
