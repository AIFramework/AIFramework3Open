using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// JSON из строки и записи с вычисляемыми именами полей.
/// </summary>
/// <remarks>
/// Клей между шагами: ответы служб и поля документов приходят строкой, а имена полей сводки
/// часто берутся из данных. Без этих функций такой код писался обходом через файл.
/// </remarks>
public sealed class JsonModuleTests
{
    [Fact]
    public void Parse_TurnsTextIntoValues()
    {
        RunResult result = Script.RunOk("""
            let v = json.parse("{\"a\": [1, 2], \"b\": \"x\", \"c\": null}")
            emit second = v.a[1]
            emit b = v.b
            emit c = type(v.c)
            """);

        Assert.Equal(2.0, result.Emitted["second"]);
        Assert.Equal("x", result.Emitted["b"]);
        Assert.Equal("none", result.Emitted["c"]);
    }

    [Fact]
    public void Write_IsCompactByDefault()
    {
        Assert.Equal("{\"a\":1,\"b\":[true,null]}", Script.Text("json.write({ a: 1, b: [true, none] })"));
    }

    [Fact]
    public void Parse_AndWrite_RoundTrip()
    {
        Assert.Equal(0.2, Script.Number("json.parse(json.write({ t: 0.2, s: \"z\" })).t"));
    }

    [Fact]
    public void Parse_Malformed_IsReported()
    {
        Assert.Equal(DiagnosticCodes.BadFileFormat, Script.FailsWith("emit r = json.parse(\"{ oops\")").Code);
    }

    [Fact]
    public void Record_TakesNamesFromData()
    {
        RunResult result = Script.RunOk("""
            let words = ["мир", "дом", "мир"]
            let counts = core.record(["мир", "дом"] |> core.map(w => [w, len(words |> core.filter(x => x == w))]))
            emit mir = counts.мир
            emit fields = len(counts)
            """);

        Assert.Equal(2.0, result.Emitted["mir"]);
        Assert.Equal(2.0, result.Emitted["fields"]);
    }

    [Fact]
    public void Record_RejectsBadPair()
    {
        Assert.Equal(DiagnosticCodes.TypeMismatch, Script.FailsWith("emit r = core.record([[1, 2]])").Code);
    }

    [Fact]
    public void With_AddsOrReplacesField()
    {
        Assert.Equal(2.0, Script.Number("core.with({ a: 1 }, \"b\", 2).b"));
        Assert.Equal(5.0, Script.Number("core.with({ a: 1 }, \"a\", 5).a"));
    }
}
