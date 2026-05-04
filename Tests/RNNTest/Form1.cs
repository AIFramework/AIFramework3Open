using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AI.DataStructs.Algebraic;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Autograd;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;
using AI.ML.NeuralNetworks.V2.Storage;
using AI.ML.NeuralNetworks.Gpu.V2;
using Tensor = AI.ML.NeuralNetworks.V2.Tensor;
using Shape = AI.ML.NeuralNetworks.V2.Shape;

namespace RNNTest
{
    /// <summary>
    /// Демо: одномерный sin-сигнал -> sin^3 (нелинейная регрессия) разными архитектурами V2.
    /// Поддерживает Filter (Linear-only), RNN, LSTM, GRU и небольшой Transformer-encoder.
    /// Все вычисления — на CPU (V2 авто-диспатч), GPU-перенос параметров пока выходит за рамки демо.
    /// </summary>
    public partial class Form1 : Form
    {
        private const int SampleCount = 166;
        private const int TrainingEpochs = 300;
        private const float LearningRate = 1e-2f;
        private const float TransformerLearningRate = 5e-3f;
        private const double SignalFrequency = 20.0;

        private static readonly int[] BatchSizes = { 1, 4, 8, 16, 32 };
        private static readonly string[] ArchNames = { "Фильтр", "RNN", "LSTM", "GRU", "Transformer" };

        private readonly Vector _steps;
        private Vector _inputSeries;
        private Vector _networkOutput;
        private readonly Vector _targetSeries;

        private Module _net;
        private Random _rng;

        private double _phaseOffset;
        private long _lastTrainMs;
        private long _lastInferMs;

        public Form1()
        {
            InitializeComponent();

            _steps = Vector.SeqBeginsWithZero(1, SampleCount);
            _inputSeries = BuildSineSeries(_steps, _phaseOffset);
            _targetSeries = _steps.Transform(r => Math.Pow(Math.Sin(SignalFrequency * r / SampleCount), 3));

            PopulateArchCombo();
            PopulateDeviceCombo();
            PopulateBatchCombo();

            _rng = new Random(42);
            _net = CreateNetwork(SelectedArch, _rng);
            _networkOutput = Forward(_inputSeries);

            progressBarTraining.Minimum = 0;
            progressBarTraining.Maximum = Math.Max(1, TrainingEpochs);
            progressBarTraining.Value = 0;
        }

        private bool _gpuAvailable;
        private string _gpuInitError;
        private const string GpuUnavailableLabel = "GPU (недоступен)";

