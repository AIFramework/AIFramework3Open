using AI.DataStructs.Algebraic;
using AI.LLM.Services.Embeddings.Base;
using AI.LLM.Services.Embeddings.Caching;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Кэш эмбеддингов: повторный текст не уходит в модель, кэш переживает перезапуск, файлы
/// разных моделей не смешиваются.
/// </summary>
public sealed class CachedEmbedderTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("embed-cache-").FullName;

    private string CachePath => Path.Combine(_folder, "sub", "vectors.bin");

    [Fact]
    public async Task RepeatedTextsGoToModelOnce()
    {
        var model = new CountingEmbedder("m1");
        using var cached = new CachedEmbedder(model, CachePath);

        Vector[] first = await cached.EncodeAsync(["альфа", "бета", "альфа"]);
        Vector[] second = await cached.EncodeAsync(["бета", "гамма"]);

        Assert.Equal(["альфа", "бета", "гамма"], model.Seen);
        Assert.Equal(first[0], first[2]);
        Assert.Equal(first[1], second[0]);
        Assert.Equal(3, cached.Misses);
    }

    [Fact]
    public async Task CacheSurvivesRestart()
    {
        Vector expected;

        using (var cached = new CachedEmbedder(new CountingEmbedder("m1"), CachePath))
            expected = (await cached.EncodeAsync(["альфа"]))[0];

        var model = new CountingEmbedder("m1");
        using var reopened = new CachedEmbedder(model, CachePath);

        Assert.Equal(expected, (await reopened.EncodeAsync(["альфа"]))[0]);
        Assert.Empty(model.Seen);
        Assert.Equal(1, reopened.Count);
    }

    [Fact]
    public async Task QuestionsAndDocumentsAreCachedSeparately()
    {
        var model = new CountingEmbedder("m1");
        using var cached = new CachedEmbedder(model, CachePath);

        Vector document = (await cached.EncodeAsync(["фильтр"]))[0];
        Vector question = await cached.EncodeQuestionAsync("фильтр");
        await cached.EncodeQuestionAsync("фильтр");

        Assert.NotEqual(document, question);
        Assert.Equal(["фильтр", "query: фильтр"], model.Seen);
    }

    [Fact]
    public async Task FileOfAnotherModelIsRefused()
    {
        using (var cached = new CachedEmbedder(new CountingEmbedder("m1"), CachePath))
            await cached.EncodeAsync(["альфа"]);

        var error = Assert.Throws<InvalidOperationException>(() => new CachedEmbedder(new CountingEmbedder("m2"), CachePath));
        Assert.Contains("m1", error.Message);
    }

    [Fact]
    public async Task TruncatedTailIsDroppedAndOverwritten()
    {
        using (var cached = new CachedEmbedder(new CountingEmbedder("m1"), CachePath))
            await cached.EncodeAsync(["альфа", "бета"]);

        // Сбой посреди дописывания: от последней записи остался обрывок.
        using (FileStream file = File.OpenWrite(CachePath)) file.SetLength(file.Length - 5);

        var model = new CountingEmbedder("m1");
        using (var reopened = new CachedEmbedder(model, CachePath))
        {
            Assert.Equal(1, reopened.Count);
            await reopened.EncodeAsync(["альфа", "бета", "гамма"]);
            Assert.Equal(["бета", "гамма"], model.Seen);
        }

        using var again = new CachedEmbedder(new CountingEmbedder("m1"), CachePath);
        Assert.Equal(3, again.Count);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>Детерминированный эмбеддер: вектор из кодов символов, вопросы с префиксом.</summary>
    private sealed class CountingEmbedder : EmbedderServiceBase
    {
        public CountingEmbedder(string model) => ModelName = model;

        public List<string> Seen { get; } = [];

        public override string GetDetailedInstruct(string question) => "query: " + question;

        public override Task<Vector[]> EncodeAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
        {
            string[] items = texts.ToArray();
            Seen.AddRange(items);
            return Task.FromResult(items.Select(text => new Vector(text.Length, text.Sum(c => c) % 97, text[0])).ToArray());
        }
    }
}
