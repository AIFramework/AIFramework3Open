using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Nn;

/// <summary>
/// Пространство <c>nn</c>: сети прямого распространения над матрицей признаков.
/// </summary>
/// <remarks>
/// Сеть обучается при создании и возвращает дескриптор с методами — так же, как <c>ml.knn</c>
/// и остальные модели языка. Архитектура задаётся размерами скрытых слоёв, а не сборкой из
/// модулей: язык описывает конвейеры обработки данных, и авторство архитектур в нём стало бы
/// вторым языком внутри первого — с худшим синтаксисом, чем у C#, и без отладчика.
/// <para>
/// Свёрточные сети сюда не вошли по той же причине, что и рекуррентные: им нужна форма данных,
/// которую матрица «объект × признак» не несёт. Признаки изображения даёт <c>cv.hog</c>, и
/// дальше работает та же сеть.
/// </para>
/// </remarks>
[ScriptModule("nn", "Сеть прямого распространения над матрицей признаков; вход нормируйте prep", Version = "0.1")]
public static class NnModule
{
    /// <summary>Тип-тег дескриптора обученной сети.</summary>
    public const string ModelHandle = "nn.model";

    /// <summary>
    /// Обучает сеть на матрице признаков.
    /// </summary>
    /// <remarks>
    /// Признаки полагается нормировать заранее — <c>prep.zscore</c>: сеть с ненормированным
    /// входом учится в разы дольше, а на признаках, различающихся на порядки, не учится вовсе.
    /// Внутрь эта нормировка не спрятана намеренно: параметры нормировки нужны и на тесте,
    /// а спрятанные они туда не попадут.
    /// </remarks>
    [ScriptFn("fit", "Обучает сеть прямого распространения", Returns = ModelHandle,
        Example = "let net = nn.fit(x_train, y_train, hidden: [64, 32], epochs: 100)")]
    public static ScriptHandle Fit(
        IScriptContext context,
        [ScriptParam("матрица объект × признак")] Matrix x,
        [ScriptParam("метки классов либо отклик")] Vector y,
        [ScriptParam("размеры скрытых слоёв")] ScriptList? hidden = null,
        [ScriptParam("задача: \"classification\" либо \"regression\"")] string task = "classification",
        [ScriptParam("активация: relu, tanh, sigmoid, gelu, silu")] string activation = "relu",
        [ScriptParam("сколько раз пройти выборку")] int epochs = 50,
        [ScriptParam("скорость обучения")] double lr = 0.01,
        [ScriptParam("размер пачки; 0 — вся выборка")] int batch = 32,
        [ScriptParam("доля отключаемых нейронов")] double dropout = 0)
    {
        if (x.Height != y.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"nn.fit: объектов {x.Height}, а меток {y.Count}",
                "число строк матрицы признаков обязано совпадать с длиной вектора меток");
        }

        if (x.Height < 2)
            throw new ScriptError(DiagnosticCodes.BadOperand, "nn.fit: нужно хотя бы два объекта");

        if (epochs < 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "nn.fit: эпох должно быть хотя бы одна");

        if (lr <= 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "nn.fit: скорость обучения должна быть положительной");

        if (dropout is < 0 or >= 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "nn.fit: доля отключаемых нейронов лежит в [0, 1)");

