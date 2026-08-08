using AI.LLM.Agents;
using AI.LLM.Agents.Orchestration;
using AI.LLM.Agents.Planning;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Приёмка шага плана. Цена ошибки здесь несимметрична: ложный провал гонит шаг на повтор, а
/// затем всю задачу на перепланирование, поэтому оба валидатора склонны принимать и придираются
/// только к явному.
/// </summary>
public class StepValidatorTests
{
    [Theory]
    [InlineData("Error: сервис недоступен")]
    [InlineData("Не удалось открыть файл")]
    [InlineData("Timeout while connecting")]
    [InlineData("")]
    [InlineData("   ")]
    public void DefaultStepValidator_IsSuccess_RejectsShortFailureReport(string answer)
    {
        var validator = new DefaultStepValidator();

        Assert.False(validator.IsSuccess(Step(), Result(answer)));
    }

    [Fact]
    public void DefaultStepValidator_IsSuccess_AcceptsLongAnswerMentioningFailureWordInside()
    {
        // Готовая работа, где слово-маркер встретилось по смыслу. Прежняя версия искала маркеры
        // по всему тексту и объявляла такой ответ провалом.
        var essay = "Рассуждение о пределах автоматизации. " + new string('я', 600)
                    + " Существуют задачи, которые cannot be solved перебором, и это принципиально. "
                    + new string('я', 600);

        Assert.True(new DefaultStepValidator().IsSuccess(Step(), Result(essay)));
    }

    [Fact]
    public void DefaultStepValidator_IsSuccess_RejectsLongAnswerThatOpensWithFailure()
    {
        var answer = "Не удалось выполнить шаг: инструмент вернул ошибку.\n"
                     + "Ниже подробности того, что было предпринято.\n" + new string('я', 1000);

        Assert.False(new DefaultStepValidator().IsSuccess(Step(), Result(answer)));
    }

    [Fact]
    public void DefaultStepValidator_IsSuccess_HonoursCustomMarkers()
    {
        var validator = new DefaultStepValidator(failureMarkers: ["отказано"]);

        Assert.False(validator.IsSuccess(Step(), Result("Отказано в доступе")));

        // Стандартные маркеры заменены, а не дополнены.
        Assert.True(validator.IsSuccess(Step(), Result("Error: что-то пошло не так")));
    }

    [Fact]
    public async Task LlmStepValidator_IsSuccessAsync_ChecksDoneWhenCriterion()
    {
        var llm = new FakeLLMClient().EnqueueText("НЕТ");
        var validator = new LlmStepValidator(llm);

        var step = Step(doneWhen: "в ответе есть готовый текст эссе не короче 2000 знаков");
        bool ok = await validator.IsSuccessAsync(step, Result("Приступаю к написанию эссе."));

        Assert.False(ok);

        // Критерий шага обязан дойти до модели — ради него он и генерировался планировщиком.
        Assert.Contains("не короче 2000 знаков", llm.SentMessages[0][^1].Content!.ToString());
        Assert.Contains("Приступаю к написанию", llm.SentMessages[0][^1].Content!.ToString());
    }

    [Fact]
    public async Task LlmStepValidator_IsSuccessAsync_AcceptsWhenModelConfirms()
    {
        var validator = new LlmStepValidator(new FakeLLMClient().EnqueueText("ДА"));

        Assert.True(await validator.IsSuccessAsync(Step(doneWhen: "есть текст"), Result("вот текст")));
    }

    [Fact]
    public async Task LlmStepValidator_IsSuccessAsync_FallsBackToDescriptionWithoutDoneWhen()
    {
        var llm = new FakeLLMClient().EnqueueText("ДА");
        var step = new PlanStep { Id = "step_0", Description = "написать эссе", DoneWhen = "" };

        await new LlmStepValidator(llm).IsSuccessAsync(step, Result("вот эссе"));

        Assert.Contains("написать эссе", llm.SentMessages[0][^1].Content!.ToString());
    }

    [Fact]
    public async Task LlmStepValidator_IsSuccessAsync_AcceptsStepWhenCheckItselfFails()
    {
        var llm = new FakeLLMClient { BeforeSend = () => throw new InvalidOperationException("сеть лежит") };
        llm.EnqueueText("НЕТ");

        // Сломанная приёмка не должна сама становиться источником провалов.
        Assert.True(await new LlmStepValidator(llm).IsSuccessAsync(Step(doneWhen: "есть текст"), Result("ответ")));
    }

    [Fact]
    public async Task LlmStepValidator_IsSuccessAsync_RejectsEmptyAnswerWithoutAskingModel()
    {
        var llm = new FakeLLMClient();

        Assert.False(await new LlmStepValidator(llm).IsSuccessAsync(Step(doneWhen: "есть текст"), Result("  ")));
        Assert.Empty(llm.SentMessages);
    }

    private static PlanStep Step(string doneWhen = "результат получен") =>
        new() { Id = "step_0", Description = "сделать дело", DoneWhen = doneWhen };

    private static AgentResult Result(string answer) =>
        new(answer, [], TimeSpan.Zero, new AgentUsage());
}
