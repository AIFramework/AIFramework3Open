using AI.LLM.Agents.Tools;
using System.Text;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Инструменты самого Атласа, в отличие от скриптовых <c>script_help</c>, <c>check_script</c>
/// и <c>run_script</c>.
/// </summary>
/// <remarks>
/// Индекс открывается при первом вопросе, а не при запуске сервера: Claude Code поднимает
/// сервер в каждой сессии, а спрашивают его не в каждой. Неудачное открытие (индекса ещё нет)
/// не запоминается: после <c>atlas index</c> следующий вопрос откроет индекс заново.
/// </remarks>
public sealed class AtlasTools(string folder) : IDisposable
{
    /// <summary>
    /// Инструкция сервера: попадает в каждую сессию, где сервер подключён, и говорит модели,
    /// когда звать инструменты. Без неё модель видит только их описания и о справке не вспоминает.
    /// </summary>
    public const string Instructions =
        "Атлас — справка по библиотеке AIFramework (C#): статистика, ML, сигналы, решатели, химия, экономика, NLP и другое. " +
        "Порядок работы. 1) Прежде чем писать вычисление или алгоритм, спроси api_search словами задачи: готовое в библиотеке " +
        "лучше своего. api_describe покажет карточку найденного: сигнатуру, описание, кто вызывает и что вызывает он сам. " +
        "2) Задачу из нескольких шагов собери find_stack: методы по порядку, пробелы и проверка компиляцией. " +
        "3) Если всё же пишешь свой метод, сначала вызови dup_check с черновиком или замыслом: он найдёт то же самое по коду, " +
        "имени и описанию. 4) После правки .cs хук Атласа может добавить замечание «Атлас после правки»: если найденное — " +
        "то же самое, замени свой код вызовом существующего; если это сознательно другое, скажи пользователю почему, а " +
        "записать решение (dup_decide) можно только по его слову. dup_explain разбирает пару и исполняет оба метода на одних " +
        "входах, dup_report показывает дубли целиком. " +
        "В проекте, который подключает AIFramework, поиск идёт по библиотеке, а dup_check и dup_report сравнивают код проекта " +
        "с библиотекой. script_help, check_script и run_script — справка, проверка и исполнение скриптов AIScript без сети и " +
        "записи файлов. atlas_status показывает ключ, модели и режим проекта.";

    private readonly object _gate = new();
    private Task<Opened>? _opening;

    private sealed record Opened(AtlasContext Context, string Channels, IReadOnlyDictionary<(string, string), int> Usage);

    [AgentTool("api_search", "Найти в библиотеке AIFramework типы и методы под задачу, описанную словами " +
        "(«сгладить временной ряд», «кратчайший путь в графе»). Возвращает сигнатуры, идентификаторы, " +
        "описания и число мест, где метод используется.")]
    public async Task<string> SearchAsync(
        [ToolParameter("задача или то, что нужно найти, на русском или английском")] string query,
        [ToolParameter("сколько результатов вернуть, до 20", Required = false)] int limit = 10,
        [ToolParameter("разобрать выдачу языковой моделью: что подходит и чего не хватает", Required = false)] bool explain = false)
    {
        if (string.IsNullOrWhiteSpace(query)) return "Пустой запрос.";

        Opened opened;
        try
        {
            opened = await OpenAsync();
        }
        catch (InvalidOperationException error)
        {
            return error.Message;
        }

        AtlasContext context = opened.Context;
        limit = Math.Clamp(limit, 1, 20);

        IReadOnlyList<SearchHit> hits = await context.Search.SearchAsync(query, new SearchOptions(Limit: explain ? 20 : limit));
        var builder = new StringBuilder().AppendLine($"Поиск: {opened.Channels}.");

        if (hits.Count == 0) return builder.Append($"По запросу «{query}» ничего не найдено.").ToString();

        if (explain && context.Llm is { } llm)
        {
            LlmAnswer answer = await llm.SelectAsync(query, hits);

            builder.AppendLine(answer.Picks.Count > 0 ? "Подходит:" : "Модель не выбрала ни одного кандидата.");
            foreach (LlmPick pick in answer.Picks) Append(builder, pick.Hit.Unit, opened.Usage, pick.Why);
            if (answer.Missing.Length > 0) builder.AppendLine($"Не хватает: {answer.Missing}");

            hits = [.. answer.Ordered.Skip(answer.Picks.Count).Take(Math.Max(0, limit - answer.Picks.Count))];
            if (hits.Count > 0) builder.AppendLine("Остальные кандидаты:");
        }
        else if (explain)
        {
            builder.AppendLine("Разбор моделью выключен: нет ключа OpenRouter или не задана searchModel.");
        }

        foreach (SearchHit hit in hits.Take(limit)) Append(builder, hit.Unit, opened.Usage, null);

        return builder.Append("Карточка с вызывающими и вызываемыми: api_describe(идентификатор).").ToString();
    }

