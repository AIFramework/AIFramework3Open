using AI.Agents.Contracts.Citations;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.ReAct;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Xunit;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Подтверждения: ReActCitation → Citation без потерь; Citation без Url в старую форму не переводится, и пропуск виден.
/// </summary>
public sealed class CitationTests
{
    [Theory]
    [InlineData("https://a.example/page", "Заголовок", "фрагмент")]
    [InlineData("https://a.example/page", null, null)]
    public void ReActCitation_ToCitationAndBack_IsLossless(string url, string? title, string? snippet)
    {
        var original = new ReActCitation(url, title, snippet);

        Citation citation = original.ToCitation();
        ReActCitation back = citation.ToReActCitation()!;

        Assert.Equal(CitationKind.Web, citation.Kind);
        Assert.Equal(original.Url, citation.Url);
        Assert.Equal(original.Title, citation.Title);
        Assert.Equal(original.Snippet ?? string.Empty, citation.Quote);
        Assert.Equal(original.Url, back.Url);
        Assert.Equal(original.Title, back.Title);
        Assert.Equal(original.Snippet, back.Snippet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Citation_WithoutUrl_IsNotInvented(string? url)
    {
        var citation = new Citation { Kind = CitationKind.File, Title = "отчет.pdf", Quote = "строка", Url = url };

        Assert.Null(citation.ToReActCitation());
    }

    [Fact]
    public void Result_WithCitationWithoutUrl_SkipsItVisibly()
    {
        var log = new CapturingLog();
        var result = new ToolResult
        {
            Status = ToolStatus.Ok,
            Text = "найдено",
            Origin = new Origin { Kind = OriginKind.External },
            Evidence =
            [
                new Citation { Kind = CitationKind.Web, Title = "Сайт", Quote = "цитата", Url = "https://s.example" },
                new Citation { Kind = CitationKind.File, Title = "отчет.pdf", Quote = "строка 3" },
                new Citation { Kind = CitationKind.Memory, Title = "память", Quote = "факт" },
            ],
        };

        ReActToolOutcome outcome = result.ToOutcome(log.Logger);

        ReActCitation kept = Assert.Single(outcome.Citations);
        Assert.Equal("https://s.example", kept.Url);
        Assert.DoesNotContain(outcome.Citations, c => !c.Url.StartsWith("https://s.example", StringComparison.Ordinal));
        string warning = Assert.Single(log.Warnings);
        Assert.Contains("2", warning);
        Assert.Contains("без Url", warning);
    }

    [Fact]
    public void Result_WithNonImageFiles_SkipsThemVisibly()
    {
        var log = new CapturingLog();
        var result = new ToolResult
        {
            Status = ToolStatus.Ok,
            Text = "готово",
            Origin = new Origin { Kind = OriginKind.Trusted },
            Files =
            [
                new ToolFile { ContentType = "image/jpeg", Content = [1, 2], ShowToModel = true },
                new ToolFile { ContentType = "application/pdf", Name = "a.pdf", Content = [3], ShowToModel = true },
                new ToolFile { ContentType = "image/png", Uri = "https://img.example/1.png", ShowToModel = true },
                new ToolFile { ContentType = "image/png", Content = [4], ShowToModel = false },
            ],
        };

        ReActToolOutcome outcome = result.ToOutcome(log.Logger);

        Assert.Equal("image/jpeg", Assert.Single(outcome.Images).MimeType);
        Assert.Contains(log.Warnings, w => w.Contains("3") && w.Contains("файлов"));
    }
}
