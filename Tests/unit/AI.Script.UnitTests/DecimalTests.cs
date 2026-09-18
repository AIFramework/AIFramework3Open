using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Точные числа, денежная запись и чистка выгрузки.
/// </summary>
/// <remarks>
/// Счёт до копейки — обещание языка перед отчётом: двоичная дробь в сумме расходится с книгой
/// Excel, и заметить это в готовом документе нечем. Поэтому проверяется и сама арифметика, и
/// то, что смешать её с обычным числом нельзя.
/// </remarks>
public sealed class DecimalTests
{
    private const string Sales = """
        let t = table.of({
            клиент: ["а", "б", "в"],
            сумма: ["1 234,50 ₽", "2 000,25 ₽", "765,25 ₽"],
            доля: ["12 %", "8 %", "80 %"]
        })
        """;

    // --- арифметика ---

    [Fact]
    public void Dec_AddsWithoutBinaryDrift()
    {
        Assert.Equal(0.3m, Script.Eval("dec.of(\"0.1\") + dec.of(\"0.2\")"));
    }

    /// <summary>
    /// Двоичное число рядом с точным — это та самая потерянная копейка, поэтому отказ, и отказ
    /// на проверке: до исполнения такой строки дело не доходит.
    /// </summary>
    [Fact]
    public void Dec_PlusNumber_IsRejected()
    {
        Diagnostic error = Script.CheckDiagnostic("emit r = dec.of(10) + 0.5", DiagnosticCodes.BadOperandTypes);

        Assert.Contains("dec.of", error.Hint, StringComparison.Ordinal);
    }

    /// <summary>Доля, ставка и курс — это умножение на число, и оно разрешено.</summary>
    [Fact]
    public void Dec_TimesNumber_Scales()
    {
        Assert.Equal(120m, Script.Eval("dec.of(100) * 1.2"));
        Assert.Equal(120m, Script.Eval("1.2 * dec.of(100)"));
    }

    [Fact]
    public void Dec_Comparison_Works()
    {
        Assert.True(Script.Flag("dec.of(\"2.50\") > dec.of(\"2.49\")"));
    }

    [Fact]
    public void Dec_SumOfList_IsExact()
    {
        Assert.Equal(0.3m, Script.Eval("dec.sum([dec.of(\"0.1\"), dec.of(\"0.1\"), dec.of(\"0.1\")])"));
    }

    [Fact]
    public void Dec_Round_HalfUpDiffersFromBank()
    {
        Assert.Equal(2.2m, Script.Eval("dec.round(dec.of(\"2.25\"), digits: 1, kind: \"bank\")"));
        Assert.Equal(2.3m, Script.Eval("dec.round(dec.of(\"2.25\"), digits: 1, kind: \"half_up\")"));
    }

