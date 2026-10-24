using AI.DataStructs.Algebraic;
using AI.ONNX.NLP.Bert;
using Xunit;
using Xunit.Abstractions;

namespace AI.LLM.UnitTests;

/// <summary>
/// Эмбеддер BERT в ONNX: папка без модели — понятная ошибка; с моделью (переменная AI_ONNX_BERT_MODEL —
/// папка rubert-tiny2 или all-MiniLM-L6-v2) — единичные векторы, и близкие по смыслу тексты ближе далёких.
/// </summary>
public class OnnxBertEmbedderTests
{
    private readonly ITestOutputHelper _output;

    public OnnxBertEmbedderTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void FolderWithoutModel_IsRejectedWithFileName()
    {
        string folder = Path.Combine(Path.GetTempPath(), "onnx-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var error = Assert.Throws<FileNotFoundException>(() => new OnnxBertEmbedder(folder));

            Assert.False(OnnxBertEmbedder.IsModelFolder(folder));
            Assert.Contains("model.onnx", error.Message);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task RealModel_GivesUnitVectors_AndRanksByMeaning()
    {
        string folder = Environment.GetEnvironmentVariable("AI_ONNX_BERT_MODEL");
        if (!OnnxBertEmbedder.IsModelFolder(folder))
        {
            _output.WriteLine("AI_ONNX_BERT_MODEL не задана — проверка на модели пропущена.");
            return;
        }

        using var embedder = new OnnxBertEmbedder(folder);
        Vector[] vectors = await embedder.EncodeAsync([
            "расчёт скидки на заказ покупателя",
            "decimal Discount(Order order) => order.Total * 0.1m; // скидка",
            "чтение настроек окна из файла конфигурации"]);
        double near = vectors[0].Dot(vectors[1]);
        double far = vectors[0].Dot(vectors[2]);
        _output.WriteLine($"Размер {embedder.Dimension}; скидка↔код {near:F3}, скидка↔настройки {far:F3}.");

        Assert.All(vectors, v => Assert.Equal(1.0, v.NormL2(), 3));
        Assert.Equal(embedder.Dimension, vectors[0].Count);
        Assert.True(near > far, $"скидка↔код {near:F3} не больше скидка↔настройки {far:F3}");
    }
}
