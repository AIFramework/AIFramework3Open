using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AI.LLM.Agents.Tools;
using AI.LLM.Core.Models.Common.ToolCalling;

namespace AI.LLM.Agents;

public sealed partial class Agent
{
    /// <summary>
    /// Пытается извлечь вызовы инструментов из текста ответа модели.
    /// Поддерживает два формата:
    /// <list type="bullet">
    ///   <item>Fenced JSON: <c>```json { "tool": "...", "arguments": {...} } ```</c></item>
    ///   <item>Inline JSON: <c>{"tool": "...", "arguments": {...}}</c></item>
    /// </list>
    /// </summary>
    private static List<ToolCall> TryParseToolCallsFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var toolCalls = new List<ToolCall>();

        // 1. Fenced ```json ... ``` блоки (самый надёжный формат)
        var fenced = Regex.Matches(text, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.Multiline);
        foreach (Match m in fenced)
            if (TryParseToolCallJson(m.Groups[1].Value) is { } tc)
                toolCalls.Add(tc);

        if (toolCalls.Count > 0)
            return toolCalls;

        // 2. Inline JSON — ищем сбалансированные фигурные скобки
        foreach (var json in ExtractBalancedJson(text))
            if (TryParseToolCallJson(json) is { } tc)
                toolCalls.Add(tc);

        return toolCalls.Count > 0 ? toolCalls : null;
    }

    /// <summary>Парсит один JSON-объект как вызов инструмента.</summary>
    private static ToolCall TryParseToolCallJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tool", out var toolEl)) return null;

            var argsRaw = root.TryGetProperty("arguments", out var argsEl)
                ? argsEl.GetRawText()
                : "{}";

            return new ToolCall
            {
                Id = $"pfc_{Guid.NewGuid():N}",
                Type = "function",
                Function = new FunctionCall { Name = toolEl.GetString(), Arguments = argsRaw }
            };
        }
        catch { return null; }
    }

    /// <summary>
    /// Извлекает JSON-объекты из произвольного текста с учётом вложенных скобок.
    /// </summary>
    private static IEnumerable<string> ExtractBalancedJson(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            int start = text.IndexOf('{', i);
            if (start < 0) yield break;

            int depth = 0;
            bool inString = false;
            for (int j = start; j < text.Length; j++)
            {
                char c = text[j];
                if (inString)
                {
                    if (c == '\\') { j++; continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inString = true; break;
                    case '{': depth++; break;
                    case '}':
                        depth--;
                        if (depth == 0)
                        {
                            yield return text[start..(j + 1)];
                            i = j + 1;
                            goto next;
                        }
                        break;
                }
            }
            break;
            next:;
        }
    }

    /// <summary>
    /// Формирует блок описания инструментов для system-промпта.
    /// Язык блока — русский, чтобы модель отвечала в нужном формате.
    /// </summary>
    internal static string BuildToolPromptBlock(ToolRegistry tools)
    {
        if (tools == null || tools.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("### Доступные инструменты");
        sb.AppendLine("Для вызова инструмента ответь ТОЛЬКО JSON-блоком:");
        sb.AppendLine("```json");
        sb.AppendLine("{\"tool\": \"имя\", \"arguments\": {\"параметр\": \"значение\"}}");
        sb.AppendLine("```");
        sb.AppendLine("НЕ добавляй текст до или после JSON при вызове инструмента.");
        sb.AppendLine();

        foreach (var def in tools.GetDefinitions())
        {
            sb.AppendLine($"- **{def.Function.Name}**: {def.Function.Description}");
            if (def.Function.Parameters.HasValue)
                sb.AppendLine($"  Параметры: {def.Function.Parameters.Value}");
        }

        return sb.ToString();
    }
}
