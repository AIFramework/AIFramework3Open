using System.Text;
using AI.Script.Hosting;
using AI.Script.Semantics;
using Microsoft.Data.Sqlite;
using Parquet.Serialization;

namespace AI.Script.UnitTests;

/// <summary>
/// Большие файлы потоком (<c>io.scan</c>) и колоночные файлы и базы (<c>data</c>).
/// </summary>
public sealed class DataScanTests
{
    private const int Rows = 50_000;

    // --- io.scan ---

    /// <summary>
    /// Отбор при чтении укладывается в потолок памяти, в который файл целиком не лезет.
    /// </summary>
    [Fact]
    public void Scan_FitsWhereLoadDoesNot()
    {
        MemorySandbox store = Journal();

        RunResult scanned = Run(store, """
            let t = io.scan("journal.csv", where: row => row.amount > 49900, cols: ["id", "amount"])
            emit rows = len(t)
            emit first = t[0].id
            """, allocations: 5_000);
        RunResult loaded = Script.RunWith(Script.FullHost(), "emit rows = len(io.load(\"journal.csv\"))",
            Options(store, allocations: 5_000));

        Assert.True(scanned.Success, Script.Report(scanned));
        Assert.Equal(100.0, scanned.Emitted["rows"]);
        Assert.Equal(49901.0, scanned.Emitted["first"]);
        Assert.False(loaded.Success);
        Assert.Equal(DiagnosticCodes.MemoryLimit, loaded.Error!.Code);
    }

