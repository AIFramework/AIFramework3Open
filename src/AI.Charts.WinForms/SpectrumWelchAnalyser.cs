using AI.DataStructs.Algebraic;
using AI.DSP.Analyse;
using AI.DSP.DSPCore;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace AI.Charts.WinForms;

[Serializable]
public partial class SpectrumWelchAnalyzer : UserControl
{
    /// <summary>
    /// Тип представления СПМ (по умолчанию в дб)
    /// </summary>
    public WelchPSDType WelchPSDTypeData { get; set; } = WelchPSDType.Db;

    /// <summary>
    /// Смещение частоты
    /// </summary>
    public double FreqOffset { get; set; } = 0;

    /// <summary>
    /// Частота дискретизации
    /// </summary>
    public int SR { get; set; } = 80000;

    /// <summary>
    /// Размер блока БПФ преобразования
    /// </summary>
    public int FFTBlock { get; set; } = 1024;

    /// <summary>
    /// Веса окна
    /// </summary>
    public Vector WindowW => WindowFunc(FFTBlock);

    /// <summary>
    /// Оконная функция (По-умолчанию окно Блэкмана)
    /// </summary>
    public Func<int, Vector> WindowFunc = WindowForFFT.BlackmanWindow;

    /// <summary>
    /// Анализ спектра методом Уэлча
    /// </summary>
    public SpectrumWelchAnalyzer()
    {
        InitializeComponent();
        BackColor = Color.FromArgb(252, 252, 254);
        chartVisual1.BackColor = Color.FromArgb(252, 252, 254);
        chartVisual1.ForeColor = Color.FromArgb(71, 85, 105);
    }

    /// <summary>
    /// Спектральный анализ сигнала
    /// </summary>
    /// <param name="signal"></param>
    public (Vector, Vector) Analyze(Vector signal)
    {
        Vector fft = Welch.WelchRun(signal, FFTBlock, 0.5, WindowW) / FFTBlock;
        WelchData welchData = new WelchData(fft, SR, WelchPSDTypeData);
        chartVisual1.Clear();
        chartVisual1.AddPlot(
            welchData.HalfFreq + FreqOffset,
            welchData.HalfPSD,
            string.Empty,
            Color.FromArgb(37, 99, 235),
            2);

        return (welchData.HalfPSD, welchData.HalfFreq + FreqOffset);
    }
}
