using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;
using AI.LLM.Services.Embeddings.Base;
using Xunit;

namespace AiFramework.Tools.Atlas.UnitTests;

public sealed class SearchTests
{
    private static readonly CodeUnit[] Units =
    [
        Unit("T:AI.DSP.SpectrumAnalyzer", "type", "public sealed class SpectrumAnalyzer", "Амплитудный спектр сигнала."),
        Unit("M:AI.DSP.KalmanFilter.Update(System.Double)", "method", "public void KalmanFilter.Update(double z)", "Шаг фильтра Калмана по новому измерению."),
        Unit("M:AI.Econometrics.Smoother.Smooth(AI.DataStructs.Algebraic.Vector)", "method", "public Vector Smoother.Smooth(Vector series)", "Сглаживание временного ряда скользящим средним."),
        Unit("M:AI.Graphs.Dijkstra.ShortestPath(System.Int32,System.Int32)", "method", "public int[] Dijkstra.ShortestPath(int from, int to)", "Кратчайший путь между вершинами графа."),
    ];

    [Fact]
    public void TokenizerSplitsIdentifiersAndNormalizesWordForms()
    {
        Assert.Equal(["calc", "fft"], CodeTokenizer.Tokens("CalcFFT"));
        Assert.Equal(CodeTokenizer.Tokens("KalmanFilter"), CodeTokenizer.Tokens("kalman filters"));
        Assert.Equal(CodeTokenizer.Tokens("фильтр"), CodeTokenizer.Tokens("фильтра"));
        Assert.DoesNotContain("the", CodeTokenizer.Tokens("the spectrum"));
    }

    [Fact]
    public async Task LexicalChannelFindsByRussianDescriptionAndEnglishName()
    {
        var search = new ApiSearch(Units);

        Assert.Equal("M:AI.Graphs.Dijkstra.ShortestPath(System.Int32,System.Int32)", (await search.SearchAsync("кратчайший путь в графе"))[0].Unit.Id);
        Assert.Equal("M:AI.DSP.KalmanFilter.Update(System.Double)", (await search.SearchAsync("kalman"))[0].Unit.Id);
    }

    [Fact]
    public async Task UsagePriorLiftsWidelyUsedUnitAmongEquals()
    {
        CodeUnit[] twins =
        [
            Unit("M:A.Norm.L2(AI.DataStructs.Algebraic.Vector)", "method", "public double Norm.L2(Vector v)", "Евклидова норма вектора."),
            Unit("M:B.Norm.L2(AI.DataStructs.Algebraic.Vector)", "method", "public double Norm.L2(Vector v)", "Евклидова норма вектора."),
        ];
        var usage = new Dictionary<(string, string), int> { [("AI", twins[1].Id)] = 40 };

        var search = new ApiSearch(twins, usage);

        Assert.Equal(twins[0].Id, (await search.SearchAsync("норма вектора", new SearchOptions(Usage: false)))[0].Unit.Id);
        Assert.Equal(twins[1].Id, (await search.SearchAsync("норма вектора"))[0].Unit.Id);
    }

    [Fact]
    public async Task VectorChannelFindsWhatWordsMiss()
    {
        var search = new ApiSearch(Units);
        search.UseVectors(await VectorIndex.BuildAsync(Units, new TopicEmbedder()));

        // Ни одного общего слова со «Сглаживанием ряда», но по смыслу это оно.
        const string query = "убрать шум из показаний датчика";

        Assert.Empty(await search.SearchAsync(query, new SearchOptions(Vectors: false)));

        IReadOnlyList<SearchHit> hits = await search.SearchAsync(query);
        Assert.Equal("M:AI.Econometrics.Smoother.Smooth(AI.DataStructs.Algebraic.Vector)", hits[0].Unit.Id);
        Assert.Null(hits[0].LexicalRank);
        Assert.Equal(1, hits[0].VectorRank);
    }

    [Fact]
    public async Task FusionPrefersUnitsFoundByBothChannels()
    {
        var search = new ApiSearch(Units);
        search.UseVectors(await VectorIndex.BuildAsync(Units, new TopicEmbedder()));

        SearchHit top = (await search.SearchAsync("спектр сигнала", new SearchOptions(Usage: false)))[0];

        Assert.Equal("T:AI.DSP.SpectrumAnalyzer", top.Unit.Id);
        Assert.Equal(1, top.LexicalRank);
        Assert.Equal(1, top.VectorRank);
    }

    [Fact]
    public async Task RerankerReordersHead()
    {
        var search = new ApiSearch(Units);
        search.UseReranker(new KeywordReranker("Dijkstra"));

        IReadOnlyList<SearchHit> hits = await search.SearchAsync("сигнал граф");

        Assert.Equal("M:AI.Graphs.Dijkstra.ShortestPath(System.Int32,System.Int32)", hits[0].Unit.Id);
    }