    /// <summary>Сработавший потолок говорит, сколько прочитано и отобрано.</summary>
    [Fact]
    public void Scan_MemoryLimit_TellsHowMuchWasRead()
    {
        RunResult result = Script.RunWith(Script.FullHost(), "emit rows = len(io.scan(\"journal.csv\"))", Options(Journal(), allocations: 3_000));

        Assert.False(result.Success);
        Assert.Contains("прочитано строк", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_Limit_StopsEarly_AndUnknownColumnListsKnown()
    {
        MemorySandbox store = Journal();

        RunResult limited = Run(store, "emit rows = len(io.scan(\"journal.csv\", limit: 10))");
        RunResult unknown = Script.RunWith(Script.FullHost(), "emit t = io.scan(\"journal.csv\", cols: [\"sum\"])", Options(store));

        Assert.Equal(10.0, limited.Emitted["rows"]);
        Assert.False(unknown.Success);
        Assert.Contains("amount", unknown.Error!.Hint, StringComparison.Ordinal);
    }

    // --- Parquet ---

    [Fact]
    public async Task Parquet_TwoColumns_And_Scan()
    {
        var store = new MemorySandbox();

        store.Put("stock.parquet", await StockAsync());

        RunResult result = Run(store, """
            let t = io.load("stock.parquet", cols: ["sku", "qty"])
            emit width = len(table.columns(t))
            emit rows = len(t)
            emit low = len(io.scan("stock.parquet", where: row => row.qty < 10))
            emit price = data.parquet("stock.parquet", limit: 1)[0].price
            """);

        Assert.Equal(2.0, result.Emitted["width"]);
        Assert.Equal(1000.0, result.Emitted["rows"]);
        Assert.Equal(10.0, result.Emitted["low"]);
        Assert.Equal(0.5, result.Emitted["price"]);
    }

    // --- SQLite ---

    [Fact]
    public void Sqlite_ReadsTableColumns_ReadOnly()
    {
        var store = new MemorySandbox();

        store.Put("shop.db", Shop());

        RunResult result = Run(store, """
            emit tables = data.tables("shop.db")
            let orders = data.sqlite("shop.db", table: "orders", cols: ["client", "total"], where: row => row.total > 100)
            emit rows = len(orders)
            emit client = orders[0].client
            """);
        RunResult wrong = Script.RunWith(Script.FullHost(), "emit t = data.sqlite(\"shop.db\")", Options(store));

        Assert.Equal(new List<object?> { "clients", "orders" }, result.Emitted["tables"]);
        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal("Бета", result.Emitted["client"]);
        Assert.False(wrong.Success);
        Assert.Contains("orders", wrong.Error!.Hint, StringComparison.Ordinal);
    }

    private static RunResult Run(MemorySandbox store, string source, long allocations = 0)
    {
        RunResult result = Script.RunWith(Script.FullHost(), source, Options(store, allocations));

        Assert.True(result.Success, Script.Report(result));

        return result;
    }

    private static RunOptions Options(MemorySandbox store, long allocations = 0)
    {
        var options = new RunOptions { Sandbox = store };

        if (allocations > 0) options.Limits.Allocations = allocations;

        return options;
    }

    private static MemorySandbox Journal()
    {
        var text = new StringBuilder("id;amount;note\n");

        for (int i = 1; i <= Rows; i++) text.Append(i).Append(';').Append(i).Append(";\"строка ").Append(i).Append("\"\n");

        var store = new MemorySandbox();

        store.Put("journal.csv", text.ToString());

        return store;
    }

    private static async Task<byte[]> StockAsync()
    {
        using var stream = new MemoryStream();

        await ParquetSerializer.SerializeAsync(
            Enumerable.Range(0, 1000).Select(i => new Stock { sku = $"S{i}", qty = i, price = 0.5 + i }), stream);

        return stream.ToArray();
    }

    private static byte[] Shop()
    {
        string file = Path.Combine(Path.GetTempPath(), $"shop-{Guid.NewGuid():N}.db");

        try
        {
            using (var connection = new SqliteConnection($"Data Source={file};Pooling=False"))
            {
                connection.Open();

                using SqliteCommand command = connection.CreateCommand();

                command.CommandText = """
                    CREATE TABLE orders (id INTEGER, client TEXT, total REAL, secret TEXT);
                    CREATE TABLE clients (name TEXT);
                    INSERT INTO orders VALUES (1, 'Альфа', 50, 'x'), (2, 'Бета', 150.5, 'y'), (3, 'Гамма', 900, 'z');
                    """;
                command.ExecuteNonQuery();
            }

            return File.ReadAllBytes(file);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private sealed class Stock
    {
        public string sku { get; set; } = string.Empty;

        public int qty { get; set; }

        public double price { get; set; }
    }

    // --- найденное при проверке ---

    [Fact]
    public void Scan_BlankLinesBeforeHeader_AreSkipped()
    {
        var store = new MemorySandbox();

        store.Put("t.csv", "\n\na,b\n1,2\n");

        RunResult result = Run(store, "emit rows = len(io.scan(\"t.csv\"))");

        Assert.Equal(1.0, result.Emitted["rows"]);
    }

    /// <summary>Номер плохой строки после образца тот же, что у io.load.</summary>
    [Fact]
    public void Scan_BadRowAfterSample_ReportsSameLineAsLoad()
    {
        var text = new StringBuilder("a,b\n");

        for (int i = 0; i < 1100; i++) text.Append("1,2\n");

        text.Append("1,2,3\n");

        var store = new MemorySandbox();

        store.Put("t.csv", text.ToString());

        string scanned = Script.RunWith(Script.FullHost(), "emit t = io.scan(\"t.csv\")", Options(store)).Error!.Message;
        string loaded = Script.RunWith(Script.FullHost(), "emit t = io.load(\"t.csv\")", Options(store)).Error!.Message;

        Assert.Equal(loaded, scanned);
    }

    /// <summary>Не база: внятный отказ, а временная копия не остается на диске.</summary>
    [Fact]
    public void Sqlite_NotADatabase_FailsClearlyWithoutLeak()
    {
        var store = new MemorySandbox();

        store.Put("bad.db", Encoding.UTF8.GetBytes(new string('x', 4096)));

        int before = Directory.GetFiles(Path.GetTempPath(), "ais-*.db*").Length;
        RunResult result = Script.RunWith(Script.FullHost(), "emit t = data.tables(\"bad.db\")", Options(store));

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.BadFileFormat, result.Error!.Code);
        Assert.Equal(before, Directory.GetFiles(Path.GetTempPath(), "ais-*.db*").Length);
    }

    /// <summary>Таблица с именем на «sqlite» пользовательская, а не служебная.</summary>
    [Fact]
    public void Sqlite_TableStartingWithSqlite_IsListed()
    {
        string file = Path.Combine(Path.GetTempPath(), $"s-{Guid.NewGuid():N}.db");

        try
        {
            using (var connection = new SqliteConnection($"Data Source={file};Pooling=False"))
            {
                connection.Open();

                using SqliteCommand command = connection.CreateCommand();

                command.CommandText = "CREATE TABLE sqlite1 (a INTEGER); INSERT INTO sqlite1 VALUES (1);";
                command.ExecuteNonQuery();
            }

            var store = new MemorySandbox();

            store.Put("s.db", File.ReadAllBytes(file));

            RunResult result = Run(store, "emit rows = len(data.sqlite(\"s.db\", table: \"sqlite1\"))");

            Assert.Equal(1.0, result.Emitted["rows"]);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
