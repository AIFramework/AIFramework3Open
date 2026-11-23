using System.Net;
using System.Text.Json;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using AI.LLM.Clients.Base;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;
using ContractToolCall = AI.Agents.Contracts.Tools.ToolCall;
using LlmToolCall = AI.LLM.Core.Models.Common.ToolCalling.ToolCall;

namespace AI.Agents.Interop.Models;

// Часть ModelTranslation: внутренние помощники перевода.
public static partial class ModelTranslation
{
    // ------------------------------------------------------------------
    // Внутреннее
    // ------------------------------------------------------------------

    private static readonly Origin UnknownOrigin = new() { Kind = OriginKind.Unknown };

    private static ToolChoice Named(ModelAsk ask)
    {
        if (string.IsNullOrEmpty(ask.ToolName))
            throw new ArgumentException("ToolMode.Named без ToolName.", nameof(ask));

        if (ask.Tools?.Any(t => ToolSpec.NameComparer.Equals(t.Name, ask.ToolName)) != true)
            throw new ArgumentException($"ToolMode.Named: инструмента «{ask.ToolName}» нет в Tools.", nameof(ask));

        return ToolChoice.ForFunction(ask.ToolName);
    }

    private static IContentItem ToContentItem(MessagePart part) => part switch
    {
        MessagePart.Text text => new TextContentItem(text.Content),
        MessagePart.Image { Data: { } data, Uri: null } image =>
            new ImageContent($"data:{image.ContentType};base64,{Convert.ToBase64String(data)}"),
        MessagePart.Image { Data: null, Uri: { } uri } => new ImageContent(uri),
        MessagePart.Image => throw new ArgumentException("У картинки должны быть байты (Data) или адрес (Uri), ровно одно из двух."),
        MessagePart.Audio { Data: { } data } audio => new AudioContent(Convert.ToBase64String(data), AudioFormat(audio.ContentType)),
        MessagePart.Audio => throw new NotSupportedException("Звук по адресу: в старой форме звук передается только байтами."),
        _ => throw new NotSupportedException($"Часть сообщения {part.GetType().Name} старая форма не выражает."),
    };

    private static MessagePart ToPart(IContentItem item, ref bool cache)
    {
        switch (item)
        {
            case TextContentItem text:
                cache |= text.CacheControl != null;
                return new MessagePart.Text { Content = text.Text ?? "", Origin = UnknownOrigin };

            case ImageContent { ImageUrl.Url: { } url }:
                if (TryParseDataUrl(url, out string? mime, out byte[]? bytes))
                    return new MessagePart.Image { ContentType = mime!, Data = bytes, Origin = UnknownOrigin };
                return new MessagePart.Image { ContentType = ImageTypeByAddress(url), Uri = url, Origin = UnknownOrigin };

            case AudioContent { InputAudio: { Data: { } data } audio }:
                return new MessagePart.Audio
                {
                    ContentType = AudioContentType(audio.Format),
                    Data = Convert.FromBase64String(data),
                    Origin = UnknownOrigin,
                };

            default:
                throw new NotSupportedException($"Часть содержимого {item?.GetType().Name ?? "null"} в контракте не выражается.");
        }
    }

    private static ContractToolCall ToToolCall(LlmToolCall call, ICollection<string>? lost)
    {
        ArgumentNullException.ThrowIfNull(call);

        if (call.Id == null) lost?.Add("ToolCall.Id");

        return new ContractToolCall
        {
            Id = call.Id ?? "",
            Name = call.Function?.Name ?? "",
            Arguments = ParseArguments(call.Function?.Arguments),
        };
    }

    private static void CollectLostSettings(GenerateSettings s, ICollection<string> lost)
    {
        if (s.TopP != null) lost.Add("GenerateSettings.TopP");
        if (s.TopK != null) lost.Add("GenerateSettings.TopK");
        if (s.RepetitionPenalty != null) lost.Add("GenerateSettings.RepetitionPenalty");
        if (s.MinTokens != null) lost.Add("GenerateSettings.MinTokens");
        if (s.LogProbs != null) lost.Add("GenerateSettings.LogProbs");
        if (s.TopLogprobs != null) lost.Add("GenerateSettings.TopLogprobs");
        if (s.Stream) lost.Add("GenerateSettings.StreamId");
        // Уровень размышления переводится (ReasoningOf); теряется то, что уровнем не выражается.
        if (s.ReasoningSettings is { } rs
            && (rs.MaxTokens != null || rs.Exclude || rs.Enabled || (rs.Effort != null && ParseEffort(rs.Effort) == null)))
            lost.Add("GenerateSettings.ReasoningSettings");
        if (s.ReasoningEffort != null && ParseEffort(s.ReasoningEffort) == null) lost.Add("GenerateSettings.ReasoningEffort");
        if (s.Modalities is { Count: > 0 }) lost.Add("GenerateSettings.Modalities");
        if (s.IncludeUsage != null) lost.Add("GenerateSettings.IncludeUsage");
        if (s.IncludeReasoning != null) lost.Add("GenerateSettings.IncludeReasoning");
    }

