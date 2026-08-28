using AI.DataStructs.Algebraic;
using AI.KNN;
using AI.ML.Classification;
using AI.ML.Clustering;
using AI.ML.DataHandling.FeaturesTransforms;
using AI.ML.Regression;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>ml</c>: классическое машинное обучение.
/// </summary>
/// <remarks>
/// Модель — дескриптор, а не значение: она изменяема, велика и не переносится в <c>emit</c>.
/// Обучение происходит при создании (<c>ml.kmeans(x, k: 4)</c> возвращает уже обученную
/// модель), потому что необученная модель в прототипе не нужна никому, а отдельный шаг
/// <c>fit</c> — это ещё одно место, где можно забыть его сделать.
/// <para>
/// Типов дескрипторов четыре — по роли, а не по алгоритму: <c>ml.classifier</c>,
/// <c>ml.clustering</c>, <c>ml.regression</c>, <c>ml.pca</c>. Так <c>predict</c> пишется один
/// раз на роль, а не по разу на каждый алгоритм.
/// </para>
/// </remarks>
[ScriptModule("ml", "Классическое машинное обучение: классификация, кластеризация, регрессия, PCA", Version = "0.1")]
public static class MlModule
{
    /// <summary>Тип-тег дескриптора классификатора.</summary>
    public const string ClassifierHandle = "ml.classifier";

    /// <summary>Тип-тег дескриптора кластеризации.</summary>
    public const string ClusteringHandle = "ml.clustering";

    /// <summary>Тип-тег дескриптора регрессии.</summary>
    public const string RegressionHandle = "ml.regression";

    /// <summary>Тип-тег дескриптора PCA.</summary>
    public const string PcaHandle = "ml.pca";

    // --- кластеризация ---