    [AgentTool("api_describe", "Карточка типа или метода AIFramework: сигнатура, описание, файл, " +
        "кто вызывает (библиотека, тесты, туториалы, демо) и что вызывает он сам.")]
    public async Task<string> DescribeAsync(
        [ToolParameter("идентификатор из api_search (M:…, T:…), имя функции AIScript или часть имени")] string name)
    {
        Opened opened;
        try
        {
            opened = await OpenAsync();
        }
        catch (InvalidOperationException error)
        {
            return error.Message;
        }

        IReadOnlyList<CodeUnit> found = opened.Context.Store.Find(name ?? "", limit: 6);

        if (found.Count == 0) return $"По «{name}» ничего не найдено. Поиск по смыслу — api_search.";

        opened.Context.Selections.Record(found[0]);
        string card = UnitReport.Describe(opened.Context.Store, found[0]);
        return found.Count == 1 ? card : card + "\nЕщё совпадения:\n" + string.Join('\n', found.Skip(1).Select(unit => "  " + unit.Id));
    }

    [AgentTool("find_stack", "Собрать стек из готовых функций AIFramework под задачу: шаги, методы по " +
        "порядку с тем, как данные переходят от шага к шагу, пробелы (чего в библиотеке нет) и проверка " +
        "цепочки компиляцией C# или проверкой скрипта AIScript. Шаги лучше передать самому.")]
    public async Task<string> FindStackAsync(
        [ToolParameter("задача словами")] string task,
        [ToolParameter("шаги через точку с запятой, каждый — одно действие; пусто — разбить автоматически", Required = false)] string steps = "",
        [ToolParameter("искать только функции AIScript и проверять скриптом, а не компиляцией C#", Required = false)] bool script = false)
    {
        if (string.IsNullOrWhiteSpace(task) && string.IsNullOrWhiteSpace(steps)) return "Пустая задача.";

        Opened opened;
        try
        {
            opened = await OpenAsync();
        }
        catch (InvalidOperationException error)
        {
            return error.Message;
        }

        IReadOnlyList<string>? given = string.IsNullOrWhiteSpace(steps)
            ? null
            : [.. steps.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

        StackAnswer answer = await StackReport.BuildAsync(opened.Context, task ?? "", given, script);
        return $"Поиск: {opened.Channels}.\n{StackReport.Format(answer)}";
    }

    [AgentTool("dup_check", "Проверить, нет ли в AIFramework того, что собираешься написать: по коду " +
        "черновика (метод целиком или тело) и по замыслу словами. Звать до написания своего метода.")]
    public async Task<string> DupCheckAsync(
        [ToolParameter("код черновика: объявление метода или тело; можно пусто, если есть замысел", Required = false)] string code = "",
        [ToolParameter("что метод делает, словами", Required = false)] string intent = "")
    {
        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(intent)) return "Нужен код черновика или замысел словами.";

        try
        {
            return await DupReport.CheckAsync((await OpenAsync()).Context, code ?? "", intent);
        }
        catch (InvalidOperationException error)
        {
            return error.Message;
        }
    }

    [AgentTool("dup_explain", "Разобрать пару методов: признаки сходства (код, описание, имя, вызовы, " +
        "константы), вердикт, каноническая версия, решение журнала, исполнение обоих методов на одних входах " +
        "(поведенческий дубль, связь выходов, таблица расходящихся входов), вердикт модели по серой зоне и начала обоих тел.")]
    public async Task<string> DupExplainAsync(
        [ToolParameter("первый метод: идентификатор или часть имени")] string a,
        [ToolParameter("второй метод: идентификатор или часть имени")] string b)
    {
        try
        {
            return await DupReport.ExplainAsync((await OpenAsync()).Context, a, b);
        }
        catch (InvalidOperationException error)
        {
            return error.Message;
        }
    }

