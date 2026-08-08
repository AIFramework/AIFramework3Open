using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using AI.LLM.Clients.OpenRouter;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Services.LLM;
using AiFrameworkDemo.Core;
using System.Globalization;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.LLM;

public static partial class LlmDemoRunner
{
    /// <summary>
    /// Цикл ReAct: Рассуждение → Действие → Наблюдение.
    ///
    /// Инструменты здесь намеренно ДЕТЕРМИНИРОВАННЫЕ и локальные (калькулятор,
    /// справочник, текущая дата): демо должно показывать сам цикл и его след,
    /// а не качество внешнего поиска. Всё, что меняется от запуска к запуску, —
    /// решения модели, и именно они попадают в таблицу шагов.
    /// </summary>
    private static string DoReAct(
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp,
        ReportBuilder rep)
    {
        var apiKey    = RequireApiKey(tp);
        var model     = GetModel(p);
        int maxIter   = Math.Clamp(I(p, "maxIterations", 6), 1, 15);
        int policyIdx = I(p, "policy", 0);
        var question  = T(tp, "_question",
            "Сколько будет 17 * 23, и какой сегодня день недели? Ответь одним предложением.");

        var options = new LLMOptions
        {
            ApiKey      = apiKey,
            ModelName   = model,
            Temperature = 0.2,
        };
        var client = new LLMWithOpenRouterClient(options);
        var settings = new GenerateSettings { Temperature = 0.2, MaxTokens = 800 };

        // -- Инструменты ------------------------------------------------
        int calcCalls = 0, dateCalls = 0, factCalls = 0;

        var calculator = DelegateReActTool.FromText(
            "calculator",
            "Вычисляет арифметическое выражение. Аргумент — выражение, например \"17 * 23\".",
            (expr, ct) =>
            {
                calcCalls++;
                return Task.FromResult(EvalArithmetic(expr));
            });

        var dateTool = DelegateReActTool.FromText(
            "current_date",
            "Возвращает текущую дату и день недели. Аргумент не нужен.",
            (_, ct) =>
            {
                dateCalls++;
                var now = DateTime.Now;
                return Task.FromResult(
                    $"{now:yyyy-MM-dd}, день недели: {now.ToString("dddd", new CultureInfo("ru-RU"))}");
            });

        var factTool = DelegateReActTool.FromText(
            "framework_facts",
            "Справочник по AIFramework: отвечает на вопросы о модулях библиотеки. Аргумент — тема.",
            (topic, ct) =>
            {
                factCalls++;
                return Task.FromResult(LookupFact(topic));
            });

        var builder = ReActAgentBuilder.Create()
            .WithSystemPrompt(
                "Ты аккуратный помощник. Пользуйся инструментами вместо того, чтобы " +
                "считать в уме. Отвечай кратко и по-русски.")
            .WithTool(calculator)
            .WithTool(dateTool)
            .WithTool(factTool)
            .WithMaxIterations(maxIter)
            .WithLlmSynthesis(client, settings);

        // Нативный function calling поддерживают не все модели; структурированный
        // JSON работает с любой, но требует более дисциплинированного ответа.
        builder = policyIdx == 1
            ? builder.WithStructuredJson(client, settings)
            : builder.WithNativeToolCalling(client, settings);

        var engine = builder.Build();

        var sb = new StringBuilder();
        sb.AppendLine("> Цикл ReAct — Рассуждение → Действие → Наблюдение");
        sb.AppendLine();
        sb.AppendLine($"  Модель:    {model}");
        sb.AppendLine($"  Политика:  {(policyIdx == 1 ? "StructuredJson" : "NativeToolCalling")}");
        sb.AppendLine($"  Лимит шагов: {maxIter}");
        sb.AppendLine();
        sb.AppendLine("- Запрос");
        sb.AppendLine(question);
        sb.AppendLine();

        ReActResult result;
        try
        {
            result = engine.RunAsync(new ReActQuery(question)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            sb.AppendLine("- Ошибка запуска цикла");
            sb.AppendLine(ex.Message);
            return sb.ToString();
        }

        // -- Метрики ----------------------------------------------------
        bool ok = result.HasAnswer && !result.Failed;
        rep.Metric("Причина остановки", StopReasonRu(result.StopReason),
                   hint: "FinalAnswer — модель сама сочла данные достаточными",
                   tone: result.StopReason == ReActStopReason.FinalAnswer ? MetricTone.Good
                       : result.Failed ? MetricTone.Bad : MetricTone.Warn)
           .Metric("Шагов цикла", result.TotalSteps, hint: $"Лимит {maxIter}",
                   tone: result.TotalSteps >= maxIter ? MetricTone.Warn : MetricTone.Neutral)
           .Metric("Вызовов инструментов", result.Usage.ToolCalls,
                   hint: $"Успешных: {result.Usage.ToolCallsSucceeded}",
                   tone: result.Usage.ToolCalls == 0 ? MetricTone.Warn : MetricTone.Good)
           .Metric("Обращений к модели", result.Usage.LlmCalls)
           .Metric("Токенов", result.Usage.TotalTokens,
                   hint: $"Промпт {result.Usage.PromptTokens}, генерация {result.Usage.CompletionTokens}")
           .Metric("Время", result.Elapsed.TotalMilliseconds, "мс", format: "F0",
                   tone: ok ? MetricTone.Good : MetricTone.Neutral)
           .Note("Каждый шаг — это одно обращение к модели плюс вызовы инструментов, которые она " +
                 "запросила. Пустой список действий на шаге означает, что модель решила отвечать " +
                 "без инструментов.");

        rep.Table("Итог", ["Что", "Значение"], numeric: [false, false])
           .Row("Ответ", result.HasAnswer ? result.Answer : "(пусто)")
           .Row("Причина остановки", result.StopReason.ToString())
           .Row("Ошибка движка", result.Error ?? "—");

        if (result.Steps.Count > 0)
        {
            var stepsTable = rep.Table("След цикла (trace)",
                ["Шаг", "Рассуждение", "Инструмент", "Аргументы", "Наблюдение", "Ок"],
                numeric: [true, false, false, false, false, false],
                note: "Именно этот след отличает ReAct от обычного вызова инструментов: " +
                      "каждое решение модели видно и воспроизводимо.");

            foreach (var step in result.Steps)
            {
                if (step.Actions.Count == 0)
                {
                    stepsTable.Row(step.Number.ToString(), Trim(step.Thought, 160),
                                   "—", "—", step.Note ?? "(без действий)", step.Ok ? "да" : "нет");
                    continue;
                }

                for (int a = 0; a < step.Actions.Count; a++)
                {
                    var act = step.Actions[a];
                    var obs = a < step.Observations.Count ? step.Observations[a] : null;
                    stepsTable.Row(
                        a == 0 ? step.Number.ToString() : "",
                        a == 0 ? Trim(step.Thought, 160) : "",
                        act.ToolName,
                        Trim(act.Arguments, 80),
                        Trim(obs?.Text, 160),
                        obs is null ? "—" : obs.Ok ? "да" : "нет");
                }
            }
        }

        rep.Table("Использование инструментов",
                ["Инструмент", "Вызовов", "Назначение"], numeric: [false, true, false])
           .Row("calculator",      calcCalls.ToString(), "Арифметика вместо счёта в уме")
           .Row("current_date",    dateCalls.ToString(), "Текущая дата и день недели")
           .Row("framework_facts", factCalls.ToString(), "Справочник по модулям AIFramework");

        // -- Текстовый лог ----------------------------------------------
        sb.AppendLine("- След цикла");
        foreach (var step in result.Steps)
        {
            sb.AppendLine($"  Шаг {step.Number}:");
            if (!string.IsNullOrWhiteSpace(step.Thought))
                sb.AppendLine($"    Рассуждение: {Trim(step.Thought, 300)}");

            for (int a = 0; a < step.Actions.Count; a++)
            {
                var act = step.Actions[a];
                sb.AppendLine($"    Действие:    {act.ToolName}({Trim(act.Arguments, 120)})");
                if (a < step.Observations.Count)
                    sb.AppendLine($"    Наблюдение:  {Trim(step.Observations[a].Text, 300)}");
            }

            if (!string.IsNullOrWhiteSpace(step.Note))
                sb.AppendLine($"    Пометка:     {step.Note}");
        }

        sb.AppendLine();
        sb.AppendLine("- Ответ");
        sb.AppendLine(result.HasAnswer ? result.Answer : "(пустой ответ)");
        sb.AppendLine();
        sb.AppendLine($"  Причина остановки: {result.StopReason}");
        sb.AppendLine($"  Шагов: {result.TotalSteps}, вызовов инструментов: {result.Usage.ToolCalls}");
        sb.AppendLine($"  Токенов: {result.Usage.TotalTokens}, время: {result.Elapsed.TotalMilliseconds:F0} мс");
        if (!string.IsNullOrEmpty(result.Error))
            sb.AppendLine($"  Ошибка: {result.Error}");

        return sb.ToString();
    }

    private static string Trim(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "—";
        s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }

    private static string StopReasonRu(ReActStopReason r) => r switch
    {
        ReActStopReason.FinalAnswer    => "ответ модели",
        ReActStopReason.TerminalTool   => "терминальный инструмент",
        ReActStopReason.IterationLimit => "лимит шагов",
        ReActStopReason.TimeLimit      => "лимит времени",
        ReActStopReason.NoProgress     => "нет продвижения",
        ReActStopReason.NoTools        => "нет инструментов",
        ReActStopReason.PolicyFailure  => "сбой политики",
        ReActStopReason.Cancelled      => "отменено",
        ReActStopReason.EngineFailure  => "сбой движка",
        _                              => r.ToString(),
    };

    /// <summary>
    /// Калькулятор для инструмента. Разбирает выражение через встроенный
    /// вычислитель библиотеки — тот же, что стоит за страницей SolversMath.
    /// </summary>
    private static string EvalArithmetic(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr))
            return "Ошибка: пустое выражение.";

