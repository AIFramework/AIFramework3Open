using AI.DataPrepaire.NLPUtils.TextGeneration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TextGenTest
{
    public partial class Form1 : Form
    {
        private readonly HMMFast _hmm = new HMMFast();
        private int _n;

        public Form1()
        {
            InitializeComponent();
            txtSeed.Text = TextGenDefaults.DefaultSeed;
            lblStatus.Text = "Готово";
            SetN();
        }

        private void numNGram_ValueChanged(object sender, EventArgs e)
        {
            SetN();
        }

        private void SetN()
        {
            _n = (int)numNGram.Value;
        }

        private async void btnTrain_Click(object sender, EventArgs e)
        {
            SetN();
            string corpus = richTextBox1.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(corpus))
            {
                MessageBox.Show(this, "Введите опорный текст для обучения.", "TextGenTest", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(true, "Обучение модели…");
            try
            {
                _hmm.NGram = _n;
                long ms = await Task.Run(() =>
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    _hmm.Train(corpus, true);
                    sw.Stop();
                    return sw.ElapsedMilliseconds;
                }).ConfigureAwait(true);

                lblStatus.Text = "Обучение завершено · " + ms + " мс · n = " + _n;
                lblStatus.ForeColor = Color.FromArgb(5, 150, 105);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка обучения";
                lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
                MessageBox.Show(this, ex.Message, "TextGenTest", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void btnGenerate_Click(object sender, EventArgs e)
        {
            SetN();
            _hmm.NGram = _n;

            string seed = txtSeed.Text ?? string.Empty;
            string[] strArr = seed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int padCount = _n - strArr.Length - 1;
            if (padCount < 0)
            {
                padCount = 0;
            }

            List<string> inp = new List<string>();
            for (int i = 0; i < padCount; i++)
            {
                inp.Add("<s>");
            }

            inp.AddRange(strArr);

            SetBusy(true, "Генерация текста…");
            try
            {
                string prefix = seed.TrimEnd();
                if (prefix.Length > 0)
                {
                    prefix += " ";
                }

                string generated = await Task.Run(() => _hmm.Generate(TextGenDefaults.GenerationMaxTokens, inp.ToArray())).ConfigureAwait(true);

                richTextBox2.Text = prefix + generated;
                lblStatus.Text = "Готово · токенов: " + TextGenDefaults.GenerationMaxTokens;
                lblStatus.ForeColor = Color.FromArgb(5, 150, 105);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка генерации";
                lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
                MessageBox.Show(this, ex.Message, "TextGenTest", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy, string statusWhenBusy = null)
        {
            UseWaitCursor = busy;
            btnTrain.Enabled = !busy;
            btnGenerate.Enabled = !busy;
            numNGram.Enabled = !busy;
            richTextBox1.ReadOnly = busy;
            txtSeed.ReadOnly = busy;
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
