using AI.NLP;

namespace AiFrameworkDemo.Core;

/// <summary>
/// Запись каталога алгоритмов — плоская проекция иерархии LibraryRegistry.
/// </summary>
public record AlgoEntry(
    string LibId,
    string LibName,
    string LibColor,
    string LibIconSvg,
    string CatId,
    string CatTitle,
    string AlgoKey,
    string Title,
    string Subtitle,
    string ApiClass);

/// <summary>
/// Глобальный BM25-индекс всех алгоритмов из LibraryRegistry.
/// В каждый документ включается: заголовок, подзаголовок, API-класс,
/// название библиотеки и категории, а также полный текст теории (.md файл).
/// Инициализируется один раз при первом обращении.
/// </summary>
public static class AlgoSearchIndex
{
    private static readonly List<AlgoEntry> _entries;
    private static readonly BM25 _bm25;

    static AlgoSearchIndex()
    {
        // Собираем все алгоритмы вместе с TutorialFolder и TheoryFile
        var raw = LibraryRegistry.Modules
            .SelectMany(lib => lib.Categories
                .SelectMany(cat => cat.Algorithms
                    .Select(algo => (
                        Entry: new AlgoEntry(
                            lib.Id, lib.Name, lib.Color, lib.IconSvg,
                            cat.Id, cat.Title,
                            algo.Key, algo.Title, algo.Subtitle, algo.ApiClass),
                        TutorialFolder: lib.TutorialFolder,
                        TheoryFile: algo.TheoryFile
                    ))))
            .ToList();

        _entries = raw.Select(x => x.Entry).ToList();

        // Документ BM25 = мета-поля + сырой текст теории (Markdown stripped)
        string[] docs = raw
            .Select(x =>
            {
                string theory = TheoryLoader.LoadText(x.TutorialFolder, x.TheoryFile);
                return $"{x.Entry.Title} {x.Entry.Subtitle} {x.Entry.ApiClass} " +
                       $"{x.Entry.LibName} {x.Entry.CatTitle} {theory}";
            })
            .ToArray();

        _bm25 = docs.Length > 0
            ? new BM25(docs)
            : new BM25(["placeholder"]);
    }

    /// <summary>Все алгоритмы из каталога.</summary>
    public static IReadOnlyList<AlgoEntry> All => _entries;

    /// <summary>
    /// Полнотекстовый BM25-поиск по каталогу.
    /// Если задан libId — поиск только внутри этой библиотеки.
    /// Возвращает результаты, отсортированные по убыванию релевантности (score > 0).
    /// </summary>
    public static List<(AlgoEntry Entry, double Score)> Search(string query, string? libId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var candidateIndexes = Enumerable.Range(0, _entries.Count)
            .Where(i => libId == null || _entries[i].LibId == libId)
            .ToList();

        return candidateIndexes
            .Select(i => (_entries[i], _bm25.Score(query, i)))
            .Where(x => x.Item2 > 0)
            .OrderByDescending(x => x.Item2)
            .ToList();
    }

    /// <summary>
    /// Все алгоритмы заданной библиотеки в исходном порядке.
    /// </summary>
    public static List<AlgoEntry> GetByLibrary(string libId)
        => _entries.Where(e => e.LibId == libId).ToList();
}
