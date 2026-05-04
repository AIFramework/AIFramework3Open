using AI.ONNX;
using AI.ONNX.NLP.Bert;
using AI.DataStructs.Algebraic;
using AI.Charts;
using AiFrameworkDemo.Core;
using SkiaSharp;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.ONNX
{
    /// <summary>
    /// Демо-кейсы AI.ONNX.
    /// Инференс реализован как точная математика соответствующих слоёв (W·x+b, softmax,
    /// косинусное сходство), идентичная тому, что выполняет ONNX Runtime.
    /// При наличии реальных .onnx-файлов замените симуляцию вызовами AI.ONNX-классов
    /// (пример API показан в текстовом выводе каждого демо).
    /// </summary>
    public static class OnnxDemoRunner
    {
        private static readonly SKColor[] Pal =
        [
            new(0x34, 0xD3, 0x99), new(0xF8, 0x71, 0x71), new(0x60, 0xA5, 0xFA),
            new(0xFB, 0xBF, 0x24), new(0xA7, 0x8B, 0xFA), new(0xFB, 0x92, 0x3C),
            new(0x38, 0xBD, 0xF8), new(0xF4, 0x72, 0xB6), new(0xE8, 0x79, 0xF9),
            new(0xFF, 0xE0, 0x60), new(0x22, 0xD3, 0xEE), new(0x4A, 0xDE, 0x80),
        ];
        private static SKColor C(int i) => Pal[i % Pal.Length];

        // -- Точка входа -------------------------------------------------------

        public static DemoResult Run(string key, IReadOnlyDictionary<string, double> p, DemoSettings s)
        {
            var cv = MakeView(s);
            string txt = key switch
            {
                "dense_inference" => DoDenseInference(p, cv),
                "t2t_image"       => DoT2TImage(p, cv),
                "softmax_cls"     => DoSoftmaxClassifier(p, cv),
                "embed_cosine"    => DoEmbedCosine(p, cv),
                "bert_config"     => DoBertConfig(p, cv),
                _                 => $"Неизвестный ключ «{key}»",
            };
            return Png(cv, s, textOutput: txt);
        }

        // -- 1. Dense Layer ----------------------------------------------------

        private static string DoDenseInference(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n    = I(p, "inputDim",  16);
            int m    = I(p, "outputDim",  8);
            int actI = I(p, "activation", 0);
            int seed = I(p, "seed",      42);

            var rng = new Random(seed);

            // Случайная инициализация (Xavier)
            double scale = Math.Sqrt(2.0 / (n + m));
            var W = new double[m, n];
            var b = new double[m];
            for (int i = 0; i < m; i++)
            {
                b[i] = (rng.NextDouble() - 0.5) * 0.1;
                for (int j = 0; j < n; j++) W[i, j] = (rng.NextDouble() - 0.5) * 2 * scale;
            }

            // Входной вектор
            var x = new double[n];
            for (int j = 0; j < n; j++) x[j] = rng.NextDouble() * 2 - 1;

            // Прямой проход: y = activate(W·x + b)
            var y = new double[m];
            for (int i = 0; i < m; i++)
            {
                double v = b[i];
                for (int j = 0; j < n; j++) v += W[i, j] * x[j];
                y[i] = Activate(v, actI);
            }

            // Статистика матрицы весов
            double wMin = double.MaxValue, wMax = double.MinValue, wMean = 0;
            for (int i = 0; i < m; i++) for (int j = 0; j < n; j++) { wMin = Math.Min(wMin, W[i,j]); wMax = Math.Max(wMax, W[i,j]); wMean += W[i,j]; }
            wMean /= (m * n);

            // -- визуализация --
            var xIdxIn  = VectorRange(n);
            var xIdxOut = VectorRange(m);
            cv.AddPlot(xIdxIn,  new Vector(x), "Вход x",   C(0), 2);
            cv.AddPlot(xIdxOut, new Vector(y), "Выход y",  C(1), 2);
            cv.AddScatter(xIdxIn, new Vector(x), "x[i]", C(0));
            cv.AddScatter(xIdxOut, new Vector(y), "y[i]", C(1));

            // -- текст --
            var sb = new StringBuilder();
            sb.AppendLine($"Слой: Dense({n} -> {m}), активация: {ActName(actI)}");
            sb.AppendLine($"Параметры: W[{m}×{n}], b[{m}]   всего {m*n + m} чисел");
            sb.AppendLine($"Вес W: min={wMin:F4}  max={wMax:F4}  mean={wMean:F4}");
            sb.AppendLine();
            sb.AppendLine("Вход x:");
            sb.AppendLine("  " + string.Join(" ", x.Select(v => $"{v:F3}")));
            sb.AppendLine("Выход y = act(W·x + b):");
            sb.AppendLine("  " + string.Join(" ", y.Select(v => $"{v:F3}")));
            sb.AppendLine();
            sb.AppendLine("-- API (реальная модель) ------------------");
            sb.AppendLine("using AI.ONNX.Base.LayersModel;");
            sb.AppendLine();
            sb.AppendLine("using var dense = new Dense(\"model.onnx\", DataType.Float32);");
            sb.AppendLine("var inputVec = new Vector(x);");
            sb.AppendLine("Vector output = dense.ForwardNoBatch(inputVec);");

            return sb.ToString();
        }

        // -- 2. Tensor2Tensor (изображение) -----------------------------------

        private static string DoT2TImage(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int H    = I(p, "H",      16);
            int W    = I(p, "W",      16);
            int Ch   = I(p, "C",       1);
            int outD = I(p, "outDim", 16);
            int seed = I(p, "seed",   42);

            var rng = new Random(seed);

            // Случайный входной тензор [H, W, Ch]
            int inpFlat = H * W * Ch;
            var inp = new double[inpFlat];
            for (int i = 0; i < inpFlat; i++) inp[i] = rng.NextDouble();

            // Случайная проекция (симуляция Global Average Pool + Dense)
            var proj = new double[outD];
            double step = (double)inpFlat / outD;
            for (int i = 0; i < outD; i++)
            {
                int start = (int)(i * step);
                int end   = Math.Min((int)((i + 1) * step), inpFlat);
                double v = 0;
                for (int k = start; k < end; k++) v += inp[k];
                proj[i] = end > start ? v / (end - start) : 0;
            }

            // -- визуализация: "пиксели" как scatter --
            int show = Math.Min(H * W, 256);
            var px = new Vector(show);
            var py = new Vector(show);
            for (int i = 0; i < show; i++) { px[i] = i % W; py[i] = -(i / W); }
            cv.AddScatter(px, py, $"Вход [{H}×{W}×{Ch}]", C(0));

            // Выходные значения
            var outX = VectorRange(outD);
            cv.AddPlot(outX, new Vector(proj), "Выход (GAP+proj)", C(1), 2);

            // -- текст --
            var sb = new StringBuilder();
            sb.AppendLine($"Входной тензор: [{H} × {W} × {Ch}]  ({inpFlat} элементов)");
            sb.AppendLine($"Выходной вектор: [{outD}]");
            sb.AppendLine();
            sb.AppendLine($"Min вход: {inp.Min():F4}  Max: {inp.Max():F4}");
            sb.AppendLine($"Min выход: {proj.Min():F4}  Max: {proj.Max():F4}");
            sb.AppendLine();
            sb.AppendLine("-- API (реальная модель) ------------------");
            sb.AppendLine("using AI.ONNX;");
            sb.AppendLine("using AI.DataStructs.Algebraic;");
            sb.AppendLine();
            sb.AppendLine("// LibType.Keras  -> каналы последними [H, W, C]");
            sb.AppendLine("// LibType.PyTorch -> каналы первыми  [C, H, W]");
            sb.AppendLine("using var t2t = new Tensor2Tensor(\"model.onnx\",");
            sb.AppendLine("    libType: LibType.Keras, libTypeOut: LibType.Keras);");
            sb.AppendLine();
            sb.AppendLine("var inputTensor = new Tensor(H, W, C);");
            sb.AppendLine("// ... заполнить inputTensor ...");
            sb.AppendLine("Tensor output = t2t.Transform(inputTensor);");
            sb.AppendLine($"// Выход: [{t2t_DimStr("H'", "W'", "D'")}]");

            return sb.ToString();
        }

        // -- 3. Softmax-классификатор ------------------------------------------

        private static string DoSoftmaxClassifier(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n    = I(p, "inputDim",   16);
            int k    = I(p, "numClasses",  5);
            int seed = I(p, "seed",       42);

            var rng = new Random(seed);
            double scale = Math.Sqrt(2.0 / (n + k));

            // Веса и смещения
            var W = new double[k, n];
            var b = new double[k];
            for (int i = 0; i < k; i++)
            {
                b[i] = (rng.NextDouble() - 0.5) * 0.1;
                for (int j = 0; j < n; j++) W[i, j] = (rng.NextDouble() - 0.5) * 2 * scale;
            }

            // Случайный вход
            var x = new double[n];
            for (int j = 0; j < n; j++) x[j] = rng.NextDouble() * 2 - 1;

            // Логиты и softmax
            var logits = new double[k];
            for (int i = 0; i < k; i++) { logits[i] = b[i]; for (int j = 0; j < n; j++) logits[i] += W[i, j] * x[j]; }

            var probs = Softmax(logits);
            int predicted = Array.IndexOf(probs, probs.Max());

            // -- визуализация: вероятности классов --
            var classX = VectorRange(k);
            cv.AddPlot(classX, new Vector(probs),   "P(класс)",  C(0), 2);
            cv.AddScatter(classX, new Vector(probs), "P(класс)", C(0));
            cv.AddPlot(classX, new Vector(logits), "Логиты", C(1), 1);

            // -- текст --
            var sb = new StringBuilder();
            sb.AppendLine($"Классификатор: {n} признаков -> {k} классов");
            sb.AppendLine();
            sb.AppendLine("Логиты:");
            for (int i = 0; i < k; i++) sb.AppendLine($"  Класс {i}: {logits[i]:+0.0000;-0.0000}");
            sb.AppendLine();
            sb.AppendLine("Вероятности (Softmax):");
            for (int i = 0; i < k; i++) sb.AppendLine($"  Класс {i}: {probs[i]:P2}{(i == predicted ? " <- предсказан" : "")}");
            sb.AppendLine();
            sb.AppendLine($"Энтропия: {Entropy(probs):F4} бит");
            sb.AppendLine();
            sb.AppendLine("-- API (реальная модель) ------------------");
            sb.AppendLine("using AI.ONNX.Classifiers;");
            sb.AppendLine("using AI.DataStructs.Algebraic;");
            sb.AppendLine();
            sb.AppendLine("// GrayScaleClassifier ожидает Matrix изображения");
            sb.AppendLine("using var cls = new GrayScaleClassifier(\"model.onnx\", LibType.Keras);");
            sb.AppendLine("var img = new Matrix(H, W); // ... заполнить ...");
            sb.AppendLine("Vector probabilities = cls.Classify(img);");
            sb.AppendLine("int classId = probabilities.MaxIdx();");

            return sb.ToString();
        }

        // -- 4. Embedding Cosine Similarity ------------------------------------

        private static string DoEmbedCosine(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int n    = I(p, "numTokens",  8);
            int d    = I(p, "embedDim",  16);
            int pool = I(p, "pooling",    0);
            int seed = I(p, "seed",      42);

            var rng = new Random(seed);

            // Генерируем "эмбеддинги" — нормализованные случайные векторы
            // (делаем кластеры: n/2 пар близких векторов)
            var embeds = new double[n][];
            for (int i = 0; i < n; i++)
            {
                embeds[i] = new double[d];
                // Каждая пара токенов — близкие векторы
                int clusterBase = (i / 2) * 2;
                for (int di = 0; di < d; di++)
                    embeds[i][di] = (rng.NextDouble() - 0.5) +
                                    (clusterBase < n ? Math.Sin(di * clusterBase) * 0.8 : 0);
                Normalize(embeds[i]);
            }

            // Пулинг: для Demo pool = mean из двух первых векторов
            var pooledEmbed = pool switch
            {
                1 => MaxPool(embeds),
                2 => embeds[0],  // CLS-token = первый
                _ => MeanPool(embeds)
            };

            // Матрица косинусного сходства
            var sim = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    sim[i, j] = CosineSim(embeds[i], embeds[j]);

            // -- визуализация: верхние и нижние пары --
            // Scatter: позиции токенов в 2D (проекция первых двух компонент)
            var tx = new Vector(n);
            var ty = new Vector(n);
            for (int i = 0; i < n; i++) { tx[i] = embeds[i][0]; ty[i] = embeds[i][1]; }
            cv.AddScatter(tx, ty, "Токены (2D-проекция)", C(0));

            // Показываем похожесть top-пар
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if (sim[i, j] > 0.6)
                    {
                        var lineX = new Vector(2) { [0] = embeds[i][0], [1] = embeds[j][0] };
                        var lineY = new Vector(2) { [0] = embeds[i][1], [1] = embeds[j][1] };
                        cv.AddPlot(lineX, lineY, $"sim({i},{j})={sim[i,j]:F2}", C(2), 1);
                    }

            // -- текст --
            var sb = new StringBuilder();
            sb.AppendLine($"Токенов: {n}, Размерность эмбеддинга: {d}");
            sb.AppendLine($"Пулинг: {PoolName(pool)}");
            sb.AppendLine();
            sb.AppendLine("Матрица косинусного сходства (верхний треугольник):");
            for (int i = 0; i < n; i++)
            {
                sb.Append($"  T{i}: ");
                for (int j = 0; j < n; j++) sb.Append($"{sim[i,j]:F2} ");
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine($"Pooled embedding norm: {Norm(pooledEmbed):F4}");

            // Топ-3 похожих пары
            var pairs = new List<(int, int, double)>();
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    pairs.Add((i, j, sim[i, j]));
            pairs.Sort((a, b2) => b2.Item3.CompareTo(a.Item3));
            sb.AppendLine();
            sb.AppendLine("Топ-3 похожих пары:");
            foreach (var (i, j, s) in pairs.Take(3))
                sb.AppendLine($"  T{i} <-> T{j}: {s:F4}");

            sb.AppendLine();
            sb.AppendLine("-- API (реальная модель) ------------------");
            sb.AppendLine("using AI.ONNX.NLP.Bert;");
            sb.AppendLine();
            sb.AppendLine("var embedder = BertEmbedder.FromPretrained(\"path/to/model/\");");
            sb.AppendLine("Vector sentVec = embedder.ForwardSBert(\"Пример предложения\");");
            sb.AppendLine("// ForwardSBert: mean pooling по всем токенам");
            sb.AppendLine();
            sb.AppendLine("var blocks = embedder.ForwardBlockPooling(texts, weights);");

            return sb.ToString();
        }

        // -- 5. BertConfig / Архитектура ---------------------------------------

        private static string DoBertConfig(IReadOnlyDictionary<string, double> p, ChartView cv)
        {
            int hidden  = I(p, "hiddenSize",       384);
            int layers  = I(p, "numLayers",          6);
            int heads   = I(p, "numHeads",            6);
            int ffn     = I(p, "intermediateSize", 1536);
            int vocab   = I(p, "vocabSize",        30522);

            // Создаём реальный BertConfig
            var cfg = new BertConfig
            {
                HiddenSize        = hidden,
                NumHiddenLayers   = layers,
                NumAttentionHeads = heads,
                IntermediateSize  = ffn,
                VocabSize         = vocab,
                MaxPositionEmbeddings = 512,
                HiddenAct         = "gelu",
            };

            // Оцениваем число параметров
            long embedParams    = (long)vocab * hidden + 512 * hidden + 2 * hidden;
            long attnParams     = 4L * hidden * hidden + 4 * hidden;
            long ffnParams      = 2L * hidden * ffn + hidden + ffn;
            long layerNormParam = 4L * hidden;
            long perLayer       = attnParams + ffnParams + layerNormParam;
            long totalParams    = embedParams + layers * perLayer;

            // -- визуализация: параметры по компонентам --
            var labels = new[] { "Embeddings", "Attention", "FFN", "LayerNorm" };
            var counts = new double[] { embedParams, layers * attnParams, layers * (long)hidden * ffn * 2, layers * layerNormParam };
            var xV     = VectorRange(4);
            var yV     = new Vector(counts);
            cv.AddPlot(xV, yV, "Параметров по компонентам", C(0), 2);
            cv.AddScatter(xV, yV, "Компоненты", C(0));

            // Размеры промежуточных тензоров
            var seqLen  = new double[] { 16, 32, 64, 128, 256, 512 };
            var memMB   = seqLen.Select(s => s * hidden * 4 * layers / 1e6).ToArray();
            var xSeq    = new Vector(seqLen);
            var yMem    = new Vector(memMB);
            cv.AddPlot(xSeq, yMem, "Память активаций (MB)", C(1), 2);

            // -- текст --
            var sb = new StringBuilder();
            sb.AppendLine($"BertConfig (реальный объект из AI.ONNX.NLP.Bert)");
            sb.AppendLine();
            sb.AppendLine($"Архитектура:");
            sb.AppendLine($"  HiddenSize:      {cfg.HiddenSize}");
            sb.AppendLine($"  NumHiddenLayers: {cfg.NumHiddenLayers}");
            sb.AppendLine($"  NumAttentionHeads:{cfg.NumAttentionHeads}");
            sb.AppendLine($"  IntermediateSize:{cfg.IntermediateSize}");
            sb.AppendLine($"  VocabSize:       {cfg.VocabSize}");
            sb.AppendLine($"  MaxPositionEmb:  {cfg.MaxPositionEmbeddings}");
            sb.AppendLine($"  HiddenAct:       {cfg.HiddenAct}");
            sb.AppendLine();
            sb.AppendLine($"Оценка параметров (FFN):");
            sb.AppendLine($"  Embeddings: {embedParams / 1e6:F1} M");
            sb.AppendLine($"  Attention:  {layers * attnParams / 1e6:F1} M × {layers} слоёв");
            sb.AppendLine($"  FFN:        {layers * (long)hidden * ffn * 2 / 1e6:F1} M × {layers} слоёв");
            sb.AppendLine($"  ---------------------------------");
            sb.AppendLine($"  ИТОГО:      ≈ {totalParams / 1e6:F1} M параметров");
            sb.AppendLine();
            sb.AppendLine($"Размер активации (seq=512, float32): {512 * hidden * 4 / 1024.0:F0} KB/слой");
            sb.AppendLine();
            sb.AppendLine("-- API ----------------------------------");
            sb.AppendLine("var cfg = BertConfig.FromJson(\"config.json\");");
            sb.AppendLine($"// cfg.HiddenSize = {cfg.HiddenSize}");
            sb.AppendLine();
            sb.AppendLine("var embedder = BertEmbedder.FromPretrained(\"folder/\");");
            sb.AppendLine("// Folder содержит: vocab.txt, tokenizer_config.json,");
            sb.AppendLine("//                  model.onnx, config.json");

            return sb.ToString();
        }

        // -- Математические вспомогательные методы ----------------------------

        private static double Activate(double v, int act) => act switch
        {
            0 => Math.Max(0, v),                            // ReLU
            1 => 1.0 / (1.0 + Math.Exp(-v)),               // Sigmoid
            2 => Math.Tanh(v),                              // Tanh
            _ => v,                                         // Linear
        };

        private static string ActName(int act) => act switch { 0 => "ReLU", 1 => "Sigmoid", 2 => "Tanh", _ => "Linear" };

        private static double[] Softmax(double[] logits)
        {
            double max = logits.Max();
            var e = logits.Select(v => Math.Exp(v - max)).ToArray();
            double sum = e.Sum();
            return e.Select(v => v / sum).ToArray();
        }

        private static double Entropy(double[] probs)
        {
            double h = 0;
            foreach (var p in probs) if (p > 1e-12) h -= p * Math.Log(p, 2);
            return h;
        }

        private static void Normalize(double[] v)
        {
            double n = Math.Sqrt(v.Sum(x => x * x));
            if (n > 1e-9) for (int i = 0; i < v.Length; i++) v[i] /= n;
        }

        private static double Norm(double[] v) => Math.Sqrt(v.Sum(x => x * x));

        private static double CosineSim(double[] a, double[] b)
        {
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
            return (na < 1e-12 || nb < 1e-12) ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        private static double[] MeanPool(double[][] vecs)
        {
            int d = vecs[0].Length;
            var r = new double[d];
            foreach (var v in vecs) for (int i = 0; i < d; i++) r[i] += v[i];
            for (int i = 0; i < d; i++) r[i] /= vecs.Length;
            return r;
        }

        private static double[] MaxPool(double[][] vecs)
        {
            int d = vecs[0].Length;
            var r = new double[d];
            for (int i = 0; i < d; i++) r[i] = vecs.Max(v => v[i]);
            return r;
        }

        private static string PoolName(int i) => i switch { 1 => "Max", 2 => "CLS-token", _ => "Mean" };

        private static string t2t_DimStr(string h, string w, string d) => $"{h}×{w}×{d}";

        private static Vector VectorRange(int n)
        {
            var v = new Vector(n);
            for (int i = 0; i < n; i++) v[i] = i;
            return v;
        }
    }
}
