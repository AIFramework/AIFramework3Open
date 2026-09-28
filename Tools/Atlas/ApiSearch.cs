using AI.LLM.Core.Abstractions;
using AI.NLP;
using System.Xml.Linq;

namespace AiFramework.Tools.Atlas;

/// <summary>Найденная единица и то, какими каналами она найдена.</summary>
/// <param name="Unit">Единица.</param>
/// <param name="Score">Итоговая оценка ступени, которая выдала список.</param>
/// <param name="LexicalRank">Место в выдаче BM25, с единицы; <c>null</c> — BM25 её не нашёл.</param>
/// <param name="VectorRank">Место в выдаче векторов; <c>null</c> — векторы её не нашли или выключены.</param>
public sealed record SearchHit(CodeUnit Unit, double Score, int? LexicalRank, int? VectorRank);

/// <summary>Какие ступени поиска включить.</summary>
/// <param name="Lexical">Канал BM25.</param>
/// <param name="Vectors">Векторный канал, если векторы подключены.</param>
/// <param name="Rerank">Переранжировать первые кандидаты реранкером, если он подключён.</param>
/// <param name="Usage">Поднимать обжитые единицы: те, что много где вызываются.</param>
/// <param name="Glossary">Дополнять запрос переводами из глоссария. По замеру прироста не дал, выключен.</param>
/// <param name="GlossaryWeight">Вес переводов относительно слов самого запроса.</param>
/// <param name="Limit">Сколько единиц вернуть.</param>
public sealed record SearchOptions(
    bool Lexical = true, bool Vectors = true, bool Rerank = true, bool Usage = true,
    bool Glossary = false, double GlossaryWeight = 0.1, int Limit = 10);

/// <summary>
/// Поиск по открытому API библиотеки: BM25 и векторы, слияние по рангам, поправка на
/// обжитость, реранкер.
/// </summary>
/// <remarks>
/// Документ единицы для BM25 — её имя (дважды: совпадение с именем весит больше совпадения с
/// описанием), имя функции AIScript, текст <c>summary</c>, параметров и результата.
/// <c>remarks</c> не входит: там обычно обоснования и оговорки, и их слова уводят поиск в
/// сторону.
/// </remarks>
public sealed class ApiSearch
{
    private const int Candidates = 100;
    private const int RerankCandidates = 50;

    // Сглаживание Reciprocal Rank Fusion; 60 — значение из статьи Cormack и др. (2009).
    private const double RrfK = 60;

    // Вес поправки на обжитость: log(1 + число вызывающих) к оценке, делённой на лучшую.
    private const double UsageWeight = 0.05;

    // Выбор после поиска — прямое свидетельство пользы, весит вдвое больше обжитости.
    private const double SelectionWeight = 0.1;

    private readonly BM25 _bm25;
    private readonly Glossary _glossary;
    private readonly int[] _usage;
    private IReadOnlyDictionary<string, int>? _selections;
    private readonly HashSet<string>[] _tokens;
    private readonly Dictionary<(string, string), int> _position;
    private VectorIndex? _vectors;
    private IRerankerService<string, string>? _reranker;

    /// <summary>Строит индекс.</summary>
    /// <param name="units">Единицы, среди которых искать.</param>
    /// <param name="usage">Сколько мест вызывают единицу; для поправки на обжитость.</param>
    public ApiSearch(IReadOnlyList<CodeUnit> units, IReadOnlyDictionary<(string, string), int>? usage = null)
    {
        ArgumentNullException.ThrowIfNull(units);

        if (units.Count == 0) throw new InvalidOperationException("Искать не среди чего: индекс пуст. Запустите atlas index.");

        Units = units;

        var names = new List<string>[units.Count];
        var docs = new List<string>[units.Count];
        var documents = new IReadOnlyList<string>[units.Count];

        for (int i = 0; i < units.Count; i++)
        {
            names[i] = CodeTokenizer.Tokens(NameText(units[i]));
            docs[i] = CodeTokenizer.Tokens(DocText(units[i].Doc));

            var document = new List<string>(names[i]);
            document.AddRange(names[i]);
            if (units[i].Script != null) document.AddRange(CodeTokenizer.Tokens(units[i].Script!));
            document.AddRange(docs[i]);
            documents[i] = document;
        }

        _bm25 = new BM25(documents);
        _tokens = [.. documents.Select(document => document.ToHashSet(StringComparer.Ordinal))];
        _position = units.Select((unit, i) => ((unit.Project, unit.Id), i)).DistinctBy(pair => pair.Item1).ToDictionary(pair => pair.Item1, pair => pair.i);
        _glossary = Glossary.Build(names.Zip(docs, (name, doc) => ((IReadOnlyCollection<string>)name, (IReadOnlyCollection<string>)doc)));
        _usage = [.. units.Select(unit => usage?.GetValueOrDefault((unit.Project, unit.Id)) ?? 0)];
    }