    [AgentTool("dup_report", "Дубли в библиотеке AIFramework: копии, похожий код, один предмет с разным кодом. " +
        "В проекте на библиотеке — отчёт «проект ↔ библиотека»: что проект повторяет из библиотеки, дубли внутри проекта, " +
        "общий код проекта — кандидаты в библиотеку; у каждой находки сказано, где править. " +
        "Решённые пары из журнала не показываются, пока их код не изменился.")]
    public async Task<string> DupReportAsync(
        [ToolParameter("сколько находок показать", Required = false)] int limit = 20,
        [ToolParameter("исполнить пары на одних входах: расхождения первыми; дольше", Required = false)] bool behavior = false)
    {
        try
        {
            AtlasContext context = (await OpenAsync()).Context;
            return context.Project is null
                ? await DupReport.LibraryAsync(context, Math.Clamp(limit, 1, 200), behavior, budget: TimeSpan.FromSeconds(60))
                : await ProjectReport.BuildAsync(context, Math.Clamp(limit, 1, 200), behavior, TimeSpan.FromSeconds(60));
        }
        catch (InvalidOperationException error)
        {
            return error.Message;
        }
    }

    [AgentTool("dup_decide", "Записать решение по паре в журнал: «перекрытие» (оставить раздельно, с причиной), " +
        "«дубль» (свести) или «нет связи». Звать только по явному решению человека.")]
    public Task<string> DupDecideAsync(
        [ToolParameter("первый метод")] string a,
        [ToolParameter("второй метод")] string b,
        [ToolParameter("вердикт: перекрытие, дубль, нет связи")] string verdict,
        [ToolParameter("почему")] string reason) =>
        WithContextAsync(context =>
        {
            CodeUnit? first = context.Find(a);
            CodeUnit? second = context.Find(b);

            if (first is null || second is null) return $"Не найдено: {(first is null ? a : b)}";

            DupDecision decision = context.Ledger.Decide(first, second, verdict, reason);
            return $"Записано: {decision.A} ↔ {decision.B} — «{decision.Verdict}».";
        });

    [AgentTool("atlas_status", "Состояние Атласа: задан ли ключ OpenRouter, какие ступени поиска " +
        "включены (векторизатор, реранкер, LLM), где лежат настройки и кэш.")]
    public string Status()
    {
        AtlasSettings settings = AtlasSettings.Load();
        string text = settings.Describe();

        try
        {
            string root = GitRepository.TopLevel(folder);
            if (LibraryLinks.Detect(root) is { } link)
            {
                using var project = new ProjectSide(root, link, settings.DisclosureFor(root));
                text += "\n" + project.Describe();
            }
        }
        catch (InvalidOperationException)
        {
            // Папка не в git: режима проекта нет.
        }

        return text;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_opening is { IsCompletedSuccessfully: true } opened) opened.Result.Context.Dispose();
    }

    private async Task<string> WithContextAsync(Func<AtlasContext, string> action)
    {
        try
        {
            Opened opened = await OpenAsync();
            return action(opened.Context);
        }
        catch (InvalidOperationException error)
        {
            return error.Message;
        }
    }

    private Task<Opened> OpenAsync()
    {
        lock (_gate)
        {
            if (_opening is null || _opening.IsFaulted) _opening = Task.Run(Open);
            return _opening;
        }
    }

    private async Task<Opened> Open()
    {
        AtlasContext context = AtlasContext.Open(folder);

        try
        {
            string vectors = await context.EnableVectorsAsync(allowFullRun: false);
            string channels = $"BM25; {vectors}; реранкер {(context.Search.HasReranker ? context.Settings.RerankModel : "выключен")}; " +
                $"{context.Search.Units.Count} единиц открытого API";

            return new Opened(context, channels, context.Store.UsageCounts());
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }

    private static void Append(StringBuilder builder, CodeUnit unit, IReadOnlyDictionary<(string, string), int> usage, string? why)
    {
        string summary = UnitReport.Summary(unit.Doc);

        builder.AppendLine(unit.Signature)
            .AppendLine($"    {unit.Id}  {unit.File}:{unit.Line}  вызывают: {usage.GetValueOrDefault((unit.Project, unit.Id))}");

        if (why is { Length: > 0 }) builder.AppendLine($"    зачем: {why}");
        if (summary.Length > 0) builder.AppendLine($"    {(summary.Length > 200 ? summary[..200] + "…" : summary)}");
    }
}
