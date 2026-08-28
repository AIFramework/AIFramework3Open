using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>Машинное обучение: обучение, предсказание, метрики, разбиение выборки.</summary>
public sealed class MlTests
{
    /// <summary>Два хорошо разделённых облака точек: на них любой алгоритм обязан работать.</summary>
    private const string TwoClouds = """
        options { seed: 42 }

        let x = mat.of([
            <0, 0>, <0.2, 0.1>, <0.1, 0.3>, <0.3, 0.2>,
            <5, 5>, <5.2, 5.1>, <5.1, 5.3>, <5.3, 5.2>
        ])
        let y = <0, 0, 0, 0, 1, 1, 1, 1>
        """;

    [Fact]
    public void Ml_KMeans_SeparatesTwoClouds()
    {
        RunResult result = Script.RunOk($$"""
            {{TwoClouds}}
            let model = ml.kmeans(x, k: 2)
            let labels = model.predict(x)
            emit clusters = len(stat.counts(core.list(labels)))
            emit sameCloud = labels[0] == labels[3]
            emit differentClouds = labels[0] != labels[4]
            emit centroids = mat.rows(model.centroids())
            """);

        Assert.Equal(2.0, result.Emitted["clusters"]);
        Assert.Equal(true, result.Emitted["sameCloud"]);
        Assert.Equal(true, result.Emitted["differentClouds"]);
        Assert.Equal(2.0, result.Emitted["centroids"]);
    }

    [Fact]
    public void Ml_KMeans_IsReproducible()
    {
        const string source = $$"""
            {{TwoClouds}}
            let model = ml.kmeans(x, k: 2)
            emit labels = core.to_str(model.predict(x))
            """;

        Assert.Equal(Script.RunOk(source).Emitted["labels"], Script.RunOk(source).Emitted["labels"]);
    }

