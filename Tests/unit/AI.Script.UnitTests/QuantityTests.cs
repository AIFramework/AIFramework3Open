using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Величины с единицами: литерал, арифметика, перевод и проверка размерности до запуска.
/// </summary>
/// <remarks>
/// Главное здесь — не удобство записи, а отказ: сложение рублей с килограммами обязано падать на
/// проверке, а не давать правдоподобное число в отчёте о себестоимости.
/// </remarks>
public sealed class QuantityTests
{
    [Fact]
    public void Price_TimesMass_GivesMoney()
    {
        RunResult result = Script.RunOk("""
            let цена = 120 rub / 1 kg
            emit стоимость = цена * 3.5 kg
            emit запись = core.to_str(цена * 3.5 kg)
            """);

        var cost = Assert.IsType<QuantityValue>(result.Emitted["стоимость"]);

        Assert.Equal(420.0, cost.Value, 9);
        Assert.Equal("rub", cost.Unit);
        Assert.Equal("420 rub", result.Emitted["запись"]);
    }

    /// <summary>Сложение складывает в единице левого слагаемого: так пишет человек.</summary>
    [Fact]
    public void Add_SameDimension_KeepsLeftUnit()
    {
        Assert.Equal("1.5 kg", Script.Text("core.to_str(1 kg + 500 g)"));
    }

    [Fact]
    public void Russian_Symbols_Work()
    {
        Assert.Equal("2 кг", Script.Text("core.to_str(1500 г + 0.5 кг |> qty.to(\"кг\"))"));
    }

