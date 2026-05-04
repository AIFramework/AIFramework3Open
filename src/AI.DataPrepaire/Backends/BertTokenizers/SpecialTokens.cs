using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.DataPrepaire.Backends.BertTokenizers;

/// <summary>
/// Класс, содержащий константы для меток токенов.
/// </summary>
[Serializable]
public class SpecialTokens
{
    /// <summary>
    /// Пустой токен (паддинг)
    /// </summary>
    [JsonPropertyName("pad_token")]
    public string Padding { get; set; } = "";

    /// <summary>
    /// Метка для неизвестных слов
    /// </summary>
    [JsonPropertyName("unk_token")]
    public string Unknown { get; set; } = "[UNK]";

    /// <summary>
    /// Метка для классификации
    /// </summary>
    [JsonPropertyName("cls_token")]
    public string Classification { get; set; } = "[CLS]";

    /// <summary>
    /// Метка разделения
    /// </summary>
    [JsonPropertyName("sep_token")]
    public string Separation { get; set; } = "[SEP]";

    /// <summary>
    /// Метка маскировки
    /// </summary>
    [JsonPropertyName("mask_token")]
    public string Mask { get; set; } = "[MASK]";


    /// <summary>
    /// Загрузка токенов из JSON
    /// </summary>
    public static SpecialTokens FromJson(string jsonMapPath)
    {
        string json = File.ReadAllText(jsonMapPath);
        SpecialTokens tokens = JsonSerializer.Deserialize<SpecialTokens>(json);
        return tokens;
    }
}