        // Модель при нативном function calling присылает JSON вида
        // {"expression":"17 * 23"} — достаём значение, если это так.
        string cleaned = expr.Trim();
        if (cleaned.StartsWith('{'))
        {
            int colon = cleaned.IndexOf(':');
            int lastQuote = cleaned.LastIndexOf('"');
            if (colon > 0 && lastQuote > colon)
            {
                int firstQuote = cleaned.IndexOf('"', colon);
                if (firstQuote > 0 && lastQuote > firstQuote)
                    cleaned = cleaned[(firstQuote + 1)..lastQuote];
            }
        }

        try
        {
            // Тот же вычислитель, что стоит за страницей SolversMath:
            // Processor понимает выражения, переменные и функции.
            var lines = new AI.ClassicMath.Calculator.ProcessorLogic.Processor().Run(cleaned);
            string value = string.Join(" ", lines).Trim();
            return string.IsNullOrEmpty(value)
                ? $"Выражение «{cleaned}» не дало результата."
                : $"{cleaned} = {value}";
        }
        catch (Exception ex)
        {
            return $"Не удалось вычислить «{cleaned}»: {ex.Message}";
        }
    }

    private static string LookupFact(string topic)
    {
        string t = (topic ?? "").ToLowerInvariant();

        if (t.Contains("nlp"))      return "AI.NLP — TF-IDF, BM25, стемминг, лемматизация, суммаризация, NER.";
        if (t.Contains("fuzzy") || t.Contains("нечёт") || t.Contains("нечет"))
            return "AI.Fuzzy — нечёткая логика: Мамдани, Ларсен, Сугено, Цукамото.";
        if (t.Contains("chart") || t.Contains("график"))
            return "AI.Charts — кроссплатформенные графики на SkiaSharp с экспортом в Plotly.";
        if (t.Contains("mapf") || t.Contains("путь") || t.Contains("path"))
            return "AI.Algorithms.MAPF — CBS, ECBS, PBS, PIBT, LaCAM, SIPP.";
        if (t.Contains("нейрон") || t.Contains("neural"))
            return "AI.NeuralNetworks — полносвязные сети, LSTM, трансформеры, ONNX-экспорт.";

        return $"По теме «{topic}» справочник ничего не знает. Известные темы: NLP, Fuzzy, Charts, MAPF, нейросети.";
    }
}
