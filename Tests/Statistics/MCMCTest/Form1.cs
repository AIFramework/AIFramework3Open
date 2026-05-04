using AI.DataStructs.Algebraic;
using AI.Statistics;
using AI.Statistics.MonteCarlo;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MCMCTest
{
    public partial class Form1 : Form
    {
        private readonly MCMC_1D _mcmc;

        public Form1()
        {
            InitializeComponent();
            _mcmc = new MCMC_1D(McmcDemoMath.TargetLogDensity, McmcDemoMath.McmcBurnInSteps);
            ApplyChartTheme();
            lblStatus.Text = "Готово";
        }

        private void ApplyChartTheme()
        {
            chartVisual1.BackColor = Color.FromArgb(252, 252, 254);
            chartVisual1.ForeColor = Color.FromArgb(71, 85, 105);
        }

        private async void btnHistogram_Click(object sender, EventArgs e)
        {
            await RunMcmcHistogramAsync().ConfigureAwait(true);
        }

        private async Task RunMcmcHistogramAsync()
        {
            SetBusy(true, "MCMC: выборка и гистограмма…");
            try
            {
                double min = McmcDemoMath.HistogramXMin;
                double max = McmcDemoMath.HistogramXMax;
                double step = McmcDemoMath.HistogramStep;

                Vector sample = await Task.Run(() =>
                {
                    Vector v = _mcmc.Generate(McmcDemoMath.McmcGenerateCount, min, max);
                    return v[null, null, -1];
                }).ConfigureAwait(true);

                Statistic stat = new Statistic(sample);
                var hist = stat.Histogramm(McmcDemoMath.HistogramBins);

                Vector xV = Vector.Seq(min, step, max);
                Vector prob = xV.Transform(x => Math.Exp(McmcDemoMath.TargetLogDensity(x)));
                prob /= prob.Sum() * step;

                chartVisual1.ChartName = "MCMC: гистограмма выборки и теоретическая плотность";
                chartVisual1.LabelX = "x";
                chartVisual1.LabelY = "Плотность / частота";
                chartVisual1.Clear();
                chartVisual1.AddArea(hist.X, hist.Y, "Гистограмма MCMC", Color.FromArgb(51, 65, 85));
                chartVisual1.AddPlot(xV, prob, "Плотность ∝ exp(log p)", Color.FromArgb(220, 38, 38), 2);
                lblStatus.Text = "Готово · выборка: " + McmcDemoMath.McmcGenerateCount;
                lblStatus.ForeColor = Color.FromArgb(5, 150, 105);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка";
                lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
                MessageBox.Show(this, ex.Message, "MCMCTest", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void btnIntegral_Click(object sender, EventArgs e)
        {
            SetBusy(true, "Интеграл Монте-Карло…");
            try
            {
                double min = McmcDemoMath.IntegralXMin;
                double max = McmcDemoMath.IntegralXMax;
                double step = McmcDemoMath.IntegralStep;

                Vector xV = Vector.Seq(min, step, max);
                Vector f = xV.Transform(McmcDemoMath.Integrand);

                chartVisual1.ChartName = "Интегрирование 1D (Монте-Карло)";
                chartVisual1.LabelX = "x";
                chartVisual1.LabelY = "Значение";
                chartVisual1.Clear();
                chartVisual1.AddPlot(xV, f, "Подынтегральная f(x)", Color.FromArgb(37, 99, 235), 2);
                chartVisual1.AddPlot(
                    xV,
                    xV.Transform(x => McmcDemoMath.Antiderivative(x) - McmcDemoMath.Antiderivative(min)),
                    "Накопленный интеграл F(x) − F(a)",
                    Color.FromArgb(5, 150, 105),
                    2);

                double exact = McmcDemoMath.Antiderivative(max) - McmcDemoMath.Antiderivative(min);
                double mc = Integration.CalcIntegral1D(McmcDemoMath.Integrand, min, max);
                lblStatus.Text = $"Готово · точный: {exact:F6} · Монте-Карло: {mc:F6}";
                lblStatus.ForeColor = Color.FromArgb(5, 150, 105);

                MessageBox.Show(
                    this,
                    $"Точное значение: {exact}\r\nМетод Монте-Карло: {mc}",
                    "Интеграл",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка";
                lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
                MessageBox.Show(this, ex.Message, "MCMCTest", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void SetBusy(bool busy, string statusWhenBusy)
        {
            UseWaitCursor = busy;
            btnHistogram.Enabled = !busy;
            btnIntegral.Enabled = !busy;
            if (busy && statusWhenBusy != null)
            {
                lblStatus.Text = statusWhenBusy;
                lblStatus.ForeColor = Color.FromArgb(100, 116, 139);
            }
        }

        private void panelHeader_Paint(object sender, PaintEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(229, 231, 235), 1f);
            int y = panelHeader.Height - 1;
            e.Graphics.DrawLine(pen, 0, y, panelHeader.Width, y);
        }
    }
}
