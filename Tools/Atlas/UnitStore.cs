using Microsoft.Data.Sqlite;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Единица индекса: тип или метод с тем, что о нём нужно поиску и сравнению.
/// </summary>
/// <param name="Project">Сборка: <c>AI.DSP</c>. Вместе с <paramref name="Id"/> — ключ единицы.</param>
/// <param name="Id">Идентификатор в формате XML doc ID: <c>M:AI.DSP.FFT.CalcFFT(AI.DataStructs.Algebraic.Vector)</c>.</param>
/// <param name="Kind"><c>type</c>, <c>method</c>, <c>ctor</c> или <c>operator</c>.</param>
/// <param name="Access">Видимость снаружи сборки: <c>public</c>, <c>internal</c> или <c>private</c>.</param>
/// <param name="File">Путь к файлу от корня репозитория; для сборки из пакета — имя DLL.</param>
/// <param name="Line">Строка объявления, с единицы; ноль, если исходника нет.</param>
/// <param name="Signature">Объявление без тела: модификаторы, тип, имя, параметры.</param>
/// <param name="Doc">XML-комментарий без обёртки <c>member</c>.</param>
/// <param name="Body">Тело метода; у типа и у единицы из пакета пусто.</param>
/// <param name="Script">Имя функции AIScript, если метод её реализует: <c>dsp.fft</c>.</param>
/// <param name="Hash">Отпечаток сигнатуры, комментария и тела.</param>
/// <param name="Returns">У метода — тип результата (у конструктора — созданный тип), ключом <see cref="SymbolUnits.TypeKey"/>.</param>
/// <param name="Inputs">У метода — типы параметров через <c>|</c>, кроме <c>out</c>.</param>
/// <param name="Supertypes">У типа — базовые типы и интерфейсы через <c>|</c>.</param>
public sealed record CodeUnit(
    string Project, string Id, string Kind, string Access, string File, int Line,
    string Signature, string Doc, string Body, string? Script, string Hash,
    string Returns = "", string Inputs = "", string Supertypes = "")
{
    /// <summary>Статический ли метод: у экземплярного входом служит ещё и сам объект.</summary>
    public bool IsStatic => Signature.StartsWith("static ", StringComparison.Ordinal) || Signature.Contains(" static ", StringComparison.Ordinal);
}

/// <summary>
/// Поток данных: результат вызова <c>From</c> попадает во вход вызова <c>To</c> — аргументом,
/// объектом вызова или через локальную переменную.
/// </summary>
public sealed record CodeFlow(string FromProject, string FromId, string ToProject, string ToId, int Count);

/// <summary>Вызов: кто кого и сколько раз в одном теле.</summary>
public sealed record CodeCall(string CallerProject, string CallerId, string CalleeProject, string CalleeId, int Count);

/// <summary>Файл индекса.</summary>
/// <param name="Path">Путь от корня репозитория.</param>
/// <param name="Hash">Отпечаток текста: совпал — файл не разбирается.</param>
/// <param name="Area"><c>library</c>, <c>test</c>, <c>demo</c>, <c>tutorial</c> или <c>tool</c>.</param>
/// <param name="ShownAs">Что показывать человеку вместо пути: у сниппета — исходный туториал.</param>
public sealed record SourceFile(string Path, string Hash, string Area, string? ShownAs);

/// <summary>Вызывающий метод вместе с местом, где он живёт.</summary>
public sealed record Caller(CodeUnit Unit, string Area, string? ShownAs, int Count);

/// <summary>Вызываемый метод; <see cref="Unit"/> пуст, если он вне индекса (BCL, пакеты).</summary>
public sealed record Callee(string Project, string Id, int Count, CodeUnit? Unit);

/// <summary>
/// Индекс репозитория в SQLite: файлы, единицы, вызовы.
/// </summary>
/// <remarks>
/// Индекс — кэш: его всегда можно построить заново сканером. Поэтому смена схемы не
/// переносит данные, а пересоздаёт таблицы. Файл лежит в <c>%LOCALAPPDATA%\Atlas</c>, не в
/// репозитории (см. <see cref="AtlasSettings.IndexPath"/>).
/// <para>
/// Полнотекстового индекса SQLite здесь нет намеренно: лексический канал поиска — BM25 из
/// <c>AI.NLP</c>, и держать второй означало бы два ранжирования одного и того же.
/// </para>
/// </remarks>
public sealed class UnitStore : IDisposable
{
    /// <summary>Версия схемы и правил извлечения: сменилась — индекс строится заново.</summary>
    private const int SchemaVersion = 5;
    private const string UnitColumns =
        "u.project, u.id, u.kind, u.access, u.file, u.line, u.signature, u.doc, u.body, u.script, u.hash, u.returns, u.inputs, u.supertypes";

