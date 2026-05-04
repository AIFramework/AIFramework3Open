using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AI.DataPrepaire.DataNormalizers;
using AI.DataPrepaire.FeatureExtractors;
using AI.DataPrepaire.Pipelines;
using AI.DataPrepaire.Pipelines.RL;
using AI.DataPrepaire.Pipelines.Utils;
using AI.DataStructs;
using AI.DataStructs.Algebraic;
using AI.ML.Classification;
using AI.ML.DataHandling.DataSets;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Ops;
using AI.ML.NeuralNetworks.V2.Optim;
using AI.Statistics;
using V2Tensor = AI.ML.NeuralNetworks.V2.Tensor;
using V2Shape = AI.ML.NeuralNetworks.V2.Shape;

namespace RLTest
{
    /// <summary>
    /// Демо-скрипт RL-конвейера AI.DataPrepaire поверх V2-нейроклассификатора:
    /// агент пытается верно классифицировать два искусственных класса в каждой партии,
    /// получая ревард = число верных ответов и тренируясь на TopK-партиях.
    /// </summary>
    public partial class Form1 : Form
    {
        private readonly List<Vector> _xList = new();
        private readonly List<int> _yList = new();
        private readonly RLEnv _env = new();
        private readonly Vector _scores = new();
        private readonly Vector _xs = new();

        private int _allCount;
        private const int LenM = 40;

        public Form1()
        {
            InitializeComponent();

            var random = new Random(1);
            Vector cl1 = new Vector(2, 2, 8, 11, -2);
            Vector cl2 = new Vector(1, 6, 8, 11, -2);

            for (int i = 0; i < LenM; i++)
            {
                _xList.Add(cl1 + 2 * Statistic.RandNorm(5, random));
                _xList.Add(cl2 + 2 * Statistic.RandNorm(5, random));
                _yList.Add(0);
                _yList.Add(1);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            const int count = 15;
            double mScore = 0;

            for (int i = 0; i < count; i++)
                mScore += RunMatch();

            int[] cls = _env.RewardData[_env.RewardData.Count - 1].Actions;
            ShowScatter(cls);

            _env.Train(3);
            if (_env.RewardData.Count > 1500) _env.ClearData();

            mScore /= count;
            _xs.Add(_allCount);
            _scores.Add((mScore - LenM) / LenM);

            chartVisual1.PlotBlack(_xs, _scores);
            _allCount += count;
        }

        private static double GetScore(int[] pred, int[] truth)
        {
            int n = Math.Min(pred.Length, truth.Length);
            int score = 0;
            for (int i = 0; i < n; i++)
                if (pred[i] == truth[i]) score++;
            return score;
        }

        private double RunMatch()
        {
            int[] pred = new int[_yList.Count];
            for (int i = 0; i < _xList.Count; i++)
                pred[i] = _env.GetAction(_xList[i], 0, 1.8);

            double score = GetScore(pred, _yList.ToArray());
            _env.SetReward(score);
            return score;
        }

        private void ShowScatter(int[] marks)
        {
            Vector cl1X = new(), cl1Y = new();
            Vector cl2X = new(), cl2Y = new();

            for (int i = 0; i < marks.Length - 1; i++)
            {
                if (marks[i] == 0)
                {
                    cl1X.Add(_xList[i][0]);
                    cl1Y.Add(_xList[i][1]);
                }
                else
                {
                    cl2X.Add(_xList[i][0]);
                    cl2Y.Add(_xList[i][1]);
                }
            }

            chartVisual2.Clear();
            chartVisual2.AddScatter(cl1X, cl1Y, "Класс 1", Color.Blue);
            chartVisual2.AddScatter(cl2X, cl2Y, "Класс 2", Color.Gray);
        }
    }

    /// <summary>RL-окружение с актором.</summary>
    public class RLEnv : RLWithoutCriticPipeline<Vector>
    {
        public RLEnv() { Actor = new Agent(); }
    }

    /// <summary>Интеллектуальный агент: ZNorm + V2-классификатор (Linear -> ReLU -> Linear).</summary>
    public class Agent : ObjectClassifierPipeline<Vector>
    {
        public Agent()
        {
            Normalizer = new ZNormalizer();
            Detector = new NoDetector<Vector>();
            Extractor = new NoExtractor();
            DataAugmetation = new NoAugmentation<Vector>();

            Classifier = new V2NeuralClassifier(
                inputDim: 5, numClasses: 2, hidden: 15,
                epochs: 1, lr: 1e-3f, seed: 11);
        }
    }