    [Fact]
    public void Dec_Round_UnknownKind_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit r = dec.round(dec.of(1), kind: \"вверх\")");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
    }

    // --- денежная запись ---

    [Theory]
    [InlineData("1 234,50 ₽", 1234.50)]
    [InlineData("1 234.50", 1234.50)]
    [InlineData("(500)", -500)]
    [InlineData("-1 000 000 ₽", -1000000)]
    [InlineData("12 %", 0.12)]
    public void Dec_Parse_ReadsHumanMoney(string text, double expected)
    {
        Assert.Equal((decimal)expected, Script.Eval($"dec.parse(\"{text}\")"));
    }

    [Fact]
    public void Dec_Parse_EnglishLocale()
    {
        Assert.Equal(1234.50m, Script.Eval("dec.parse(\"$1,234.50\", locale: \"en\")"));
    }

    [Fact]
    public void Dec_Parse_Garbage_IsRejected()
    {
        Diagnostic error = Script.FailsWith("emit r = dec.parse(\"итого\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    // --- запись для человека ---

    [Fact]
    public void Fmt_Money_GroupsAndKeepsKopecks()
    {
        Assert.Equal("1 234 567,50 ₽", Script.Text("fmt.money(dec.of(\"1234567.5\"))"));
    }

    [Fact]
    public void Fmt_Num_RespectsLocale()
    {
        Assert.Equal("1 234,5", Script.Text("fmt.num(1234.5, digits: 1)"));
        Assert.Equal("1,234.5", Script.Text("fmt.num(1234.5, digits: 1, locale: \"en\")"));
    }

    /// <summary>На вход идёт доля: 0.124 — это 12,4 %, а не 0,1 %.</summary>
    [Fact]
    public void Fmt_Pct_TakesShare()
    {
        Assert.Equal("12,4 %", Script.Text("fmt.pct(0.124)"));
    }

    // --- чистка выгрузки ---

    [Fact]
    public void Clean_MoneyColumn_BecomesExact()
    {
        RunResult result = Script.RunOk($"{Sales}\nlet c = table.clean(t)\nemit s = dec.sum(c[\"сумма\"])");

        Assert.Equal(4000.00m, result.Emitted["s"]);
    }

    /// <summary>Проценты — это доля, а не деньги: колонка остаётся обычными числами.</summary>
    [Fact]
    public void Clean_PercentColumn_BecomesShare()
    {
        Assert.Equal(1.0, Script.Number("vec.sum(table.clean(t)[\"доля\"])", prelude: Sales), 9);
    }

    /// <summary>Одна строка примечания не превращает текстовую колонку в дырявые числа.</summary>
    [Fact]
    public void Clean_MostlyText_StaysText()
    {
        RunResult result = Script.RunOk("""
            let t = table.of({ note: ["10", "по договору", "уточняется", "нет счёта", "оплачено"] })
            emit r = table.clean(t)["note"][1]
            """);

        Assert.Equal("по договору", result.Emitted["r"]);
    }

    /// <summary>Непрочитанное становится пропуском, а не нулём: ноль сместил бы среднее молча.</summary>
    [Fact]
    public void Clean_UnreadableCell_IsMissingAndListed()
    {
        RunResult result = Script.RunOk("""
            let t = table.of({ сумма: ["10", "20", "30", "40", "по договору"] })
            let issues = table.issues(t)
            emit rows = len(issues)
            emit value = issues[0].value
            emit column = issues[0].column
            """);

        Assert.Equal(1.0, result.Emitted["rows"]);
        Assert.Equal("по договору", result.Emitted["value"]);
        Assert.Equal("сумма", result.Emitted["column"]);
    }

    [Fact]
    public void Clean_Dates_AreRecognised()
    {
        RunResult result = Script.RunOk("""
            let t = table.of({ дата: ["15.03.2026", "16.03.2026", "17.03.2026"] })
            emit r = date.year(table.clean(t)["дата"][0])
            """);

        Assert.Equal(2026.0, result.Emitted["r"]);
    }

    /// <summary>«н/д» — это пропуск, а не ноль: в числовой колонке он становится «не числом».</summary>
    [Fact]
    public void Clean_MissingMarks_BecomeGaps()
    {
        RunResult result = Script.RunOk("""
            let c = table.clean(table.of({ сумма: ["10", "20", "н/д", "40", "50"] }))

            emit пропуск = math.is_nan(c["сумма"][2])
            emit жалобы = len(table.issues(table.of({ сумма: ["10", "20", "н/д", "40", "50"] })))
            """);

        Assert.Equal(true, result.Emitted["пропуск"]);
        Assert.Equal(0.0, result.Emitted["жалобы"]);
    }

    // --- найденное при проверке ---

    /// <summary>Коды, периоды и экспонента не деньги: буквы внутри числа не выбрасываются.</summary>
    [Theory]
    [InlineData("SKU-100")]
    [InlineData("Q1 2024")]
    [InlineData("1E5")]
    public void Codes_AreNotNumbers(string text)
    {
        Assert.NotEmpty(Script.FailsWith($"emit v = dec.parse(\"{text}\")").Message);
    }

    [Theory]
    [InlineData("1 234,50 руб.")]
    [InlineData("RUB 1234.50")]
    [InlineData("1 234,50 ₽")]
    public void CurrencyWords_StillStripped(string text)
    {
        RunResult result = Script.RunOk($"emit v = dec.to_num(dec.parse(\"{text}\"))");

        Assert.Equal(1234.5, result.Emitted["v"]);
    }

    /// <summary>Смешанная колонка: уже числа остаются числами, а не пропусками.</summary>
    [Fact]
    public void Clean_MixedColumn_KeepsTypedValues()
    {
        RunResult result = Script.RunOk("""
            let t = table.of({ x: [1, "2,5", 3] }) |> table.clean()
            emit total = vec.sum(t["x"])
            """);

        Assert.Equal(6.5, (double)result.Emitted["total"]!, 9);
    }
}
