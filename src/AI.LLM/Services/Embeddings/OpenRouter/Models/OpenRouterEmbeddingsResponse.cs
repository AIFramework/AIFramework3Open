using AI.DataStructs.Algebraic;
using AI.LLM.Core.Models.Common.Responses;
using System.Text.Json.Serialization;

namespace AI.LLM.Services.Embeddings.OpenRouter.Models;

/// <summary>
/// Ответ эндпоинта эмбеддингов OpenRouter
/// </summary>
public class OpenRouterEmbeddingsResponse
{
    /// <summary>
    /// Модель, которая фактически обработала запрос
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; }

    /// <summary>
    /// Векторы в порядке, который сервер не гарантирует — упорядочивать по <see cref="OpenRouterEmbeddingData.Index"/>
    /// </summary>
    [JsonPropertyName("data")]
    public List<OpenRouterEmbeddingData> Data { get; set; }

    /// <summary>
    /// Потребление токенов и стоимость запроса
    /// </summary>
    [JsonPropertyName("usage")]
    public Usage Usage { get; set; }
}

/// <summary>
/// Отдельный вектор в ответе
/// </summary>
public class OpenRouterEmbeddingData
{
    /// <summary>
    /// Вектор эмбеддинга
    /// </summary>
    [JsonPropertyName("embedding")]
    public Vector Embedding { get; set; }

    /// <summary>
    /// Позиция соответствующего элемента во входном массиве
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }
}
