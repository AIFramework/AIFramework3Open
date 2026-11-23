using System.Text.Json;
using AI.Agents.Contracts.Citations;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;
using AI.LLM.Agents.ReAct;
using AI.LLM.Agents.ReAct.Tools;
using Serilog;
using AgentImage = AI.LLM.Agents.Multimodal.AgentImage;

namespace AI.Agents.Interop.ReAct;

/// <summary>
/// Перевод значений между старой формой ReAct (<see cref="ReActToolOutcome"/>, <see cref="ReActCitation"/>,
/// <see cref="AgentImage"/>) и контрактами (<see cref="ToolResult"/>, <see cref="Citation"/>, <see cref="ToolFile"/>).
/// </summary>
/// <remarks>
/// Единственное место, где одна форма собирается из другой (это проверяет тест). Что переводится и куда,
/// а что не переводится и почему, записано в <see cref="ReActTranslationTable"/>. Потеря данных при
/// переводе не бывает молчаливой: она либо видна в самом значении (код ошибки), либо пишется
/// предупреждением в журнал Serilog.
/// </remarks>
public static class ReActTranslation
{
    /// <summary>Имя инструмента в журнале, когда перевод вызван не из адаптера.</summary>
    private const string NoToolName = "(без имени)";

    /// <summary>Переводит итог старого инструмента в итог контракта.</summary>
    /// <param name="outcome">Итог старого инструмента.</param>
    /// <param name="log">Журнал для предупреждений о потерях; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Итог контракта с <see cref="OriginKind.Unknown"/> и <see cref="ToolResult.Raised"/> = <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException"><see cref="ReActToolOutcome.Payload"/> не переводится в JSON.</exception>
    public static ToolResult ToResult(this ReActToolOutcome outcome, ILogger? log = null) =>
        ToResult(outcome, NoToolName, log ?? Log.Logger);

    /// <summary>Переводит итог контракта в итог старого инструмента.</summary>
    /// <param name="result">Итог контракта.</param>
    /// <param name="log">Журнал для предупреждений о потерях; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>
    /// Итог старого инструмента. Статусы, которых старый движок не знает, дают <c>Ok = false</c> и код из
    /// <see cref="ReActInteropCodes"/>.
    /// </returns>
    public static ReActToolOutcome ToOutcome(this ToolResult result, ILogger? log = null) =>
        ToOutcome(result, NoToolName, log ?? Log.Logger);

    /// <summary>Переводит цитату старого движка в подтверждение контракта без потерь.</summary>
    /// <param name="citation">Цитата старого движка.</param>
    /// <returns>
    /// Подтверждение вида <see cref="CitationKind.Web"/>: адрес, заголовок и фрагмент (<see cref="Citation.Quote"/>;
    /// фрагмента нет — пустая строка).
    /// </returns>
    public static Citation ToCitation(this ReActCitation citation)
    {
        ArgumentNullException.ThrowIfNull(citation);

        return new Citation
        {
            Kind = CitationKind.Web,
            Url = citation.Url,
            Title = citation.Title,
            Quote = citation.Snippet ?? string.Empty,
        };
    }

    /// <summary>
    /// Переводит подтверждение контракта в цитату старого движка. Адрес не выдумывается: без
    /// <see cref="Citation.Url"/> перевода нет.
    /// </summary>
    /// <param name="citation">Подтверждение контракта.</param>
    /// <returns>Цитата; <see langword="null"/>: у подтверждения нет адреса.</returns>
    public static ReActCitation? ToReActCitation(this Citation citation)
    {
        ArgumentNullException.ThrowIfNull(citation);

        if (string.IsNullOrWhiteSpace(citation.Url))
            return null;

        return new ReActCitation(
            citation.Url,
            citation.Title,
            string.IsNullOrEmpty(citation.Quote) ? null : citation.Quote);
    }

    /// <summary>Переводит картинку старого инструмента в файл контракта, показываемый модели.</summary>
    /// <param name="image">Картинка старого инструмента.</param>
    /// <returns>Файл с байтами, типом содержимого, меткой в имени и <see cref="ToolFile.ShowToModel"/> = <see langword="true"/>.</returns>
    public static ToolFile ToFile(this AgentImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return new ToolFile
        {
            ContentType = image.MimeType,
            Name = image.Label,
            Content = image.Data,
            ShowToModel = true,
        };
    }

    /// <summary>
    /// Переводит файл контракта в картинку старого инструмента. Переводится только картинка для модели
    /// с байтами: <see cref="ToolFile.ShowToModel"/>, тип <c>image/*</c> и <see cref="ToolFile.Content"/>.
    /// </summary>
    /// <param name="file">Файл контракта.</param>
    /// <returns>Картинка; <see langword="null"/>: файл в старой форме не выражается.</returns>
    public static AgentImage? ToImage(this ToolFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        bool image = file.ShowToModel
                     && file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                     && file.Content is { Length: > 0 };

        return image ? new AgentImage(file.Content!, file.ContentType, file.Name) : null;
    }

