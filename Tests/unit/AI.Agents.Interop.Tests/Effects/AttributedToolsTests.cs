using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Effects;
using AI.LLM.Agents.Tools;
using Xunit;

namespace AI.Agents.Interop.Tests.Effects;

/// <summary>
/// Атрибут эффекта на инструментах [AgentTool]: с атрибутом — его значения, без — Unknown по обеим осям и одно
/// предупреждение; атрибут без [AgentTool] — ошибка.
/// </summary>
public sealed class AttributedToolsTests
{
    [Fact]
    public void WithAttribute_SpecCarriesActionAndIdempotency()
    {
        IToolSet set = AttributedTools.AsToolSet([new Mailer()]);

        ToolSpec send = set.Available.Single(s => s.Name == "send_mail");
        Assert.Equal(ToolAction.Irreversible, send.Action);
        Assert.Equal(ToolIdempotency.NotIdempotent, send.Idempotency);

        ToolSpec read = set.Available.Single(s => s.Name == "list_inbox");
        Assert.Equal(ToolAction.Read, read.Action);
        Assert.Equal(ToolIdempotency.Idempotent, read.Idempotency);
    }

    [Fact]
    public void WithoutAttribute_UnknownOnBothAxes_AndOneWarningPerTool()
    {
        var log = new CapturingLog();
        IToolSet set = AttributedTools.AsToolSet([new Mailer()], log.Logger);

        for (int i = 0; i < 3; i++)
        {
            ToolSpec plain = set.Available.Single(s => s.Name == "archive");
            Assert.Equal(ToolAction.Unknown, plain.Action);
            Assert.Equal(ToolIdempotency.Unknown, plain.Idempotency);
        }

        string warning = Assert.Single(log.Warnings);
        Assert.Contains("archive", warning, StringComparison.Ordinal);
        Assert.Contains("ToolEffect", warning, StringComparison.Ordinal);
    }

    /// <summary>Read и Idempotent сами не появляются: ни у одного инструмента без атрибута.</summary>
    [Fact]
    public void ReadAndIdempotent_NeverAppearByThemselves()
    {
        IToolSet set = AttributedTools.AsToolSet([new Plain()]);

        Assert.All(set.Available, spec =>
        {
            Assert.Equal(ToolAction.Unknown, spec.Action);
            Assert.Equal(ToolIdempotency.Unknown, spec.Idempotency);
            Assert.Equal(ToolAction.Irreversible, spec.Action.Effective());
            Assert.Equal(ToolIdempotency.NotIdempotent, spec.Idempotency.Effective());
        });
    }

    [Fact]
    public void EffectWithoutAgentTool_IsAnError()
    {
        var error = Assert.Throws<ArgumentException>(() => AttributedTools.AsToolSet([new Orphan()]));
        Assert.Contains("Orphan.Forgotten", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Calls_GoThroughRegistry()
    {
        IToolSet set = AttributedTools.AsToolSet([new Mailer()]);

        ToolResult result = Kit.Result(await Kit.ReadAll(set.CallAsync(
            new ToolCall { Id = "c1", Name = "send_mail", Arguments = Kit.Json("""{"to":"a@example.org"}""") },
            Kit.Context())));

        Assert.Equal(ToolStatus.Ok, result.Status);
        Assert.Equal("отправлено a@example.org", result.Text);
    }

    [Fact]
    public async Task AttributedSet_ThroughGate_DoesNotResendMail()
    {
        var mailer = new Mailer();
        var gate = new EffectGate(AttributedTools.AsToolSet([mailer]));
        var call = new ToolCall { Id = "c1", Name = "send_mail", Arguments = Kit.Json("""{"to":"a@example.org"}""") };

        await Kit.ReadAll(gate.CallAsync(call, Kit.Context()));
        await Kit.ReadAll(gate.CallAsync(call with { Id = "c2" }, Kit.Context()));

        Assert.Equal(1, mailer.Sent);
    }

    private sealed class Mailer
    {
        public int Sent { get; private set; }

        [AgentTool("send_mail", "Отправить письмо")]
        [ToolEffect(ToolAction.Irreversible, ToolIdempotency.NotIdempotent)]
        public string SendMail(string to)
        {
            Sent++;
            return "отправлено " + to;
        }

        [AgentTool("list_inbox", "Список писем")]
        [ToolEffect(ToolAction.Read, ToolIdempotency.Idempotent)]
        public string ListInbox() => "пусто";

        [AgentTool("archive", "В архив")]
        public string Archive(string id) => "в архиве " + id;
    }

    private sealed class Plain
    {
        [AgentTool("one", "Первый")]
        public string One() => "1";

        [AgentTool("two", "Второй")]
        public string Two() => "2";
    }

    private sealed class Orphan
    {
        [AgentTool("ok", "Нормальный")]
        public string Ok() => "ok";

        [ToolEffect(ToolAction.Read, ToolIdempotency.Idempotent)]
        public string Forgotten() => "забыт";
    }
}
