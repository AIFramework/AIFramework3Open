using System.Text;
using System.Text.Json;
using AI.Charts.Data;
using AI.LLM.Agents;
using AI.LLM.Agents.Memory;
using AI.LLM.Agents.Multimodal;
using AI.LLM.Agents.Planning;
using AI.LLM.Agents.Tools;
using AI.LLM.Clients.OpenRouter;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Core.Models.Common.ToolCalling;
using AI.LLM.Integration.SemanticKernel.Extensions;
using AI.LLM.Services.LLM;
using AiFrameworkDemo.Core;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.Agents;

/// <summary>Диспетчер демо-сценариев AI.LLM.Agents.</summary>
public static class AgentsDemoRunner
{
    private static readonly string[] ModelNames =
    [
        "google/gemini-2.0-flash-001",
        "deepseek/deepseek-chat-v3-0324",
        "anthropic/claude-sonnet-4",
        "openai/gpt-4.1-mini",
    ];

    public static DemoResult Run(
        string key,
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp,
        DemoSettings s)
    {
        if (key == "plan_visualize")
        {
            try { return DoPlanVisualize(p, tp, s); }
            catch (Exception ex)
            {
                return new DemoResult { TextOutput = $"Ошибка: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}" };
            }
        }

        string txt;
        try
        {
            txt = key switch
            {
                "agent_basic"       => DoAgentBasic(p, tp),
                "agent_with_tools"  => DoAgentWithTools(p, tp),
                "agent_sk"          => DoAgentSK(p, tp),
                "agent_multimodal"  => DoAgentMultimodal(p, tp),
                "tool_registry"     => DoToolRegistry(),
                "tool_execution"    => DoToolExecution(tp),
                "memory_sliding"    => DoMemorySliding(p),
                "plan_generate"     => DoPlanGenerate(p, tp),
                "mcp_tools_list"    => DoMcpToolsCall(p, tp),
                _                   => $"Неизвестный ключ «{key}»",
            };
        }
        catch (Exception ex)
        {
            txt = $"Ошибка: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}";
        }

        return new DemoResult { TextOutput = txt };
    }

    private static string GetModel(IReadOnlyDictionary<string, double> p) =>
        ModelNames[Math.Clamp(I(p, "model", 0), 0, ModelNames.Length - 1)];