    private readonly SqliteConnection _connection;

    /// <summary>Открывает индекс, создавая файл и таблицы при необходимости.</summary>
    public UnitStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string? folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (folder != null) Directory.CreateDirectory(folder);

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        _connection.Open();

        Execute("PRAGMA journal_mode = WAL");
        Execute("CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL)");

        if (ReadMeta("schema") != SchemaVersion.ToString()) CreateSchema();
    }

    /// <summary>Число единиц в индексе.</summary>
    public int Count => Convert.ToInt32(Scalar("SELECT COUNT(*) FROM units"));

    /// <summary>Число записей о вызовах.</summary>
    public int CallCount => Convert.ToInt32(Scalar("SELECT COUNT(*) FROM calls"));

    /// <summary>Разобран ли файл с таким текстом.</summary>
    public bool IsCurrent(string path, string hash)
    {
        using SqliteCommand command = Command("SELECT hash FROM files WHERE path = $path");
        command.Parameters.AddWithValue("$path", path);
        return command.ExecuteScalar() as string == hash;
    }

    /// <summary>
    /// Заменяет единицы и вызовы одного файла на новый набор.
    /// </summary>
    /// <returns>
    /// Сколько единиц изменилось по содержанию: новые, с другим отпечатком, удалённые. Единица,
    /// которую только сдвинули по строкам, переписывается, но не считается: её векторы и
    /// сравнения остаются в силе.
    /// </returns>
    /// <remarks>
    /// Удаляются только строки этого файла: метод, переехавший в другой файл, уже перезаписан
    /// там под тем же ключом, и удаление по ключу стёрло бы его.
    /// </remarks>
    public int ReplaceFile(SourceFile file, IReadOnlyCollection<CodeUnit> units, IReadOnlyCollection<CodeCall> calls,
        IReadOnlyCollection<CodeFlow>? flows = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(calls);

        using SqliteTransaction transaction = _connection.BeginTransaction();

        Dictionary<(string, string), (string Hash, int Line)> existing = Existing(file.Path, transaction);
        int changed = 0;

        foreach (CodeUnit unit in units)
        {
            bool known = existing.Remove((unit.Project, unit.Id), out var old);

            if (known && old.Hash == unit.Hash && old.Line == unit.Line) continue;
            if (!known || old.Hash != unit.Hash) changed++;

            WriteUnit(unit, transaction);
        }

        foreach ((string project, string id) in existing.Keys)
        {
            using SqliteCommand delete = Command("DELETE FROM units WHERE project = $project AND id = $id AND file = $file", transaction);
            delete.Parameters.AddWithValue("$project", project);
            delete.Parameters.AddWithValue("$id", id);
            delete.Parameters.AddWithValue("$file", file.Path);
            changed += delete.ExecuteNonQuery();
        }

        WriteCalls(file.Path, calls, transaction);
        WriteFlows(file.Path, flows ?? [], transaction);
        WriteFile(file, transaction);

        transaction.Commit();
        return changed;
    }

    /// <summary>Удаляет файлы, которых больше нет, вместе с их единицами и вызовами.</summary>
    /// <returns>Сколько файлов удалено.</returns>
    public int RemoveFilesExcept(IReadOnlySet<string> present)
    {
        ArgumentNullException.ThrowIfNull(present);

        var stale = new List<string>();

        using (SqliteCommand select = Command("SELECT path FROM files"))
        using (SqliteDataReader reader = select.ExecuteReader())
        {
            while (reader.Read())
            {
                string path = reader.GetString(0);
                if (!present.Contains(path)) stale.Add(path);
            }
        }

        using SqliteTransaction transaction = _connection.BeginTransaction();

        foreach (string path in stale)
        {
            foreach (string table in (string[])["units", "calls", "flows"])
            {
                using SqliteCommand delete = Command($"DELETE FROM {table} WHERE file = $path", transaction);
                delete.Parameters.AddWithValue("$path", path);
                delete.ExecuteNonQuery();
            }

            using SqliteCommand file = Command("DELETE FROM files WHERE path = $path", transaction);
            file.Parameters.AddWithValue("$path", path);
            file.ExecuteNonQuery();
        }

        transaction.Commit();
        return stale.Count;
    }

    /// <summary>Все единицы индекса.</summary>
    public IReadOnlyList<CodeUnit> Units() => ReadUnits(Command($"SELECT {UnitColumns} FROM units u ORDER BY u.file, u.line"));

    /// <summary>Область файла единицы.</summary>
    public string AreaOf(CodeUnit unit)
    {
        using SqliteCommand command = Command("SELECT area FROM files WHERE path = $path");
        command.Parameters.AddWithValue("$path", unit.File);
        return command.ExecuteScalar() as string ?? "";
    }

    /// <summary>
    /// Единицы, у которых имя функции AIScript равно запросу или идентификатор его содержит.
    /// </summary>
    /// <remarks>
    /// Это поиск по имени для ручной проверки индекса, а не поиск по смыслу: тот появится на
    /// этапе 2. Библиотека выдаётся раньше тестов и демо.
    /// </remarks>
    public IReadOnlyList<CodeUnit> Find(string text, int limit = 20)
    {
        SqliteCommand command = Command($"""
            SELECT {UnitColumns} FROM units u JOIN files f ON f.path = u.file
            WHERE u.script = $exact OR u.id LIKE $like ESCAPE '\'
            ORDER BY u.script = $exact DESC, f.area = 'library' DESC, length(u.id)
            LIMIT $limit
            """);
        command.Parameters.AddWithValue("$exact", text);
        command.Parameters.AddWithValue("$like", "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%");
        command.Parameters.AddWithValue("$limit", limit);
        return ReadUnits(command);
    }

    /// <summary>Кто вызывает единицу: методы с их областью, по убыванию числа вызовов.</summary>
    public IReadOnlyList<Caller> Callers(CodeUnit unit)
    {
        using SqliteCommand command = Command($"""
            SELECT {UnitColumns}, f.area, f.shown_as, c.count
            FROM calls c
            JOIN units u ON u.project = c.caller_project AND u.id = c.caller_id
            JOIN files f ON f.path = u.file
            WHERE c.callee_project = $project AND c.callee_id = $id
            ORDER BY c.count DESC, u.id
            """);
        command.Parameters.AddWithValue("$project", unit.Project);
        command.Parameters.AddWithValue("$id", unit.Id);
        using SqliteDataReader reader = command.ExecuteReader();

        var callers = new List<Caller>();

        while (reader.Read())
            callers.Add(new Caller(ReadUnit(reader), reader.GetString(14), reader.IsDBNull(15) ? null : reader.GetString(15), reader.GetInt32(16)));

        return callers;
    }

    /// <summary>Кого вызывает единица, по убыванию числа вызовов.</summary>
    public IReadOnlyList<Callee> Callees(CodeUnit unit)
    {
        using SqliteCommand command = Command($"""
            SELECT c.callee_project, c.callee_id, c.count, {UnitColumns}
            FROM calls c
            LEFT JOIN units u ON u.project = c.callee_project AND u.id = c.callee_id
            WHERE c.caller_project = $project AND c.caller_id = $id
            ORDER BY c.count DESC, c.callee_id
            """);
        command.Parameters.AddWithValue("$project", unit.Project);
        command.Parameters.AddWithValue("$id", unit.Id);
        using SqliteDataReader reader = command.ExecuteReader();

        var callees = new List<Callee>();

        while (reader.Read())
            callees.Add(new Callee(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.IsDBNull(4) ? null : ReadUnit(reader, 3)));

        return callees;
    }

    /// <summary>
    /// Потоки данных по всему индексу, сложенные по парам «откуда → куда».
    /// </summary>
    public IReadOnlyDictionary<((string Project, string Id) From, (string Project, string Id) To), int> FlowCounts()
    {
        using SqliteCommand command = Command("""
            SELECT from_project, from_id, to_project, to_id, SUM(count) FROM flows
            GROUP BY from_project, from_id, to_project, to_id
            """);
        using SqliteDataReader reader = command.ExecuteReader();

        var flows = new Dictionary<((string, string), (string, string)), int>();

        while (reader.Read())
            flows[((reader.GetString(0), reader.GetString(1)), (reader.GetString(2), reader.GetString(3)))] = reader.GetInt32(4);

        return flows;
    }

    /// <summary>
    /// Соседи единицы по потокам данных: откуда приходит её вход (<paramref name="incoming"/>)
    /// или куда уходит её результат, по убыванию числа мест.
    /// </summary>
    public IReadOnlyList<(string Project, string Id, int Count)> FlowNeighbours(CodeUnit unit, bool incoming)
    {
        string near = incoming ? "to" : "from";
        string far = incoming ? "from" : "to";

        using SqliteCommand command = Command($"""
            SELECT {far}_project, {far}_id, SUM(count) AS total FROM flows
            WHERE {near}_project = $project AND {near}_id = $id
            GROUP BY {far}_project, {far}_id
            ORDER BY total DESC, {far}_id
            """);
        command.Parameters.AddWithValue("$project", unit.Project);
        command.Parameters.AddWithValue("$id", unit.Id);
        using SqliteDataReader reader = command.ExecuteReader();

        var neighbours = new List<(string, string, int)>();
        while (reader.Read()) neighbours.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));
        return neighbours;
    }

    /// <summary>Сколько методов вызывают каждую единицу: мера того, насколько она обжита.</summary>
    public IReadOnlyDictionary<(string Project, string Id), int> UsageCounts()
    {
        using SqliteCommand command = Command("SELECT callee_project, callee_id, COUNT(*) FROM calls GROUP BY callee_project, callee_id");
        using SqliteDataReader reader = command.ExecuteReader();

        var usage = new Dictionary<(string, string), int>();

        while (reader.Read()) usage[(reader.GetString(0), reader.GetString(1))] = reader.GetInt32(2);

        return usage;
    }

    /// <summary>Методы библиотеки с телом, любой видимости: материал для поиска дублей.</summary>
    public IReadOnlyList<CodeUnit> LibraryMethods() => ReadUnits(Command($"""
        SELECT {UnitColumns} FROM units u JOIN files f ON f.path = u.file
        WHERE f.area = 'library' AND u.kind <> 'type' AND length(u.body) > 0
        ORDER BY u.project, u.id
        """));

    /// <summary>Кого вызывает каждый метод: ключи вызываемых в виде «сборка|id».</summary>
    public IReadOnlyDictionary<(string Project, string Id), HashSet<string>> CalleeSets()
    {
        using SqliteCommand command = Command("SELECT caller_project, caller_id, callee_project, callee_id FROM calls");
        using SqliteDataReader reader = command.ExecuteReader();

        var sets = new Dictionary<(string, string), HashSet<string>>();

        while (reader.Read())
        {
            var key = (reader.GetString(0), reader.GetString(1));
            if (!sets.TryGetValue(key, out var set)) sets[key] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(reader.GetString(2) + "|" + reader.GetString(3));
        }

        return sets;
    }

    /// <summary>
    /// Открытое API библиотеки: единицы из файлов области <c>library</c> с видимостью <c>public</c>.
    /// </summary>
    public IReadOnlyList<CodeUnit> PublicLibraryUnits() => ReadUnits(Command($"""
        SELECT {UnitColumns} FROM units u JOIN files f ON f.path = u.file
        WHERE f.area = 'library' AND u.access = 'public'
        ORDER BY u.project, u.id
        """));

    /// <summary>Ключ метаданных: состояние git, по которому построен индекс (<see cref="GitRepository.State"/>).</summary>
    public const string StateKey = "state";

    /// <summary>Значение метаданных; <c>null</c> — не записано.</summary>
    public string? Meta(string key) => ReadMeta(key);

    /// <summary>Записывает значение метаданных.</summary>
    public void SetMeta(string key, string value)
    {
        using SqliteCommand command = Command("INSERT OR REPLACE INTO meta (key, value) VALUES ($key, $value)");
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    /// <inheritdoc/>
    public void Dispose() => _connection.Dispose();

    private void CreateSchema()
    {
        foreach (string table in (string[])["units", "calls", "flows", "files"]) Execute($"DROP TABLE IF EXISTS {table}");

        Execute("CREATE TABLE files (path TEXT PRIMARY KEY, hash TEXT NOT NULL, area TEXT NOT NULL, shown_as TEXT)");
        Execute("""
            CREATE TABLE units (
                project TEXT NOT NULL, id TEXT NOT NULL, kind TEXT NOT NULL, access TEXT NOT NULL,
                file TEXT NOT NULL, line INTEGER NOT NULL, signature TEXT NOT NULL, doc TEXT NOT NULL,
                body TEXT NOT NULL, script TEXT, hash TEXT NOT NULL,
                returns TEXT NOT NULL, inputs TEXT NOT NULL, supertypes TEXT NOT NULL, PRIMARY KEY (project, id))
            """);
        Execute("CREATE INDEX units_file ON units (file)");
        Execute("CREATE INDEX units_script ON units (script)");
        Execute("""
            CREATE TABLE calls (
                file TEXT NOT NULL, caller_project TEXT NOT NULL, caller_id TEXT NOT NULL,
                callee_project TEXT NOT NULL, callee_id TEXT NOT NULL, count INTEGER NOT NULL)
            """);
        Execute("CREATE INDEX calls_file ON calls (file)");
        Execute("CREATE INDEX calls_caller ON calls (caller_project, caller_id)");
        Execute("CREATE INDEX calls_callee ON calls (callee_project, callee_id)");
        Execute("""
            CREATE TABLE flows (
                file TEXT NOT NULL, from_project TEXT NOT NULL, from_id TEXT NOT NULL,
                to_project TEXT NOT NULL, to_id TEXT NOT NULL, count INTEGER NOT NULL)
            """);
        Execute("CREATE INDEX flows_file ON flows (file)");
        Execute($"INSERT OR REPLACE INTO meta (key, value) VALUES ('schema', '{SchemaVersion}')");
    }

    private Dictionary<(string, string), (string Hash, int Line)> Existing(string file, SqliteTransaction transaction)
    {
        using SqliteCommand command = Command("SELECT project, id, hash, line FROM units WHERE file = $file", transaction);
        command.Parameters.AddWithValue("$file", file);
        using SqliteDataReader reader = command.ExecuteReader();

        var existing = new Dictionary<(string, string), (string, int)>();

        while (reader.Read()) existing[(reader.GetString(0), reader.GetString(1))] = (reader.GetString(2), reader.GetInt32(3));

        return existing;
    }

    private void WriteUnit(CodeUnit unit, SqliteTransaction transaction)
    {
        using SqliteCommand command = Command("""
            INSERT OR REPLACE INTO units (project, id, kind, access, file, line, signature, doc, body, script, hash, returns, inputs, supertypes)
            VALUES ($project, $id, $kind, $access, $file, $line, $signature, $doc, $body, $script, $hash, $returns, $inputs, $supertypes)
            """, transaction);

        command.Parameters.AddWithValue("$project", unit.Project);
        command.Parameters.AddWithValue("$id", unit.Id);
        command.Parameters.AddWithValue("$kind", unit.Kind);
        command.Parameters.AddWithValue("$access", unit.Access);
        command.Parameters.AddWithValue("$file", unit.File);
        command.Parameters.AddWithValue("$line", unit.Line);
        command.Parameters.AddWithValue("$signature", unit.Signature);
        command.Parameters.AddWithValue("$doc", unit.Doc);
        command.Parameters.AddWithValue("$body", unit.Body);
        command.Parameters.AddWithValue("$script", (object?)unit.Script ?? DBNull.Value);
        command.Parameters.AddWithValue("$hash", unit.Hash);
        command.Parameters.AddWithValue("$returns", unit.Returns);
        command.Parameters.AddWithValue("$inputs", unit.Inputs);
        command.Parameters.AddWithValue("$supertypes", unit.Supertypes);
        command.ExecuteNonQuery();
    }

    private void WriteFlows(string file, IReadOnlyCollection<CodeFlow> flows, SqliteTransaction transaction)
    {
        using (SqliteCommand delete = Command("DELETE FROM flows WHERE file = $file", transaction))
        {
            delete.Parameters.AddWithValue("$file", file);
            delete.ExecuteNonQuery();
        }

        using SqliteCommand insert = Command("""
            INSERT INTO flows (file, from_project, from_id, to_project, to_id, count)
            VALUES ($file, $from_project, $from_id, $to_project, $to_id, $count)
            """, transaction);

        SqliteParameter[] parameters =
        [
            insert.Parameters.Add("$file", SqliteType.Text), insert.Parameters.Add("$from_project", SqliteType.Text),
            insert.Parameters.Add("$from_id", SqliteType.Text), insert.Parameters.Add("$to_project", SqliteType.Text),
            insert.Parameters.Add("$to_id", SqliteType.Text), insert.Parameters.Add("$count", SqliteType.Integer),
        ];

        foreach (CodeFlow flow in flows)
        {
            object[] values = [file, flow.FromProject, flow.FromId, flow.ToProject, flow.ToId, flow.Count];
            for (int i = 0; i < parameters.Length; i++) parameters[i].Value = values[i];
            insert.ExecuteNonQuery();
        }
    }

    private void WriteCalls(string file, IReadOnlyCollection<CodeCall> calls, SqliteTransaction transaction)
    {
        using (SqliteCommand delete = Command("DELETE FROM calls WHERE file = $file", transaction))
        {
            delete.Parameters.AddWithValue("$file", file);
            delete.ExecuteNonQuery();
        }

        using SqliteCommand insert = Command("""
            INSERT INTO calls (file, caller_project, caller_id, callee_project, callee_id, count)
            VALUES ($file, $caller_project, $caller_id, $callee_project, $callee_id, $count)
            """, transaction);

        SqliteParameter[] parameters =
        [
            insert.Parameters.Add("$file", SqliteType.Text), insert.Parameters.Add("$caller_project", SqliteType.Text),
            insert.Parameters.Add("$caller_id", SqliteType.Text), insert.Parameters.Add("$callee_project", SqliteType.Text),
            insert.Parameters.Add("$callee_id", SqliteType.Text), insert.Parameters.Add("$count", SqliteType.Integer),
        ];

        foreach (CodeCall call in calls)
        {
            object[] values = [file, call.CallerProject, call.CallerId, call.CalleeProject, call.CalleeId, call.Count];
            for (int i = 0; i < parameters.Length; i++) parameters[i].Value = values[i];
            insert.ExecuteNonQuery();
        }
    }

    private void WriteFile(SourceFile file, SqliteTransaction transaction)
    {
        using SqliteCommand command = Command(
            "INSERT OR REPLACE INTO files (path, hash, area, shown_as) VALUES ($path, $hash, $area, $shown_as)", transaction);

        command.Parameters.AddWithValue("$path", file.Path);
        command.Parameters.AddWithValue("$hash", file.Hash);
        command.Parameters.AddWithValue("$area", file.Area);
        command.Parameters.AddWithValue("$shown_as", (object?)file.ShownAs ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private string? ReadMeta(string key)
    {
        using SqliteCommand command = Command("SELECT value FROM meta WHERE key = $key");
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private void Execute(string sql)
    {
        using SqliteCommand command = Command(sql);
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        using SqliteCommand command = Command(sql);
        return command.ExecuteScalar();
    }

    private SqliteCommand Command(string sql, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    private static IReadOnlyList<CodeUnit> ReadUnits(SqliteCommand command)
    {
        using (command)
        using (SqliteDataReader reader = command.ExecuteReader())
        {
            var units = new List<CodeUnit>();
            while (reader.Read()) units.Add(ReadUnit(reader));
            return units;
        }
    }

    private static CodeUnit ReadUnit(SqliteDataReader reader, int offset = 0) => new(
        reader.GetString(offset), reader.GetString(offset + 1), reader.GetString(offset + 2), reader.GetString(offset + 3),
        reader.GetString(offset + 4), reader.GetInt32(offset + 5), reader.GetString(offset + 6), reader.GetString(offset + 7),
        reader.GetString(offset + 8), reader.IsDBNull(offset + 9) ? null : reader.GetString(offset + 9), reader.GetString(offset + 10),
        reader.GetString(offset + 11), reader.GetString(offset + 12), reader.GetString(offset + 13));
}
