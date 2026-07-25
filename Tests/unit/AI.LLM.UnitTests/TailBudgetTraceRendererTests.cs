using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Rendering;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Проверки рендерера следа. Это единственное место, где след укорачивается, и здесь же
/// проверяется главное его свойство: при нехватке места выживают САМЫЕ СВЕЖИЕ наблюдения.
/// Обратное поведение (обрезка «первые N символов») приводит к тому, что модель перестаёт
/// видеть результаты собственных последних действий и начинает их повторять.
/// </summary>
public class TailBudgetTraceRendererTests
{
    [Fact]
    public void TailBudgetTraceRenderer_Render_KeepsNewestObservationsWhenOverBudget()
    {
        ReActTrace trace = BuildTrace(20);
        var renderer = new TailBudgetTraceRenderer(maxObservationChars: 200, maxTotalChars: 400);

        string text = renderer.Render(trace);

        Assert.Contains("наблюдение-20", text);
        Assert.DoesNotContain("наблюдение-1 ", text);
        Assert.Contains("ранние шаги опущены", text);
    }

    [Fact]
    public void TailBudgetTraceRenderer_Render_TrimsEachObservationToLimit()
    {
        var trace = new ReActTrace();
        AddStep(trace, 1, new string('x', 5000));

        var renderer = new TailBudgetTraceRenderer(maxObservationChars: 100, maxTotalChars: 100000);

        string text = renderer.Render(trace);

        Assert.True(text.Length < 400, "наблюдение должно быть усечено до лимита");
        Assert.Contains("…", text);
    }

    [Fact]
    public void TailBudgetTraceRenderer_Render_KeepsLatestStepEvenWhenItAloneExceedsBudget()
    {
        var trace = new ReActTrace();
        AddStep(trace, 1, "старое наблюдение");
        AddStep(trace, 2, new string('y', 2000));

        var renderer = new TailBudgetTraceRenderer(maxObservationChars: 2000, maxTotalChars: 50);

        string text = renderer.Render(trace);

        // Остаться совсем без последнего наблюдения хуже, чем превысить лимит.
        Assert.Contains("yyy", text);
    }

    [Fact]
    public void TailBudgetTraceRenderer_Render_ReturnsEmptyForEmptyTrace() =>
        Assert.Equal(string.Empty, new TailBudgetTraceRenderer().Render(new ReActTrace()));

    private static ReActTrace BuildTrace(int steps)
    {
        var trace = new ReActTrace();
        for (int i = 1; i <= steps; i++)
            AddStep(trace, i, "наблюдение-" + i + " " + new string('z', 60));

        return trace;
    }

    private static void AddStep(ReActTrace trace, int number, string observation)
    {
        var action = new ReActAction("search", "запрос-" + number);
        trace.Add(new ReActStep
        {
            Number = number,
            Actions = [action],
            Observations = [new ReActObservation { Action = action, Ok = true, Text = observation }],
        });
    }
}
