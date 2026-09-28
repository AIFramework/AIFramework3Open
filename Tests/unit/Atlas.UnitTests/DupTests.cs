using AI.NLP.Similarity;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AiFramework.Tools.Atlas.UnitTests;

public sealed class DupTests : IDisposable
{
    private const string TransposeA = """
        {
            int n = matrix.GetLength(0), m = matrix.GetLength(1);
            var result = new double[m, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                    result[j, i] = matrix[i, j];
            return result;
        }
        """;

    // Та же функция с другими именами переменных.
    private const string TransposeB = """
        {
            int rows = a.GetLength(0), cols = a.GetLength(1);
            var t = new double[cols, rows];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    t[c, r] = a[r, c];
            return t;
        }
        """;

    private const string SumOfSquares = """
        {
            double total = 0;
            foreach (double value in values)
            {
                if (double.IsNaN(value)) continue;
                total += value * value * 0.5772;
            }
            return Math.Sqrt(total);
        }
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("atlas-dup-").FullName;

    [Fact]
    public void RenamedCopyHasIdenticalShape()
    {
        CodeShape a = CodeShapes.Of(TransposeA)!, b = CodeShapes.Of(TransposeB)!;

        Assert.Equal(1.0, MinHash.Jaccard(a.Shingles, b.Shingles));
        Assert.True(MinHash.Jaccard(a.Shingles, CodeShapes.Of(SumOfSquares)!.Shingles) < 0.2);
        Assert.Contains("0.5772", CodeShapes.Of(SumOfSquares)!.Constants);
        Assert.Equal([2, 0, 1, 0], a.Control);
    }

    [Fact]
    public void TextBodyIsRecognised()
    {
        CodeShape text = CodeShapes.Of("""{ return "Справка:\n" + "строка один\n" + "строка два\n" + "строка три\n" + "строка четыре"; }""")!;

        Assert.True(text.IsText);
        Assert.False(CodeShapes.Of(TransposeA)!.IsText);
    }

    [Fact]
    public void DetectorFindsCopyAndSkipsWrapper()
    {
        CodeUnit first = Method("M:Lib.Algebra.Transpose(System.Double[0:,0:])", TransposeA, "public static double[,] Algebra.Transpose(double[,] matrix)");
        CodeUnit second = Method("M:Lib.Pde.Elastic.Transpose(System.Double[0:,0:])", TransposeB, "private static double[,] Elastic.Transpose(double[,] a)", project: "Lib.Pde");
        CodeUnit wrapper = Method("M:Lib.Algebra.TransposeCopy(System.Double[0:,0:])", TransposeB, "public static double[,] Algebra.TransposeCopy(double[,] a)");

        var calls = new Dictionary<(string, string), HashSet<string>> { [("Lib", wrapper.Id)] = [first.Project + "|" + first.Id] };
        DupDetector detector = Detector([first, second, wrapper], calls);

        IReadOnlyList<DupFinding> findings = detector.FindAll();

        DupFinding copy = Assert.Single(findings, f => Pair(f, first, second));
        Assert.Equal("копия", copy.Verdict);
        Assert.Equal(first.Id, copy.Canonical.Id);
        Assert.DoesNotContain(findings, f => Pair(f, first, wrapper));
    }

    [Fact]
    public void SameNamedFunctionsAreOneSubjectButPolymorphicMethodsAreNot()
    {
        CodeUnit gammaA = Method("M:Lib.Special.LogGamma(System.Double)", SumOfSquares, "public static double Special.LogGamma(double x)", inputs: "double", returns: "double");
        CodeUnit gammaB = Method("M:Lib.Stats.LogGamma(System.Double)", TransposeA, "public static double Stats.LogGamma(double x)", inputs: "double", returns: "double");
        CodeUnit refitA = Method("M:Lib.Laplace.Refit(System.Double[])", SumOfSquares, "public override Dist Laplace.Refit(double[] data)", inputs: "double[]", returns: "Lib.Dist");
        CodeUnit refitB = Method("M:Lib.Gauss.Refit(System.Double[])", TransposeA, "public override Dist Gauss.Refit(double[] data)", inputs: "double[]", returns: "Lib.Dist");

        IReadOnlyList<DupFinding> findings = Detector([gammaA, gammaB, refitA, refitB], []).FindAll();

        Assert.Equal("один предмет, разный код", Assert.Single(findings, f => Pair(f, gammaA, gammaB)).Verdict);
        Assert.DoesNotContain(findings, f => Pair(f, refitA, refitB));
    }

    [Fact]
    public void BoilerplateConstructorsAreNotCopies()
    {
        const string assignments = "{ _agents = agents; _tasks = tasks; _count = agents.Count; _cost = new double[agents.Count]; _best = -1; _iterations = 0; }";
        CodeUnit a = Method("M:Lib.Auction.#ctor(Lib.A,Lib.T)", assignments, "public Auction(A agents, T tasks)", kind: "ctor");
        CodeUnit b = Method("M:Lib.Colony.#ctor(Lib.A,Lib.T)", assignments, "public Colony(A agents, T tasks)", kind: "ctor");

        Assert.Equal(DupDetector.Unrelated, Detector([a, b], []).Compare(a, b)!.Verdict);
    }

    [Fact]
    public void DraftIsCheckedAgainstLibrary()
    {
        CodeUnit library = Method("M:Lib.Algebra.Transpose(System.Double[0:,0:])", TransposeA, "public static double[,] Algebra.Transpose(double[,] matrix)");

        // Слой черновика неизвестен (0), но каноническая версия всё равно библиотечная.
        var detector = new DupDetector([library], new Dictionary<(string, string), HashSet<string>>(), new Dictionary<(string, string), int>(), project => project == "Lib" ? 1 : 0);

        IReadOnlyList<DupFinding> findings = detector.Check("public static double[,] Flip(double[,] a) " + TransposeB, "транспонировать матрицу");

        Assert.Equal(library.Id, Assert.Single(findings).B.Id);
        Assert.Equal("копия", findings[0].Verdict);
        Assert.Equal(library.Id, findings[0].Canonical.Id);
    }

    [Fact]
    public void LedgerKeepsDecisionUntilBodyChanges()
    {
        string path = Path.Combine(_folder, "decisions.json");
        CodeUnit a = Method("M:Lib.A.F", TransposeA, "void A.F()");
        CodeUnit b = Method("M:Lib.B.F", TransposeB, "void B.F()");

        DupLedger ledger = DupLedger.Open(path);
        ledger.Decide(a, b, "перекрытие", "разные контракты");

        DupLedger reopened = DupLedger.Open(path);
        Assert.Equal("перекрытие", reopened.Find(b, a)!.Verdict);

        CodeUnit changed = a with { Hash = "другой" };
        Assert.Null(reopened.Find(changed, b));
        Assert.True(reopened.IsReopened(changed, b));
    }

    [Fact]
    public void LedgerSeedsCoverWholeTypes()
    {
        DupLedger ledger = DupLedger.Open(Path.Combine(_folder, "seeded.json"));
        CodeUnit ols = Method("M:AI.Econometrics.Numerics.Ols.Fit(System.Double[0:,0:])", TransposeA, "Ols.Fit");
        CodeUnit regression = Method("M:AI.ML.Regression.MultipleRegression.Train(AI.DataStructs.Algebraic.Matrix)", TransposeB, "MultipleRegression.Train");

        Assert.Equal(3, ledger.Decisions.Count);
        Assert.Equal("перекрытие", ledger.Find(regression, ols)!.Verdict);
    }

    [Theory]
    [InlineData("""Ответ: {"verdict":"Расхождение","why":"нет вычитания максимума"}""", "расхождение")]
    [InlineData("""{"verdict":"похоже","why":"?"}""", "")]
    [InlineData("не знаю", "")]
    public void JudgeAcceptsOnlyKnownVerdicts(string reply, string verdict) =>
        Assert.Equal(verdict, DupJudge.Parse(reply).Verdict);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private static bool Pair(DupFinding finding, CodeUnit a, CodeUnit b) =>
        (finding.A.Id == a.Id && finding.B.Id == b.Id) || (finding.A.Id == b.Id && finding.B.Id == a.Id);

    private static DupDetector Detector(CodeUnit[] units, Dictionary<(string, string), HashSet<string>> calls) =>
        new(units, calls, new Dictionary<(string, string), int>(), project => project == "Lib" ? 1 : 3);

    private static CodeUnit Method(string id, string body, string signature, string project = "Lib", string kind = "method",
        string inputs = "double[,]", string returns = "double[,]") =>
        new(project, id, kind, signature.Contains("private ", StringComparison.Ordinal) ? "private" : "public", "src/x.cs", 1,
            signature, "", body, null, SymbolUnits.Hash(body), Returns: returns, Inputs: inputs);
}
