using AI.DataStructs.Algebraic;
using AI.Script.Semantics;

namespace AI.Script.Runtime;

/// <summary>
/// Неизменяемая колоночная таблица.
/// </summary>
/// <remarks>
/// Колоночная, а не построчная: конвейеры обработки данных почти всегда работают со всей
/// колонкой сразу (нормировать, посчитать среднее, перевести в матрицу), и построчное хранение
/// заставляло бы собирать её заново на каждом шаге.
/// <para>
/// Строка собирается по требованию — <see cref="Row"/>. Это плата за <c>table.filter</c>, и она
/// осознанная: читаемость предиката <c>row =&gt; row.amount &gt; 0</c> стоит дороже, чем
/// экономия на сборке записи в прототипе.
/// </para>
/// </remarks>
public sealed class ScriptTable
{
    private readonly ScriptColumn[] _columns;
    private readonly Dictionary<string, int> _index;

    /// <summary>Пустая таблица без колонок.</summary>
    public static readonly ScriptTable Empty = new([], 0);

    private ScriptTable(ScriptColumn[] columns, int rowCount)
    {
        _columns = columns;
        RowCount = rowCount;
        _index = new Dictionary<string, int>(columns.Length, StringComparer.Ordinal);

        for (int i = 0; i < columns.Length; i++) _index[columns[i].Name] = i;
    }

    /// <summary>Колонки в порядке объявления.</summary>
    public IReadOnlyList<ScriptColumn> Columns => _columns;

    /// <summary>Число строк.</summary>
    public int RowCount { get; }

    /// <summary>Число колонок.</summary>
    public int ColumnCount => _columns.Length;

    /// <summary>Колонка по номеру.</summary>
    public ScriptColumn this[int index] => _columns[index];

    /// <summary>
    /// Собирает таблицу; все колонки обязаны быть одной длины, имена — различны.
    /// </summary>
    public static ScriptTable Create(IReadOnlyList<ScriptColumn> columns)
    {
        if (columns.Count == 0) return Empty;

        int rows = columns[0].Count;
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (ScriptColumn column in columns)
        {
            if (column.Count != rows)
            {
                throw new ScriptError(
                    DiagnosticCodes.SizeMismatch,
                    $"колонка '{column.Name}' содержит {column.Count} строк, а '{columns[0].Name}' — {rows}",
                    "все колонки таблицы обязаны быть одной длины");
            }

            if (!names.Add(column.Name))
            {
                throw new ScriptError(
                    DiagnosticCodes.DuplicateArgument,
                    $"колонка '{column.Name}' объявлена дважды",
                    "переименуйте одну из них: table.rename(t, from: \"...\", to: \"...\")");
            }
        }

        return new ScriptTable([.. columns], rows);
    }

    /// <summary>Ищет колонку по имени.</summary>
    public bool TryGet(string name, out ScriptColumn column)
    {
        if (_index.TryGetValue(name, out int position))
        {
            column = _columns[position];
            return true;
        }

        column = null!;
        return false;
    }

    /// <summary>Колонка по имени; отказ с подсказкой, если её нет.</summary>
    public ScriptColumn Column(string name)
    {
        if (TryGet(name, out ScriptColumn column)) return column;

        string? closest = Suggestions.Closest(name, Names());

        throw new ScriptError(
            DiagnosticCodes.UnknownArgument,
            $"в таблице нет колонки '{name}'",
            closest != null
                ? $"возможно, имелось в виду: {closest}"
                : $"колонки: {string.Join(", ", Names())}");
    }

    /// <summary>Имена колонок.</summary>
    public IReadOnlyList<string> Names()
    {
        var names = new string[_columns.Length];

        for (int i = 0; i < _columns.Length; i++) names[i] = _columns[i].Name;

        return names;
    }