        NetworkTask kind = task switch
        {
            "classification" => NetworkTask.Classification,
            "regression" => NetworkTask.Regression,
            _ => throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"nn.fit: неизвестная задача '{task}'",
                "известны: \"classification\" — метки классов, \"regression\" — вещественный отклик"),
        };

        int[] sizes = Sizes(hidden);

        // Обучение — самая дорогая операция языка: учитываем её объём заранее, чтобы потолок
        // памяти сработал до того, как сеть съест всё.
        context.CountAllocation(Weights(x.Width, sizes) + ((long)x.Height * x.Width));

        NeuralNetwork network = NeuralNetwork.Fit(
            x, y, sizes, kind, activation, epochs, lr, batch, dropout, context.Random);

        string summary = kind == NetworkTask.Regression
            ? $"регрессия, слоёв: {sizes.Length + 1}, параметров: {network.ParameterCount}"
            : $"классов: {network.Classes}, слоёв: {sizes.Length + 1}, параметров: {network.ParameterCount}";

        return new ScriptHandle(ModelHandle, network, summary);
    }

    [ScriptFn("predict", "Предсказание сети: метка класса либо значение отклика",
        Example = "net.predict(x_test)")]
    [ScriptMethod(ModelHandle)]
    public static Vector Predict(
        IScriptContext context,
        [ScriptParam("обученная сеть")] ScriptHandle model,
        [ScriptParam("матрица объект × признак")] Matrix x)
    {
        context.CountAllocation(x.Height);

        return Unwrap(model).Predict(x);
    }

    [ScriptFn("proba", "Вероятности классов: строка на объект, столбец на класс",
        Example = "net.proba(x_test)")]
    [ScriptMethod(ModelHandle)]
    public static Matrix Probabilities(
        IScriptContext context,
        [ScriptParam("обученная сеть")] ScriptHandle model,
        [ScriptParam("матрица объект × признак")] Matrix x)
    {
        NeuralNetwork network = Unwrap(model);

        context.CountAllocation((long)x.Height * network.Classes);

        return network.Probabilities(x);
    }

    [ScriptFn("score", "Качество: доля верных либо коэффициент детерминации",
        Example = "net.score(x_test, y_test)")]
    [ScriptMethod(ModelHandle)]
    public static double Score(
        [ScriptParam("обученная сеть")] ScriptHandle model,
        [ScriptParam("матрица объект × признак")] Matrix x,
        [ScriptParam("истинные метки либо отклик")] Vector y) => Unwrap(model).Score(x, y);

    /// <summary>
    /// Значение функции потерь по эпохам.
    /// </summary>
    /// <remarks>
    /// То, по чему видно, обучилась сеть или только сделала вид: кривая, вышедшая на полку с
    /// первых эпох, означает слишком малую скорость обучения либо ненормированный вход, и
    /// точность на тесте об этом не скажет.
    /// </remarks>
    [ScriptFn("history", "Функция потерь по эпохам", Example = "show plot.line(net.history())")]
    [ScriptMethod(ModelHandle)]
    public static Vector History([ScriptParam("обученная сеть")] ScriptHandle model) => Unwrap(model).History;

    [ScriptFn("describe", "Устройство сети: слои, классы, число параметров", Example = "emit model = net.describe()")]
    [ScriptMethod(ModelHandle)]
    public static ScriptRecord Describe([ScriptParam("обученная сеть")] ScriptHandle model)
    {
        NeuralNetwork network = Unwrap(model);
        var hidden = new ScriptValue[network.Hidden.Count];

        for (int i = 0; i < network.Hidden.Count; i++) hidden[i] = ScriptValue.Num(network.Hidden[i]);

        return ScriptRecord.From(
        [
            new("task", ScriptValue.Str(network.Task == NetworkTask.Regression ? "regression" : "classification")),
            new("inputs", ScriptValue.Num(network.Inputs)),
            new("classes", ScriptValue.Num(network.Classes)),
            new("hidden", ScriptValue.List(ScriptList.Own(hidden))),
            new("activation", ScriptValue.Str(network.Activation)),
            new("parameters", ScriptValue.Num(network.ParameterCount)),
            new("epochs", ScriptValue.Num(network.History.Count)),
            new("loss", ScriptValue.Num(network.History.Count > 0 ? network.History[^1] : double.NaN)),
        ]);
    }

    // --- внутреннее ---

    private static NeuralNetwork Unwrap(ScriptHandle handle) => (NeuralNetwork)handle.Target;

    private static int[] Sizes(ScriptList? hidden)
    {
        if (hidden == null || hidden.Count == 0) return [32];

        var sizes = new int[hidden.Count];

        for (int i = 0; i < hidden.Count; i++)
        {
            if (hidden[i].Type != ScriptType.Num)
            {
                throw new ScriptError(
                    DiagnosticCodes.TypeMismatch,
                    $"nn.fit: размер слоя {i} — {hidden[i].Type.ToName()}, а нужно число",
                    "скрытые слои задаются списком чисел: hidden: [64, 32]");
            }

            sizes[i] = (int)hidden[i].RawNumber;
        }

        return sizes;
    }

    /// <summary>Сколько весов будет у сети: оценка сверху, без учёта числа классов.</summary>
    private static long Weights(int inputs, IReadOnlyList<int> hidden)
    {
        long total = 0;
        long previous = inputs;

        foreach (int size in hidden)
        {
            total += (previous + 1) * size;
            previous = size;
        }

        return total + previous + 1;
    }
}
