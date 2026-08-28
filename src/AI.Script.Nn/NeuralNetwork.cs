using AI.DataStructs.Algebraic;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;
using AI.Script.Runtime;
using AI.Script.Semantics;

// Тензор здесь — тензор нейросетевой библиотеки: одноимённый тип из AI.DataStructs к сети
// отношения не имеет, и молчаливое разрешение неоднозначности компилятором было бы хуже,
// чем явная строка.
using Tensor = AI.ML.NeuralNetworks.V2.Tensor;

namespace AI.Script.Nn;

/// <summary>Чему учится сеть.</summary>
public enum NetworkTask
{
    /// <summary>Классификация: выход — вероятности классов.</summary>
    Classification,

    /// <summary>Регрессия: выход — одно число.</summary>
    Regression,
}

/// <summary>
/// Обученная сеть прямого распространения.
/// </summary>
/// <remarks>
/// Тонкая оболочка над <c>AI.NeuralNetworks</c>, знающая ровно то, что нужно скрипту: как
/// превратить матрицу признаков в предсказание и насколько хорошо это выходит. Архитектура
/// задаётся списком размеров скрытых слоёв, а не конструированием модулей: язык описывает
/// конвейеры, и авторство архитектур в нём было бы вторым языком внутри первого.
/// <para>
/// Нормировка входа не делается: её место в <c>prep</c>, где параметры запоминаются и
/// применяются к тесту. Скрытая нормировка внутри сети рассогласовала бы обучение с
/// применением ровно тогда, когда об этом забыли.
/// </para>
/// </remarks>
public sealed class NeuralNetwork
{
    private readonly Sequential _model;
    private readonly int _classes;

    private NeuralNetwork(Sequential model, NetworkTask task, int inputs, int classes, Vector history)
    {
        _model = model;
        _classes = classes;

        Task = task;
        Inputs = inputs;
        History = history;
    }

    /// <summary>Чему обучена сеть.</summary>
    public NetworkTask Task { get; }

    /// <summary>Сколько признаков ожидает вход.</summary>
    public int Inputs { get; }

    /// <summary>Сколько классов различает; для регрессии — единица.</summary>
    public int Classes => _classes;

    /// <summary>Значение функции потерь по эпохам.</summary>
    public Vector History { get; }

    /// <summary>Размеры скрытых слоёв.</summary>
    public IReadOnlyList<int> Hidden { get; private init; } = [];

    /// <summary>Имя функции активации.</summary>
    public string Activation { get; private init; } = "relu";

    /// <summary>Сколько параметров обучено.</summary>
    public long ParameterCount
    {
        get
        {
            long total = 0;

            foreach (Parameter parameter in _model.Parameters()) total += parameter.Tensor.NumElements;

            return total;
        }
    }

    /// <summary>
    /// Обучает сеть на матрице признаков.
    /// </summary>
    /// <param name="x">Матрица «объект × признак».</param>
    /// <param name="y">Метки классов либо отклик.</param>
    /// <param name="hidden">Размеры скрытых слоёв.</param>
    /// <param name="task">Чему учить.</param>
    /// <param name="activation">Имя активации: <c>relu</c>, <c>tanh</c>, <c>sigmoid</c>, <c>gelu</c>.</param>
    /// <param name="epochs">Сколько раз пройти обучающую выборку.</param>
    /// <param name="learningRate">Скорость обучения.</param>
    /// <param name="batch">Размер пачки; ноль — вся выборка целиком.</param>
    /// <param name="dropout">Доля отключаемых нейронов; ноль — без отключения.</param>
    /// <param name="random">ГСЧ прогона: от него зависит и начальная инициализация, и перемешивание.</param>
    public static NeuralNetwork Fit(
        Matrix x,
        Vector y,
        IReadOnlyList<int> hidden,
        NetworkTask task,
        string activation,
        int epochs,
        double learningRate,
        int batch,
        double dropout,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        int samples = x.Height;
        int inputs = x.Width;
        int outputs = task == NetworkTask.Regression ? 1 : ClassCount(y);

        Sequential model = Build(inputs, hidden, outputs, activation, dropout, random);
        var optimizer = new Adam(model.Parameters(), (float)learningRate);

        Tensor features = Features(x);
        Tensor targets = task == NetworkTask.Regression ? Response(y) : Labels(y, outputs);

        int size = batch > 0 ? Math.Min(batch, samples) : samples;
        var history = new Vector(epochs);
        int[] order = Order(samples);

        _ = model.Train();

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            Shuffle(order, random);

            double total = 0;
            int batches = 0;

            for (int start = 0; start < samples; start += size)
            {
                int count = Math.Min(size, samples - start);

                Tensor batchX = Rows(features, order, start, count, inputs);
                Tensor batchY = task == NetworkTask.Regression
                    ? Rows(targets, order, start, count, 1)
                    : LabelRows(targets, order, start, count);

                optimizer.ZeroGrad();

                Tensor output = model.Forward(batchX);
                Tensor loss = task == NetworkTask.Regression
                    ? RegressionLosses.MSE(output, batchY)
                    : ClassificationLosses.CrossEntropy(output, batchY);

                loss.Backward();
                optimizer.Step();

                total += loss.GetFloat();
                batches++;
            }

            history[epoch] = batches > 0 ? total / batches : 0;
        }