    [Fact]
    public void LlmAnswerKeepsOnlyReferencesToShownCandidates()
    {
        SearchHit[] hits = [.. Units.Select((unit, i) => new SearchHit(unit, 1.0 / (i + 1), i + 1, null))];
        const string reply = """
            Вот выбор: {"picks":[{"n":3,"why":"сглаживает ряд"},{"n":9,"why":"нет такого"},{"n":3,"why":"повтор"},{"n":1,"why":"спектр"}],
            "missing":"нет оценки сезонности"}
            """;

        LlmAnswer answer = LlmSelector.Parse(reply, hits, hits);

        Assert.Equal([hits[2], hits[0]], answer.Picks.Select(pick => pick.Hit));
        Assert.Equal(2, answer.InvalidReferences);
        Assert.Equal("нет оценки сезонности", answer.Missing);
        Assert.Equal([hits[2], hits[0], hits[1], hits[3]], answer.Ordered);
    }

    [Fact]
    public void UnparsableLlmReplyKeepsOriginalOrder()
    {
        SearchHit[] hits = [.. Units.Select((unit, i) => new SearchHit(unit, 1, i + 1, null))];

        LlmAnswer answer = LlmSelector.Parse("не знаю", hits, hits);

        Assert.Empty(answer.Picks);
        Assert.Equal(1, answer.InvalidReferences);
        Assert.Equal(hits, answer.Ordered);
    }

    [Fact]
    public void GoldMatchesTypeAndMembersBySimpleOrFullName()
    {
        var bySimple = new GoldItem("tutorial", "t", "q", ["KalmanFilter"]);
        var byFull = new GoldItem("demo", "d", "q", ["AI.Graphs.Dijkstra"]);

        Assert.True(GoldSet.Matches(Units[1], bySimple));
        Assert.False(GoldSet.Matches(Units[0], bySimple));
        Assert.True(GoldSet.Matches(Units[3], byFull));
        Assert.True(new GoldItem("t", "t", "оценка через фильтр KalmanFilter", ["KalmanFilter"]).NamesAnswer);
        Assert.False(bySimple.NamesAnswer);
    }

    [Fact]
    public void GoldSetReadsTutorialsAndDemo()
    {
        string root = Directory.CreateTempSubdirectory("atlas-gold-").FullName;

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Docs", "Tutorials", "DSP"));
            File.WriteAllText(Path.Combine(root, "Docs", "Tutorials", "DSP", "kalman.md"), """
                # Фильтр Калмана

                ## Постановка задачи

                Дано: зашумлённые измерения. Требуется: **оценка** состояния.

                ## API

                | Метод | Описание |
                |-------|----------|
                | `KalmanFilter.Update(z)` | Шаг |
                | `KalmanState` | Состояние |
                | `src/AI.DSP/Kalman.cs` | Исходник |

                ## Код
                """);

            Directory.CreateDirectory(Path.Combine(root, "Demo"));
            File.WriteAllText(Path.Combine(root, "Demo", "Module.cs"), """
                var algos = new[] { new AlgoDef("dijkstra", "Кратчайший путь", "Алгоритм " + "Дейкстры", "AI.Graphs.Dijkstra", "d.md", []) };
                """);

            IReadOnlyList<GoldItem> gold = GoldSet.Load(root);

            GoldItem tutorial = Assert.Single(gold, item => item.Source == "tutorial");
            Assert.Equal("Дано: зашумлённые измерения. Требуется: оценка состояния.", tutorial.Query);
            Assert.Equal(["KalmanFilter", "KalmanState"], tutorial.Types);

            GoldItem demo = Assert.Single(gold, item => item.Source == "demo");
            Assert.Equal("Кратчайший путь. Алгоритм Дейкстры", demo.Query);
            Assert.Equal(["AI.Graphs.Dijkstra"], demo.Types);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CodeUnit Unit(string id, string kind, string signature, string summary) =>
        new("AI", id, kind, "public", "src/x.cs", 1, signature, $"<summary>{summary}</summary>", "", null, id);

    /// <summary>Векторы по темам: шум и сглаживание, фильтр Калмана, спектр, графы.</summary>
    private sealed class TopicEmbedder : EmbedderServiceBase
    {
        private static readonly string[][] Topics =
        [
            ["шум", "сглаж", "smooth", "датчик", "ряд"],
            ["калман", "kalman"],
            ["спектр", "spectrum"],
            ["граф", "путь", "dijkstra"],
        ];

        public override Task<Vector[]> EncodeAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default) =>
            Task.FromResult(texts.Select(text => new Vector(Topics.Select(words =>
                (double)words.Count(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))).Append(0.01).ToArray())).ToArray());
    }

    private sealed class KeywordReranker(string keyword) : IRerankerService<string, string>
    {
        public double Sim(string query, string document, string instruct = null!) =>
            document.Contains(keyword, StringComparison.Ordinal) ? 1.0 : 0.0;

        public Task<double> SimAsync(string query, string document, string instruct = null!) =>
            Task.FromResult(document.Contains(keyword, StringComparison.Ordinal) ? 1.0 : 0.0);

        public Task<Vector> SimsAsync(string query, IEnumerable<string> document, string instruct = null!) =>
            Task.FromResult(new Vector(document.Select(text => text.Contains(keyword, StringComparison.Ordinal) ? 1.0 : 0.0).ToArray()));
    }
}
