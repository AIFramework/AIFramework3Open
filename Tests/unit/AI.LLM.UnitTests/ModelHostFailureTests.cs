using AI.LLM.Agents.ReAct;
using AI.LLM.Clients.Base;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Сбой хоста модели не выглядит ни успехом, ни «неверным форматом» и не теряет причину: цикл ReAct кончается
/// сбоем с исключением <see cref="LlmRequestException"/>, а не подсказками формата и <c>PolicyFailure</c>.
/// </summary>
public class ModelHostFailureTests
{
    [Fact]
    public async Task ReActEngine_ClientCrash_IsModelFailureWithCause()
    {
        var llm = new FakeLLMClient { BeforeSend = () => throw new InvalidOperationException("клиент модели упал") };
        ReActEngine engine = ReActAgentBuilder.Create()
            .WithNativeToolCalling(llm)
            .WithTool(new FakeReActTool("web_search"))
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        // Прежде сбой клиента считался неверным форматом: подсказки модели, затем PolicyFailure без причины.
        Assert.Equal(ReActStopReason.EngineFailure, result.StopReason);
        Assert.True(result.ModelFailed);
        Assert.IsType<InvalidOperationException>(result.Failure!.InnerException);
    }

    [Fact]
    public async Task ReActEngine_EmptyAnswerFromClient_StillGetsAnotherStep()
    {
        var llm = new FakeLLMClient();
        int call = 0;
        llm.BeforeSend = () => ++call == 1 ? throw new EmptyModelAnswerException() : Task.CompletedTask;
        llm.EnqueueText("ответ");
        ReActEngine engine = ReActAgentBuilder.Create()
            .WithNativeToolCalling(llm)
            .WithTool(new FakeReActTool("web_search"))
            .Build();

        ReActResult result = await engine.RunAsync("вопрос");

        // Молчание модели — не сбой хоста: цикл подсказывает и спрашивает снова.
        Assert.Equal(ReActStopReason.FinalAnswer, result.StopReason);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void FromClient_KeepsCause_AndDoesNotInviteRetry()
    {
        var cause = new InvalidOperationException("разбор потока");

        LlmRequestException error = LlmRequestException.FromClient(cause);

        Assert.Same(cause, error.InnerException);
        Assert.False(error.IsRetryable);
        Assert.Null(error.StatusCode);
        Assert.Same(error, LlmRequestException.FromClient(error));
    }
}
