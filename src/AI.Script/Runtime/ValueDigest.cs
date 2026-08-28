using AI.DataStructs.Algebraic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AI.Script.Runtime;

/// <summary>
/// Устойчивый отпечаток значения языка — основа ключа кэша стадий.
/// </summary>
/// <remarks>
/// Отпечаток строится по содержимому, а не по ссылке: две одинаковые таблицы, собранные
/// разными путями, обязаны дать один ключ, иначе кэш не срабатывал бы никогда.
/// <para>
/// Не всякое значение отпечатывается. Дескриптор (<c>handle</c>) — это живой объект
/// фреймворка с собственным состоянием, а функция — код с захваченной областью; ни для того,
/// ни для другого честного содержательного отпечатка не существует. Такие значения делают
/// стадию некэшируемой, и об этом сообщается, а не умалчивается.
/// </para>
/// </remarks>
public static class ValueDigest
{
    /// <summary>
    /// Разделитель составляющих ключа.
    /// </summary>
    /// <remarks>
    /// Управляющий символ, а не запятая: иначе строки <c>"a,b"</c> и пара <c>"a"</c>, <c>"b"</c>
    /// склеились бы в один и тот же ключ, и стадия получила бы чужой результат.
    /// </remarks>
    private const char Separator = (char)1;

    /// <summary>Итог попытки отпечатать значение.</summary>
    /// <param name="Success">Удалось ли построить отпечаток.</param>
    /// <param name="Reason">Почему не удалось; <c>null</c> при успехе.</param>
    public readonly record struct Result(bool Success, string? Reason)
    {
        /// <summary>Успешный итог.</summary>
        public static readonly Result Ok = new(true, null);
    }

    /// <summary>
    /// Строит отпечаток набора значений вместе с текстом и версиями.
    /// </summary>
    /// <param name="parts">Текстовые составляющие ключа: текст стадии, версии модулей.</param>
    /// <param name="values">Значения аргументов.</param>
    /// <param name="key">Полученный ключ.</param>
    /// <param name="reason">Причина отказа, если ключ построить нельзя.</param>
    /// <returns><c>true</c>, если ключ построен.</returns>
    public static bool TryBuild(
        IEnumerable<string> parts,
        IEnumerable<ScriptValue> values,
        out string key,
        out string? reason)
    {
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(values);

        var builder = new StringBuilder();

        foreach (string part in parts) builder.Append(part).Append(Separator);

        foreach (ScriptValue value in values)
        {
            Result result = Append(builder, value);

            if (!result.Success)
            {
                key = string.Empty;
                reason = result.Reason;

                return false;
            }

            builder.Append(Separator);
        }

        key = Hash(builder.ToString());
        reason = null;

        return true;
    }

    /// <summary>Шестнадцатеричный отпечаток строки.</summary>
    public static string Hash(string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));

        return Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    private static Result Append(StringBuilder builder, ScriptValue value)
    {
        _ = builder.Append((int)value.Type).Append(':');

        switch (value.Type)
        {
            case ScriptType.None:
                return Result.Ok;

            case ScriptType.Num:
                // «R» вместо формата по умолчанию: иначе 0.1 + 0.2 и 0.3 дали бы один ключ,
                // хотя это разные числа и стадия вправе посчитать по ним разное.
                _ = builder.Append(value.RawNumber.ToString("R", CultureInfo.InvariantCulture));
                return Result.Ok;

            case ScriptType.Bool:
                _ = builder.Append(value.RawNumber != 0 ? '1' : '0');
                return Result.Ok;

            case ScriptType.Str:
                _ = builder.Append(value.AsString());
                return Result.Ok;

            case ScriptType.Date:
                _ = builder.Append(value.AsDate().Ticks);
                return Result.Ok;

            case ScriptType.Dur:
                _ = builder.Append(value.AsDuration().Ticks);
                return Result.Ok;

            case ScriptType.Range:
                {
                    ScriptRange range = value.AsRange();
                    _ = builder.Append(range.Start).Append('.').Append(range.End).Append('.').Append(range.Step);

                    return Result.Ok;
                }

            case ScriptType.Vec:
                return AppendNumbers(builder, value.AsVector());

            case ScriptType.Mat:
                {
                    Matrix matrix = value.AsMatrix();
                    _ = builder.Append(matrix.Height).Append('x').Append(matrix.Width).Append(';');

                    for (int i = 0; i < matrix.Height; i++)
                    {
                        for (int j = 0; j < matrix.Width; j++)
                            _ = builder.Append(matrix[i, j].ToString("R", CultureInfo.InvariantCulture)).Append(',');
                    }

                    return Result.Ok;
                }

            case ScriptType.List:
                {
                    ScriptList list = value.AsList();
                    _ = builder.Append(list.Count).Append(';');

                    for (int i = 0; i < list.Count; i++)
                    {
                        Result item = Append(builder, list[i]);
                        if (!item.Success) return item;

                        _ = builder.Append(',');
                    }

                    return Result.Ok;
                }

            case ScriptType.Record:
                {
                    ScriptRecord record = value.AsRecord();
                    _ = builder.Append(record.Count).Append(';');

                    for (int i = 0; i < record.Count; i++)
                    {
                        _ = builder.Append(record.Keys[i]).Append('=');

                        Result field = Append(builder, record.Values[i]);
                        if (!field.Success) return field;

                        _ = builder.Append(',');
                    }

                    return Result.Ok;
                }

            case ScriptType.Table:
                {
                    ScriptTable table = value.AsTable();
                    _ = builder.Append(table.RowCount).Append('x').Append(table.ColumnCount).Append(';');

                    foreach (ScriptColumn column in table.Columns)
                    {
                        _ = builder.Append(column.Name).Append('=');

                        for (int i = 0; i < column.Count; i++)
                        {
                            Result cell = Append(builder, column[i]);
                            if (!cell.Success) return cell;

                            _ = builder.Append(',');
                        }

                        _ = builder.Append(';');
                    }

                    return Result.Ok;
                }

            case ScriptType.Fn:
                return new Result(false, "функция в аргументах");

            case ScriptType.Handle:
                return new Result(false, $"дескриптор {value.AsHandle().TypeName} в аргументах");

            default:
                return new Result(false, $"значение типа {value.Type.ToName()}");
        }
    }

    private static Result AppendNumbers(StringBuilder builder, Vector vector)
    {
        _ = builder.Append(vector.Count).Append(';');

        for (int i = 0; i < vector.Count; i++)
            _ = builder.Append(vector[i].ToString("R", CultureInfo.InvariantCulture)).Append(',');

        return Result.Ok;
    }
}
