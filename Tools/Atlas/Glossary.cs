namespace AiFramework.Tools.Atlas;

/// <summary>
/// Глоссарий «русский ↔ английский», выведенный из самого индекса.
/// </summary>
/// <remarks>
/// Описания в библиотеке русские, имена английские. Если слово описания и слово имени
/// встречаются в одних и тех же единицах заметно чаще случайного («свёртка» в описании,
/// <c>Convolution</c> в имени), это пара перевода. Сила связи — нормированная взаимная
/// информация (NPMI): 1 — всегда вместе, 0 — независимы. Запрос дополняется связанными
/// словами другого языка, и русский запрос находит английское имя без всякой модели.
/// </remarks>
internal sealed class Glossary
{
    private readonly Dictionary<string, (string Term, double Npmi)[]> _links;

    private Glossary(Dictionary<string, (string Term, double Npmi)[]> links) => _links = links;

    /// <summary>Число слов, у которых есть перевод.</summary>
    public int Count => _links.Count;

    /// <summary>Строит глоссарий.</summary>
    /// <param name="units">Пары «слова имени, слова описания» по единицам.</param>
    /// <param name="minPairs">Сколько раз пара должна встретиться, чтобы ей верить.</param>
    /// <param name="minNpmi">Порог силы связи.</param>
    /// <param name="perTerm">Сколько переводов оставлять у слова.</param>
    public static Glossary Build(IEnumerable<(IReadOnlyCollection<string> Name, IReadOnlyCollection<string> Doc)> units,
        int minPairs = 3, double minNpmi = 0.35, int perTerm = 2)
    {
        var single = new Dictionary<string, int>(StringComparer.Ordinal);
        var pairs = new Dictionary<(string English, string Russian), int>();
        int total = 0;

        foreach ((IReadOnlyCollection<string> name, IReadOnlyCollection<string> doc) in units)
        {
            HashSet<string> english = [.. name.Where(token => !CodeTokenizer.IsRussian(token))];
            HashSet<string> russian = [.. doc.Where(CodeTokenizer.IsRussian)];

            if (english.Count == 0 || russian.Count == 0) continue;

            total++;
            foreach (string token in english.Concat(russian)) single[token] = single.GetValueOrDefault(token) + 1;
            foreach (string e in english)
            {
                foreach (string r in russian) pairs[(e, r)] = pairs.GetValueOrDefault((e, r)) + 1;
            }
        }

        var links = new Dictionary<string, List<(string, double)>>(StringComparer.Ordinal);

        foreach (((string english, string russian), int together) in pairs)
        {
            if (together < minPairs) continue;

            double pxy = (double)together / total;
            double npmi = together == total ? 1 : Math.Log(pxy / ((double)single[english] / total * single[russian] / total)) / -Math.Log(pxy);

            if (npmi < minNpmi) continue;

            Add(links, english, russian, npmi);
            Add(links, russian, english, npmi);
        }

        return new Glossary(links.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.OrderByDescending(link => link.Item2).Take(perTerm).ToArray(),
            StringComparer.Ordinal));
    }

    /// <summary>Токены запроса вместе с переводами, без повторов добавленного.</summary>
    public List<string> Expand(IReadOnlyList<string> tokens)
    {
        var expanded = new List<string>(tokens);
        var seen = new HashSet<string>(tokens, StringComparer.Ordinal);

        foreach (string token in tokens)
        {
            if (!_links.TryGetValue(token, out var translations)) continue;

            foreach ((string term, _) in translations)
            {
                if (seen.Add(term)) expanded.Add(term);
            }
        }

        return expanded;
    }

    /// <summary>Переводы слова: для проверки глоссария глазами.</summary>
    public IReadOnlyList<(string Term, double Npmi)> Translations(string token) =>
        _links.TryGetValue(token, out var translations) ? translations : [];

    private static void Add(Dictionary<string, List<(string, double)>> links, string from, string to, double npmi)
    {
        if (!links.TryGetValue(from, out var list)) links[from] = list = [];
        list.Add((to, npmi));
    }
}
