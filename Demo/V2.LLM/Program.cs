using System;
using System.Diagnostics;
using System.Text;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;

namespace V2.LLM;

/// <summary>
/// Демо: char-level language model на трансформере (V2 ядро).
/// </summary>
/// <remarks>
/// <para>
/// Учим модель на маленьком корпусе из синтетического текста ABCDEF... +
/// ритмических паттернов. Демонстрирует:
/// </para>
/// <list type="bullet">
///   <item><see cref="Embedding"/> + позиционная синусная кодировка</item>
///   <item><see cref="TransformerEncoderLayer"/> с causal-маской</item>
///   <item><see cref="ClassificationLosses.CrossEntropy"/> на каждом токене</item>
///   <item>Жадная генерация продолжения</item>
/// </list>
/// </remarks>
internal static class Program
{
    private const int VocabSize = 30;        // 'A'..'Z' + 4 спец-токена
    private const int DModel = 64;
    private const int NHead = 4;
    private const int NLayers = 2;
    private const int FfnDim = 128;
    private const int SeqLen = 32;
    private const int BatchSize = 32;
    private const int Epochs = 8;
    private const int StepsPerEpoch = 50;

    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var rng = new Random(7);

        Console.WriteLine("V2.LLM demo — char-LM на трансформере (синтетика).");

        // Корпус: повторяющиеся ритмические шаблоны.
        // "ABCDABCD...", "AABBCCDD...", фразы. Модель должна выучить периодичность.
        string corpus = BuildCorpus();
        Console.WriteLine($"Корпус: {corpus.Length} символов.");

        var tokenized = Tokenize(corpus);

        var embedding = new Embedding(VocabSize, DModel, rng: rng);
        var pe = new SinusoidalPositionalEncoding(DModel, maxLen: SeqLen + 64);
        var encLayer = new TransformerEncoderLayer(DModel, NHead, FfnDim,
            dropout: 0.1f, normFirst: true, rng: rng);
        var encoder = new TransformerEncoder(encLayer, NLayers, finalNorm: new LayerNorm(DModel));
        var head = new Linear(DModel, VocabSize, bias: true, rng: rng);

        // Объединим все параметры в один список.
        var allParams = new System.Collections.Generic.List<Parameter>();
        allParams.AddRange(embedding.Parameters());
        allParams.AddRange(encoder.Parameters());
        allParams.AddRange(head.Parameters());

        var optim = Adam.AdamW(allParams, lr: 3e-4f, weightDecay: 1e-4f);

        Console.WriteLine($"Параметров: {ParamCount(allParams)}");

        var sw = Stopwatch.StartNew();
        for (int epoch = 1; epoch <= Epochs; epoch++)
        {
            float loss = 0f;
            for (int s = 0; s < StepsPerEpoch; s++)
            {
                var (xb, yb) = SampleBatch(tokenized, rng);
                var logits = ForwardModel(embedding, pe, encoder, head, xb);
                // logits: (B, T, V), yb: (B, T) — flatten для CE.
                int B = logits.Shape[0], T = logits.Shape[1], V = logits.Shape[2];
                var logitsFlat = logits.Reshape(B * T, V);
                var ybFlat = yb.Reshape(B * T);
                var l = ClassificationLosses.CrossEntropy(logitsFlat, ybFlat);
                l.Backward();
                optim.Step();
                optim.ZeroGrad();
                loss += l.AsReadOnlySpan<float>()[0];
            }
            Console.WriteLine($"Epoch {epoch}: avg_loss={loss / StepsPerEpoch:F4}, elapsed={sw.Elapsed.TotalSeconds:F1}s");
        }

        // Жадная генерация.
        encoder.Eval(); head.Eval(); embedding.Eval();
        string seed = "ABCDABCD";
        Console.Write($"\nГенерация (seed=\"{seed}\"):\n  {seed}");
        var ctx = new System.Collections.Generic.List<int>();
        foreach (char c in seed) ctx.Add(CharToTok(c));

        for (int step = 0; step < 60; step++)
        {
            int t = Math.Min(SeqLen, ctx.Count);
            var inp = new int[t];
            for (int i = 0; i < t; i++) inp[i] = ctx[ctx.Count - t + i];
            var x = Tensor.From(inp, new Shape(1, t));
            using var _ = TapeContext.NoGrad();
            var logits = ForwardModel(embedding, pe, encoder, head, x);
            var ls = logits.AsReadOnlySpan<float>();
            // последняя позиция
            int best = 0;
            float bv = ls[(t - 1) * VocabSize];
            for (int v = 1; v < VocabSize; v++)
            {
                float val = ls[(t - 1) * VocabSize + v];
                if (val > bv) { bv = val; best = v; }
            }
            ctx.Add(best);
            char ch = TokToChar(best);
            Console.Write(ch);
        }
        Console.WriteLine("\n\nГотово.");
    }

    private static int ParamCount(System.Collections.Generic.IEnumerable<Parameter> ps)
    {
        int n = 0;
        foreach (var p in ps) n += (int)p.Tensor.Shape.NumElements;
        return n;
    }

    private static Tensor ForwardModel(Embedding emb, SinusoidalPositionalEncoding pe,
        TransformerEncoder enc, Linear head, Tensor inp)
    {
        // inp: (B, T) Int32
        var e = emb.Forward(inp);                        // (B, T, D)
        e = pe.Forward(e);                                // + sinusoidal pe
        var h = enc.Forward(e, mask: null, isCausal: true); // (B, T, D)
        return head.Forward(h);                           // (B, T, V)
    }

    private static (Tensor, Tensor) SampleBatch(int[] tokens, Random rng)
    {
        var x = new int[BatchSize * SeqLen];
        var y = new int[BatchSize * SeqLen];
        int max = tokens.Length - SeqLen - 1;
        for (int b = 0; b < BatchSize; b++)
        {
            int start = rng.Next(max);
            for (int t = 0; t < SeqLen; t++)
            {
                x[b * SeqLen + t] = tokens[start + t];
                y[b * SeqLen + t] = tokens[start + t + 1];
            }
        }
        return (Tensor.From(x, new Shape(BatchSize, SeqLen)),
                Tensor.From(y, new Shape(BatchSize, SeqLen)));
    }

    private static string BuildCorpus()
    {
        var sb = new StringBuilder(20_000);
        // ABCDABCDABCD... много раз
        for (int i = 0; i < 200; i++) sb.Append("ABCDABCDABCDABCDABCDABCDABCD");
        sb.Append('\n');
        // AABBCCDD...
        for (int i = 0; i < 200; i++) sb.Append("AABBCCDDAABBCCDDAABBCCDDAABBCCDD");
        sb.Append('\n');
        // Фраза на простом языке (выученное предложение)
        for (int i = 0; i < 100; i++) sb.Append("HELLO WORLD HELLO AI HELLO TENSOR ");
        return sb.ToString();
    }

    private static int[] Tokenize(string s)
    {
        var arr = new int[s.Length];
        for (int i = 0; i < s.Length; i++) arr[i] = CharToTok(s[i]);
        return arr;
    }

    private static int CharToTok(char c)
    {
        if (c >= 'A' && c <= 'Z') return c - 'A';
        if (c == ' ') return 26;
        if (c == '\n') return 27;
        if (c == '.') return 28;
        return 29;
    }

    private static char TokToChar(int t)
    {
        if (t >= 0 && t < 26) return (char)('A' + t);
        if (t == 26) return ' ';
        if (t == 27) return '\n';
        if (t == 28) return '.';
        return '?';
    }
}
