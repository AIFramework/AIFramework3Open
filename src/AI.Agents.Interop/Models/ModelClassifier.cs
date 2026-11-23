using System.Text.RegularExpressions;
using AI.Agents.Contracts.Models;
using AI.LLM.Clients.Base;
using AI.LLM.Services.LLM;
using Serilog;

namespace AI.Agents.Interop.Models;

/// <summary>
/// Классификатор текста поверх <see cref="IModelPort"/>: модель отвечает одним словом из списка классов, итог несет
/// статус, класс и весь ответ порта с расходом. Замена <c>ClassifierWithLLM</c>, который ходит мимо порта (без
/// повторов, цены и плательщика хоста) и сообщает сбой исключением.
/// </summary>
/// <remarks>
/// <para>
/// Сбой не выглядит как ответ: сбой порта — <see cref="ClassificationStatus.Failed"/> с кодом в
/// <see cref="Classification.Answer"/>, пустой ответ — <see cref="ClassificationStatus.Empty"/>, ответ без слова из
/// списка — <see cref="ClassificationStatus.NoClass"/>. Класс берется по первому слову ответа, которое есть в списке
/// (без учета регистра и знаков препинания по краям), поэтому «2», «2.» и «Ответ: 2» дают один класс, как у
/// <c>ClassifierWithLLM</c> без вероятностей.
/// </para>
/// <para>
/// Вероятностей классов нет: <see cref="IModelPort"/> не отдает вероятности токенов (развилка в отчете С2).
/// </para>
/// </remarks>
public sealed class ModelClassifier
{
    /// <summary>Место текста в шаблоне запроса.</summary>
    public const string InputPlaceholder = "{{input_text}}";

    private static readonly Regex Word = new(@"[a-zA-Zа-яА-ЯёЁ0-9]+", RegexOptions.Compiled);

    private readonly IModelPort _port;
    private readonly ModelRole _role;
    private readonly string _prompt;
    private readonly Dictionary<string, string> _classes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger? _log;

    /// <summary>Создает классификатор.</summary>
    /// <param name="port">Порт модели.</param>
    /// <param name="role">Роль, с которой спрашивать порт (обычно <see cref="ModelRole.Decide"/>).</param>
    /// <param name="classes">Классы — слова, которыми модель отвечает; знаки препинания по краям отбрасываются.</param>
    /// <param name="prompt">Шаблон запроса с <see cref="InputPlaceholder"/> на месте текста.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <exception cref="ArgumentException">Роль не задана; классов нет или класс не одно слово; в шаблоне нет
    /// <see cref="InputPlaceholder"/>.</exception>
    public ModelClassifier(IModelPort port, ModelRole role, IEnumerable<string> classes, string prompt, ILogger? log = null)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
        if (role.Name == null)
            throw new ArgumentException("Роль модели не задана (default(ModelRole)).", nameof(role));
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(prompt);
        if (!prompt.Contains(InputPlaceholder, StringComparison.Ordinal))
            throw new ArgumentException($"В шаблоне запроса нет места текста {InputPlaceholder}.", nameof(prompt));

        foreach (string name in classes)
        {
            string key = Clean(name ?? "");
            if (key.Length == 0 || Word.Match(key).Value != key)
                throw new ArgumentException(
                    $"Класс «{name}» должен быть одним словом из букв и цифр (знаки по краям отбрасываются).", nameof(classes));
            _classes.TryAdd(key, name!.Trim());
        }

        if (_classes.Count == 0)
            throw new ArgumentException("Список классов пуст.", nameof(classes));

