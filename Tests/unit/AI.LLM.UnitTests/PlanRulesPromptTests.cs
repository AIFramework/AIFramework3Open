using AI.LLM.Agents.Planning;
using AI.LLM.Agents.Tools;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.UnitTests.Fakes;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Правила планировщика (<see cref="PlanGenerator"/>) сверяются с эталоном дословно: перенос текста из кода в файл
/// промпта и любая правка формата видны здесь, а не по поведению модели. Эталона нет, и тест его записывает и падает:
/// новый эталон смотрит человек.
/// </summary>
public sealed class PlanRulesPromptTests
{
    [Fact]
    public Task Правила_без_инструментов_и_портов() =>
        Check("plan-rules-plain", PlanGeneratorBuilder.Create().WithLLM(new FakeLLMClient()).WithMaxSteps(4));

    [Fact]
    public Task Правила_строгой_схемы_с_портами() =>
        Check("plan-rules-strict", PlanGeneratorBuilder.Create().WithLLM(new FakeLLMClient()).WithMaxSteps(6)
            .WithTools(new SampleTools()).WithStrictSchema().WithPortsPrompt("echo: вход text, выход reply"));

    private static async Task Check(string name, PlanGeneratorBuilder builder)
    {
        var llm = new FakeLLMClient().EnqueueText("{\"steps\": []}");
        var generator = builder.WithLLM(llm).Build();

        await generator.GenerateAsync("задача");

        var rules = llm.SentMessages[0][0].Content is MessageContent parts
            ? string.Concat(parts.OfType<TextContentItem>().Select(t => t.Text))
            : llm.SentMessages[0][0].Content?.ToString() ?? "";
        rules = rules.Replace("\r\n", "\n");

        var path = Path.Combine(ProjectDir(), "Golden", name + ".txt");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, rules);
            Assert.Fail($"Эталон записан: {path}");
        }
        Assert.Equal((await File.ReadAllTextAsync(path)).Replace("\r\n", "\n"), rules);
    }

    private static string ProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AI.LLM.UnitTests.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Каталог тестового проекта не найден");
    }

    private sealed class SampleTools
    {
        [AgentTool("echo", "Повторяет переданный текст")]
        public string Echo([ToolParameter("Текст для повтора")] string text) => "эхо: " + text;
    }
}