    /// <summary>Строка как запись «колонка → значение».</summary>
    public ScriptRecord Row(int index)
    {
        var fields = new List<KeyValuePair<string, ScriptValue>>(_columns.Length);

        foreach (ScriptColumn column in _columns)
            fields.Add(new KeyValuePair<string, ScriptValue>(column.Name, column[index]));

        return ScriptRecord.From(fields);
    }

    /// <summary>Перечисляет строки записями.</summary>
    public IEnumerable<ScriptRecord> Rows()
    {
        for (int i = 0; i < RowCount; i++) yield return Row(i);
    }

    /// <summary>Таблица из указанных строк в указанном порядке.</summary>
    public ScriptTable Take(IReadOnlyList<int> rows)
    {
        if (_columns.Length == 0) return Empty;

        var columns = new ScriptColumn[_columns.Length];

        for (int i = 0; i < _columns.Length; i++) columns[i] = _columns[i].Take(rows);

        return new ScriptTable(columns, rows.Count);
    }

    /// <summary>Таблица только из указанных колонок, в указанном порядке.</summary>
    public ScriptTable Select(IReadOnlyList<string> names)
    {
        var columns = new List<ScriptColumn>(names.Count);

        foreach (string name in names) columns.Add(Column(name));

        return Create(columns);
    }

    /// <summary>Таблица без указанных колонок.</summary>
    public ScriptTable Without(IReadOnlyCollection<string> names)
    {
        foreach (string name in names) _ = Column(name);

        var kept = new List<ScriptColumn>(_columns.Length);

        foreach (ScriptColumn column in _columns)
        {
            if (!names.Contains(column.Name)) kept.Add(column);
        }

        return Create(kept);
    }

    /// <summary>Копия с добавленной либо заменённой колонкой.</summary>
    public ScriptTable With(ScriptColumn column)
    {
        var columns = new List<ScriptColumn>(_columns.Length + 1);
        bool replaced = false;

        foreach (ScriptColumn existing in _columns)
        {
            if (string.Equals(existing.Name, column.Name, StringComparison.Ordinal))
            {
                columns.Add(column);
                replaced = true;
                continue;
            }

            columns.Add(existing);
        }

        if (!replaced) columns.Add(column);

        return Create(columns);
    }

    /// <summary>
    /// Матрица «строка × колонка» из числовых колонок.
    /// </summary>
    /// <remarks>
    /// Нечисловая колонка — отказ с перечислением виновных, а не молчаливый пропуск: матрица
    /// с исчезнувшими признаками обучит модель не на тех данных, и заметить это будет нечем.
    /// </remarks>
    public Matrix ToMatrix()
    {
        var offenders = new List<string>();

        foreach (ScriptColumn column in _columns)
        {
            if (column.Type != ScriptType.Num) offenders.Add($"{column.Name} ({column.Type.ToName()})");
        }

        if (offenders.Count > 0)
        {
            throw new ScriptError(
                DiagnosticCodes.TypeMismatch,
                $"в матрицу переводятся только числовые колонки; нечисловые: {string.Join(", ", offenders)}",
                "уберите их (table.drop) либо закодируйте (table.one_hot)");
        }

        var matrix = new Matrix(RowCount, _columns.Length);

        for (int j = 0; j < _columns.Length; j++)
        {
            Vector values = _columns[j].ToVector();

            for (int i = 0; i < RowCount; i++) matrix[i, j] = values[i];
        }

        return matrix;
    }

    /// <summary>Таблица из матрицы; имена колонок задаются или получаются как <c>c0</c>, <c>c1</c>…</summary>
    public static ScriptTable FromMatrix(Matrix matrix, IReadOnlyList<string>? names = null)
    {
        var columns = new List<ScriptColumn>(matrix.Width);

        for (int j = 0; j < matrix.Width; j++)
        {
            var values = new Vector(matrix.Height);

            for (int i = 0; i < matrix.Height; i++) values[i] = matrix[i, j];

            string name = names != null && j < names.Count ? names[j] : $"c{j}";
            columns.Add(ScriptColumn.FromVector(name, values));
        }

        return Create(columns);
    }
}
