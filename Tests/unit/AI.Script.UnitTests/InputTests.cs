using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Объявленные входы: что скрипт просит у хоста.
/// </summary>
/// <remarks>
/// Скрипт пишется раньше, чем найдены файлы, поэтому список недостающих данных обязан
/// возвращаться проверкой — до запуска и до первой оплаченной строки. Отсюда и проверки: вход
/// виден в списке, непереданный вход роняет проверку, а колонки поданной таблицы сверяются так
/// же, как у таблицы из файла.
/// </remarks>
public sealed class InputTests
{
    private const string Source = """
        let t = input("продажи", kind: "table", about: "выгрузка за квартал")

        emit r = vec.sum(t["сумма"])
        """;

    private static RunOptions With(params (string Name, object? Value)[] seeded)
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach ((string name, object? value) in seeded) data[name] = value;

        return new RunOptions { Seeded = data };
    }

    private static ScriptTable Sales() => ScriptTable.Create(
    [
        ScriptColumn.Own("клиент", [ScriptValue.Str("а"), ScriptValue.Str("б"), ScriptValue.Str("в")]),
        ScriptColumn.Own("сумма", [ScriptValue.Num(10), ScriptValue.Num(20), ScriptValue.Num(30)]),
    ]);

    [Fact]
    public void Input_IsListedByCheck()
    {
        CheckResult result = Script.Host().Check(Source);

        ScriptInput input = Assert.Single(result.Inputs);

        Assert.Equal("продажи", input.Name);
        Assert.Equal("table", input.Kind);
        Assert.Equal("выгрузка за квартал", input.About);
    }

    /// <summary>Отказ до первой строки: скрипт без данных сорвётся всё равно, но позже.</summary>
    [Fact]
    public void Input_NotGiven_FailsBeforeRun()
    {
        RunResult result = Script.RunWith(Script.Host(), Source, With());

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.MissingInput, result.Error!.Code);
        Assert.Contains("продажи", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Input_Given_IsRead()
    {
        RunResult result = Script.RunWith(Script.Host(), Source, With(("продажи", Sales())));

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(60.0, result.Emitted["r"]);
    }

    /// <summary>Колонка входа сверяется до запуска — так же, как колонка таблицы из файла.</summary>
    [Fact]
    public void Input_UnknownColumn_IsCaughtByCheck()
    {
        CheckResult result = Script.Host().Check(
            "let t = input(\"продажи\", kind: \"table\")\nemit r = vec.sum(t[\"сума\"])",
            With(("продажи", Sales())));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.UnknownColumn);
    }

    /// <summary>Имя входа обязано быть литералом: вычисленное нельзя ни показать, ни проверить.</summary>
    [Fact]
    public void Input_ComputedName_IsWarned()
    {
        CheckResult result = Script.Host().Check("let n = \"продажи\"\nemit r = input(n)");

        Assert.Empty(result.Inputs);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Input_SameNameTwice_IsListedOnce()
    {
        CheckResult result = Script.Host().Check(
            "let a = input(\"продажи\")\nlet b = input(\"продажи\")\nemit r = len(a) + len(b)");

        _ = Assert.Single(result.Inputs);
    }
}
