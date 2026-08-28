using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>Регулярные выражения и работа с датами.</summary>
public sealed class TextAndDateTests
{
    [Fact]
    public void Re_Test()
    {
        Assert.True(Script.Flag("re.test(\"AIS1101\", \"^AIS[0-9]+$\")"));
        Assert.False(Script.Flag("re.test(\"AIS\", \"^AIS[0-9]+$\")"));
    }

    [Fact]
    public void Re_IgnoreCase()
    {
        Assert.True(Script.Flag("re.test(\"ААА\", \"^а+$\", ignore_case: true)"));
    }

    [Fact]
    public void Re_Find_ReturnsMatchRecord()
    {
        const string source = """
            let m = re.find("цена 1250 руб", "[0-9]+")
            emit text = m.text
            emit at = m.at
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal("1250", result.Emitted["text"]);
        Assert.Equal(5.0, result.Emitted["at"]);
    }

    [Fact]
    public void Re_Find_NoMatchGivesNone()
    {
        Assert.Equal("none", Script.Text("type(re.find(\"abc\", \"[0-9]+\"))"));
    }

    [Fact]
    public void Re_NamedGroups()
    {
        const string source = """
            let m = re.find("2026-08-28", "(?<y>[0-9]{4})-(?<mo>[0-9]{2})")
            emit year = m.named.y
            emit month = m.named.mo
            emit first = m.groups[0]
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal("2026", result.Emitted["year"]);
        Assert.Equal("08", result.Emitted["month"]);
        Assert.Equal("2026", result.Emitted["first"]);
    }

    [Fact]
    public void Re_FindAllAndSplitAndReplace()
    {
        Assert.Equal(3.0, Script.Number("len(re.find_all(\"a1 b22 c333\", \"[0-9]+\"))"), 9);
        Assert.Equal(3.0, Script.Number("len(re.split(\"a,b;c\", \"[,;]\"))"), 9);
        Assert.Equal("a b", Script.Text("re.replace(\"a   b\", \"\\\\s+\", to: \" \")"));
    }

    [Fact]
    public void Re_BadPattern_IsReported()
    {
        Diagnostic error = Script.FailsWith("emit r = re.test(\"a\", \"([\")");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
    }

    [Fact]
    public void Date_Literals_And_Parts()
    {
        const string source = """
            let d = @2026-08-28T14:30
            emit year = date.year(d)
            emit month = date.month(d)
            emit day = date.day(d)
            emit hour = date.hour(d)
            emit weekday = date.weekday(d)
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(2026.0, result.Emitted["year"]);
        Assert.Equal(8.0, result.Emitted["month"]);
        Assert.Equal(28.0, result.Emitted["day"]);
        Assert.Equal(14.0, result.Emitted["hour"]);
        Assert.Equal(5.0, result.Emitted["weekday"]);
    }

    [Fact]
    public void Date_ParseWithFormat()
    {
        Assert.Equal("2026-08-28", Script.Text("date.format(date.parse(\"28.08.2026\", format: \"dd.MM.yyyy\"))"));
    }

    [Fact]
    public void Date_ParseFailureGivesNone()
    {
        Assert.Equal("none", Script.Text("type(date.parse(\"не дата\"))"));
    }

    [Fact]
    public void Date_Arithmetic()
    {
        const string source = """
            let d = @2026-01-31
            emit plusMonth = date.format(date.add(d, months: 1))
            emit startOfMonth = date.format(date.start_of(@2026-08-28, unit: "month"))
            emit days = date.days(date.diff(@2026-01-03, @2026-01-01))
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal("2026-02-28", result.Emitted["plusMonth"]);
        Assert.Equal("2026-08-01", result.Emitted["startOfMonth"]);
        Assert.Equal(2.0, result.Emitted["days"]);
    }

    [Fact]
    public void Date_Of_RejectsImpossibleDate()
    {
        Assert.Equal(DiagnosticCodes.BadOperand, Script.FailsWith("emit r = date.of(2026, month: 13)").Code);
    }

    [Fact]
    public void Date_DurationLiteralsAndConversion()
    {
        Assert.Equal(90.0, Script.Number("date.seconds(1m + 30s)"), 9);
        Assert.Equal(1.5, Script.Number("date.hours(90m)"), 9);
    }
}
