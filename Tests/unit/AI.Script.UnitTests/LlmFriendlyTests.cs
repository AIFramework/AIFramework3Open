using AI.Script.Hosting;

namespace AI.Script.UnitTests;

/// <summary>
/// То, как пишут скрипт модели и люди из других языков, работает как ожидается: необязательные
/// аргументы позиционно, математика над вектором поэлементно, числовой список рядом с вектором — вектор.
/// </summary>
public class LlmFriendlyTests
{
    [Theory]
    [InlineData("emit r = core.round(3.14159, 2)", 3.14)]
    [InlineData("emit r = core.round(3.14159, digits: 2)", 3.14)]
    [InlineData("emit r = math.log(8, 2)", 3)]
    public void OptionalParameters_CanBePositional(string source, double expected)
    {
        RunResult result = Script.RunOk(source);

        Assert.Equal(expected, (double)result.Emitted["r"]!, 6);
    }

    [Fact]
    public void OptionalStringParameters_CanBePositional()
    {
        Assert.Equal("3.5", Script.Text("""str.replace("3,5", ",", ".")"""));
    }

    [Fact]
    public void TooManyPositional_StillReported()
    {
        Assert.Equal(Semantics.DiagnosticCodes.ExtraPositional, Script.CheckFailsWith("emit r = math.sqrt(4, 5, 6)").Code);
    }

    [Theory]
    [InlineData("math.sqrt(<1, 4, 9>)", new[] { 1.0, 2, 3 })]
    [InlineData("math.exp(<0, 0>)", new[] { 1.0, 1 })]
    [InlineData("core.round(<1.234, 5.678>, 1)", new[] { 1.2, 5.7 })]
    [InlineData("math.abs([-1, 2, -3])", new[] { 1.0, 2, 3 })]
    [InlineData("math.sqrt(<1, 4>) + <1, 1>", new[] { 2.0, 3 })]
    public void NumberFunctions_ApplyElementwiseToVectors(string expression, double[] expected)
    {
        RunResult result = Script.RunOk($"emit r = {expression}");

        Assert.Equal(expected, ((AI.DataStructs.Algebraic.Vector)result.Emitted["r"]!).ToArray());
    }

    [Fact]
    public void ElementwiseCall_IsTypedAsVector()
    {
        // Результат — vec: дальше работают векторные функции, и проверка это видит до запуска.
        RunResult result = Script.RunOk("let v = math.sqrt(<4, 16>)\nemit s = vec.sum(v)");

        Assert.Equal(6.0, (double)result.Emitted["s"]!, 9);
    }

    [Theory]
    [InlineData("<1, 2, 3> |> core.map(x => x * 2)", "* 2", new[] { 4.0, 8, 12 })]
    [InlineData("<1, 2, 3> |> core.map(x => x * 2)", "+ <1, 1, 1>", new[] { 3.0, 5, 7 })]
    [InlineData("[1, 2, 3]", "- 1", new[] { 0.0, 1, 2 })]
    public void NumericList_NextToVectorOrNumber_IsVector(string list, string tail, double[] expected)
    {
        RunResult result = Script.RunOk($"let xs = {list}\nemit r = xs {tail}");

        Assert.Equal(expected, ((AI.DataStructs.Algebraic.Vector)result.Emitted["r"]!).ToArray());
    }

    [Fact]
    public void ListConcatenation_IsUnchanged()
    {
        RunResult result = Script.RunOk("emit r = [1, 2] + [3]");

        Assert.Equal(3, ((System.Collections.IEnumerable)result.Emitted["r"]!).Cast<object>().Count());
    }
}
