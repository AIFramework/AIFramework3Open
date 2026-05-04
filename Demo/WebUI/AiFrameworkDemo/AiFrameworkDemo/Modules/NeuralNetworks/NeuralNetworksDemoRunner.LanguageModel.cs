using AI.Charts;
using AI.Charts.JS;
using AI.DataPrepaire.Tokenizers.TextTokenizers;
using AI.DataStructs.Algebraic;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.V2.Optim;
using AiFrameworkDemo.Core;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using V2T = AI.ML.NeuralNetworks.V2.Tensor;
using V2S = AI.ML.NeuralNetworks.V2.Shape;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.NeuralNetworks;

public static partial class NeuralNetworksDemoRunner
{
    #region LSTM Language Model

    private static readonly string DefaultLmCorpus =
        "Нейронные сети являются основой современного глубокого обучения. " +
        "Машинное обучение позволяет компьютерам учиться на данных. " +
        "Алгоритмы обработки естественного языка анализируют текст автоматически. " +
        "Глубокие нейронные сети обучаются на больших наборах данных. " +
        "Трансформеры совершили революцию в обработке текста. " +
        "Векторные представления слов кодируют семантическую близость. " +
        "Модели машинного обучения требуют качественной подготовки данных. " +
        "Регуляризация помогает избежать переобучения нейронных сетей. " +
        "Рекуррентные сети обрабатывают последовательности данных. " +
        "Сверточные сети извлекают пространственные признаки из изображений. " +
        "Автоэнкодеры обучаются сжатому представлению входных данных. " +
        "Генеративные модели создают новые данные по обученному распределению. " +
        "Обучение с подкреплением позволяет агентам принимать решения. " +
        "Функции активации вносят нелинейность в нейронные сети. " +
        "Оптимизатор Adam адаптирует скорость обучения для каждого параметра. " +
        "Батч-нормализация ускоряет сходимость обучения нейронных сетей.";

    private static void RunLanguageModelCase(
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp,
        ChartView cv, ref string? textOut)
    {
        double N(string k, double def = 0) => p.TryGetValue(k, out var v) ? v : def;

        int epochs    = Math.Clamp((int)N("epochs", 15), 3, 50);
        int hidden    = Math.Clamp((int)N("hiddenSize", 32), 16, 64);
        int embDim    = Math.Clamp((int)N("embDim", 16), 8, 32);
        int maxTokens = Math.Clamp((int)N("maxTokens", 20), 3, 50);

        string corpus = T(tp, "_corpus");
        if (string.IsNullOrWhiteSpace(corpus))
            corpus = DefaultLmCorpus;
        string prompt = T(tp, "_prompt", "нейронные");

        var tokenizer = new WordTokenizer();
        tokenizer.TrainFromText(corpus);
        int vocab = tokenizer.DictLen;

        var sentences = corpus.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var sequences = new List<int[]>();
        foreach (string raw in sentences)
        {
            string s = raw.Trim();
            if (s.Length == 0) continue;
            int[] ids = tokenizer.Encode($"<s> {s} <e>");
            if (ids.Length >= 2) sequences.Add(ids);
        }

        var rng = new Random(42);
        var model = new LmModel(vocab, embDim, hidden, rng);

        long paramCount = 0;
        foreach (var (_, param) in model.NamedParameters())
            paramCount += param.Tensor.Shape.NumElements;

        var optim = new Adam(model.Parameters(), lr: 5e-3f);
        var lossHistory = new List<float>();

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            float epochLoss = 0;
            int steps = 0;
            foreach (int[] seq in sequences)
            {
                int T = seq.Length - 1;
                if (T <= 0) continue;
                int[] x = new int[T], y = new int[T];
                for (int t = 0; t < T; t++) { x[t] = seq[t]; y[t] = seq[t + 1]; }

                var xT = V2T.From(x, new V2S(1, T));
                var yT = V2T.From(y, new V2S(T));

                optim.ZeroGrad();
                var logits = model.ForwardLogits(xT);
                var loss = ClassificationLosses.CrossEntropy(logits, yT);
                loss.Backward();
                optim.Step();

                epochLoss += loss.AsReadOnlySpan<float>()[0];
                steps++;
            }
            lossHistory.Add(steps > 0 ? epochLoss / steps : 0);
        }

        var generatedIds = new List<int>(tokenizer.Encode("<s> " + prompt));
        double cumProb = 1.0;
        V2T? h = null, c = null;
        using (TapeContext.NoGrad())
        {
            for (int i = 0; i < generatedIds.Count; i++)
                (h, c, _) = model.StepLogits(generatedIds[i], h, c);

            int last = generatedIds[^1];
            for (int step = 0; step < maxTokens; step++)
            {
                V2T logits;
                (h, c, logits) = model.StepLogits(last, h, c);
                int next = LmArgMax(logits, out float prob);
                cumProb *= prob;
                if (next == tokenizer.EndToken) break;
                generatedIds.Add(next);
                last = next;
            }
        }

