using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;

namespace AI.LLM.Services.Embeddings.Base;

/// <summary>
/// Базовая реализация эмбеддера: нормализация косинуса, обработка блоков и одиночных текстов.
/// Наследнику остаётся реализовать только пакетное кодирование <see cref="EncodeAsync(IEnumerable{string}, CancellationToken)"/>.
/// </summary>
public abstract class EmbedderServiceBase : IEmbedderService
{
    /// <summary>
    /// Параметры тангенса "k", f(x) = tanh(k*x+b)
    /// </summary>
    public double TanhNormParamK { get; set; } = 0.64;

    /// <summary>
    /// Параметры тангенса "b", f(x) = tanh(k*x+b)
    /// </summary>
    public double TanhNormParamB { get; set; } = 0.55;

    /// <summary>
    /// СКО косинуса
    /// </summary>
    public double StdCos { get; set; } = 1;

    /// <summary>
    /// Среднее косинуса
    /// </summary>
    public double MeanCos { get; set; } = 1;

    /// <summary>
    /// Имя модели эмбеддера
    /// </summary>
    public virtual string ModelName { get; set; }

    /// <summary>
    /// Оформление запроса под инструктивные модели (Qwen3 Embedding, E5 и т.п.).
    /// По умолчанию запрос уходит как есть.
    /// </summary>
    public virtual string GetDetailedInstruct(string question) => question;

    /// <summary>
    /// Нормализация косинуса через гиперболический тангенс
    /// </summary>
    public virtual double TanhCosineNormalize(double cosine) =>
        Math.Tanh(TanhNormParamK * (cosine - MeanCos) / StdCos + TanhNormParamB);

    /// <inheritdoc/>
    public abstract Task<Vector[]> EncodeAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public virtual async Task<Vector> EncodeAsync(string text, CancellationToken cancellationToken = default)
    {
        var vectors = await EncodeAsync([text], cancellationToken);
        return vectors.FirstOrDefault()
            ?? throw new InvalidOperationException("Embedding result is empty or null");
    }

    /// <inheritdoc/>
    public virtual async Task<Vector> EncodeQuestionAsync(string question, CancellationToken cancellationToken = default)
    {
        var vectors = await EncodeAsync([GetDetailedInstruct(question)], cancellationToken);
        return vectors.FirstOrDefault()
            ?? throw new InvalidOperationException("Question embedding result is empty or null");
    }

    /// <inheritdoc/>
    public virtual async Task<Vector[]> EncodeAsyncWithBlockSize(
        IEnumerable<string> processedTexts,
        IEnumerable<int> blockSizes,
        IEnumerable<int> excludeBlockSizes = null,
        CancellationToken cancellationToken = default)
    {
        var snippetsTexts = processedTexts.ToArray();
        var blockSizesArray = blockSizes.ToArray();

        if (snippetsTexts.Length != blockSizesArray.Length)
            throw new ArgumentException("Array size mismatch between texts and blockSizes");

        List<int> indexes = [];
        List<string> texts = [];
        var embeddings = new Vector[snippetsTexts.Length];

        for (int i = 0; i < snippetsTexts.Length; i++)
        {
            if (excludeBlockSizes == null ||
                !excludeBlockSizes.Contains(blockSizesArray[i]))
            {
                indexes.Add(i);
                texts.Add(snippetsTexts[i]);
            }
        }

        if (texts.Count > 0)
        {
            var vectors = await EncodeAsync(texts, cancellationToken);
            for (int i = 0; i < vectors.Length; i++)
                embeddings[indexes[i]] = vectors[i];
        }

        return embeddings;
    }
}
