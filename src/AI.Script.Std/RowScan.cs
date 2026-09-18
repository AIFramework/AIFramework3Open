using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Построчный отбор при чтении большого файла: условие, колонки и предел строк прямо в читателе.
/// </summary>
/// <remarks>
/// Выгрузка на миллионы строк целиком в память прогона не ложится, а нужна из нее обычно малая
/// часть. Поэтому строка проверяется условием сразу после чтения и в таблицу попадает только
/// отобранное; потолок памяти считает отобранные строки, а не прочитанные. Сработал потолок —
/// сообщение говорит, сколько прочитано и сколько отобрано: по нему видно, ужесточать условие
/// или брать меньше колонок.
/// <para>
/// Общий для всех потоковых читателей: CSV в <c>io.scan</c>, Parquet и SQLite в своих сборках.
/// </para>
/// </remarks>
public sealed class RowScan
{
    private readonly IScriptContext _context;
    private readonly string _what;
    private readonly ScriptCallable? _where;
    private readonly int _limit;
    private readonly IReadOnlyList<string> _names;
    private readonly int[] _output;
    private readonly List<ScriptValue>[] _kept;

    /// <summary>Готовит отбор.</summary>
    /// <param name="context">Прогон.</param>
    /// <param name="what">Имя функции для сообщений.</param>
    /// <param name="names">Все колонки источника по порядку.</param>
    /// <param name="where">Условие на строку-запись; <c>null</c> — берутся все строки.</param>
    /// <param name="cols">Какие колонки оставить; <c>null</c> либо пусто — все.</param>
    /// <param name="limit">Сколько строк отобрать; 0 — без предела.</param>
    public RowScan(
        IScriptContext context, string what, IReadOnlyList<string> names, ScriptCallable? where, string[]? cols, int limit)
    {
        _context = context;
        _what = what;
        _names = names;
        _where = where;
        _limit = Math.Max(0, limit);
        _output = Indexes(what, names, cols);
        _kept = [.. _output.Select(_ => new List<ScriptValue>())];
    }

    /// <summary>Сколько строк прочитано.</summary>
    public long Read { get; private set; }

    /// <summary>Сколько строк отобрано.</summary>
    public int Kept { get; private set; }

    /// <summary>Отобран ли уже предел: дальше читать незачем.</summary>
    public bool Full => _limit > 0 && Kept >= _limit;

    /// <summary>Индексы оставляемых колонок источника: читателю, умеющему не читать лишнее.</summary>
    public IReadOnlyList<int> Output => _output;

    /// <summary>Нужна ли условию вся строка: без условия достаточно оставляемых колонок.</summary>
    public bool NeedsAllColumns => _where is not null;

    /// <summary>
    /// Предлагает строку: значения по порядку колонок источника. Читатель, не читавший лишних
    /// колонок (условия нет, см. <see cref="NeedsAllColumns"/>), оставляет на их местах пропуск.
    /// </summary>
    /// <returns><c>false</c> — предел отобран, чтение можно заканчивать.</returns>
    public async ValueTask<bool> OfferAsync(ScriptValue[] row)
    {
        _context.Cancellation.ThrowIfCancellationRequested();
        Read++;

        if (_where is not null)
        {
            ScriptRecord record = ScriptRecord.From(_names.Select((name, i) => new KeyValuePair<string, ScriptValue>(name, row[i])));
            ScriptValue passed = await _context.CallAsync(ScriptValue.Fn(_where), ScriptValue.Record(record)).ConfigureAwait(false);

            if (!passed.AsBool($"{_what}: условие where")) return true;
        }

        Count();

        for (int j = 0; j < _output.Length; j++) _kept[j].Add(row[_output[j]]);

        Kept++;

        return !Full;
    }

    /// <summary>Отобранные строки таблицей.</summary>
    public ScriptTable Table() =>
        ScriptTable.Create([.. _output.Select((index, j) => ScriptColumn.Own(_names[index], [.. _kept[j]]))]);

    private void Count()
    {
        try
        {
            _context.CountAllocation(Math.Max(1, _output.Length));
        }
        catch (ScriptError error) when (error.Code == DiagnosticCodes.MemoryLimit)
        {
            throw new ScriptAbort(
                DiagnosticCodes.MemoryLimit,
                $"{_what}: потолок памяти прогона, прочитано строк {Read}, отобрано {Kept}",
                "ужесточите условие where, возьмите меньше колонок (cols) либо задайте limit");
        }
    }

    private static int[] Indexes(string what, IReadOnlyList<string> names, string[]? cols)
    {
        if (cols is not { Length: > 0 }) return [.. Enumerable.Range(0, names.Count)];

        var output = new int[cols.Length];

        for (int j = 0; j < cols.Length; j++)
        {
            int index = IndexOf(names, cols[j]);

            output[j] = index >= 0 ? index : throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{what}: колонки «{cols[j]}» в файле нет",
                "колонки файла: " + string.Join(", ", names));
        }

        return output;
    }

    private static int IndexOf(IReadOnlyList<string> names, string name)
    {
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name, StringComparison.Ordinal)) return i;
        }

        return -1;
    }
}