    // --- внутреннее: перевод с именем инструмента для журнала ---

    /// <summary>Итог старого инструмента → итог контракта.</summary>
    internal static ToolResult ToResult(ReActToolOutcome outcome, string toolName, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        // Инструмент вернул итог контракта целиком (AttributedTools): он и есть итог, переводить нечего.
        if (outcome.Payload is ToolResult whole)
            return whole;

        string text = outcome.Observation ?? string.Empty;

        // В контракте один текст. У финала движок брал ответ запуска из TerminalAnswer, поэтому он и
        // становится текстом; отличающийся Observation (виден только в следе) перенести некуда.
        if (outcome.EndsTurn && outcome.TerminalAnswer != null)
        {
            if (!string.Equals(outcome.TerminalAnswer, text, StringComparison.Ordinal))
            {
                log.Warning(
                    "ReAct → контракт: у инструмента {Tool} финал с TerminalAnswer, отличным от Observation; " +
                    "в ToolResult.Text идет TerminalAnswer, Observation не переносится (в контракте один текст)",
                    toolName);
            }

            text = outcome.TerminalAnswer;
        }
        else if (outcome.EndsTurn)
        {
            log.Warning(
                "ReAct → контракт: у инструмента {Tool} финал без TerminalAnswer; ToolResult.Text = Observation, " +
                "отличить «ответа нет» от ответа в контракте нельзя",
                toolName);
        }
        else if (outcome.TerminalAnswer != null)
        {
            log.Warning(
                "ReAct → контракт: у инструмента {Tool} TerminalAnswer без EndsTurn; движок его не читает, " +
                "в контракт он не переносится",
                toolName);
        }

        return new ToolResult
        {
            Status = outcome.Ok ? ToolStatus.Ok : ToolStatus.Failed,
            Text = text,
            Origin = new Origin { Kind = OriginKind.Unknown },
            ErrorCode = outcome.ErrorCode,
            Data = ToJson(outcome.Payload, "ReActToolOutcome.Payload", toolName),
            Files = outcome.Images is { Count: > 0 } images ? [.. images.Select(ToFile)] : null,
            Evidence = outcome.Citations is { Count: > 0 } citations ? [.. citations.Select(ToCitation)] : null,
            Question = null,
            Raised = null,
            EndsRun = outcome.EndsTurn,
        };
    }

    /// <summary>
    /// Итог контракта, который едет через старую форму целиком: в <c>Payload</c> лежит он сам, и
    /// <see cref="ToResult(ReActToolOutcome, string, ILogger)"/> отдает его без потерь. Признак успеха и текст —
    /// для тех, кто читает старую форму.
    /// </summary>
    internal static ReActToolOutcome Carry(ToolResult result) => new()
    {
        Ok = result.Status == ToolStatus.Ok,
        Observation = result.Text,
        ErrorCode = result.ErrorCode,
        EndsTurn = result.EndsRun,
        TerminalAnswer = result.EndsRun ? result.Text : null,
        Payload = result,
    };

    /// <summary>Итог контракта → итог старого инструмента.</summary>
    internal static ReActToolOutcome ToOutcome(ToolResult result, string toolName, ILogger log)
    {
        ArgumentNullException.ThrowIfNull(result);

        (bool ok, string? errorCode, string observation) = result.Status switch
        {
            ToolStatus.Ok => (true, result.ErrorCode, result.Text),
            ToolStatus.Failed => (false, result.ErrorCode, result.Text),
            ToolStatus.Denied => (false, ReActInteropCodes.Denied, Reason(result, "вызов отклонен")),
            ToolStatus.NeedsInput => (false, ReActInteropCodes.NeedsInput, Reason(result, "инструменту нужны данные от человека")),
            ToolStatus.AwaitsUser => (false, ReActInteropCodes.AwaitsUser, Reason(result, "инструмент ждет решения человека")),
            _ => (false, ReActInteropCodes.UnknownStatus, Reason(result, "исход вызова неизвестен")),
        };

        IReadOnlyList<ReActCitation> citations = ToReActCitations(result.Evidence, out int skippedCitations);
        if (skippedCitations > 0)
        {
            log.Warning(
                "Контракт → ReAct: у инструмента {Tool} {Count} подтверждений без Url не переданы в Citations: " +
                "адрес не выдумывается",
                toolName, skippedCitations);
        }

        var images = new List<AgentImage>();
        int skippedFiles = 0;
        foreach (ToolFile file in result.Files ?? [])
        {
            if (ToImage(file) is { } image)
                images.Add(image);
            else
                skippedFiles++;
        }

        if (skippedFiles > 0)
        {
            log.Warning(
                "Контракт → ReAct: у инструмента {Tool} {Count} файлов не переданы: в старой форме есть только " +
                "картинки для модели с байтами (ShowToModel, image/*, Content)",
                toolName, skippedFiles);
        }

        if (result.Handle is { } handle)
        {
            log.Warning(
                "Контракт → ReAct: у инструмента {Tool} итог с Handle {Handle}; в старой форме поля нет, значение " +
                "не переносится",
                toolName, handle);
        }

        if (result.Raised is { } raised)
        {
            log.Warning(
                "Контракт → ReAct: инструмент {Tool} поднял вид вызова до {Raised}; в старой форме поля нет, " +
                "значение не переносится",
                toolName, raised);
        }

        return new ReActToolOutcome
        {
            Ok = ok,
            Observation = observation ?? string.Empty,
            Citations = citations,
            Images = images,
            EndsTurn = result.EndsRun,
            TerminalAnswer = result.EndsRun ? observation : null,
            Payload = result.Data is { } data ? data : null,
            ErrorCode = errorCode,
        };
    }

