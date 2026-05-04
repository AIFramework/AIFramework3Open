using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AI.DataPrepaire.Tokenizers.TextTokenizers;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.V2.Optim;

namespace ControllerTest;

/// <summary>
/// Демо: символьно/словная LSTM-языковая модель на V2 (PyTorch-style).
/// Бывшая `NeuralNetworkManager` + `EmbeddingLayer` + `ControllerLResNet`
/// заменена на `Embedding` -> `LSTM` -> `Linear`, обучение Adam + CrossEntropy.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        string corpusPath = Path.Combine(AppContext.BaseDirectory, "cat.txt");
        if (!File.Exists(corpusPath))
        {
            Console.Error.WriteLine($"Не найден файл корпуса: {corpusPath}");
            return;
        }

        var tokenizer = new WordTokenizer(corpusPath);
        Console.WriteLine($"Размер словаря: {tokenizer.DictLen}");

        string[] sentences = File.ReadAllText(corpusPath)
            .Split('.', StringSplitOptions.RemoveEmptyEntries);

        var sequences = new List<int[]>();
        foreach (string raw in sentences)
        {
            string s = raw.Trim();
            if (s.Length == 0) continue;
            int[] ids = tokenizer.Encode($"<s> {s} <e>");
            if (ids.Length >= 2) sequences.Add(ids);
        }
        Console.WriteLine($"Тренировочных последовательностей: {sequences.Count}");

        var rng = new Random(42);
        var model = new LMModel(tokenizer.DictLen, embeddingDim: 24, hiddenSize: 32, rng: rng);

        Console.WriteLine($"Параметров: {CountParams(model)}");
        Train(model, sequences, epochs: 200, lr: 5e-3f);

        Console.WriteLine("\n=== Сгенерированные продолжения ===");
        foreach (string prompt in new[] { "кот", "кот сидит", "кот играет" })
        {
            var (text, prob) = Generate(model, tokenizer, prompt);
            Console.WriteLine($"  «{prompt}» -> «{text}»   (P≈{prob:E2})");
        }
    }

    private static long CountParams(Module model)
    {
        long total = 0;
        foreach (var (_, p) in model.NamedParameters())
            total += p.Tensor.Shape.NumElements;
        return total;
    }

    private static void Train(LMModel model, List<int[]> sequences, int epochs, float lr)
    {
        var optim = new Adam(model.Parameters(), lr: lr);
        for (int epoch = 0; epoch < epochs; epoch++)
        {
            float epochLoss = 0;
            int steps = 0;
            foreach (int[] seq in sequences)
            {
                int T = seq.Length - 1;
                if (T <= 0) continue;

                int[] x = new int[T];
                int[] y = new int[T];
                for (int t = 0; t < T; t++) { x[t] = seq[t]; y[t] = seq[t + 1]; }

                Tensor xT = Tensor.From(x, new Shape(1, T));
                Tensor yT = Tensor.From(y, new Shape(T));

                optim.ZeroGrad();
                Tensor logits = model.ForwardLogits(xT);
                Tensor loss = ClassificationLosses.CrossEntropy(logits, yT);
                loss.Backward();
                optim.Step();

                epochLoss += loss.AsReadOnlySpan<float>()[0];
                steps++;
            }
            if ((epoch + 1) % 25 == 0 || epoch == 0)
                Console.WriteLine($"  epoch {epoch + 1,4}: avg loss = {epochLoss / Math.Max(1, steps):F4}");
        }
    }

    private static (string text, double prob) Generate(LMModel model, WordTokenizer tokenizer, string prompt, int maxTokens = 20)
    {
        var ids = new List<int>(tokenizer.Encode("<s> " + prompt));
        double cumProb = 1.0;

        Tensor h = null;
        Tensor c = null;
        using (TapeContext.NoGrad())
        {
            for (int i = 0; i < ids.Count; i++)
                (h, c, _) = model.StepLogits(ids[i], h, c);

            int last = ids[^1];
            for (int step = 0; step < maxTokens; step++)
            {
                Tensor logits;
                (h, c, logits) = model.StepLogits(last, h, c);
                int next = ArgMax(logits, out float p);
                cumProb *= p;
                if (next == tokenizer.EndToken) break;
                ids.Add(next);
                last = next;
            }
        }

        // Skip starting <s>.
        var generated = ids.Skip(1).Where(t => t != tokenizer.EndToken).ToArray();
        return (tokenizer.DecodeObj(generated), cumProb);
    }

    private static int ArgMax(Tensor logits1d, out float prob)
    {
        var span = logits1d.Reshape(-1).AsReadOnlySpan<float>();
        float max = float.NegativeInfinity;
        int idx = 0;
        for (int i = 0; i < span.Length; i++)
            if (span[i] > max) { max = span[i]; idx = i; }
        // softmax(max) — оценка вероятности.
        double sumExp = 0;
        for (int i = 0; i < span.Length; i++) sumExp += Math.Exp(span[i] - max);
        prob = (float)(1.0 / sumExp);
        return idx;
    }

    /// <summary>
    /// Embedding(vocab, d) -> LSTM(d, h) -> Linear(h, vocab).
    /// </summary>
    private sealed class LMModel : Module
    {
        public Embedding Emb { get; }
        public LSTMCell Cell { get; }
        public Linear Head { get; }
        public int Vocab { get; }
        public int Hidden { get; }

        public LMModel(int vocab, int embeddingDim, int hiddenSize, Random rng)
        {
            Vocab = vocab;
            Hidden = hiddenSize;
            Emb = RegisterModule("emb", new Embedding(vocab, embeddingDim, rng: rng));
            Cell = RegisterModule("cell", new LSTMCell(embeddingDim, hiddenSize, rng: rng));
            Head = RegisterModule("head", new Linear(hiddenSize, vocab, rng: rng));
        }

        /// <summary>Forward для пакета последовательностей: x (B, T) Int32 -> (B*T, V) logits.</summary>
        public Tensor ForwardLogits(Tensor xIds)
        {
            int B = xIds.Shape[0], T = xIds.Shape[1];
            Tensor emb = Emb.Forward(xIds);

            Tensor h = Tensor.Zeros(new Shape(B, Hidden));
            Tensor c = Tensor.Zeros(new Shape(B, Hidden));
            var logitsList = new List<Tensor>(T);

            for (int t = 0; t < T; t++)
            {
                Tensor xt = IndexingOps.Narrow(emb, 1, t, 1).Squeeze(1);
                (h, c) = Cell.Step(xt, h, c);
                Tensor logitT = Head.Forward(h);
                logitsList.Add(logitT);
            }

            Tensor stacked = IndexingOps.Stack(logitsList, 1);
            return stacked.Reshape(B * T, Vocab);
        }

        /// <summary>Один шаг для генерации (без autograd).</summary>
        public (Tensor h, Tensor c, Tensor logits) StepLogits(int tokenId, Tensor h, Tensor c)
        {
            Tensor xIdx = Tensor.From(new[] { tokenId }, new Shape(1, 1));
            Tensor emb = Emb.Forward(xIdx);
            Tensor xt = IndexingOps.Narrow(emb, 1, 0, 1).Squeeze(1);
            h ??= Tensor.Zeros(new Shape(1, Hidden));
            c ??= Tensor.Zeros(new Shape(1, Hidden));
            (h, c) = Cell.Step(xt, h, c);
            Tensor logits = Head.Forward(h);
            return (h, c, logits);
        }

        public override Tensor Forward(Tensor input) => ForwardLogits(input);
    }
}
