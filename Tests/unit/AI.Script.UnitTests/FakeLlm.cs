using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Core.Models.Common.Responses;
using AI.LLM.Services.Embeddings.Base;

namespace AI.Script.UnitTests;

/// <summary>
/// Языковая модель, отвечающая заранее заготовленным.
/// </summary>
/// <remarks>
/// Тесты LLM-контура проверяют контур, а не модель: как разбирается ответ, как считается
/// расход, что происходит при отказе. Настоящая модель сделала бы эти проверки медленными,
/// платными и невоспроизводимыми — то есть перестала бы быть проверкой.
/// </remarks>
internal sealed class FakeLlm : ILLMClient
{
    private readonly Queue<string> _answers;

    /// <summary>Создаёт модель, выдающую ответы по очереди.</summary>
    public FakeLlm(params string[] answers) => _answers = new Queue<string>(answers);

    /// <summary>Сколько токенов сообщать в ответе.</summary>
    public int Tokens { get; set; } = 100;

    /// <summary>Какую стоимость сообщать в ответе.</summary>
    public decimal Cost { get; set; }

    /// <summary>Сколько запросов сделано.</summary>
    public int Requests { get; private set; }

    /// <summary>Сообщения последнего запроса.</summary>
    public IReadOnlyList<LLMMessage> LastMessages { get; private set; } = [];

    /// <inheritdoc/>
    public Task<string> SendAsync(string text, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default) =>
        SendAsync([new LLMMessage(LLMMessage.UserRole, text)], generateSettings, cancellationToken);

    /// <inheritdoc/>
    public async Task<string> SendAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default)
    {
        ChatCompletionsResponse response = await SendFullAsync(messages, generateSettings, cancellationToken)
            .ConfigureAwait(false);

        return response.Choices[0].Message.Content?.ToString() ?? string.Empty;
    }

    /// <inheritdoc/>
    public Task<ChatCompletionsResponse> SendFullAsync(IEnumerable<LLMMessage> messages, GenerateSettings generateSettings = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Requests++;
        LastMessages = [.. messages];

        string answer = _answers.Count > 0 ? _answers.Dequeue() : string.Empty;

        var response = new ChatCompletionsResponse(answer);

        response.Usage.TotalTokens = Tokens;
        response.Usage.Cost = Cost;

        return Task.FromResult(response);
    }

    /// <inheritdoc/>
    public Task<int> TokenizeAsync(IEnumerable<LLMMessage> messages, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tokens);
}

/// <summary>
/// Эмбеддер, считающий вектор по словам без обращения к сети.
/// </summary>
/// <remarks>
/// Мешок слов из небольшого словаря: близость по нему ведёт себя так же, как у настоящего
/// эмбеддера на очевидных примерах — «прокси» ближе к тексту про прокси, чем к тексту про
/// матрицы, — и этого достаточно, чтобы проверить сам поиск.
/// </remarks>
internal sealed class FakeEmbedder : IEmbedderService
{
    private static readonly string[] Vocabulary =
        ["прокси", "сеть", "матрица", "вектор", "модель", "обучение", "таблица", "график"];

    /// <summary>Сколько раз вызывали кодирование.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc/>
    public Task<Vector> EncodeQuestionAsync(string question, CancellationToken cancellationToken = default) =>
        EncodeAsync(question, cancellationToken);

    /// <inheritdoc/>
    public Task<Vector> EncodeAsync(string text, CancellationToken cancellationToken = default)
    {
        Calls++;

        return Task.FromResult(Encode(text));
    }

    /// <inheritdoc/>
    public Task<Vector[]> EncodeAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        Calls++;

        var result = new List<Vector>();

        foreach (string text in texts) result.Add(Encode(text));

        return Task.FromResult(result.ToArray());
    }

    /// <inheritdoc/>
    public Task<Vector[]> EncodeAsyncWithBlockSize(
        IEnumerable<string> processedTexts,
        IEnumerable<int> blockSizes,
        IEnumerable<int> excludeBlockSizes = null,
        CancellationToken cancellationToken = default) =>
        EncodeAsync(processedTexts, cancellationToken);

    /// <inheritdoc/>
    public double TanhCosineNormalize(double cosine) => cosine;

    /// <inheritdoc/>
    public Task<Vector> EncodeQueryAsync(string query, CancellationToken cancellationToken = default) =>
        EncodeAsync(query, cancellationToken);

    private static Vector Encode(string text)
    {
        var vector = new Vector(Vocabulary.Length + 1);

        for (int i = 0; i < Vocabulary.Length; i++)
        {
            if (text.Contains(Vocabulary[i], StringComparison.OrdinalIgnoreCase)) vector[i] = 1;
        }

        // Постоянная составляющая: без неё вектор текста, не содержащего ни одного слова
        // словаря, оказался бы нулевым, а близость к нему — неопределённой.
        vector[Vocabulary.Length] = 0.1;

        return vector;
    }
}