    [ScriptFn("kmeans", "Кластеризация методом k-средних", Returns = ClusteringHandle,
        Example = "let m = ml.kmeans(x, k: 4)\nlet labels = m.predict(x)")]
    public static ScriptHandle KMeans(
        IScriptContext context,
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("число кластеров")] int k,
        [ScriptParam("зерно ГСЧ; 0 — из options.seed")] int seed = 0)
    {
        _ = Datasets.RequireNotEmpty(data, "ml.kmeans");
        RequireClusters(k, data.Height, "ml.kmeans");

        var model = new KMeans(k);
        model.Train(Datasets.Rows(data), seed == 0 ? context.Seed : seed);

        return new ScriptHandle(ClusteringHandle, model, $"k-means, k={k}, обучена на {data.Height}×{data.Width}");
    }

    [ScriptFn("fast_kmeans", "Быстрая кластеризация k-средних на дереве шаров", Returns = ClusteringHandle,
        Example = "ml.fast_kmeans(x, k: 8)")]
    public static ScriptHandle FastKMeans(
        IScriptContext context,
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("число кластеров")] int k,
        [ScriptParam("зерно ГСЧ; 0 — из options.seed")] int seed = 0)
    {
        _ = Datasets.RequireNotEmpty(data, "ml.fast_kmeans");
        RequireClusters(k, data.Height, "ml.fast_kmeans");

        var model = new FastKMeans(k);
        model.Train(Datasets.Rows(data), seed == 0 ? context.Seed : seed);

        return new ScriptHandle(ClusteringHandle, model, $"fast k-means, k={k}");
    }

    [ScriptFn("forel", "Кластеризация ФОРЭЛ: число кластеров определяется радиусом", Returns = ClusteringHandle,
        Example = "ml.forel(x, radius: 2)")]
    public static ScriptHandle Forel(
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("радиус сферы поиска")] int radius = 0)
    {
        _ = Datasets.RequireNotEmpty(data, "ml.forel");

        var model = new Forel();
        model.Train(Datasets.Rows(data), radius);

        return new ScriptHandle(ClusteringHandle, model, $"ФОРЭЛ, кластеров: {model.Centroids.Length}");
    }

    [ScriptFn("som", "Самоорганизующаяся карта Кохонена", Returns = ClusteringHandle,
        Example = "ml.som(x, k: 4)")]
    public static ScriptHandle Som(
        IScriptContext context,
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("число кластеров")] int k,
        [ScriptParam("зерно ГСЧ; 0 — из options.seed")] int seed = 0)
    {
        _ = Datasets.RequireNotEmpty(data, "ml.som");
        RequireClusters(k, data.Height, "ml.som");

        var model = new KohonenNet(k, data.Width, seed == 0 ? Math.Max(1, context.Seed) : seed);
        model.Train(Datasets.Rows(data), 0);

        return new ScriptHandle(ClusteringHandle, model, $"карта Кохонена, k={k}");
    }

    [ScriptFn("predict", "Метки для новых объектов", Example = "model.predict(x)")]
    [ScriptMethod(ClusteringHandle)]
    public static ScriptValue ClusteringPredict(
        [ScriptParam("обученная модель")] ScriptHandle model,
        [ScriptParam("вектор-объект либо матрица объект × признак")] ScriptValue data)
    {
        var clustering = (IClustering)model.Target;

        return Apply(data, vector => clustering.Classify(vector), "ml.predict");
    }

    [ScriptFn("centroids", "Центры кластеров матрицей", Example = "model.centroids()")]
    [ScriptMethod(ClusteringHandle)]
    public static Matrix Centroids([ScriptParam("обученная модель")] ScriptHandle model) =>
        Datasets.FromRows(((IClustering)model.Target).Centroids);

    // --- классификация ---

    [ScriptFn("knn", "Классификатор ближайших соседей", Returns = ClassifierHandle,
        Example = "let m = ml.knn(x, y, k: 5)")]
    public static ScriptHandle Knn(
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("метки классов")] Vector labels,
        [ScriptParam("число соседей")] int k = 4)
    {
        Prepare(data, labels, "ml.knn", out Vector[] rows, out int[] classes);

        var model = new KNNCl { K = k };
        model.Train(rows, classes);

        return new ScriptHandle(ClassifierHandle, model, $"kNN, k={k}, классов: {Distinct(classes)}");
    }

    [ScriptFn("nearest", "Классификатор по ближайшему эталону", Returns = ClassifierHandle,
        Example = "ml.nearest(x, y)")]
    public static ScriptHandle Nearest(
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("метки классов")] Vector labels)
    {
        Prepare(data, labels, "ml.nearest", out Vector[] rows, out int[] classes);

        var model = new NN();
        model.Train(rows, classes);

        return new ScriptHandle(ClassifierHandle, model, $"ближайший эталон, классов: {Distinct(classes)}");
    }

    [ScriptFn("bayes", "Байесовский классификатор", Returns = ClassifierHandle,
        Example = "ml.bayes(x, y)")]
    public static ScriptHandle Bayes(
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("метки классов")] Vector labels)
    {
        Prepare(data, labels, "ml.bayes", out Vector[] rows, out int[] classes);

        var model = new BayesianClassifier();
        model.Train(rows, classes);

        return new ScriptHandle(ClassifierHandle, model, $"байесовский, классов: {Distinct(classes)}");
    }

    /// <summary>
    /// Линейный классификатор с зазором (SVM).
    /// </summary>
    /// <remarks>
    /// Реализация во фреймворке двухклассовая, поэтому метки проверяются заранее: молча
    /// обучиться на трёх классах и выдать бессмысленные предсказания — худший из исходов.
    /// </remarks>
    [ScriptFn("svm", "Линейный классификатор с зазором (двухклассовый)", Returns = ClassifierHandle,
        Example = "ml.svm(x, y, c: 1.0)")]
    public static ScriptHandle Svm(
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("метки классов: ровно два значения")] Vector labels,
        [ScriptParam("коэффициент регуляризации")] double c = 1.0,
        [ScriptParam("скорость обучения")] double lr = 0.01,
        [ScriptParam("число эпох")] int epochs = 10)
    {
        Prepare(data, labels, "ml.svm", out Vector[] rows, out int[] classes);
        RequireBinary(classes, "ml.svm");

        var model = new SVMBinary(data.Width) { C = c, LearningRate = lr, EpochesToPass = epochs };
        model.Train(rows, classes);

        return new ScriptHandle(ClassifierHandle, model, $"SVM, C={c}, признаков: {data.Width}");
    }

    [ScriptFn("linear_cls", "Линейный классификатор (двухклассовый)", Returns = ClassifierHandle,
        Example = "ml.linear_cls(x, y)")]
    public static ScriptHandle LinearClassifier(
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("метки классов: ровно два значения")] Vector labels,
        [ScriptParam("скорость обучения")] double lr = 0.01,
        [ScriptParam("число эпох")] int epochs = 10)
    {
        Prepare(data, labels, "ml.linear_cls", out Vector[] rows, out int[] classes);
        RequireBinary(classes, "ml.linear_cls");

        var model = new LinearClassifierBinarry(data.Width) { LearningRate = lr, EpochesToPass = epochs };
        model.Train(rows, classes);

        return new ScriptHandle(ClassifierHandle, model, $"линейный, признаков: {data.Width}");
    }

    [ScriptFn("predict", "Предсказанные метки классов", Example = "model.predict(x)")]
    [ScriptMethod(ClassifierHandle)]
    public static ScriptValue ClassifierPredict(
        [ScriptParam("обученная модель")] ScriptHandle model,
        [ScriptParam("вектор-объект либо матрица объект × признак")] ScriptValue data)
    {
        var classifier = (IClassifier)model.Target;

        return Apply(data, vector => classifier.Classify(vector), "ml.predict");
    }

    [ScriptFn("predict_proba", "Вероятности принадлежности классам", Example = "model.predict_proba(x)")]
    [ScriptMethod(ClassifierHandle)]
    public static ScriptValue PredictProbabilities(
        [ScriptParam("обученная модель")] ScriptHandle model,
        [ScriptParam("вектор-объект либо матрица объект × признак")] ScriptValue data)
    {
        var classifier = (IClassifier)model.Target;

        if (data.Type == ScriptType.Vec) return ScriptValue.Vec(classifier.ClassifyProbVector(data.AsVector()));

        Matrix matrix = AsMatrix(data, "ml.predict_proba");
        var rows = new List<Vector>(matrix.Height);

        foreach (Vector row in Datasets.Rows(matrix)) rows.Add(classifier.ClassifyProbVector(row));

        return ScriptValue.Mat(Datasets.FromRows(rows));
    }

    [ScriptFn("score", "Доля верных предсказаний классификатора", Example = "model.score(x, y)")]
    [ScriptMethod(ClassifierHandle)]
    public static double ClassifierScore(
        [ScriptParam("обученная модель")] ScriptHandle model,
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("истинные метки")] Vector labels)
    {
        Datasets.RequireSameLength(data, labels, "ml.score");

        var classifier = (IClassifier)model.Target;
        int[] expected = Datasets.Labels(labels, "ml.score");
        int correct = 0;

        Vector[] rows = Datasets.Rows(data);

        for (int i = 0; i < rows.Length; i++)
        {
            if (classifier.Classify(rows[i]) == expected[i]) correct++;
        }

        return rows.Length == 0 ? 0 : (double)correct / rows.Length;
    }

    // --- регрессия ---

    [ScriptFn("linreg", "Линейная регрессия одной переменной", Returns = RegressionHandle,
        Example = "let m = ml.linreg(x, y)\nemit slope = m.coefficients()[0]")]
    public static ScriptHandle LinearRegressionModel(
        [ScriptParam("значения независимой переменной")] Vector x,
        [ScriptParam("значения отклика")] Vector y)
    {
        RequireSameLength(x, y, "ml.linreg");

        var model = new LinearRegression();
        model.Fit(x, y);

        return new ScriptHandle(RegressionHandle, model,
            $"линейная: y = {ScriptFormatter.Number(model.Lrm.Slope)}·x + {ScriptFormatter.Number(model.Lrm.Intercept)}");
    }

    [ScriptFn("multireg", "Множественная линейная регрессия", Returns = RegressionHandle,
        Example = "ml.multireg(x, y)")]
    public static ScriptHandle MultipleRegressionModel(
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("значения отклика")] Vector y,
        [ScriptParam("нормировать признаки перед обучением")] bool scale = false)
    {
        Datasets.RequireSameLength(data, y, "ml.multireg");

        var model = new MultipleRegression(scale);
        model.Train(Datasets.Rows(data), y);

        return new ScriptHandle(RegressionHandle, model, $"множественная, признаков: {data.Width}");
    }

    [ScriptFn("polyreg", "Полиномиальная регрессия одной переменной", Returns = RegressionHandle,
        Example = "ml.polyreg(x, y, degree: 3)")]
    public static ScriptHandle PolynomialRegressionModel(
        [ScriptParam("значения независимой переменной")] Vector x,
        [ScriptParam("значения отклика")] Vector y,
        [ScriptParam("степень полинома")] int degree = 3)
    {
        RequireSameLength(x, y, "ml.polyreg");

        var model = new PolynomialRegression(x, y, degree);

        return new ScriptHandle(RegressionHandle, model, $"полиномиальная, степень {degree}");
    }

    [ScriptFn("predict", "Предсказанные значения отклика", Example = "model.predict(x)")]
    [ScriptMethod(RegressionHandle)]
    public static ScriptValue RegressionPredict(
        [ScriptParam("обученная модель")] ScriptHandle model,
        [ScriptParam("число, вектор либо матрица — смотря по модели")] ScriptValue data)
        => model.Target switch
        {
            PolynomialRegression polynomial => OneDimensional(data, polynomial.Predict, "ml.predict"),
            LinearRegression linear => OneDimensional(data, linear.Predict, "ml.predict"),
            IRegression regression => Apply(data, regression.Predict, "ml.predict"),
            _ => throw new ScriptError(DiagnosticCodes.FunctionFailed, "ml.predict: неизвестный вид модели"),
        };

    [ScriptFn("score", "Коэффициент детерминации R²", Example = "model.score(x, y)")]
    [ScriptMethod(RegressionHandle)]
    public static double RegressionScore(
        [ScriptParam("обученная модель")] ScriptHandle model,
        [ScriptParam("признаки: число, вектор либо матрица")] ScriptValue data,
        [ScriptParam("истинные значения отклика")] Vector y)
    {
        ScriptValue predicted = RegressionPredict(model, data);
        Vector prediction = predicted.Type == ScriptType.Num
            ? new Vector(predicted.RawNumber)
            : predicted.AsVector("предсказание");

        return StatModule.R2(y, prediction);
    }

    // --- снижение размерности ---

    [ScriptFn("pca", "Метод главных компонент", Returns = PcaHandle,
        Example = "let p = ml.pca(x, k: 2)\nlet z = p.transform(x)")]
    public static ScriptHandle Pca(
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("сколько компонент оставить; 0 — выбрать автоматически")] int k = 0)
    {
        _ = Datasets.RequireNotEmpty(data, "ml.pca");

        var model = new PCA(k <= 0 ? null : k);
        _ = model.Train(data);

        return new ScriptHandle(PcaHandle, model, k <= 0 ? "PCA, число компонент выбрано автоматически" : $"PCA, компонент: {k}");
    }

    [ScriptFn("transform", "Проекция данных на главные компоненты", Example = "p.transform(x)")]
    [ScriptMethod(PcaHandle)]
    public static Matrix PcaTransform(
        [ScriptParam("обученная модель")] ScriptHandle model,
        [ScriptParam("матрица объект × признак")] Matrix data)
        => ((PCA)model.Target).Transform(data);

    [ScriptFn("eigenvalues", "Собственные числа компонент", Example = "p.eigenvalues()")]
    [ScriptMethod(PcaHandle)]
    public static Vector Eigenvalues([ScriptParam("обученная модель")] ScriptHandle model) =>
        ((PCA)model.Target).Eigenvalues;

    // --- подготовка выборки ---

    /// <summary>
    /// Делит выборку на обучающую и тестовую части.
    /// </summary>
    /// <remarks>
    /// Перемешивание идёт от ГСЧ прогона, засеянного <c>options.seed</c>: разбиение обязано
    /// повторяться от запуска к запуску, иначе сравнивать метрики двух прогонов бессмысленно.
    /// </remarks>
    [ScriptFn("split", "Делит выборку на обучающую и тестовую части",
        Example = "let s = ml.split(x, y, test: 0.25)\nlet m = ml.knn(s.x_train, s.y_train)")]
    public static ScriptRecord Split(
        IScriptContext context,
        [ScriptParam("матрица объект × признак")] Matrix data,
        [ScriptParam("метки либо отклик")] Vector labels,
        [ScriptParam("доля тестовой части от 0 до 1")] double test = 0.25,
        [ScriptParam("перемешивать перед разбиением")] bool shuffle = true)
    {
        Datasets.RequireSameLength(data, labels, "ml.split");

        if (test is < 0 or > 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "ml.split: доля должна лежать в [0, 1]");

        int count = data.Height;
        int[] order = shuffle ? Datasets.Shuffled(context.Random, count) : Sequence(count);
        int testCount = (int)Math.Round(count * test);

        Vector[] rows = Datasets.Rows(data);

        var trainRows = new List<Vector>(count - testCount);
        var testRows = new List<Vector>(testCount);
        var trainLabels = new Vector(count - testCount);
        var testLabels = new Vector(testCount);

        for (int i = 0; i < count; i++)
        {
            int source = order[i];

            if (i < testCount)
            {
                testRows.Add(rows[source]);
                testLabels[i] = labels[source];
                continue;
            }

            trainRows.Add(rows[source]);
            trainLabels[i - testCount] = labels[source];
        }

        return ScriptRecord.From(
        [
            new KeyValuePair<string, ScriptValue>("x_train", ScriptValue.Mat(Datasets.FromRows(trainRows))),
            new KeyValuePair<string, ScriptValue>("y_train", ScriptValue.Vec(trainLabels)),
            new KeyValuePair<string, ScriptValue>("x_test", ScriptValue.Mat(Datasets.FromRows(testRows))),
            new KeyValuePair<string, ScriptValue>("y_test", ScriptValue.Vec(testLabels)),
        ]);
    }

    // --- вспомогательное ---

    private static ScriptValue Apply(ScriptValue data, Func<Vector, double> predict, string what)
    {
        if (data.Type == ScriptType.Vec) return ScriptValue.Num(predict(data.AsVector()));

        Matrix matrix = AsMatrix(data, what);
        var result = new Vector(matrix.Height);
        Vector[] rows = Datasets.Rows(matrix);

        for (int i = 0; i < rows.Length; i++) result[i] = predict(rows[i]);

        return ScriptValue.Vec(result);
    }

    private static ScriptValue OneDimensional(ScriptValue data, Func<double, double> predict, string what)
    {
        if (data.Type == ScriptType.Num) return ScriptValue.Num(predict(data.RawNumber));

        if (data.Type is ScriptType.Vec or ScriptType.List or ScriptType.Range)
        {
            var input = (Vector)Marshaller.ToClr(data, typeof(Vector), what)!;
            var result = new Vector(input.Count);

            for (int i = 0; i < input.Count; i++) result[i] = predict(input[i]);

            return ScriptValue.Vec(result);
        }

        throw new ScriptError(
            DiagnosticCodes.TypeMismatch,
            $"{what}: модель одномерная, ожидалось число либо вектор, получено {data.Type.ToName()}");
    }

    private static Matrix AsMatrix(ScriptValue data, string what) =>
        (Matrix)Marshaller.ToClr(data, typeof(Matrix), what)!;

    private static void Prepare(Matrix data, Vector labels, string what, out Vector[] rows, out int[] classes)
    {
        _ = Datasets.RequireNotEmpty(data, what);
        Datasets.RequireSameLength(data, labels, what);

        rows = Datasets.Rows(data);
        classes = Datasets.Labels(labels, what);
    }

    private static void RequireBinary(int[] classes, string what)
    {
        var distinct = new HashSet<int>(classes);

        if (distinct.Count <= 2) return;

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"{what}: реализация двухклассовая, а в метках {distinct.Count} классов",
            "для нескольких классов подойдут ml.knn, ml.bayes либо ml.nearest");
    }

    private static void RequireClusters(int k, int objects, string what)
    {
        if (k >= 1 && k <= objects) return;

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"{what}: кластеров {k} при {objects} объектах",
            "число кластеров должно быть от 1 до числа объектов");
    }

    private static void RequireSameLength(Vector x, Vector y, string what)
    {
        if (x.Count == y.Count) return;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"{what}: {x.Count} значений и {y.Count} откликов");
    }

    private static int Distinct(int[] classes) => new HashSet<int>(classes).Count;

    private static int[] Sequence(int count)
    {
        var order = new int[count];

        for (int i = 0; i < count; i++) order[i] = i;

        return order;
    }
}
