using System.Text.Json;
using System.Xml.Linq;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Runs;
using AI.Agents.Contracts.Tools;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Запуск как сущность (<see cref="RunState"/>, <see cref="IRunStore"/>, <see cref="IHumanChannel"/>) и ось
/// «повтор безопасен» (<see cref="ToolIdempotency"/>).
/// </summary>
public sealed class RunContractTests
{
    [Theory]
    [InlineData(ToolIdempotency.Unknown, ToolIdempotency.NotIdempotent)]
    [InlineData(ToolIdempotency.Idempotent, ToolIdempotency.Idempotent)]
    [InlineData(ToolIdempotency.WithKey, ToolIdempotency.WithKey)]
    [InlineData(ToolIdempotency.NotIdempotent, ToolIdempotency.NotIdempotent)]
    [InlineData((ToolIdempotency)42, ToolIdempotency.NotIdempotent)]
    public void Idempotency_Effective_TreatsUnknownAsNotRepeatable(ToolIdempotency declared, ToolIdempotency expected)
    {
        Assert.Equal(expected, declared.Effective());
        Assert.Equal(expected, ((ToolIdempotency?)declared).Effective());
    }

    [Fact]
    public void Idempotency_Null_IsNotRepeatable()
    {
        ToolIdempotency? missing = null;
        Assert.Equal(ToolIdempotency.NotIdempotent, missing.Effective());
    }

    [Fact]
    public void ToolSpec_WithoutIdempotencyField_ReadsAsNull()
    {
        var spec = JsonSerializer.Deserialize<ToolSpec>(
            """{"Name":"send","Description":"d","Parameters":{"type":"object"},"Action":"Irreversible"}""")!;

        Assert.Null(spec.Idempotency);
        Assert.Equal(ToolIdempotency.NotIdempotent, spec.Idempotency.Effective());
    }

    [Fact]
    public void ToolSpec_Idempotency_IsWrittenByName()
    {
        var spec = new ToolSpec
        {
            Name = "pay",
            Description = "Оплата",
            Parameters = JsonSerializer.SerializeToElement(new { type = "object" }),
            Action = ToolAction.Irreversible,
            Idempotency = ToolIdempotency.WithKey,
        };

        string json = JsonSerializer.Serialize(spec);
        Assert.Contains("\"Idempotency\":\"WithKey\"", json, StringComparison.Ordinal);
        Assert.Equal(ToolIdempotency.WithKey, JsonSerializer.Deserialize<ToolSpec>(json)!.Idempotency);
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("Agent")]
    [InlineData("Task")]
    [InlineData("Status")]
    [InlineData("Updated")]
    public void RunState_RequiredField_Missing_Throws(string field)
    {
        var all = new Dictionary<string, string>
        {
            ["Id"] = "\"run-1\"",
            ["Agent"] = """{"Id":"a"}""",
            ["Task"] = """{"Text":"t"}""",
            ["Status"] = "\"Running\"",
            ["Updated"] = "\"2026-11-20T10:00:00+00:00\"",
        };
        all.Remove(field);
        string json = "{" + string.Join(",", all.Select(p => $"\"{p.Key}\":{p.Value}")) + "}";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RunState>(json));
    }

    [Fact]
    public void RunStatus_HasLostAndUnknownZero()
    {
        Assert.Equal(0, (int)RunStatus.Unknown);
        Assert.Contains(RunStatus.Lost, Enum.GetValues<RunStatus>());
        Assert.Contains(RunStatus.AwaitsUser, Enum.GetValues<RunStatus>());
    }

    [Fact]
    public void RunStore_IsMinimalPort()
    {
        string[] methods = [.. typeof(IRunStore).GetMethods().Select(m => m.Name).Order(StringComparer.Ordinal)];
        Assert.Equal(["GetAsync", "ListAsync", "SaveAsync"], methods);
        Assert.Equal(typeof(IAsyncEnumerable<RunState>), typeof(IRunStore).GetMethod(nameof(IRunStore.ListAsync))!.ReturnType);
    }

    [Fact]
    public async Task HumanChannel_AnswersQuestionByItsId()
    {
        IHumanChannel channel = new EchoChannel();
        var question = new Question { Id = "q-7", Kind = QuestionKind.Confirm, Text = "Оплатить 100?", AskedBy = "run-1" };

        Answer answer = await channel.AskAsync(question);

        Assert.Equal("q-7", answer.QuestionId);
        Assert.True(answer.Confirmed);
        Assert.Equal(typeof(Task<Answer>), typeof(IHumanChannel).GetMethod(nameof(IHumanChannel.AskAsync))!.ReturnType);
    }

    /// <summary>
    /// Храповик слоев (решение владельца 20.11): стабильный пакет AI.LLM не ссылается на предварительные
    /// AI.Agents.Contracts и AI.Agents.Interop. Ссылку вернут к 1.0 контрактов или к шагу Ф5 отдельным решением.
    /// </summary>
    [Fact]
    public void AiLlm_DoesNotReferenceAgentPackages()
    {
        string path = Path.Combine(Repository.Root, "src", "AI.LLM", "AI.LLM.csproj");
        XDocument project = XDocument.Load(path);

        string[] offenders = [.. project.Descendants()
            .Where(e => e.Name.LocalName is "ProjectReference" or "PackageReference" or "Reference")
            .Select(e => (string?)e.Attribute("Include") ?? "")
            .Where(include => include.Contains("AI.Agents.", StringComparison.OrdinalIgnoreCase))];

        Assert.True(offenders.Length == 0,
            "src/AI.LLM/AI.LLM.csproj ссылается на " + string.Join(", ", offenders) + ". По решению владельца " +
            "(20.11) стабильный пакет AI.LLM не зависит от предварительных AI.Agents.Contracts и AI.Agents.Interop; " +
            "атрибут эффекта и адаптеры живут в AI.Agents.Interop.");
    }

    private sealed class EchoChannel : IHumanChannel
    {
        public Task<Answer> AskAsync(Question question, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Answer { QuestionId = question.Id, Confirmed = true });
    }
}