    private static string RequireApiKey(IReadOnlyDictionary<string, string> tp)
    {
        var key = T(tp, "_apikey", "").Trim();
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "API-ключ OpenRouter не указан.\n" +
                "Получите бесплатный ключ на https://openrouter.ai/keys " +
                "и вставьте в поле «API-ключ OpenRouter».");
        return key;
    }

    private static (LLMBase llm, string model) CreateLLM(
        IReadOnlyDictionary<string, double> p, IReadOnlyDictionary<string, string> tp)
    {
        var apiKey = RequireApiKey(tp);
        var model = GetModel(p);
        return (new LLMBase(new OpenRouterModelApi(apiKey, model)), model);
    }

    #region Агент (ReAct)

    private static string DoAgentBasic(IReadOnlyDictionary<string, double> p, IReadOnlyDictionary<string, string> tp)
    {
        var (llm, model) = CreateLLM(p, tp);
        var message = T(tp, "_message", "Объясни кратко, что такое ReAct-агент.");
        var temperature = N(p, "temperature", 1) / 10.0;

        var agent = AgentBuilder.Create()
            .WithLLM(llm)
            .WithSystemPrompt("Ты полезный ассистент. Отвечай кратко и по делу.")
            .WithTemperature(temperature)
            .WithMaxIterations(1)
            .Build();

        var result = agent.RunAsync(message).GetAwaiter().GetResult();

        var sb = new StringBuilder();
        sb.AppendLine("=== Базовый агент (без инструментов) ===");
        sb.AppendLine($"Модель: {model}");
        sb.AppendLine($"Шагов: {result.TotalSteps}, Время: {result.Elapsed.TotalSeconds:F1}с");
        sb.AppendLine();
        sb.AppendLine("--- Ответ ---");
        sb.AppendLine(result.Answer);
        sb.AppendLine();
        sb.AppendLine("--- Использование ---");
        sb.AppendLine(result.Usage.ToString());
        return sb.ToString();
    }

    private static string DoAgentWithTools(IReadOnlyDictionary<string, double> p, IReadOnlyDictionary<string, string> tp)
    {
        var (llm, model) = CreateLLM(p, tp);
        var message = T(tp, "_message", "Вычисли среднее и стандартное отклонение для чисел: 2, 5, 8, 11, 14, 17");

        var agent = AgentBuilder.Create()
            .WithLLM(llm)
            .WithSystemPrompt("Ты аналитик данных. ВСЕГДА используй доступные инструменты для вычислений. " +
                              "НИКОГДА не считай вручную — вызывай инструмент compute_statistics. Отвечай на русском.")
            .WithTools(new DemoStatisticsTools())
            .WithMaxIterations(5)
            .Build();

        return RunAgentAndFormat(agent, message, model, "Агент с инструментами (native FC)");
    }

    /// <summary>
    /// Демо: SK-интеграция через LLMClientChatCompletionService.
    /// LLM-вызовы проходят: SK -> LLMClientChatCompletionService -> ILLMClient -> ChatLLMApi.
    /// Биллинг полностью сохраняется.
    /// </summary>
    private static string DoAgentSK(IReadOnlyDictionary<string, double> p, IReadOnlyDictionary<string, string> tp)
    {
        var (llm, model) = CreateLLM(p, tp);
        var message = T(tp, "_message", "Вычисли статистику для чисел: 3, 7, 12, 5, 9");

        var kernel = Kernel.CreateBuilder()
            .AddSharpGPTChatCompletion(llm, model)
            .Build();

        var tools = new DemoStatisticsTools();
        kernel.Plugins.Add(ToolRegistry.FromObjects(tools).ToKernelPlugin());

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory("Ты аналитик данных. Используй инструменты для вычислений.");
        history.AddUserMessage(message);

        var result = chatService.GetChatMessageContentsAsync(history, kernel: kernel)
            .GetAwaiter().GetResult();

        var sb = new StringBuilder();
        sb.AppendLine("=== Агент через Semantic Kernel ===");
        sb.AppendLine($"Модель: {model}");
        sb.AppendLine();

        if (result.Count > 0)
        {
            var last = result[^1];
            sb.AppendLine("--- Ответ ---");
            sb.AppendLine(last.Content);

            if (last.Metadata != null)
            {
                sb.AppendLine();
                sb.AppendLine("--- Использование ---");

                if (last.Metadata.TryGetValue("Usage", out var usage) && usage is Usage u)
                {
                    sb.AppendLine($"  Токены        {u.TotalTokens:N0}  (prompt {u.PromptTokens:N0} + completion {u.CompletionTokens:N0})");
                    if (u.ReasoningTokens > 0)
                        sb.AppendLine($"  Reasoning     {u.ReasoningTokens:N0}");
                }

                if (last.Metadata.TryGetValue("Cost", out var costObj) && costObj is decimal cost && cost > 0)
                    sb.AppendLine($"  Стоимость     ${cost:F6}");
            }
        }

        return sb.ToString();
    }

    private static string RunAgentAndFormat(Agent agent, string message, string model, string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {title} ===");
        sb.AppendLine($"Модель: {model}");

        agent.OnToolExecuted += (_, r) =>
            sb.AppendLine($"  tool {r.ToolName}: {(r.IsSuccess ? "OK" : "ERR")} ({r.Elapsed.TotalMilliseconds:F0}ms)");

        var result = agent.RunAsync(message).GetAwaiter().GetResult();

        sb.AppendLine($"Шагов: {result.TotalSteps}, Время: {result.Elapsed.TotalSeconds:F1}с");
        sb.AppendLine();

        foreach (var step in result.Steps)
        {
            sb.AppendLine($"--- Шаг {step.StepNumber} ({step.FinishReason}) ---");
            if (step.ToolCalls != null)
                foreach (var tc in step.ToolCalls)
                    sb.AppendLine($"  -> {tc.Function?.Name}({tc.Function?.Arguments})");
            if (step.ToolResults != null)
                foreach (var tr in step.ToolResults)
                    sb.AppendLine($"  <- {tr.Content}");
            if (!string.IsNullOrEmpty(step.AssistantMessage))
                sb.AppendLine($"  {step.AssistantMessage}");
        }

        sb.AppendLine();
        sb.AppendLine("--- Финальный ответ ---");
        sb.AppendLine(result.Answer);
        sb.AppendLine();
        sb.AppendLine("--- Использование ---");
        sb.AppendLine(result.Usage.ToString());
        return sb.ToString();
    }

    #endregion

    #region Инструменты

    private static string DoToolRegistry()
    {
        var registry = ToolRegistry.FromObjects(new DemoStatisticsTools());

        var sb = new StringBuilder();
        sb.AppendLine("=== ToolRegistry: автосканирование [AgentTool] ===");
        sb.AppendLine($"Найдено инструментов: {registry.Count}");
        sb.AppendLine();

        foreach (var def in registry.GetDefinitions())
        {
            sb.AppendLine($"-- {def.Function.Name} --");
            sb.AppendLine($"   Описание: {def.Function.Description}");
            if (def.Function.Parameters.HasValue)
                sb.AppendLine($"   Schema: {JsonSerializer.Serialize(def.Function.Parameters.Value, new JsonSerializerOptions { WriteIndented = true })}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string DoToolExecution(IReadOnlyDictionary<string, string> tp)
    {
        var numbers = T(tp, "_numbers", "1, 3, 5, 7, 9, 11, 13");
        var registry = ToolRegistry.FromObjects(new DemoStatisticsTools());

        var toolCall = new ToolCall
        {
            Id = "demo_call_1",
            Type = "function",
            Function = new FunctionCall
            {
                Name = "compute_statistics",
                Arguments = JsonSerializer.Serialize(new { numbers })
            }
        };

        var result = registry.ExecuteAsync(toolCall).GetAwaiter().GetResult();

        return new StringBuilder()
            .AppendLine("=== Ручной вызов инструмента ===")
            .AppendLine($"Инструмент: {result.ToolName}")
            .AppendLine($"Аргументы: numbers = \"{numbers}\"")
            .AppendLine($"Успешно: {result.IsSuccess}")
            .AppendLine($"Время: {result.Elapsed.TotalMilliseconds:F1}ms")
            .AppendLine()
            .AppendLine("--- Результат ---")
            .AppendLine(result.Content)
            .ToString();
    }

    #endregion

    #region Память

    private static string DoMemorySliding(IReadOnlyDictionary<string, double> p)
    {
        int windowSize = I(p, "windowSize", 10);
        var memory = new SlidingWindowMemory(windowSize);

        var sb = new StringBuilder();
        sb.AppendLine($"=== SlidingWindowMemory (окно: {windowSize}) ===");
        sb.AppendLine();

        var pairs = new[]
        {
            ("Что такое C#?", "C# — объектно-ориентированный язык программирования от Microsoft."),
            ("А что такое .NET?", ".NET — платформа для разработки приложений."),
            ("Какие фреймворки есть?", "ASP.NET Core, WPF, MAUI, Blazor и другие."),
            ("Что такое LINQ?", "LINQ — Language Integrated Query, встроенные запросы в C#."),
            ("А Entity Framework?", "EF — ORM для работы с базами данных в .NET."),
        };

        foreach (var (q, a) in pairs)
        {
            memory.SaveInteractionAsync(q, a, null).GetAwaiter().GetResult();
            sb.AppendLine($"  + «{q}» -> «{a[..Math.Min(50, a.Length)]}...»");
        }

        sb.AppendLine();
        sb.AppendLine("--- Контекст для нового запроса ---");
        var ctx = memory.BuildContextAsync("Расскажи подробнее", "Ты помощник.").GetAwaiter().GetResult();

        foreach (var msg in ctx)
            sb.AppendLine($"  [{msg.Role}] {msg.Content?.ToString()?[..Math.Min(80, msg.Content?.ToString()?.Length ?? 0)]}");

        sb.AppendLine($"\nВсего сообщений в контексте: {ctx.Count}");
        return sb.ToString();
    }

    #endregion

    #region MCP

    private static readonly string[] McpToolNames = ["compute_statistics", "sum_numbers"];

    private static string DoMcpToolsCall(IReadOnlyDictionary<string, double> p, IReadOnlyDictionary<string, string> tp)
    {
        var registry = ToolRegistry.FromObjects(new DemoStatisticsTools());
        var toolIdx = Math.Clamp(I(p, "tool", 0), 0, McpToolNames.Length - 1);
        var toolName = McpToolNames[toolIdx];
        var args = T(tp, "_args", "2, 5, 8, 11, 14, 17");

        var sb = new StringBuilder();
        sb.AppendLine("=== MCP-инструменты ([AgentTool]) ===");
        sb.AppendLine();

        foreach (var def in registry.GetDefinitions())
        {
            var marker = def.Function.Name == toolName ? ">" : "*";
            sb.AppendLine($"  {marker} {def.Function.Name}");
            sb.AppendLine($"    {def.Function.Description}");

            if (def.Function.Parameters.HasValue)
            {
                var schema = def.Function.Parameters.Value;
                if (schema.TryGetProperty("properties", out var props))
                {
                    foreach (var prop in props.EnumerateObject())
                    {
                        var desc = prop.Value.TryGetProperty("description", out var d) ? d.GetString() : "";
                        var type = prop.Value.TryGetProperty("type", out var t) ? t.GetString() : "any";
                        sb.AppendLine($"      + {prop.Name} ({type}): {desc}");
                    }
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine($"--- Вызов: {toolName}(\"{args}\") ---");

        var toolCall = new ToolCall
        {
            Id = $"mcp_demo_{Guid.NewGuid():N}",
            Type = "function",
            Function = new FunctionCall
            {
                Name = toolName,
                Arguments = JsonSerializer.Serialize(new { numbers = args })
            }
        };

        var result = registry.ExecuteAsync(toolCall).GetAwaiter().GetResult();

        sb.AppendLine($"Статус: {(result.IsSuccess ? "OK" : "Ошибка")}");
        sb.AppendLine($"Время: {result.Elapsed.TotalMilliseconds:F1} мс");
        sb.AppendLine();
        sb.AppendLine("--- Результат ---");
        sb.AppendLine(result.Content);
        sb.AppendLine();
        sb.AppendLine("--- Эквивалент MCP-вызова (JSON-RPC) ---");
        sb.AppendLine("{");
        sb.AppendLine("  \"jsonrpc\": \"2.0\",");
        sb.AppendLine("  \"method\": \"tools/call\",");
        sb.AppendLine("  \"params\": {");
        sb.AppendLine($"    \"name\": \"{toolName}\",");
        sb.AppendLine($"    \"arguments\": {{ \"numbers\": \"{args}\" }}");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region Мультимодальный агент

    private static string DoAgentMultimodal(IReadOnlyDictionary<string, double> p, IReadOnlyDictionary<string, string> tp)
    {
        var (llm, model) = CreateLLM(p, tp);
        var message = T(tp, "_message", "Опиши что ты видишь на изображении и вычисли площадь прямоугольника 640×480");

        var testImage = GenerateTestImage();
        var observer = new DemoObservationProvider();

        var agent = AgentBuilder.Create()
            .WithLLM(llm)
            .WithSystemPrompt(
                "Ты мультимодальный ассистент с Vision. Ты можешь анализировать изображения " +
                "и использовать инструменты. После каждого действия ты получаешь наблюдение среды. " +
                "Отвечай на русском.")
            .WithTools(new DemoMultimodalTools())
            .WithObserver(observer)
            .WithMaxIterations(5)
            .Build();

        var query = new AgentQuery(message, new AgentImage(testImage, "image/png", "test_pattern"));
        var result = agent.RunAsync(query).GetAwaiter().GetResult();

        var sb = new StringBuilder();
        sb.AppendLine("=== Мультимодальный агент (Observe-Reason-Act) ===");
        sb.AppendLine($"Модель: {model}");
        sb.AppendLine($"Шагов: {result.TotalSteps}, Время: {result.Elapsed.TotalSeconds:F1}с");
        sb.AppendLine($"Наблюдений: {result.Steps.Count(s => s.Observation != null)}");
        sb.AppendLine();

        foreach (var step in result.Steps)
        {
            sb.AppendLine($"--- Шаг {step.StepNumber} ({step.FinishReason}) ---");
            if (step.ToolCalls != null)
                foreach (var tc in step.ToolCalls)
                    sb.AppendLine($"  -> {tc.Function?.Name}({tc.Function?.Arguments})");
            if (step.ToolResults != null)
                foreach (var tr in step.ToolResults)
                {
                    sb.AppendLine($"  <- {tr.Content}");
                    if (tr.HasImages)
                        sb.AppendLine($"     + {tr.Images.Count} изображение(й)");
                }
            if (step.Observation != null)
                sb.AppendLine($"  Наблюдение: {step.Observation.Description} ({step.Observation.Images.Count} изобр.)");
            if (!string.IsNullOrEmpty(step.AssistantMessage))
                sb.AppendLine($"  {step.AssistantMessage}");
        }

        sb.AppendLine();
        sb.AppendLine("--- Финальный ответ ---");
        sb.AppendLine(result.Answer);
        sb.AppendLine();
        sb.AppendLine("--- Использование ---");
        sb.AppendLine(result.Usage.ToString());
        return sb.ToString();
    }

    /// <summary>Генерирует минимальный PNG 2x2 (тестовый паттерн).</summary>
    private static byte[] GenerateTestImage()
    {
        // Минимальный валидный PNG 2×2 пикселя (красный квадрат)
        var header = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var ms = new System.IO.MemoryStream();
        ms.Write(header);

        WriteChunk(ms, "IHDR",
        [
            0, 0, 0, 2,   // width = 2
            0, 0, 0, 2,   // height = 2
            8,             // bit depth
            2,             // color type = RGB
            0, 0, 0        // compression, filter, interlace
        ]);

        var rawData = new byte[]
        {
            0, 255, 0, 0, 255, 0, 0,    // filter=0, row1: red, red
            0, 0, 0, 255, 0, 0, 255     // filter=0, row2: blue, blue
        };

        using var deflateMs = new System.IO.MemoryStream();
        using (var deflate = new System.IO.Compression.DeflateStream(deflateMs, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(rawData);

        var compressed = deflateMs.ToArray();
        var zlibData = new byte[compressed.Length + 6];
        zlibData[0] = 0x78; zlibData[1] = 0x9C;
        Array.Copy(compressed, 0, zlibData, 2, compressed.Length);
        var adler = Adler32(rawData);
        zlibData[^4] = (byte)(adler >> 24);
        zlibData[^3] = (byte)(adler >> 16);
        zlibData[^2] = (byte)(adler >> 8);
        zlibData[^1] = (byte)(adler);

        WriteChunk(ms, "IDAT", zlibData);
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();

        static void WriteChunk(System.IO.Stream s, string type, byte[] data)
        {
            var len = BitConverter.GetBytes(data.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(len);
            s.Write(len);
            var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(typeBytes);
            s.Write(data);
            var crcData = new byte[typeBytes.Length + data.Length];
            typeBytes.CopyTo(crcData, 0);
            data.CopyTo(crcData, typeBytes.Length);
            var crc = BitConverter.GetBytes(Crc32(crcData));
            if (BitConverter.IsLittleEndian) Array.Reverse(crc);
            s.Write(crc);
        }

        static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (var b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                    crc = (crc >> 1) ^ (crc & 1) * 0xEDB88320;
            }
            return crc ^ 0xFFFFFFFF;
        }

        static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;
            foreach (var d in data)
            {
                a = (a + d) % 65521;
                b = (b + a) % 65521;
            }
            return (b << 16) | a;
        }
    }

    /// <summary>
    /// Демо-реализация IObservationProvider: эмулирует наблюдение среды.
    /// В реальном проекте — скриншот рабочего стола или камера робота.
    /// </summary>
    private sealed class DemoObservationProvider : IObservationProvider
    {
        private int _callCount;

        public Task<AgentObservation> ObserveAsync(CancellationToken cancellationToken = default)
        {
            _callCount++;
            var img = GenerateTestImage();
            return Task.FromResult(new AgentObservation(
                new AgentImage(img, "image/png", $"observation_{_callCount}"),
                $"Эмуляция наблюдения #{_callCount} (демо-паттерн 2×2 px)"));
        }
    }

    #endregion

    #region Планирование

    private static string DoPlanGenerate(IReadOnlyDictionary<string, double> p, IReadOnlyDictionary<string, string> tp)
    {
        var (llm, model) = CreateLLM(p, tp);
        var goal = T(tp, "_goal", "Разработай и протестируй REST API для интернет-магазина книг");
        int maxSteps = I(p, "maxSteps", 15);

        var builder = PlanGeneratorBuilder.Create()
            .WithLLM(llm)
            .WithTools(new DemoStatisticsTools())
            .WithMaxSteps(maxSteps);

        var skillText = T(tp, "_skill", "");
        if (!string.IsNullOrWhiteSpace(skillText))
            builder.WithSkill(new Skill("user_skill", skillText));

        var planner = builder.Build();
        var plan = planner.GenerateAsync(goal).GetAwaiter().GetResult();

        var sb = new StringBuilder();
        sb.AppendLine("=== Генератор планов (LLM + алгоритм Кана) ===");
        sb.AppendLine($"Модель: {model}");
        sb.AppendLine($"Задача: {goal}");
        sb.AppendLine($"Шагов: {plan.Steps.Count}, Ярусов: {plan.Depth}");

        if (plan.HasCycle)
        {
            sb.AppendLine("ОБНАРУЖЕН ЦИКЛ В ЗАВИСИМОСТЯХ — план невалиден");
            return sb.ToString();
        }

        sb.AppendLine();

        foreach (var tier in plan.Tiers)
        {
            sb.AppendLine($"--- Ярус {tier.Level} ({tier.Steps.Count} шагов, параллельно) ---");
            foreach (var step in tier.Steps)
            {
                var toolInfo = step.ToolName != null ? $" [tool: {step.ToolName}]" : "";
                var depsInfo = step.DependsOn.Count > 0 ? $" (после: {string.Join(", ", step.DependsOn)})" : "";
                sb.AppendLine($"  {step.Id}: {step.Description}{toolInfo}{depsInfo}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("--- Граф зависимостей ---");
        foreach (var step in plan.Steps)
        {
            if (step.DependsOn.Count > 0)
                sb.AppendLine($"  {string.Join(", ", step.DependsOn)} -> {step.Id}");
        }

        sb.AppendLine();
        sb.AppendLine("--- Использование ---");
        sb.AppendLine(plan.Usage.ToString());
        return sb.ToString();
    }

    private static DemoResult DoPlanVisualize(
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp,
        DemoSettings s)
    {
        var (llm, model) = CreateLLM(p, tp);
        var goal = T(tp, "_goal", "Создай веб-приложение для чата с авторизацией и базой данных");
        int maxSteps = I(p, "maxSteps", 10);

        var planner = PlanGeneratorBuilder.Create()
            .WithLLM(llm)
            .WithTools(new DemoStatisticsTools())
            .WithMaxSteps(maxSteps)
            .Build();

        var plan = planner.GenerateAsync(goal).GetAwaiter().GetResult();

        var sb = new StringBuilder();
        sb.AppendLine("=== Визуализация плана ===");
        sb.AppendLine($"Модель: {model}");
        sb.AppendLine($"Задача: {goal}");
        sb.AppendLine($"Шагов: {plan.Steps.Count}, Ярусов: {plan.Depth}");

        if (plan.HasCycle)
        {
            sb.AppendLine("ЦИКЛ В ЗАВИСИМОСТЯХ — визуализация невозможна");
            return new DemoResult { TextOutput = sb.ToString() };
        }

        sb.AppendLine();
        sb.AppendLine(PlanTreeVisualizer.ToText(plan));
        sb.AppendLine();
        sb.AppendLine("--- SVG ---");
        sb.AppendLine(PlanTreeVisualizer.ToSvg(plan));
        sb.AppendLine();
        sb.AppendLine("--- Mermaid ---");
        sb.AppendLine(PlanTreeVisualizer.ToMermaid(plan));
        sb.AppendLine();
        sb.AppendLine("--- Использование ---");
        sb.AppendLine(plan.Usage.ToString());

        var (steps, edges) = PlanTreeVisualizer.ExtractGraphLayout(plan);
        var graphData = GraphData.CreateTieredLayout(steps, edges);

        var cv = MakeView(s);
        cv.ChartName = $"План: {Truncate(goal, 50)}";
        cv.LabelX = "";
        cv.LabelY = "";
        cv.AddGraph(graphData);

        return Png(cv, s, textOutput: sb.ToString());
    }

    private static string Truncate(string text, int max)
        => text != null && text.Length > max ? text[..(max - 1)] + "…" : text ?? "";

    #endregion

    #region Demo tools

    private sealed class DemoMultimodalTools
    {
        [AgentTool("compute_area", "Вычисляет площадь прямоугольника по ширине и высоте")]
        public string ComputeArea(
            [ToolParameter("Ширина", Required = true)] double width,
            [ToolParameter("Высота", Required = true)] double height)
        {
            return $"Площадь = {width * height:F2} (пикселей² / ед²)";
        }

        [AgentTool("describe_colors", "Описывает цвета изображения по палитре RGB")]
        public ToolResult DescribeColors(
            [ToolParameter("Описание палитры")] string palette = "red, blue")
        {
            return new ToolResult(
                $"Палитра: {palette}. Изображение содержит тестовый паттерн.",
                new AgentImage(GenerateTestImage(), "image/png", "palette_analysis"));
        }
    }

    private sealed class DemoStatisticsTools
    {
        [AgentTool("compute_statistics", "Вычисляет описательную статистику для числового ряда")]
        public string ComputeStats(
            [ToolParameter("Числа через запятую")] string numbers)
        {
            var values = numbers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(double.Parse).ToArray();
            var vector = new AI.DataStructs.Algebraic.Vector(values);
            var stat = new AI.Statistics.Statistic(vector);
            return $"n={values.Length}, μ={stat.Expected:F4}, σ={stat.STD:F4}, " +
                   $"min={stat.MinValue:F4}, max={stat.MaxValue:F4}";
        }

        [AgentTool("sum_numbers", "Вычисляет сумму чисел")]
        public string SumNumbers(
            [ToolParameter("Числа через запятую")] string numbers)
        {
            var values = numbers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(double.Parse);
            return $"Сумма = {values.Sum():F4}";
        }
    }

    #endregion
}