        private void PopulateDeviceCombo()
        {
            comboDevice.Items.Add("CPU (V2)");
            try
            {
                GpuBackend.Initialize();
                comboDevice.Items.Add("GPU (CUDA)");
                _gpuAvailable = true;
            }
            catch (Exception ex)
            {
                // Не глушим — выводим placeholder и сохраняем причину для тултипа/MessageBox.
                _gpuInitError = BuildExceptionChain(ex);
                comboDevice.Items.Add(GpuUnavailableLabel);
                var tip = new ToolTip { AutoPopDelay = 30000, InitialDelay = 200, ReshowDelay = 100 };
                tip.SetToolTip(comboDevice, "GPU не инициализирован:\n" + _gpuInitError);
                comboDevice.SelectedIndexChanged += (_, __) =>
                {
                    if (!_gpuAvailable && comboDevice.SelectedIndex == 1)
                    {
                        MessageBox.Show(this,
                            "Не удалось инициализировать GPU-backend (CUDA/ILGPU):\n\n" + _gpuInitError +
                            "\n\nПроверьте установлен ли драйвер NVIDIA, наличие CUDA-устройства и что собрана свежая версия AI.NeuralNetworks.Gpu.dll.",
                            "GPU недоступен", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        comboDevice.SelectedIndex = 0;
                    }
                };
            }
            comboDevice.SelectedIndex = 0;
        }

        private static string BuildExceptionChain(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            for (var e = ex; e != null; e = e.InnerException)
                sb.Append(e.GetType().Name).Append(": ").AppendLine(e.Message);
            return sb.ToString().TrimEnd();
        }

        private bool UseGpu => _gpuAvailable && comboDevice.SelectedIndex == 1;
        private Device SelectedDevice => UseGpu ? Device.Cuda() : Device.Cpu;

        private int SelectedBatchSize => BatchSizes[comboBatch.SelectedIndex];
        private string SelectedArch => ArchNames[comboArch.SelectedIndex];

        private void PopulateArchCombo()
        {
            foreach (string n in ArchNames) comboArch.Items.Add(n);
            comboArch.SelectedIndex = 0;
        }

        private void PopulateBatchCombo()
        {
            foreach (int bs in BatchSizes) comboBatch.Items.Add($"Batch {bs}");
            comboBatch.SelectedIndex = 0;
        }

        private static Module CreateNetwork(string archType, Random rng)
        {
            return archType switch
            {
                "RNN" => new SeqRegressor(new RNN(1, 16, "tanh", true, true, rng), 16, rng),
                "LSTM" => new SeqRegressor(new LSTM(1, 16, true, true, rng), 16, rng),
                "GRU" => new SeqRegressor(new GRU(1, 16, true, true, rng), 16, rng),
                "Transformer" => new TransformerRegressor(dModel: 16, nHead: 2, dFf: 64, rng: rng),
                _ => new Sequential(
                        new Linear(1, 16, true, rng),
                        new ReLU(),
                        new Linear(16, 16, true, rng),
                        new ReLU(),
                        new Linear(16, 1, true, rng)),
            };
        }

        private static Vector BuildSineSeries(Vector steps, double phase) =>
            steps.Transform(r => Math.Sin(SignalFrequency * r / SampleCount + phase));

        private void Form1_Load(object sender, EventArgs e)
        {
            chartVisual1.PlotBlack(_inputSeries);
            Visualize();
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            SetUiBusy(true);
            try
            {
                string arch = SelectedArch;
                int batch = SelectedBatchSize;
                bool useGpu = UseGpu;
                var device = SelectedDevice;

                _rng = new Random(42);
                _net = CreateNetwork(arch, _rng);
                if (useGpu) _net.Cuda();
                progressBarTraining.Maximum = Math.Max(1, TrainingEpochs);
                progressBarTraining.Value = 0;
                string devLabel = useGpu ? "GPU" : "CPU";
                labelTraining.Text = $"[{devLabel}] Подготовка…";

                var sw = Stopwatch.StartNew();
                await Task.Run(() => Train(arch, batch, device, devLabel)).ConfigureAwait(true);
                sw.Stop();
                _lastTrainMs = sw.ElapsedMilliseconds;

                sw.Restart();
                _networkOutput = Forward(_inputSeries);
                sw.Stop();
                _lastInferMs = sw.ElapsedMilliseconds;

                Visualize();
                UpdateTimingLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Ошибка обучения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                labelTraining.Text = string.Empty;
                progressBarTraining.Value = 0;
                SetUiBusy(false);
            }
        }

        private void SetUiBusy(bool busy)
        {
            button1.Enabled = !busy;
            button2.Enabled = !busy;
            comboDevice.Enabled = !busy;
            comboBatch.Enabled = !busy;
            comboArch.Enabled = !busy;
        }

        private void UpdateTimingLabel()
        {
            string dev = UseGpu ? "GPU" : "CPU";
            labelTiming.Text = $"[{SelectedArch}/{dev} b={SelectedBatchSize}]  Обучение: {_lastTrainMs} мс  |  Инференс: {_lastInferMs} мс";
        }

        /// <summary>Обучение модели на повторённой пакетной копии целевого сигнала.</summary>
        private void Train(string arch, int batchSize, Device device, string devLabel)
        {
            int B = batchSize;

            bool isSequential = _net is Sequential;
            bool isTransformer = arch == "Transformer";

            Tensor xTensor = MakeBatchInput(_inputSeries, B, isSequential).To(device);
            Tensor yTensor = MakeBatchTarget(_targetSeries, B, isSequential).To(device);

            float lr = isTransformer ? TransformerLearningRate : LearningRate;
            var optim = new Adam(_net.Parameters(), lr: lr);

            for (int epoch = 0; epoch < TrainingEpochs; epoch++)
            {
                optim.ZeroGrad();
                Tensor pred = _net.Forward(xTensor);
                Tensor loss = RegressionLosses.MSE(pred, yTensor);
                loss.Backward();
                optim.Step();

                if ((epoch & 7) == 0 || epoch == TrainingEpochs - 1)
                {
                    float l = loss.ToCpu().AsReadOnlySpan<float>()[0];
                    int captured = epoch + 1;
                    BeginInvokeUi(() =>
                    {
                        progressBarTraining.Value = Math.Min(captured, progressBarTraining.Maximum);
                        labelTraining.Text = $"[{devLabel}] Эпоха {captured}/{TrainingEpochs}  loss={l:F4}";
                    });
                }
            }
        }

        private void BeginInvokeUi(Action a)
        {
            if (InvokeRequired) BeginInvoke(a);
            else a();
        }

        private Vector Forward(Vector input)
        {
            using var _ = TapeContext.NoGrad();
            bool isSequential = _net is Sequential;
            Tensor xTensor = MakeBatchInput(input, batchSize: 1, sequentialMode: isSequential);
            if (UseGpu) xTensor = xTensor.To(SelectedDevice);
            Tensor pred = _net.Forward(xTensor).ToCpu();

            var output = new Vector(input.Count);
            var span = pred.AsReadOnlySpan<float>();
            for (int i = 0; i < input.Count; i++)
                output[i] = span[i];
            return output;
        }

        /// <summary>Готовит тензор входа: (B*T, 1) для Sequential / (B, T, 1) для остальных.</summary>
        private static Tensor MakeBatchInput(Vector source, int batchSize, bool sequentialMode)
        {
            int T = source.Count;
            int N = batchSize * T;
            var data = new float[N];
            for (int b = 0; b < batchSize; b++)
                for (int t = 0; t < T; t++)
                    data[b * T + t] = (float)source[t];
            return sequentialMode
                ? Tensor.From(data, new Shape(N, 1))
                : Tensor.From(data, new Shape(batchSize, T, 1));
        }

        /// <summary>Готовит тензор цели той же формы, что и выход модели.</summary>
        private static Tensor MakeBatchTarget(Vector target, int batchSize, bool sequentialMode)
        {
            int T = target.Count;
            int N = batchSize * T;
            var data = new float[N];
            for (int b = 0; b < batchSize; b++)
                for (int t = 0; t < T; t++)
                    data[b * T + t] = (float)target[t];
            return sequentialMode
                ? Tensor.From(data, new Shape(N, 1))
                : Tensor.From(data, new Shape(batchSize, T, 1));
        }

        private void Visualize()
        {
            chartVisual2.Clear();
            chartVisual2.AddPlot(_steps, _targetSeries, "Целевой сигнал (sin\u00B3)");
            chartVisual2.AddPlot(_steps, _networkOutput, "Выход сети");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            _phaseOffset += 0.1;
            _inputSeries = BuildSineSeries(_steps, _phaseOffset);

            var sw = Stopwatch.StartNew();
            _networkOutput = Forward(_inputSeries);
            sw.Stop();
            _lastInferMs = sw.ElapsedMilliseconds;

            chartVisual1.PlotBlack(_inputSeries);
            Visualize();
            UpdateTimingLabel();
        }

        /// <summary>
        /// Адаптер «recurrent -> скаляр на каждый шаг»: (B,T,1) -> recurrent -> Linear(H->1) -> (B,T,1).
        /// </summary>
        private sealed class SeqRegressor : Module
        {
            private readonly Module _recurrent;
            private readonly Linear _head;
            private readonly bool _isLstm;
            private readonly bool _isGru;
            private readonly bool _isRnn;

            public SeqRegressor(Module recurrent, int hiddenSize, Random rng)
            {
                _recurrent = RegisterModule("rnn", recurrent);
                _head = RegisterModule("head", new Linear(hiddenSize, 1, true, rng));
                _isLstm = recurrent is LSTM;
                _isGru = recurrent is GRU;
                _isRnn = recurrent is RNN;
            }

            public override Tensor Forward(Tensor input)
            {
                Tensor outputs;
                if (_isLstm) outputs = ((LSTM)_recurrent).ForwardSeq(input).outputs;
                else if (_isGru) outputs = ((GRU)_recurrent).ForwardSeq(input).outputs;
                else if (_isRnn) outputs = ((RNN)_recurrent).ForwardSeq(input).outputs;
                else outputs = _recurrent.Forward(input);
                return _head.Forward(outputs);
            }
        }

        /// <summary>
        /// Простейший трансформер: Linear up -> PositionalEncoding -> EncoderLayer -> Linear down.
        /// </summary>
        private sealed class TransformerRegressor : Module
        {
            private readonly Linear _proj;
            private readonly SinusoidalPositionalEncoding _pe;
            private readonly TransformerEncoderLayer _enc;
            private readonly Linear _head;

            public TransformerRegressor(int dModel, int nHead, int dFf, Random rng)
            {
                _proj = RegisterModule("proj", new Linear(1, dModel, true, rng));
                _pe = RegisterModule("pe", new SinusoidalPositionalEncoding(dModel, maxLen: 1024));
                _enc = RegisterModule("enc", new TransformerEncoderLayer(
                    dModel, nHead, dimFeedforward: dFf, dropout: 0f,
                    activation: "gelu", normFirst: true, rng: rng));
                _head = RegisterModule("head", new Linear(dModel, 1, true, rng));
            }

            public override Tensor Forward(Tensor input)
            {
                Tensor h = _proj.Forward(input);
                h = _pe.Forward(h);
                h = _enc.Forward(h);
                return _head.Forward(h);
            }
        }
    }
}