    /// <summary>Единицы индекса.</summary>
    public IReadOnlyList<CodeUnit> Units { get; }

    /// <summary>Подключены ли векторы.</summary>
    public bool HasVectors => _vectors != null;

    /// <summary>Подключён ли реранкер.</summary>
    public bool HasReranker => _reranker != null;

    /// <summary>Глоссарий, выведенный из индекса.</summary>
    internal Glossary Glossary => _glossary;

    /// <summary>
    /// Доля слов запроса, найденных в документе единицы, с весом IDF: мера того, покрывает ли
    /// найденное запрос или это лучшее из плохого. Нужна, чтобы отличить пробел от находки.
    /// </summary>
    /// <remarks>
    /// Вес IDF, а не простая доля: в «среднее значение чисел» слова «значение» и «числа» есть
    /// почти в каждом описании, и без веса они топили бы единственное значимое слово.
    /// </remarks>
    public double Coverage(string query, CodeUnit unit)
    {
        string[] words = [.. CodeTokenizer.Tokens(query).Distinct(StringComparer.Ordinal)];

        if (words.Length == 0 || !_position.TryGetValue((unit.Project, unit.Id), out int index)) return 0;

        double total = words.Sum(_bm25.IDFWord);
        return total > 0 ? words.Where(_tokens[index].Contains).Sum(_bm25.IDFWord) / total : 0;
    }

    /// <summary>Подключает журнал выбора: выбранные после поиска единицы поднимаются в выдаче (читается вживую).</summary>
    public void UseSelections(IReadOnlyDictionary<string, int> selections) => _selections = selections;

    /// <summary>Подключает векторный канал; векторы построены по тем же единицам.</summary>
    public void UseVectors(VectorIndex vectors) => _vectors = vectors ?? throw new ArgumentNullException(nameof(vectors));

    /// <summary>Подключает реранкер.</summary>
    public void UseReranker(IRerankerService<string, string> reranker) => _reranker = reranker ?? throw new ArgumentNullException(nameof(reranker));

