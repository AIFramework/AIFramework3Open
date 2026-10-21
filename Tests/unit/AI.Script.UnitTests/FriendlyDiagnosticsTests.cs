using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Ошибки, которые чаще всего делают модели и люди из других языков, дают одну диагностику с
/// точным советом, а не каскад: комментарий //, степень **, время через пробел, неизвестная
/// функция и неизвестный метод — с перечнем того, что есть.
/// </summary>
public class FriendlyDiagnosticsTests
{
    [Fact]
    public void SlashComment_IsOneErrorWithHint()
    {
        IReadOnlyList<string> codes = Script.CheckCodes("// сумма\nlet x = 1 + 2 // и ещё\nemit x = x");

        Assert.Equal(2, codes.Count);
        Assert.Contains("#", Script.CheckFailsWith("// сумма\nemit x = 1").Hint);
    }

    [Fact]
    public void PythonPower_IsOneErrorAndParsesAsCaret()
    {
        IReadOnlyList<string> codes = Script.CheckCodes("emit x = 2 ** 10");

        Assert.Single(codes);
        Assert.Contains("^", Script.CheckFailsWith("emit x = 2 ** 10").Message);
    }

    [Fact]
    public void TimeWithSpace_IsOneErrorWithBothSpellings()
    {
        Diagnostic error = Script.CheckFailsWith("let t = 1.5 h\nemit t = t");

        Assert.Single(Script.CheckCodes("let t = 1.5 h\nemit t = t"));
        Assert.Contains("1.5h", error.Hint);
        Assert.Contains("qty.of(1.5, \"h\")", error.Hint);
    }

    [Fact]
    public void UnknownFunction_WithoutCloseName_ListsNamespace()
    {
        Diagnostic error = Script.CheckFailsWith("emit x = fmt.percent(0.5)");

        Assert.Equal(DiagnosticCodes.UnknownFunction, error.Code);
        Assert.Contains("pct", error.Hint);
    }

    [Fact]
    public void UnknownHandleMethod_ListsMethods()
    {
        Diagnostic error = Script.FailsWith("let m = ml.linreg(<1, 2, 3>, <2, 4, 6>)\nemit k = m.coefficients()");

        Assert.Contains("predict", error.Hint);
        Assert.Contains("score", error.Hint);
    }

    [Fact]
    public void Help_WithTaskWords_FindsFunctions()
    {
        Assert.Contains("stat.corr", Script.Text("help(\"корреляция\")"));
    }

    [Fact]
    public void CardForHost_KeepsOnlyNotesOfPresentModules()
    {
        string plain = Llm.ScriptPrompt.CardFor(Script.Host());
        string full = Llm.ScriptPrompt.CardFor(Script.FullHost());

        Assert.DoesNotContain("<!--", plain);
        Assert.DoesNotContain("doc.extract", plain);
        Assert.Contains("doc.extract", full);
        Assert.Contains("Синтаксис в десяти строках", plain);
        Assert.True(Llm.ScriptPrompt.Card.Length < 8000);
    }

    [Fact]
    public void MatrixEmit_IsWrittenAsRowsAndPrintedWithValues()
    {
        RunResult result = Script.RunOk("let m = mat.eye(2)\nprint(m)\nemit m = m");

        Assert.Contains("[1, 0]", result.Transcript[0]);
        Assert.Contains("[[1,0],[0,1]]", Std.Json.Write(Binding.Marshaller.FromClr(result.Emitted["m"]), pretty: false).Replace(" ", "").Replace("\n", ""));
    }
}
