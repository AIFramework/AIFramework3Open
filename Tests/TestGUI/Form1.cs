using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AI.ML.NeuralNetworks.V2;
using AI.ML.NeuralNetworks.V2.Losses;
using AI.ML.NeuralNetworks.V2.Nn;
using AI.ML.NeuralNetworks.V2.Optim;

namespace TestGUI
{
    /// <summary>
    /// Простая GUI-демонстрация обучения V2-сети (Sequential + Linear + ReLU)
    /// на двух парах вход/выход с логом потерь по эпохам.
    /// </summary>
    public partial class TestGui : Form
    {
        private readonly Sequential _net;
        private readonly Tensor _x;
        private readonly Tensor _y;
        private CancellationTokenSource _cts = new();

        public TestGui()
        {
            InitializeComponent();

            _net = new Sequential(
                new Linear(8, 2130),
                new ReLU(),
                new Linear(2130, 130),
                new ReLU(),
                new Linear(130, 3));

            _x = Tensor.From(new float[]
            {
                0.9f, 0.1f, 0.9f, 0.1f, 0.9f, 0.1f, 0.9f, 0.1f,
                0.1f, 0.9f, 0.1f, 0.9f, 0.1f, 0.9f, 0.1f, 0.9f
            }, new Shape(2, 8));

            _y = Tensor.From(new float[]
            {
                0.23f, -0.10f, 0.60f,
                -0.90f, 0.80f, 0.40f
            }, new Shape(2, 3));

            long total = 0;
            foreach (var (_, p) in _net.NamedParameters())
                total += p.Tensor.Shape.NumElements;

            rtbLog.Text =
                $"V2 Sequential: 8 -> 2130 (ReLU) -> 130 (ReLU) -> 3{Environment.NewLine}" +
                $"Параметров: {total:N0}{Environment.NewLine}" +
                $"Семплов: {_x.Shape[0]}, входов: {_x.Shape[1]}, выходов: {_y.Shape[1]}{Environment.NewLine}{Environment.NewLine}";
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            btnCancel.Enabled = true;

            try
            {
                if (_cts.IsCancellationRequested)
                {
                    _cts.Dispose();
                    _cts = new CancellationTokenSource();
                }

                CancellationToken ct = _cts.Token;
                await Task.Run(() => RunTraining(epochs: 30, lr: 1e-3f, ct), ct);
                AppendLog("Обучение завершено.");
            }
            catch (OperationCanceledException)
            {
                AppendLog("Обучение отменено пользователем.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка во время обучения:{Environment.NewLine}{ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnStart.Enabled = true;
                btnCancel.Enabled = false;
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            _cts.Cancel();
            btnCancel.Enabled = false;
        }

        private void btnOpenImgFilters_Click(object sender, EventArgs e)
        {
            using var fImg = new FImg();
            fImg.ShowDialog(this);
        }

        private void RunTraining(int epochs, float lr, CancellationToken ct)
        {
            var optim = new Adam(_net.Parameters(), lr: lr);
            for (int epoch = 0; epoch < epochs; epoch++)
            {
                ct.ThrowIfCancellationRequested();
                optim.ZeroGrad();
                Tensor pred = _net.Forward(_x);
                Tensor loss = RegressionLosses.MSE(pred, _y);
                loss.Backward();
                optim.Step();

                float l = loss.AsReadOnlySpan<float>()[0];
                AppendLog($"epoch {epoch + 1,3}/{epochs}: loss = {l:F6}");
            }
        }

        private void AppendLog(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => AppendLog(text)));
                return;
            }
            rtbLog.AppendText(text + Environment.NewLine);
            rtbLog.ScrollToCaret();
        }
    }
}
