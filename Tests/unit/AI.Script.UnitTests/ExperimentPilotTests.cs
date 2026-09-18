using AI.Script.Hosting;
using AI.Script.Llm;
using AI.Script.Semantics;
using AI.Script.Std;

namespace AI.Script.UnitTests;

/// <summary>
/// Пробный прогон опыта: одно испытание, оценка на весь план и остановка прогона.
/// </summary>
public sealed class ExperimentPilotTests
{
    private static RunOptions Pilot(ExperimentPilot pilot) =>
        new() { Network = NetworkPolicy.Allowed, Pilot = pilot };

    /// <summary>Два вызова модели на испытание, шесть испытаний в плане: оценка двенадцать вызовов.</summary>
    [Fact]
    public void Pilot_OneTrial_EstimatesWholePlan()
    {
        var llm = new FakeLlm("1", "1", "1") { Cost = 0.5m };
        var pilot = new ExperimentPilot();

        RunResult result = Script.RunWith(StandardLibrary.CreateHost().UseLlm(llm), """
            let prep = llm.ask("prepare")
            let outcomes = exp.grid({ v: ["a", "b"] })
                |> exp.run(p => { score: len(llm.ask("one")) + len(llm.ask("two")) }, repeat: 3)

            emit rows = len(outcomes)
            """, Pilot(pilot));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.PilotStopped);

        Assert.NotNull(pilot.Estimate);
        Assert.Equal(6, pilot.Estimate!.Trials);
        Assert.Equal(13, pilot.Estimate.Calls);
        Assert.Equal(6.5m, pilot.Estimate.Cost);
    }

    /// <summary>Пробное испытание в журнал не пишется: это не опыт.</summary>
    [Fact]
    public void Pilot_DoesNotWriteJournal()
    {
        var journal = new MemoryExperimentJournal();
        var options = Pilot(new ExperimentPilot());

        options.Journal = journal;

        _ = Script.RunWith(StandardLibrary.CreateHost(), """
            emit rows = len(exp.grid({ v: [1, 2] }) |> exp.run(p => { score: p.v }))
            """, options);

        Assert.Empty(journal.ReadAsync().GetAwaiter().GetResult());
    }

    /// <summary>До опыта скрипт не дошел: пробный прогон и есть итог, оценки нет.</summary>
    [Fact]
    public void Pilot_WithoutExperiment_RunsWholeScript()
    {
        var pilot = new ExperimentPilot();

        RunResult result = Script.RunWith(StandardLibrary.CreateHost(), "emit total = 2 + 3", Pilot(pilot));

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(5.0, result.Emitted["total"]);
        Assert.Null(pilot.Estimate);
    }
}
