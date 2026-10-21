using AI.Script.Hosting;
using AI.Script.Runtime;

namespace AI.Script.UnitTests;

/// <summary>
/// Отладчик прогона: точки останова, переменные кадра, контрольные значения, шаги с заходом,
/// обходом и выходом, остановка на ошибке и параллельный участок без взаимной блокировки.
/// </summary>
public class DebuggerTests
{
    private const string Sum = """
        fn square(x: num) -> num {
            let y = x * x
            return y
        }
        let total = 0
        for v in [1, 2, 3] {
            set total = total + square(v)
        }
        emit total = total
        """;

    [Fact]
    public void Breakpoint_StopsWithLocalsAndWatch()
    {
        var session = new DebugSession();
        session.SetBreakpoints([3]);
        var seen = new List<string>();
        session.Paused += (context, reason) =>
        {
            Dictionary<string, ScriptValue> locals = context.Locals().ToDictionary(p => p.Key, p => p.Value);
            seen.Add($"{reason}:{context.Line}:{context.Frame.Name}:{ScriptFormatter.Format(locals["y"])}:{ScriptFormatter.Format(context.Evaluate("y + 1"))}");
            Assert.Equal(["square", "скрипт"], context.Frame.Stack().Select(f => f.Name));
            Assert.True(locals.ContainsKey("total"));
            Assert.False(locals.ContainsKey("pi"));
            Assert.False(locals.ContainsKey("square"));
            session.Continue();
        };

        RunResult result = Script.Run(Sum, new RunOptions { Debugger = session });

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(["Breakpoint:3:square:1:2", "Breakpoint:3:square:4:5", "Breakpoint:3:square:9:10"], seen);
    }

    [Fact]
    public void Steps_IntoOverAndOutFollowCallDepth()
    {
        var session = new DebugSession();
        session.SetBreakpoints([7]);
        var lines = new List<int>();
        var commands = new Queue<Action>([session.StepInto, session.StepOver, session.StepOut, session.StepOver]);
        session.Paused += (context, reason) =>
        {
            lines.Add(context.Line);
            session.SetBreakpoints([]);
            if (commands.Count > 0) commands.Dequeue()();
            else session.Continue();
        };

        RunResult result = Script.Run(Sum, new RunOptions { Debugger = session });

        Assert.True(result.Success, Script.Report(result));
        // 7 — в цикле; с заходом — 2 в square; с обходом — 3; с выходом — 7 второго витка (сам for — одна инструкция);
        // с обходом — 7 третьего витка: вызов square проходится целиком.
        Assert.Equal([7, 2, 3, 7, 7], lines);
    }

    [Fact]
    public void Error_StopsAtThrowSiteWithItsLocals()
    {
        var session = new DebugSession();
        string? where = null;
        session.Paused += (context, reason) =>
        {
            where = $"{reason}:{context.Line}:{ScriptFormatter.Format(context.Evaluate("n"))}:{context.Error?.Message}";
            session.Continue();
        };

        RunResult result = Script.Run("let n = 3\nlet xs = [1, 2]\nemit bad = xs[n]", new RunOptions { Debugger = session });

        Assert.False(result.Success);
        Assert.NotNull(where);
        Assert.StartsWith("Error:3:3:", where);
    }

    [Fact]
    public void Watch_MustBeOneExpression()
    {
        var session = new DebugSession();
        session.SetBreakpoints([2]);
        ScriptError? error = null;
        session.Paused += (context, reason) =>
        {
            error = Assert.Throws<ScriptError>(() => context.Evaluate("let z = 1"));
            session.Continue();
        };

        RunResult result = Script.Run("let a = 1\nemit a = a", new RunOptions { Debugger = session });

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(Semantics.DiagnosticCodes.WatchNotExpression, error?.Code);
    }

    [Fact]
    public void ParallelMap_BreakpointInLambdaStopsEveryBranch()
    {
        var session = new DebugSession();
        session.SetBreakpoints([1]);
        var branches = new List<int?>();
        session.Paused += (context, reason) =>
        {
            lock (branches) branches.Add(context.Branch);
            session.Continue();
        };

        RunResult result = Script.Run(
            "let squares = [1, 2, 3, 4, 5, 6] |> core.map(x => x * x, parallel: true)\nemit s = squares",
            new RunOptions { Debugger = session, Parallelism = 4 });

        Assert.True(result.Success, Script.Report(result));
        // Первая остановка — сама инструкция (строка 1), дальше — лямбда в каждой из шести ветвей.
        Assert.Equal(7, branches.Count);
        Assert.Equal(6, branches.Where(b => b != null).Distinct().Count());
    }

    [Fact]
    public void Stop_WhilePausedCancelsRun()
    {
        var session = new DebugSession();
        session.SetBreakpoints([2]);
        using var stop = new CancellationTokenSource();
        session.Paused += (context, reason) => stop.Cancel();

        RunResult result = Script.Host().RunAsync("let a = 1\nlet b = 2\nemit b = b", new RunOptions { Debugger = session }, stop.Token).GetAwaiter().GetResult();

        Assert.False(result.Success);
        Assert.Null(session.Current);
    }
}
