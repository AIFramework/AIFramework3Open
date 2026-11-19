using System.Runtime.CompilerServices;
using System.Text.Json;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Инструмент: незаданный вид действия, проверка имени и схемы, описание из одной строки.
/// </summary>
public sealed class ToolContractTests
{
    private static readonly JsonElement EmptySchema = ContractSamples.Json("""{"type":"object","properties":{}}""");

    private static readonly ToolCall AnyCall = new() { Id = "c", Name = "t", Arguments = ContractSamples.Json("{}") };

    /// <summary>Незаданный вид действия — не «чтение».</summary>
    [Fact]
    public void ToolAction_Default_IsUnknownNotRead()
    {
        Assert.NotEqual(ToolAction.Read, default);
        Assert.Equal(ToolAction.Unknown, default(ToolAction));
    }

    /// <summary>Unknown напрямую и через помощник трактуется как Irreversible.</summary>
    [Fact]
    public void Effective_Unknown_IsIrreversible()
    {
        Assert.Equal(ToolAction.Irreversible, ToolAction.Unknown.Effective());
        Assert.Equal(ToolAction.Irreversible, default(ToolAction).Effective());
        Assert.Equal(ToolAction.Irreversible, ToolActions.Effective(ToolAction.Unknown));
    }

    /// <summary>Необъявленное число тоже считается необратимым; объявленные виды не меняются.</summary>
    [Fact]
    public void Effective_KeepsDeclaredAndHardensUndeclared()
    {
        Assert.Equal(ToolAction.Irreversible, ((ToolAction)42).Effective());
        Assert.Equal(ToolAction.Read, ToolAction.Read.Effective());
        Assert.Equal(ToolAction.Compute, ToolAction.Compute.Effective());
        Assert.Equal(ToolAction.Reversible, ToolAction.Reversible.Effective());
        Assert.Equal(ToolAction.Irreversible, ToolAction.Irreversible.Effective());
    }

    /// <summary>Classify по умолчанию отдает объявленный вид; для Unknown гейт получает Irreversible.</summary>
    [Fact]
    public void Classify_Default_ReturnsSpecActionAndUnknownBecomesIrreversible()
    {
        ITool unknown = new FixedTool(ToolAction.Unknown);
        ITool read = new FixedTool(ToolAction.Read);

        Assert.Equal(ToolAction.Unknown, unknown.Classify(AnyCall));
        Assert.Equal(ToolAction.Irreversible, unknown.Classify(AnyCall).Effective());
        Assert.Equal(ToolAction.Read, read.Classify(AnyCall));
        Assert.Equal(ToolAction.Read, read.Classify(AnyCall).Effective());
    }

    /// <summary>ToolSpec с неверным именем не создается — ни в коде, ни при чтении JSON.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("dot.name")]
    [InlineData("кириллица")]
    [InlineData("slash/name")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345")]
    public void ToolSpec_InvalidName_IsRejected(string name)
    {
        Assert.False(ToolSpec.IsValidName(name));
        Assert.Throws<ArgumentException>(() => Spec(name));

        string json = $$"""{"Name":{{JsonSerializer.Serialize(name)}},"Description":"d","Parameters":{"type":"object"},"Action":"Read"}""";
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<ToolSpec>(json));
    }

    /// <summary>Допустимые имена проходят.</summary>
    [Theory]
    [InlineData("a")]
    [InlineData("get_weather-2")]
    [InlineData("A_B")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234")]
    public void ToolSpec_ValidName_IsAccepted(string name)
    {
        Assert.True(ToolSpec.IsValidName(name));
        Assert.Equal(name, Spec(name).Name);
    }

    /// <summary>Имена сравниваются с учетом регистра.</summary>
    [Fact]
    public void ToolSpec_NameComparer_IsCaseSensitive()
    {
        Assert.False(ToolSpec.NameComparer.Equals("get_rate", "GET_RATE"));
        Assert.True(ToolSpec.NameComparer.Equals("get_rate", "get_rate"));
    }

    /// <summary>Шаблон имени в константе совпадает с проверкой.</summary>
    [Fact]
    public void ToolSpec_NamePattern_MatchesCheck()
    {
        var pattern = new System.Text.RegularExpressions.Regex(ToolSpec.NamePattern);

        foreach (string name in new[] { "a", "a-b_c", "", "a b", "ы", new string('x', 64), new string('x', 65) })
            Assert.Equal(pattern.IsMatch(name), ToolSpec.IsValidName(name));
    }

    /// <summary>Схема параметров обязана быть объектом: незаданный элемент и массив отвергаются.</summary>
    [Fact]
    public void ToolSpec_NonObjectSchema_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ToolSpec
        {
            Name = "t", Description = "d", Parameters = default, Action = ToolAction.Read,
        });

        Assert.Throws<ArgumentException>(() => new ToolSpec
        {
            Name = "t", Description = "d", Parameters = ContractSamples.Json("[]"), Action = ToolAction.Read,
        });
    }

    /// <summary>Описание из одной строки заполняет только назначение.</summary>
    [Fact]
    public void About_FromText_FillsDoesOnly()
    {
        About about = About.FromText("  Ищет курс валют  ");

        Assert.Equal("Ищет курс валют", about.Does);
        Assert.Null(about.DoesNot);
        Assert.Null(about.Rules);
        Assert.Null(about.Examples);
        Assert.Null(about.Guardrails);
        Assert.Throws<ArgumentException>(() => About.FromText(" "));
    }

    private static ToolSpec Spec(string name) => new()
    {
        Name = name,
        Description = "d",
        Parameters = EmptySchema,
        Action = ToolAction.Read,
    };

    private sealed class FixedTool(ToolAction action) : ITool
    {
        public ToolSpec Spec { get; } = new()
        {
            Name = "t",
            Description = "Синтетический инструмент",
            Parameters = EmptySchema,
            Action = action,
        };

        public async IAsyncEnumerable<ToolUpdate> CallAsync(
            ToolCall call, ToolContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ToolUpdate.Final
            {
                Result = new ToolResult { Status = ToolStatus.Ok, Text = "ok", Origin = new Origin { Kind = OriginKind.Trusted } },
            };
        }
    }
}
