using AI.DataStructs.Algebraic;
using AI.DSP.Analyse;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SpectrumAnalyzer
{
    public partial class Form1 : Form
    {
        private readonly Vector _signal;

        public Form1()
        {
            InitializeComponent();

            _signal = LfmSpectrumDemo.BuildSignal();

            spectrumWelchAnalyzer1.FFTBlock = LfmSpectrumDemo.FftBlockSize;
            spectrumWelchAnalyzer1.SR = LfmSpectrumDemo.SampleRate;
            spectrumWelchAnalyzer1.WelchPSDTypeData = WelchPSDType.Db;
            spectrumWelchAnalyzer1.FreqOffset = 0;

            UpdateParamsLabel();

            Shown += (_, _) => RunSpectrumAnalysis();
            btnAnalyze.Click += (_, _) => RunSpectrumAnalysis();
        }

        private void UpdateParamsLabel()
        {
            lblParams.Text =
                $"ЛЧМ: Δf = {LfmSpectrumDemo.FrequencySweep} Гц, f₀ = {LfmSpectrumDemo.StartFrequencyHz} Гц, " +
                $"fd = {LfmSpectrumDemo.SampleRate} Гц, T = {LfmSpectrumDemo.DurationSeconds} с · " +
                $"отсчётов: {_signal.Count} · БПФ: {LfmSpectrumDemo.FftBlockSize} · СПМ: дБ";
        }

        private void RunSpectrumAnalysis()
        {
            btnAnalyze.Enabled = false;
            UseWaitCursor = true;
            lblStatus.Text = "Спектр Уэлча…";
            lblStatus.ForeColor = Color.FromArgb(100, 116, 139);
            try
            {
                spectrumWelchAnalyzer1.Analyze(_signal);
                lblStatus.Text = "Готово";
                lblStatus.ForeColor = Color.FromArgb(5, 150, 105);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка: " + ex.Message;
                lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
                MessageBox.Show(this, ex.Message, "SpectrumAnalyzer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
                btnAnalyze.Enabled = true;
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
