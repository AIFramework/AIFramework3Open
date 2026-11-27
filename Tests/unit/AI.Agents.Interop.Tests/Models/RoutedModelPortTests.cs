using AI.Agents.Contracts.Models;
using AI.Agents.Interop.Models;
using Xunit;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>
/// Порт по ролям с запасными моделями: роль знает основную модель и запасные, переход — одно правило
/// <see cref="ModelFallback"/>, итог говорит, какая модель ответила. Имена моделей синтетические.
/// </summary>
public sealed class RoutedModelPortTests
{
    private static LlmAnswer Failed(LlmFailure failure, int tokens = 10) => new()
    {
        Status = LlmStatus.Failed,
        Failure = failure,
        FailureText = "сбой " + failure,
        Usage = new ModelUsage { InputTokens = tokens, OutputTokens = 0, Cost = 0.01m },
    };

    private static LlmAnswer Ok(string text) => new()
    {
        Status = LlmStatus.Ok,
        Text = text,
        Usage = new ModelUsage { InputTokens = 5, OutputTokens = 7, Cost = 0.02m },
    };

    private static IModelPort Routed(ModelRole role, params ModelSlot[] chain) =>
        ModelPort.Routed(new Dictionary<ModelRole, IReadOnlyList<ModelSlot>> { [role] = chain });

    [Fact]
    public async Task Ask_RateLimitedPrimary_FallbackAnswers_AndSaysWhichModel()
    {
        var primary = new FakePort { Answer = Failed(LlmFailure.RateLimit) };
        var backup = new FakePort { Answer = Ok("ответ запасной") };
        IModelPort port = Routed(ModelRole.Decide, new("model-primary", primary), new("model-backup", backup));

        LlmAnswer answer = await port.AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "вопрос")));

        Assert.Equal(LlmStatus.Ok, answer.Status);
        Assert.Equal("ответ запасной", answer.Text);
        Assert.Equal("model-backup", answer.Model);
        Assert.Equal(15, answer.Usage!.InputTokens);
        Assert.Equal(0.03m, answer.Usage.Cost);
        Assert.Single(primary.Asks);
        Assert.Single(backup.Asks);
    }

    [Fact]
    public async Task Ask_ContextOverflow_DoesNotFallBack()
    {
        var backup = new FakePort { Answer = Ok("не должен") };
        IModelPort port = Routed(ModelRole.Decide,
            new("model-primary", new FakePort { Answer = Failed(LlmFailure.ContextOverflow) }), new("model-backup", backup));

        LlmAnswer answer = await port.AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "вопрос")));

        Assert.Equal(LlmFailure.ContextOverflow, answer.Failure);
        Assert.Equal("model-primary", answer.Model);
        Assert.Empty(backup.Asks);
    }

    [Fact]
    public async Task Ask_WholeChainFails_LastFailureWithItsModel()
    {
        IModelPort port = Routed(ModelRole.Decide,
            new("model-primary", new FakePort { Answer = Failed(LlmFailure.Network) }),
            new("model-backup", new FakePort { Answer = Failed(LlmFailure.Access, tokens: 3) }));

        LlmAnswer answer = await port.AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "вопрос")));

        Assert.Equal(LlmStatus.Failed, answer.Status);
        Assert.Equal(LlmFailure.Access, answer.Failure);
        Assert.Equal("model-backup", answer.Model);
        Assert.Equal(13, answer.Usage!.InputTokens);
    }

    [Fact]
    public async Task Stream_FallsBackOnlyBeforeFirstFrame()
    {
        var shownThenFailed = new FakePort { Answer = Failed(LlmFailure.Stalled) };
        shownThenFailed.Frames.Add(new LlmChunk.Text { Chunk = "нача" });
        var backup = new FakePort { Answer = Ok("не должен") };
        IModelPort port = Routed(ModelRole.Write, new("model-primary", shownThenFailed), new("model-backup", backup));

        List<LlmChunk> frames = await Kit.ReadAll(port.StreamAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "вопрос")) with { Role = ModelRole.Write }));

        // Начатый показ не переписывается чужим ответом: итог прежней модели окончательный.
        LlmChunk.Final final = Assert.IsType<LlmChunk.Final>(frames[^1]);
        Assert.Equal(LlmFailure.Stalled, final.Answer.Failure);
        Assert.Empty(backup.Asks);
    }

    [Fact]
    public async Task Stream_SilentFailure_FallsBack()
    {
        var backup = new FakePort { Answer = Ok("ответ") };
        IModelPort port = Routed(ModelRole.Decide,
            new("model-primary", new FakePort { Answer = Failed(LlmFailure.ProviderRefusal) }), new("model-backup", backup));

        List<LlmChunk> frames = await Kit.ReadAll(port.StreamAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "вопрос"))));

        LlmChunk.Final final = Assert.IsType<LlmChunk.Final>(Assert.Single(frames));
        Assert.Equal("model-backup", final.Answer.Model);
    }

    [Fact]
    public async Task UnknownRole_IsRefused_NotDefaulted()
    {
        IModelPort port = Routed(ModelRole.Decide, new ModelSlot("model-primary", new FakePort()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            port.AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.User, "вопрос")) with { Role = ModelRole.Judge }));
        Assert.Throws<ArgumentException>(() => port.Describe(ModelRole.Judge));
    }

    [Fact]
    public void Fallback_UnknownFailureCode_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ModelFallback.Cures(LlmFailure.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => ModelFallback.Cures((LlmFailure)999));
        Assert.False(ModelFallback.TryNext(new LlmAnswer { Status = LlmStatus.Empty }));
    }

    [Fact]
    public void Fallback_EveryKnownCode_HasADecision()
    {
        // Новое значение LlmFailure без решения здесь уронит этот тест, а не пройдет молча.
        foreach (LlmFailure failure in Enum.GetValues<LlmFailure>().Where(f => f != LlmFailure.Unknown))
            _ = ModelFallback.Cures(failure);
    }

    [Fact]
    public void Routed_EmptyChainOrNoRoles_Refused()
    {
        Assert.Throws<ArgumentException>(() => ModelPort.Routed(new Dictionary<ModelRole, IReadOnlyList<ModelSlot>>()));
        Assert.Throws<ArgumentException>(() => Routed(ModelRole.Decide));
        Assert.Throws<ArgumentException>(() => new ModelSlot(" ", new FakePort()));
    }
}
