namespace AI.Agents.Interop.ReAct;

/// <summary>
/// Явный список перевода между старой формой ReAct и контрактами: каждое публичное свойство переводимых
/// типов записано либо в <see cref="Translated"/> (куда идет), либо в <see cref="NotTranslated"/> (почему нет).
/// </summary>
/// <remarks>
/// Ключ — <c>Тип.Свойство</c>, у вложенного типа — <c>Внешний.Вложенный.Свойство</c>. Храповик в тестах
/// перебирает свойства рефлексией: новое свойство любого из типов без записи здесь роняет тест, поэтому
/// перевод не может молча отстать от типов.
/// </remarks>
public static class ReActTranslationTable
{
    /// <summary>Свойства, которые переводятся, и куда.</summary>
    public static IReadOnlyDictionary<string, string> Translated { get; } = new Dictionary<string, string>
    {
        // ReActToolOutcome → ToolResult
        ["ReActToolOutcome.Ok"] = "ToolResult.Status: true → Ok, false → Failed",
        ["ReActToolOutcome.Observation"] =
            "ToolResult.Text; у финала с другим TerminalAnswer текстом становится TerminalAnswer, а Observation " +
            "не переносится (предупреждение в журнал)",
        ["ReActToolOutcome.Citations"] = "ToolResult.Evidence через ReActCitation → Citation; пустой список → null",
        ["ReActToolOutcome.Images"] = "ToolResult.Files через AgentImage → ToolFile (ShowToModel = true); пустой список → null",
        ["ReActToolOutcome.EndsTurn"] = "ToolResult.EndsRun",
        ["ReActToolOutcome.TerminalAnswer"] =
            "при EndsTurn — ToolResult.Text (ответ запуска старый движок брал отсюда); без EndsTurn движок его не " +
            "читает и он не переносится (предупреждение); финал без него — Text = Observation (предупреждение)",
        ["ReActToolOutcome.IsTerminal"] = "вычисляется из EndsTurn, отдельно не переводится",
        ["ReActToolOutcome.Payload"] =
            "ToolResult.Data через System.Text.Json; не сериализуется — InvalidOperationException; обратно приходит " +
            "JsonElement, тип CLR не сохраняется",
        ["ReActToolOutcome.ErrorCode"] = "ToolResult.ErrorCode",

        // ToolResult → ReActToolOutcome
        ["ToolResult.Status"] =
            "ReActToolOutcome.Ok: Ok → true, Failed → false; Denied, NeedsInput, AwaitsUser, Unknown → false и " +
            "ErrorCode denied, needs_input, awaits_user, unknown_status",
        ["ToolResult.Text"] =
            "ReActToolOutcome.Observation, при EndsRun и TerminalAnswer; у невыразимого статуса пустой текст " +
            "заменяется причиной",
        ["ToolResult.ErrorCode"] =
            "ReActToolOutcome.ErrorCode; у невыразимого статуса там код статуса, а свой код дописывается в Observation",
        ["ToolResult.Data"] = "ReActToolOutcome.Payload (JsonElement)",
        ["ToolResult.Files"] =
            "ReActToolOutcome.Images: только ShowToModel, image/* и Content; остальные файлы не переносятся " +
            "(предупреждение с числом)",
        ["ToolResult.Evidence"] =
            "ReActToolOutcome.Citations: только подтверждения с Url; без Url не переносятся, адрес не выдумывается " +
            "(предупреждение с числом)",
        ["ToolResult.EndsRun"] = "ReActToolOutcome.EndsTurn; TerminalAnswer = Observation",

        // ReActCitation ↔ Citation
        ["ReActCitation.Url"] = "Citation.Url",
        ["ReActCitation.Title"] = "Citation.Title",
        ["ReActCitation.Snippet"] = "Citation.Quote; null → пустая строка",
        ["Citation.Url"] = "ReActCitation.Url; без Url подтверждение в старую форму не переводится",
        ["Citation.Title"] = "ReActCitation.Title (пустой заголовок старая форма заменяет адресом)",
        ["Citation.Quote"] = "ReActCitation.Snippet; пустая строка → null",

        // ToolSpec ↔ IReActTool
        ["ToolSpec.Name"] = "IReActTool.Name",
        ["ToolSpec.Description"] = "IReActTool.Description",
        ["ToolSpec.Parameters"] = "IReActTool.ParametersJsonSchema (текст JSON)",
        ["IReActTool.Name"] = "ToolSpec.Name (проверка ToolSpec.NamePattern; не подходит — ArgumentException)",
        ["IReActTool.Description"] = "ToolSpec.Description; null → пустая строка",
        ["IReActTool.ParametersJsonSchema"] =
            "ToolSpec.Parameters; null или пусто — простая схема {\"input\": string} из NativeToolCallPolicy",

        // ToolFile ↔ AgentImage
        ["ToolFile.ContentType"] = "AgentImage.MimeType",
        ["ToolFile.Name"] = "AgentImage.Label",
        ["ToolFile.Content"] = "AgentImage.Data",
        ["ToolFile.ShowToModel"] = "вперед всегда true; обратно переводится только файл с true",
        ["AgentImage.Data"] = "ToolFile.Content",
        ["AgentImage.MimeType"] = "ToolFile.ContentType",
        ["AgentImage.Label"] = "ToolFile.Name",

        // Кадры потока
        ["ReActToolEvent.Progress.Message"] = "ToolUpdate.Progress.Message; null → пустая строка",
        ["ReActToolEvent.Progress.Payload"] = "ToolUpdate.Progress.Data через System.Text.Json, как Payload итога",
        ["ReActToolEvent.Result.Value"] = "ToolUpdate.Final.Result",
        ["ToolUpdate.Progress.Message"] = "ReActToolEvent.Progress.Message",
        ["ToolUpdate.Progress.Data"] = "ReActToolEvent.Progress.Payload (JsonElement)",
        ["ToolUpdate.Final.Result"] = "ReActToolEvent.Result.Value",
    };

