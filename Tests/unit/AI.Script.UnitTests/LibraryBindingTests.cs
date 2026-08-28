using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>Привязки к nlp, solve, graph, geom и fuzzy.</summary>
public sealed class LibraryBindingTests
{
    // --- nlp ---

    [Fact]
    public void Nlp_NormalizeAndWords()
    {
        RunResult result = Script.RunOk("""
            emit normalized = nlp.normalize("  Привет,   МИР! ")
            emit words = len(nlp.words("кот и пёс", drop_stop_words: true))
            emit all = len(nlp.words("кот и пёс"))
            """);

        // Нормализация приводит и знаки конца предложения к точке — это её работа.
        Assert.Equal("привет, мир.", result.Emitted["normalized"]);
        Assert.Equal(2.0, result.Emitted["words"]);
        Assert.Equal(3.0, result.Emitted["all"]);
    }

    [Fact]
    public void Nlp_StemAndLemma()
    {
        RunResult result = Script.RunOk("""
            emit stem = nlp.stem("бегущего")
            emit stems = len(nlp.stem(["кошки", "собаки"]))
            emit lemma = len(nlp.lemma("столами")) > 0
            """);

        Assert.NotEqual("бегущего", result.Emitted["stem"]);
        Assert.Equal(2.0, result.Emitted["stems"]);
        Assert.Equal(true, result.Emitted["lemma"]);
    }

    [Fact]
    public void Nlp_SentencesAndWindow()
    {
        RunResult result = Script.RunOk("""
            let text = "Первое предложение. Второе предложение. Третье предложение."
            let parts = nlp.sentences(text)
            emit sentences = len(parts) >= 3
            emit windows = len(parts |> nlp.window(size: 2, stride: 1)) >= 2
            """);

        Assert.Equal(true, result.Emitted["sentences"]);
        Assert.Equal(true, result.Emitted["windows"]);
    }

    [Fact]
    public void Nlp_Window_ValidatesArguments()
    {
        Assert.Equal(
            DiagnosticCodes.BadOperand,
            Script.FailsWith("emit r = [\"a\"] |> nlp.window(size: 0)").Code);
    }

    [Fact]
    public void Nlp_Similarity_IsHigherForCloserTexts()
    {
        RunResult result = Script.RunOk("""
            let a = "настройка прокси для сервера"
            emit близкий = nlp.similarity(a, "настройка прокси на сервере")
            emit далёкий = nlp.similarity(a, "рецепт борща с капустой")
            """);

        Assert.True((double)result.Emitted["близкий"]! > (double)result.Emitted["далёкий"]!);
    }

    [Fact]
    public void Nlp_Bow_CountsWords()
    {
        RunResult result = Script.RunOk("""
            let t = nlp.bow(["кот кот пёс", "кот"], top: 5)
            emit rows = len(t)
            emit top = t[0].word
            emit count = t[0].count
            """);

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal("кот", result.Emitted["top"]);
        Assert.Equal(3.0, result.Emitted["count"]);
    }

    /// <summary>
    /// Поиск обязан ставить первым документ, где запрос действительно есть: это и проверяется,
    /// а не то, что «функция что-то вернула».
    /// </summary>
    [Theory]
    [InlineData("nlp.bm25(docs)")]
    [InlineData("nlp.tfidf(docs)")]
    public void Nlp_Search_RanksRelevantDocumentFirst(string constructor)
    {
        RunResult result = Script.RunOk($$"""
            let docs = [
                "настройка ротации прокси и таймаутов",
                "рецепт борща с капустой и свёклой",
                "обучение нейросети на графическом процессоре"
            ]
            let index = {{constructor}}
            let found = index.search("прокси ротация", top: 3)
            emit best = found[0].doc
            emit rows = len(found)
            """);

        Assert.Equal(0.0, result.Emitted["best"]);
        Assert.Equal(3.0, result.Emitted["rows"]);
    }

    [Fact]
    public void Nlp_Index_RejectsEmptyCorpus()
    {
        Assert.Equal(DiagnosticCodes.SizeMismatch, Script.FailsWith("emit r = nlp.bm25([])").Code);
    }

    [Fact]
    public void Nlp_Score_ValidatesDocumentIndex()
    {
        Diagnostic error = Script.FailsWith("""
            let index = nlp.bm25(["а", "б"])
            emit r = index.score("а", doc: 5)
            """);

        Assert.Equal(DiagnosticCodes.IndexOutOfRange, error.Code);
    }