    /// <summary>Ищет единицы по запросу на естественном языке.</summary>
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchOptions? options = null, CancellationToken cancellation = default)
    {
        options ??= new SearchOptions();

        (int Index, double Score)[] lexical = options.Lexical ? Lexical(query, options) : [];
        (int Index, double Score)[] vector = options.Vectors && _vectors != null
            ? await _vectors.SearchAsync(query, Candidates, cancellation)
            : [];

        List<SearchHit> hits = Fuse(lexical, vector, options.Usage);

        if (options.Rerank && _reranker != null && hits.Count > 1) hits = await RerankAsync(query, hits, cancellation);

        return [.. hits.Take(options.Limit)];
    }

    /// <summary>
    /// Одна выдача — её оценки, делённые на лучшую; две — Reciprocal Rank Fusion. Ранги, а
    /// не оценки: косинус и BM25 в разных шкалах, и сумма оценок зависела бы от того, чья
    /// шкала крупнее.
    /// </summary>
    private double Prior(int index) =>
        UsageWeight * Math.Log(1 + _usage[index]) + SelectionWeight * Math.Log(1 + (_selections?.GetValueOrDefault(Units[index].Id) ?? 0));

    private List<SearchHit> Fuse((int Index, double Score)[] lexical, (int Index, double Score)[] vector, bool usage)
    {
        Dictionary<int, int> lexicalRank = lexical.Select((hit, rank) => (hit.Index, rank + 1)).ToDictionary();
        Dictionary<int, int> vectorRank = vector.Select((hit, rank) => (hit.Index, rank + 1)).ToDictionary();
        bool both = lexical.Length > 0 && vector.Length > 0;

        double Raw(int index) => both
            ? (lexicalRank.TryGetValue(index, out int l) ? 1 / (RrfK + l) : 0) + (vectorRank.TryGetValue(index, out int v) ? 1 / (RrfK + v) : 0)
            : lexical.Length > 0 ? lexical[lexicalRank[index] - 1].Score : vector[vectorRank[index] - 1].Score;

        int[] candidates = [.. lexicalRank.Keys.Union(vectorRank.Keys)];
        if (candidates.Length == 0) return [];

        double best = candidates.Max(Raw);

        return [.. candidates
            .Select(index => new SearchHit(
                Units[index],
                (best > 0 ? Raw(index) / best : 0) + (usage ? Prior(index) : 0),
                lexicalRank.TryGetValue(index, out int l) ? l : null,
                vectorRank.TryGetValue(index, out int v) ? v : null))
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.LexicalRank ?? int.MaxValue)
            .ThenBy(hit => hit.VectorRank ?? int.MaxValue)];
    }

    /// <summary>Первые кандидаты в порядке реранкера, остальные следом в прежнем порядке.</summary>
    private async Task<List<SearchHit>> RerankAsync(string query, List<SearchHit> hits, CancellationToken cancellation)
    {
        List<SearchHit> head = [.. hits.Take(RerankCandidates)];
        var scores = await _reranker!.SimsAsync(query, head.Select(hit => VectorIndex.Text(hit.Unit)));
        cancellation.ThrowIfCancellationRequested();

        return [.. head.Select((hit, i) => hit with { Score = scores[i] }).OrderByDescending(hit => hit.Score), .. hits.Skip(RerankCandidates)];
    }

    /// <summary>
    /// Лексическая выдача: BM25 по словам запроса плюс BM25 по их переводам с меньшим весом.
    /// </summary>
    /// <remarks>
    /// Переводы идут отдельным слагаемым, а не в общий список слов: с равным весом они
    /// перетягивали выдачу на английские имена, и точность падала (замер на эталоне).
    /// Оценка считается точно по объединению кандидатов обоих списков.
    /// </remarks>
    private (int Index, double Score)[] Lexical(string query, SearchOptions options)
    {
        List<string> tokens = CodeTokenizer.Tokens(query);
        List<string> translations = options.Glossary ? [.. _glossary.Expand(tokens).Skip(tokens.Count)] : [];

        HashSet<int> candidates = [.. _bm25.SearchTopN(tokens, Candidates).Where(hit => hit.score > 0).Select(hit => hit.index)];

        if (translations.Count > 0)
            candidates.UnionWith(_bm25.SearchTopN(translations, Candidates).Where(hit => hit.score > 0).Select(hit => hit.index));

        return [.. candidates
            .Select(index => (index, _bm25.Score(tokens, index) + (translations.Count > 0 ? options.GlossaryWeight * _bm25.Score(translations, index) : 0)))
            .Where(hit => hit.Item2 > 0)
            .OrderByDescending(hit => hit.Item2).ThenBy(hit => hit.index)
            .Take(Candidates)];
    }

    /// <summary>
    /// Имя единицы словами: пространство имён, тип, метод. Конструктор называется типом.
    /// </summary>
    public static string NameText(CodeUnit unit)
    {
        string[] parts = QualifiedName(unit.Id).Split('.');
        int take = unit.Kind == "type" ? 2 : 3;

        return string.Join(' ', parts.TakeLast(take).Where(part => part != "#ctor"));
    }

    /// <summary>
    /// Полное имя без префикса вида, параметров и обобщённой арности:
    /// <c>M:AI.DSP.FFT.CalcFFT(…)</c> → <c>AI.DSP.FFT.CalcFFT</c>.
    /// </summary>
    public static string QualifiedName(string id)
    {
        int colon = id.IndexOf(':');
        string name = colon >= 0 ? id[(colon + 1)..] : id;
        int paren = name.IndexOf('(');
        if (paren >= 0) name = name[..paren];

        return string.Join('.', name.Split('.').Select(part => part.Split('`')[0]));
    }

    /// <summary>Текст <c>summary</c>, параметров и результата из XML-комментария.</summary>
    public static string DocText(string doc)
    {
        if (doc.Length == 0) return "";

        try
        {
            XElement root = XElement.Parse($"<doc>{doc}</doc>");
            return string.Join(' ', root.Elements()
                .Where(element => element.Name.LocalName is "summary" or "param" or "returns" or "typeparam")
                .Select(element => element.Value));
        }
        catch (System.Xml.XmlException)
        {
            return "";
        }
    }
}
