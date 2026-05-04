using AI.Charts.ChartElements;
using AI.Charts.Data;
using AI.Charts.Forms;
using AI.Charts.Rendering;
using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace AI.Charts.WinForms;

public partial class ChartVisual
{
    #region Контекстное меню

    // Выбор фона/стиля
    private void выборФонаToolStripMenuItem_Click(object sender, EventArgs e)
    {

        OpenFileDialog ofd = new OpenFileDialog();
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            _backgroundSkImage?.Dispose();
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(ofd.FileName);
                _backgroundSkImage = SKImage.FromEncodedData(bytes);
            }
            catch
            {
                _backgroundSkImage = null;
            }

            SKColor meanSk = DataMethods.GetColorForStyle(ofd.FileName);
            Color meanColor = Color.FromArgb(meanSk.Alpha, meanSk.Red, meanSk.Green, meanSk.Blue);
            Color inversColor = Color.FromArgb(255 - meanSk.Red, 255 - meanSk.Green, 255 - meanSk.Blue);

            BackColor = meanColor;
            ForeColor = inversColor;
            skChart.BackColor = meanColor;
            skChart.ForeColor = inversColor;
            skChart.Invalidate();
        }
    }

    // Сохранение в буфер обмена
    private void отправитьИзображениеВБуферОбменаToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Clipboard.Clear();
        Clipboard.SetImage(ChartImg());
        _ = MessageBox.Show("Изображение в буффере обмена!", "Информация");
    }

    // Сохранение в файл
    private void сохранитьToolStripMenuItem_Click(object sender, EventArgs e)
    {
        SaveFileDialog sfd = new SaveFileDialog
        {
            Filter = "(.jpg)|*.jpg"
        };

        if (DialogResult.OK == sfd.ShowDialog())
        {
            ChartImg().Save(sfd.FileName);
        }
    }

    // Масштабирование
    private void масштабToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AutoScale();
    }

    //Вывод в новом окне
    private void NewWindowOutp_Click(object sender, EventArgs e)
    {
        FormChart fChart = new FormChart
        {
            ChartName = ChartName,
            LabelX = LabelX,
            LabelY = LabelY
        };

        foreach (IChartElement item in chartElements)
        {
            fChart.AddChartElement(item);
        }

        fChart.Show();
    }

    //Спектр
    private void СпектрToolStripMenuItem_Click(object sender, EventArgs e)
    {
        FormChart fChart = new FormChart
        {
            ChartName = "Amplitude spectrum (Hamming window)",
            LabelX = (LabelX == "X-axis" || LabelX.Contains("s")) ? "Hz" : "1/" + LabelX,
            LabelY = LabelY
        };

        foreach (IChartElement item in chartElements)
        {
            fChart.AddSpectrum(item);
        }

        fChart.Show();
    }

    //Гистограмма
    private void ГистограммаToolStripMenuItem_Click(object sender, EventArgs e)
    {
        FormChart fChart = new FormChart
        {
            ChartName = "Гистограмма",
            LabelX = (LabelY == "Ось Y") ? "Значения функции" : LabelY,
            LabelY = "Вероятность попадания в интервал p(x)"
        };

        foreach (IChartElement item in chartElements)
        {
            fChart.AddHistoramm(item);
        }

        fChart.Show();
    }

    //Производная
    private void Diff_Click(object sender, EventArgs e)
    {

        FormChart fChart = new FormChart
        {
            ChartName = ChartName,
            LabelX = LabelX,
            LabelY = LabelY.Contains("[Производная]") ? LabelY : LabelY + " [Производная]"
        };

        foreach (IChartElement item in chartElements)
        {
            fChart.AddDiff(item);
        }

        fChart.Show();
    }

    //Интеграл
    private void Integ_Click(object sender, EventArgs e)
    {
        FormChart fChart = new FormChart
        {
            ChartName = ChartName,
            LabelX = LabelX,
            LabelY = LabelY.Contains("[Интеграл]") ? LabelY : LabelY + " [Интеграл]"
        };

        foreach (IChartElement item in chartElements)
        {
            fChart.AddIntegr(item);
        }

        fChart.Show();
    }

    #endregion
}