        _ = model.Eval();

        return new NeuralNetwork(model, task, inputs, outputs, history)
        {
            Hidden = [.. hidden],
            Activation = activation,
        };
    }

    /// <summary>Предсказание: метка класса либо значение отклика на каждый объект.</summary>
    public Vector Predict(Matrix x)
    {
        RequireInputs(x, "nn.predict");

        Tensor output = _model.Forward(Features(x));
        var result = new Vector(x.Height);

        if (Task == NetworkTask.Regression)
        {
            for (int i = 0; i < x.Height; i++) result[i] = output.GetFloat(i, 0);

            return result;
        }

        for (int i = 0; i < x.Height; i++) result[i] = ArgMax(output, i);

        return result;
    }

    /// <summary>Вероятности классов: строка на объект, столбец на класс.</summary>
    public Matrix Probabilities(Matrix x)
    {
        if (Task == NetworkTask.Regression)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                "nn.proba: сеть обучена регрессии, у неё нет классов",
                "используйте predict либо обучите сеть с task: \"classification\"");
        }

        RequireInputs(x, "nn.proba");

        Tensor output = _model.Forward(Features(x));
        var result = new Matrix(x.Height, _classes);

        for (int i = 0; i < x.Height; i++)
        {
            // Мягкий максимум считается здесь, а не слоем сети: выход обучается логитами,
            // и добавление слоя после обучения изменило бы саму модель.
            double max = double.NegativeInfinity;

            for (int c = 0; c < _classes; c++) max = Math.Max(max, output.GetFloat(i, c));

            double sum = 0;

            for (int c = 0; c < _classes; c++)
            {
                double value = Math.Exp(output.GetFloat(i, c) - max);

                result[i, c] = value;
                sum += value;
            }

            for (int c = 0; c < _classes; c++) result[i, c] /= sum;
        }

        return result;
    }

    /// <summary>
    /// Качество на выборке: доля верных для классификации, коэффициент детерминации для регрессии.
    /// </summary>
    /// <remarks>
    /// Две разные меры под одним именем — потому что вопрос один и тот же: «насколько хорошо».
    /// Обе растут к единице, и обе сравнимы между прогонами.
    /// </remarks>
    public double Score(Matrix x, Vector y)
    {
        if (x.Height != y.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"nn.score: объектов {x.Height}, а меток {y.Count}");
        }

        Vector predicted = Predict(x);

        if (Task == NetworkTask.Classification)
        {
            int correct = 0;

            for (int i = 0; i < y.Count; i++)
            {
                if (Math.Abs(predicted[i] - y[i]) < 0.5) correct++;
            }

            return (double)correct / y.Count;
        }

        double mean = 0;

        for (int i = 0; i < y.Count; i++) mean += y[i];

        mean /= y.Count;

        double residual = 0;
        double variance = 0;

        for (int i = 0; i < y.Count; i++)
        {
            residual += (y[i] - predicted[i]) * (y[i] - predicted[i]);
            variance += (y[i] - mean) * (y[i] - mean);
        }

        return variance == 0 ? 0 : 1 - (residual / variance);
    }

    // --- внутреннее ---

    private static Sequential Build(
        int inputs,
        IReadOnlyList<int> hidden,
        int outputs,
        string activation,
        double dropout,
        Random random)
    {
        var model = new Sequential();
        int previous = inputs;

        foreach (int size in hidden)
        {
            if (size < 1)
                throw new ScriptError(DiagnosticCodes.BadOperand, "nn.fit: размер скрытого слоя должен быть больше нуля");

            _ = model.Add(new Linear(previous, size, bias: true, random));
            _ = model.Add(ActivationOf(activation));

            if (dropout > 0) _ = model.Add(new Dropout((float)dropout, random));

            previous = size;
        }

        _ = model.Add(new Linear(previous, outputs, bias: true, random));

        return model;
    }

    private static Module ActivationOf(string name) => name switch
    {
        "relu" => new ReLU(),
        "tanh" => new Tanh(),
        "sigmoid" => new Sigmoid(),
        "gelu" => new GELU(),
        "silu" => new SiLU(),
        _ => throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"nn.fit: неизвестная активация '{name}'",
            "известны: \"relu\", \"tanh\", \"sigmoid\", \"gelu\", \"silu\""),
    };

    private static int ClassCount(Vector y)
    {
        double max = 0;

        for (int i = 0; i < y.Count; i++)
        {
            if (y[i] < 0 || y[i] != Math.Floor(y[i]))
            {
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    "nn.fit: метки классов — целые числа от нуля",
                    "для вещественного отклика укажите task: \"regression\"");
            }

            max = Math.Max(max, y[i]);
        }

        return (int)max + 1;
    }

    private static Tensor Features(Matrix x)
    {
        var data = new float[x.Height * x.Width];

        for (int i = 0; i < x.Height; i++)
        {
            for (int j = 0; j < x.Width; j++) data[(i * x.Width) + j] = (float)x[i, j];
        }

        return Tensor.From(data, new Shape(x.Height, x.Width));
    }

    private static Tensor Response(Vector y)
    {
        var data = new float[y.Count];

        for (int i = 0; i < y.Count; i++) data[i] = (float)y[i];

        return Tensor.From(data, new Shape(y.Count, 1));
    }

    private static Tensor Labels(Vector y, int classes)
    {
        var data = new int[y.Count];

        for (int i = 0; i < y.Count; i++)
        {
            int label = (int)y[i];

            if (label >= classes)
                throw new ScriptError(DiagnosticCodes.BadOperand, $"nn.fit: метка {label} вне числа классов {classes}");

            data[i] = label;
        }

        return Tensor.From(data, new Shape(y.Count));
    }

    private static int[] Order(int count)
    {
        var order = new int[count];

        for (int i = 0; i < count; i++) order[i] = i;

        return order;
    }

    /// <summary>
    /// Перемешивание Фишера — Йетса ГСЧ прогона.
    /// </summary>
    /// <remarks>
    /// Через ГСЧ прогона, а не собственный: обучение обязано воспроизводиться при том же
    /// <c>options.seed</c>, иначе точность на одних и тех же данных гуляет от запуска к запуску.
    /// </remarks>
    private static void Shuffle(int[] order, Random random)
    {
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);

            (order[i], order[j]) = (order[j], order[i]);
        }
    }

    private static Tensor Rows(Tensor source, int[] order, int start, int count, int width)
    {
        var data = new float[count * width];

        for (int i = 0; i < count; i++)
        {
            int row = order[start + i];

            for (int j = 0; j < width; j++) data[(i * width) + j] = source.GetFloat(row, j);
        }

        return Tensor.From(data, new Shape(count, width));
    }

    private static Tensor LabelRows(Tensor source, int[] order, int start, int count)
    {
        var data = new int[count];

        for (int i = 0; i < count; i++) data[i] = source.Get<int>(order[start + i]);

        return Tensor.From(data, new Shape(count));
    }

    private static int ArgMax(Tensor output, int row)
    {
        int best = 0;
        double value = double.NegativeInfinity;

        for (int c = 0; c < output.Shape[1]; c++)
        {
            double current = output.GetFloat(row, c);

            if (current <= value) continue;

            value = current;
            best = c;
        }

        return best;
    }

    private void RequireInputs(Matrix x, string what)
    {
        if (x.Width == Inputs) return;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"{what}: сеть обучена на {Inputs} признаках, а подано {x.Width}");
    }
}
