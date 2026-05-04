using SkiaSharp;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoRegr
{
    public partial class Form1 : Form
    {
        private static readonly SKColor ColorNoisy = new SKColor(41, 98, 255);
        private static readonly SKColor ColorClean = new SKColor(0, 150, 136);
        private static readonly SKColor ColorForecast = new SKColor(183, 28, 28);

        public Form1()
        {
            InitializeComponent();
            Shown += async (_, _) => await RunExperimentAsync();
            btnRepeat.Click += async (_, _) => await RunExperimentAsync();
        }

        private async Task RunExperimentAsync()
        {
            btnRepeat.Enabled = false;
            lblStatus.Text = "Обучение AR…";
            lblStatus.ForeColor = Color.DimGray;
            Application.DoEvents();

            var random = new Random();
            AutoRegressionExperiment.Result result;

            try
            {
                result = await Task.Run(() => AutoRegressionExperiment.Run(
                    AutoRegressionExperiment.DefaultWindowSize,
                    AutoRegressionExperiment.DefaultTrainLength,
                    AutoRegressionExperiment.DefaultPredictHorizon,
                    random)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка: " + ex.Message;
                lblStatus.ForeColor = Color.Firebrick;
                btnRepeat.Enabled = true;
                return;
            }

            chartVisual1.Clear();
            chartVisual1.ChartName = "Ряд и прогноз";
            chartVisual1.LabelX = "Время (шаг)";
            chartVisual1.LabelY = "Значение";

            chartVisual1.AddPlot(result.TimeTrain, result.SeriesNoisy, "Обучающий ряд (с шумом)", ColorNoisy, 2);
            chartVisual1.AddPlot(result.TimeFull, result.SeriesClean, "Эталон без шума", ColorClean, 2);
            chartVisual1.AddPlot(result.TimeFull, result.Prediction, "Прогноз AR", ColorForecast, 2);

            lblMetrics.Text =
                $"R² (корреляция² на тестовом участке): {result.RSquared:F4}\r\n" +
                $"Время обучения: {result.TrainTimeMs} мс · окно AR: {AutoRegressionExperiment.DefaultWindowSize} · горизонт: {AutoRegressionExperiment.DefaultPredictHorizon}";

            lblStatus.Text = "Готово";
            lblStatus.ForeColor = Color.DarkGreen;
            btnRepeat.Enabled = true;
        }
    }
}
