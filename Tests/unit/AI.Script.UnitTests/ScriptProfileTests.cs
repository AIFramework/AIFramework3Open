using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Text.Json.Nodes;

namespace AI.Script.UnitTests;

/// <summary>
/// Профиль pc-1: линейный конечный скрипт проходит, остальное отклоняется до первого вызова; функции из схемы JSON получают
/// аргументы объектом.
/// </summary>
public sealed class ScriptProfileTests
{
    private static readonly SchemaTool[] Tools =
    [
        new("media_play", "Включить музыку", new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["kind"] = new JsonObject { ["type"] = "string" } },
        }),
        new("volume_set", "Громкость", new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["level"] = new JsonObject { ["type"] = "integer" }, ["step"] = new JsonObject { ["type"] = "integer" } },
            ["required"] = new JsonArray("level"),
        }),
        new("audio_status", "Звук сейчас", null),
    ];

    [Theory]
    [InlineData("set x = 1")]
    [InlineData("let x = 1\nwhile x < 3 { x }")]
    [InlineData("fn twice(x) { x * 2 }")]
    [InlineData("let f = (x) => x + 1")]
    [InlineData("use pc as p")]
    [InlineData("emit x = math.sqrt(4)")]
    [InlineData("emit x = core.len(\"ab\")")]
    [InlineData("let r = pc.audio_status()\nemit t = r.text.upper()")]
    [InlineData("for x in [1, 2] { break }")]
    public void Forbidden_construct_is_refused_before_any_call(string script)
    {
        var calls = new List<string>();
        var host = ScriptProfile.Pc1.Host(Module(calls));

        CheckResult result = ScriptProfile.Pc1.Check(host, script);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.ProfileForbidden);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Linear_plan_passes_and_calls_get_named_arguments()
    {
        var calls = new List<string>();
        var host = ScriptProfile.Pc1.Host(Module(calls));
        const string script = """
            let now = pc.audio_status()
            if now.muted {
                pc.volume_set(level: 30)
            }
            try {
                pc.media_play(kind: "music")
            } catch e {
                emit failed = e
            }
            for i in range(2) {
                pc.volume_set(level: 10, step: i)
            }
            emit level = len("${now.level}")
            """;

        Assert.True(ScriptProfile.Pc1.Check(host, script).Success, ScriptProfile.Pc1.Check(host, script).Render());
        RunResult run = await host.RunAsync(script, ScriptProfile.Pc1.Options());

        Assert.True(run.Success, string.Join("\n", run.Diagnostics.Select(d => d.Render())));
        Assert.Equal(
            ["audio_status {}", "volume_set {\"level\":30}", "media_play {\"kind\":\"music\"}", "volume_set {\"level\":10,\"step\":0}", "volume_set {\"level\":10,\"step\":1}"],
            calls);
    }

    [Fact]
    public void Misspelled_argument_is_caught_by_the_schema()
    {
        var host = ScriptProfile.Pc1.Host(Module([]));

        CheckResult result = ScriptProfile.Pc1.Check(host, "pc.volume_set(levl: 30)");

        Assert.False(result.Success);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == DiagnosticCodes.ProfileForbidden);
    }

    [Fact]
    public async Task Loop_over_twenty_passes_stops_the_run()
    {
        var calls = new List<string>();
        var host = ScriptProfile.Pc1.Host(Module(calls));

        RunResult run = await host.RunAsync("for i in range(25) { pc.audio_status() }", ScriptProfile.Pc1.Options());

        Assert.False(run.Success);
        Assert.Contains(run.Diagnostics, d => d.Code == DiagnosticCodes.LoopLimit);
        Assert.Equal(20, calls.Count);
    }

    [Theory]
    [InlineData("emit ok = r.text")]
    [InlineData("if r { emit ok = 1 }")]
    [InlineData("emit ok = \"итог ${r}\"")]
    [InlineData("pc.media_play(kind: r)")]
    public async Task Reading_a_deferred_value_stops_the_run_even_inside_try(string use)
    {
        var module = SchemaModule.Build("pc", "компьютер", Tools, (_, _, _) => ValueTask.FromResult(ScriptHandle.Deferred("шаг 1")));
        var host = ScriptProfile.Pc1.Host(module);

        RunResult run = await host.RunAsync($$"""
            try {
                let r = pc.media_play(kind: "music")
                {{use}}
            } catch e {
                emit failed = e
            }
            """, ScriptProfile.Pc1.Options());

        Assert.False(run.Success);
        Assert.Contains(run.Diagnostics, d => d.Code == DiagnosticCodes.DeferredValue);
        Assert.False(run.Emitted.ContainsKey("failed"));
    }

    [Fact]
    public void Long_script_is_refused()
    {
        var result = ScriptProfile.Pc1.Violations(new string(' ', ScriptProfile.Pc1.MaxChars + 1));

        Assert.Contains(result, d => d.Code == DiagnosticCodes.ProfileTooLong);
    }

    private static ScriptModule Module(List<string> calls) =>
        SchemaModule.Build("pc", "компьютер", Tools, (name, args, _) =>
        {
            calls.Add($"{name} {args.ToJsonString()}");
            return ValueTask.FromResult(name == "audio_status"
                ? ScriptJson.FromAnswer("{\"level\":40,\"muted\":true}")
                : ScriptJson.FromAnswer("готово"));
        });
}
