using System.Text;
using System.Text.RegularExpressions;
using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Requests;
using AI.LLM.Services.Reranking.Base;

namespace AI.LLM.Services.Reranking.Llm;

/// <summary>
/// Реранкер на любой чат-модели: одним запросом модель получает вопрос и пронумерованные документы и называет номера
/// подходящих — от самого подходящего. Нужен там, где кросс-энкодера нет (локальная модель Ollama, сервер без /rerank).
/// </summary>
/// <remarks>
/// Оценка документа — по его месту в ответе: первый названный — 1, следующие меньше, неназванные — 0. Модель видит все
/// документы сразу и сравнивает их между собой, поэтому один запрос дешевле и точнее отдельной оценки каждого.
/// </remarks>
public sealed class LlmReranker : RerankerBase<string, string>
{
    /// <summary>Сколько знаков документа видит модель: длиннее — начало, по нему обычно ясно, о чём документ.</summary>
    public int MaxDocumentChars { get; set; } = 800;

    private static readonly Regex Numbers = new(@"\d+", RegexOptions.Compiled);

    private readonly ILLMClient _llm;
    private readonly GenerateSettings _settings;

    /// <param name="llm">Чат-модель, которая упорядочивает документы.</param>
    /// <param name="settings">Настройки генерации; по умолчанию — температура 0 и короткий ответ.</param>
    public LlmReranker(ILLMClient llm, GenerateSettings settings = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _settings = settings ?? new GenerateSettings(temperature: 0, repetitionPenalty: null, topP: null, topK: null, minTokens: null, maxTokens: 200);
    }

    /// <inheritdoc />
    public override double Sim(string query, string document, string instruct = null) =>
        SimAsync(query, document, instruct).GetAwaiter().GetResult();

    /// <inheritdoc />
    public override async Task<double> SimAsync(string query, string document, string instruct = null) =>
        (await SimsAsync(query, [document], instruct).ConfigureAwait(false))[0];

    /// <inheritdoc />
    public override Vector Sims(string query, IEnumerable<string> documents, string instruct = null) =>
        SimsAsync(query, documents, instruct).GetAwaiter().GetResult();

    /// <inheritdoc />
    public override async Task<Vector> SimsAsync(string query, IEnumerable<string> documents, string instruct = null)
    {
        string[] all = documents.ToArray();
        var scores = new double[all.Length];
        if (all.Length == 0 || string.IsNullOrWhiteSpace(query))
            return new Vector(scores);
        string answer = await _llm.SendAsync(Prompt(query, all, instruct), _settings).ConfigureAwait(false);
        List<int> order = Ranking(answer, all.Length);
        for (int place = 0; place < order.Count; place++)
            scores[order[place]] = 1.0 - (double)place / all.Length;
        return new Vector(scores);
    }

    /// <summary>Номера документов из ответа модели (с единицы), по порядку и без повторов; вне диапазона — пропускаются.</summary>
    internal static List<int> Ranking(string answer, int count)
    {
        var order = new List<int>();
        foreach (Match match in Numbers.Matches(answer ?? ""))
            if (int.TryParse(match.Value, out int number) && number >= 1 && number <= count && !order.Contains(number - 1))
                order.Add(number - 1);
        return order;
    }

    private string Prompt(string query, string[] documents, string instruct)
    {
        var text = new StringBuilder();
        text.Append("Упорядочи документы по тому, насколько каждый отвечает на вопрос.\n");
        if (!string.IsNullOrWhiteSpace(instruct))
            text.Append(instruct.Trim()).Append('\n');
        text.Append("\nВопрос: ").Append(query.Trim()).Append("\n\nДокументы:\n");
        for (int i = 0; i < documents.Length; i++)
        {
            string document = documents[i] ?? "";
            if (document.Length > MaxDocumentChars)
                document = document[..MaxDocumentChars] + "…";
            text.Append('[').Append(i + 1).Append("]\n").Append(document.Trim()).Append("\n\n");
        }
        text.Append("Ответь номерами документов, которые отвечают на вопрос, от самого подходящего, через запятую — например: 4, 1, 7. ")
            .Append("Документы, которые не отвечают, не называй. Без пояснений.");
        return text.ToString();
    }
}
