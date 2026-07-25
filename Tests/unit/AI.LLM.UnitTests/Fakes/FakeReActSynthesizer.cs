using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Synthesis;

namespace AI.LLM.UnitTests.Fakes;

/// <summary>Синтез с заданным ответом; запоминает контекст, с которым его вызвали.</summary>
internal sealed class FakeReActSynthesizer : IReActSynthesizer
{
    private readonly string _answer;

    public FakeReActSynthesizer(string answer = "итоговый ответ") => _answer = answer;

    /// <summary>Контексты состоявшихся вызовов.</summary>
    public List<ReActSynthesisContext> Calls { get; } = [];

    public async IAsyncEnumerable<ReActTextChunk> SynthesizeAsync(
        ReActSynthesisContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Calls.Add(context);
        await Task.Yield();
        yield return new ReActTextChunk(_answer, null);
    }
}
