using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.ONNX.NLP.Bert;

/// <summary>
/// Конфигурация Bert
/// </summary>
[Serializable]
public class BertConfig
{
    /// <summary>
    /// Имя модели
    /// </summary>
    [JsonPropertyName("_name_or_path")]
    public string NameOrPath { get; set; }

    /// <summary>
    /// Архитектура модели
    /// </summary>
    [JsonPropertyName("architectures")]
    public List<string> Architectures { get; set; }

    /// <summary>
    /// Частота дропаута для слоя внимания
    /// </summary>
    [JsonPropertyName("attention_probs_dropout_prob")]
    public double AttentionProbsDropoutProb { get; set; }

    /// <summary>
    /// Gradient checkpointing
    /// </summary>
    [JsonPropertyName("gradient_checkpointing")]
    public bool GradientCheckpointing { get; set; }

    /// <summary>
    /// Активационная функция в скрытом слое
    /// </summary>
    [JsonPropertyName("hidden_act")]
    public string HiddenAct { get; set; }

    /// <summary>
    /// Частота дропаута для скрытого слоя
    /// </summary>
    [JsonPropertyName("hidden_dropout_prob")]
    public double HiddenDropoutProb { get; set; }

    /// <summary>
    /// Размерность скрытого слоя
    /// </summary>
    [JsonPropertyName("hidden_size")]
    public int HiddenSize { get; set; } = 384;

    /// <summary>
    /// Разброс значений при инициализации
    /// </summary>
    [JsonPropertyName("initializer_range")]
    public double InitializerRange { get; set; }

    /// <summary>
    /// Промежуточная размерность
    /// </summary>
    [JsonPropertyName("intermediate_size")]
    public int IntermediateSize { get; set; }

    /// <summary>
    /// Эпсилон для нормирующего слоя
    /// </summary>
    [JsonPropertyName("layer_norm_eps")]
    public double LayerNormEps { get; set; }

    /// <summary>
    /// Длина последовательности в токенах
    /// </summary>
    [JsonPropertyName("max_position_embeddings")]
    public int MaxPositionEmbeddings { get; set; }

    /// <summary>
    /// Частота дропаута для классификатора (может быть null)
    /// </summary>
    [JsonPropertyName("classifier_dropout")]
    public double? ClassifierDropout { get; set; }

    /// <summary>
    /// Число голов внимания
    /// </summary>
    [JsonPropertyName("num_attention_heads")]
    public int NumAttentionHeads { get; set; }

    /// <summary>
    /// Число скрытых слоев
    /// </summary>
    [JsonPropertyName("num_hidden_layers")]
    public int NumHiddenLayers { get; set; }

    /// <summary>
    /// Индекс добавочного пустого токена
    /// </summary>
    [JsonPropertyName("pad_token_id")]
    public int PadTokenId { get; set; }

    /// <summary>
    /// Тип позиционного кодирования
    /// </summary>
    [JsonPropertyName("position_embedding_type")]
    public string PositionEmbeddingType { get; set; }

    /// <summary>
    /// Версия библиотеки transformers
    /// </summary>
    [JsonPropertyName("transformers_version")]
    public string TransformersVersion { get; set; }

    /// <summary>
    /// Тип словаря
    /// </summary>
    [JsonPropertyName("type_vocab_size")]
    public int TypeVocabSize { get; set; }

    /// <summary>
    /// Используется ли кэш
    /// </summary>
    [JsonPropertyName("use_cache")]
    public bool UseCache { get; set; }

    /// <summary>
    /// Размер словаря токенов
    /// </summary>
    [JsonPropertyName("vocab_size")]
    public int VocabSize { get; set; }

    /// <summary>
    /// Загрузка конфигурации из JSON
    /// </summary>
    public static BertConfig FromJson(string jsonConfigPath)
    {
        string json = File.ReadAllText(jsonConfigPath);
        return JsonSerializer.Deserialize<BertConfig>(json);
    }
}