    /// <summary>Подтверждения с адресом → цитаты; сколько подтверждений без адреса пропущено.</summary>
    internal static IReadOnlyList<ReActCitation> ToReActCitations(IReadOnlyList<Citation>? evidence, out int skipped)
    {
        skipped = 0;
        var citations = new List<ReActCitation>(evidence?.Count ?? 0);
        foreach (Citation item in evidence ?? [])
        {
            if (ToReActCitation(item) is { } citation)
                citations.Add(citation);
            else
                skipped++;
        }

        return citations;
    }

    /// <summary>Кадр старого инструмента → кадр контракта.</summary>
    internal static ToolUpdate ToUpdate(ReActToolEvent toolEvent, string toolName, ILogger log) => toolEvent switch
    {
        ReActToolEvent.Progress progress => new ToolUpdate.Progress
        {
            Message = progress.Message ?? string.Empty,
            Data = ToJson(progress.Payload, "ReActToolEvent.Progress.Payload", toolName),
        },
        ReActToolEvent.Result { Value: { } outcome } => new ToolUpdate.Final { Result = ToResult(outcome, toolName, log) },
        ReActToolEvent.Result => throw new InvalidOperationException(
            $"Инструмент «{toolName}» вернул ReActToolEvent.Result без итога (Value = null)."),
        _ => throw new InvalidOperationException(
            $"Инструмент «{toolName}» вернул неизвестный кадр {toolEvent?.GetType().Name ?? "null"}."),
    };

    /// <summary>Кадр контракта → кадр старого инструмента.</summary>
    internal static ReActToolEvent ToEvent(ToolUpdate update, string toolName, ILogger log) => update switch
    {
        ToolUpdate.Progress progress => new ReActToolEvent.Progress(progress.Message, progress.Data is { } data ? data : null),
        ToolUpdate.Final final => new ReActToolEvent.Result(ToOutcome(final.Result, toolName, log)),
        _ => throw new InvalidOperationException(
            $"Инструмент «{toolName}» вернул неизвестный кадр {update?.GetType().Name ?? "null"}."),
    };

    /// <summary>
    /// Контекст вызова контракта → контекст прогона старого движка. Запрос и история берутся из
    /// <paramref name="baseRun"/> (в контексте вызова их нет), номер шага — из <see cref="StepPath.Tier"/>,
    /// а сам контекст вызова (идентификатор запуска, путь шага) едет в <see cref="ReActRunContext.Tag"/>.
    /// </summary>
    internal static ReActRunContext ToRunContext(ToolContext context, ReActRunContext? baseRun) =>
        new(baseRun?.Query ?? string.Empty, baseRun?.History, context.Path.Tier, context);

    /// <summary>
    /// Контекст прогона старого движка → контекст вызова контракта. Если в метке уже лежит контекст
    /// контракта (вызов пришел через адаптер), он и отдается; иначе идентификатор запуска неизвестен
    /// (пустая строка: старый движок его инструменту не передает), ярус — номер шага.
    /// </summary>
    internal static ToolContext ToToolContext(ReActRunContext? run) =>
        run?.Tag as ToolContext
        ?? new ToolContext { RunId = string.Empty, Path = new StepPath { Tier = run?.StepNumber ?? 0 } };

    /// <summary>Значение → JSON через System.Text.Json; не переводится — исключение с понятным текстом.</summary>
    internal static JsonElement? ToJson(object? value, string what, string toolName)
    {
        if (value is null)
            return null;

        if (value is JsonElement element)
            return element.ValueKind == JsonValueKind.Undefined ? null : element.Clone();

        try
        {
            return JsonSerializer.SerializeToElement(value, value.GetType());
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"{what} инструмента «{toolName}» (тип {value.GetType().FullName}) не переводится в JSON " +
                $"System.Text.Json, а в контракте это поле — JsonElement: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Текст причины для статуса, которого старый движок не знает: текст итога (или причина по умолчанию),
    /// свой код инструмента и текст вопроса — ни то, ни другое не должно пропасть молча.
    /// </summary>
    private static string Reason(ToolResult result, string fallback)
    {
        string reason = string.IsNullOrWhiteSpace(result.Text) ? fallback : result.Text;

        if (!string.IsNullOrEmpty(result.ErrorCode))
            reason += $" (код инструмента: {result.ErrorCode})";

        if (result.Question is { } question)
            reason += $"\nВопрос: {question.Text}";

        return reason;
    }
}