        var generated = generatedIds.Skip(1).Where(t => t != tokenizer.EndToken).ToArray();
        string generatedText = tokenizer.DecodeObj(generated);

        if (lossHistory.Count > 0)
        {
            var xEpoch = new Vector(lossHistory.Count);
            var yLoss  = new Vector(lossHistory.Count);
            for (int i = 0; i < lossHistory.Count; i++) { xEpoch[i] = i + 1; yLoss[i] = lossHistory[i]; }
            cv.ChartName = "LSTM LM — кривая обучения (CrossEntropy)";
            cv.LabelX = "Эпоха"; cv.LabelY = "Loss";
            cv.AddPlot(xEpoch, yLoss, "Avg Loss", Palette[0], width: 2);
        }

        var sb = new StringBuilder();
        sb.AppendLine("> LSTM языковая модель — генерация текста");
        sb.AppendLine();
        sb.AppendLine($"  Архитектура:   Embedding({vocab},{embDim}) -> LSTMCell({embDim},{hidden}) -> Linear({hidden},{vocab})");
        sb.AppendLine($"  Параметров:    {paramCount:N0}");
        sb.AppendLine($"  Словарь:       {vocab} слов");
        sb.AppendLine($"  Корпус:        {sequences.Count} предложений, {corpus.Length} символов");
        sb.AppendLine($"  Эпох:          {epochs}");
        sb.AppendLine($"  Финальный loss: {(lossHistory.Count > 0 ? lossHistory[^1] : 0):F4}");
        sb.AppendLine();
        sb.AppendLine("- Генерация");
        sb.AppendLine($"  Промпт:     «{prompt}»");
        sb.AppendLine($"  Результат:  «{generatedText}»");
        sb.AppendLine($"  P(текст):    {cumProb:E2}");

        textOut = sb.ToString();
    }

    private static int LmArgMax(V2T logits1d, out float prob)
    {
        var span = logits1d.Reshape(-1).AsReadOnlySpan<float>();
        float max = float.NegativeInfinity;
        int idx = 0;
        for (int i = 0; i < span.Length; i++)
            if (span[i] > max) { max = span[i]; idx = i; }
        double sumExp = 0;
        for (int i = 0; i < span.Length; i++) sumExp += Math.Exp(span[i] - max);
        prob = (float)(1.0 / sumExp);
        return idx;
    }

    /// <summary>Embedding(vocab, d) -> LSTMCell(d, h) -> Linear(h, vocab).</summary>
    private sealed class LmModel : Module
    {
        private readonly Embedding _emb;
        private readonly LSTMCell  _cell;
        private readonly Linear    _head;
        private readonly int       _vocab;
        private readonly int       _hidden;

        public LmModel(int vocab, int embeddingDim, int hiddenSize, Random rng)
        {
            _vocab  = vocab;
            _hidden = hiddenSize;
            _emb  = RegisterModule("emb",  new Embedding(vocab, embeddingDim, rng: rng));
            _cell = RegisterModule("cell", new LSTMCell(embeddingDim, hiddenSize, rng: rng));
            _head = RegisterModule("head", new Linear(hiddenSize, vocab, rng: rng));
        }

        public V2T ForwardLogits(V2T xIds)
        {
            int B = xIds.Shape[0], T = xIds.Shape[1];
            var emb = _emb.Forward(xIds);
            var h = V2T.Zeros(new V2S(B, _hidden));
            var c = V2T.Zeros(new V2S(B, _hidden));
            var logitsList = new List<V2T>(T);

            for (int t = 0; t < T; t++)
            {
                var xt = IndexingOps.Narrow(emb, 1, t, 1).Squeeze(1);
                (h, c) = _cell.Step(xt, h, c);
                logitsList.Add(_head.Forward(h));
            }
            return IndexingOps.Stack(logitsList, 1).Reshape(B * T, _vocab);
        }

        public (V2T h, V2T c, V2T logits) StepLogits(int tokenId, V2T? h, V2T? c)
        {
            var xIdx = V2T.From(new[] { tokenId }, new V2S(1, 1));
            var emb = _emb.Forward(xIdx);
            var xt = IndexingOps.Narrow(emb, 1, 0, 1).Squeeze(1);
            h ??= V2T.Zeros(new V2S(1, _hidden));
            c ??= V2T.Zeros(new V2S(1, _hidden));
            (h, c) = _cell.Step(xt, h, c);
            return (h, c, _head.Forward(h));
        }

        public override V2T Forward(V2T input) => ForwardLogits(input);
    }

    #endregion
}
