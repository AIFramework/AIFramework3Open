using AI.LLM.Core.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace AI.LLM.Integration.SemanticKernel;

#pragma warning disable SKEXP0001

/// <summary>
/// SK-совместимая обёртка над <see cref="IEmbedderService"/>.
/// Реализует оба интерфейса: устаревший <see cref="ITextEmbeddingGenerationService"/>
/// и новый <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>, что обеспечивает
/// совместимость со всеми версиями SK.
/// </summary>
public class SharpGPTEmbeddingService : ITextEmbeddingGenerationService
{
    private readonly IEmbedderService _embedder;
    private readonly Dictionary<string, object> _attributes;

    /// <param name="embedder">Настроенный экземпляр IEmbedderService.</param>
    /// <param name="modelId">Идентификатор модели для метаданных SK.</param>
    public SharpGPTEmbeddingService(IEmbedderService embedder, string modelId = "sharpgpt-embedding")
    {
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
        _attributes = new Dictionary<string, object>
        {
            ["ModelId"] = modelId,
        };
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> Attributes => _attributes;

    /// <inheritdoc />
    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel kernel = null,
        CancellationToken cancellationToken = default)
    {
        if (data == null || data.Count == 0)
            return [];

        var vectors = await _embedder.EncodeAsync(data, cancellationToken);

        var result = new List<ReadOnlyMemory<float>>(vectors.Length);

        foreach (var vector in vectors)
        {
            result.Add(VectorToFloatMemory(vector));
        }

        return result;
    }

    private static ReadOnlyMemory<float> VectorToFloatMemory(AI.DataStructs.Algebraic.Vector vector)
    {
        var floats = new float[vector.Count];
        for (int i = 0; i < vector.Count; i++)
        {
            floats[i] = (float)vector[i];
        }
        return new ReadOnlyMemory<float>(floats);
    }
}

#pragma warning restore SKEXP0001