    [Fact]
    public void Ml_KMeans_RejectsImpossibleClusterCount()
    {
        Diagnostic error = Script.FailsWith($"{TwoClouds}\nemit r = ml.kmeans(x, k: 100)");

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("до числа объектов", error.Hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ml.knn(x, y, k: 3)")]
    [InlineData("ml.bayes(x, y)")]
    [InlineData("ml.nearest(x, y)")]
    public void Ml_Classifiers_LearnSeparableData(string constructor)
    {
        RunResult result = Script.RunOk($$"""
            {{TwoClouds}}
            let model = {{constructor}}
            emit score = model.score(x, y)
            """);

        Assert.Equal(1.0, (double)result.Emitted["score"]!, 6);
    }

    [Fact]
    public void Ml_Classifier_PredictsSingleVectorAsNumber()
    {
        RunResult result = Script.RunOk($$"""
            {{TwoClouds}}
            let model = ml.knn(x, y, k: 1)
            emit one = model.predict(<5, 5>)
            emit many = type(model.predict(x))
            """);

        Assert.Equal(1.0, result.Emitted["one"]);
        Assert.Equal("vec", result.Emitted["many"]);
    }

    [Fact]
    public void Ml_Classifier_Probabilities()
    {
        RunResult result = Script.RunOk($$"""
            {{TwoClouds}}
            let model = ml.bayes(x, y)
            emit single = type(model.predict_proba(<5, 5>))
            emit batch = type(model.predict_proba(x))
            """);

        Assert.Equal("vec", result.Emitted["single"]);
        Assert.Equal("mat", result.Emitted["batch"]);
    }

    [Fact]
    public void Ml_BinaryOnlyClassifier_RejectsThreeClasses()
    {
        Diagnostic error = Script.FailsWith("""
            let x = mat.of([<0>, <1>, <2>])
            let y = <0, 1, 2>
            emit r = ml.svm(x, y)
            """);

        Assert.Equal(DiagnosticCodes.BadOperand, error.Code);
        Assert.Contains("ml.knn", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Ml_FractionalLabel_IsRejected()
    {
        Diagnostic error = Script.FailsWith("""
            let x = mat.of([<0>, <1>])
            emit r = ml.knn(x, <0, 0.5>)
            """);

        Assert.Equal(DiagnosticCodes.TypeMismatch, error.Code);
        Assert.Contains("table.encode", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Ml_LabelCountMismatch_IsRejected()
    {
        Diagnostic error = Script.FailsWith("""
            let x = mat.of([<0>, <1>, <2>])
            emit r = ml.knn(x, <0, 1>)
            """);

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
        Assert.Contains("3 объектов и 2 меток", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ml_LinearRegression_RecoversLine()
    {
        RunResult result = Script.RunOk("""
            let x = <1, 2, 3, 4, 5>
            let y = <3, 5, 7, 9, 11>
            let model = ml.linreg(x, y)
            emit prediction = core.round(model.predict(6), digits: 6)
            emit score = core.round(model.score(x, y), digits: 6)
            """);

        Assert.Equal(13.0, result.Emitted["prediction"]);
        Assert.Equal(1.0, result.Emitted["score"]);
    }

    [Fact]
    public void Ml_MultipleRegression_FitsPlane()
    {
        RunResult result = Script.RunOk("""
            let x = mat.of([<1, 1>, <2, 1>, <3, 2>, <4, 3>, <5, 5>])
            let y = <3, 5, 8, 11, 15>
            let model = ml.multireg(x, y)
            emit good = model.score(x, y) > 0.95
            """);

        Assert.Equal(true, result.Emitted["good"]);
    }

    [Fact]
    public void Ml_PolynomialRegression_FitsCurve()
    {
        RunResult result = Script.RunOk("""
            let x = <0, 1, 2, 3, 4>
            let y = <0, 1, 4, 9, 16>
            let model = ml.polyreg(x, y, degree: 2)
            emit prediction = model.predict(5)
            """);

        Assert.Equal(25.0, (double)result.Emitted["prediction"]!, 1);
    }

    [Fact]
    public void Ml_Pca_ReducesDimension()
    {
        RunResult result = Script.RunOk("""
            let x = mat.of([<1, 2, 3>, <2, 4.1, 6>, <3, 5.9, 9.2>, <4, 8, 11.8>])
            let p = ml.pca(x, k: 1)
            let z = p.transform(x)
            emit rows = mat.rows(z)
            emit cols = mat.cols(z)
            emit eigen = len(p.eigenvalues()) > 0
            """);

        Assert.Equal(4.0, result.Emitted["rows"]);
        Assert.Equal(1.0, result.Emitted["cols"]);
        Assert.Equal(true, result.Emitted["eigen"]);
    }

    [Fact]
    public void Ml_Split_KeepsAllObjectsAndIsReproducible()
    {
        const string source = """
            options { seed: 3 }
            let x = table.of({ a: vec.arange(0, 20) }) |> table.to_matrix()
            let y = vec.arange(0, 20)
            let s = ml.split(x, y, test: 0.25)
            emit train = mat.rows(s.x_train)
            emit test = mat.rows(s.x_test)
            emit labels = len(s.y_train) + len(s.y_test)
            emit firstTest = s.y_test[0]
            """;

        RunResult first = Script.RunOk(source);
        RunResult second = Script.RunOk(source);

        Assert.Equal(15.0, first.Emitted["train"]);
        Assert.Equal(5.0, first.Emitted["test"]);
        Assert.Equal(20.0, first.Emitted["labels"]);
        Assert.Equal(first.Emitted["firstTest"], second.Emitted["firstTest"]);
    }

    [Fact]
    public void Ml_Split_KeepsObjectWithItsLabel()
    {
        // Признак равен метке, поэтому любое расхождение означает, что разбиение перепутало
        // строки с метками — самая дорогая из возможных ошибок здесь.
        RunResult result = Script.RunOk("""
            options { seed: 11 }
            let x = table.of({ a: vec.arange(0, 30) }) |> table.to_matrix()
            let y = vec.arange(0, 30)
            let s = ml.split(x, y, test: 0.3)
            let ok = 0
            for i in 0..len(s.y_test) {
                if s.x_test[i, 0] == s.y_test[i] { set ok += 1 }
            }
            emit matched = ok
            emit total = len(s.y_test)
            """);

        Assert.Equal(result.Emitted["total"], result.Emitted["matched"]);
    }

    [Fact]
    public void Stat_ClassificationMetrics()
    {
        RunResult result = Script.RunOk("""
            let y = <0, 0, 1, 1>
            let pred = <0, 1, 1, 1>
            emit accuracy = stat.accuracy(y, pred)
            emit f1 = stat.f1(y, pred) > 0.5
            emit confusion = mat.rows(stat.confusion(y, pred))
            emit report = len(stat.report(y, pred)) > 0
            """);

        Assert.Equal(0.75, result.Emitted["accuracy"]);
        Assert.Equal(true, result.Emitted["f1"]);
        Assert.Equal(2.0, result.Emitted["confusion"]);
        Assert.Equal(true, result.Emitted["report"]);
    }

    [Fact]
    public void Stat_Counts_ReturnsTable()
    {
        RunResult result = Script.RunOk("""
            let t = stat.counts([1, 1, 2])
            emit rows = len(t)
            emit first = t[0].count
            """);

        Assert.Equal(2.0, result.Emitted["rows"]);
        Assert.Equal(2.0, result.Emitted["first"]);
    }

    [Fact]
    public void Stat_Silhouette_IsHighForSeparatedClusters()
    {
        RunResult result = Script.RunOk($$"""
            {{TwoClouds}}
            let model = ml.kmeans(x, k: 2)
            emit good = stat.silhouette(x, model.predict(x)) > 0.8
            emit bad = stat.silhouette(x, <0, 1, 0, 1, 0, 1, 0, 1>) < 0.2
            """);

        Assert.Equal(true, result.Emitted["good"]);
        Assert.Equal(true, result.Emitted["bad"]);
    }

    [Fact]
    public void Ml_ModelIsHandle_WithReadableSummary()
    {
        RunResult result = Script.RunOk($$"""
            {{TwoClouds}}
            let model = ml.kmeans(x, k: 2)
            emit kind = type(model)
            emit text = core.to_str(model)
            """);

        Assert.Equal("handle", result.Emitted["kind"]);
        Assert.Contains("k-means", (string)result.Emitted["text"]!, StringComparison.Ordinal);
    }

    [Fact]
    public void Ml_UnknownMethodOnModel_IsReported()
    {
        Diagnostic error = Script.FailsWith($"{TwoClouds}\nlet m = ml.kmeans(x, k: 2)\nemit r = m.fit(x)");

        Assert.Equal(DiagnosticCodes.UnknownFunction, error.Code);
        Assert.Contains("ml.clustering", error.Message, StringComparison.Ordinal);
    }
}
