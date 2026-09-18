using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Script.Std;
using Microsoft.Data.Sqlite;
using Parquet;
using Parquet.Schema;

namespace AI.Script.Data;

/// <summary>
/// Пространство <c>data</c>: Parquet и SQLite только на чтение, с выбором колонок и отбором строк.
/// </summary>
/// <remarks>
/// Колоночный файл хорош тем, что колонку можно прочитать, не трогая остальных: две колонки из
/// выгрузки на десять миллионов строк это две колонки, а не вся выгрузка. Поэтому без условия
/// читаются только названные колонки, а строки идут через общий отбор (<see cref="RowScan"/>),
/// как у <c>io.scan</c>. Базы открываются только на чтение и без своего SQL: таблица и колонки
/// сверяются со схемой, и запрос из скрипта не может ни изменить базу, ни выйти за ее пределы.
/// </remarks>
[ScriptModule("data", "Parquet и SQLite на чтение: колонки и отбор строк при чтении", Version = "0.1", Group = "данные")]
public static class DataModule
{
    [ScriptFn("parquet", "Читает Parquet: только названные колонки, условие и предел строк",
        Example = "data.parquet(\"stock.parquet\", cols: [\"sku\", \"qty\"])", Reads = "parquet")]
    public static async Task<ScriptTable> Parquet(
        IScriptContext context,
        [ScriptParam("путь к файлу .parquet")] string path,
        [ScriptParam("какие колонки взять; нет — все")] string[]? cols = null,
        [ScriptParam("условие на строку-запись; нет — все строки")] ScriptCallable? where = null,
        [ScriptParam("сколько строк отобрать; 0 — без предела")] int limit = 0)
    {
        await using Stream source = await context.Sandbox.OpenReadAsync(path, context.Cancellation).ConfigureAwait(false);
        await using Stream stream = await Seekable(source, context.Cancellation).ConfigureAwait(false);
        await using ParquetReader reader = await ParquetReader.CreateAsync(stream, cancellationToken: context.Cancellation).ConfigureAwait(false);

        DataField[] fields = reader.Schema.GetDataFields();
        var scan = new RowScan(context, "data.parquet", [.. fields.Select(field => field.Name)], where, cols, limit);
        IReadOnlyList<int> needed = scan.NeedsAllColumns ? [.. Enumerable.Range(0, fields.Length)] : scan.Output;

        for (int g = 0; g < reader.RowGroupCount; g++)
        {
            using ParquetRowGroupReader group = reader.OpenRowGroupReader(g);
            int count = (int)Math.Min(group.RowCount, int.MaxValue);
            var columns = new ScriptValue[fields.Length][];

            foreach (int j in needed) columns[j] = await ParquetColumns.ReadAsync(group, fields[j], count, context.Cancellation).ConfigureAwait(false);

            for (int r = 0; r < count; r++)
            {
                var row = new ScriptValue[fields.Length];

                for (int j = 0; j < row.Length; j++) row[j] = columns[j] is { } column ? column[r] : ScriptValue.None;

                if (!await scan.OfferAsync(row).ConfigureAwait(false)) return scan.Table();
            }
        }

        return scan.Table();
    }

    [ScriptFn("sqlite", "Читает таблицу базы SQLite: колонки, условие и предел строк",
        Example = "data.sqlite(\"shop.db\", table: \"orders\", limit: 100)", Reads = "sqlite,sqlite3,db")]
    public static async Task<ScriptTable> Sqlite(
        IScriptContext context,
        [ScriptParam("путь к файлу базы")] string path,
        [ScriptParam("таблица; пусто — единственная в базе")] string table = "",
        [ScriptParam("какие колонки взять; нет — все")] string[]? cols = null,
        [ScriptParam("условие на строку-запись; нет — все строки")] ScriptCallable? where = null,
        [ScriptParam("сколько строк отобрать; 0 — без предела")] int limit = 0)
    {
        using var database = await SqliteCopy.OpenAsync(context, path).ConfigureAwait(false);
        string name = database.Table(table);
        IReadOnlyList<string> names = database.Columns(name);
        var scan = new RowScan(context, "data.sqlite", names, where, cols, limit);
        IReadOnlyList<int> selected = scan.NeedsAllColumns ? [.. Enumerable.Range(0, names.Count)] : scan.Output;

        using SqliteDataReader reader = database.Select(name, [.. selected.Select(index => names[index])]);

        while (await reader.ReadAsync(context.Cancellation).ConfigureAwait(false))
        {
            var row = new ScriptValue[names.Count];

            Array.Fill(row, ScriptValue.None);

            for (int k = 0; k < selected.Count; k++) row[selected[k]] = SqliteCopy.Value(reader, k);

            if (!await scan.OfferAsync(row).ConfigureAwait(false)) break;
        }

        return scan.Table();
    }

    [ScriptFn("tables", "Таблицы базы SQLite", Example = "data.tables(\"shop.db\")")]
    public static async Task<string[]> Tables(IScriptContext context, [ScriptParam("путь к файлу базы")] string path)
    {
        using var database = await SqliteCopy.OpenAsync(context, path).ConfigureAwait(false);

        return [.. database.Tables];
    }

    /// <summary>Parquet читает по смещениям: поток без перемотки копируется в память.</summary>
    private static async Task<Stream> Seekable(Stream source, CancellationToken ct)
    {
        if (source.CanSeek) return source;

        var copy = new MemoryStream();

        await source.CopyToAsync(copy, ct).ConfigureAwait(false);
        copy.Position = 0;

        return copy;
    }
}
