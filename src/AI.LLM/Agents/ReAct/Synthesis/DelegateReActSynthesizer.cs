using System.Runtime.CompilerServices;
using System.Text;
using AI.LLM.Agents.ReAct.Policies;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;

namespace AI.LLM.Agents.ReAct.Synthesis;

/// <summary>
/// Синтез, собранный из делегата. Как и у принятия решений, делегат вместо интерфейса:
/// вызывающая сторона обычно хочет для итогового текста другую модель и другой бюджет,
/// чем для решений, и должна выбирать это сама.
/// </summary>
public sealed class DelegateReActSynthesizer : IReActSynthesizer
{
    private const string DefaultInstruction =
        "Напиши итоговый ответ на запрос пользователя, опираясь ТОЛЬКО на приведённые наблюдения "
        + "и черновик. Не выдумывай фактов сверх них. Если данных не хватает — скажи об этом прямо.";

    private readonly Func<string, string, CancellationToken, IAsyncEnumerable<ReActTextChunk>> _stream;
    private readonly string _instruction;

    /// <summary>Создаёт синтез поверх потокового обращения к модели.</summary>
    /// <param name="stream">Обращение к модели: системная инструкция и запрос на входе, фрагменты на выходе.</param>
    /// <param name="instruction">Инструкция синтеза; при <c>null</c> берётся стандартная.</param>
    public DelegateReActSynthesizer(
        Func<string, string, CancellationToken, IAsyncEnumerable<ReActTextChunk>> stream,
        string instruction = null)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _instruction = string.IsNullOrWhiteSpace(instruction) ? DefaultInstruction : instruction;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ReActTextChunk> SynthesizeAsync(
        ReActSynthesisContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _stream(_instruction, BuildPrompt(context), cancellationToken);
    }

    /// <summary>Создаёт синтез поверх разового обращения к модели.</summary>
    /// <param name="complete">Обращение к модели.</param>
    /// <param name="instruction">Инструкция синтеза; при <c>null</c> берётся стандартная.</param>
    public static DelegateReActSynthesizer FromCompletion(
        ReActCompletionDelegate complete, string instruction = null)
    {
        ArgumentNullException.ThrowIfNull(complete);
        return new DelegateReActSynthesizer((system, user, ct) => OnceAsync(complete, system, user, ct), instruction);
    }

    /// <summary>Создаёт синтез поверх клиента библиотеки.</summary>
    /// <param name="llm">Клиент модели.</param>
    /// <param name="settings">Настройки генерации; при <c>null</c> берётся полный бюджет.</param>
    /// <param name="instruction">Инструкция синтеза; при <c>null</c> берётся стандартная.</param>
    public static DelegateReActSynthesizer FromLlm(
        ILLMClient llm, GenerateSettings settings = null, string instruction = null)
    {
        ArgumentNullException.ThrowIfNull(llm);

        return FromCompletion(
            async (system, user, ct) =>
            {
                var messages = new List<LLMMessage>
                {
                    LLMMessage.CreateMessage(Roles.System, system),
                    LLMMessage.CreateMessage(Roles.User, user),
                };

                return await llm
                    .SendAsync(messages, settings ?? new GenerateSettings(temperature: 0.3, maxTokens: 2048), ct)
                    .ConfigureAwait(false);
            },
            instruction);
    }

    /// <summary>Собирает запрос синтеза из наблюдений, черновика и причины остановки.</summary>
    /// <param name="context">Контекст синтеза.</param>
    internal static string BuildPrompt(ReActSynthesisContext context)
    {
        var sb = new StringBuilder();
        sb.Append("Запрос: ").Append(context.Query?.Text).Append("\n\n");

        if (!string.IsNullOrWhiteSpace(context.RenderedTrace))
            sb.Append("Наблюдения:\n").Append(context.RenderedTrace).Append('\n');

        if (!string.IsNullOrWhiteSpace(context.Draft))
            sb.Append("Черновик (используй как основу, улучши и дополни):\n")
              .Append(context.Draft)
              .Append("\n\n");

        if (context.StopReason is ReActStopReason.IterationLimit or ReActStopReason.TimeLimit
            or ReActStopReason.NoProgress or ReActStopReason.PolicyFailure)
            sb.Append("Работа остановлена до полного решения задачи: ответь по тому, что удалось собрать, "
                      + "и честно отметь, чего не хватает.\n");

        return sb.ToString();
    }

    private static async IAsyncEnumerable<ReActTextChunk> OnceAsync(
        ReActCompletionDelegate complete,
        string system,
        string user,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string text = await complete(system, user, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(text))
            yield return new ReActTextChunk(text, null);
    }
}
