using AI.LLM.Agents.Multimodal;
using AI.LLM.Core.Models.Common.Messages;

namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Вход цикла: запрос, история диалога, изображения и метка вызывающей стороны.
/// </summary>
/// <remarks>
/// История принимается данными, а не через <see cref="Memory.IAgentMemory"/>. Тот контракт
/// владеет всем списком сообщений, включая системное, и не имеет пошагового хука — подключив
/// его, цикл потерял бы контроль над собственным системным промптом. Кому нужна память,
/// вызывает её сам и передаёт результат сюда.
/// </remarks>
public sealed class ReActQuery
{
    /// <summary>Текст запроса. Никогда не <c>null</c>.</summary>
    public string Text { get; }

    /// <summary>История диалога. Никогда не <c>null</c>.</summary>
    public IReadOnlyList<LLMMessage> History { get; }

    /// <summary>Изображения, приложенные к запросу. Никогда не <c>null</c>.</summary>
    public IReadOnlyList<AgentImage> Images { get; }

    /// <summary>
    /// Действие, которое нужно выполнить до первого обращения к модели. Покрывает случаи,
    /// когда вызывающая сторона уже знает, что делать (разобранная команда, явно затребованный
    /// инструмент), и спрашивать модель незачем. Может быть <c>null</c>.
    /// </summary>
    public ReActAction ForcedFirstAction { get; }

    /// <summary>Произвольная метка вызывающей стороны. Может быть <c>null</c>.</summary>
    public object Tag { get; }

    /// <summary>Создаёт запрос.</summary>
    /// <param name="text">Текст запроса; обязателен.</param>
    /// <param name="history">История диалога; допускается <c>null</c>.</param>
    /// <param name="images">Изображения; допускается <c>null</c>.</param>
    /// <param name="forcedFirstAction">Действие до первого обращения к модели; допускается <c>null</c>.</param>
    /// <param name="tag">Метка вызывающей стороны; допускается <c>null</c>.</param>
    public ReActQuery(
        string text,
        IReadOnlyList<LLMMessage> history = null,
        IReadOnlyList<AgentImage> images = null,
        ReActAction forcedFirstAction = null,
        object tag = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Запрос не может быть пустым.", nameof(text));

        Text = text;
        History = history ?? [];
        Images = images ?? [];
        ForcedFirstAction = forcedFirstAction;
        Tag = tag;
    }

    /// <summary>Запрос из одной строки — обычный случай без истории и метки.</summary>
    /// <param name="text">Текст запроса.</param>
    public static implicit operator ReActQuery(string text) => new(text);

    /// <summary>Контекст прогона, собранный из этого запроса.</summary>
    /// <param name="stepNumber">Номер шага.</param>
    public ReActRunContext ToRunContext(int stepNumber = 0) => new(Text, History, stepNumber, Tag);
}
