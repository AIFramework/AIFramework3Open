using System.Globalization;
using System.Text.Json;
using AI.LLM.Core.Models.Common.Responses;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Стоимость не должна зависеть от культуры процесса: под ru-RU дробь пишется через запятую, и
/// разбор строки текущей культуры инвариантным превращал 0,5 в 5, а 0,000123 в 123.
/// </summary>
public class CostExtractorTests
{
    public static TheoryData<object, decimal> Costs => new()
    {
        { 0.5m, 0.5m },
        { 0.000123m, 0.000123m },
        { 0.25d, 0.25m },
        { 0.125f, 0.125m },
        { 3, 3m },
        { 7L, 7m },
        { "0.5", 0.5m },
        { Json("0.000123"), 0.000123m },
        { Json("\"0.5\""), 0.5m },
        { Json("{\"total_cost\": 0.002}"), 0.002m },
    };

    [Theory]
    [MemberData(nameof(Costs))]
    public void TryExtract_UnderRussianCulture_KeepsValue(object cost, decimal expected) =>
        UnderCulture("ru-RU", () => Assert.Equal(expected, CostExtractor.TryExtract(cost)));

    [Theory]
    [MemberData(nameof(Costs))]
    public void TryExtract_UnderInvariantCulture_KeepsValue(object cost, decimal expected) =>
        UnderCulture("", () => Assert.Equal(expected, CostExtractor.TryExtract(cost)));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TryExtract_NonFiniteDouble_IsNull(double cost) =>
        Assert.Null(CostExtractor.TryExtract(cost));

    [Fact]
    public void TryExtract_Null_IsNull() =>
        Assert.Null(CostExtractor.TryExtract(null!));

    private static JsonElement Json(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static void UnderCulture(string name, Action check)
    {
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(name);
            check();
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