    // --- solve ---

    [Fact]
    public void Solve_SymbolicDerivative()
    {
        RunResult result = Script.RunOk("emit r = solve.diff(\"x^2\", by: \"x\")");
        string derivative = Assert.IsType<string>(result.Emitted["r"]);

        Assert.Contains("2", derivative, StringComparison.Ordinal);
        Assert.Contains("x", derivative, StringComparison.Ordinal);
    }

    [Fact]
    public void Solve_SymbolicIntegral()
    {
        RunResult result = Script.RunOk("emit r = solve.integrate(\"2*x\", by: \"x\")");

        Assert.Contains("x", Assert.IsType<string>(result.Emitted["r"]), StringComparison.Ordinal);
    }

    [Fact]
    public void Solve_EmptyExpression_IsRejected()
    {
        Assert.Equal(DiagnosticCodes.BadOperand, Script.FailsWith("emit r = solve.diff(\"  \")").Code);
    }

    [Fact]
    public void Solve_NumericIntegralOfScriptFunction()
    {
        RunResult result = Script.RunOk("""
            emit r = core.round(solve.integrate_fn(x => x * x, from: 0, to: 3), digits: 6)
            """);

        Assert.Equal(9.0, (double)result.Emitted["r"]!, 4);
    }

    [Fact]
    public void Solve_RootOfScriptFunction()
    {
        RunResult result = Script.RunOk("""
            emit r = core.round(solve.root(x => x * x - 2, from: 0, to: 2), digits: 6)
            """);

        Assert.Equal(Math.Sqrt(2), (double)result.Emitted["r"]!, 4);
    }

