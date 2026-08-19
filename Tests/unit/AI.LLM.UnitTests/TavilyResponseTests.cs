using System.Text.Json;
using AI.LLM.Clients.Tavily.Models;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Разбор ответов Tavily. Главное здесь — картинки: один и тот же API отдаёт их СТРОКОЙ
/// (адрес) и ОБЪЕКТОМ (адрес плюс описание) в зависимости от флага запроса, и на второй форме
/// разбор молча отдавал бы пустой список — то есть «картинок не нашлось» при полной выдаче.
/// </summary>
public class TavilyResponseTests
{
    [Fact]
    public void SearchResult_ReadsAnswerAndImageStrings()
    {
        var json = """
        {
          "query": "суда на воздушной подушке",
          "answer": "Коротко о главном.",
          "images": ["https://site/a.jpg", "https://site/b.jpg"],
          "results": [{"url": "https://site/page", "title": "Страница", "content": "Текст"}]
        }
        """;

        var result = JsonSerializer.Deserialize<SearchResult>(json)!;

        Assert.Equal("Коротко о главном.", result.Answer);
        Assert.Collection(result.Images,
            first => Assert.Equal("https://site/a.jpg", first.Url),
            second => Assert.Equal("https://site/b.jpg", second.Url));
        Assert.All(result.Images, image => Assert.Null(image.Description));
    }

    [Fact]
    public void SearchResult_ReadsImageObjectsWithDescriptions()
    {
        var json = """
        {
          "images": [{"url": "https://site/a.jpg", "description": "Схема узла"}],
          "results": []
        }
        """;

        var image = Assert.Single(JsonSerializer.Deserialize<SearchResult>(json)!.Images);

        Assert.Equal("https://site/a.jpg", image.Url);
        Assert.Equal("Схема узла", image.Description);
    }

    [Fact]
    public void ExtractResult_ReadsPageImages()
    {
        var json = """
        {
          "results": [{"url": "https://site/page", "raw_content": "# Заголовок",
                       "images": ["https://site/a.jpg"]}],
          "failed_results": []
        }
        """;

        var page = Assert.Single(JsonSerializer.Deserialize<ExtractResult>(json)!.Results);

        Assert.Equal("# Заголовок", page.RawContent);
        Assert.Equal("https://site/a.jpg", Assert.Single(page.Images).Url);
    }

    /// <summary>Незнакомая форма элемента не должна ронять разбор всего ответа.</summary>
    [Fact]
    public void SearchResult_SkipsUnknownImageShape()
    {
        var json = """{"images": [42, {"url": "https://site/a.jpg"}], "results": []}""";

        var images = JsonSerializer.Deserialize<SearchResult>(json)!.Images;

        Assert.Collection(images,
            first => Assert.Null(first),
            second => Assert.Equal("https://site/a.jpg", second.Url));
    }
}
