using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Runs;
using AI.Agents.Contracts.Tools;
using Serilog;

namespace AI.Agents.Interop.Humans;

/// <summary>
/// Набор, который отвечает на вопрос инструмента сам, синхронно, через <see cref="IHumanChannel"/>: итог
/// <see cref="ToolStatus.AwaitsUser"/> или <see cref="ToolStatus.NeedsInput"/> с вопросом превращается в вопрос
/// человеку, а затем в один повторный вызов с ответом (<see cref="AnsweredToolContext"/>).
/// </summary>
/// <remarks>
/// <para>Все происходит внутри вызова набора, поэтому движок над набором (в том числе старый ReAct) видит только
/// итоговый результат и своего поведения не меняет.</para>
/// <list type="bullet">
/// <item>Ответ на другой вопрос (<see cref="Answer.QuestionId"/> не совпал) — итог <see cref="ToolStatus.Failed"/> с
/// кодом <see cref="HumanChannelCodes.AnswerMismatch"/>, инструмент повторно не вызывается.</item>
/// <item>Вопрос о согласии (<see cref="QuestionKind.Confirm"/>) без явного «да» — итог <see cref="ToolStatus.Denied"/>,
/// инструмент повторно не вызывается.</item>
/// <item>Повторный вызов снова спросил — его итог отдается как есть: раунд один.</item>
/// <item>Если контекст вызова уже наследник <see cref="ToolContext"/> со своими данными хоста, заменить его
/// ответом без потери нельзя: итог инструмента отдается как есть, с предупреждением в журнал.</item>
/// </list>
/// <para>
/// Это временная замена паузы. Когда движок начнет продолжать запуск с места, вопрос будет уходить итогом
/// <see cref="EngineStatus.AwaitsUser"/>, а ответ приходить в <see cref="EngineTask.Answer"/>.
/// </para>
/// </remarks>
internal sealed class HumanChannelToolSet : IToolSet
{
    private readonly IHumanChannel _channel;
    private readonly ILogger? _log;

    public HumanChannelToolSet(IToolSet inner, IHumanChannel channel, ILogger? log)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _log = log;
    }

    /// <summary>Набор, к которому идут вызовы.</summary>
    public IToolSet Inner { get; }

    /// <inheritdoc />
    public IReadOnlyList<ToolSpec> Available => Inner.Available;

    private ILogger Logger => _log ?? Log.Logger;

    /// <inheritdoc />
    public async IAsyncEnumerable<ToolUpdate> CallAsync(
        ToolCall call, ToolContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);

        ToolResult? asked = null;
        await foreach (ToolUpdate update in Inner.CallAsync(call, context, cancellationToken).ConfigureAwait(false))
        {
            if (update is ToolUpdate.Final { Result: { Status: ToolStatus.AwaitsUser or ToolStatus.NeedsInput, Question: not null } result })
            {
                asked = result;
                break;
            }

            yield return update;

            if (update is ToolUpdate.Final)
                yield break;
        }

        if (asked == null)
            yield break;

        if (context.GetType() != typeof(ToolContext))
        {
            Logger.Warning(
                "Канал к человеку: контекст вызова {Tool} — наследник {Type} с данными хоста; заменить его ответом " +
                "без потери нельзя, вопрос отдается итогом", call.Name, context.GetType().Name);
            yield return new ToolUpdate.Final { Result = asked };
            yield break;
        }

        Question question = asked.Question!;
        Answer answer = await _channel.AskAsync(question, cancellationToken).ConfigureAwait(false);

        if (answer == null || answer.QuestionId != question.Id)
        {
            yield return Final(ToolStatus.Failed, HumanChannelCodes.AnswerMismatch,
                $"ответ человека пришел не на вопрос {question.Id}; инструмент {call.Name} не вызван повторно");
            yield break;
        }

        if (question.Kind == QuestionKind.Confirm && answer.Confirmed != true)
        {
            yield return Final(ToolStatus.Denied, HumanChannelCodes.Declined,
                $"человек не дал согласия на действие {call.Name}");
            yield break;
        }

        var answered = new AnsweredToolContext { RunId = context.RunId, Path = context.Path, Answer = answer };
        await foreach (ToolUpdate update in Inner.CallAsync(call, answered, cancellationToken).ConfigureAwait(false))
        {
            yield return update;
            if (update is ToolUpdate.Final)
                yield break;
        }
    }

    private static ToolUpdate.Final Final(ToolStatus status, string code, string text) => new()
    {
        Result = new ToolResult
        {
            Status = status,
            Text = text,
            Origin = new Origin { Kind = OriginKind.Trusted },
            ErrorCode = code,
        },
    };
}
