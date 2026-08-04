using System.Text;
using System.Text.Json;
using AI.Algorithms.EWG;
using AI.Algorithms.GraphStructure;
using AI.LLM.Agents.Tools;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using Serilog;

namespace AI.LLM.Agents.Planning;

/// <summary>
/// Генератор планов на основе LLM с ярусной декомпозицией алгоритмом Кана.
/// <para>
/// Цикл: Задача + скилы + инструменты -> LLM (JSON) -> парсинг -> DAG ->
/// <see cref="TopologicalSort"/> -> ярусы (<see cref="PlanTier"/>).
/// </para>
/// </summary>
public sealed class PlanGenerator
{
    private readonly ILLMClient _llm;
    private readonly ToolRegistry _tools;
    private readonly List<Skill> _skills;
    private readonly PlanGeneratorConfig _config;

    internal PlanGenerator(ILLMClient llm, ToolRegistry tools,
        IReadOnlyList<Skill> skills, PlanGeneratorConfig config)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _tools = tools;
        _skills = skills != null ? [.. skills] : [];
        _config = config ?? new PlanGeneratorConfig();
    }

    /// <summary>
    /// Генерирует план выполнения задачи через LLM, разбивает на ярусы алгоритмом Кана.
    /// </summary>
    public Task<PlanTree> GenerateAsync(
        string goal,
        IReadOnlyList<Skill> additionalSkills = null,
        CancellationToken ct = default)
        => GenerateAsync(goal, additionalSkills, null, ct);

    /// <summary>
    /// Генерирует план, используя инструменты <paramref name="toolsOverride"/> вместо заданных
    /// при сборке генератора.
    /// </summary>
    /// <param name="toolsOverride">
    /// Инструменты текущей задачи. <c>null</c> — берутся инструменты сборки. Пустой реестр —
    /// осознанное «инструментов нет»: план будет из шагов без привязки к <c>tool</c>.
    /// </param>
    /// <remarks>
    /// Перекрытие пер-задачное и НЕ меняет состояние генератора: один экземпляр можно звать
    /// параллельно с разными наборами инструментов. Набор обязан совпадать с тем, что реально
    /// доступно исполнителю, — иначе планировщик назначит шаг на инструмент, которого у
    /// исполняющего агента нет.
    /// </remarks>
    public async Task<PlanTree> GenerateAsync(
        string goal,
        IReadOnlyList<Skill> additionalSkills,
        ToolRegistry toolsOverride,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(goal))
            throw new ArgumentException("Задача не может быть пустой.", nameof(goal));

        var usage = new AgentUsage();
        var allSkills = CombineSkills(additionalSkills);
        var messages = BuildMessages(goal, allSkills, toolsOverride ?? _tools);
        var settings = new GenerateSettings(
            temperature: _config.Temperature,
            maxTokens: _config.MaxTokens)
        {
            ResponseFormat = ResponseFormat.CreateJsonObject()
        };

        var response = await _llm.SendFullAsync(messages, settings, ct).ConfigureAwait(false);
        usage.AddLlmUsage(response?.Usage);

        var content = response?.Choices is { Count: > 0 }
            ? response.Choices[0].Message?.Content?.ToString()
            : null;

        if (string.IsNullOrWhiteSpace(content))
        {
            Log.Warning("PlanGenerator: LLM вернул пустой ответ");
            return new PlanTree(goal, [], [], false, usage);
        }

        var steps = ParseSteps(content);

        if (steps.Count == 0)
        {
            Log.Warning("PlanGenerator: не удалось распарсить шаги из ответа LLM");
            return new PlanTree(goal, [], [], false, usage);
        }

        var (tiers, hasCycle) = BuildTiers(steps);

        return new PlanTree(goal, steps, tiers, hasCycle, usage);
    }

    #region Промпт

    private List<LLMMessage> BuildMessages(string goal, List<Skill> skills, ToolRegistry tools)
    {
        var systemPrompt = BuildSystemPrompt(skills, tools);
        return
        [
            LLMMessage.CreateMessage(Roles.System, systemPrompt),
            LLMMessage.CreateMessage(Roles.User, goal)
        ];
    }

    private string BuildSystemPrompt(List<Skill> skills, ToolRegistry tools)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ты — планировщик задач. Разбей задачу пользователя на конкретные шаги.");
        sb.AppendLine("Каждый шаг может зависеть от других шагов (выполняться после них).");
        sb.AppendLine("Если шаги независимы друг от друга — НЕ указывай зависимости, они будут выполнены параллельно.");
        sb.AppendLine();
        sb.AppendLine("ПРАВИЛА:");
        sb.AppendLine($"- Максимум {_config.MaxSteps} шагов");
        sb.AppendLine("- Каждый шаг имеет уникальный id (step_0, step_1, ...)");
        sb.AppendLine("- depends_on — массив id шагов, которые ДОЛЖНЫ быть выполнены ДО этого шага");
        sb.AppendLine("- Если для шага есть подходящий инструмент — укажи его в поле tool и аргументы в args");
        sb.AppendLine("- Если подходящего инструмента нет — оставь tool = null");
        sb.AppendLine("- done_when — КРИТЕРИЙ ГОТОВНОСТИ шага: по чему видно, что он СДЕЛАН, а не начат.");
        sb.AppendLine("  Пиши проверяемый признак РЕЗУЛЬТАТА, а не пересказ задачи:");
        sb.AppendLine("  плохо: «эссе написано»; хорошо: «в ответе есть готовый текст эссе не короче 2000 знаков».");
        sb.AppendLine("  План работы, «приступаю» и обещания сделать позже критерию НЕ удовлетворяют.");
        sb.AppendLine("  done_when обязателен для КАЖДОГО шага.");
        sb.AppendLine("- outputs — что шаг передаёт дальше: {\"имя_порта_инструмента\": \"идентификатор_артефакта\"}");
        sb.AppendLine("- input_mapping — откуда шаг берёт данные: {\"имя_порта_инструмента\": \"источник\"}");
        sb.AppendLine("- Источник — либо \"step_X.outputs.порт\" (шаг step_X ОБЯЗАН быть в depends_on), либо \"user_context.ключ\"");

        if (tools is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("### Доступные инструменты");
            foreach (var def in tools.GetDefinitions())
            {
                sb.AppendLine($"- **{def.Function.Name}**: {def.Function.Description}");
                if (def.Function.Parameters.HasValue)
                    sb.AppendLine($"  Параметры: {def.Function.Parameters.Value}");
            }
        }

        if (skills.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Доступные навыки (инструкции)");
            foreach (var skill in skills)
            {
                sb.AppendLine($"- **{skill.Name}**: {skill.Description}");
            }
        }

        if (!string.IsNullOrWhiteSpace(_config.PortsPrompt))
        {
            sb.AppendLine();
            sb.AppendLine("### Порты инструментов и правила соединения");
            sb.AppendLine(_config.PortsPrompt.Trim());
        }

        sb.AppendLine();
        sb.AppendLine("Ответь СТРОГО в JSON-формате:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"steps\": [");
        sb.AppendLine("    {\"id\": \"step_0\", \"description\": \"...\", \"tool\": \"essay_writer\", \"args\": {},");
        sb.AppendLine("     \"done_when\": \"в ответе есть готовый текст эссе, а не план и не обещание\",");
        sb.AppendLine("     \"depends_on\": [], \"outputs\": {\"essay\": \"artifact_essay_1\"}, \"input_mapping\": {\"task\": \"user_context.message\"}},");
        sb.AppendLine("    {\"id\": \"step_1\", \"description\": \"...\", \"tool\": \"publisher\", \"args\": {},");
        sb.AppendLine("     \"done_when\": \"в ответе есть подтверждение отправки в канал\",");
        sb.AppendLine("     \"depends_on\": [\"step_0\"], \"outputs\": {},");
        sb.AppendLine("     \"input_mapping\": {\"content\": \"step_0.outputs.essay\"}}");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }

    #endregion

    #region Парсинг

    private static List<PlanStep> ParseSteps(string json)
    {
        try
        {
            json = ExtractJsonBlock(json);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("steps", out var stepsEl) || stepsEl.ValueKind != JsonValueKind.Array)
                return [];

            var steps = new List<PlanStep>();
            foreach (var el in stepsEl.EnumerateArray())
            {
                // id/description могут быть null или не-строкой — тогда используем fallback,
                // иначе null Id уронит построение ярусов (ArgumentNullException в словаре)
                var id = el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                    ? idEl.GetString() : null;
                var description = el.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String
                    ? descEl.GetString() : null;

                var doneWhen = el.TryGetProperty("done_when", out var doneEl) && doneEl.ValueKind == JsonValueKind.String
                    ? doneEl.GetString() : null;

                var step = new PlanStep
                {
                    Id = !string.IsNullOrWhiteSpace(id) ? id : $"step_{steps.Count}",
                    Description = !string.IsNullOrWhiteSpace(description) ? description : "",
                    DoneWhen = doneWhen?.Trim() ?? "",
                    ToolName = el.TryGetProperty("tool", out var toolEl) && toolEl.ValueKind == JsonValueKind.String
                        ? toolEl.GetString() : null,
                };

                if (el.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in argsEl.EnumerateObject())
                        step.ToolArguments[prop.Name] = prop.Value.ToString();
                }

                ReadStringMap(el, "outputs", step.Outputs);
                ReadStringMap(el, "input_mapping", step.InputMapping);

                if (el.TryGetProperty("depends_on", out var depsEl) && depsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var dep in depsEl.EnumerateArray())
                    {
                        // Не-строковый элемент (число/объект) пропускаем,
                        // иначе GetString() выбросит исключение и весь план будет потерян
                        if (dep.ValueKind != JsonValueKind.String)
                        {
                            Log.Warning("PlanGenerator: элемент depends_on имеет тип {Kind}, пропущен", dep.ValueKind);
                            continue;
                        }

                        var depId = dep.GetString();
                        if (!string.IsNullOrEmpty(depId))
                            step.DependsOn.Add(depId);
                    }
                }

                steps.Add(step);
            }

            return steps;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PlanGenerator: ошибка парсинга JSON плана");
            return [];
        }
    }

    /// <summary>
    /// Читает объект вида {"порт": "значение"} в словарь, пропуская не-строковые значения.
    /// </summary>
    /// <remarks>
    /// Модель регулярно кладёт в такие карты объекты и числа. GetString() на них бросает
    /// исключение, а оно в ParseSteps означает потерю ВСЕГО плана — поэтому пропускаем
    /// поэлементно и с предупреждением.
    /// </remarks>
    private static void ReadStringMap(JsonElement element, string propertyName, Dictionary<string, string> target)
    {
        if (!element.TryGetProperty(propertyName, out var mapEl) || mapEl.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in mapEl.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String)
            {
                Log.Warning("PlanGenerator: {Property}.{Key} имеет тип {Kind}, пропущен",
                    propertyName, prop.Name, prop.Value.ValueKind);
                continue;
            }

            var value = prop.Value.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                target[prop.Name] = value;
        }
    }

    /// <summary>Извлекает JSON из markdown code fence если нужно.</summary>
    private static string ExtractJsonBlock(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];
        return text;
    }

    #endregion

    #region Ярусная декомпозиция (алгоритм Кана)

    /// <summary>
    /// Строит DAG из шагов, выполняет <see cref="TopologicalSort"/> (Кан),
    /// и разбивает на ярусы по глубине зависимостей.
    /// </summary>
    private static (List<PlanTier> Tiers, bool HasCycle) BuildTiers(List<PlanStep> steps)
    {
        var indexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < steps.Count; i++)
            indexById[steps[i].Id] = i;

        var graph = new Graph(steps.Count);
        foreach (var step in steps)
        {
            if (!indexById.TryGetValue(step.Id, out var toIdx)) continue;
            foreach (var dep in step.DependsOn)
            {
                if (indexById.TryGetValue(dep, out var fromIdx))
                    graph.AddArc(fromIdx, toIdx);
            }
        }

        var topo = new TopologicalSort(graph);
        if (topo.HasCycle)
        {
            Log.Warning("PlanGenerator: обнаружен цикл в зависимостях плана");
            return ([], true);
        }

        // Вычисляем ярус каждой вершины: level[v] = max(level[dep] + 1)
        var level = new int[steps.Count];
        foreach (var v in topo.Order)
        {
            foreach (var u in graph.Adj(v))
                level[u] = Math.Max(level[u], level[v] + 1);
        }

        // Проставляем ярус в шаги и группируем
        for (int i = 0; i < steps.Count; i++)
            steps[i].Tier = level[i];

        var tiers = level
            .Select((lvl, idx) => (lvl, step: steps[idx]))
            .GroupBy(x => x.lvl)
            .OrderBy(g => g.Key)
            .Select(g => new PlanTier(g.Key, g.Select(x => x.step).ToList()))
            .ToList();

        return (tiers, false);
    }

    #endregion

    private List<Skill> CombineSkills(IReadOnlyList<Skill> additional)
    {
        if (additional is not { Count: > 0 }) return _skills;
        var combined = new List<Skill>(_skills);
        combined.AddRange(additional);
        return combined;
    }
}

/// <summary>Конфигурация генератора планов.</summary>
public sealed class PlanGeneratorConfig
{
    /// <summary>Максимальное количество шагов в плане.</summary>
    public int MaxSteps { get; set; } = 20;

    /// <summary>Температура генерации (0..2). Низкая — более детерминированный план.</summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>Максимальное число токенов в ответе LLM.</summary>
    public int? MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Справочник портов инструментов и правила их соединения — вставляется в системный промпт
    /// отдельной секцией. <c>null</c>/пусто — секции нет, план строится без маппинга данных.
    /// </summary>
    /// <remarks>
    /// Текст задаёт ПРИЛОЖЕНИЕ, а не библиотека: онтология данных (сложный тип, семантика,
    /// область знаний) принадлежит конкретной системе агентов, и зашивать её сюда значило бы
    /// навязать её всем потребителям. Библиотека отвечает лишь за то, чтобы справочник дошёл
    /// до модели, а маппинг вернулся распарсенным (<see cref="PlanStep.InputMapping"/>).
    /// </remarks>
    public string PortsPrompt { get; set; }
}
