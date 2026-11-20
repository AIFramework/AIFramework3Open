using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Runs;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Effects;
using AI.Agents.Interop.Humans;
using Xunit;

namespace AI.Agents.Interop.Tests.Effects;

/// <summary>
/// Синхронный канал к человеку: вопрос инструмента (AwaitsUser) → ответ через IHumanChannel → один повторный вызов
/// с ответом. Опасность видна только по состоянию мира — инструмент сам спрашивает с видом действия.
/// </summary>
public sealed class HumanChannelTests
{
    private static readonly Question PayQuestion = new()
    {
        Id = "q-pay",
        Kind = QuestionKind.Confirm,
        Text = "Нажатие оформит оплату 990. Продолжить?",
        AskedBy = "run-42",
        Action = ToolAction.Irreversible,
        Target = "shop.example.org",
    };

    /// <summary>Кнопка в браузере: без ответа спрашивает, с согласием — нажимает.</summary>
    private static CountingTool Click() => new(
        Kit.Spec("click", ToolAction.Reversible) with { Idempotency = ToolIdempotency.NotIdempotent },
        (_, context) => context is AnsweredToolContext { Answer.Confirmed: true }
            ? CountingTool.Ok("оплачено")
            : new ToolResult
            {
                Status = ToolStatus.AwaitsUser,
                Text = "нужно согласие на оплату",
                Origin = new Origin { Kind = OriginKind.Trusted },
                Question = PayQuestion,
            });

    private static Task<List<ToolUpdate>> Call(IToolSet set) =>
        Kit.ReadAll(set.CallAsync(new ToolCall { Id = "c1", Name = "click", Arguments = Kit.Json("""{"q":"#pay"}""") },
            new ToolContext { RunId = "run-42", Path = new StepPath { Tier = 1 } }));

    [Fact]
    public async Task Confirmed_CallsAgainWithAnswer()
    {
        CountingTool tool = Click();
        var channel = new Channel(q => new Answer { QuestionId = q.Id, Confirmed = true });
        var gate = new EffectGate(new ListToolSet(tool));

        ToolResult result = Kit.Result(await Call(gate.WithHumanChannel(channel)));

        Assert.Equal("оплачено", result.Text);
        Assert.Equal(2, tool.Calls);
        Assert.Equal(PayQuestion, Assert.Single(channel.Asked));
        Assert.Equal(ToolStatus.Ok, Assert.Single(gate.Journal).Result.Status);
    }

    [Fact]
    public async Task Declined_IsDenied_WithoutSecondCall()
    {
        CountingTool tool = Click();
        var channel = new Channel(q => new Answer { QuestionId = q.Id, Confirmed = false });

        ToolResult result = Kit.Result(await Call(new EffectGate(new ListToolSet(tool)).WithHumanChannel(channel)));

        Assert.Equal(ToolStatus.Denied, result.Status);
        Assert.Equal(HumanChannelCodes.Declined, result.ErrorCode);
        Assert.Equal(1, tool.Calls);
    }

    [Fact]
    public async Task AnswerToOtherQuestion_Fails()
    {
        CountingTool tool = Click();
        var channel = new Channel(_ => new Answer { QuestionId = "другой", Confirmed = true });

        ToolResult result = Kit.Result(await Call(new ListToolSet(tool).WithHumanChannel(channel)));

        Assert.Equal(HumanChannelCodes.AnswerMismatch, result.ErrorCode);
        Assert.Equal(1, tool.Calls);
    }

    [Fact]
    public async Task OkResult_PassesWithoutQuestion()
    {
        var tool = new CountingTool(Kit.Spec("click"), (_, _) => CountingTool.Ok());
        var channel = new Channel(_ => throw new InvalidOperationException("не должен спрашивать"));

        Assert.Equal(ToolStatus.Ok, Kit.Result(await Call(new ListToolSet(tool).WithHumanChannel(channel))).Status);
    }

    private sealed class Channel(Func<Question, Answer> answer) : IHumanChannel
    {
        public List<Question> Asked { get; } = [];

        public Task<Answer> AskAsync(Question question, CancellationToken cancellationToken = default)
        {
            Asked.Add(question);
            return Task.FromResult(answer(question));
        }
    }
}
