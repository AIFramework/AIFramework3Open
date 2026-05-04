using System;
using System.Drawing;
using System.Windows.Forms;
using Matrix = AI.DataStructs.Algebraic.Matrix;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace AI.Charts.WinForms;

/// <summary>
/// Тепловая карта
/// </summary>
[Serializable]
public partial class HeatMapControl : UserControl
{
    private SKBitmap grad;
    private SKBitmap bitmapHM;
    private double min, mean, max, len600;

    /// <summary>
    /// Тепловая карта
    /// </summary>
    public HeatMapControl()
    {
        InitializeComponent();
        DoubleBuffered = true;
    }

    private void ReleaseSkiaResources()
    {
        if (gradient != null)
        {
            Image g = gradient.Image;
            gradient.Image = null;
            g?.Dispose();
        }

        if (mainPict != null)
        {
            Image m = mainPict.Image;
            mainPict.Image = null;
            m?.Dispose();
        }

        grad?.Dispose();
        grad = null;
        bitmapHM?.Dispose();
        bitmapHM = null;
    }

    private void Gradient_SizeChanged(object sender, EventArgs e)
    {

    }

    private void HeatMap_Load(object sender, EventArgs e)
    {

    }

    /// <summary>
    /// Задает градиент тепловой карты
    /// </summary>
    private void NewGrad()
    {
        grad?.Dispose();
        int gh = 600;
        int gw = Math.Max(gradient.Width, 6);
        grad = new SKBitmap(gw, gh, SKColorType.Bgra8888, SKAlphaType.Opaque);

        using (SKCanvas canvas = new SKCanvas(grad))
        using (SKPaint paint = new SKPaint())
        {
            SKColor[] colors =
            {
                new SKColor(255, 0, 0),
                new SKColor(255, 165, 0),
                new SKColor(255, 215, 0),
                new SKColor(173, 255, 47),
                new SKColor(0, 0, 255)
            };
            float[] positions = { 0, 0.25f, 0.5f, 0.75f, 1f };
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, gh),
                colors,
                positions,
                SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, gw, gh), paint);
        }

        SetPictureBoxImage(gradient, grad);
    }

    /// <summary>
    /// Получение цвета из значения
    /// </summary>
    private SKColor GetColor(double value)
    {
        if (grad == null)
        {
            return SKColors.Gray;
        }

        double position = 599 - ((value - min) / len600);
        if (position < 0)
        {
            position = 0;
        }

        if (position > 599)
        {
            position = 599;
        }

        int py = (int)Math.Round(position);
        py = Math.Clamp(py, 0, grad.Height - 1);
        int px = Math.Clamp(grad.Width / 2, 0, Math.Max(0, grad.Width - 1));
        return grad.GetPixel(px, py);
    }

    private void MainPict_SizeChanged(object sender, EventArgs e)
    {
        if (bitmapHM == null || bitmapHM.Width < 1 || bitmapHM.Height < 1)
        {
            return;
        }

        using (SKBitmap resized = ResizeSkBitmap(bitmapHM, mainPict.Width, mainPict.Height))
        {
            SetPictureBoxImage(mainPict, resized);
        }
    }

    private void DrawHeatMapPix(Matrix matrix)
    {
        bitmapHM?.Dispose();
        int w = matrix.Width;
        int h = matrix.Height;
        if (w < 1 || h < 1)
        {
            return;
        }

        bitmapHM = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);

        // Пиксель (i,j): i — по горизонтали (столбец), j — по вертикали (строка матрицы).
        for (int col = 0; col < w; col++)
        {
            for (int row = 0; row < h; row++)
            {
                bitmapHM.SetPixel(col, row, GetColor(matrix[row, col]));
            }
        }

        using (SKBitmap resized = ResizeSkBitmap(bitmapHM, mainPict.Width, mainPict.Height))
        {
            SetPictureBoxImage(mainPict, resized);
        }
    }

    private static SKBitmap ResizeSkBitmap(SKBitmap image, int width, int height)
    {
        try
        {
            if (width <= 0 || height <= 0 || image == null)
            {
                return new SKBitmap(1, 1);
            }

            SKImageInfo info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            return image.Resize(info, SKFilterQuality.None) ?? new SKBitmap(1, 1);
        }
        catch
        {
            return new SKBitmap(1, 1);
        }
    }

    private static void SetPictureBoxImage(PictureBox box, SKBitmap skBmp)
    {
        if (skBmp == null)
        {
            return;
        }

        using (Bitmap bmp = skBmp.ToBitmap())
        {
            Image old = box.Image;
            box.Image = (Bitmap)bmp.Clone();
            old?.Dispose();
        }
    }

    /// <summary>
    /// Удержание позиций меток
    /// </summary>
    private void HeatMap_SizeChanged(object sender, EventArgs e)
    {
        q25.Location = new Point(q25.Location.X, gradient.Location.Y + (int)(0.75 * gradient.Size.Height) - q25.Size.Height);
        q75.Location = new Point(q75.Location.X, gradient.Location.Y + (int)(0.25 * gradient.Size.Height) - q75.Size.Height);
        meanLabel.Location = new Point(meanLabel.Location.X, gradient.Location.Y + (int)(0.5 * gradient.Size.Height) - meanLabel.Size.Height);
    }

    /// <summary>
    /// Расчет тепловой карты для матрицы
    /// </summary>
    /// <param name="matrix">Матрица</param>
    public void CalculateHeatMap(Matrix matrix)
    {
        if (matrix == null || matrix.Width < 1 || matrix.Height < 1)
        {
            return;
        }

        min = matrix.Min();
        max = matrix.Max();
        mean = (max + min) / 2;
        double span = max - min;
        len600 = span > 1e-15 ? span / 599.0 : 1.0;
        NewGrad();
        minLabel.Text = Math.Round(min, 3).ToString();
        maxLabel.Text = Math.Round(max, 3).ToString();
        meanLabel.Text = Math.Round(mean, 3).ToString();
        q25.Text = Math.Round((mean + min) / 2, 3).ToString();
        q75.Text = Math.Round((max + mean) / 2, 3).ToString();

        DrawHeatMapPix(matrix);

        xValue.Text = matrix.Width + ",0";
        yValue.Text = "0," + matrix.Height + "";
        xyValue.Text = "" + matrix.Width + "," + matrix.Height + "";
    }

    /// <summary>
    /// Расчет тепловой карты для двумерного массива
    /// </summary>
    /// <param name="data">Массив</param>
    public void CalculateHeatMap(double[,] data)
    {
        Matrix matrix = new Matrix(data);
        CalculateHeatMap(matrix);
    }
}
