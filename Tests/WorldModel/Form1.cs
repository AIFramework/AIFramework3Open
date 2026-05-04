using AI.Charts.WinForms;
using AI.DataStructs.Algebraic;
using AI.ML.SequenceAnalysis.HMM;
using AI.ML.Utils.NeuralSymbolic;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WorldModel
{
    public partial class Form1 : Form
    {
        private HMM _hmm;
        private Vector[] _columns;
        private int _ind = 2;
        private int _ind2 = 2;

        public Form1()
        {
            InitializeComponent();
            timer1.Enabled = false;
            ApplyChartTheme(chartVisual1);
            ApplyChartTheme(chartVisual2);
            chartVisual1.ChartName = "Цепь 1: распределение по переходам";
            chartVisual2.ChartName = "Цепь 2: распределение по переходам";
            chartVisual1.LabelX = "Состояние";
            chartVisual1.LabelY = "Вероятность";
            chartVisual2.LabelX = "Состояние";
            chartVisual2.LabelY = "Вероятность";
            lblStatus.Text = "Инициализация…";
        }

        private static void ApplyChartTheme(ChartVisual chart)
        {
            chart.BackColor = Color.FromArgb(252, 252, 254);
            chart.ForeColor = Color.FromArgb(71, 85, 105);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            UseWaitCursor = true;
            btnPause.Enabled = false;
            try
            {
                (HMM hmm, Vector[] columns) = await Task.Run(WorldModelHmmDemo.Build).ConfigureAwait(true);
                _hmm = hmm;
                _columns = columns;
                lblStatus.Text = "Готово · таймер " + timer1.Interval + " мс";
                lblStatus.ForeColor = Color.FromArgb(5, 150, 105);
                timer1.Enabled = true;
                btnPause.Enabled = true;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка";
                lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
                MessageBox.Show(this, ex.Message, "WorldModel", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private void ShowHeatMap(int state, HeatMapControl heatMapControl)
        {
            bool[] bits = state.DecimalToGrayBits(9);
            Vector v = Similarity.Bools2Vect(bits);
            if (v.Count != 9)
            {
                return;
            }

            Matrix matrix = new Matrix(3, 3);
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    matrix[row, col] = v[row * 3 + col];
                }
            }

            heatMapControl.CalculateHeatMap(matrix);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_columns == null || _hmm == null)
            {
                return;
            }

            chartVisual1.BarBlack(_columns[_ind] / _columns[_ind].Sum());
            chartVisual2.BarBlack(_columns[_ind2] / _columns[_ind2].Sum());

            _ind = _hmm.Generate(2, _ind)[1];
            _ind2 = _hmm.Generate(2, _ind2)[1];

            ShowHeatMap(_ind, heatMapControl1);
            ShowHeatMap(_ind2, heatMapControl2);

            lblTick.Text = "s₁ = " + _ind + "    s₂ = " + _ind2;
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            timer1.Enabled = !timer1.Enabled;
            btnPause.Text = timer1.Enabled ? "Пауза" : "Продолжить";
        }

        private void panelHeader_Paint(object sender, PaintEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(229, 231, 235), 1f);
            int y = panelHeader.Height - 1;
            e.Graphics.DrawLine(pen, 0, y, panelHeader.Width, y);
        }
    }
}