    [Fact]
    public void Solve_Root_ReportsMissingSignChange()
    {
        Diagnostic error = Script.FailsWith("emit r = solve.root(x => x * x + 1, from: 0, to: 2)");

        Assert.Equal(DiagnosticCodes.FunctionFailed, error.Code);
        Assert.Contains("меняла знак", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Solve_NumericDerivative()
    {
        RunResult result = Script.RunOk("emit r = core.round(solve.derivative_fn(x => x * x, at: 3), digits: 4)");

        Assert.Equal(6.0, (double)result.Emitted["r"]!, 2);
    }

    [Fact]
    public void Solve_Roots_FindsSeveral()
    {
        RunResult result = Script.RunOk("""
            emit r = len(solve.roots(x => math.sin(x), from: 0.5, to: 10)) >= 2
            """);

        Assert.Equal(true, result.Emitted["r"]);
    }

    // --- graph ---

    private const string SmallGraph = """
        let edges = table.of({
            from:   <0, 1, 2, 3>,
            to:     <1, 2, 3, 0>,
            weight: <1, 5, 1, 1>
        })
        let g = graph.of(edges)
        """;

    [Fact]
    public void Graph_BuildsAndReportsSize()
    {
        RunResult result = Script.RunOk($$"""
            {{SmallGraph}}
            let size = g.size()
            emit vertices = size.vertices
            emit edges = size.edges
            emit directed = size.directed
            """);

        Assert.Equal(4.0, result.Emitted["vertices"]);
        Assert.Equal(4.0, result.Emitted["edges"]);
        Assert.Equal(false, result.Emitted["directed"]);
    }

    [Fact]
    public void Graph_Dijkstra_PrefersCheaperPath()
    {
        // Прямое ребро 1→2 стоит 5, обход 1→0→3→2 стоит 3: алгоритм обязан выбрать обход.
        RunResult result = Script.RunOk($$"""
            {{SmallGraph}}
            let d = g.dijkstra(1)
            emit toTwo = d[2]
            """);

        Assert.Equal(3.0, result.Emitted["toTwo"]);
    }

    [Fact]
    public void Graph_BfsCountsEdgesNotWeights()
    {
        RunResult result = Script.RunOk($$"""
            {{SmallGraph}}
            emit hops = g.bfs(1)[2]
            emit path = len(g.path(1, to: 2))
            """);

        Assert.Equal(1.0, result.Emitted["hops"]);
        Assert.Equal(2.0, result.Emitted["path"]);
    }

    [Fact]
    public void Graph_UnreachableVertexIsInfinite()
    {
        RunResult result = Script.RunOk("""
            let edges = table.of({ from: <0>, to: <1> })
            let g = graph.of(edges, vertices: 4)
            emit far = math.is_finite(g.bfs(0)[3])
            emit components = stat.max(g.components())
            """);

        Assert.Equal(false, result.Emitted["far"]);
        Assert.Equal(2.0, result.Emitted["components"]);
    }

    [Fact]
    public void Graph_Mst_PicksCheapEdges()
    {
        RunResult result = Script.RunOk($$"""
            {{SmallGraph}}
            let tree = g.mst()
            emit weight = tree.weight
            emit edges = len(tree.edges)
            """);

        Assert.Equal(3.0, result.Emitted["weight"]);
        Assert.Equal(3.0, result.Emitted["edges"]);
    }

    [Fact]
    public void Graph_TopologicalOrderAndCycle()
    {
        RunResult result = Script.RunOk("""
            let acyclic = graph.of(table.of({ from: <0, 1>, to: <1, 2> }), directed: true)
            let cyclic = graph.of(table.of({ from: <0, 1, 2>, to: <1, 2, 0> }), directed: true)
            emit order = len(acyclic.topological().order)
            emit cycle = cyclic.topological().has_cycle
            """);

        Assert.Equal(3.0, result.Emitted["order"]);
        Assert.Equal(true, result.Emitted["cycle"]);
    }

    [Fact]
    public void Graph_Topological_RequiresDirectedGraph()
    {
        Diagnostic error = Script.FailsWith($"{SmallGraph}\nemit r = g.topological()");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("directed: true", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Graph_RejectsFractionalVertexNumber()
    {
        Diagnostic error = Script.FailsWith("emit r = graph.of(table.of({ from: <0.5>, to: <1> }))");

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
        Assert.Contains("table.encode", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Graph_ValidatesVertexIndex()
    {
        Assert.Equal(
            DiagnosticCodes.IndexOutOfRange,
            Script.FailsWith($"{SmallGraph}\nemit r = g.dijkstra(10)").Code);
    }

    // --- geom ---

    [Fact]
    public void Geom_TransformsCompose()
    {
        RunResult result = Script.RunOk("""
            let points = mat.of([<1, 0>])
            let moved = geom.apply(points, transform: geom.translate(dx: 2, dy: 3))
            emit x = moved[0, 0]
            emit y = moved[0, 1]
            """);

        Assert.Equal(3.0, result.Emitted["x"]);
        Assert.Equal(3.0, result.Emitted["y"]);
    }

    [Fact]
    public void Geom_Apply_ValidatesShapes()
    {
        Assert.Equal(
            DiagnosticCodes.SizeMismatch,
            Script.FailsWith("emit r = geom.apply(mat.of([<1, 2, 3>]), transform: geom.rotate(0))").Code);
    }

    [Fact]
    public void Geom_FitLine_RecoversSlope()
    {
        RunResult result = Script.RunOk("""
            let points = mat.of([<0, 1>, <1, 3>, <2, 5>, <3, 7>])
            let line = geom.fit_line(points)
            emit slope = core.round(line.slope, digits: 6)
            emit intercept = core.round(line.intercept, digits: 6)
            """);

        Assert.Equal(2.0, (double)result.Emitted["slope"]!, 4);
        Assert.Equal(1.0, (double)result.Emitted["intercept"]!, 4);
    }

    [Fact]
    public void Geom_FitLineRobust_IgnoresOutlier()
    {
        // Одна выброшенная точка не должна сдвинуть прямую: в этом весь смысл RANSAC.
        RunResult result = Script.RunOk("""
            let points = mat.of([<0, 1>, <1, 3>, <2, 5>, <3, 7>, <4, 100>])
            let robust = geom.fit_line_robust(points, threshold: 0.5)
            emit slope = core.round(robust.slope, digits: 3)
            emit outlier = robust.inliers[4]
            """);

        Assert.Equal(2.0, (double)result.Emitted["slope"]!, 1);
        Assert.Equal(0.0, result.Emitted["outlier"]);
    }

    [Fact]
    public void Geom_FitCircle_RecoversRadius()
    {
        RunResult result = Script.RunOk("""
            let points = mat.of([<1, 0>, <0, 1>, <-1, 0>, <0, -1>])
            let circle = geom.fit_circle(points)
            emit radius = core.round(circle.radius, digits: 4)
            emit x = core.round(circle.x, digits: 4)
            """);

        Assert.Equal(1.0, (double)result.Emitted["radius"]!, 3);
        Assert.Equal(0.0, (double)result.Emitted["x"]!, 3);
    }

    [Fact]
    public void Geom_BezierAndCentroidAndDistance()
    {
        RunResult result = Script.RunOk("""
            let control = mat.of([<0, 0>, <1, 2>, <2, 0>])
            emit points = mat.rows(geom.bezier(control, points: 20))
            emit centroid = geom.centroid(control)[0]
            emit distance = geom.distance(<0, 0>, <3, 4>)
            """);

        Assert.Equal(20.0, result.Emitted["points"]);
        Assert.Equal(1.0, result.Emitted["centroid"]);
        Assert.Equal(5.0, result.Emitted["distance"]);
    }

    [Fact]
    public void Geom_FitCircle_RequiresThreePoints()
    {
        Assert.Equal(
            DiagnosticCodes.SizeMismatch,
            Script.FailsWith("emit r = geom.fit_circle(mat.of([<0, 0>, <1, 1>]))").Code);
    }

    // --- fuzzy ---

    [Fact]
    public void Fuzzy_TriangleMembership()
    {
        RunResult result = Script.RunOk("""
            let u = fuzzy.universe(0, 40, n: 41)
            let warm = fuzzy.triangle(u, a: 10, b: 20, c: 30)
            emit peak = fuzzy.degree(u, term: warm, at: 20)
            emit edge = fuzzy.degree(u, term: warm, at: 10)
            emit outside = fuzzy.degree(u, term: warm, at: 35)
            emit half = core.round(fuzzy.degree(u, term: warm, at: 15), digits: 3)
            """);

        Assert.Equal(1.0, result.Emitted["peak"]);
        Assert.Equal(0.0, result.Emitted["edge"]);
        Assert.Equal(0.0, result.Emitted["outside"]);
        Assert.Equal(0.5, (double)result.Emitted["half"]!, 2);
    }

    [Fact]
    public void Fuzzy_LogicOperations()
    {
        RunResult result = Script.RunOk("""
            let u = fuzzy.universe(0, 10, n: 11)
            let a = fuzzy.triangle(u, a: 0, b: 5, c: 10)
            let b = fuzzy.trapezoid(u, a: 3, b: 4, c: 6, d: 7)
            emit and = stat.max(fuzzy.and(a, b)) <= stat.max(a)
            emit or = stat.max(fuzzy.or(a, b)) >= stat.max(a)
            emit not = core.round(stat.max(fuzzy.not(a)), digits: 3)
            """);

        Assert.Equal(true, result.Emitted["and"]);
        Assert.Equal(true, result.Emitted["or"]);
        Assert.Equal(1.0, result.Emitted["not"]);
    }

    /// <summary>
    /// Вывод по Мамдани: при перевесе второго правила результат смещается к его терму.
    /// </summary>
    [Fact]
    public void Fuzzy_Infer_ShiftsTowardsHeavierRule()
    {
        RunResult result = Script.RunOk("""
            let u = fuzzy.universe(0, 100, n: 101)
            let slow = fuzzy.triangle(u, a: 0, b: 10, c: 30)
            let fast = fuzzy.triangle(u, a: 70, b: 90, c: 100)
            emit toSlow = fuzzy.infer(u, weights: <0.9, 0.1>, terms: [slow, fast])
            emit toFast = fuzzy.infer(u, weights: <0.1, 0.9>, terms: [slow, fast])
            """);

        Assert.True((double)result.Emitted["toSlow"]! < (double)result.Emitted["toFast"]!);
    }

    [Fact]
    public void Fuzzy_Infer_ValidatesRuleCount()
    {
        Diagnostic error = Script.FailsWith("""
            let u = fuzzy.universe(0, 10, n: 11)
            emit r = fuzzy.infer(u, weights: <0.5>, terms: [fuzzy.triangle(u, a: 0, b: 5, c: 10), u])
            """);

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
        Assert.Contains("свой вес", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Fuzzy_MismatchedUniverse_IsReported()
    {
        Diagnostic error = Script.FailsWith("""
            let u = fuzzy.universe(0, 10, n: 11)
            emit r = fuzzy.and(fuzzy.triangle(u, a: 0, b: 5, c: 10), <1, 2>)
            """);

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
        Assert.Contains("fuzzy.universe", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Fuzzy_Defuzzify_FindsCentre()
    {
        RunResult result = Script.RunOk("""
            let u = fuzzy.universe(0, 100, n: 101)
            let middle = fuzzy.triangle(u, a: 40, b: 50, c: 60)
            emit centre = core.round(fuzzy.defuzzify(u, term: middle), digits: 1)
            """);

        Assert.Equal(50.0, (double)result.Emitted["centre"]!, 1);
    }
}
