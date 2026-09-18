using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using Microsoft.Data.Sqlite;

namespace AI.Script.Data;

/// <summary>
/// База SQLite из хранилища прогона, открытая только на чтение.
/// </summary>
/// <remarks>
/// SQLite читает файл с диска, а хранилище прогона бывает в памяти или у хоста, поэтому база
/// копируется во временный файл и удаляется при закрытии. Своего SQL скрипт не пишет: таблица и
/// колонки сверяются со схемой и подставляются в запрос в кавычках.
/// </remarks>
internal sealed class SqliteCopy : IDisposable
{
    private readonly string _file;
    private readonly SqliteConnection _connection;

    private SqliteCopy(string file)
    {
        _file = file;
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = file,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());

        try
        {
            _connection.Open();

            using SqliteCommand command = _connection.CreateCommand();

            // Подчеркивание в LIKE это любой знак: без экранирования пропадали и пользовательские
            // таблицы вида «sqlite1».
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite\\_%' ESCAPE '\\' ORDER BY name";

            using SqliteDataReader reader = command.ExecuteReader();
            var tables = new List<string>();

            while (reader.Read()) tables.Add(reader.GetString(0));

            Tables = tables;
        }
        catch
        {
            // Не база: соединение закрывается до удаления копии, иначе файл занят и ошибка
            // удаления заслоняет настоящую причину.
            _connection.Dispose();
            throw;
        }
    }

    /// <summary>Пользовательские таблицы базы.</summary>
    public IReadOnlyList<string> Tables { get; }

    /// <summary>Таблица по имени либо единственная в базе.</summary>
    public string Table(string name)
    {
        if (name.Length == 0 && Tables.Count == 1) return Tables[0];

        if (Tables.Contains(name, StringComparer.Ordinal)) return name;

        throw new ScriptError(
            DiagnosticCodes.UnknownArgument,
            name.Length == 0 ? "data.sqlite: в базе несколько таблиц, назовите нужную" : $"data.sqlite: таблицы «{name}» в базе нет",
            "таблицы базы: " + string.Join(", ", Tables));
    }

    /// <summary>Колонки таблицы по порядку.</summary>
    public IReadOnlyList<string> Columns(string table)
    {
        using SqliteCommand command = _connection.CreateCommand();

        command.CommandText = $"SELECT name FROM pragma_table_info({Literal(table)})";

        using SqliteDataReader reader = command.ExecuteReader();
        var names = new List<string>();

        while (reader.Read()) names.Add(reader.GetString(0));

        return names;
    }

    /// <summary>Чтение колонок таблицы; имена уже сверены со схемой.</summary>
    public SqliteDataReader Select(string table, IReadOnlyList<string> columns)
    {
        SqliteCommand command = _connection.CreateCommand();

        command.CommandText = $"SELECT {string.Join(", ", columns.Select(Quote))} FROM {Quote(table)}";

        return command.ExecuteReader();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _connection.Dispose();
        Remove(_file);
    }

    /// <summary>Копирует базу во временный файл и открывает ее на чтение.</summary>
    public static async Task<SqliteCopy> OpenAsync(IScriptContext context, string path)
    {
        string file = Path.Combine(Path.GetTempPath(), $"ais-{Guid.NewGuid():N}.db");

        try
        {
            await using (Stream source = await context.Sandbox.OpenReadAsync(path, context.Cancellation).ConfigureAwait(false))
            await using (FileStream target = File.Create(file))
            {
                await source.CopyToAsync(target, context.Cancellation).ConfigureAwait(false);
            }

            return new SqliteCopy(file);
        }
        catch (SqliteException error)
        {
            Remove(file);

            throw new ScriptError(DiagnosticCodes.BadFileFormat, $"data.sqlite: '{path}' не открывается как база SQLite ({error.Message})");
        }
        catch
        {
            Remove(file);
            throw;
        }
    }

    /// <summary>Значение ячейки: числа числами, текст текстом, двоичное подписью размера.</summary>
    public static ScriptValue Value(SqliteDataReader reader, int column)
    {
        if (reader.IsDBNull(column)) return ScriptValue.None;

        return reader.GetValue(column) switch
        {
            long integer => ScriptValue.Num(integer),
            double real => ScriptValue.Num(real),
            string text => ScriptValue.Str(text),
            byte[] bytes => ScriptValue.Str($"[{bytes.Length} байт]"),
            var other => ScriptValue.Str(Convert.ToString(other, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
        };
    }

    /// <summary>Удаляет копию вместе с журналами WAL, которые SQLite заводит рядом с базой.</summary>
    private static void Remove(string file)
    {
        foreach (string part in new[] { file, file + "-wal", file + "-shm", file + "-journal" })
        {
            try
            {
                File.Delete(part);
            }
            catch (IOException)
            {
                // Временный файл удалит система: ронять прочитанный итог из-за уборки незачем.
            }
            catch (UnauthorizedAccessException)
            {
                // То же: копия во временной папке, и ее уборка не повод отказывать скрипту.
            }
        }
    }

    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static string Literal(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
}
