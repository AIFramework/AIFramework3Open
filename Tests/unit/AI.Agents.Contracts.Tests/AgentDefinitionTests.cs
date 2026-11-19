using System.Text.Json;
using System.Text.Json.Nodes;
using AI.Agents.Contracts.Engines;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Запись агента: круг через JSON, правило «ключ промпта или текст», движок по умолчанию, ссылка на версию.
/// </summary>
public sealed class AgentDefinitionTests
{
    /// <summary>Полная и минимальная записи проходят круг через JSON без потерь.</summary>
    [Fact]
    public void FullAndMinimal_SurviveRoundTrip()
    {
        foreach (AgentDefinition agent in new[] { ContractSamples.FullAgent(), ContractSamples.MinimalAgent() })
        {
            foreach (JsonSerializerOptions options in new[] { JsonSerializerOptions.Default, JsonSerializerOptions.Web })
            {
                string json = JsonSerializer.Serialize(agent, options);
                AgentDefinition back = JsonSerializer.Deserialize<AgentDefinition>(json, options)!;

                Assert.Empty(ValueComparison.Differences(agent, back));
                Assert.Equal(json, JsonSerializer.Serialize(back, options));
            }
        }
    }

    /// <summary>Engine = null проходит круг как null: движок по умолчанию у хоста, без подстановки.</summary>
    [Fact]
    public void Engine_Null_SurvivesRoundTripAsNull()
    {
        AgentDefinition agent = ContractSamples.MinimalAgent();

        string json = JsonSerializer.Serialize(agent);
        JsonObject node = JsonNode.Parse(json)!.AsObject();

        Assert.True(node.ContainsKey(nameof(AgentDefinition.Engine)));
        Assert.Null(node[nameof(AgentDefinition.Engine)]);
        Assert.Null(JsonSerializer.Deserialize<AgentDefinition>(json)!.Engine);
        Assert.Null(JsonSerializer.Deserialize<AgentDefinition>(Minimal(""))!.Engine);
    }

    /// <summary>Ключ и текст вместе отвергаются в любом порядке задания и при чтении, с понятным сообщением.</summary>
    [Fact]
    public void Instructions_Both_AreRejected()
    {
        var keyFirst = Assert.Throws<ArgumentException>(() => new AgentInstructions { Key = "k", Text = "t" });
        var textFirst = Assert.Throws<ArgumentException>(() => new AgentInstructions { Text = "t", Key = "k" });
        var json = Assert.ThrowsAny<Exception>(() =>
            JsonSerializer.Deserialize<AgentDefinition>(Minimal("", """{"Key":"k","Text":"t"}""")));
        Assert.Throws<ArgumentException>(() => AgentInstructions.FromKey("k") with { Text = "t" });

        foreach (Exception exception in new Exception[] { keyFirst, textFirst, json })
            Assert.Contains("ровно одно из двух", exception.Message);
    }

    /// <summary>Ни ключа, ни текста — отказ при создании записи и при чтении, с понятным сообщением.</summary>
    [Fact]
    public void Instructions_None_AreRejected()
    {
        var inCode = Assert.Throws<ArgumentException>(() => ContractSamples.MinimalAgent() with { Instructions = new AgentInstructions() });
        var json = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDefinition>(Minimal("", "{}")));
        var nested = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentInstructions>("""{"Key":null,"Text":null}"""));

        foreach (Exception exception in new Exception[] { inCode, json, nested })
            Assert.Contains("не задано ни одного", exception.Message);

        Assert.Throws<ArgumentNullException>(() => ContractSamples.MinimalAgent() with { Instructions = null! });
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentDefinition>(
            """{"Id":"a","Version":1,"Name":"n","Role":"r"}"""));
    }

    /// <summary>Пустой ключ или текст не считается значением.</summary>
    [Fact]
    public void Instructions_Blank_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => AgentInstructions.FromKey(" "));
        Assert.Throws<ArgumentException>(() => AgentInstructions.FromText(""));
        Assert.Throws<ArgumentException>(() => new AgentInstructions { Text = " " });
    }

    /// <summary>Версия с 1, ключ движка непустой, потолки положительные.</summary>
    [Fact]
    public void InvalidParts_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ContractSamples.MinimalAgent() with { Version = 0 });
        Assert.Throws<ArgumentException>(() => new EngineChoice { Key = " " });
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentLimits { Steps = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentLimits { Cost = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new AgentLimits { Duration = TimeSpan.Zero });
    }

    /// <summary>Ссылка из записи несет ее идентификатор и версию.</summary>
    [Fact]
    public void ToRef_PinsIdAndVersion()
    {
        AgentRef reference = ContractSamples.FullAgent().ToRef();

        Assert.Equal(new AgentRef { Id = "contract-analyst", Version = 4 }, reference);
    }

    /// <summary>
    /// Агент — данные: интерфейса агента в контрактах нет, а в записи нет полей хоста (исполнитель хода,
    /// хранилище, публикация, тариф, плательщик) и отдельного поля примеров (они в About.Examples).
    /// </summary>
    [Fact]
    public void Agent_IsDataOnly()
    {
        Assert.DoesNotContain(Repository.Contracts.GetExportedTypes(), type => type.IsInterface && type.Name == "IAgent");

        string[] properties = typeof(AgentDefinition).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(
            ["Id", "Version", "Name", "Role", "About", "Tools", "Engine", "Model", "Limits", "Instructions", "Inputs", "Agents", "Output", "Acceptance"],
            properties);
        Assert.DoesNotContain(typeof(AgentDefinition).GetProperties(), property => property.PropertyType.IsInterface
            && property.PropertyType.Assembly == Repository.Contracts);
    }

    private static string Minimal(string extra, string instructions = """{"Text":"t"}""") =>
        $$"""{"Id":"a","Version":1,"Name":"n","Role":"r","Instructions":{{instructions}}{{extra}}}""";
}