    /// <summary>Рубли плюс килограммы — ошибка проверки: до первой строки счёта.</summary>
    [Fact]
    public void Add_DifferentDimensions_FailsCheck()
    {
        Diagnostic error = Script.CheckDiagnostic("""
            let цена = 120 rub
            let вес = 3 kg
            emit r = цена + вес
            """, DiagnosticCodes.BadOperandTypes);

        Assert.Contains("rub", error.Message, StringComparison.Ordinal);
        Assert.Contains("kg", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Размерность, выведенная через умножение, тоже проверяется до запуска.</summary>
    [Fact]
    public void Compare_DerivedDimensions_FailsCheck()
    {
        Assert.Contains(DiagnosticCodes.BadOperandTypes, Script.CheckCodes("""
            let цена = 120 rub / 1 kg
            let итог = цена * 2 kg
            emit r = итог > 5 kg
            """));
    }

    /// <summary>Разная размерность, которую проверка вывести не смогла, всё равно ловится при счёте.</summary>
    [Fact]
    public void Add_DifferentDimensions_FailsAtRunToo()
    {
        Diagnostic error = Script.FailsWith("emit r = qty.of(1, \"rub\") + qty.of(1, \"kg\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    [Fact]
    public void Add_QuantityAndNumber_IsRejected()
    {
        Assert.Contains(DiagnosticCodes.BadOperandTypes, Script.CheckCodes("emit r = 5 kg + 3"));
    }

    /// <summary>Единицы сократились — получилось число, и с ним работают как с числом.</summary>
    [Fact]
    public void Ratio_OfSameDimension_IsNumber()
    {
        Assert.Equal(3.0, Script.Number("1500 g / 500 g"), 9);
    }

    [Fact]
    public void To_ConvertsAndKeepsEquality()
    {
        RunResult result = Script.RunOk("""
            let вес = 1.5 kg
            emit граммов = qty.value(вес, unit: "g")
            emit равно = qty.to(вес, "g") == вес
            emit единица = qty.unit(qty.to(вес, "g"))
            """);

        Assert.Equal(1500.0, (double)result.Emitted["граммов"]!, 9);
        Assert.Equal(true, result.Emitted["равно"]);
        Assert.Equal("g", result.Emitted["единица"]);
    }

    [Fact]
    public void To_DifferentDimension_IsRefused()
    {
        Diagnostic error = Script.FailsWith("emit r = qty.to(5 kg, \"m\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    /// <summary>Физика берётся из реестра СИ фреймворка: приставки и производные единицы.</summary>
    [Fact]
    public void Si_Registry_IsReused()
    {
        RunResult result = Script.RunOk("""
            let мощность = 2 kW
            emit ватт = qty.value(мощность, unit: "W")
            emit литров = qty.value(0.5 m * 1 m * 1 m, unit: "l")
            """);

        Assert.Equal(2000.0, (double)result.Emitted["ватт"]!, 9);
        Assert.Equal(500.0, (double)result.Emitted["литров"]!, 6);
    }

    /// <summary>Валюты — разные размерности: курса у языка нет.</summary>
    [Fact]
    public void Currencies_DoNotMix()
    {
        Assert.Contains(DiagnosticCodes.BadOperandTypes, Script.CheckCodes("emit r = 5 usd + 5 eur"));
    }

    /// <summary>Слитная запись остаётся длительностью, раздельная — величиной; час величиной не бывает.</summary>
    [Fact]
    public void Duration_StaysDuration()
    {
        Assert.Equal("dur", Script.Text("type(5m)"));
        Assert.Equal("qty", Script.Text("type(5 m)"));
        Assert.NotEmpty(Script.CheckCodes("emit r = 5 h"));
    }

    [Fact]
    public void UnknownUnit_IsRefused()
    {
        Diagnostic error = Script.FailsWith("emit r = qty.of(5, \"попугай\")");

        Assert.Equal(DiagnosticCodes.UnknownArgument, error.Code);
    }

    /// <summary>Величина переживает запись и чтение: и в кэш стадии, и в JSON.</summary>
    [Fact]
    public void Quantity_SurvivesCodecAndJson()
    {
        RunResult result = Script.RunOk("""
            emit json = json.write({ вес: 2.5 kg })
            """);

        Assert.Contains("\"unit\": \"kg\"", (string)result.Emitted["json"]!, StringComparison.Ordinal);

        using var stream = new MemoryStream();

        ScriptValueCodec.Write(stream, ScriptValue.Quantity(120, MeasureUnitOf("rub/kg")));
        stream.Position = 0;

        Assert.True(ScriptValueCodec.TryRead(stream, out ScriptValue back));
        Assert.Equal("120 rub/kg", ScriptFormatter.Format(back));
    }

    private static MeasureUnit MeasureUnitOf(string symbol)
    {
        Assert.True(MeasureUnit.TryParse(symbol, out MeasureUnit unit));

        return unit;
    }

    // --- проверка размерностей не мешает верным скриптам ---

    /// <summary>Параметр лямбды, переменная цикла и параметр функции заслоняют единицу внешнего имени.</summary>
    [Theory]
    [InlineData("let x = 5 kg\nemit r = [1 rub, 2 rub] |> core.map(x => x + 1 rub)")]
    [InlineData("let x = 5 kg\nfor x in [1 rub] { print(x + 1 rub) }")]
    [InlineData("let x = 5 kg\nfn f(x) { x + 1 rub }\nemit r = f(2 rub)")]
    public void InnerName_ShadowsOuterUnit(string source)
    {
        Assert.Empty(Script.CheckCodes(source));
    }

    [Fact]
    public void CompoundAssignment_ForgetsUnit()
    {
        RunResult result = Script.RunOk("""
            let a = 2 m
            set a *= 3 m
            emit r = a + 1 m * 1 m
            """);

        Assert.Equal(7.0, Assert.IsType<QuantityValue>(result.Emitted["r"]).Value, 9);
    }

    /// <summary>Присваивание в ветке случается не всегда: после него единица неизвестна, и проверка молчит.</summary>
    [Fact]
    public void AssignmentInBranch_ForgetsUnit()
    {
        Assert.Empty(Script.CheckCodes("let x = 5 kg\nif false { set x = 3 rub }\nemit r = x + 1 kg"));
    }

    /// <summary>Равенство разных размерностей при запуске просто ложно, и проверка его не запрещает.</summary>
    [Fact]
    public void Equality_AcrossDimensions_IsFalseNotError()
    {
        RunResult result = Script.RunOk("emit same = 5 kg == 5 rub");

        Assert.Equal(false, result.Emitted["same"]);
        Assert.NotEmpty(Script.CheckCodes("emit less = 5 kg < 5 rub"));
    }

    /// <summary>Число на безразмерную величину это число, а не величина с пустой единицей.</summary>
    [Fact]
    public void NumberOverDimensionless_IsNumber()
    {
        RunResult result = Script.RunOk("emit r = 1 / 2 deg");

        Assert.IsType<double>(result.Emitted["r"]);
    }

    /// <summary>Ключ кэша различает 1 kg и 1000 g: печатаются они по-разному.</summary>
    [Fact]
    public void StageCache_DistinguishesUnits()
    {
        var options = new RunOptions { Cache = new MemoryStageCache() };

        RunResult result = Script.RunOk("""
            @cache stage s(x: qty) -> str { core.to_str(x) }
            emit a = s(1 kg)
            emit b = s(1000 g)
            """, options);

        Assert.Equal("1 kg", result.Emitted["a"]);
        Assert.Equal("1000 g", result.Emitted["b"]);
    }
}
