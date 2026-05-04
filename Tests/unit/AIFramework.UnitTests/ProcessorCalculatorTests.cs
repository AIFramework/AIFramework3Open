using AI.ClassicMath.Calculator.ProcessorLogic;
using AIFramework.TestHelpers;
using Xunit;

namespace AIFramework.UnitTests;

public class ProcessorCalculatorTests
{
    [Theory]
    [InlineData("2 + 3", "5")]
    [InlineData("2 * 11", "22")]
    [InlineData("17 % 5", "2")]
    public void Run_LastExpression_MatchesExpected(string script, string expectedToken)
    {
        var processor = new Processor();
        var output = processor.Run(script);
        Assert.False(ProcessorOutputReader.HasCriticalError(output));
        var value = ProcessorOutputReader.GetLastExpressionDisplay(output);
        Assert.Contains(expectedToken, value);
    }

    [Fact]
    public void Run_ScriptWithVariable_ReturnsVariableValue()
    {
        var processor = new Processor();
        var value = ProcessorOutputReader.GetLastExpressionDisplay(processor, "x = 7; x * 2");
        Assert.Contains("14", value);
    }
}
