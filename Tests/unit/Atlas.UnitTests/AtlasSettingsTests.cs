using AI.LLM.Services.Embeddings.OpenRouter;
using Xunit;

namespace AiFramework.Tools.Atlas.UnitTests;

public sealed class AtlasSettingsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("atlas-settings-").FullName;

    [Fact]
    public void MissingFileGivesDefaults()
    {
        AtlasSettings settings = AtlasSettings.Load(Path.Combine(_folder, "settings.json"));

        Assert.Equal(OpenRouterEmbeddingModels.BgeM3, settings.EmbeddingModel);
        Assert.Null(settings.SearchModel);
    }

    [Fact]
    public void ReadsModelsButNeverKeyFromFile()
    {
        string path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, """
            {
              // комментарии и висячие запятые разрешены
              "searchModel": "vendor/fast",
              "verdictModel": "vendor/strong",
              "apiKey": "sk-from-file",
            }
            """);

        AtlasSettings settings = AtlasSettings.Load(path);

        Assert.Equal("vendor/fast", settings.SearchModel);
        Assert.Equal("vendor/strong", settings.VerdictModel);
        Assert.NotEqual("sk-from-file", settings.ApiKey);
    }

    [Fact]
    public void WithoutKeyNetworkStagesAreOff()
    {
        var settings = new AtlasSettings { SearchModel = "vendor/fast", ApiKey = null };

        string report = settings.Describe();

        Assert.Contains("не задан", report);
        Assert.Contains("LLM поиска: vendor/fast, но выключен — нет ключа.", report);
    }

    [Fact]
    public void ReportNeverShowsKey()
    {
        var settings = new AtlasSettings { SearchModel = "vendor/fast", ApiKey = "sk-or-v1-secret-for-test" };

        string report = settings.Describe();

        Assert.Contains("Ключ OpenRouter: задан", report);
        Assert.DoesNotContain("secret-for-test", report);
    }

    [Fact]
    public void IndexLivesOutsideRepositoryAndDependsOnPath()
    {
        string first = AtlasSettings.IndexPath(@"C:\repos\AIFramework3Open");
        string second = AtlasSettings.IndexPath(@"D:\copy\AIFramework3Open");

        Assert.StartsWith(AtlasSettings.CacheRoot, first);
        Assert.Contains("AIFramework3Open-", first);
        Assert.NotEqual(first, second);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
