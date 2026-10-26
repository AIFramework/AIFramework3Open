using AI.DataStructs.Algebraic;
using AI.LLM.Services.Reranking.Llm;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>Реранкер на чат-модели: модель называет номера подходящих документов, оценка — по месту в её ответе.</summary>
public class LlmRerankerTests
{
    [Fact]
    public async Task LlmReranker_SimsAsync_ScoresByPlaceInAnswerAndIgnoresJunk()
    {
        var llm = new FakeLLMClient().EnqueueText("3, 1, 3, 9 — остальные не о том");
        var reranker = new LlmReranker(llm) { MaxDocumentChars = 10 };

        Vector sims = await reranker.SimsAsync("где считается скидка", ["цена со скидкой", "чтение файла", "Discount(total) => total * 0.9m — длинный текст"]);

        Assert.Equal(1.0, sims[2]);
        Assert.Equal(1.0 - 1.0 / 3, sims[0], 6);
        Assert.Equal(0.0, sims[1]);
        string prompt = llm.SentPrompts.Single();
        Assert.Contains("Вопрос: где считается скидка", prompt);
        Assert.Contains("[3]\nDiscount(t…", prompt);
    }

    [Fact]
    public async Task LlmReranker_TopKSimsAsync_ReturnsNamedFirst()
    {
        var llm = new FakeLLMClient().EnqueueText("2");
        var reranker = new LlmReranker(llm);

        List<(int, double)> top = await reranker.TopKSimsAsync("вопрос", ["а", "б", "в"], 1);

        Assert.Equal(1, Assert.Single(top).Item1);
    }
}