        _role = role;
        _prompt = prompt;
        _log = log;
    }

    /// <summary>Температура запроса; 0 — самый вероятный класс. <see langword="null"/>: выбирает порт.</summary>
    public double? Temperature { get; init; } = 0;

    /// <summary>
    /// Предел токенов ответа; <see langword="null"/>: выбирает порт. Рассуждающей модели нужен запас: рассуждение
    /// расходует тот же предел.
    /// </summary>
    public int? MaxTokens { get; init; }

    /// <summary>Сколько модели размышлять; <see langword="null"/>: выбирает порт.</summary>
    public ReasoningLevel? Reasoning { get; init; }

    /// <summary>Классы в порядке объявления.</summary>
    public IReadOnlyCollection<string> Classes => _classes.Values;

    private ILogger Logger => _log ?? Log.Logger;

    /// <summary>
    /// Классификатор по старой настройке: <c>ChatLLMApi</c> через <c>LLMBase</c> и порт <see cref="ModelPort.Fixed"/>.
    /// Путь перехода с <c>ClassifierWithLLM</c>; повторов и цены хоста у такого порта нет, лучше дать классификатору
    /// порт хоста.
    /// </summary>
    /// <param name="api">Клиент API, как у <c>ClassifierWithLLM.ChatLLMApi</c>.</param>
    /// <param name="classes">Классы; <see langword="null"/>: цифры 0–9, как у <c>ClassifierWithLLM</c>.</param>
    /// <param name="prompt">Шаблон запроса с <see cref="InputPlaceholder"/>, как <c>ClassifierWithLLM.ClPrompt</c>.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Классификатор с ролью <see cref="ModelRole.Decide"/>.</returns>
    public static ModelClassifier FromChatApi(ChatLLMApi api, IEnumerable<string>? classes, string prompt, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        IModelPort port = new LLMBase(api).AsModelPort(
            new ModelCapabilities { ToolCalling = false, Images = false, Audio = false, StructuredOutput = false, Streaming = false },
            settings: null,
            log);
        return new ModelClassifier(
            port, ModelRole.Decide, classes ?? Enumerable.Range(0, 10).Select(i => i.ToString()), prompt, log);
    }

    /// <summary>Относит текст к одному из классов.</summary>
    /// <param name="text">Текст; пустой не допускается.</param>
    /// <param name="cancellationToken">Отмена; отмена вызывающим — исключение <see cref="OperationCanceledException"/>.</param>
    /// <returns>Итог со статусом; класс только при <see cref="ClassificationStatus.Ok"/>.</returns>
    /// <exception cref="ArgumentException">Текст пуст.</exception>
    public async Task<Classification> ClassifyAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        // Текст не может подменить место вставки: иначе он вставил бы себя второй раз.
        string request = _prompt.Replace(InputPlaceholder, text.Replace(InputPlaceholder, "[input_text]", StringComparison.Ordinal),
            StringComparison.Ordinal);

        var ask = new ModelAsk
        {
            Role = _role,
            Messages = [new ModelMessage
            {
                Role = MessageRole.User,
                Parts = [new MessagePart.Text { Content = request, Origin = new Contracts.Tools.Origin { Kind = Contracts.Tools.OriginKind.Trusted } }],
            }],
            ToolMode = ToolMode.None,
            Temperature = Temperature,
            MaxTokens = MaxTokens,
            Reasoning = Reasoning,
        };

        LlmAnswer answer = await _port.AskAsync(ask, cancellationToken).ConfigureAwait(false);

        ClassificationStatus status = answer.Status switch
        {
            LlmStatus.Ok or LlmStatus.Truncated => ClassificationStatus.NoClass,
            LlmStatus.Empty => ClassificationStatus.Empty,
            _ => ClassificationStatus.Failed,
        };

        string? found = status == ClassificationStatus.NoClass ? Find(answer.Text) : null;
        if (found != null)
            status = ClassificationStatus.Ok;
        else if (status != ClassificationStatus.Ok)
            Logger.Warning("Классификатор: итог {Status} (ответ порта {Answer}, {Failure})", status, answer.Status, answer.Failure);

        return new Classification { Status = status, Class = found, Answer = answer };
    }

    /// <summary>Первое слово ответа из списка классов; <see langword="null"/>: такого нет.</summary>
    private string? Find(string? answer) =>
        answer == null
            ? null
            : Word.Matches(answer).Select(m => m.Value).Select(w => _classes.GetValueOrDefault(w)).FirstOrDefault(c => c != null);

    private static string Clean(string value) =>
        Regex.Replace(value, @"^[^a-zA-Zа-яА-ЯёЁ0-9]+|[^a-zA-Zа-яА-ЯёЁ0-9]+$", "");
}