    /// <summary>Свойства, которые не переводятся, и почему.</summary>
    public static IReadOnlyDictionary<string, string> NotTranslated { get; } = new Dictionary<string, string>
    {
        // ToolResult
        ["ToolResult.Origin"] =
            "старый инструмент происхождения не знает: вперед всегда Unknown; в старой форме поля нет",
        ["ToolResult.Question"] =
            "в ReActToolOutcome вопрос не выражается: текст вопроса дописывается в Observation, факт виден в " +
            "ErrorCode needs_input или awaits_user, а поля, вид и адресат вопроса теряются",
        ["ToolResult.Raised"] =
            "вперед всегда null (старый инструмент вида не знает); обратно поля нет, значение пишется предупреждением",

        // Citation: поля, которых у ReActCitation нет
        ["Citation.Kind"] = "у ReActCitation вида нет: вперед всегда Web, обратно вид теряется",
        ["Citation.Claim"] = "у ReActCitation нет утверждения",
        ["Citation.Verified"] = "у ReActCitation нет признака сверки; вперед false: сверяет приемка, а не инструмент",
        ["Citation.Prefix"] = "у ReActCitation нет контекста до цитаты",
        ["Citation.Suffix"] = "у ReActCitation нет контекста после цитаты",
        ["Citation.Location"] = "у ReActCitation нет места в источнике",
        ["Citation.At"] = "у ReActCitation нет позиции в ответе",
        ["Citation.Start"] = "у ReActCitation нет позиции в ответе",
        ["Citation.End"] = "у ReActCitation нет позиции в ответе",
        ["Citation.Site"] = "у ReActCitation нет сайта отдельно от адреса",
        ["Citation.Step"] = "у ReActCitation нет номера шага",
        ["Citation.Taken"] = "у ReActCitation нет времени снятия",
        ["Citation.Basis"] = "у ReActCitation нет вложенных оснований",

        // ToolSpec ↔ IReActTool
        ["ToolSpec.Action"] =
            "у IReActTool вида действия нет: вперед Unknown или явный параметр адаптера (Read сам не появляется), " +
            "обратно не переносится — перед вызовом вид из Classify пишется в журнал",
        ["ToolSpec.Group"] = "у IReActTool группы нет; метки (Tags) в группу не превращаются",
        ["ToolSpec.About"] = "у IReActTool есть только Description; вперед null",
        ["IReActTool.Tags"] = "в контракте меток нет, а превращать их в ToolSpec.Group нельзя: смысл другой",

        // ToolFile
        ["ToolFile.Uri"] = "AgentImage держит только байты: файл по адресу без Content в старую форму не переводится",
    };
}