    /// <summary>
    /// Стоимость: и число CLR, и JSON провайдера (<see cref="JsonElement"/>) разбирает <c>CostExtractor</c>
    /// без оглядки на культуру процесса.
    /// </summary>
    private static decimal? Cost(object? cost) => cost is null ? null : CostExtractor.TryExtract(cost);

    private static LlmRequestException Refused(HttpStatusCode code, string reason, LlmAnswer answer) =>
        LlmRequestException.FromResponse(
            code, JsonSerializer.Serialize(new { error = new { message = WithText("порт модели: " + reason, answer) } }),
            retryAfter: null);

    /// <summary>Причина и текст сбоя для человека (<see cref="LlmAnswer.FailureText"/>), если он есть.</summary>
    private static string WithText(string reason, LlmAnswer answer) =>
        answer.FailureText is { Length: > 0 } text ? reason + ": " + text : reason;

    /// <summary>
    /// Уровень размышления в настройки: в <c>ReasoningSettings.Effort</c>, если начальные настройки порта говорят с
    /// провайдером объектом рассуждения (OpenRouter), иначе в <c>ReasoningEffort</c> (поле OpenAI). Объект начальных
    /// настроек не меняется: он общий для всех запросов порта.
    /// </summary>
    private static void SetReasoning(GenerateSettings settings, ReasoningLevel level)
    {
        string effort = level switch
        {
            ReasoningLevel.None => "none",
            ReasoningLevel.Minimal => "minimal",
            ReasoningLevel.Low => "low",
            ReasoningLevel.Medium => "medium",
            ReasoningLevel.High => "high",
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Уровень размышления не задан."),
        };

        if (settings.ReasoningSettings is { } current)
        {
            settings.ReasoningSettings = new ReasoningSettings
            {
                Effort = effort,
                MaxTokens = current.MaxTokens,
                Exclude = current.Exclude,
                Enabled = current.Enabled,
            };
        }
        else
        {
            settings.ReasoningEffort = effort;
        }
    }

    /// <summary>Уровень размышления из настроек старой формы; <see langword="null"/>: не задан или не выражается.</summary>
    private static ReasoningLevel? ReasoningOf(GenerateSettings? settings) =>
        ParseEffort(settings?.ReasoningSettings?.Effort) ?? ParseEffort(settings?.ReasoningEffort);

    private static ReasoningLevel? ParseEffort(string? effort) => effort?.Trim().ToLowerInvariant() switch
    {
        "none" => ReasoningLevel.None,
        "minimal" => ReasoningLevel.Minimal,
        "low" => ReasoningLevel.Low,
        "medium" => ReasoningLevel.Medium,
        "high" => ReasoningLevel.High,
        _ => null,
    };

    private static string? Text(object? content) => content switch
    {
        null => null,
        string text => NullIfEmpty(text),
        MessageContent items => NullIfEmpty(items.ToString()),
        var other => NullIfEmpty(other.ToString()),
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static int Clamp(long value) => (int)Math.Clamp(value, 0, int.MaxValue);

    /// <summary>Формат <c>input_audio</c> по типу содержимого: <c>audio/wav</c> → <c>wav</c>, <c>audio/mpeg</c> → <c>mp3</c>.</summary>
    private static string AudioFormat(string contentType)
    {
        const string prefix = "audio/";
        if (!contentType.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Тип звука «{contentType}» не audio/*: формат для старой формы не определить.");

        string subtype = contentType[prefix.Length..].Split(';')[0].Trim().ToLowerInvariant();
        if (subtype.StartsWith("x-", StringComparison.Ordinal))
            subtype = subtype[2..];

        return subtype switch
        {
            "mpeg" or "mp3" => "mp3",
            "wave" or "wav" => "wav",
            "mp4" or "m4a" => "m4a",
            "" => throw new NotSupportedException($"Тип звука «{contentType}» без подтипа."),
            _ => subtype,
        };
    }

    /// <summary>Тип содержимого по формату <c>input_audio</c>: обратное к <see cref="AudioFormat(string)"/>.</summary>
    private static string AudioContentType(string? format) => format?.ToLowerInvariant() switch
    {
        "mp3" => "audio/mpeg",
        "m4a" => "audio/mp4",
        null or "" => throw new NotSupportedException("У звука не указан формат."),
        var other => "audio/" + other,
    };

    private static bool TryParseDataUrl(string url, out string? mime, out byte[]? bytes)
    {
        mime = null;
        bytes = null;

        const string marker = ";base64,";
        if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return false;

        int at = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
            return false;

        mime = url[5..at];
        bytes = Convert.FromBase64String(url[(at + marker.Length)..]);
        return mime.Length > 0;
    }

    /// <summary>
    /// Тип картинки по адресу: по расширению, иначе <c>image/*</c> — «картинка, подтип неизвестен». Старая форма
    /// тип картинки по адресу не хранит, а контракт требует его.
    /// </summary>
    private static string ImageTypeByAddress(string url)
    {
        string path = url.Split('?', '#')[0];
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/*",
        };
    }
}