    /// <summary>
    /// Простейший V2-нейроклассификатор: Linear(in,h) -> ReLU -> Linear(h,C).
    /// Сохраняет/загружает state-dict через стандартный <see cref="ISavable"/>-контракт демо-уровня
    /// (для RL не используется, но требуется интерфейсом).
    /// </summary>
    internal sealed class V2NeuralClassifier : IClassifier
    {
        private readonly int _inputDim;
        private readonly int _numClasses;
        private readonly int _epochs;
        private readonly float _lr;
        private readonly Sequential _net;

        public V2NeuralClassifier(int inputDim, int numClasses, int hidden, int epochs, float lr, int seed)
        {
            _inputDim = inputDim;
            _numClasses = numClasses;
            _epochs = epochs;
            _lr = lr;
            var rng = new Random(seed);
            _net = new Sequential(
                new Linear(inputDim, hidden, true, rng),
                new ReLU(),
                new Linear(hidden, numClasses, true, rng));
        }

        public void Train(Vector[] features, int[] classes)
        {
            if (features == null || features.Length == 0) return;
            int N = features.Length;

            var data = new float[N * _inputDim];
            var ids = new int[N];
            for (int i = 0; i < N; i++)
            {
                var f = features[i];
                if (f.Count != _inputDim)
                    throw new ArgumentException($"Ожидался вектор длины {_inputDim}, получено {f.Count}.");
                for (int j = 0; j < _inputDim; j++)
                    data[i * _inputDim + j] = (float)f[j];
                ids[i] = classes[i];
            }

            V2Tensor x = V2Tensor.From(data, new V2Shape(N, _inputDim));
            V2Tensor y = V2Tensor.From(ids, new V2Shape(N));

            var optim = new Adam(_net.Parameters(), lr: _lr);
            for (int epoch = 0; epoch < _epochs; epoch++)
            {
                optim.ZeroGrad();
                V2Tensor logits = _net.Forward(x);
                V2Tensor loss = ClassificationLosses.CrossEntropy(logits, y);
                loss.Backward();
                optim.Step();
            }
        }

        public void Train(VectorDataset dataset)
        {
            var feats = new Vector[dataset.Count];
            var ids = new int[dataset.Count];
            for (int i = 0; i < dataset.Count; i++)
            {
                feats[i] = dataset[i].Features;
                ids[i] = dataset[i].ClassMark;
            }
            Train(feats, ids);
        }

        public int Classify(Vector inp)
        {
            using var _ = TapeContext.NoGrad();
            V2Tensor logits = ForwardSingle(inp);
            var s = logits.AsReadOnlySpan<float>();
            int best = 0;
            float bestVal = s[0];
            for (int c = 1; c < _numClasses; c++)
                if (s[c] > bestVal) { bestVal = s[c]; best = c; }
            return best;
        }

        public Vector ClassifyProbVector(Vector inp)
        {
            using var _ = TapeContext.NoGrad();
            V2Tensor logits = ForwardSingle(inp);
            V2Tensor probs = SoftmaxOps.Softmax(logits, axis: -1);
            var s = probs.AsReadOnlySpan<float>();
            var v = new Vector(_numClasses);
            for (int c = 0; c < _numClasses; c++) v[c] = s[c];
            return v;
        }

        private V2Tensor ForwardSingle(Vector inp)
        {
            if (inp.Count != _inputDim)
                throw new ArgumentException($"Ожидался вектор длины {_inputDim}, получено {inp.Count}.");
            var data = new float[_inputDim];
            for (int j = 0; j < _inputDim; j++) data[j] = (float)inp[j];
            return _net.Forward(V2Tensor.From(data, new V2Shape(1, _inputDim))).Reshape(_numClasses);
        }

        public void Save(string path) =>
            throw new NotSupportedException("Сериализация V2NeuralClassifier не реализована в демо.");

        public void Save(Stream stream) =>
            throw new NotSupportedException("Сериализация V2NeuralClassifier не реализована в демо.");

        public void Load(string path) =>
            throw new NotSupportedException("Десериализация V2NeuralClassifier не реализована в демо.");

        public void Load(Stream stream) =>
            throw new NotSupportedException("Десериализация V2NeuralClassifier не реализована в демо.");
    }
}
